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
        var servers = BuildServers(options);

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
            allowCustomEndpoint = options.AllowCustomEndpoint,
            servers = servers.Select(s => new
            {
                id = s.Id,
                title = s.Title,
                endpoint = s.Endpoint,
                local = s.IsLocal,
                headers = s.Headers.Select(h => new
                {
                    name = h.Name,
                    description = h.Description,
                    required = h.Required,
                    secret = h.Secret,
                    placeholder = h.Placeholder,
                    defaultValue = h.DefaultValue,
                }),
                // Names only: values stay on the server.
                serverHeaders = s.AdditionalHeaders.Keys,
            }),
        }));

        group.MapPost("/api/connect", async (ConnectRequest body, HttpContext ctx, ILoggerFactory loggerFactory) =>
        {
            var sw = Stopwatch.StartNew();
            try
            {
                using var cts = CreateTimeout(ctx, options);
                var server = ResolveServer(body.ServerId, body.Endpoint, servers, options);
                await using var client = await CreateClientAsync(server, ctx, loggerFactory, cts.Token);

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
                    serverId = server.Id,
                    endpoint = ResolveEndpoint(server.Endpoint, ctx).ToString(),
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
                var server = ResolveServer(body.ServerId, body.Endpoint, servers, options);
                await using var client = await CreateClientAsync(server, ctx, loggerFactory, cts.Token);

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

    // Without AddServer calls, the top-level options describe a single "default" server (pre-0.5 behaviour).
    private static IReadOnlyList<McpExplorerServer> BuildServers(McpExplorerOptions options)
    {
        if (options.Servers.Count > 0) return options.Servers.ToList();

        var server = new McpExplorerServer("default") { Endpoint = options.McpEndpoint };
        foreach (var h in options.Headers) server.Headers.Add(h);
        foreach (var name in options.ForwardedHeaders) server.ForwardedHeaders.Add(name);
        foreach (var (key, value) in options.AdditionalHeaders) server.AdditionalHeaders[key] = value;
        return new[] { server };
    }

    // The proxy only talks to registered servers; an arbitrary URL from the browser is accepted only
    // when AllowCustomEndpoint is on (it would otherwise let anyone use this app as an open proxy).
    private static McpExplorerServer ResolveServer(
        string? serverId, string? endpoint, IReadOnlyList<McpExplorerServer> servers, McpExplorerOptions options)
    {
        if (!string.IsNullOrWhiteSpace(serverId))
        {
            var match = servers.FirstOrDefault(s => string.Equals(s.Id, serverId, StringComparison.OrdinalIgnoreCase));
            if (match is not null) return match;
            if (string.IsNullOrWhiteSpace(endpoint))
                throw new McpExplorerConfigurationException($"Unknown MCP server '{serverId}'.");
        }

        if (!string.IsNullOrWhiteSpace(endpoint))
        {
            if (!options.AllowCustomEndpoint)
                throw new McpExplorerConfigurationException("Custom MCP endpoints are disabled. Set AllowCustomEndpoint = true to enable them.");
            var custom = new McpExplorerServer("custom") { Endpoint = endpoint.Trim() };
            if (custom.IsLocal && !custom.Endpoint.StartsWith('/'))
                throw new McpExplorerConfigurationException("Endpoint must be an http(s) URL or a path starting with '/'.");
            return custom;
        }

        return servers[0];
    }

    private static async Task<McpClient> CreateClientAsync(
        McpExplorerServer server, HttpContext ctx, ILoggerFactory loggerFactory, CancellationToken ct)
    {
        // Precedence (lowest to highest): forwarded browser headers, headers typed in the UI, server-side AdditionalHeaders.
        var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var name in server.EffectiveForwardedHeaders)
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

        foreach (var (key, value) in server.AdditionalHeaders)
            headers[key] = value;

        var missing = server.Headers.Where(h => h.Required && !headers.ContainsKey(h.Name)).Select(h => h.Name).ToList();
        if (missing.Count > 0)
            throw new McpExplorerConfigurationException($"Required header(s) missing: {string.Join(", ", missing)}. Set them in the Headers panel.");

        var transport = new HttpClientTransport(new HttpClientTransportOptions
        {
            Endpoint = ResolveEndpoint(server.Endpoint, ctx),
            Name = "McpExplorer",
            AdditionalHeaders = headers,
        }, loggerFactory);

        return await McpClient.CreateAsync(transport, new McpClientOptions
        {
            ClientInfo = new Implementation { Name = "McpExplorer", Version = ClientVersion },
        }, loggerFactory, ct);
    }

    private static readonly string ClientVersion =
        typeof(McpExplorerOptions).Assembly.GetName().Version?.ToString(3) ?? "0.0.0";

    private static Uri ResolveEndpoint(string endpoint, HttpContext ctx)
    {
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

    private sealed record ConnectRequest(string? ServerId, string? Endpoint);

    private sealed record CallRequest(
        string? ServerId,
        string? Endpoint,
        string Tool,
        [property: JsonPropertyName("arguments")] Dictionary<string, JsonElement>? Arguments);
}
