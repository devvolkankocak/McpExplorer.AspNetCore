using Microsoft.Extensions.Configuration;

namespace McpExplorer;

/// <summary>Options for MCP Explorer.</summary>
public sealed class McpExplorerOptions
{
    /// <summary>
    /// MCP servers listed in the UI's server picker. When empty, a single server named "default" is built from
    /// <see cref="McpEndpoint"/>, <see cref="Headers"/>, <see cref="ForwardedHeaders"/> and <see cref="AdditionalHeaders"/>.
    /// </summary>
    public IList<McpExplorerServer> Servers { get; } = new List<McpExplorerServer>();

    /// <summary>Adds an MCP server to the picker, e.g. <c>o.AddServer("github", s =&gt; s.Endpoint = "https://…/mcp")</c>.</summary>
    public McpExplorerOptions AddServer(string id, Action<McpExplorerServer> configure)
    {
        var server = new McpExplorerServer(id);
        configure(server);
        return AddServer(server);
    }

    /// <summary>Adds an MCP server to the picker with the given endpoint.</summary>
    public McpExplorerOptions AddServer(string id, string endpoint, Action<McpExplorerServer>? configure = null)
    {
        var server = new McpExplorerServer(id) { Endpoint = endpoint };
        configure?.Invoke(server);
        return AddServer(server);
    }

    private McpExplorerOptions AddServer(McpExplorerServer server)
    {
        if (Servers.Any(s => string.Equals(s.Id, server.Id, StringComparison.OrdinalIgnoreCase)))
            throw new InvalidOperationException($"An MCP server with id '{server.Id}' is already registered.");
        Servers.Add(server);
        return this;
    }

    /// <summary>
    /// Adds the servers described in a configuration section, one child per server:
    /// <code>
    /// "McpExplorer": { "Servers": {
    ///   "github": { "Title": "GitHub MCP", "Endpoint": "https://…/mcp",
    ///               "Headers": { "Authorization": { "Required": true, "Secret": true } },
    ///               "AdditionalHeaders": { "X-Api-Key": "…" }, "ForwardedHeaders": [ "X-Trace-Id" ] } } }
    /// </code>
    /// </summary>
    public McpExplorerOptions AddServersFrom(IConfiguration section)
    {
        foreach (var child in section.GetChildren())
        {
            var server = new McpExplorerServer(child.Key)
            {
                Title = child["Title"],
                Endpoint = child["Endpoint"] ?? throw new InvalidOperationException($"MCP server '{child.Key}' has no Endpoint."),
            };
            foreach (var h in child.GetSection("Headers").GetChildren())
            {
                server.AddHeader(h.Key, header =>
                {
                    header.Description = h["Description"];
                    header.Required = bool.TryParse(h["Required"], out var required) && required;
                    header.Secret = bool.TryParse(h["Secret"], out var secret) && secret;
                    header.Placeholder = h["Placeholder"];
                    header.DefaultValue = h["DefaultValue"];
                });
            }
            foreach (var h in child.GetSection("AdditionalHeaders").GetChildren())
                if (h.Value is not null) server.AdditionalHeaders[h.Key] = h.Value;
            var forwarded = child.GetSection("ForwardedHeaders");
            if (forwarded.Exists())
                foreach (var f in forwarded.GetChildren())
                    if (!string.IsNullOrWhiteSpace(f.Value)) server.ForwardedHeaders.Add(f.Value);
            AddServer(server);
        }
        return this;
    }

    /// <summary>
    /// MCP endpoint the UI connects to by default (used when no <see cref="Servers"/> are added). A relative path (e.g. "/mcp") is resolved
    /// against the current request's host; an absolute URL points to any MCP server.
    /// </summary>
    public string McpEndpoint { get; set; } = "/mcp";

    /// <summary>Title shown in the UI header and the browser tab.</summary>
    public string Title { get; set; } = "MCP Explorer";

    /// <summary>
    /// Lets users add temporary MCP servers by URL in the UI. The UI calls servers through this
    /// app (server-side proxy), so only enable it in trusted environments (e.g. Development).
    /// </summary>
    public bool AllowCustomEndpoint { get; set; }

    /// <summary>
    /// Headers this MCP server expects. They are listed in the UI's header panel (with description,
    /// required flag and masking) so every project can describe its own auth/tenant/trace headers.
    /// Users can still add any other header by hand in the UI.
    /// </summary>
    public IList<McpExplorerHeader> Headers { get; } = new List<McpExplorerHeader>();

    /// <summary>Shortcut for adding a header definition.</summary>
    public McpExplorerOptions AddHeader(string name, Action<McpExplorerHeader>? configure = null)
    {
        var header = new McpExplorerHeader(name);
        configure?.Invoke(header);
        Headers.Add(header);
        return this;
    }

    /// <summary>Request headers copied from the browser request to the MCP server (e.g. "Authorization").</summary>
    public IList<string> ForwardedHeaders { get; } = new List<string> { "Authorization" };

    /// <summary>
    /// Static headers added server-side to every MCP request made by the UI. These never reach the
    /// browser, so this is the place for internal API keys. They override values typed in the UI.
    /// </summary>
    public IDictionary<string, string> AdditionalHeaders { get; } = new Dictionary<string, string>();

    /// <summary>Timeout for a single MCP operation (connect + list / call).</summary>
    public TimeSpan RequestTimeout { get; set; } = TimeSpan.FromSeconds(100);
}
