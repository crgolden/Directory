namespace Directory.Tests.Unit.TestSupport;

using System.Data;
using System.Data.Common;

internal sealed class FakeDbTransaction : DbTransaction
{
    public FakeDbTransaction(DbConnection connection, IsolationLevel isolationLevel)
    {
        DbConnection = connection;
        IsolationLevel = isolationLevel;
    }

    public override IsolationLevel IsolationLevel { get; }

    public bool Committed { get; private set; }

    public bool RolledBack { get; private set; }

    protected override DbConnection DbConnection { get; }

    public override void Commit() => Committed = true;

    public override void Rollback() => RolledBack = true;

    public override Task CommitAsync(CancellationToken cancellationToken = default)
    {
        Committed = true;
        return Task.CompletedTask;
    }

    public override Task RollbackAsync(CancellationToken cancellationToken = default)
    {
        RolledBack = true;
        return Task.CompletedTask;
    }
}
