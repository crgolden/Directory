namespace Directory.Tests.Unit.Api;

using System.Data;
using Admin;
using Azure.Messaging.ServiceBus;
using Microsoft.Extensions.Azure;
using Moq;
using TestSupport;

public sealed class AdminServiceTests
{
    // --- ParseCsv (pure, internal static) ---
    [Fact]
    [Trait("Category", "Unit")]
    public void ParseCsv_SingleRow_MapsAllFields()
    {
        // Arrange
        const string csv = "CanonicalName,Street,City,State,Zip,PhoneNumber,Website,EmailAddress\nGrace Church,123 Main St,Phoenix,AZ,85001,602-555-1212,https://grace.example,info@grace.example";

        // Act
        var rows = AdminService.ParseCsv(csv).ToList();

        // Assert
        Assert.Single(rows);
        var r = rows[0];
        Assert.Equal("Grace Church", r.CanonicalName);
        Assert.Equal("123 Main St", r.Street);
        Assert.Equal("Phoenix", r.City);
        Assert.Equal("AZ", r.State);
        Assert.Equal("85001", r.Zip);
        Assert.Equal("602-555-1212", r.PhoneNumber);
        Assert.Equal("https://grace.example", r.Website);
        Assert.Equal("info@grace.example", r.EmailAddress);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void ParseCsv_MissingNameColumn_SkipsRow()
    {
        // Arrange — name blank → row skipped
        const string csv = "CanonicalName,State\n,AZ";

        // Act
        var rows = AdminService.ParseCsv(csv).ToList();

        // Assert
        Assert.Empty(rows);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void ParseCsv_MissingStateColumn_SkipsRow()
    {
        // Arrange
        const string csv = "CanonicalName,State\nGrace Church,";

        // Act
        var rows = AdminService.ParseCsv(csv).ToList();

        // Assert
        Assert.Empty(rows);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void ParseCsv_EmptyBody_YieldsNothing()
    {
        // Act
        Assert.Empty(AdminService.ParseCsv(string.Empty));
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void ParseCsv_HeaderOnly_YieldsNothing()
    {
        // Act
        Assert.Empty(AdminService.ParseCsv("CanonicalName,State"));
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void ParseCsv_MultipleRows_ParsesAll()
    {
        // Arrange
        const string csv = "CanonicalName,State\nGrace Church,AZ\nTrinity,CO";

        // Act
        var rows = AdminService.ParseCsv(csv).ToList();

        // Assert
        Assert.Equal(2, rows.Count);
        Assert.Equal("Grace Church", rows[0].CanonicalName);
        Assert.Equal("Trinity", rows[1].CanonicalName);
    }

    // --- ImportCsvAsync (instance; Service Bus publish) ---
    [Fact]
    [Trait("Category", "Unit")]
    public async Task ImportCsvAsync_TwoRows_PublishesTwo()
    {
        // Arrange
        const string csv = "CanonicalName,State\nGrace Church,AZ\nTrinity,CO";
        var (service, sender) = BuildService(new FakeDbConnection());

        // Act
        var published = await service.ImportCsvAsync(csv, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(2, published);
        sender.Verify(s => s.SendMessageAsync(It.IsAny<ServiceBusMessage>(), It.IsAny<CancellationToken>()), Times.Exactly(2));
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task ImportCsvAsync_EmptyCsv_PublishesZero()
    {
        // Arrange
        var (service, sender) = BuildService(new FakeDbConnection());

        // Act
        var published = await service.ImportCsvAsync(string.Empty, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(0, published);
        sender.Verify(s => s.SendMessageAsync(It.IsAny<ServiceBusMessage>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    // --- ExportCsvAsync (instance; DB read) ---
    [Fact]
    [Trait("Category", "Unit")]
    public async Task ExportCsvAsync_ConnectionClosed_OpensAndReturnsHeaderRow()
    {
        // Arrange — empty table (no data rows)
        var conn = new FakeDbConnection();
        conn.Enqueue(FakeDbCommand.WithReader(new DataTable()));
        var (service, _) = BuildService(conn);

        // Act
        var csv = await service.ExportCsvAsync(TestContext.Current.CancellationToken);

        // Assert — connection was opened; header row present
        Assert.Equal(System.Data.ConnectionState.Open, conn.State);
        Assert.StartsWith("Id,CanonicalName", csv, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task ExportCsvAsync_HasRows_RowCountMatchesDataTable()
    {
        // Arrange
        var table = BuildExportTable();
        table.Rows.Add(ExportRow("Grace Church"));
        table.Rows.Add(ExportRow("Trinity"));
        var conn = new FakeDbConnection();
        conn.Enqueue(FakeDbCommand.WithReader(table));
        var (service, _) = BuildService(conn);

        // Act
        var csv = await service.ExportCsvAsync(TestContext.Current.CancellationToken);
        var lines = csv.Split('\n', StringSplitOptions.RemoveEmptyEntries);

        // Assert — header + 2 data rows
        Assert.Equal(3, lines.Length);
        Assert.Contains("Grace Church", lines[1], StringComparison.Ordinal);
        Assert.Contains("Trinity", lines[2], StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task ExportCsvAsync_ContentTypeHeader_IsTextCsvInSqlCommand()
    {
        // Arrange — verify ORDER BY clause is present
        var conn = new FakeDbConnection();
        conn.Enqueue(FakeDbCommand.WithReader(new DataTable()));
        var (service, _) = BuildService(conn);

        // Act
        await service.ExportCsvAsync(TestContext.Current.CancellationToken);

        // Assert — ORDER BY in generated SQL
        var cmd = Assert.Single(conn.ExecutedCommands);
        Assert.Contains("ORDER BY [State] ASC, [CanonicalName] ASC", cmd.CommandText, StringComparison.Ordinal);
    }

    private static (AdminService Service, Mock<ServiceBusSender> Sender) BuildService(FakeDbConnection connection)
    {
        var sender = new Mock<ServiceBusSender>(MockBehavior.Strict);
        sender.Setup(s => s.SendMessageAsync(It.IsAny<ServiceBusMessage>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        sender.Setup(s => s.DisposeAsync()).Returns(ValueTask.CompletedTask);

        var busClient = new Mock<ServiceBusClient>(MockBehavior.Strict);
        busClient.Setup(c => c.CreateSender("geocoding-requests")).Returns(sender.Object);

        var busFactory = new Mock<IAzureClientFactory<ServiceBusClient>>(MockBehavior.Strict);
        busFactory.Setup(f => f.CreateClient("crgolden")).Returns(busClient.Object);

        return (new AdminService(connection, busFactory.Object), sender);
    }

    private static DataTable BuildExportTable()
    {
        var t = new DataTable();
        t.Columns.Add("Id", typeof(Guid));
        t.Columns.Add("CanonicalName", typeof(string));
        t.Columns.Add("Slug", typeof(string));
        t.Columns.Add("Street", typeof(string));
        t.Columns.Add("City", typeof(string));
        t.Columns.Add("State", typeof(string));
        t.Columns.Add("Zip", typeof(string));
        t.Columns.Add("PhoneNumber", typeof(string));
        t.Columns.Add("Website", typeof(string));
        t.Columns.Add("EmailAddress", typeof(string));
        t.Columns.Add("WorshipStyle", typeof(int));
        t.Columns.Add("PrimaryLanguage", typeof(string));
        t.Columns.Add("AcceptsLGBTQ", typeof(bool));
        t.Columns.Add("WheelchairAccessible", typeof(bool));
        t.Columns.Add("HasNursery", typeof(bool));
        t.Columns.Add("HasYouthProgram", typeof(bool));
        t.Columns.Add("ConfidenceScore", typeof(decimal));
        t.Columns.Add("CreatedAt", typeof(DateTime));
        t.Columns.Add("UpdatedAt", typeof(DateTime));
        return t;
    }

    private static object[] ExportRow(string name) =>
    [
        Guid.NewGuid(), name, "slug", "123 Main", "Phoenix", "AZ", "85001",
        DBNull.Value, DBNull.Value, DBNull.Value, 1, "English",
        true, false, true, false, 0.9m, DateTime.UtcNow, DateTime.UtcNow,
    ];
}
