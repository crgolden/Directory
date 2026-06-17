namespace Directory.Tests.Api;

using System.Data;
using System.Net;
using System.Net.Http.Json;
using TestSupport;

public sealed class ModerationEndpointsTests : IClassFixture<DirectoryWebApplicationFactory>
{
    private readonly DirectoryWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public ModerationEndpointsTests(DirectoryWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task GetCorrections_ReturnsOk()
    {
        _factory.FakeDb.Reset();
        _factory.FakeDb.Enqueue(FakeDbCommand.WithReader(CorrectionTable(includeTotalCount: true)));

        var response = await _client.GetAsync("/corrections?page=1&pageSize=10", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task GetCorrections_ClampsPagination_WhenOutOfRange()
    {
        _factory.FakeDb.Reset();
        _factory.FakeDb.Enqueue(FakeDbCommand.WithReader(CorrectionTable(includeTotalCount: true)));

        var response = await _client.GetAsync("/corrections?page=0&pageSize=200", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task GetCorrectionById_ReturnsOk_WhenFound()
    {
        _factory.FakeDb.Reset();
        var table = CorrectionTable(includeTotalCount: false);
        table.Rows.Add(CorrectionRow());
        _factory.FakeDb.Enqueue(FakeDbCommand.WithReader(table));

        var response = await _client.GetAsync($"/corrections/{Guid.NewGuid()}", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task GetCorrectionById_ReturnsNotFound_WhenMissing()
    {
        _factory.FakeDb.Reset();
        _factory.FakeDb.Enqueue(FakeDbCommand.WithReader(CorrectionTable(includeTotalCount: false)));

        var response = await _client.GetAsync($"/corrections/{Guid.NewGuid()}", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task SubmitCorrection_ReturnsAccepted_WhenChurchExists()
    {
        _factory.FakeDb.Reset();
        _factory.FakeDb.Enqueue(FakeDbCommand.WithScalarResult(1)); // ExistsAsync → found

        var body = new { ChurchId = Guid.NewGuid(), Field = "PhoneNumber", OldValue = (string?)null, NewValue = "602-555-1212" };
        var response = await _client.PostAsJsonAsync("/corrections", body, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task SubmitCorrection_ReturnsNotFound_WhenChurchMissing()
    {
        _factory.FakeDb.Reset();
        _factory.FakeDb.Enqueue(FakeDbCommand.WithScalarResult(0)); // ExistsAsync → not found

        var body = new { ChurchId = Guid.NewGuid(), Field = "PhoneNumber", OldValue = (string?)null, NewValue = "602-555-1212" };
        var response = await _client.PostAsJsonAsync("/corrections", body, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task ApproveCorrection_ReturnsNoContent_WhenUpdated()
    {
        _factory.FakeDb.Reset();
        _factory.FakeDb.Enqueue(FakeDbCommand.WithNonQueryResult(1));

        var response = await _client.PatchAsync($"/corrections/{Guid.NewGuid()}/approve", null, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task ApproveCorrection_ReturnsNotFound_WhenMissing()
    {
        _factory.FakeDb.Reset();
        _factory.FakeDb.Enqueue(FakeDbCommand.WithNonQueryResult(0));

        var response = await _client.PatchAsync($"/corrections/{Guid.NewGuid()}/approve", null, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task RejectCorrection_ReturnsNoContent_WhenUpdated()
    {
        _factory.FakeDb.Reset();
        _factory.FakeDb.Enqueue(FakeDbCommand.WithNonQueryResult(1));

        var response = await _client.PatchAsync($"/corrections/{Guid.NewGuid()}/reject", null, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task RejectCorrection_ReturnsNotFound_WhenMissing()
    {
        _factory.FakeDb.Reset();
        _factory.FakeDb.Enqueue(FakeDbCommand.WithNonQueryResult(0));

        var response = await _client.PatchAsync($"/corrections/{Guid.NewGuid()}/reject", null, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task MergeChurches_ReturnsNoContent()
    {
        _factory.FakeDb.Reset();
        for (var i = 0; i < 8; i++)
        {
            _factory.FakeDb.Enqueue(FakeDbCommand.WithNonQueryResult(1));
        }

        var survivingId = Guid.NewGuid();
        var absorbedId = Guid.NewGuid();
        var response = await _client.PostAsync($"/churches/{survivingId}/merge/{absorbedId}", null, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    private static DataTable CorrectionTable(bool includeTotalCount)
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

    private static object[] CorrectionRow() =>
    [
        Guid.NewGuid(), Guid.NewGuid(), "some-user-id", "PhoneNumber", DBNull.Value, "602-555-1212",
        0, DBNull.Value, DBNull.Value, DateTime.UtcNow, DBNull.Value,
    ];
}
