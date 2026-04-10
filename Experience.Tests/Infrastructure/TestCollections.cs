namespace Experience.Tests.Infrastructure;

/// <summary>
/// xUnit collection that shares <see cref="PlaywrightFixture"/> across all E2E and smoke tests.
/// </summary>
[CollectionDefinition(Name)]
public sealed class E2ECollection : ICollectionFixture<PlaywrightFixture>
{
    public const string Name = "E2E";
}

/// <summary>
/// xUnit collection for all unit tests.
/// </summary>
[CollectionDefinition(Name)]
public sealed class UnitCollection
{
    public const string Name = "Unit";

    private UnitCollection()
    {
    }
}
