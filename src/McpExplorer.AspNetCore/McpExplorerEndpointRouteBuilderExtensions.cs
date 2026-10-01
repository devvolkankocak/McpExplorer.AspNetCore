using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Serialization;
using McpExplorer;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Logging;
using ModelContextProtocol;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;

namespace Microsoft.AspNetCore.Builder;

public static class McpExplorerEndpointRouteBuilderExtensions
{
    private const string HeaderPrefix = "X-McpExplorer-Header-";

    /// <summary>
    /// Maps an interactive UI (like Swagger UI / Scalar) for listing and invoking MCP tools.
    /// </summary>
    /// <example><code>app.MapMcp("/mcp"); app.MapMcpExplorer("/mcp-explorer");</code></example>
    public static IEndpointConventionBuilder MapMcpExplorer(
        this IEndpointRouteBuilder endpoints,
        string pattern = "/mcp-explorer",
        Action<McpExplorerOptions>? configure = null)
    {
        var options = new McpExplorerOptions();
        configure?.Invoke(options);

        var prefix = "/" + pattern.Trim('/');
        var group = endpoints.MapGroup(prefix).ExcludeFromDescription();

        var indexHtml = LoadIndexHtml(options);

        group.MapGet("/", (HttpContext ctx) =>
        {
            // Relative asset/api URLs in the page need a trailing slash.
            if (!ctx.Request.Path.Value!.EndsWith('/'))
                return Results.Redirect(ctx.Request.PathBase + ctx.Request.Path + "/" + ctx.Request.QueryString);
            return Results.Content(indexHtml, "text/html; charset=utf-8");
        });

        group.MapGet("/api/config", () => Results.Json(new
        {
            title = options.Title,
            endpoint = options.McpEndpoint,
            allowCustomEndpoint = options.AllowCustomEndpoint,
            headers = options.Headers.Select(h => new
            {
                name = h.Name,
                description = h.Description,
                required = h.Required,
                secret = h.Secret,
                placeholder = h.Placeholder,
                defaultValue = h.DefaultValue,
            }),
            serverHeaders = options.AdditionalHeaders.Keys,
        }));

        group.MapPost("/api/connect", async (ConnectRequest body, HttpContext ctx, ILoggerFactory loggerFactory) =>
        {
            var sw = Stopwatch.StartNew();
            try
            {
                using var cts = CreateTimeout(ctx, options);
                await using var client = await CreateClientAsync(body.Endpoint, ctx, options, loggerFactory, cts.Token);

                var tools = new List<Tool>();
                string? cursor = null;
                do
                {
                    var page = await client.ListToolsAsync(new ListToolsRequestParams { Cursor = cursor }, cts.Token);
                    tools.AddRange(page.Tools);
                    cursor = page.NextCursor;
                } while (cursor is not null);

                return Json(new
                {
                    ok = true,
                    endpoint = ResolveEndpoint(body.Endpoint, ctx, options).ToString(),
                    server = client.ServerInfo,
                    protocolVersion = client.NegotiatedProtocolVersion,
                    capabilities = client.ServerCapabilities,
                    instructions = client.ServerInstructions,
                    tools,
                    elapsedMs = sw.ElapsedMilliseconds,
                });
            }
            catch (Exception ex)
            {
                return Error(ex, sw);
            }
        });

        group.MapPost("/api/call", async (CallRequest body, HttpContext ctx, ILoggerFactory loggerFactory) =>
        {
            var sw = Stopwatch.StartNew();
            try
            {
                if (string.IsNullOrWhiteSpace(body.Tool))
                    return Results.BadRequest(new { ok = false, error = "Tool name is required." });

                using var cts = CreateTimeout(ctx, options);
                await using var client = await CreateClientAsync(body.Endpoint, ctx, options, loggerFactory, cts.Token);

                var result = await client.CallToolAsync(new CallToolRequestParams
                {
                    Name = body.Tool,
                    Arguments = body.Arguments,
                }, cts.Token);

                return Json(new { ok = true, result, elapsedMs = sw.ElapsedMilliseconds });
            }
            catch (Exception ex)
            {
                return Error(ex, sw);
            }
        });

        return group;
    }

    private static async Task<McpClient> CreateClientAsync(
        string? endpoint, HttpContext ctx, McpExplorerOptions options, ILoggerFactory loggerFactory, CancellationToken ct)
    {
        // Precedence (lowest to highest): forwarded browser headers, headers typed in the UI, server-side AdditionalHeaders.
        var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var name in options.ForwardedHeaders)
        {
            if (ctx.Request.Headers.TryGetValue(name, out var value) && !string.IsNullOrEmpty(value))
                headers[name] = value.ToString();
        }

        // Headers the user typed in the UI arrive as X-McpExplorer-Header-{Name}.
        foreach (var (key, value) in ctx.Request.Headers)
        {
            if (key.StartsWith(HeaderPrefix, StringComparison.OrdinalIgnoreCase) && key.Length > HeaderPrefix.Length
                && !string.IsNullOrEmpty(value))
                headers[key[HeaderPrefix.Length..]] = value.ToString();
        }

        foreach (var (key, value) in options.AdditionalHeaders)
            headers[key] = value;

        var missing = options.Headers.Where(h => h.Required && !headers.ContainsKey(h.Name)).Select(h => h.Name).ToList();
        if (missing.Count > 0)
            throw new McpExplorerConfigurationException($"Required header(s) missing: {string.Join(", ", missing)}. Set them in the Headers panel.");

        var transport = new HttpClientTransport(new HttpClientTransportOptions
        {
            Endpoint = ResolveEndpoint(endpoint, ctx, options),
            Name = "McpExplorer",
            AdditionalHeaders = headers,
        }, loggerFactory);

        return await McpClient.CreateAsync(transport, new McpClientOptions
        {
            ClientInfo = new Implementation { Name = "McpExplorer", Version = "0.2.0" },
        }, loggerFactory, ct);
    }

    private static Uri ResolveEndpoint(string? requested, HttpContext ctx, McpExplorerOptions options)
    {
        var endpoint = options.AllowCustomEndpoint && !string.IsNullOrWhiteSpace(requested)
            ? requested.Trim()
            : options.McpEndpoint;

        if (Uri.TryCreate(endpoint, UriKind.Absolute, out var absolute) &&
            (absolute.Scheme == Uri.UriSchemeHttp || absolute.Scheme == Uri.UriSchemeHttps))
            return absolute;

        var baseUri = new Uri($"{ctx.Request.Scheme}://{ctx.Request.Host}{ctx.Request.PathBase}/");
        return new Uri(baseUri, endpoint.TrimStart('/'));
    }

    private static CancellationTokenSource CreateTimeout(HttpContext ctx, McpExplorerOptions options)
    {
        var cts = CancellationTokenSource.CreateLinkedTokenSource(ctx.RequestAborted);
        cts.CancelAfter(options.RequestTimeout);
        return cts;
    }

    private static IResult Json(object value) => Results.Json(value, McpJsonUtilities.DefaultOptions);

    private static IResult Error(Exception ex, Stopwatch sw) => Results.Json(new
    {
        ok = false,
        error = ex is OperationCanceledException ? "The request timed out or was cancelled." : ex.Message,
        type = ex.GetType().Name,
        elapsedMs = sw.ElapsedMilliseconds,
    }, statusCode: ex is McpExplorerConfigurationException ? StatusCodes.Status400BadRequest : StatusCodes.Status502BadGateway);

    private static string LoadIndexHtml(McpExplorerOptions options)
    {
        using var stream = typeof(McpExplorerOptions).Assembly.GetManifestResourceStream("McpExplorer.wwwroot.index.html")
            ?? throw new InvalidOperationException("McpExplorer index.html resource not found.");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd().Replace("{{TITLE}}", System.Net.WebUtility.HtmlEncode(options.Title));
    }

    private sealed class McpExplorerConfigurationException(string message) : Exception(message);

    private sealed record ConnectRequest(string? Endpoint);

    private sealed record CallRequest(
        string? Endpoint,
        string Tool,
        [property: JsonPropertyName("arguments")] Dictionary<string, JsonElement>? Arguments);
}
