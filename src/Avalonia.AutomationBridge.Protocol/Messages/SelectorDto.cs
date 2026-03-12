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
    public string? Id { get; init; }

    /// <summary>
    /// Accessible name match.  Exact when <see cref="NameSubstring"/> is false (default),
    /// otherwise treated as a case-insensitive substring.
    /// </summary>
    [JsonPropertyName("name")]
    public string? Name { get; init; }

    /// <summary>When true, <see cref="Name"/> is matched as a case-insensitive substring.</summary>
    [JsonPropertyName("nameSubstring")]
    public bool NameSubstring { get; init; }

    /// <summary>Semantic role filter: button, edit, checkbox, listitem, window, etc.</summary>
    [JsonPropertyName("role")]
    public string? Role { get; init; }

    /// <summary>Simple class name filter.</summary>
    [JsonPropertyName("className")]
    public string? ClassName { get; init; }

    /// <summary>When non-null, restricts matches to elements whose focus state equals this value.</summary>
    [JsonPropertyName("focused")]
    public bool? Focused { get; init; }

    /// <summary>When non-null, restricts matches to elements whose enabled state equals this value.</summary>
    [JsonPropertyName("enabled")]
    public bool? Enabled { get; init; }

    /// <summary>
    /// Restricts evaluation to the subtree rooted at this node handle.
    /// Corresponds to the <c>within</c> concept in the design.
    /// </summary>
    [JsonPropertyName("within")]
    public string? Within { get; init; }

    /// <summary>
    /// Ancestor chain segments evaluated from root to leaf order.  Each entry is matched as a
    /// name or role fragment against the ancestor at that depth.
    /// </summary>
    [JsonPropertyName("path")]
    public string[]? Path { get; init; }

    /// <summary>
    /// Zero-based index tie-breaker applied after all other filters.  Nodes are ordered by
    /// depth-first traversal before <c>nth</c> is applied, giving deterministic results.
    /// </summary>
    [JsonPropertyName("nth")]
    public int? Nth { get; init; }
}
