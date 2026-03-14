using System.Text.Json.Serialization;

namespace Avalonia.AutomationBridge.Protocol.Messages;

/// <summary>Response envelope returned from the in-process bridge to the CLI or tool.</summary>
public sealed class BridgeResponse
{
    /// <summary>Correlation identifier echoed from <see cref="BridgeRequest.RequestId"/>. May be null.</summary>
    [JsonPropertyName("requestId")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? RequestId { get; init; }

    /// <summary>True when the action completed without error.</summary>
    [JsonPropertyName("ok")]
    public required bool Ok { get; init; }

    /// <summary>Error payload when <see cref="Ok"/> is false. Null on success.</summary>
    [JsonPropertyName("error")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public ErrorDto? Error { get; init; }

    /// <summary>
    /// Node summaries returned by query, describe, or roots requests.
    /// Empty array when the action returned no nodes.
    /// </summary>
    [JsonPropertyName("nodes")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public NodeSummaryDto[]? Nodes { get; init; }

    /// <summary>
    /// Revision delta included in responses from mutating actions (invoke, set-value, toggle, etc.)
    /// and watch events.  Null for read-only requests.
    /// </summary>
    [JsonPropertyName("delta")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public DeltaDto? Delta { get; init; }

    /// <summary>
    /// Completion state for successful mutating actions. Null for read-only requests and failures.
    /// </summary>
    [JsonPropertyName("completion")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public ActionCompletionDto? Completion { get; init; }

    /// <summary>
    /// Base64-encoded PNG image data for screenshot responses. Null for non-screenshot requests.
    /// </summary>
    [JsonPropertyName("image")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Image { get; init; }

    /// <summary>
    /// Bounding rectangle of the captured element as [x, y, width, height]. Null for non-screenshot requests.
    /// </summary>
    [JsonPropertyName("imageBounds")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public double[]? ImageBounds { get; init; }

    // --- Factory helpers -------------------------------------------------

    /// <summary>Creates a successful response with no payload.</summary>
    public static BridgeResponse Success(string? requestId = null) =>
        new() { RequestId = requestId, Ok = true };

    /// <summary>Creates a successful response containing node summaries.</summary>
    public static BridgeResponse WithNodes(NodeSummaryDto[] nodes, string? requestId = null) =>
        new() { RequestId = requestId, Ok = true, Nodes = nodes };

    /// <summary>Creates a successful response containing a revision delta.</summary>
    public static BridgeResponse WithDelta(DeltaDto delta, string? requestId = null) =>
        new() { RequestId = requestId, Ok = true, Delta = delta };

    /// <summary>Creates a successful response containing both a revision delta and completion state.</summary>
    public static BridgeResponse WithCompletion(
        DeltaDto delta,
        string completionState,
        string? requestId = null) =>
        new()
        {
            RequestId = requestId,
            Ok = true,
            Delta = delta,
            Completion = new ActionCompletionDto { State = completionState },
        };

    /// <summary>Creates a successful response containing a screenshot image.</summary>
    public static BridgeResponse WithScreenshot(string base64Png, double[] bounds, string? requestId = null) =>
        new() { RequestId = requestId, Ok = true, Image = base64Png, ImageBounds = bounds };

    /// <summary>Creates an error response using the supplied typed error.</summary>
    public static BridgeResponse Failure(ErrorDto error, string? requestId = null) =>
        new() { RequestId = requestId, Ok = false, Error = error };

    /// <summary>Creates an error response using a well-known error code and optional message.</summary>
    public static BridgeResponse Failure(string code, string? message = null, string? requestId = null) =>
        Failure(new ErrorDto { Code = code, Message = message }, requestId);
}
