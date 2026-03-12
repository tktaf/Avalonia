using System.Text.Json.Serialization;

namespace Avalonia.AutomationBridge.Protocol.Messages;

/// <summary>
/// Compact, semantic description of an automation node.  Fields are intentionally small;
/// child lists and large text content are omitted unless explicitly requested.
/// </summary>
public sealed class NodeSummaryDto
{
    /// <summary>Session-local handle such as <c>n42</c>.</summary>
    [JsonPropertyName("id")]
    public required string Id { get; init; }

    /// <summary>Handle of the root window that owns this node, such as <c>w1</c>.</summary>
    [JsonPropertyName("rootId")]
    public required string RootId { get; init; }

    /// <summary>Semantic role: button, edit, checkbox, listitem, window, and so on.</summary>
    [JsonPropertyName("role")]
    public required string Role { get; init; }

    /// <summary>Accessible name as exposed by the automation peer. May be null.</summary>
    [JsonPropertyName("name")]
    public string? Name { get; init; }

    /// <summary>Avalonia AutomationProperties.AutomationId value. May be null.</summary>
    [JsonPropertyName("automationId")]
    public string? AutomationId { get; init; }

    /// <summary>Simple class name of the underlying control. May be null.</summary>
    [JsonPropertyName("className")]
    public string? ClassName { get; init; }

    /// <summary>Whether the element accepts interaction.</summary>
    [JsonPropertyName("enabled")]
    public bool Enabled { get; init; }

    /// <summary>Whether the element currently holds keyboard focus.</summary>
    [JsonPropertyName("focused")]
    public bool Focused { get; init; }

    /// <summary>Whether the element is scrolled or clipped out of the visible area.</summary>
    [JsonPropertyName("offscreen")]
    public bool Offscreen { get; init; }

    /// <summary>Current value string for editable elements (text box, slider, etc.). May be null.</summary>
    [JsonPropertyName("value")]
    public string? Value { get; init; }

    /// <summary>
    /// Bounding rectangle as <c>[x, y, width, height]</c> in screen coordinates. May be null when
    /// bounds are not available.
    /// </summary>
    [JsonPropertyName("bounds")]
    public double[]? Bounds { get; init; }

    /// <summary>Actions the node supports: invoke, setValue, toggle, select, expand, collapse, etc.</summary>
    [JsonPropertyName("actions")]
    public required string[] Actions { get; init; }
}
