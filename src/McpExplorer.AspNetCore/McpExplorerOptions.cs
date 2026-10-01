namespace McpExplorer;

/// <summary>Options for MCP Explorer.</summary>
public sealed class McpExplorerOptions
{
    /// <summary>
    /// MCP endpoint the UI connects to by default. A relative path (e.g. "/mcp") is resolved
    /// against the current request's host; an absolute URL points to any MCP server.
    /// </summary>
    public string McpEndpoint { get; set; } = "/mcp";

    /// <summary>Title shown in the UI header and the browser tab.</summary>
    public string Title { get; set; } = "MCP Explorer";

    /// <summary>
    /// Lets users type a different MCP server URL in the UI. The UI calls servers through this
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
