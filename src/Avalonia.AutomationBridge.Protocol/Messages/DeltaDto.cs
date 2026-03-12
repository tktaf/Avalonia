using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Avalonia.AutomationBridge.Protocol.Messages;

/// <summary>
/// Compact revision delta emitted after any mutating action or watch event.
/// Contains only changed fields rather than full snapshots to minimise token use.
/// </summary>
public sealed class DeltaDto
{
    /// <summary>Monotonically increasing revision counter for this session.</summary>
    [JsonPropertyName("revision")]
    public required long Revision { get; init; }

    /// <summary>Handle of the element that now holds focus, or null if focus is unchanged.</summary>
    [JsonPropertyName("focus")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Focus { get; init; }

    /// <summary>Nodes whose properties changed.  Each entry contains only the fields that changed.</summary>
    [JsonPropertyName("updated")]
    public required NodePatchDto[] Updated { get; init; }

    /// <summary>Handles of nodes that were added to the tree since the previous revision.</summary>
    [JsonPropertyName("added")]
    public required string[] Added { get; init; }

    /// <summary>Handles of nodes that were removed from the tree since the previous revision.</summary>
    [JsonPropertyName("removed")]
    public required string[] Removed { get; init; }
}

/// <summary>Sparse property update for a single node within a <see cref="DeltaDto"/>.</summary>
public sealed class NodePatchDto
{
    /// <summary>Session-local handle of the affected node.</summary>
    [JsonPropertyName("id")]
    public required string Id { get; init; }

    /// <summary>Updated enabled state, or null if unchanged.</summary>
    [JsonPropertyName("enabled")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public bool? Enabled { get; init; }

    /// <summary>Updated focused state, or null if unchanged.</summary>
    [JsonPropertyName("focused")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public bool? Focused { get; init; }

    /// <summary>Updated value string, or null if unchanged.</summary>
    [JsonPropertyName("value")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Value { get; init; }

    /// <summary>Updated offscreen state, or null if unchanged.</summary>
    [JsonPropertyName("offscreen")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public bool? Offscreen { get; init; }

    /// <summary>Updated selected state, or null if unchanged.</summary>
    [JsonPropertyName("selected")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public bool? Selected { get; init; }

    /// <summary>Updated expanded state, or null if unchanged.</summary>
    [JsonPropertyName("expanded")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public bool? Expanded { get; init; }

    /// <summary>Updated checked state, or null if unchanged.</summary>
    [JsonPropertyName("checked")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public bool? Checked { get; init; }

    /// <summary>Updated name, or null if unchanged.</summary>
    [JsonPropertyName("name")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Name { get; init; }

    /// <summary>Updated metadata bag, or null if unchanged.</summary>
    [JsonPropertyName("metadata")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyDictionary<string, string>? Metadata { get; init; }
}
