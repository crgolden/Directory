namespace Directory.Tests.Unit.Api;

using System.Globalization;
using Directory.Church;
using Directory.Entities;
using Directory.Enums;
using Directory.Moderation;
using Directory.Tests.Unit.TestSupport;
using Microsoft.Extensions.Azure;
using Moq;

[Trait("Category", "Unit")]
public sealed class ModerationApplyCorrectionTests
{
    [Fact]
    public async Task ApplyCorrectionAsync_FieldCorrection_SavesTheChurchWithTheNewValue()
    {
        // Arrange
        var newPhoneNumber = Generated.NewPhoneNumber();
        var correction = CorrectionFor(nameof(Church.PhoneNumber), newPhoneNumber);
        var conn = new FakeDbConnection();
        conn.Enqueue(FakeDbCommand.WithReader(ChurchRows.ActiveChurch(correction.ChurchId)));
        conn.Enqueue(FakeDbCommand.WithNonQueryResult(1));
        var service = NewService(conn);

        // Act
        var refusal = await service.ApplyCorrectionAsync(correction, Generated.NewUserId(), null, TestContext.Current.CancellationToken);

        // Assert
        Assert.Null(refusal);
        var update = conn.ExecutedCommands[^1];
        Assert.Contains("UPDATE [dbo].[Churches]", update.CapturedCommandText, StringComparison.Ordinal);
        Assert.Equal(newPhoneNumber, update.Parameters[SqlParameters.PhoneNumber].Value);
        Assert.Equal(correction.ChurchId, update.Parameters[SqlParameters.Id].Value);
    }

    [Fact]
    public async Task ApplyCorrectionAsync_FieldOutsideTheCorrectableSet_ReadsAndWritesNothing()
    {
        // Arrange
        var correction = CorrectionFor(Generated.NewFieldName(), Generated.NewName());
        var conn = new FakeDbConnection();
        var service = NewService(conn);

        // Act
        var refusal = await service.ApplyCorrectionAsync(correction, Generated.NewUserId(), null, TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(refusal);
        Assert.Empty(conn.ExecutedCommands);
    }

    [Fact]
    public async Task ApplyCorrectionAsync_BlankName_WritesNothing()
    {
        // Arrange
        var correction = CorrectionFor(nameof(Church.CanonicalName), Generated.NewBlank());
        var conn = new FakeDbConnection();
        conn.Enqueue(FakeDbCommand.WithReader(ChurchRows.ActiveChurch(correction.ChurchId)));
        var service = NewService(conn);

        // Act
        var refusal = await service.ApplyCorrectionAsync(correction, Generated.NewUserId(), null, TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(refusal);
        Assert.Single(conn.ExecutedCommands);
    }

    [Fact]
    public async Task ApplyCorrectionAsync_ChurchWhoseStoredRowIsInvalid_WritesNothing()
    {
        // Arrange
        var correction = CorrectionFor(nameof(Church.PhoneNumber), Generated.NewPhoneNumber());
        var conn = new FakeDbConnection();
        conn.Enqueue(FakeDbCommand.WithReader(ChurchRows.ActiveChurchWithBlankCity(correction.ChurchId)));
        var service = NewService(conn);

        // Act
        var refusal = await service.ApplyCorrectionAsync(correction, Generated.NewUserId(), null, TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(refusal);
        Assert.Single(conn.ExecutedCommands);
    }

    [Fact]
    public async Task ApplyCorrectionAsync_InactiveChurch_WritesNothing()
    {
        // Arrange
        var correction = CorrectionFor(nameof(Church.PhoneNumber), Generated.NewPhoneNumber());
        var conn = new FakeDbConnection();
        conn.Enqueue(FakeDbCommand.WithReader(ChurchRows.InactiveChurch(correction.ChurchId)));
        var service = NewService(conn);

        // Act
        var refusal = await service.ApplyCorrectionAsync(correction, Generated.NewUserId(), null, TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(refusal);
        Assert.Single(conn.ExecutedCommands);
    }

    [Fact]
    public async Task ApplyCorrectionAsync_MissingChurch_WritesNothing()
    {
        // Arrange
        var correction = CorrectionFor(nameof(Church.PhoneNumber), Generated.NewPhoneNumber());
        var conn = new FakeDbConnection();
        conn.Enqueue(FakeDbCommand.WithReader(ChurchRows.Table(includeTotalCount: false)));
        var service = NewService(conn);

        // Act
        var refusal = await service.ApplyCorrectionAsync(correction, Generated.NewUserId(), null, TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(refusal);
        Assert.Single(conn.ExecutedCommands);
    }

    [Fact]
    public async Task ApplyCorrectionAsync_ChurchGoneBeforeTheSave_IsRefused()
    {
        // Arrange
        var correction = CorrectionFor(nameof(Church.PhoneNumber), Generated.NewPhoneNumber());
        var conn = new FakeDbConnection();
        conn.Enqueue(FakeDbCommand.WithReader(ChurchRows.ActiveChurch(correction.ChurchId)));
        conn.Enqueue(FakeDbCommand.WithNonQueryResult(0));
        var service = NewService(conn);

        // Act
        var refusal = await service.ApplyCorrectionAsync(correction, Generated.NewUserId(), null, TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(refusal);
    }

    [Fact]
    public async Task ApplyCorrectionAsync_StateCorrectionThatIsNotAUspsCode_WritesNothing()
    {
        // Arrange
        var correction = CorrectionFor(nameof(Church.State), Generated.NewUnparseableStateCode());
        var conn = new FakeDbConnection();
        conn.Enqueue(FakeDbCommand.WithReader(ChurchRows.ActiveChurch(correction.ChurchId)));
        var service = NewService(conn);

        // Act
        var refusal = await service.ApplyCorrectionAsync(correction, Generated.NewUserId(), null, TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(refusal);
        Assert.Single(conn.ExecutedCommands);
    }

    [Fact]
    public async Task ApplyCorrectionAsync_WorshipStyleCorrection_BindsTheStyleAsItsNumber()
    {
        // Arrange
        var style = Generated.NewDefinedValue<WorshipStyle>();
        var correction = CorrectionFor(nameof(Church.WorshipStyle), ((int)style).ToString(CultureInfo.InvariantCulture));
        var conn = new FakeDbConnection();
        conn.Enqueue(FakeDbCommand.WithReader(ChurchRows.ActiveChurch(correction.ChurchId)));
        conn.Enqueue(FakeDbCommand.WithNonQueryResult(1));
        var service = NewService(conn);

        // Act
        var refusal = await service.ApplyCorrectionAsync(correction, Generated.NewUserId(), null, TestContext.Current.CancellationToken);

        // Assert
        Assert.Null(refusal);
        Assert.Equal((int)style, conn.ExecutedCommands[^1].Parameters[SqlParameters.WorshipStyle].Value);
    }

    [Fact]
    public async Task ApplyCorrectionAsync_WorshipStyleOutsideTheEnum_WritesNothing()
    {
        // Arrange
        var correction = CorrectionFor(
            nameof(Church.WorshipStyle),
            ((int)Generated.NewUndefinedValue<WorshipStyle>()).ToString(CultureInfo.InvariantCulture));
        var conn = new FakeDbConnection();
        conn.Enqueue(FakeDbCommand.WithReader(ChurchRows.ActiveChurch(correction.ChurchId)));
        var service = NewService(conn);

        // Act
        var refusal = await service.ApplyCorrectionAsync(correction, Generated.NewUserId(), null, TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(refusal);
        Assert.Single(conn.ExecutedCommands);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task ApplyCorrectionAsync_AccessibilityCorrection_BindsABoolean(bool accessible)
    {
        // Arrange
        var correction = CorrectionFor(nameof(Church.WheelchairAccessible), accessible.ToString());
        var conn = new FakeDbConnection();
        conn.Enqueue(FakeDbCommand.WithReader(ChurchRows.ActiveChurch(correction.ChurchId)));
        conn.Enqueue(FakeDbCommand.WithNonQueryResult(1));
        var service = NewService(conn);

        // Act
        var refusal = await service.ApplyCorrectionAsync(correction, Generated.NewUserId(), null, TestContext.Current.CancellationToken);

        // Assert
        Assert.Null(refusal);
        Assert.Equal(accessible, Assert.IsType<bool>(conn.ExecutedCommands[^1].Parameters[SqlParameters.WheelchairAccessible].Value));
    }

    [Fact]
    public async Task ApplyCorrectionAsync_DenominationThatIsNotAnId_WritesNothing()
    {
        // Arrange
        var correction = CorrectionFor(nameof(Church.DenominationId), Generated.NewName());
        var conn = new FakeDbConnection();
        conn.Enqueue(FakeDbCommand.WithReader(ChurchRows.ActiveChurch(correction.ChurchId)));
        var service = NewService(conn);

        // Act
        var refusal = await service.ApplyCorrectionAsync(correction, Generated.NewUserId(), null, TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(refusal);
        Assert.Single(conn.ExecutedCommands);
    }

    [Fact]
    public async Task ApplyCorrectionAsync_MergeWithNoSurvivorChosen_MergesNothing()
    {
        // Arrange
        var correction = CorrectionFor(ModerationService.MergeField, Generated.NewChurchId().ToString());
        var conn = new FakeDbConnection();
        var service = NewService(conn);

        // Act
        var refusal = await service.ApplyCorrectionAsync(correction, Generated.NewUserId(), null, TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(refusal);
        Assert.Empty(conn.ExecutedCommands);
    }

    [Fact]
    public async Task ApplyCorrectionAsync_MergeSurvivorOutsideThePair_MergesNothing()
    {
        // Arrange
        var correction = CorrectionFor(ModerationService.MergeField, Generated.NewChurchId().ToString());
        var unrelatedChurchId = Generated.NewChurchId();
        var conn = new FakeDbConnection();
        var service = NewService(conn);

        // Act
        var refusal = await service.ApplyCorrectionAsync(correction, Generated.NewUserId(), unrelatedChurchId, TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(refusal);
        Assert.Empty(conn.ExecutedCommands);
    }

    [Fact]
    public async Task ApplyCorrectionAsync_MergeKeepingTheSuggestedChurch_AbsorbsTheOtherOneAndClosesItsSuggestions()
    {
        // Arrange
        var suggestedId = Generated.NewChurchId();
        var correction = CorrectionFor(ModerationService.MergeField, suggestedId.ToString());
        var conn = new FakeDbConnection();
        conn.Enqueue(FakeDbCommand.WithScalarResult(1));
        conn.Enqueue(FakeDbCommand.WithScalarResult(1));
        var service = NewService(conn);

        // Act
        var refusal = await service.ApplyCorrectionAsync(correction, Generated.NewUserId(), suggestedId, TestContext.Current.CancellationToken);

        // Assert
        Assert.Null(refusal);
        var absorbedParams = conn.ExecutedCommands
            .Where(cmd => cmd.Parameters.Contains(SqlParameters.Absorbed))
            .Select(cmd => cmd.Parameters[SqlParameters.Absorbed].Value)
            .ToList();
        Assert.All(absorbedParams, absorbed => Assert.Equal(correction.ChurchId, absorbed));
        Assert.Contains(
            conn.ExecutedCommands,
            cmd => cmd.CapturedCommandText?.Contains("UPDATE [dbo].[UserCorrections]", StringComparison.Ordinal) == true
                   && cmd.CapturedCommandText.Contains("TRY_CONVERT(UNIQUEIDENTIFIER, [NewValue]) = @Absorbed", StringComparison.Ordinal));
    }

    private static ModerationService NewService(FakeDbConnection conn) =>
        new ModerationService(
            conn,
            Mock.Of<IAzureClientFactory<Azure.Messaging.ServiceBus.ServiceBusClient>>(),
            new ChurchService(conn));

    private static UserCorrection CorrectionFor(string field, string newValue) => new UserCorrection
    {
        ChurchId = Generated.NewChurchId(),
        Field = field,
        NewValue = newValue,
    };
}
