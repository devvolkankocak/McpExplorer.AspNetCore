namespace McpExplorer;

/// <summary>
/// A header the target MCP server expects (e.g. Authorization, X-Refresh-Token, X-Tenant-Id).
/// Defined headers appear in the UI's header panel with their description, so developers know
/// what to fill in. Values typed in the UI are stored in the browser and sent with every MCP request.
/// </summary>
public sealed class McpExplorerHeader
{
    public McpExplorerHeader(string name) => Name = name;

    /// <summary>Header name, e.g. "Authorization".</summary>
    public string Name { get; }

    /// <summary>Shown under the input to explain what the header is for.</summary>
    public string? Description { get; set; }

    /// <summary>When true the UI will not connect or call tools until a value is set.</summary>
    public bool Required { get; set; }

    /// <summary>Masks the value in the UI (tokens, API keys).</summary>
    public bool Secret { get; set; }

    /// <summary>Hint text inside the empty input, e.g. "Bearer &lt;token&gt;".</summary>
    public string? Placeholder { get; set; }

    /// <summary>
    /// Prefilled value. It is sent to the browser, so never put real secrets here;
    /// use <see cref="McpExplorerOptions.AdditionalHeaders"/> for values that must stay on the server.
    /// </summary>
    public string? DefaultValue { get; set; }
}
