namespace Directory.Tests.Integration.TestSupport;

public sealed partial class DirectoryWebApplicationFactory
{
    public string? RefusedCatalog { get; private set; }

    partial void OnCatalogRefused(string? catalog)
    {
        RefusedCatalog = catalog;
    }
}
