using System.Text.Json;
using Avalonia.AutomationBridge.Protocol.Messages;

namespace Avalonia.Diagnostics.AutomationBridge.Transport;

internal static class AutomationBridgeJsonSerializer
{
    private static readonly JsonSerializerOptions s_options = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true,
    };

    public static BridgeRequest DeserializeRequest(string json)
        => JsonSerializer.Deserialize<BridgeRequest>(json, s_options)
           ?? throw new JsonException("Request payload deserialized to null.");

    public static string SerializeResponse(BridgeResponse response)
        => JsonSerializer.Serialize(response, s_options);
}
