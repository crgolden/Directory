namespace Directory.Tests.Unit.Api;

using System.Data;
using System.Globalization;
using Admin;
using Azure.Messaging.ServiceBus;
using Entities;
using Messaging;
using Microsoft.Extensions.Azure;
using Moq;
using TestSupport;
using static AdminCsvFixtureConstants;

public sealed class AdminServiceTests
{
    [Fact]
    [Trait("Category", "Unit")]
    public void ParseCsv_SingleRow_MapsAllFields()
    {
        // Arrange
        var canonicalName = TestValues.NewName();
        var street = TestValues.NewStreet();
        var city = TestValues.NewCity();
        var state = TestValues.NewStateCodeText();
        var zip = TestValues.NewZip();
        var phoneNumber = TestValues.NewPhoneNumber();
        var website = TestValues.NewWebsite();
        var emailAddress = TestValues.NewEmailAddress();
        var csv = string.Join(
            CsvLineSeparator,
            FullCsvHeader(),
            string.Join(CsvFieldSeparator, canonicalName, street, city, state, zip, phoneNumber, website, emailAddress));

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
        var state = TestValues.NewStateCodeText();
        var csv = string.Join(
            CsvLineSeparator,
            MinimalCsvHeader(),
            string.Join(CsvFieldSeparator, TestValues.NewBlank(), state));

        // Act
        var rows = AdminService.ParseCsv(csv).ToList();

        // Assert
        Assert.Empty(rows);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void ParseCsv_StateIsNotAUspsCode_SkipsRow()
    {
        // Arrange
        var canonicalName = TestValues.NewName();
        var unparseableState = TestValues.NewUnparseableStateCode();
        var csv = string.Join(
            CsvLineSeparator,
            MinimalCsvHeader(),
            string.Join(CsvFieldSeparator, canonicalName, unparseableState));

        // Act
        var rows = AdminService.ParseCsv(csv).ToList();

        // Assert
        Assert.Empty(rows);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void ParseCsv_StateIsAUspsCode_KeepsRow()
    {
        // Arrange
        var canonicalName = TestValues.NewName();
        var state = TestValues.NewStateCodeText();
        var csv = string.Join(
            CsvLineSeparator,
            MinimalCsvHeader(),
            string.Join(CsvFieldSeparator, canonicalName, state));

        // Act
        var rows = AdminService.ParseCsv(csv).ToList();

        // Assert
        var row = Assert.Single(rows);
        Assert.Equal(canonicalName, row.CanonicalName);
        Assert.Equal(state, row.State);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void ParseCsv_MissingStateColumn_SkipsRow()
    {
        // Arrange
        var canonicalName = TestValues.NewName();
        var csv = string.Join(
            CsvLineSeparator,
            MinimalCsvHeader(),
            string.Join(CsvFieldSeparator, canonicalName, TestValues.NewBlank()));

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
        Assert.Empty(AdminService.ParseCsv(MinimalCsvHeader()));
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void ParseCsv_MultipleRows_ParsesAll()
    {
        // Arrange
        var churchNames = new[] { TestValues.NewName(), TestValues.NewName() };
        var csv = BuildCsv(churchNames);

        // Act
        var rows = AdminService.ParseCsv(csv).ToList();

        // Assert
        Assert.Equal(churchNames.Length, rows.Count);
        Assert.Equal(churchNames[0], rows[0].CanonicalName);
        Assert.Equal(churchNames[1], rows[1].CanonicalName);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task ImportCsvAsync_TwoRows_PublishesTwo()
    {
        // Arrange
        var churchNames = new[] { TestValues.NewName(), TestValues.NewName() };
        var csv = BuildCsv(churchNames);
        var (service, sender) = BuildService(new FakeDbConnection());

        // Act
        var published = await service.ImportCsvAsync(csv, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(churchNames.Length, published);
        sender.Verify(
            s => s.SendMessageAsync(It.IsAny<ServiceBusMessage>(), It.IsAny<CancellationToken>()),
            Times.Exactly(churchNames.Length));
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
        Assert.StartsWith(AdminService.ExportHeader, csv, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task ExportCsvAsync_HasRows_RowCountMatchesDataTable()
    {
        // Arrange
        var churchNames = new[] { TestValues.NewName(), TestValues.NewName() };
        var table = BuildExportTable();
        table.Rows.Add(ExportRow(churchNames[0]));
        table.Rows.Add(ExportRow(churchNames[1]));
        var conn = new FakeDbConnection();
        conn.Enqueue(FakeDbCommand.WithReader(table));
        var (service, _) = BuildService(conn);

        // Act
        var csv = await service.ExportCsvAsync(TestContext.Current.CancellationToken);
        var lines = csv.Split(CsvLineSeparator, StringSplitOptions.RemoveEmptyEntries);

        // Assert
        Assert.Equal(churchNames.Length + CsvHeaderLineCount, lines.Length);
        Assert.Contains(churchNames[0], lines[CsvHeaderLineCount], StringComparison.Ordinal);
        Assert.Contains(churchNames[1], lines[CsvHeaderLineCount + 1], StringComparison.Ordinal);
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

    private static string FullCsvHeader() => string.Join(
        CsvFieldSeparator,
        nameof(ImportRow.CanonicalName),
        nameof(ImportRow.Street),
        nameof(ImportRow.City),
        nameof(ImportRow.State),
        nameof(ImportRow.Zip),
        nameof(ImportRow.PhoneNumber),
        nameof(ImportRow.Website),
        nameof(ImportRow.EmailAddress));

    private static string MinimalCsvHeader() => string.Join(
        CsvFieldSeparator,
        nameof(ImportRow.CanonicalName),
        nameof(ImportRow.State));

    private static string BuildCsv(string[] churchNames)
    {
        var lines = new List<string> { MinimalCsvHeader() };
        foreach (var churchName in churchNames)
        {
            lines.Add(string.Join(CsvFieldSeparator, churchName, TestValues.NewStateCodeText()));
        }

        return string.Join(CsvLineSeparator, lines);
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
        t.Columns.Add(nameof(Church.Id), typeof(Guid));
        t.Columns.Add(nameof(Church.CanonicalName), typeof(string));
        t.Columns.Add(nameof(Church.Slug), typeof(string));
        t.Columns.Add(nameof(Church.Street), typeof(string));
        t.Columns.Add(nameof(Church.City), typeof(string));
        t.Columns.Add(nameof(Church.State), typeof(string));
        t.Columns.Add(nameof(Church.Zip), typeof(string));
        t.Columns.Add(nameof(Church.PhoneNumber), typeof(string));
        t.Columns.Add(nameof(Church.Website), typeof(string));
        t.Columns.Add(nameof(Church.EmailAddress), typeof(string));
        t.Columns.Add(nameof(Church.WorshipStyle), typeof(int));
        t.Columns.Add(nameof(Church.PrimaryLanguage), typeof(string));
        t.Columns.Add(nameof(Church.AcceptsLGBTQ), typeof(bool));
        t.Columns.Add(nameof(Church.WheelchairAccessible), typeof(bool));
        t.Columns.Add(nameof(Church.HasNursery), typeof(bool));
        t.Columns.Add(nameof(Church.HasYouthProgram), typeof(bool));
        t.Columns.Add(nameof(Church.ConfidenceScore), typeof(decimal));
        t.Columns.Add(nameof(Church.CreatedAt), typeof(DateTimeOffset));
        t.Columns.Add(nameof(Church.UpdatedAt), typeof(DateTimeOffset));
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
        var state = TestValues.NewStateCodeText();
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
