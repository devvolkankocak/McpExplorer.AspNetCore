using ExampleBackendApi.Services;
using Microsoft.AspNetCore.Mvc;

namespace ExampleBackendApi.Controllers;

[ApiController]
[Route("api/products")]
public sealed class ProductsController(ProductService products) : ControllerBase
{
    [HttpGet]
    public IEnumerable<Product> Search(string? query, string? category, decimal? maxPrice) =>
        products.Search(query, category, maxPrice);

    [HttpGet("{id:int}")]
    public ActionResult<Product> Get(int id) => products.Get(id) is { } p ? p : NotFound();

    [HttpPost]
    public ActionResult<Product> Create(CreateProductRequest request)
    {
        var p = products.Add(request);
        return CreatedAtAction(nameof(Get), new { id = p.Id }, p);
    }

    [HttpDelete("{id:int}")]
    public IActionResult Delete(int id) => products.Delete(id) ? NoContent() : NotFound();
}
