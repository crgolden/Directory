namespace Directory.Tests.Unit.Api;

using System.Data;
using System.Globalization;
using System.Text.Json;
using Azure.Messaging.ServiceBus;
using Directory.Church;
using Directory.Entities;
using Directory.Enums;
using Directory.Messaging;
using Directory.Moderation;
using Directory.Tests.Unit.TestSupport;
using Microsoft.Extensions.Azure;
using Moq;

[Trait("Category", "Unit")]
public sealed class ModerationServiceTests
{
    private const int NoRowsUpdated = 0;

    private const int OneRowUpdated = 1;

    [Fact]
    public async Task ReviewCorrectionAsync_ReturnsFalse_WhenNoRowsUpdated()
    {
        // Arrange
        var correctionId = Guid.NewGuid();
        var reviewedBy = Generated.NewUserId();
        var conn = new FakeDbConnection();
        conn.Enqueue(FakeDbCommand.WithNonQueryResult(NoRowsUpdated));
        var service = Create(conn);

        // Act
        var result = await service.ReviewCorrectionAsync(
            correctionId,
            CorrectionStatus.Approved,
            reviewedBy,
            TestContext.Current.CancellationToken);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public async Task ReviewCorrectionAsync_ReturnsTrue_WhenRowUpdated()
    {
        // Arrange
        var correctionId = Guid.NewGuid();
        var reviewedBy = Generated.NewUserId();
        var conn = new FakeDbConnection();
        conn.Enqueue(FakeDbCommand.WithNonQueryResult(OneRowUpdated));
        var service = Create(conn);

        // Act
        var result = await service.ReviewCorrectionAsync(
            correctionId,
            CorrectionStatus.Approved,
            reviewedBy,
            TestContext.Current.CancellationToken);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public async Task ReviewCorrectionAsync_StoresTheReviewersSubjectAsAGuid()
    {
        // Arrange
        var correctionId = Guid.NewGuid();
        var reviewedBy = Generated.NewUserId();
        var conn = new FakeDbConnection();
        var update = FakeDbCommand.WithNonQueryResult(OneRowUpdated);
        conn.Enqueue(update);
        var service = Create(conn);

        // Act
        await service.ReviewCorrectionAsync(
            correctionId,
            CorrectionStatus.Rejected,
            reviewedBy,
            TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(reviewedBy, update.Parameters[SqlParameters.ReviewedBy].Value);
    }

    [Fact]
    public async Task SubmitCorrectionAsync_SendsTheSubmittersSubjectAsAGuid()
    {
        // Arrange
        var submittingUserId = Generated.NewUserId();
        var churchId = Guid.NewGuid();
        var sent = new List<ServiceBusMessage>();
        var senderMock = new Mock<ServiceBusSender>(MockBehavior.Strict);
        senderMock
            .Setup(s => s.SendMessageAsync(It.IsAny<ServiceBusMessage>(), It.IsAny<CancellationToken>()))
            .Callback<ServiceBusMessage, CancellationToken>((message, _) => sent.Add(message))
            .Returns(Task.CompletedTask);
        var service = Create(new FakeDbConnection(), senderMock);

        // Act
        await service.SubmitCorrectionAsync(
            churchId,
            submittingUserId,
            Generated.NewFieldName(),
            null,
            Generated.NewPhoneNumber(),
            TestContext.Current.CancellationToken);

        // Assert
        var body = Assert.Single(sent).Body.ToObjectFromJson<JsonElement>();
        Assert.Equal(submittingUserId, body.GetProperty(nameof(UserCorrection.UserId)).GetGuid());
    }

    [Fact]
    public async Task SubmitCorrectionAsync_EnqueuesMessageAndReturnsId()
    {
        // Arrange
        var churchId = Guid.NewGuid();
        var submittingUserId = Generated.NewUserId();
        var correctedField = Generated.NewFieldName();
        var proposedPhoneNumber = Generated.NewPhoneNumber();
        var senderMock = new Mock<ServiceBusSender>(MockBehavior.Strict);
        senderMock
            .Setup(s => s.SendMessageAsync(It.IsAny<ServiceBusMessage>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        var service = Create(new FakeDbConnection(), senderMock);

        // Act
        var id = await service.SubmitCorrectionAsync(
            churchId,
            submittingUserId,
            correctedField,
            null,
            proposedPhoneNumber,
            TestContext.Current.CancellationToken);

        // Assert
        Assert.NotEqual(Guid.Empty, id);
        senderMock.Verify(
            s => s.SendMessageAsync(It.Is<ServiceBusMessage>(m => m.MessageId == id.ToString()), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task GetCorrectionByIdAsync_ReturnsNull_WhenNoRows()
    {
        // Arrange
        var correctionId = Guid.NewGuid();
        var conn = new FakeDbConnection();
        conn.Enqueue(FakeDbCommand.WithReader(new DataTable()));
        var service = Create(conn);

        // Act
        var result = await service.GetCorrectionByIdAsync(
            correctionId, TestContext.Current.CancellationToken);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task MergeAsync_CommitsTransaction()
    {
        // Arrange
        var survivingChurchId = Guid.NewGuid();
        var absorbedChurchId = Guid.NewGuid();
        var mergedBy = Generated.NewUserId();
        var conn = new FakeDbConnection();
        conn.Enqueue(SurvivingChurchExists());
        conn.Enqueue(AbsorbedChurchExists());
        EnqueueSuccessfulMergeWrites(conn);

        var service = Create(conn);

        // Act
        await service.MergeAsync(
            survivingChurchId,
            absorbedChurchId,
            mergedBy,
            TestContext.Current.CancellationToken);

        // Assert
        Assert.True(conn.LastTransaction?.Committed);
    }

    [Fact]
    public async Task MergeAsync_WhenCommandThrows_RollsBackAndRethrows()
    {
        // Arrange
        var survivingChurchId = Guid.NewGuid();
        var absorbedChurchId = Guid.NewGuid();
        var mergedBy = Generated.NewUserId();
        var repointFailureMessage = Generated.NewFailureMessage();
        var conn = new FakeDbConnection();
        conn.Enqueue(SurvivingChurchExists());
        conn.Enqueue(AbsorbedChurchExists());
        conn.Enqueue(FakeDbCommand.WithException(new InvalidOperationException(repointFailureMessage)));
        var service = Create(conn);

        // Act
        var exception = await Record.ExceptionAsync(() =>
            service.MergeAsync(survivingChurchId, absorbedChurchId, mergedBy, TestContext.Current.CancellationToken));

        // Assert
        var ex = Assert.IsType<InvalidOperationException>(exception);
        Assert.Equal(repointFailureMessage, ex.Message);
        Assert.True(conn.LastTransaction?.RolledBack);
    }

    [Fact]
    public async Task MergeAsync_SameSurvivingAndAbsorbedId_ThrowsWithoutTouchingDb()
    {
        // Arrange
        var conn = new FakeDbConnection();
        var survivingChurchId = Guid.NewGuid();
        var absorbedId = survivingChurchId;
        var mergedBy = Generated.NewUserId();
        var service = Create(conn);

        // Act
        var exception = await Record.ExceptionAsync(() =>
            service.MergeAsync(survivingChurchId, absorbedId, mergedBy, TestContext.Current.CancellationToken));

        // Assert
        var ex = Assert.IsType<ArgumentException>(exception);
        Assert.Equal(nameof(absorbedId), ex.ParamName);
        Assert.Empty(conn.ExecutedCommands);
    }

    [Fact]
    public async Task MergeAsync_SurvivingChurchNotActive_ThrowsAndNeverStartsTransaction()
    {
        // Arrange
        var survivingChurchId = Guid.NewGuid();
        var absorbedChurchId = Guid.NewGuid();
        var mergedBy = Generated.NewUserId();
        var conn = new FakeDbConnection();
        conn.Enqueue(ChurchDoesNotExist());
        var service = Create(conn);

        // Act
        var exception = await Record.ExceptionAsync(() =>
            service.MergeAsync(survivingChurchId, absorbedChurchId, mergedBy, TestContext.Current.CancellationToken));

        // Assert
        Assert.IsType<InvalidOperationException>(exception);
        Assert.Null(conn.LastTransaction);
    }

    [Fact]
    public async Task MergeAsync_AbsorbedChurchNotActive_ThrowsAndNeverStartsTransaction()
    {
        // Arrange
        var survivingChurchId = Guid.NewGuid();
        var absorbedChurchId = Guid.NewGuid();
        var mergedBy = Generated.NewUserId();
        var conn = new FakeDbConnection();
        conn.Enqueue(SurvivingChurchExists());
        conn.Enqueue(ChurchDoesNotExist());
        var service = Create(conn);

        // Act
        var exception = await Record.ExceptionAsync(() =>
            service.MergeAsync(survivingChurchId, absorbedChurchId, mergedBy, TestContext.Current.CancellationToken));

        // Assert
        Assert.IsType<InvalidOperationException>(exception);
        Assert.Null(conn.LastTransaction);
    }

    [Fact]
    public async Task GetCorrectionsAsync_WithStatusFilter_AddsWhereClauseAndMapsRow()
    {
        // Arrange
        var expectedNewValue = Generated.NewStreet();
        var expectedTotalCount = Generated.NewRowCount();
        var requestedPage = Generated.NewPage();
        var requestedPageSize = Generated.NewPageSize();
        var table = BuildCorrectionTable(includeTotalCount: true);
        table.Rows.Add(CorrectionRowPopulated(newValue: expectedNewValue, totalCount: expectedTotalCount));
        var conn = new FakeDbConnection();
        var cmd = FakeDbCommand.WithReader(table);
        conn.Enqueue(cmd);
        var service = Create(conn);

        // Act
        var (items, totalCount) = await service.GetCorrectionsAsync(
            CorrectionStatus.Pending, requestedPage, requestedPageSize, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal((int)CorrectionStatus.Pending, cmd.Parameters[SqlParameters.Status].Value);
        Assert.Equal(expectedTotalCount, totalCount);
        Assert.Equal(expectedNewValue, Assert.Single(items).NewValue);
    }

    [Fact]
    public async Task GetCorrectionsAsync_WithoutStatusFilter_PassesDbNullStatus()
    {
        // Arrange
        var requestedPage = Generated.NewPage();
        var requestedPageSize = Generated.NewPageSize();
        var conn = new FakeDbConnection();
        var cmd = FakeDbCommand.WithReader(BuildCorrectionTable(includeTotalCount: true));
        conn.Enqueue(cmd);
        var service = Create(conn);

        // Act
        var (items, totalCount) = await service.GetCorrectionsAsync(
            null, requestedPage, requestedPageSize, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(DBNull.Value, cmd.Parameters[SqlParameters.Status].Value);
        Assert.Empty(items);
        Assert.Equal(0, totalCount);
    }

    [Fact]
    public async Task GetCorrectionByIdAsync_SystemAuthoredRowWithNullableNulls_MapsNulls()
    {
        // Arrange
        var correctionId = Guid.NewGuid();
        var table = BuildCorrectionTable(includeTotalCount: false);
        table.Rows.Add(CorrectionRowNullable());
        var conn = new FakeDbConnection();
        conn.Enqueue(FakeDbCommand.WithReader(table));
        var service = Create(conn);

        // Act
        var result = await service.GetCorrectionByIdAsync(correctionId, TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(result);
        Assert.Null(result.UserId);
        Assert.Null(result.OldValue);
        Assert.Null(result.ReviewedBy);
        Assert.Null(result.ReviewedAt);
        Assert.Null(result.ChurchName);
    }

    [Fact]
    public async Task GetCorrectionByIdAsync_RowPopulated_MapsAllColumns()
    {
        // Arrange
        var correctionId = Guid.NewGuid();
        var expectedOldValue = Generated.NewStreet();
        var expectedUserId = Generated.NewUserId();
        var expectedReviewedBy = Generated.NewUserId();
        var expectedChurchName = Generated.NewName();
        var table = BuildCorrectionTable(includeTotalCount: false);
        table.Rows.Add(CorrectionRowPopulated(
            oldValue: expectedOldValue,
            userId: expectedUserId,
            reviewedBy: expectedReviewedBy,
            churchName: expectedChurchName,
            totalCount: null));
        var conn = new FakeDbConnection();
        conn.Enqueue(FakeDbCommand.WithReader(table));
        var service = Create(conn);

        // Act
        var result = await service.GetCorrectionByIdAsync(correctionId, TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(expectedOldValue, result.OldValue);
        Assert.Equal(expectedUserId, result.UserId);
        Assert.Equal(expectedReviewedBy, result.ReviewedBy);
        Assert.NotNull(result.ReviewedAt);
        Assert.Equal(expectedChurchName, result.ChurchName);
    }

    [Fact]
    public async Task GetCorrectionsAsync_PageAfterTheFirst_BindsTheRowsBeforeItAsTheOffset()
    {
        // Arrange
        var pageAfterTheFirst = Generated.NewPage() + 1;
        var pageSizeAboveAnyPage = Generated.NewCountCeiling();
        var expectedOffset = (pageAfterTheFirst - 1) * pageSizeAboveAnyPage;
        var conn = new FakeDbConnection();
        var cmd = FakeDbCommand.WithReader(BuildCorrectionTable(includeTotalCount: true));
        conn.Enqueue(cmd);
        var service = Create(conn);

        // Act
        await service.GetCorrectionsAsync(
            null, pageAfterTheFirst, pageSizeAboveAnyPage, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(expectedOffset, cmd.Parameters[SqlParameters.Offset].Value);
        Assert.Equal(pageSizeAboveAnyPage, cmd.Parameters[SqlParameters.PageSize].Value);
    }

    [Fact]
    public async Task GetCorrectionByIdAsync_RowWithTargetAndSlugs_MapsThem()
    {
        // Arrange
        var correctionId = Guid.NewGuid();
        var expectedTargetChurchName = Generated.NewName();
        var expectedChurchSlug = Generated.NewSlug();
        var expectedTargetChurchSlug = Generated.NewSlug();
        var table = BuildCorrectionTable(includeTotalCount: false);
        table.Rows.Add(CorrectionRowPopulated(
            totalCount: null,
            links: new CorrectionLinks(expectedTargetChurchName, expectedChurchSlug, expectedTargetChurchSlug)));
        var conn = new FakeDbConnection();
        conn.Enqueue(FakeDbCommand.WithReader(table));
        var service = Create(conn);

        // Act
        var result = await service.GetCorrectionByIdAsync(correctionId, TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(expectedTargetChurchName, result.TargetChurchName);
        Assert.Equal(expectedChurchSlug, result.ChurchSlug);
        Assert.Equal(expectedTargetChurchSlug, result.TargetChurchSlug);
    }

    [Fact]
    public async Task MergeAsync_BothChurchesActive_ExecutesEveryCommandItCreates()
    {
        // Arrange
        var survivingChurchId = Guid.NewGuid();
        var absorbedChurchId = Guid.NewGuid();
        var mergedBy = Generated.NewUserId();
        var conn = new FakeDbConnection();
        conn.Enqueue(SurvivingChurchExists());
        conn.Enqueue(AbsorbedChurchExists());
        EnqueueSuccessfulMergeWrites(conn);
        var service = Create(conn);

        // Act
        await service.MergeAsync(survivingChurchId, absorbedChurchId, mergedBy, TestContext.Current.CancellationToken);

        // Assert
        Assert.All(conn.ExecutedCommands, command => Assert.True(command.Executed));
    }

    [Fact]
    public async Task MergeAsync_BothChurchesActive_WritesMergeWriteCountStatementsInsideTheTransaction()
    {
        // Arrange
        var survivingChurchId = Guid.NewGuid();
        var absorbedChurchId = Guid.NewGuid();
        var mergedBy = Generated.NewUserId();
        var conn = new FakeDbConnection();
        conn.Enqueue(SurvivingChurchExists());
        conn.Enqueue(AbsorbedChurchExists());
        EnqueueSuccessfulMergeWrites(conn);
        var service = Create(conn);

        // Act
        await service.MergeAsync(survivingChurchId, absorbedChurchId, mergedBy, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(
            ModerationService.MergeWriteCount,
            conn.ExecutedCommands.Count(command => command.Transaction is not null));
    }

    [Fact]
    public async Task MergeAsync_BothChurchesActive_RepointsEveryChildTableToTheSurvivor()
    {
        // Arrange
        var survivingChurchId = Guid.NewGuid();
        var absorbedChurchId = Guid.NewGuid();
        var mergedBy = Generated.NewUserId();
        var conn = new FakeDbConnection();
        conn.Enqueue(SurvivingChurchExists());
        conn.Enqueue(AbsorbedChurchExists());
        EnqueueSuccessfulMergeWrites(conn);
        var service = Create(conn);

        // Act
        await service.MergeAsync(survivingChurchId, absorbedChurchId, mergedBy, TestContext.Current.CancellationToken);

        // Assert
        var inTransaction = conn.ExecutedCommands.Where(command => command.Transaction is not null).Select(command => command.CommandText).ToList();
        var repointStatements = ModerationService.MergeRepointStatements();
        Assert.NotEmpty(repointStatements);
        Assert.All(repointStatements, statement => Assert.Contains(statement, inTransaction));
    }

    [Fact]
    public async Task MergeAsync_SameSurvivingAndAbsorbedId_ExplainsTheRefusal()
    {
        // Arrange
        var conn = new FakeDbConnection();
        var survivingChurchId = Guid.NewGuid();
        var mergedBy = Generated.NewUserId();
        var service = Create(conn);

        // Act
        var exception = await Record.ExceptionAsync(() =>
            service.MergeAsync(survivingChurchId, survivingChurchId, mergedBy, TestContext.Current.CancellationToken));

        // Assert
        var ex = Assert.IsType<ArgumentException>(exception);
        Assert.Contains(ModerationService.SelfMergeRefusal, ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task MergeAsync_SurvivingChurchNotActive_NamesTheSurvivingChurch()
    {
        // Arrange
        var survivingChurchId = Guid.NewGuid();
        var absorbedChurchId = Guid.NewGuid();
        var mergedBy = Generated.NewUserId();
        var conn = new FakeDbConnection();
        conn.Enqueue(ChurchDoesNotExist());
        var service = Create(conn);

        // Act
        var exception = await Record.ExceptionAsync(() =>
            service.MergeAsync(survivingChurchId, absorbedChurchId, mergedBy, TestContext.Current.CancellationToken));

        // Assert
        var ex = Assert.IsType<InvalidOperationException>(exception);
        Assert.Contains(survivingChurchId.ToString(), ex.Message, StringComparison.Ordinal);
        Assert.Single(conn.ExecutedCommands);
    }

    [Fact]
    public async Task MergeAsync_AbsorbedChurchNotActive_NamesTheAbsorbedChurch()
    {
        // Arrange
        var survivingChurchId = Guid.NewGuid();
        var absorbedChurchId = Guid.NewGuid();
        var mergedBy = Generated.NewUserId();
        var conn = new FakeDbConnection();
        conn.Enqueue(SurvivingChurchExists());
        conn.Enqueue(ChurchDoesNotExist());
        var service = Create(conn);

        // Act
        var exception = await Record.ExceptionAsync(() =>
            service.MergeAsync(survivingChurchId, absorbedChurchId, mergedBy, TestContext.Current.CancellationToken));

        // Assert
        var ex = Assert.IsType<InvalidOperationException>(exception);
        Assert.Contains(absorbedChurchId.ToString(), ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ApplyCorrectionAsync_FieldOutsideTheCorrectableSet_NamesTheField()
    {
        // Arrange
        var field = Generated.NewFieldName();
        var correction = CorrectionFor(field, Generated.NewName());
        var conn = new FakeDbConnection();
        var service = Create(conn);

        // Act
        var refusal = await service.ApplyCorrectionAsync(correction, Generated.NewUserId(), null, TestContext.Current.CancellationToken);

        // Assert
        Assert.Contains(field, refusal, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ApplyCorrectionAsync_FlagCorrectionThatIsNotABoolean_NamesTheValueAndWritesNothing()
    {
        // Arrange
        var notABoolean = Generated.NewName();
        var correction = CorrectionFor(nameof(Church.HasNursery), notABoolean);
        var conn = new FakeDbConnection();
        conn.Enqueue(FakeDbCommand.WithReader(ChurchRows.ActiveChurch(correction.ChurchId)));
        var service = Create(conn);

        // Act
        var refusal = await service.ApplyCorrectionAsync(correction, Generated.NewUserId(), null, TestContext.Current.CancellationToken);

        // Assert
        Assert.Contains(notABoolean, refusal, StringComparison.Ordinal);
        Assert.Single(conn.ExecutedCommands);
    }

    [Fact]
    public async Task ApplyCorrectionAsync_CorrectionTheChurchCannotBeSavedWith_NamesTheRejectedParameter()
    {
        // Arrange
        var canonicalName = Generated.NewBlank();
        var correction = CorrectionFor(nameof(Church.CanonicalName), canonicalName);
        var conn = new FakeDbConnection();
        conn.Enqueue(FakeDbCommand.WithReader(ChurchRows.ActiveChurch(correction.ChurchId)));
        var service = Create(conn);

        // Act
        var refusal = await service.ApplyCorrectionAsync(correction, Generated.NewUserId(), null, TestContext.Current.CancellationToken);

        // Assert
        Assert.Contains(nameof(canonicalName), refusal, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ApplyCorrectionAsync_StateCorrectionToAUspsCode_SavesIt()
    {
        // Arrange
        var correction = CorrectionFor(nameof(Church.State), Generated.NewStateCodeText());
        var conn = new FakeDbConnection();
        conn.Enqueue(FakeDbCommand.WithReader(ChurchRows.ActiveChurch(correction.ChurchId)));
        conn.Enqueue(FakeDbCommand.WithNonQueryResult(OneRowUpdated));
        var service = Create(conn);
        var reviewedBy = Generated.NewUserId();

        // Act
        var refusal = await service.ApplyCorrectionAsync(correction, reviewedBy, null, TestContext.Current.CancellationToken);

        // Assert
        Assert.Null(refusal);
    }

    [Fact]
    public async Task ApplyCorrectionAsync_StateCorrectionThatIsNotAUspsCode_NamesTheValueAndWritesNothing()
    {
        // Arrange
        var notAState = Generated.NewUnparseableStateCode();
        var correction = CorrectionFor(nameof(Church.State), notAState);
        var conn = new FakeDbConnection();
        conn.Enqueue(FakeDbCommand.WithReader(ChurchRows.ActiveChurch(correction.ChurchId)));
        var service = Create(conn);
        var reviewedBy = Generated.NewUserId();

        // Act
        var refusal = await service.ApplyCorrectionAsync(correction, reviewedBy, null, TestContext.Current.CancellationToken);

        // Assert
        Assert.Contains(notAState, refusal, StringComparison.Ordinal);
        Assert.Single(conn.ExecutedCommands);
    }

    [Fact]
    public async Task ApplyCorrectionAsync_WorshipStyleCorrectionToADefinedStyle_SavesIt()
    {
        // Arrange
        var style = Generated.NewDefinedValue<WorshipStyle>();
        var correction = CorrectionFor(nameof(Church.WorshipStyle), style.ToString());
        var conn = new FakeDbConnection();
        conn.Enqueue(FakeDbCommand.WithReader(ChurchRows.ActiveChurch(correction.ChurchId)));
        conn.Enqueue(FakeDbCommand.WithNonQueryResult(OneRowUpdated));
        var service = Create(conn);
        var reviewedBy = Generated.NewUserId();

        // Act
        var refusal = await service.ApplyCorrectionAsync(correction, reviewedBy, null, TestContext.Current.CancellationToken);

        // Assert
        Assert.Null(refusal);
    }

    [Fact]
    public async Task ApplyCorrectionAsync_WorshipStyleCorrectionToAnUndefinedNumber_NamesTheValueAndWritesNothing()
    {
        // Arrange
        var undefinedStyle = ((int)Generated.NewUndefinedValue<WorshipStyle>()).ToString(CultureInfo.InvariantCulture);
        var correction = CorrectionFor(nameof(Church.WorshipStyle), undefinedStyle);
        var conn = new FakeDbConnection();
        conn.Enqueue(FakeDbCommand.WithReader(ChurchRows.ActiveChurch(correction.ChurchId)));
        var service = Create(conn);
        var reviewedBy = Generated.NewUserId();

        // Act
        var refusal = await service.ApplyCorrectionAsync(correction, reviewedBy, null, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(ModerationService.UnacceptedValue(correction), refusal);
        Assert.Contains(undefinedStyle, refusal, StringComparison.Ordinal);
        Assert.Single(conn.ExecutedCommands);
    }

    [Fact]
    public async Task ApplyCorrectionAsync_DenominationCorrectionToAnId_SavesIt()
    {
        // Arrange
        var denominationId = Guid.NewGuid();
        var correction = CorrectionFor(nameof(Church.DenominationId), denominationId.ToString());
        var conn = new FakeDbConnection();
        conn.Enqueue(FakeDbCommand.WithReader(ChurchRows.ActiveChurch(correction.ChurchId)));
        conn.Enqueue(FakeDbCommand.WithNonQueryResult(OneRowUpdated));
        var service = Create(conn);
        var reviewedBy = Generated.NewUserId();

        // Act
        var refusal = await service.ApplyCorrectionAsync(correction, reviewedBy, null, TestContext.Current.CancellationToken);

        // Assert
        Assert.Null(refusal);
    }

    [Fact]
    public async Task ApplyCorrectionAsync_MergeSuggestionWithoutAChurchId_RefusesForTheMissingTarget()
    {
        // Arrange
        var correction = CorrectionFor(ModerationService.MergeField, Generated.NewName());
        var conn = new FakeDbConnection();
        var service = Create(conn);

        // Act
        var refusal = await service.ApplyCorrectionAsync(correction, Generated.NewUserId(), null, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(ModerationService.MergeTargetMissing, refusal);
        Assert.Empty(conn.ExecutedCommands);
    }

    [Fact]
    public async Task ApplyCorrectionAsync_MergeWithNoSurvivorChosen_AsksForTheSurvivor()
    {
        // Arrange
        var correction = CorrectionFor(ModerationService.MergeField, Generated.NewChurchId().ToString());
        var conn = new FakeDbConnection();
        var service = Create(conn);

        // Act
        var refusal = await service.ApplyCorrectionAsync(correction, Generated.NewUserId(), null, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(ModerationService.MergeSurvivorMissing, refusal);
    }

    [Fact]
    public async Task ApplyCorrectionAsync_MergeSurvivorOutsideThePair_RefusesTheSurvivor()
    {
        // Arrange
        var correction = CorrectionFor(ModerationService.MergeField, Generated.NewChurchId().ToString());
        var unrelatedChurchId = Generated.NewChurchId();
        var conn = new FakeDbConnection();
        var service = Create(conn);

        // Act
        var refusal = await service.ApplyCorrectionAsync(correction, Generated.NewUserId(), unrelatedChurchId, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(ModerationService.MergeSurvivorOutsideThePair, refusal);
    }

    [Fact]
    public async Task ApplyCorrectionAsync_MergeKeepingTheCorrectedChurch_AbsorbsTheSuggestedChurch()
    {
        // Arrange
        var suggestedId = Generated.NewChurchId();
        var correction = CorrectionFor(ModerationService.MergeField, suggestedId.ToString());
        var conn = new FakeDbConnection();
        conn.Enqueue(SurvivingChurchExists());
        conn.Enqueue(AbsorbedChurchExists());
        var service = Create(conn);

        // Act
        var refusal = await service.ApplyCorrectionAsync(correction, Generated.NewUserId(), correction.ChurchId, TestContext.Current.CancellationToken);

        // Assert
        Assert.Null(refusal);
        var absorbedParams = conn.ExecutedCommands
            .Where(command => command.Parameters.Contains(SqlParameters.Absorbed))
            .Select(command => command.Parameters[SqlParameters.Absorbed].Value)
            .ToList();
        Assert.NotEmpty(absorbedParams);
        Assert.All(absorbedParams, absorbed => Assert.Equal(suggestedId, absorbed));
    }

    [Fact]
    public async Task ApplyCorrectionAsync_MergeApplied_ExecutesTheSuggestionClosure()
    {
        // Arrange
        var suggestedId = Generated.NewChurchId();
        var correction = CorrectionFor(ModerationService.MergeField, suggestedId.ToString());
        var conn = new FakeDbConnection();
        conn.Enqueue(SurvivingChurchExists());
        conn.Enqueue(AbsorbedChurchExists());
        var service = Create(conn);

        // Act
        await service.ApplyCorrectionAsync(correction, Generated.NewUserId(), suggestedId, TestContext.Current.CancellationToken);

        // Assert
        var closure = conn.ExecutedCommands[^1];
        Assert.Equal((int)CorrectionStatus.Rejected, closure.Parameters[SqlParameters.Rejected].Value);
        Assert.Equal(correction.ChurchId, closure.Parameters[SqlParameters.Absorbed].Value);
        Assert.True(closure.Executed);
    }

    private static UserCorrection CorrectionFor(string field, string newValue) => new UserCorrection
    {
        ChurchId = Generated.NewChurchId(),
        Field = field,
        NewValue = newValue,
    };

    private static FakeDbCommand SurvivingChurchExists() => FakeDbCommand.WithScalarResult(1);

    private static FakeDbCommand AbsorbedChurchExists() => FakeDbCommand.WithScalarResult(1);

    private static FakeDbCommand ChurchDoesNotExist() => FakeDbCommand.WithScalarResult(0);

    private static void EnqueueSuccessfulMergeWrites(FakeDbConnection conn)
    {
        for (var i = 0; i < ModerationService.MergeWriteCount; i++)
        {
            conn.Enqueue(FakeDbCommand.WithNonQueryResult(OneRowUpdated));
        }
    }

    private static ModerationService Create(FakeDbConnection conn, Mock<ServiceBusSender>? senderMock = null)
    {
        var clientMock = new Mock<ServiceBusClient>(MockBehavior.Loose);
        clientMock.Setup(c => c.CreateSender(ServiceBusNames.Contributions))
                  .Returns(senderMock?.Object ?? new Mock<ServiceBusSender>().Object);
        var factory = new Mock<IAzureClientFactory<ServiceBusClient>>(MockBehavior.Loose);
        factory.Setup(f => f.CreateClient(ServiceBusNames.Client))
               .Returns(clientMock.Object);
        return new ModerationService(conn, factory.Object, new ChurchService(conn));
    }

    private static DataTable BuildCorrectionTable(bool includeTotalCount)
    {
        var t = new DataTable();
        t.Columns.Add(nameof(UserCorrection.Id), typeof(Guid));
        t.Columns.Add(nameof(UserCorrection.ChurchId), typeof(Guid));
        t.Columns.Add(nameof(UserCorrection.UserId), typeof(Guid));
        t.Columns.Add(nameof(UserCorrection.Field), typeof(string));
        t.Columns.Add(nameof(UserCorrection.OldValue), typeof(string));
        t.Columns.Add(nameof(UserCorrection.NewValue), typeof(string));
        t.Columns.Add(nameof(UserCorrection.Status), typeof(int));
        t.Columns.Add(nameof(UserCorrection.ReviewedBy), typeof(Guid));
        t.Columns.Add(nameof(UserCorrection.ReviewedAt), typeof(DateTimeOffset));
        t.Columns.Add(nameof(UserCorrection.CreatedAt), typeof(DateTimeOffset));
        t.Columns.Add(nameof(UserCorrection.ChurchName), typeof(string));
        t.Columns.Add(nameof(UserCorrection.TargetChurchName), typeof(string));
        t.Columns.Add(nameof(UserCorrection.ChurchSlug), typeof(string));
        t.Columns.Add(nameof(UserCorrection.TargetChurchSlug), typeof(string));
        if (includeTotalCount)
        {
            t.Columns.Add(nameof(PagedResult<UserCorrection>.TotalCount), typeof(int));
        }

        return t;
    }

    private static object[] CorrectionRowPopulated(
        int? totalCount,
        string? oldValue = null,
        string? newValue = null,
        Guid? userId = null,
        Guid? reviewedBy = null,
        string? churchName = null,
        CorrectionLinks? links = null)
    {
        // Arrange
        var correctionId = Guid.NewGuid();
        var churchId = Guid.NewGuid();
        var values = new List<object>
        {
            correctionId,
            churchId,
            userId ?? Generated.NewUserId(),
            Generated.NewFieldName(),
            oldValue ?? Generated.NewStreet(),
            newValue ?? Generated.NewStreet(),
            (int)CorrectionStatus.Approved,
            reviewedBy ?? Generated.NewUserId(),
            Generated.NewUtcTimestamp(),
            Generated.NewUtcTimestamp(),
            churchName ?? Generated.NewName(),
            (object?)links?.TargetChurchName ?? DBNull.Value,
            (object?)links?.ChurchSlug ?? DBNull.Value,
            (object?)links?.TargetChurchSlug ?? DBNull.Value,
        };
        if (totalCount.HasValue)
        {
            values.Add(totalCount.Value);
        }

        return [.. values];
    }

    private static object[] CorrectionRowNullable()
    {
        // Arrange
        var correctionId = Guid.NewGuid();
        var churchId = Guid.NewGuid();
        return
        [
            correctionId,
            churchId,
            DBNull.Value,
            Generated.NewFieldName(),
            DBNull.Value,
            Generated.NewStreet(),
            (int)CorrectionStatus.Pending,
            DBNull.Value,
            DBNull.Value,
            Generated.NewUtcTimestamp(),
            DBNull.Value,
            DBNull.Value,
            DBNull.Value,
            DBNull.Value,
        ];
    }

    private sealed record CorrectionLinks(string TargetChurchName, string ChurchSlug, string TargetChurchSlug);
}
