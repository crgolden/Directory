namespace Directory.Tests.Unit.TestSupport;

using System.Data;
using System.Data.Common;
using System.Diagnostics.CodeAnalysis;

internal sealed class FakeDbConnection : DbConnection
{
    private readonly Queue<FakeDbCommand> _commandQueue = new();
    private ConnectionState _state = ConnectionState.Closed;
    private string? _connectionString;

    public List<FakeDbCommand> ExecutedCommands { get; } = [];

    public FakeDbTransaction? LastTransaction { get; private set; }

    [AllowNull]
    public override string ConnectionString
    {
        get => _connectionString ?? throw FakeDbMember.NotSet(nameof(ConnectionString), nameof(FakeDbConnection));
        set => _connectionString = value;
    }

    public override ConnectionState State => _state;

    public override string Database => nameof(FakeDbConnection);

    public override string DataSource => nameof(FakeDbConnection);

    public override string ServerVersion => nameof(FakeDbConnection);

    public void Enqueue(FakeDbCommand cmd) => _commandQueue.Enqueue(cmd);

    public void Reset()
    {
        _commandQueue.Clear();
        ExecutedCommands.Clear();
        _state = ConnectionState.Closed;
    }

    public override void Open() => _state = ConnectionState.Open;

    public override void Close() => _state = ConnectionState.Closed;

    public override void ChangeDatabase(string databaseName)
    {
    }

    protected override DbCommand CreateDbCommand()
    {
        var cmd = _commandQueue.Count > 0 ? _commandQueue.Dequeue() : new FakeDbCommand();
        cmd.Connection = this;
        ExecutedCommands.Add(cmd);
        return cmd;
    }

    protected override DbTransaction BeginDbTransaction(IsolationLevel isolationLevel)
    {
        LastTransaction = new FakeDbTransaction(this, isolationLevel);
        return LastTransaction;
    }
}
