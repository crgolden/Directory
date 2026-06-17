namespace Directory.Tests.Api;

using System.Data;
using Azure.Messaging.ServiceBus;
using Enums;
using Microsoft.Extensions.Azure;
using Moderation;
using Moq;
using TestSupport;

public sealed class ModerationServiceTests
{
    [Fact]
    [Trait("Category", "Unit")]
    public async Task ReviewCorrectionAsync_ReturnsFalse_WhenNoRowsUpdated()
    {
        var conn = new FakeDbConnection();
        conn.Enqueue(FakeDbCommand.WithNonQueryResult(0));
        var service = Create(conn);

        var result = await service.ReviewCorrectionAsync(
            Guid.NewGuid(),
            CorrectionStatus.Approved,
            "moderator@example.com",
            TestContext.Current.CancellationToken);

        Assert.False(result);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task ReviewCorrectionAsync_ReturnsTrue_WhenRowUpdated()
    {
        var conn = new FakeDbConnection();
        conn.Enqueue(FakeDbCommand.WithNonQueryResult(1));
        var service = Create(conn);

        var result = await service.ReviewCorrectionAsync(
            Guid.NewGuid(),
            CorrectionStatus.Approved,
            "moderator@example.com",
            TestContext.Current.CancellationToken);

        Assert.True(result);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task SubmitCorrectionAsync_EnqueuesMessageAndReturnsId()
    {
        var senderMock = new Mock<ServiceBusSender>(MockBehavior.Strict);
        senderMock
            .Setup(s => s.SendMessageAsync(It.IsAny<ServiceBusMessage>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        var service = Create(new FakeDbConnection(), senderMock);

        var id = await service.SubmitCorrectionAsync(
            Guid.NewGuid(),
            "user-123",
            "PhoneNumber",
            null,
            "555-1234",
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
        var conn = new FakeDbConnection();
        conn.Enqueue(FakeDbCommand.WithReader(new DataTable()));
        var service = Create(conn);

        var result = await service.GetCorrectionByIdAsync(
            Guid.NewGuid(), TestContext.Current.CancellationToken);

        Assert.Null(result);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task MergeAsync_CommitsTransaction()
    {
        var conn = new FakeDbConnection();

        // 6 repoint UPDATEs + 1 soft delete + 1 audit INSERT
        for (var i = 0; i < 8; i++)
        {
            conn.Enqueue(FakeDbCommand.WithNonQueryResult(1));
        }

        var service = Create(conn);

        await service.MergeAsync(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "moderator@example.com",
            TestContext.Current.CancellationToken);

        Assert.True(conn.LastTransaction?.Committed);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task MergeAsync_WhenCommandThrows_RollsBackAndRethrows()
    {
        var conn = new FakeDbConnection();
        conn.Enqueue(FakeDbCommand.WithException(new InvalidOperationException("boom")));
        var service = Create(conn);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.MergeAsync(Guid.NewGuid(), Guid.NewGuid(), "moderator@example.com", TestContext.Current.CancellationToken));

        Assert.True(conn.LastTransaction?.RolledBack);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task GetCorrectionsAsync_WithStatusFilter_AddsWhereClauseAndMapsRow()
    {
        var table = BuildCorrectionTable(includeTotalCount: true);
        table.Rows.Add(CorrectionRowPopulated(totalCount: 4));
        var conn = new FakeDbConnection();
        var cmd = FakeDbCommand.WithReader(table);
        conn.Enqueue(cmd);
        var service = Create(conn);

        var (items, totalCount) = await service.GetCorrectionsAsync(
            CorrectionStatus.Pending, 1, 10, TestContext.Current.CancellationToken);

        Assert.Contains("WHERE c.[Status] = @Status", cmd.CapturedCommandText, StringComparison.Ordinal);
        Assert.Equal(4, totalCount);
        Assert.Equal("999 New St", Assert.Single(items).NewValue);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task GetCorrectionsAsync_WithoutStatusFilter_OmitsWhereClause()
    {
        var conn = new FakeDbConnection();
        var cmd = FakeDbCommand.WithReader(BuildCorrectionTable(includeTotalCount: true));
        conn.Enqueue(cmd);
        var service = Create(conn);

        var (items, totalCount) = await service.GetCorrectionsAsync(
            null, 1, 10, TestContext.Current.CancellationToken);

        Assert.DoesNotContain("WHERE c.[Status]", cmd.CapturedCommandText, StringComparison.Ordinal);
        Assert.Empty(items);
        Assert.Equal(0, totalCount);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task GetCorrectionByIdAsync_RowWithNullableNulls_MapsNulls()
    {
        var table = BuildCorrectionTable(includeTotalCount: false);
        table.Rows.Add(CorrectionRowNullable());
        var conn = new FakeDbConnection();
        conn.Enqueue(FakeDbCommand.WithReader(table));
        var service = Create(conn);

        var result = await service.GetCorrectionByIdAsync(Guid.NewGuid(), TestContext.Current.CancellationToken);

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
        var table = BuildCorrectionTable(includeTotalCount: false);
        table.Rows.Add(CorrectionRowPopulated(totalCount: null));
        var conn = new FakeDbConnection();
        conn.Enqueue(FakeDbCommand.WithReader(table));
        var service = Create(conn);

        var result = await service.GetCorrectionByIdAsync(Guid.NewGuid(), TestContext.Current.CancellationToken);

        Assert.NotNull(result);
        Assert.Equal("901 Trinity St", result.OldValue);
        Assert.Equal("e2e-mod-id", result.ReviewedBy);
        Assert.NotNull(result.ReviewedAt);
        Assert.Equal("First Baptist Church Austin", result.ChurchName);
    }

    private static ModerationService Create(FakeDbConnection conn, Mock<ServiceBusSender>? senderMock = null)
    {
        var clientMock = new Mock<ServiceBusClient>(MockBehavior.Loose);
        clientMock.Setup(c => c.CreateSender("contributions"))
                  .Returns(senderMock?.Object ?? new Mock<ServiceBusSender>().Object);
        var factory = new Mock<IAzureClientFactory<ServiceBusClient>>(MockBehavior.Loose);
        factory.Setup(f => f.CreateClient("crgolden"))
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
        t.Columns.Add("ReviewedAt", typeof(DateTime));
        t.Columns.Add("CreatedAt", typeof(DateTime));
        t.Columns.Add("ChurchName", typeof(string));
        if (includeTotalCount)
        {
            t.Columns.Add("TotalCount", typeof(int));
        }

        return t;
    }

    private static object[] CorrectionRowPopulated(int? totalCount)
    {
        var values = new List<object>
        {
            Guid.NewGuid(), Guid.NewGuid(), "some-user-id", "street", "901 Trinity St", "999 New St",
            1, "e2e-mod-id", DateTime.UtcNow, DateTime.UtcNow, "First Baptist Church Austin",
        };
        if (totalCount.HasValue)
        {
            values.Add(totalCount.Value);
        }

        return [.. values];
    }

    private static object[] CorrectionRowNullable() =>
    [
        Guid.NewGuid(), Guid.NewGuid(), "some-user-id", "street", DBNull.Value, "999 New St",
        0, DBNull.Value, DBNull.Value, DateTime.UtcNow, DBNull.Value,
    ];
}
