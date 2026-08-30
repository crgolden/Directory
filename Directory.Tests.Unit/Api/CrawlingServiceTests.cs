namespace Directory.Tests.Unit.Api;

using System.Data;
using Azure.Messaging.ServiceBus;
using Crawling;
using Enums;
using Messaging;
using Microsoft.Extensions.Azure;
using Moq;
using TestSupport;

public sealed class CrawlingServiceTests
{
    private const int NoRowsAffected = 0;

    private const int OneRowAffected = 1;

    [Fact]
    [Trait("Category", "Unit")]
    public async Task GetAllAsync_RowPopulated_MapsAllColumns()
    {
        var crawlSourceId = Guid.NewGuid();
        var churchId = Guid.NewGuid();
        var crawlUrl = TestValues.NewWebsite();
        var lastCrawledAt = TestValues.NewUtcTimestamp();
        var lastStatus = CrawlStatus.Success;
        var createdAt = TestValues.NewUtcTimestamp();
        var updatedAt = TestValues.NewUtcTimestamp();
        var table = BuildCrawlTable();
        table.Rows.Add(
            crawlSourceId,
            churchId,
            crawlUrl,
            lastCrawledAt,
            (int)lastStatus,
            createdAt,
            updatedAt);
        var conn = new FakeDbConnection();
        conn.Enqueue(FakeDbCommand.WithReader(table));
        var service = Create(conn);

        var items = await service.GetAllAsync(TestContext.Current.CancellationToken);

        var item = Assert.Single(items);
        Assert.Equal(crawlUrl, item.Url);
        Assert.Equal(churchId, item.ChurchId);
        Assert.Equal(lastStatus, item.LastStatus);
        Assert.NotNull(item.LastCrawledAt);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task GetAllAsync_RowWithNullableNulls_MapsNulls()
    {
        var crawlSourceId = Guid.NewGuid();
        var crawlUrl = TestValues.NewWebsite();
        var createdAt = TestValues.NewUtcTimestamp();
        var updatedAt = TestValues.NewUtcTimestamp();
        var table = BuildCrawlTable();
        table.Rows.Add(
            crawlSourceId,
            DBNull.Value,
            crawlUrl,
            DBNull.Value,
            (int)CrawlStatus.Pending,
            createdAt,
            updatedAt);
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
        var crawlUrl = TestValues.NewWebsite();
        var churchId = Guid.NewGuid();
        var conn = new FakeDbConnection();
        conn.Enqueue(FakeDbCommand.WithNonQueryResult(OneRowAffected));
        var service = Create(conn);

        await service.CreateAsync(crawlUrl, churchId, TestContext.Current.CancellationToken);

        var insert = Assert.Single(conn.ExecutedCommands);
        Assert.Equal(churchId, insert.Parameters["@ChurchId"].Value);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task CreateAsync_NullChurchId_BindsDbNull()
    {
        var crawlUrl = TestValues.NewWebsite();
        var conn = new FakeDbConnection();
        conn.Enqueue(FakeDbCommand.WithNonQueryResult(OneRowAffected));
        var service = Create(conn);

        await service.CreateAsync(crawlUrl, null, TestContext.Current.CancellationToken);

        var insert = Assert.Single(conn.ExecutedCommands);
        Assert.Equal(DBNull.Value, insert.Parameters["@ChurchId"].Value);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task DeleteAsync_RowDeleted_ReturnsTrue()
    {
        var crawlSourceId = Guid.NewGuid();
        var conn = new FakeDbConnection();
        conn.Enqueue(FakeDbCommand.WithNonQueryResult(OneRowAffected));
        var service = Create(conn);

        var result = await service.DeleteAsync(crawlSourceId, TestContext.Current.CancellationToken);

        Assert.True(result);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task DeleteAsync_NoRows_ReturnsFalse()
    {
        var crawlSourceId = Guid.NewGuid();
        var conn = new FakeDbConnection();
        conn.Enqueue(FakeDbCommand.WithNonQueryResult(NoRowsAffected));
        var service = Create(conn);

        var result = await service.DeleteAsync(crawlSourceId, TestContext.Current.CancellationToken);

        Assert.False(result);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task TriggerScrapeAsync_UrlNotFound_ReturnsFalseWithoutSending()
    {
        var crawlSourceId = Guid.NewGuid();
        var conn = new FakeDbConnection();
        conn.Enqueue(FakeDbCommand.WithScalarResult(null));
        var senderMock = new Mock<ServiceBusSender>(MockBehavior.Strict);
        var service = Create(conn, senderMock);

        var result = await service.TriggerScrapeAsync(crawlSourceId, TestContext.Current.CancellationToken);

        Assert.False(result);
        Assert.Single(conn.ExecutedCommands);
        senderMock.Verify(s => s.SendMessageAsync(It.IsAny<ServiceBusMessage>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task TriggerScrapeAsync_UrlFound_SendsMessageUpdatesStatusAndReturnsTrue()
    {
        var crawlSourceId = Guid.NewGuid();
        var crawlUrl = TestValues.NewWebsite();
        var conn = new FakeDbConnection();
        conn.Enqueue(FakeDbCommand.WithScalarResult(crawlUrl));
        conn.Enqueue(FakeDbCommand.WithNonQueryResult(OneRowAffected));
        var senderMock = new Mock<ServiceBusSender>(MockBehavior.Strict);
        senderMock
            .Setup(s => s.SendMessageAsync(It.IsAny<ServiceBusMessage>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        var service = Create(conn, senderMock);

        var result = await service.TriggerScrapeAsync(crawlSourceId, TestContext.Current.CancellationToken);

        Assert.True(result);
        Assert.Equal(2, conn.ExecutedCommands.Count);
        Assert.Contains("UPDATE [dbo].[CrawlSources]", conn.ExecutedCommands[1].CommandText, StringComparison.Ordinal);
        senderMock.Verify(s => s.SendMessageAsync(It.IsAny<ServiceBusMessage>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    private static CrawlingService Create(FakeDbConnection conn, Mock<ServiceBusSender>? senderMock = null)
    {
        var clientMock = new Mock<ServiceBusClient>(MockBehavior.Loose);
        clientMock.Setup(c => c.CreateSender(ServiceBusNames.ScrapeRequests))
                  .Returns(senderMock?.Object ?? new Mock<ServiceBusSender>().Object);
        var factory = new Mock<IAzureClientFactory<ServiceBusClient>>(MockBehavior.Loose);
        factory.Setup(f => f.CreateClient(ServiceBusNames.Client))
               .Returns(clientMock.Object);
        return new CrawlingService(conn, factory.Object);
    }

    private static DataTable BuildCrawlTable()
    {
        var t = new DataTable();
        t.Columns.Add("Id", typeof(Guid));
        t.Columns.Add("ChurchId", typeof(Guid));
        t.Columns.Add("Url", typeof(string));
        t.Columns.Add("LastCrawledAt", typeof(DateTimeOffset));
        t.Columns.Add("LastStatus", typeof(int));
        t.Columns.Add("CreatedAt", typeof(DateTimeOffset));
        t.Columns.Add("UpdatedAt", typeof(DateTimeOffset));
        return t;
    }
}
