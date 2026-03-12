using System.Text.Json.Serialization;

namespace Avalonia.AutomationBridge.Protocol.Messages;

/// <summary>
/// Deterministic in-process selector resolved against the peer tree.
/// All fields are optional; non-null fields are combined with AND semantics.
/// </summary>
public sealed class SelectorDto
{
    /// <summary>Exact <c>AutomationId</c> match.</summary>
    [JsonPropertyName("id")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Id { get; init; }

    /// <summary>Preferred exact <c>AutomationId</c> match alias.</summary>
    [JsonPropertyName("automationId")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? AutomationId { get; init; }

    /// <summary>
    /// Accessible name match.  Exact when <see cref="NameSubstring"/> is false (default),
    /// otherwise treated as a case-insensitive substring.
    /// </summary>
    [JsonPropertyName("name")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Name { get; init; }

    /// <summary>When true, <see cref="Name"/> is matched as a case-insensitive substring.</summary>
    [JsonPropertyName("nameSubstring")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public bool NameSubstring { get; init; }

    /// <summary>Semantic role filter: button, edit, checkbox, listitem, window, etc.</summary>
    [JsonPropertyName("role")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Role { get; init; }

    /// <summary>Simple class name filter.</summary>
    [JsonPropertyName("className")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? ClassName { get; init; }

    /// <summary>When non-null, restricts matches to elements whose focus state equals this value.</summary>
    [JsonPropertyName("focused")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public bool? Focused { get; init; }

    /// <summary>When non-null, restricts matches to elements whose enabled state equals this value.</summary>
    [JsonPropertyName("enabled")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public bool? Enabled { get; init; }

    /// <summary>When non-null, restricts matches to elements whose selected state equals this value.</summary>
    [JsonPropertyName("selected")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public bool? Selected { get; init; }

    /// <summary>When non-null, restricts matches to elements whose visible state equals this value.</summary>
    [JsonPropertyName("visible")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public bool? Visible { get; init; }

    /// <summary>When non-null, restricts matches to elements that expose the requested action.</summary>
    [JsonPropertyName("hasAction")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? HasAction { get; init; }

    /// <summary>Optional state-bag key/value predicates matched against the published automation state.</summary>
    [JsonPropertyName("state")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyDictionary<string, string>? State { get; init; }

    /// <summary>
    /// Restricts evaluation to the subtree rooted at this node handle.
    /// Corresponds to the <c>within</c> concept in the design.
    /// </summary>
    [JsonPropertyName("within")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Within { get; init; }

    /// <summary>Preferred alias for <see cref="Within"/>.</summary>
    [JsonPropertyName("containerId")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? ContainerId { get; init; }

    /// <summary>
    /// Ancestor chain segments evaluated from root to leaf order.  Each entry is matched as a
    /// name or role fragment against the ancestor at that depth.
    /// </summary>
    [JsonPropertyName("path")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string[]? Path { get; init; }

    /// <summary>
    /// Zero-based index tie-breaker applied after all other filters.  Nodes are ordered by
    /// depth-first traversal before <c>nth</c> is applied, giving deterministic results.
    /// </summary>
    [JsonPropertyName("nth")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? Nth { get; init; }

    /// <summary>Optional response projection for query results. <c>id</c>, <c>rootId</c>, and <c>role</c> are always included.</summary>
    [JsonPropertyName("fields")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string[]? Fields { get; init; }
}
