namespace Experience.Tests.Infrastructure;

using System.Collections.Concurrent;

public sealed class InMemoryCatalogStore
{
    private readonly ConcurrentDictionary<Guid, CatalogRecord> _products = new();

    public sealed record CatalogRecord(
        Guid Id,
        string? Name,
        decimal? Price,
        string? Brand,
        string? Category,
        string? ManualUrl,
        DateTimeOffset CreatedAt);

    public IReadOnlyList<CatalogRecord> GetProducts(string? nameFilter = null)
    {
        var all = _products.Values.AsEnumerable();
        if (!string.IsNullOrWhiteSpace(nameFilter))
        {
            all = all.Where(p => p.Name?.Contains(nameFilter, StringComparison.OrdinalIgnoreCase) == true);
        }

        return [.. all];
    }

    public CatalogRecord? GetProduct(Guid id) =>
        _products.TryGetValue(id, out var p) ? p : null;

    public CatalogRecord Create(
        string? name,
        decimal? price = null,
        string? brand = null,
        string? category = null,
        string? manualUrl = null)
    {
        var id = Guid.NewGuid();
        var product = new CatalogRecord(id, name, price, brand, category, manualUrl, DateTimeOffset.UtcNow);
        _products[id] = product;
        return product;
    }

    public void Clear() => _products.Clear();
}
