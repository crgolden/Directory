namespace Directory.Tests.Unit.TestSupport;

using System.Data;
using System.Data.Common;
using System.Diagnostics.CodeAnalysis;

internal sealed class FakeDbParameter : DbParameter
{
    private string? _parameterName;
    private string? _sourceColumn;

    public override DbType DbType { get; set; }

    public override ParameterDirection Direction { get; set; }

    public override bool IsNullable { get; set; }

    [AllowNull]
    public override string ParameterName
    {
        get => _parameterName ?? throw FakeDbMember.NotSet(nameof(ParameterName), nameof(FakeDbParameter));
        set => _parameterName = value;
    }

    public override int Size { get; set; }

    [AllowNull]
    public override string SourceColumn
    {
        get => _sourceColumn ?? throw FakeDbMember.NotSet(nameof(SourceColumn), nameof(FakeDbParameter));
        set => _sourceColumn = value;
    }

    public override bool SourceColumnNullMapping { get; set; }

    public override object? Value { get; set; }

    public override void ResetDbType()
    {
    }
}
