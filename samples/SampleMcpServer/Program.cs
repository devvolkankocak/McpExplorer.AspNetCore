using SampleMcpServer.Tools;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddHttpContextAccessor();

builder.Services
    .AddMcpServer()
    .WithHttpTransport()
    .WithTools<DemoTools>()
    .WithTools<OrderTools>()
    .WithTools<HeaderTools>();

var app = builder.Build();

app.MapMcp("/mcp");

// Only one line is needed to get the UI. Keep it to development environments.
if (app.Environment.IsDevelopment())
{
    app.MapMcpExplorer("/mcp-explorer", options =>
    {
        options.Title = "Sample MCP Server";
        options.AllowCustomEndpoint = true;

        // Headers this project's MCP server expects. They show up in the UI's Headers panel.
        options.AddHeader("Authorization", h =>
        {
            h.Required = true;
            h.Secret = true;
            h.Placeholder = "Bearer <access-token>";
            h.Description = "Access token for the API. The demo accepts any value.";
            h.DefaultValue = "Bearer demo-token";
        });
        options.AddHeader("X-Refresh-Token", h =>
        {
            h.Secret = true;
            h.Description = "Optional refresh token, used when the access token expires.";
        });
        options.AddHeader("X-Tenant-Id", h =>
        {
            h.Description = "Tenant the request runs for.";
            h.DefaultValue = "acme";
        });

        // Server-side only: added to every MCP request, never sent to the browser.
        options.AdditionalHeaders["X-Internal-Caller"] = "McpExplorer";
    });
}

app.MapGet("/", () => Results.Redirect("/mcp-explorer/"));

app.Run();
