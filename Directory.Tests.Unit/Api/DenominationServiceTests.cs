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
        // Arrange
        var firstDenominationId = Guid.NewGuid();
        var firstDenominationName = TestValues.NewName();
        var secondDenominationId = Guid.NewGuid();
        var secondDenominationName = TestValues.NewName();
        var table = BuildDenominationTable();
        table.Rows.Add(firstDenominationId, firstDenominationName);
        table.Rows.Add(secondDenominationId, secondDenominationName);

        var conn = new FakeDbConnection();
        conn.Enqueue(FakeDbCommand.WithReader(table));
        var service = new DenominationService(conn);

        // Act
        var result = await service.GetAllAsync(TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(System.Data.ConnectionState.Open, conn.State);
        Assert.Equal(2, result.Count);
        Assert.Equal(firstDenominationName, result[0].Name);
        Assert.Equal(firstDenominationId, result[0].Id);
        Assert.Equal(secondDenominationName, result[1].Name);
        Assert.Equal(secondDenominationId, result[1].Id);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task GetAllAsync_ConnectionAlreadyOpen_DoesNotReopenOrFail()
    {
        // Arrange
        var denominationId = Guid.NewGuid();
        var denominationName = TestValues.NewName();
        var table = BuildDenominationTable();
        table.Rows.Add(denominationId, denominationName);

        var conn = new FakeDbConnection();
        await conn.OpenAsync(TestContext.Current.CancellationToken);
        conn.Enqueue(FakeDbCommand.WithReader(table));
        var service = new DenominationService(conn);

        // Act
        var result = await service.GetAllAsync(TestContext.Current.CancellationToken);

        // Assert
        Assert.Single(result);
        Assert.Equal(denominationName, result[0].Name);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task GetAllAsync_EmptyTable_ReturnsEmptyList()
    {
        // Arrange
        var table = BuildDenominationTable();

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
        // Arrange
        var table = BuildDenominationTable();
        table.Rows.Add(Guid.NewGuid(), TestValues.NewName());

        var conn = new FakeDbConnection();
        conn.Enqueue(FakeDbCommand.WithReader(table));
        var service = new DenominationService(conn);

        // Act
        await service.GetAllAsync(TestContext.Current.CancellationToken);

        // Assert
        var cmd = Assert.Single(conn.ExecutedCommands);
        Assert.Contains("ORDER BY [Name] ASC", cmd.CommandText, StringComparison.Ordinal);
    }

    private static DataTable BuildDenominationTable()
    {
        var table = new DataTable();
        table.Columns.Add("Id", typeof(Guid));
        table.Columns.Add("Name", typeof(string));
        return table;
    }
}
