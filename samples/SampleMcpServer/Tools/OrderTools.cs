using System.ComponentModel;
using ModelContextProtocol.Server;

namespace SampleMcpServer.Tools;

[McpServerToolType]
public sealed class OrderTools
{
    [McpServerTool(Name = "create_order", Destructive = false)]
    [Description("Creates an order. Shows nested objects and arrays in the generated form.")]
    public static OrderResult CreateOrder(
        [Description("Customer info")] Customer customer,
        [Description("Order lines")] List<OrderLine> lines,
        [Description("Tags for the order")] string[]? tags = null,
        [Description("Express shipping")] bool express = false)
    {
        var total = lines.Sum(l => l.Quantity * l.UnitPrice);
        return new OrderResult(Guid.NewGuid(), customer.Name, lines.Count, total, express, tags ?? []);
    }

    [McpServerTool(Name = "delete_order", Destructive = true)]
    [Description("Deletes an order by id (demo, nothing is actually deleted).")]
    public static string DeleteOrder([Description("Order id")] Guid orderId) => $"Order {orderId} deleted.";
}

public record Customer(
    [property: Description("Full name")] string Name,
    [property: Description("E-mail address")] string Email,
    [property: Description("Optional phone")] string? Phone = null);

public record OrderLine(
    [property: Description("Product SKU")] string Sku,
    [property: Description("Quantity")] int Quantity,
    [property: Description("Unit price")] decimal UnitPrice);

public record OrderResult(Guid OrderId, string Customer, int LineCount, decimal Total, bool Express, string[] Tags);
