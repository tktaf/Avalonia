using System.Text.Json.Serialization;

namespace Avalonia.AutomationBridge.Protocol.Messages;

/// <summary>Typed error codes returned by the automation bridge.</summary>
public static class BridgeErrorCode
{
    /// <summary>Bridge is not enabled in the current process.</summary>
    public const string BridgeNotEnabled = "bridge_not_enabled";

    /// <summary>The requested root window was not found.</summary>
    public const string RootNotFound = "root_not_found";

    /// <summary>No node matched the selector.</summary>
    public const string NodeNotFound = "node_not_found";

    /// <summary>More than one node matched the selector and no <c>nth</c> was specified.</summary>
    public const string SelectorAmbiguous = "selector_ambiguous";

    /// <summary>The node does not expose the requested action through its provider interfaces.</summary>
    public const string ActionNotSupported = "action_not_supported";

    /// <summary>The target element is disabled and cannot accept the action.</summary>
    public const string ElementNotEnabled = "element_not_enabled";

    /// <summary>The client's revision is older than the current bridge revision.</summary>
    public const string StaleRevision = "stale_revision";

    /// <summary>The supplied request payload could not be parsed or validated.</summary>
    public const string InvalidRequest = "invalid_request";

    /// <summary>The requested action threw while executing against the target node.</summary>
    public const string ActionFailed = "action_failed";

    /// <summary>An unexpected bridge-side exception interrupted request processing.</summary>
    public const string InternalError = "internal_error";
}

/// <summary>Error payload returned in a failed <see cref="BridgeResponse"/>.</summary>
public sealed class ErrorDto
{
    /// <summary>Machine-readable error code; one of the constants on <see cref="BridgeErrorCode"/>.</summary>
    [JsonPropertyName("code")]
    public required string Code { get; init; }

    /// <summary>Human-readable description of the error. May be null.</summary>
    [JsonPropertyName("message")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Message { get; init; }
}
