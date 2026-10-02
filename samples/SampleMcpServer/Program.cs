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
        options.AllowCustomEndpoint = true; // lets you add temporary servers from the UI

        // This app's own MCP server. Headers listed here show up in the UI's Headers panel.
        options.AddServer("local", "/mcp", s =>
        {
            s.Title = "Sample MCP Server";
            s.AddHeader("Authorization", h =>
            {
                h.Required = true;
                h.Secret = true;
                h.Placeholder = "Bearer <access-token>";
                h.Description = "Access token for the API. The demo accepts any value.";
                h.DefaultValue = "Bearer demo-token";
            });
            s.AddHeader("X-Refresh-Token", h =>
            {
                h.Secret = true;
                h.Description = "Optional refresh token, used when the access token expires.";
            });
            s.AddHeader("X-Tenant-Id", h =>
            {
                h.Description = "Tenant the request runs for.";
                h.DefaultValue = "acme";
            });

            // Server-side only: added to every MCP request, never sent to the browser.
            s.AdditionalHeaders["X-Internal-Caller"] = "McpExplorer";
        });

        // Other (external) MCP servers, from appsettings.Development.json.
        options.AddServersFrom(builder.Configuration.GetSection("McpExplorer:Servers"));
    });
}

app.MapGet("/", () => Results.Redirect("/mcp-explorer/"));

app.Run();
