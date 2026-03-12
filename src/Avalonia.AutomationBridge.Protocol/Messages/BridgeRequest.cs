using System.Text.Json.Serialization;

namespace Avalonia.AutomationBridge.Protocol.Messages;

/// <summary>Well-known request action names.</summary>
public static class BridgeAction
{
    /// <summary>List top-level automation roots.</summary>
    public const string Roots = "roots";

    /// <summary>Query nodes matching a selector within a root.</summary>
    public const string Query = "query";

    /// <summary>Return the full node summary for a specific handle.</summary>
    public const string Describe = "describe";

    /// <summary>Invoke the IInvokeProvider on the target node.</summary>
    public const string Invoke = "invoke";

    /// <summary>Set a value via IValueProvider on the target node.</summary>
    public const string SetValue = "set-value";

    /// <summary>Toggle the target node via IToggleProvider.</summary>
    public const string Toggle = "toggle";

    /// <summary>Select an item via ISelectionItemProvider.</summary>
    public const string Select = "select";

    /// <summary>Expand via IExpandCollapseProvider.</summary>
    public const string Expand = "expand";

    /// <summary>Collapse via IExpandCollapseProvider.</summary>
    public const string Collapse = "collapse";

    /// <summary>Move keyboard focus to the target node.</summary>
    public const string SetFocus = "set-focus";

    /// <summary>Open the target node's context menu.</summary>
    public const string ShowContextMenu = "show-context-menu";

    /// <summary>Scroll the target node by provider-defined increments.</summary>
    public const string Scroll = "scroll";

    /// <summary>Set the target node's scroll offsets as percentages.</summary>
    public const string SetScrollPercent = "set-scroll-percent";

    /// <summary>Subscribe to revision updates for a root.</summary>
    public const string Watch = "watch";
}

/// <summary>Request envelope sent from the CLI or tool to the in-process bridge.</summary>
public sealed class BridgeRequest
{
    /// <summary>Client-assigned correlation identifier echoed in the response.</summary>
    [JsonPropertyName("requestId")]
    public string? RequestId { get; init; }

    /// <summary>Action to perform; one of the constants on <see cref="BridgeAction"/>.</summary>
    [JsonPropertyName("action")]
    public required string Action { get; init; }

    /// <summary>Root window handle required for query and action requests.</summary>
    [JsonPropertyName("rootId")]
    public string? RootId { get; init; }

    /// <summary>Target node handle for describe, invoke, set-value, toggle, and similar actions.</summary>
    [JsonPropertyName("nodeId")]
    public string? NodeId { get; init; }

    /// <summary>Selector for query requests.</summary>
    [JsonPropertyName("selector")]
    public SelectorDto? Selector { get; init; }

    /// <summary>Maximum number of results to return from a query. Defaults to 1.</summary>
    [JsonPropertyName("maxResults")]
    public int? MaxResults { get; init; }

    /// <summary>Value string for set-value requests.</summary>
    [JsonPropertyName("value")]
    public string? Value { get; init; }

    /// <summary>Horizontal scroll amount for scroll requests.</summary>
    [JsonPropertyName("horizontalAmount")]
    public string? HorizontalAmount { get; init; }

    /// <summary>Vertical scroll amount for scroll requests.</summary>
    [JsonPropertyName("verticalAmount")]
    public string? VerticalAmount { get; init; }

    /// <summary>Horizontal scroll percentage for set-scroll-percent requests.</summary>
    [JsonPropertyName("horizontalPercent")]
    public double? HorizontalPercent { get; init; }

    /// <summary>Vertical scroll percentage for set-scroll-percent requests.</summary>
    [JsonPropertyName("verticalPercent")]
    public double? VerticalPercent { get; init; }

    /// <summary>
    /// Client's last-known revision, used for watch and delta requests.
    /// The bridge will return <see cref="BridgeErrorCode.StaleRevision"/> if this is outdated.
    /// </summary>
    [JsonPropertyName("sinceRevision")]
    public long? SinceRevision { get; init; }
}
