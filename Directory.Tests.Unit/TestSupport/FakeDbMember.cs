namespace Directory.Tests.Unit.TestSupport;

internal static class FakeDbMember
{
    internal static InvalidOperationException NotSet(string memberName, string typeName) =>
        new InvalidOperationException($"{memberName} was never set on this {typeName}.");
}
