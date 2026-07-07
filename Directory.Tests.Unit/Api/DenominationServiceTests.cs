namespace Directory.Tests.Unit.Api;

using System.Data;
using Denomination;
using TestSupport;

public sealed class DenominationServiceTests
{
    [Fact]
    [Trait("Category", "Unit")]
    public async Task GetAllAsync_ConnectionClosed_OpensAndReturnsRows()
    {
        // Arrange — reader returns two denominations; connection starts Closed
        var table = new DataTable();
        table.Columns.Add("Id", typeof(Guid));
        table.Columns.Add("Name", typeof(string));
        var id1 = Guid.NewGuid();
        var id2 = Guid.NewGuid();
        table.Rows.Add(id1, "Baptist");
        table.Rows.Add(id2, "Methodist");

        var conn = new FakeDbConnection();
        conn.Enqueue(FakeDbCommand.WithReader(table));
        var service = new DenominationService(conn);

        // Act
        var result = await service.GetAllAsync(TestContext.Current.CancellationToken);

        // Assert — connection opened; two rows returned in order
        Assert.Equal(System.Data.ConnectionState.Open, conn.State);
        Assert.Equal(2, result.Count);
        Assert.Equal("Baptist", result[0].Name);
        Assert.Equal(id1, result[0].Id);
        Assert.Equal("Methodist", result[1].Name);
        Assert.Equal(id2, result[1].Id);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task GetAllAsync_ConnectionAlreadyOpen_DoesNotReopenOrFail()
    {
        // Arrange — connection already Open; reader has one row
        var table = new DataTable();
        table.Columns.Add("Id", typeof(Guid));
        table.Columns.Add("Name", typeof(string));
        table.Rows.Add(Guid.NewGuid(), "Lutheran");

        var conn = new FakeDbConnection();
        await conn.OpenAsync(TestContext.Current.CancellationToken);
        conn.Enqueue(FakeDbCommand.WithReader(table));
        var service = new DenominationService(conn);

        // Act
        var result = await service.GetAllAsync(TestContext.Current.CancellationToken);

        // Assert
        Assert.Single(result);
        Assert.Equal("Lutheran", result[0].Name);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task GetAllAsync_EmptyTable_ReturnsEmptyList()
    {
        // Arrange
        var table = new DataTable();
        table.Columns.Add("Id", typeof(Guid));
        table.Columns.Add("Name", typeof(string));

        var conn = new FakeDbConnection();
        conn.Enqueue(FakeDbCommand.WithReader(table));
        var service = new DenominationService(conn);

        // Act
        var result = await service.GetAllAsync(TestContext.Current.CancellationToken);

        // Assert
        Assert.Empty(result);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task GetAllAsync_OrdersByNameAscending()
    {
        // Arrange — DataTable rows in reverse order; the ORDER BY is in SQL so our reader reflects that order
        var table = new DataTable();
        table.Columns.Add("Id", typeof(Guid));
        table.Columns.Add("Name", typeof(string));
        table.Rows.Add(Guid.NewGuid(), "Anglican");
        table.Rows.Add(Guid.NewGuid(), "Baptist");
        table.Rows.Add(Guid.NewGuid(), "Catholic");

        var conn = new FakeDbConnection();
        conn.Enqueue(FakeDbCommand.WithReader(table));
        var service = new DenominationService(conn);

        // Act
        var result = await service.GetAllAsync(TestContext.Current.CancellationToken);

        // Assert — the service returns whatever order the DB gives; ORDER BY is verified in the SQL text
        Assert.Equal(3, result.Count);
        var cmd = Assert.Single(conn.ExecutedCommands);
        Assert.Contains("ORDER BY [Name] ASC", cmd.CommandText, StringComparison.Ordinal);
    }
}
