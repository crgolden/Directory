namespace Directory.Tests.Api;

using System.Data;
using System.Net;
using System.Net.Http.Json;
using Enums;
using TestSupport;

public sealed class ChurchEndpointsTests : IClassFixture<DirectoryWebApplicationFactory>
{
    private readonly DirectoryWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public ChurchEndpointsTests(DirectoryWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task GetChurches_ReturnsOk_WithEmptyList()
    {
        _factory.FakeDb.Reset();
        _factory.FakeDb.Enqueue(FakeDbCommand.WithReader(ChurchTable(includeTotalCount: true)));

        var response = await _client.GetAsync("/churches?page=1&pageSize=10", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task GetChurches_ClampsPagination_WhenOutOfRange()
    {
        _factory.FakeDb.Reset();
        _factory.FakeDb.Enqueue(FakeDbCommand.WithReader(ChurchTable(includeTotalCount: true)));

        var response = await _client.GetAsync("/churches?page=0&pageSize=200", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task GetChurchBySlug_ReturnsOk_WhenFound()
    {
        _factory.FakeDb.Reset();
        var table = ChurchTable(includeTotalCount: false);
        table.Rows.Add(ChurchRow());
        _factory.FakeDb.Enqueue(FakeDbCommand.WithReader(table));

        var response = await _client.GetAsync("/churches/grace-church", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task GetChurchBySlug_ReturnsNotFound_WhenMissing()
    {
        _factory.FakeDb.Reset();
        _factory.FakeDb.Enqueue(FakeDbCommand.WithReader(ChurchTable(includeTotalCount: false)));

        var response = await _client.GetAsync("/churches/no-such-slug", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task CreateChurch_ReturnsCreated()
    {
        _factory.FakeDb.Reset();
        _factory.FakeDb.Enqueue(FakeDbCommand.WithScalarResult(0)); // slug free
        _factory.FakeDb.Enqueue(FakeDbCommand.WithNonQueryResult(1)); // insert

        var body = new
        {
            CanonicalName = "Grace Church",
            Latitude = 33.4,
            Longitude = -112.0,
            Street = (string?)null,
            City = "Phoenix",
            State = "AZ",
            Zip = "85001",
            PhoneNumber = (string?)null,
            Website = (string?)null,
            EmailAddress = (string?)null,
            DenominationId = (Guid?)null,
            WorshipStyle = (int)WorshipStyle.Traditional,
            PrimaryLanguage = "English",
            AcceptsLGBTQ = (bool?)null,
            WheelchairAccessible = (bool?)null,
            HasNursery = (bool?)null,
            HasYouthProgram = (bool?)null,
        };
        var response = await _client.PostAsJsonAsync("/churches", body, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task UpdateChurch_ReturnsOk_WhenFound()
    {
        _factory.FakeDb.Reset();
        var table = ChurchTable(includeTotalCount: false);
        table.Rows.Add(ChurchRow());
        _factory.FakeDb.Enqueue(FakeDbCommand.WithReader(table)); // GetByIdAsync
        _factory.FakeDb.Enqueue(FakeDbCommand.WithNonQueryResult(1)); // UpdateAsync

        var id = Guid.NewGuid();
        var body = ChurchRequestBody();
        var response = await _client.PutAsJsonAsync($"/churches/{id}", body, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task UpdateChurch_ReturnsNotFound_WhenMissing()
    {
        _factory.FakeDb.Reset();
        _factory.FakeDb.Enqueue(FakeDbCommand.WithReader(ChurchTable(includeTotalCount: false))); // GetByIdAsync → no rows

        var response = await _client.PutAsJsonAsync($"/churches/{Guid.NewGuid()}", ChurchRequestBody(), TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task PatchChurch_ReturnsOk_WhenFound()
    {
        _factory.FakeDb.Reset();
        var table = ChurchTable(includeTotalCount: false);
        table.Rows.Add(ChurchRow());
        _factory.FakeDb.Enqueue(FakeDbCommand.WithReader(table)); // GetByIdAsync
        _factory.FakeDb.Enqueue(FakeDbCommand.WithNonQueryResult(1)); // UpdateAsync

        var patch = new { CanonicalName = "Grace Church Updated", City = "Tempe" };
        using var content = JsonContent.Create(patch);
        var response = await _client.PatchAsync($"/churches/{Guid.NewGuid()}", content, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task PatchChurch_ReturnsNotFound_WhenMissing()
    {
        _factory.FakeDb.Reset();
        _factory.FakeDb.Enqueue(FakeDbCommand.WithReader(ChurchTable(includeTotalCount: false)));

        var patch = new { CanonicalName = "Grace Church Updated" };
        using var content = JsonContent.Create(patch);
        var response = await _client.PatchAsync($"/churches/{Guid.NewGuid()}", content, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task DeleteChurch_ReturnsNoContent_WhenDeleted()
    {
        _factory.FakeDb.Reset();
        _factory.FakeDb.Enqueue(FakeDbCommand.WithNonQueryResult(1));

        var response = await _client.DeleteAsync($"/churches/{Guid.NewGuid()}", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task DeleteChurch_ReturnsNotFound_WhenMissing()
    {
        _factory.FakeDb.Reset();
        _factory.FakeDb.Enqueue(FakeDbCommand.WithNonQueryResult(0));

        var response = await _client.DeleteAsync($"/churches/{Guid.NewGuid()}", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    private static object ChurchRequestBody() => new
    {
        CanonicalName = "Grace Church",
        Latitude = 33.4,
        Longitude = -112.0,
        Street = (string?)null,
        City = "Phoenix",
        State = "AZ",
        Zip = "85001",
        PhoneNumber = (string?)null,
        Website = (string?)null,
        EmailAddress = (string?)null,
        DenominationId = (Guid?)null,
        WorshipStyle = (int)WorshipStyle.Traditional,
        PrimaryLanguage = "English",
        AcceptsLGBTQ = (bool?)null,
        WheelchairAccessible = (bool?)null,
        HasNursery = (bool?)null,
        HasYouthProgram = (bool?)null,
    };

    private static DataTable ChurchTable(bool includeTotalCount)
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

    private static object[] ChurchRow() =>
    [
        Guid.NewGuid(), "Grace Church", "grace-church", 33.4, -112.0, DBNull.Value,
        "Phoenix", "AZ", "85001", DBNull.Value, DBNull.Value, DBNull.Value,
        DBNull.Value, 1, "English", DBNull.Value, DBNull.Value, DBNull.Value, DBNull.Value, 0.9m,
        DBNull.Value, DateTime.UtcNow, DateTime.UtcNow, true,
    ];
}
