using System.Collections.Concurrent;

namespace ExampleBackendApi.Services;

public record Product(int Id, string Name, string Category, decimal Price, int Stock);

public record CreateProductRequest(string Name, string Category, decimal Price, int Stock = 0);

/// <summary>Business logic shared by the REST controller and the MCP tools.</summary>
public sealed class ProductService
{
    private readonly ConcurrentDictionary<int, Product> _products = new();
    private int _nextId;

    public ProductService()
    {
        Add(new("Mechanical Keyboard", "Electronics", 1899.90m, 25));
        Add(new("Wireless Mouse", "Electronics", 649.50m, 80));
        Add(new("Standing Desk", "Furniture", 8499m, 7));
        Add(new("Espresso Beans 1kg", "Grocery", 749m, 120));
    }

    public IEnumerable<Product> Search(string? query = null, string? category = null, decimal? maxPrice = null) =>
        _products.Values
            .Where(p => query is null || p.Name.Contains(query, StringComparison.OrdinalIgnoreCase))
            .Where(p => category is null || p.Category.Equals(category, StringComparison.OrdinalIgnoreCase))
            .Where(p => maxPrice is null || p.Price <= maxPrice)
            .OrderBy(p => p.Id);

    public Product? Get(int id) => _products.GetValueOrDefault(id);

    public Product Add(CreateProductRequest request)
    {
        var product = new Product(Interlocked.Increment(ref _nextId), request.Name, request.Category, request.Price, request.Stock);
        _products[product.Id] = product;
        return product;
    }

    public Product? UpdateStock(int id, int delta)
    {
        if (!_products.TryGetValue(id, out var p)) return null;
        var updated = p with { Stock = Math.Max(0, p.Stock + delta) };
        _products[id] = updated;
        return updated;
    }

    public bool Delete(int id) => _products.TryRemove(id, out _);
}
