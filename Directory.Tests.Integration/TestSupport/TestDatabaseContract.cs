namespace Directory.Tests.Integration.TestSupport;

internal static class TestDatabaseContract
{
    internal static bool IsDisposableCatalog(string? catalog) =>
        catalog is not null
        && (catalog.EndsWith(TestDatabaseContractConstants.TestCatalogSuffix, StringComparison.Ordinal)
            || catalog.EndsWith(TestDatabaseContractConstants.TriageCatalogSuffix, StringComparison.Ordinal));
}
