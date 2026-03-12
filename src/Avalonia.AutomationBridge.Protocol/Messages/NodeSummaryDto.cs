using System.Collections.Generic;
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
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Name { get; init; }

    /// <summary>Avalonia AutomationProperties.AutomationId value. May be null.</summary>
    [JsonPropertyName("automationId")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? AutomationId { get; init; }

    /// <summary>Simple class name of the underlying control. May be null.</summary>
    [JsonPropertyName("className")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? ClassName { get; init; }

    /// <summary>Whether the element accepts interaction.</summary>
    [JsonPropertyName("enabled")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public bool? Enabled { get; init; }

    /// <summary>Whether the element currently holds keyboard focus.</summary>
    [JsonPropertyName("focused")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public bool? Focused { get; init; }

    /// <summary>Whether the element is scrolled or clipped out of the visible area.</summary>
    [JsonPropertyName("offscreen")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public bool? Offscreen { get; init; }

    /// <summary>Whether the element is currently selected, when selection semantics are available.</summary>
    [JsonPropertyName("selected")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public bool? Selected { get; init; }

    /// <summary>Whether the element is currently expanded, when expand/collapse semantics are available.</summary>
    [JsonPropertyName("expanded")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public bool? Expanded { get; init; }

    /// <summary>Whether the element is currently checked, when toggle semantics are available.</summary>
    [JsonPropertyName("checked")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public bool? Checked { get; init; }

    /// <summary>Current value string for editable elements (text box, slider, etc.). May be null.</summary>
    [JsonPropertyName("value")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Value { get; init; }

    /// <summary>
    /// Bounding rectangle as <c>[x, y, width, height]</c> in screen coordinates. May be null when
    /// bounds are not available.
    /// </summary>
    [JsonPropertyName("bounds")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public double[]? Bounds
    {
        get => _bounds;
        init => _bounds = value is null || value.Length == 4
            ? value
            : throw new ArgumentException("Bounds must be null or exactly [x, y, width, height] (4 elements).", nameof(value));
    }

    private readonly double[]? _bounds;

    /// <summary>Actions the node supports: invoke, setValue, toggle, select, expand, collapse, etc.</summary>
    [JsonPropertyName("actions")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string[]? Actions { get; init; }

    /// <summary>Structured state bag extracted from the automation surface when available.</summary>
    [JsonPropertyName("state")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyDictionary<string, string>? State { get; init; }

    /// <summary>Structured metadata extracted from the automation surface when available.</summary>
    [JsonPropertyName("metadata")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyDictionary<string, string>? Metadata { get; init; }
}
