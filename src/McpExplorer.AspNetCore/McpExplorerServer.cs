namespace McpExplorer;

/// <summary>
/// An MCP server listed in the explorer. Each server has its own endpoint, header definitions and
/// server-side headers, and the UI keeps the values typed for it separate from other servers.
/// </summary>
public sealed class McpExplorerServer
{
    private List<string>? _forwardedHeaders;

    public McpExplorerServer(string id)
    {
        if (string.IsNullOrWhiteSpace(id) || id.Any(c => !(char.IsLetterOrDigit(c) || c is '-' or '_' or '.')))
            throw new ArgumentException("Server id must be non-empty and contain only letters, digits, '-', '_' or '.'.", nameof(id));
        Id = id;
    }

    /// <summary>Stable id, used in the UI's URL (e.g. #github/get_issue) and in API requests.</summary>
    public string Id { get; }

    /// <summary>Name shown in the server picker. Defaults to the name the server reports.</summary>
    public string? Title { get; set; }

    /// <summary>
    /// MCP endpoint. A relative path (e.g. "/mcp") points to this app; an absolute http(s) URL points to an external server.
    /// </summary>
    public string Endpoint { get; set; } = "/mcp";

    /// <summary>Headers this server expects; listed in the UI's header panel for this server.</summary>
    public IList<McpExplorerHeader> Headers { get; } = new List<McpExplorerHeader>();

    /// <summary>Shortcut for adding a header definition.</summary>
    public McpExplorerServer AddHeader(string name, Action<McpExplorerHeader>? configure = null)
    {
        var header = new McpExplorerHeader(name);
        configure?.Invoke(header);
        Headers.Add(header);
        return this;
    }

    /// <summary>
    /// Request headers copied from the browser request to this server. When left untouched, "Authorization"
    /// is forwarded to servers in this app (relative endpoint) and nothing is forwarded to external servers,
    /// so the app's own token never leaks to a third party.
    /// </summary>
    public IList<string> ForwardedHeaders => _forwardedHeaders ??= new List<string>();

    /// <summary>
    /// Static headers added server-side to every request to this server (e.g. an API key). They never reach
    /// the browser and override values typed in the UI.
    /// </summary>
    public IDictionary<string, string> AdditionalHeaders { get; } = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

    /// <summary>True when the endpoint is relative, i.e. the MCP server is hosted by this app.</summary>
    public bool IsLocal => !(Uri.TryCreate(Endpoint, UriKind.Absolute, out var uri)
        && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps));

    internal IEnumerable<string> EffectiveForwardedHeaders =>
        _forwardedHeaders ?? (IsLocal ? new List<string> { "Authorization" } : new List<string>());
}
