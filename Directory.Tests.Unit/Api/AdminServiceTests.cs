namespace Directory.Tests.Unit.Api;

using System.Data;
using System.Globalization;
using Admin;
using Azure.Messaging.ServiceBus;
using Messaging;
using Microsoft.Extensions.Azure;
using Moq;
using TestSupport;

public sealed class AdminServiceTests
{
    private const string FullCsvHeader = "CanonicalName,Street,City,State,Zip,PhoneNumber,Website,EmailAddress";

    private const string MinimalCsvHeader = "CanonicalName,State";

    private const string ExportCsvHeaderPrefix = "Id,CanonicalName";

    private static readonly CultureInfo CommaDecimalDottedDateCulture = CultureInfo.GetCultureInfo("de-DE");

    [Fact]
    [Trait("Category", "Unit")]
    public void ParseCsv_SingleRow_MapsAllFields()
    {
        // Arrange
        var canonicalName = TestValues.NewName();
        var street = TestValues.NewStreet();
        var city = TestValues.NewCity();
        var state = TestValues.NewStateCode();
        var zip = TestValues.NewZip();
        var phoneNumber = TestValues.NewPhoneNumber();
        var website = TestValues.NewWebsite();
        var emailAddress = TestValues.NewEmailAddress();
        var csv = $"{FullCsvHeader}\n{canonicalName},{street},{city},{state},{zip},{phoneNumber},{website},{emailAddress}";

        // Act
        var rows = AdminService.ParseCsv(csv).ToList();

        // Assert
        Assert.Single(rows);
        var r = rows[0];
        Assert.Equal(canonicalName, r.CanonicalName);
        Assert.Equal(street, r.Street);
        Assert.Equal(city, r.City);
        Assert.Equal(state, r.State);
        Assert.Equal(zip, r.Zip);
        Assert.Equal(phoneNumber, r.PhoneNumber);
        Assert.Equal(website, r.Website);
        Assert.Equal(emailAddress, r.EmailAddress);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void ParseCsv_MissingNameColumn_SkipsRow()
    {
        // Arrange
        var state = TestValues.NewStateCode();
        var csv = $"{MinimalCsvHeader}\n,{state}";

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
        var canonicalName = TestValues.NewName();
        var csv = $"{MinimalCsvHeader}\n{canonicalName},";

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
        Assert.Empty(AdminService.ParseCsv(MinimalCsvHeader));
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void ParseCsv_MultipleRows_ParsesAll()
    {
        // Arrange
        var firstChurchName = TestValues.NewName();
        var secondChurchName = TestValues.NewName();
        var csv = BuildTwoRowCsv(firstChurchName, secondChurchName);

        // Act
        var rows = AdminService.ParseCsv(csv).ToList();

        // Assert
        Assert.Equal(2, rows.Count);
        Assert.Equal(firstChurchName, rows[0].CanonicalName);
        Assert.Equal(secondChurchName, rows[1].CanonicalName);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task ImportCsvAsync_TwoRows_PublishesTwo()
    {
        // Arrange
        var firstChurchName = TestValues.NewName();
        var secondChurchName = TestValues.NewName();
        var csv = BuildTwoRowCsv(firstChurchName, secondChurchName);
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

    [Fact]
    [Trait("Category", "Unit")]
    public async Task ExportCsvAsync_ConnectionClosed_OpensAndReturnsHeaderRow()
    {
        // Arrange
        var conn = new FakeDbConnection();
        conn.Enqueue(FakeDbCommand.WithReader(new DataTable()));
        var (service, _) = BuildService(conn);

        // Act
        var csv = await service.ExportCsvAsync(TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(System.Data.ConnectionState.Open, conn.State);
        Assert.StartsWith(ExportCsvHeaderPrefix, csv, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task ExportCsvAsync_HasRows_RowCountMatchesDataTable()
    {
        // Arrange
        var firstChurchName = TestValues.NewName();
        var secondChurchName = TestValues.NewName();
        var table = BuildExportTable();
        table.Rows.Add(ExportRow(firstChurchName));
        table.Rows.Add(ExportRow(secondChurchName));
        var conn = new FakeDbConnection();
        conn.Enqueue(FakeDbCommand.WithReader(table));
        var (service, _) = BuildService(conn);

        // Act
        var csv = await service.ExportCsvAsync(TestContext.Current.CancellationToken);
        var lines = csv.Split('\n', StringSplitOptions.RemoveEmptyEntries);

        // Assert
        Assert.Equal(3, lines.Length);
        Assert.Contains(firstChurchName, lines[1], StringComparison.Ordinal);
        Assert.Contains(secondChurchName, lines[2], StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task ExportCsvAsync_OrdersByStateThenCanonicalName()
    {
        // Arrange
        var conn = new FakeDbConnection();
        conn.Enqueue(FakeDbCommand.WithReader(new DataTable()));
        var (service, _) = BuildService(conn);

        // Act
        await service.ExportCsvAsync(TestContext.Current.CancellationToken);

        // Assert
        var cmd = Assert.Single(conn.ExecutedCommands);
        Assert.Contains("ORDER BY [State] ASC, [CanonicalName] ASC", cmd.CommandText, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task ExportCsvAsync_ServerCultureIsNotInvariant_FormatsNumbersAndTimestampsInvariantly()
    {
        // Arrange
        var churchName = TestValues.NewName();
        var confidenceScore = TestValues.NewConfidenceScore();
        var createdAt = TestValues.NewUtcTimestamp();
        var updatedAt = TestValues.NewUtcTimestamp();
        var exportRow = ExportRow(churchName, confidenceScore, createdAt, updatedAt);

        // Act
        var csv = await ExportCsvUnderCultureAsync(CommaDecimalDottedDateCulture, exportRow);

        // Assert
        Assert.NotEqual(
            updatedAt.ToString(CultureInfo.InvariantCulture),
            updatedAt.ToString(CommaDecimalDottedDateCulture));
        Assert.Contains(
            confidenceScore.ToString(CultureInfo.InvariantCulture),
            csv,
            StringComparison.Ordinal);
        Assert.Contains(
            createdAt.ToString(CultureInfo.InvariantCulture),
            csv,
            StringComparison.Ordinal);
        Assert.Contains(
            updatedAt.ToString(CultureInfo.InvariantCulture),
            csv,
            StringComparison.Ordinal);
    }

    private static async Task<string> ExportCsvUnderCultureAsync(CultureInfo culture, object[] exportRow)
    {
        var table = BuildExportTable();
        table.Rows.Add(exportRow);
        var conn = new FakeDbConnection();
        conn.Enqueue(FakeDbCommand.WithReader(table));
        var (service, _) = BuildService(conn);

        var originalCulture = CultureInfo.CurrentCulture;
        CultureInfo.CurrentCulture = culture;
        try
        {
            return await service.ExportCsvAsync(TestContext.Current.CancellationToken);
        }
        finally
        {
            CultureInfo.CurrentCulture = originalCulture;
        }
    }

    private static string BuildTwoRowCsv(string firstChurchName, string secondChurchName)
    {
        var firstState = TestValues.NewStateCode();
        var secondState = TestValues.NewStateCode();
        return $"{MinimalCsvHeader}\n{firstChurchName},{firstState}\n{secondChurchName},{secondState}";
    }

    private static (AdminService Service, Mock<ServiceBusSender> Sender) BuildService(FakeDbConnection connection)
    {
        var sender = new Mock<ServiceBusSender>(MockBehavior.Strict);
        sender.Setup(s => s.SendMessageAsync(It.IsAny<ServiceBusMessage>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        sender.Setup(s => s.DisposeAsync()).Returns(ValueTask.CompletedTask);

        var busClient = new Mock<ServiceBusClient>(MockBehavior.Strict);
        busClient.Setup(c => c.CreateSender(ServiceBusNames.GeocodingRequests)).Returns(sender.Object);

        var busFactory = new Mock<IAzureClientFactory<ServiceBusClient>>(MockBehavior.Strict);
        busFactory.Setup(f => f.CreateClient(ServiceBusNames.Client)).Returns(busClient.Object);

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
        t.Columns.Add("CreatedAt", typeof(DateTimeOffset));
        t.Columns.Add("UpdatedAt", typeof(DateTimeOffset));
        return t;
    }

    private static object[] ExportRow(string canonicalName) =>
        ExportRow(
            canonicalName,
            TestValues.NewConfidenceScore(),
            TestValues.NewUtcTimestamp(),
            TestValues.NewUtcTimestamp());

    private static object[] ExportRow(
        string canonicalName,
        decimal confidenceScore,
        DateTimeOffset createdAt,
        DateTimeOffset updatedAt)
    {
        var churchId = Guid.NewGuid();
        var slug = TestValues.NewSlug();
        var street = TestValues.NewStreet();
        var city = TestValues.NewCity();
        var state = TestValues.NewStateCode();
        var zip = TestValues.NewZip();
        var worshipStyle = TestValues.NewWorshipStyle();
        var primaryLanguage = TestValues.NewLanguage();
        return
        [
            churchId, canonicalName, slug, street, city, state, zip,
            DBNull.Value, DBNull.Value, DBNull.Value, (int)worshipStyle, primaryLanguage,
            true, false, true, false, confidenceScore, createdAt, updatedAt,
        ];
    }
}
