using System.ComponentModel;
using ModelContextProtocol.Server;

namespace SampleMcpServer.Tools;

[McpServerToolType]
public sealed class HeaderTools(IHttpContextAccessor accessor)
{
    [McpServerTool(Name = "whoami", ReadOnly = true)]
    [Description("Shows which request headers reached the MCP server, to verify the Headers panel.")]
    public Dictionary<string, string?> WhoAmI()
    {
        var headers = accessor.HttpContext?.Request.Headers;
        if (headers is null) return [];
        return headers
            .Where(h => h.Key is "Authorization" || h.Key.StartsWith("X-", StringComparison.OrdinalIgnoreCase))
            .ToDictionary(h => h.Key, h => (string?)Mask(h.Key, h.Value.ToString()));
    }

    private static string Mask(string name, string value) =>
        name is "Authorization" or "X-Refresh-Token" && value.Length > 10 ? value[..10] + "…" : value;
}
