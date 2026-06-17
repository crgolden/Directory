namespace Directory.Tests.Api;

using System.Data;
using Church;
using Entities;
using TestSupport;

public sealed class ChurchServiceTests
{
    [Fact]
    [Trait("Category", "Unit")]
    public async Task DeleteAsync_ReturnsFalse_WhenNoRowsAffected()
    {
        var conn = new FakeDbConnection();
        conn.Enqueue(FakeDbCommand.WithNonQueryResult(0));
        var service = new ChurchService(conn);

        var result = await service.DeleteAsync(Guid.NewGuid(), TestContext.Current.CancellationToken);

        Assert.False(result);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task DeleteAsync_ReturnsTrue_WhenRowDeleted()
    {
        var conn = new FakeDbConnection();
        conn.Enqueue(FakeDbCommand.WithNonQueryResult(1));
        var service = new ChurchService(conn);

        var result = await service.DeleteAsync(Guid.NewGuid(), TestContext.Current.CancellationToken);

        Assert.True(result);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task UpdateAsync_ReturnsFalse_WhenNoRowsAffected()
    {
        var conn = new FakeDbConnection();
        conn.Enqueue(FakeDbCommand.WithNonQueryResult(0));
        var service = new ChurchService(conn);

        var result = await service.UpdateAsync(BuildChurch(), TestContext.Current.CancellationToken);

        Assert.False(result);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task UpdateAsync_ReturnsTrue_WhenRowUpdated()
    {
        var conn = new FakeDbConnection();
        conn.Enqueue(FakeDbCommand.WithNonQueryResult(1));
        var service = new ChurchService(conn);

        var result = await service.UpdateAsync(BuildChurch(), TestContext.Current.CancellationToken);

        Assert.True(result);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task GetBySlugAsync_ReturnsNull_WhenNoRows()
    {
        var conn = new FakeDbConnection();
        conn.Enqueue(FakeDbCommand.WithReader(new DataTable()));
        var service = new ChurchService(conn);

        var result = await service.GetBySlugAsync("some-slug", TestContext.Current.CancellationToken);

        Assert.Null(result);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task CreateAsync_GeneratesKebabCaseSlug()
    {
        var conn = new FakeDbConnection();

        // slug existence check → 0 means slug is free
        conn.Enqueue(FakeDbCommand.WithScalarResult(0));

        // INSERT
        conn.Enqueue(FakeDbCommand.WithNonQueryResult(1));

        var service = new ChurchService(conn);
        var church = BuildChurch();
        church.Slug = string.Empty;

        var result = await service.CreateAsync(church, TestContext.Current.CancellationToken);

        Assert.Equal("grace-church-phoenix-az", result.Slug);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task CreateAsync_AppendsSuffix_WhenSlugCollides()
    {
        var conn = new FakeDbConnection();

        // first check: slug exists
        conn.Enqueue(FakeDbCommand.WithScalarResult(1));

        // second check: slug-2 is free
        conn.Enqueue(FakeDbCommand.WithScalarResult(0));

        // INSERT
        conn.Enqueue(FakeDbCommand.WithNonQueryResult(1));

        var service = new ChurchService(conn);
        var church = BuildChurch();
        church.Slug = string.Empty;

        var result = await service.CreateAsync(church, TestContext.Current.CancellationToken);

        Assert.Equal("grace-church-phoenix-az-2", result.Slug);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task GetPageAsync_NoRows_ReturnsEmptyAndOpensConnection()
    {
        var conn = new FakeDbConnection();
        conn.Enqueue(FakeDbCommand.WithReader(BuildChurchTable(includeTotalCount: true)));
        var service = new ChurchService(conn);

        var (items, totalCount) = await service.GetPageAsync(1, 20, TestContext.Current.CancellationToken);

        Assert.Empty(items);
        Assert.Equal(0, totalCount);
        Assert.Equal(ConnectionState.Open, conn.State);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task GetPageAsync_WithRows_MapsItemsAndReadsTotalCount()
    {
        var table = BuildChurchTable(includeTotalCount: true);
        table.Rows.Add(PopulatedRow(totalCount: 5));
        var conn = new FakeDbConnection();
        conn.Enqueue(FakeDbCommand.WithReader(table));
        var service = new ChurchService(conn);

        var (items, totalCount) = await service.GetPageAsync(1, 20, TestContext.Current.CancellationToken);

        Assert.Single(items);
        Assert.Equal(5, totalCount);
        Assert.Equal("Grace Church", items[0].CanonicalName);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task GetBySlugAsync_RowWithNullableNulls_MapsNullsForOptionalColumns()
    {
        var table = BuildChurchTable(includeTotalCount: false);
        table.Rows.Add(NullableNullRow());
        var conn = new FakeDbConnection();
        conn.Enqueue(FakeDbCommand.WithReader(table));
        var service = new ChurchService(conn);

        var result = await service.GetBySlugAsync("grace-church", TestContext.Current.CancellationToken);

        Assert.NotNull(result);
        Assert.Null(result.Street);
        Assert.Null(result.PhoneNumber);
        Assert.Null(result.DenominationId);
        Assert.Null(result.AcceptsLGBTQ);
        Assert.Null(result.LastVerifiedAt);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task GetByIdAsync_NoRow_ReturnsNull()
    {
        var conn = new FakeDbConnection();
        conn.Enqueue(FakeDbCommand.WithReader(BuildChurchTable(includeTotalCount: false)));
        var service = new ChurchService(conn);

        var result = await service.GetByIdAsync(Guid.NewGuid(), TestContext.Current.CancellationToken);

        Assert.Null(result);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task GetByIdAsync_RowPopulated_MapsAllOptionalColumns()
    {
        var table = BuildChurchTable(includeTotalCount: false);
        table.Rows.Add(PopulatedRow(totalCount: null));
        var conn = new FakeDbConnection();
        await conn.OpenAsync(TestContext.Current.CancellationToken);
        conn.Enqueue(FakeDbCommand.WithReader(table));
        var service = new ChurchService(conn);

        var result = await service.GetByIdAsync(Guid.NewGuid(), TestContext.Current.CancellationToken);

        Assert.NotNull(result);
        Assert.Equal("123 Main", result.Street);
        Assert.Equal("602-555-1212", result.PhoneNumber);
        Assert.NotNull(result.DenominationId);
        Assert.True(result.AcceptsLGBTQ is true);
        Assert.NotNull(result.LastVerifiedAt);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task ExistsAsync_ScalarPositive_ReturnsTrue()
    {
        var conn = new FakeDbConnection();
        conn.Enqueue(FakeDbCommand.WithScalarResult(1));
        var service = new ChurchService(conn);

        var result = await service.ExistsAsync(Guid.NewGuid(), TestContext.Current.CancellationToken);

        Assert.True(result);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task ExistsAsync_ScalarZero_ReturnsFalse()
    {
        var conn = new FakeDbConnection();
        conn.Enqueue(FakeDbCommand.WithScalarResult(0));
        var service = new ChurchService(conn);

        var result = await service.ExistsAsync(Guid.NewGuid(), TestContext.Current.CancellationToken);

        Assert.False(result);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task RecalculateConfidenceAsync_ChurchNotFound_DoesNotUpdate()
    {
        var conn = new FakeDbConnection();
        conn.Enqueue(FakeDbCommand.WithReader(BuildChurchTable(includeTotalCount: false)));
        var service = new ChurchService(conn);

        await service.RecalculateConfidenceAsync(Guid.NewGuid(), TestContext.Current.CancellationToken);

        Assert.Single(conn.ExecutedCommands);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task RecalculateConfidenceAsync_ChurchFound_CalculatesAndUpdates()
    {
        var table = BuildChurchTable(includeTotalCount: false);
        table.Rows.Add(PopulatedRow(totalCount: null));
        var conn = new FakeDbConnection();
        conn.Enqueue(FakeDbCommand.WithReader(table));
        conn.Enqueue(FakeDbCommand.WithScalarResult(5));
        conn.Enqueue(FakeDbCommand.WithNonQueryResult(1));
        var service = new ChurchService(conn);

        await service.RecalculateConfidenceAsync(Guid.NewGuid(), TestContext.Current.CancellationToken);

        Assert.Equal(3, conn.ExecutedCommands.Count);
        Assert.Contains("UPDATE [dbo].[Directory]", conn.ExecutedCommands[2].CommandText, StringComparison.Ordinal);
        Assert.Contains("@ConfidenceScore", conn.ExecutedCommands[2].CommandText, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task RecalculateConfidenceAsync_AttributeCountNotInt_FallsBackToZero()
    {
        var table = BuildChurchTable(includeTotalCount: false);
        table.Rows.Add(PopulatedRow(totalCount: null));
        var conn = new FakeDbConnection();
        conn.Enqueue(FakeDbCommand.WithReader(table));
        conn.Enqueue(FakeDbCommand.WithScalarResult(3L));
        conn.Enqueue(FakeDbCommand.WithNonQueryResult(1));
        var service = new ChurchService(conn);

        await service.RecalculateConfidenceAsync(Guid.NewGuid(), TestContext.Current.CancellationToken);

        Assert.Equal(3, conn.ExecutedCommands.Count);
        Assert.Contains("UPDATE [dbo].[Directory]", conn.ExecutedCommands[2].CommandText, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task CreateAsync_FullyPopulatedChurch_BindsOptionalValues()
    {
        var conn = new FakeDbConnection();
        conn.Enqueue(FakeDbCommand.WithScalarResult(0)); // slug free
        conn.Enqueue(FakeDbCommand.WithNonQueryResult(1)); // insert
        var service = new ChurchService(conn);
        var church = BuildChurch();
        church.Street = "123 Main";
        church.PhoneNumber = "602-555-1212";
        church.Website = "https://grace.example";
        church.EmailAddress = "hi@grace.example";
        church.DenominationId = Guid.NewGuid();
        church.AcceptsLGBTQ = true;
        church.WheelchairAccessible = true;
        church.HasNursery = true;
        church.HasYouthProgram = true;
        church.LastVerifiedAt = DateTimeOffset.UtcNow;

        await service.CreateAsync(church, TestContext.Current.CancellationToken);

        var insert = conn.ExecutedCommands[1];
        Assert.Equal("123 Main", insert.Parameters["@Street"].Value);
        Assert.True(insert.Parameters["@AcceptsLGBTQ"].Value is true);
        Assert.NotEqual(DBNull.Value, insert.Parameters["@DenominationId"].Value);
        Assert.NotEqual(DBNull.Value, insert.Parameters["@LastVerifiedAt"].Value);
    }

    private static Church BuildChurch() => new Church
    {
        CanonicalName = "Grace Church",
        Slug = "grace-church-phoenix-az",
        Latitude = 33.4,
        Longitude = -112.0,
        City = "Phoenix",
        State = "AZ",
        Zip = "85001",
        PrimaryLanguage = "English",
    };

    private static DataTable BuildChurchTable(bool includeTotalCount)
    {
        var t = new DataTable();
        t.Columns.Add("Id", typeof(Guid));
        t.Columns.Add("CanonicalName", typeof(string));
        t.Columns.Add("Slug", typeof(string));
        t.Columns.Add("Latitude", typeof(double));
        t.Columns.Add("Longitude", typeof(double));
        t.Columns.Add("Street", typeof(string));
        t.Columns.Add("City", typeof(string));
        t.Columns.Add("State", typeof(string));
        t.Columns.Add("Zip", typeof(string));
        t.Columns.Add("PhoneNumber", typeof(string));
        t.Columns.Add("Website", typeof(string));
        t.Columns.Add("EmailAddress", typeof(string));
        t.Columns.Add("DenominationId", typeof(Guid));
        t.Columns.Add("WorshipStyle", typeof(int));
        t.Columns.Add("PrimaryLanguage", typeof(string));
        t.Columns.Add("AcceptsLGBTQ", typeof(bool));
        t.Columns.Add("WheelchairAccessible", typeof(bool));
        t.Columns.Add("HasNursery", typeof(bool));
        t.Columns.Add("HasYouthProgram", typeof(bool));
        t.Columns.Add("ConfidenceScore", typeof(decimal));
        t.Columns.Add("LastVerifiedAt", typeof(DateTime));
        t.Columns.Add("CreatedAt", typeof(DateTime));
        t.Columns.Add("UpdatedAt", typeof(DateTime));
        t.Columns.Add("IsActive", typeof(bool));
        if (includeTotalCount)
        {
            t.Columns.Add("TotalCount", typeof(int));
        }

        return t;
    }

    private static object[] PopulatedRow(int? totalCount)
    {
        var values = new List<object>
        {
            Guid.NewGuid(), "Grace Church", "grace-church", 33.4, -112.0, "123 Main",
            "Phoenix", "AZ", "85001", "602-555-1212", "https://grace.example", "hi@grace.example",
            Guid.NewGuid(), 1, "English", true, true, true, true, 0.9m,
            DateTime.UtcNow, DateTime.UtcNow, DateTime.UtcNow, true,
        };
        if (totalCount.HasValue)
        {
            values.Add(totalCount.Value);
        }

        return [.. values];
    }

    private static object[] NullableNullRow() =>
    [
        Guid.NewGuid(), "Grace Church", "grace-church", 33.4, -112.0, DBNull.Value,
        "Phoenix", "AZ", "85001", DBNull.Value, DBNull.Value, DBNull.Value,
        DBNull.Value, 1, "English", DBNull.Value, DBNull.Value, DBNull.Value, DBNull.Value, 0.9m,
        DBNull.Value, DateTime.UtcNow, DateTime.UtcNow, true,
    ];
}
