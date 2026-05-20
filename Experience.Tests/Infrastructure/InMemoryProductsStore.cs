namespace Experience.Tests.Infrastructure;

using System.Collections.Concurrent;

public sealed class InMemoryProductsStore
{
    private readonly ConcurrentDictionary<Guid, ProductRecord> _products = new();

    public sealed record ProductRecord(
        Guid Id,
        string? Name,
        decimal? Price,
        string? Brand,
        string? ModelNumber,
        string? SerialNumber,
        string? PurchaseDate,
        string? Category,
        string? Description,
        string? ManualUrl,
        DateTimeOffset CreatedAt,
        DateTimeOffset? UpdatedAt);

    public IReadOnlyList<ProductRecord> GetProducts(string? nameFilter = null)
    {
        var all = _products.Values.OrderBy(p => p.Name).AsEnumerable();
        if (!string.IsNullOrWhiteSpace(nameFilter))
        {
            all = all.Where(p => p.Name?.Contains(nameFilter, StringComparison.OrdinalIgnoreCase) == true);
        }

        return [.. all];
    }

    public ProductRecord? GetProduct(Guid id) =>
        _products.TryGetValue(id, out var p) ? p : null;

    public ProductRecord Create(
        string? name,
        decimal? price = null,
        string? brand = null,
        string? modelNumber = null,
        string? serialNumber = null,
        string? purchaseDate = null,
        string? category = null,
        string? description = null,
        string? manualUrl = null)
    {
        var id = Guid.NewGuid();
        var product = new ProductRecord(
            id, name, price, brand, modelNumber, serialNumber,
            purchaseDate, category, description, manualUrl,
            DateTimeOffset.UtcNow, null);
        _products[id] = product;
        return product;
    }

    public ProductRecord? Put(Guid id, ProductRecord replacement)
    {
        if (!_products.ContainsKey(id))
        {
            return null;
        }

        var updated = replacement with { Id = id, UpdatedAt = DateTimeOffset.UtcNow };
        _products[id] = updated;
        return updated;
    }

    public ProductRecord? Patch(
        Guid id,
        string? name = null,
        decimal? price = null,
        string? brand = null,
        string? modelNumber = null,
        string? serialNumber = null,
        string? purchaseDate = null,
        string? category = null,
        string? description = null,
        string? manualUrl = null)
    {
        if (!_products.TryGetValue(id, out var existing))
        {
            return null;
        }

        var updated = existing with
        {
            Name = name ?? existing.Name,
            Price = price ?? existing.Price,
            Brand = brand ?? existing.Brand,
            ModelNumber = modelNumber ?? existing.ModelNumber,
            SerialNumber = serialNumber ?? existing.SerialNumber,
            PurchaseDate = purchaseDate ?? existing.PurchaseDate,
            Category = category ?? existing.Category,
            Description = description ?? existing.Description,
            ManualUrl = manualUrl ?? existing.ManualUrl,
            UpdatedAt = DateTimeOffset.UtcNow,
        };
        _products[id] = updated;
        return updated;
    }

    public bool Delete(Guid id) => _products.TryRemove(id, out _);

    public void Clear() => _products.Clear();
}
