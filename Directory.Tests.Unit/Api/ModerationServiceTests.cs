namespace Directory.Tests.Unit.Api;

using System.Data;
using Azure.Messaging.ServiceBus;
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
        var correctionId = Guid.NewGuid();
        var reviewedBy = TestValues.NewUserId();
        var conn = new FakeDbConnection();
        conn.Enqueue(FakeDbCommand.WithNonQueryResult(NoRowsUpdated));
        var service = Create(conn);

        var result = await service.ReviewCorrectionAsync(
            correctionId,
            CorrectionStatus.Approved,
            reviewedBy,
            TestContext.Current.CancellationToken);

        Assert.False(result);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task ReviewCorrectionAsync_ReturnsTrue_WhenRowUpdated()
    {
        var correctionId = Guid.NewGuid();
        var reviewedBy = TestValues.NewUserId();
        var conn = new FakeDbConnection();
        conn.Enqueue(FakeDbCommand.WithNonQueryResult(OneRowUpdated));
        var service = Create(conn);

        var result = await service.ReviewCorrectionAsync(
            correctionId,
            CorrectionStatus.Approved,
            reviewedBy,
            TestContext.Current.CancellationToken);

        Assert.True(result);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task SubmitCorrectionAsync_EnqueuesMessageAndReturnsId()
    {
        var churchId = Guid.NewGuid();
        var submittingUserId = TestValues.NewUserId();
        var correctedField = TestValues.NewFieldName();
        var proposedPhoneNumber = TestValues.NewPhoneNumber();
        var senderMock = new Mock<ServiceBusSender>(MockBehavior.Strict);
        senderMock
            .Setup(s => s.SendMessageAsync(It.IsAny<ServiceBusMessage>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        var service = Create(new FakeDbConnection(), senderMock);

        var id = await service.SubmitCorrectionAsync(
            churchId,
            submittingUserId,
            correctedField,
            null,
            proposedPhoneNumber,
            TestContext.Current.CancellationToken);

        Assert.NotEqual(Guid.Empty, id);
        senderMock.Verify(
            s => s.SendMessageAsync(It.Is<ServiceBusMessage>(m => m.MessageId == id.ToString()), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task GetCorrectionByIdAsync_ReturnsNull_WhenNoRows()
    {
        var correctionId = Guid.NewGuid();
        var conn = new FakeDbConnection();
        conn.Enqueue(FakeDbCommand.WithReader(new DataTable()));
        var service = Create(conn);

        var result = await service.GetCorrectionByIdAsync(
            correctionId, TestContext.Current.CancellationToken);

        Assert.Null(result);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task MergeAsync_CommitsTransaction()
    {
        var survivingChurchId = Guid.NewGuid();
        var absorbedChurchId = Guid.NewGuid();
        var mergedBy = TestValues.NewUserId();
        var conn = new FakeDbConnection();
        conn.Enqueue(SurvivingChurchExists());
        conn.Enqueue(AbsorbedChurchExists());
        EnqueueSuccessfulMergeWrites(conn);

        var service = Create(conn);

        await service.MergeAsync(
            survivingChurchId,
            absorbedChurchId,
            mergedBy,
            TestContext.Current.CancellationToken);

        Assert.True(conn.LastTransaction?.Committed);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task MergeAsync_WhenCommandThrows_RollsBackAndRethrows()
    {
        var survivingChurchId = Guid.NewGuid();
        var absorbedChurchId = Guid.NewGuid();
        var mergedBy = TestValues.NewUserId();
        var repointFailureMessage = TestValues.NewFailureMessage();
        var conn = new FakeDbConnection();
        conn.Enqueue(SurvivingChurchExists());
        conn.Enqueue(AbsorbedChurchExists());
        conn.Enqueue(FakeDbCommand.WithException(new InvalidOperationException(repointFailureMessage)));
        var service = Create(conn);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.MergeAsync(survivingChurchId, absorbedChurchId, mergedBy, TestContext.Current.CancellationToken));

        Assert.Equal(repointFailureMessage, ex.Message);
        Assert.True(conn.LastTransaction?.RolledBack);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task MergeAsync_SameSurvivingAndAbsorbedId_ThrowsWithoutTouchingDb()
    {
        var conn = new FakeDbConnection();
        var survivingChurchId = Guid.NewGuid();
        var mergedBy = TestValues.NewUserId();
        var service = Create(conn);

        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            service.MergeAsync(survivingChurchId, survivingChurchId, mergedBy, TestContext.Current.CancellationToken));

        Assert.Equal("absorbedId", ex.ParamName);
        Assert.Empty(conn.ExecutedCommands);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task MergeAsync_SurvivingChurchNotActive_ThrowsAndNeverStartsTransaction()
    {
        var survivingChurchId = Guid.NewGuid();
        var absorbedChurchId = Guid.NewGuid();
        var mergedBy = TestValues.NewUserId();
        var conn = new FakeDbConnection();
        conn.Enqueue(ChurchDoesNotExist());
        var service = Create(conn);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.MergeAsync(survivingChurchId, absorbedChurchId, mergedBy, TestContext.Current.CancellationToken));

        Assert.Null(conn.LastTransaction);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task MergeAsync_AbsorbedChurchNotActive_ThrowsAndNeverStartsTransaction()
    {
        var survivingChurchId = Guid.NewGuid();
        var absorbedChurchId = Guid.NewGuid();
        var mergedBy = TestValues.NewUserId();
        var conn = new FakeDbConnection();
        conn.Enqueue(SurvivingChurchExists());
        conn.Enqueue(ChurchDoesNotExist());
        var service = Create(conn);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.MergeAsync(survivingChurchId, absorbedChurchId, mergedBy, TestContext.Current.CancellationToken));

        Assert.Null(conn.LastTransaction);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task GetCorrectionsAsync_WithStatusFilter_AddsWhereClauseAndMapsRow()
    {
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

        var (items, totalCount) = await service.GetCorrectionsAsync(
            CorrectionStatus.Pending, requestedPage, requestedPageSize, TestContext.Current.CancellationToken);

        Assert.Contains("WHERE (@Status IS NULL OR c.[Status] = @Status)", cmd.CapturedCommandText, StringComparison.Ordinal);
        Assert.Equal((int)CorrectionStatus.Pending, cmd.Parameters["@Status"].Value);
        Assert.Equal(expectedTotalCount, totalCount);
        Assert.Equal(expectedNewValue, Assert.Single(items).NewValue);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task GetCorrectionsAsync_WithoutStatusFilter_PassesDbNullStatus()
    {
        var requestedPage = TestValues.NewPage();
        var requestedPageSize = TestValues.NewPageSize();
        var conn = new FakeDbConnection();
        var cmd = FakeDbCommand.WithReader(BuildCorrectionTable(includeTotalCount: true));
        conn.Enqueue(cmd);
        var service = Create(conn);

        var (items, totalCount) = await service.GetCorrectionsAsync(
            null, requestedPage, requestedPageSize, TestContext.Current.CancellationToken);

        Assert.Equal(DBNull.Value, cmd.Parameters["@Status"].Value);
        Assert.Empty(items);
        Assert.Equal(0, totalCount);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task GetCorrectionByIdAsync_RowWithNullableNulls_MapsNulls()
    {
        var correctionId = Guid.NewGuid();
        var table = BuildCorrectionTable(includeTotalCount: false);
        table.Rows.Add(CorrectionRowNullable());
        var conn = new FakeDbConnection();
        conn.Enqueue(FakeDbCommand.WithReader(table));
        var service = Create(conn);

        var result = await service.GetCorrectionByIdAsync(correctionId, TestContext.Current.CancellationToken);

        Assert.NotNull(result);
        Assert.Null(result.OldValue);
        Assert.Null(result.ReviewedBy);
        Assert.Null(result.ReviewedAt);
        Assert.Null(result.ChurchName);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task GetCorrectionByIdAsync_RowPopulated_MapsAllColumns()
    {
        var correctionId = Guid.NewGuid();
        var expectedOldValue = TestValues.NewStreet();
        var expectedReviewedBy = TestValues.NewUserId();
        var expectedChurchName = TestValues.NewName();
        var table = BuildCorrectionTable(includeTotalCount: false);
        table.Rows.Add(CorrectionRowPopulated(
            oldValue: expectedOldValue,
            reviewedBy: expectedReviewedBy,
            churchName: expectedChurchName,
            totalCount: null));
        var conn = new FakeDbConnection();
        conn.Enqueue(FakeDbCommand.WithReader(table));
        var service = Create(conn);

        var result = await service.GetCorrectionByIdAsync(correctionId, TestContext.Current.CancellationToken);

        Assert.NotNull(result);
        Assert.Equal(expectedOldValue, result.OldValue);
        Assert.Equal(expectedReviewedBy, result.ReviewedBy);
        Assert.NotNull(result.ReviewedAt);
        Assert.Equal(expectedChurchName, result.ChurchName);
    }

    private static FakeDbCommand SurvivingChurchExists() => FakeDbCommand.WithScalarResult(1);

    private static FakeDbCommand AbsorbedChurchExists() => FakeDbCommand.WithScalarResult(1);

    private static FakeDbCommand ChurchDoesNotExist() => FakeDbCommand.WithScalarResult(0);

    private static void EnqueueSuccessfulMergeWrites(FakeDbConnection conn)
    {
        for (var i = 0; i < 8; i++)
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
        t.Columns.Add("Id", typeof(Guid));
        t.Columns.Add("ChurchId", typeof(Guid));
        t.Columns.Add("UserId", typeof(string));
        t.Columns.Add("Field", typeof(string));
        t.Columns.Add("OldValue", typeof(string));
        t.Columns.Add("NewValue", typeof(string));
        t.Columns.Add("Status", typeof(int));
        t.Columns.Add("ReviewedBy", typeof(string));
        t.Columns.Add("ReviewedAt", typeof(DateTimeOffset));
        t.Columns.Add("CreatedAt", typeof(DateTimeOffset));
        t.Columns.Add("ChurchName", typeof(string));
        if (includeTotalCount)
        {
            t.Columns.Add("TotalCount", typeof(int));
        }

        return t;
    }

    private static object[] CorrectionRowPopulated(
        int? totalCount,
        string? oldValue = null,
        string? newValue = null,
        string? reviewedBy = null,
        string? churchName = null)
    {
        var values = new List<object>
        {
            Guid.NewGuid(),
            Guid.NewGuid(),
            TestValues.NewUserId(),
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

    private static object[] CorrectionRowNullable() =>
    [
        Guid.NewGuid(),
        Guid.NewGuid(),
        TestValues.NewUserId(),
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
