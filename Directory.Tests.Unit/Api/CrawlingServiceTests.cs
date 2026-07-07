namespace Directory.Tests.Unit.Api;

using System.Data;
using Azure.Messaging.ServiceBus;
using Crawling;
using Microsoft.Extensions.Azure;
using Moq;
using TestSupport;

public sealed class CrawlingServiceTests
{
    [Fact]
    [Trait("Category", "Unit")]
    public async Task GetAllAsync_RowPopulated_MapsAllColumns()
    {
        var table = BuildCrawlTable();
        table.Rows.Add(Guid.NewGuid(), Guid.NewGuid(), "https://grace.example", DateTime.UtcNow, 1, DateTime.UtcNow, DateTime.UtcNow);
        var conn = new FakeDbConnection();
        conn.Enqueue(FakeDbCommand.WithReader(table));
        var service = Create(conn);

        var items = await service.GetAllAsync(TestContext.Current.CancellationToken);

        var item = Assert.Single(items);
        Assert.Equal("https://grace.example", item.Url);
        Assert.NotNull(item.ChurchId);
        Assert.NotNull(item.LastCrawledAt);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task GetAllAsync_RowWithNullableNulls_MapsNulls()
    {
        var table = BuildCrawlTable();
        table.Rows.Add(Guid.NewGuid(), DBNull.Value, "https://grace.example", DBNull.Value, 0, DateTime.UtcNow, DateTime.UtcNow);
        var conn = new FakeDbConnection();
        conn.Enqueue(FakeDbCommand.WithReader(table));
        var service = Create(conn);

        var items = await service.GetAllAsync(TestContext.Current.CancellationToken);

        var item = Assert.Single(items);
        Assert.Null(item.ChurchId);
        Assert.Null(item.LastCrawledAt);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task CreateAsync_WithChurchId_BindsValue()
    {
        var conn = new FakeDbConnection();
        conn.Enqueue(FakeDbCommand.WithNonQueryResult(1));
        var service = Create(conn);

        await service.CreateAsync("https://grace.example", Guid.NewGuid(), TestContext.Current.CancellationToken);

        var insert = Assert.Single(conn.ExecutedCommands);
        Assert.NotEqual(DBNull.Value, insert.Parameters["@ChurchId"].Value);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task CreateAsync_NullChurchId_BindsDbNull()
    {
        var conn = new FakeDbConnection();
        conn.Enqueue(FakeDbCommand.WithNonQueryResult(1));
        var service = Create(conn);

        await service.CreateAsync("https://grace.example", null, TestContext.Current.CancellationToken);

        var insert = Assert.Single(conn.ExecutedCommands);
        Assert.Equal(DBNull.Value, insert.Parameters["@ChurchId"].Value);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task DeleteAsync_RowDeleted_ReturnsTrue()
    {
        var conn = new FakeDbConnection();
        conn.Enqueue(FakeDbCommand.WithNonQueryResult(1));
        var service = Create(conn);

        var result = await service.DeleteAsync(Guid.NewGuid(), TestContext.Current.CancellationToken);

        Assert.True(result);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task DeleteAsync_NoRows_ReturnsFalse()
    {
        var conn = new FakeDbConnection();
        conn.Enqueue(FakeDbCommand.WithNonQueryResult(0));
        var service = Create(conn);

        var result = await service.DeleteAsync(Guid.NewGuid(), TestContext.Current.CancellationToken);

        Assert.False(result);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task TriggerScrapeAsync_UrlNotFound_ReturnsFalseWithoutSending()
    {
        var conn = new FakeDbConnection();
        conn.Enqueue(FakeDbCommand.WithScalarResult(null));
        var senderMock = new Mock<ServiceBusSender>(MockBehavior.Strict);
        var service = Create(conn, senderMock);

        var result = await service.TriggerScrapeAsync(Guid.NewGuid(), TestContext.Current.CancellationToken);

        Assert.False(result);
        Assert.Single(conn.ExecutedCommands);
        senderMock.Verify(s => s.SendMessageAsync(It.IsAny<ServiceBusMessage>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task TriggerScrapeAsync_UrlFound_SendsMessageUpdatesStatusAndReturnsTrue()
    {
        var conn = new FakeDbConnection();
        conn.Enqueue(FakeDbCommand.WithScalarResult("https://grace.example"));
        conn.Enqueue(FakeDbCommand.WithNonQueryResult(1));
        var senderMock = new Mock<ServiceBusSender>(MockBehavior.Strict);
        senderMock
            .Setup(s => s.SendMessageAsync(It.IsAny<ServiceBusMessage>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        var service = Create(conn, senderMock);

        var result = await service.TriggerScrapeAsync(Guid.NewGuid(), TestContext.Current.CancellationToken);

        Assert.True(result);
        Assert.Equal(2, conn.ExecutedCommands.Count);
        Assert.Contains("UPDATE [dbo].[CrawlSources]", conn.ExecutedCommands[1].CommandText, StringComparison.Ordinal);
        senderMock.Verify(s => s.SendMessageAsync(It.IsAny<ServiceBusMessage>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    private static CrawlingService Create(FakeDbConnection conn, Mock<ServiceBusSender>? senderMock = null)
    {
        var clientMock = new Mock<ServiceBusClient>(MockBehavior.Loose);
        clientMock.Setup(c => c.CreateSender("scrape-requests"))
                  .Returns(senderMock?.Object ?? new Mock<ServiceBusSender>().Object);
        var factory = new Mock<IAzureClientFactory<ServiceBusClient>>(MockBehavior.Loose);
        factory.Setup(f => f.CreateClient("crgolden"))
               .Returns(clientMock.Object);
        return new CrawlingService(conn, factory.Object);
    }

    private static DataTable BuildCrawlTable()
    {
        var t = new DataTable();
        t.Columns.Add("Id", typeof(Guid));
        t.Columns.Add("ChurchId", typeof(Guid));
        t.Columns.Add("Url", typeof(string));
        t.Columns.Add("LastCrawledAt", typeof(DateTime));
        t.Columns.Add("LastStatus", typeof(int));
        t.Columns.Add("CreatedAt", typeof(DateTime));
        t.Columns.Add("UpdatedAt", typeof(DateTime));
        return t;
    }
}
