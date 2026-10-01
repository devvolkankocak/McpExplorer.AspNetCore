using System.ComponentModel;
using ExampleBackendApi.Services;
using ModelContextProtocol;
using ModelContextProtocol.Server;

namespace ExampleBackendApi.Mcp;

/// <summary>The same ProductService, exposed to AI agents as MCP tools.</summary>
[McpServerToolType]
public sealed class ProductTools(ProductService products, IHttpContextAccessor http)
{
    [McpServerTool(Name = "search_products", ReadOnly = true)]
    [Description("Searches products by name, category and maximum price.")]
    public IEnumerable<Product> SearchProducts(
        [Description("Part of the product name")] string? query = null,
        [Description("Category, e.g. Electronics, Furniture, Grocery")] string? category = null,
        [Description("Maximum price")] decimal? maxPrice = null)
    {
        EnsureAuthorized();
        return products.Search(query, category, maxPrice);
    }

    [McpServerTool(Name = "get_product", ReadOnly = true)]
    [Description("Gets a single product by id.")]
    public Product GetProduct([Description("Product id")] int id)
    {
        EnsureAuthorized();
        return products.Get(id) ?? throw new McpException($"Product {id} not found.");
    }

    [McpServerTool(Name = "create_product")]
    [Description("Creates a new product.")]
    public Product CreateProduct([Description("Product to create")] CreateProductRequest product)
    {
        EnsureAuthorized();
        return products.Add(product);
    }

    [McpServerTool(Name = "update_stock", Idempotent = false)]
    [Description("Increases or decreases the stock of a product.")]
    public Product UpdateStock(
        [Description("Product id")] int id,
        [Description("Amount to add (negative to remove)")] int delta)
    {
        EnsureAuthorized();
        return products.UpdateStock(id, delta) ?? throw new McpException($"Product {id} not found.");
    }

    [McpServerTool(Name = "delete_product", Destructive = true)]
    [Description("Deletes a product.")]
    public string DeleteProduct([Description("Product id")] int id)
    {
        EnsureAuthorized();
        return products.Delete(id) ? $"Product {id} deleted." : throw new McpException($"Product {id} not found.");
    }

    // Demo auth: real projects would use ASP.NET Core authentication (e.g. app.MapMcp().RequireAuthorization()).
    private void EnsureAuthorized()
    {
        var auth = http.HttpContext?.Request.Headers.Authorization.ToString();
        if (string.IsNullOrEmpty(auth) || !auth.StartsWith("Bearer ", StringComparison.Ordinal))
            throw new McpException("Unauthorized: send 'Authorization: Bearer <token>'.");
    }
}
