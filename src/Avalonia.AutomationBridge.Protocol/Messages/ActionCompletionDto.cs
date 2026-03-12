using System.Text.Json.Serialization;

namespace Avalonia.AutomationBridge.Protocol.Messages;

/// <summary>Well-known completion states for a successful mutating bridge action.</summary>
public static class BridgeActionCompletionState
{
    /// <summary>The action was accepted but did not publish an immediate observable delta.</summary>
    public const string Accepted = "accepted";

    /// <summary>The action published an immediate observable delta before returning.</summary>
    public const string Completed = "completed";
}

/// <summary>Completion payload attached to successful mutating bridge responses.</summary>
public sealed class ActionCompletionDto
{
    /// <summary>Machine-readable completion state.</summary>
    [JsonPropertyName("state")]
    public required string State { get; init; }
}
