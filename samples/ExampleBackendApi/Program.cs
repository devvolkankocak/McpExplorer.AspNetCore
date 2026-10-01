using ExampleBackendApi.Mcp;
using ExampleBackendApi.Services;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

// Normal backend services
builder.Services.AddControllers();
builder.Services.AddOpenApi();
builder.Services.AddSingleton<ProductService>();
builder.Services.AddHttpContextAccessor();

// MCP server: exposes the same business logic as MCP tools
builder.Services
    .AddMcpServer()
    .WithHttpTransport()
    .WithTools<ProductTools>();

var app = builder.Build();

app.MapControllers();
app.MapMcp("/mcp");

if (app.Environment.IsDevelopment())
{
    // REST API docs (as usual)
    app.MapOpenApi();
    app.MapScalarApiReference("/scalar");

    // MCP tools UI (this library)
    app.MapMcpExplorer("/mcp-explorer", o =>
    {
        o.Title = "Example Backend · MCP";
        o.AddHeader("Authorization", h =>
        {
            h.Required = true;
            h.Secret = true;
            h.Placeholder = "Bearer <token>";
            h.Description = "Demo: any value starting with 'Bearer ' is accepted.";
            h.DefaultValue = "Bearer demo-token";
        });
        o.AddHeader("X-Tenant-Id", h =>
        {
            h.Description = "Tenant whose products are used.";
            h.DefaultValue = "acme";
        });
    });
}

app.MapGet("/", () => Results.Redirect("/mcp-explorer/")).ExcludeFromDescription();

app.Run();
