namespace Directory.Tests.Unit.Api;

using System.Data;
using System.Text.Json;
using Azure.Messaging.ServiceBus;
using Church;
using Entities;
using Enums;
using Messaging;
using Microsoft.Extensions.Azure;
using Moderation;
using Moq;
using TestSupport;

public sealed class ModerationServiceTests
{
    private const int NoRowsUpdated = 0;

    private const int OneRowUpdated = 1;

    [Fact]
    [Trait("Category", "Unit")]
    public async Task ReviewCorrectionAsync_ReturnsFalse_WhenNoRowsUpdated()
    {
        // Arrange
        var correctionId = Guid.NewGuid();
        var reviewedBy = TestValues.NewUserId();
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
    [Trait("Category", "Unit")]
    public async Task ReviewCorrectionAsync_ReturnsTrue_WhenRowUpdated()
    {
        // Arrange
        var correctionId = Guid.NewGuid();
        var reviewedBy = TestValues.NewUserId();
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
    [Trait("Category", "Unit")]
    public async Task ReviewCorrectionAsync_StoresTheReviewersSubjectAsAGuid()
    {
        // Arrange
        var correctionId = Guid.NewGuid();
        var reviewedBy = TestValues.NewUserId();
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
        Assert.Equal(reviewedBy, update.Parameters["@ReviewedBy"].Value);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task SubmitCorrectionAsync_SendsTheSubmittersSubjectAsAGuid()
    {
        // Arrange
        var submittingUserId = TestValues.NewUserId();
        var sent = new List<ServiceBusMessage>();
        var senderMock = new Mock<ServiceBusSender>(MockBehavior.Strict);
        senderMock
            .Setup(s => s.SendMessageAsync(It.IsAny<ServiceBusMessage>(), It.IsAny<CancellationToken>()))
            .Callback<ServiceBusMessage, CancellationToken>((message, _) => sent.Add(message))
            .Returns(Task.CompletedTask);
        var service = Create(new FakeDbConnection(), senderMock);

        // Act
        await service.SubmitCorrectionAsync(
            Guid.NewGuid(),
            submittingUserId,
            TestValues.NewFieldName(),
            null,
            TestValues.NewPhoneNumber(),
            TestContext.Current.CancellationToken);

        // Assert
        var body = Assert.Single(sent).Body.ToObjectFromJson<JsonElement>();
        Assert.Equal(submittingUserId, body.GetProperty(nameof(UserCorrection.UserId)).GetGuid());
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task SubmitCorrectionAsync_EnqueuesMessageAndReturnsId()
    {
        // Arrange
        var churchId = Guid.NewGuid();
        var submittingUserId = TestValues.NewUserId();
        var correctedField = TestValues.NewFieldName();
        var proposedPhoneNumber = TestValues.NewPhoneNumber();
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
    [Trait("Category", "Unit")]
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
    [Trait("Category", "Unit")]
    public async Task MergeAsync_CommitsTransaction()
    {
        // Arrange
        var survivingChurchId = Guid.NewGuid();
        var absorbedChurchId = Guid.NewGuid();
        var mergedBy = TestValues.NewUserId();
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
    [Trait("Category", "Unit")]
    public async Task MergeAsync_WhenCommandThrows_RollsBackAndRethrows()
    {
        // Arrange
        var survivingChurchId = Guid.NewGuid();
        var absorbedChurchId = Guid.NewGuid();
        var mergedBy = TestValues.NewUserId();
        var repointFailureMessage = TestValues.NewFailureMessage();
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
    [Trait("Category", "Unit")]
    public async Task MergeAsync_SameSurvivingAndAbsorbedId_ThrowsWithoutTouchingDb()
    {
        // Arrange
        var conn = new FakeDbConnection();
        var survivingChurchId = Guid.NewGuid();
        var absorbedId = survivingChurchId;
        var mergedBy = TestValues.NewUserId();
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
    [Trait("Category", "Unit")]
    public async Task MergeAsync_SurvivingChurchNotActive_ThrowsAndNeverStartsTransaction()
    {
        // Arrange
        var survivingChurchId = Guid.NewGuid();
        var absorbedChurchId = Guid.NewGuid();
        var mergedBy = TestValues.NewUserId();
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
    [Trait("Category", "Unit")]
    public async Task MergeAsync_AbsorbedChurchNotActive_ThrowsAndNeverStartsTransaction()
    {
        // Arrange
        var survivingChurchId = Guid.NewGuid();
        var absorbedChurchId = Guid.NewGuid();
        var mergedBy = TestValues.NewUserId();
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
    [Trait("Category", "Unit")]
    public async Task GetCorrectionsAsync_WithStatusFilter_AddsWhereClauseAndMapsRow()
    {
        // Arrange
        var expectedNewValue = TestValues.NewStreet();
        var expectedTotalCount = TestValues.NewRowCount();
        var requestedPage = TestValues.NewPage();
        var requestedPageSize = TestValues.NewPageSize();
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
        Assert.Contains("WHERE (@Status IS NULL OR c.[Status] = @Status)", cmd.CapturedCommandText, StringComparison.Ordinal);
        Assert.Equal((int)CorrectionStatus.Pending, cmd.Parameters["@Status"].Value);
        Assert.Equal(expectedTotalCount, totalCount);
        Assert.Equal(expectedNewValue, Assert.Single(items).NewValue);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task GetCorrectionsAsync_WithoutStatusFilter_PassesDbNullStatus()
    {
        // Arrange
        var requestedPage = TestValues.NewPage();
        var requestedPageSize = TestValues.NewPageSize();
        var conn = new FakeDbConnection();
        var cmd = FakeDbCommand.WithReader(BuildCorrectionTable(includeTotalCount: true));
        conn.Enqueue(cmd);
        var service = Create(conn);

        // Act
        var (items, totalCount) = await service.GetCorrectionsAsync(
            null, requestedPage, requestedPageSize, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(DBNull.Value, cmd.Parameters["@Status"].Value);
        Assert.Empty(items);
        Assert.Equal(0, totalCount);
    }

    [Fact]
    [Trait("Category", "Unit")]
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
    [Trait("Category", "Unit")]
    public async Task GetCorrectionByIdAsync_RowPopulated_MapsAllColumns()
    {
        // Arrange
        var correctionId = Guid.NewGuid();
        var expectedOldValue = TestValues.NewStreet();
        var expectedUserId = TestValues.NewUserId();
        var expectedReviewedBy = TestValues.NewUserId();
        var expectedChurchName = TestValues.NewName();
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
        return new ModerationService(conn, factory.Object);
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
        string? churchName = null)
    {
        // Arrange
        var correctionId = Guid.NewGuid();
        var churchId = Guid.NewGuid();
        var values = new List<object>
        {
            correctionId,
            churchId,
            userId ?? TestValues.NewUserId(),
            TestValues.NewFieldName(),
            oldValue ?? TestValues.NewStreet(),
            newValue ?? TestValues.NewStreet(),
            (int)CorrectionStatus.Approved,
            reviewedBy ?? TestValues.NewUserId(),
            TestValues.NewUtcTimestamp(),
            TestValues.NewUtcTimestamp(),
            churchName ?? TestValues.NewName(),
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
            TestValues.NewFieldName(),
            DBNull.Value,
            TestValues.NewStreet(),
            (int)CorrectionStatus.Pending,
            DBNull.Value,
            DBNull.Value,
            TestValues.NewUtcTimestamp(),
            DBNull.Value,
        ];
    }
}
