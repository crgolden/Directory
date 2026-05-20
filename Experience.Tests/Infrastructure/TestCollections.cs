namespace Experience.Tests.Infrastructure;

[CollectionDefinition(Name)]
public sealed class E2ECollection : ICollectionFixture<PlaywrightFixture>
{
    public const string Name = "E2E";
}

[CollectionDefinition(Name)]
public sealed class UnitCollection
{
    public const string Name = "Unit";

    private UnitCollection()
    {
    }
}
