namespace Directory.Tests.Unit.TestSupport;

using System.Data;
using System.Data.Common;
using System.Diagnostics.CodeAnalysis;

internal sealed class FakeDbCommand : DbCommand
{
    private readonly FakeDbParameterCollection _parameters = new();
    private int _nonQueryResult;
    private object? _scalarResult;
    private DataTable[] _readerTables = [];
    private Exception? _throwOnExecute;

    public string? CapturedCommandText { get; private set; }

    [AllowNull]
    public override string CommandText
    {
        get => CapturedCommandText ?? throw FakeDbMember.NotSet(nameof(CommandText), nameof(FakeDbCommand));
        set => CapturedCommandText = value;
    }

    public override int CommandTimeout { get; set; }

    public override CommandType CommandType { get; set; }

    public override bool DesignTimeVisible { get; set; }

    public override UpdateRowSource UpdatedRowSource { get; set; }

    protected override DbConnection? DbConnection { get; set; }

    protected override DbParameterCollection DbParameterCollection => _parameters;

    protected override DbTransaction? DbTransaction { get; set; }

    public static FakeDbCommand WithNonQueryResult(int rowsAffected) => new() { _nonQueryResult = rowsAffected };

    public static FakeDbCommand WithScalarResult(object? value) => new() { _scalarResult = value };

    public static FakeDbCommand WithReader(DataTable table) => new() { _readerTables = [table] };

    public static FakeDbCommand WithReaders(params DataTable[] tables) => new() { _readerTables = tables };

    public static FakeDbCommand WithException(Exception toThrow) => new() { _throwOnExecute = toThrow };

    public override void Cancel()
    {
    }

    public override void Prepare()
    {
    }

    public override int ExecuteNonQuery() => _nonQueryResult;

    public override Task<int> ExecuteNonQueryAsync(CancellationToken cancellationToken) =>
        _throwOnExecute is not null
            ? Task.FromException<int>(_throwOnExecute)
            : Task.FromResult(_nonQueryResult);

    public override object? ExecuteScalar() => _scalarResult;

    public override Task<object?> ExecuteScalarAsync(CancellationToken cancellationToken) =>
        _throwOnExecute is not null
            ? Task.FromException<object?>(_throwOnExecute)
            : Task.FromResult(_scalarResult);

    protected override DbParameter CreateDbParameter() => new FakeDbParameter();

    protected override DbDataReader ExecuteDbDataReader(CommandBehavior behavior) =>
        new DataTableReader(ReaderTables());

    protected override Task<DbDataReader> ExecuteDbDataReaderAsync(CommandBehavior behavior, CancellationToken cancellationToken) =>
        _throwOnExecute is not null
            ? Task.FromException<DbDataReader>(_throwOnExecute)
            : Task.FromResult<DbDataReader>(new DataTableReader(ReaderTables()));

    private DataTable[] ReaderTables() => _readerTables.Length > 0 ? _readerTables : [new DataTable()];
}
