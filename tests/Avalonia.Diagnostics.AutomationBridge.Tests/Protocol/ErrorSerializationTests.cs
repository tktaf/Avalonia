using System.Text.Json;
using Avalonia.AutomationBridge.Protocol.Messages;
using Xunit;

namespace Avalonia.Diagnostics.AutomationBridge.Tests.Protocol;

public sealed class ErrorSerializationTests
{
    private static readonly JsonSerializerOptions s_options = ProtocolTestOptions.Default;

    [Theory]
    [InlineData(BridgeErrorCode.BridgeNotEnabled, "bridge_not_enabled")]
    [InlineData(BridgeErrorCode.RootNotFound, "root_not_found")]
    [InlineData(BridgeErrorCode.NodeNotFound, "node_not_found")]
    [InlineData(BridgeErrorCode.SelectorAmbiguous, "selector_ambiguous")]
    [InlineData(BridgeErrorCode.ActionNotSupported, "action_not_supported")]
    [InlineData(BridgeErrorCode.ElementNotEnabled, "element_not_enabled")]
    [InlineData(BridgeErrorCode.StaleRevision, "stale_revision")]
    public void ErrorCode_HasExpectedStringValue(string constant, string expected)
    {
        Assert.Equal(expected, constant);
    }

    [Fact]
    public void ErrorDto_RoundTrips_CodeAndMessage()
    {
        var original = new ErrorDto
        {
            Code = BridgeErrorCode.NodeNotFound,
            Message = "No node matching the selector was found."
        };

        var json = JsonSerializer.Serialize(original, s_options);
        var restored = JsonSerializer.Deserialize<ErrorDto>(json, s_options);

        Assert.NotNull(restored);
        Assert.Equal(original.Code, restored.Code);
        Assert.Equal(original.Message, restored.Message);
    }

    [Fact]
    public void ErrorDto_StableJsonShape()
    {
        var error = new ErrorDto
        {
            Code = BridgeErrorCode.SelectorAmbiguous,
            Message = "Multiple nodes matched."
        };

        var json = JsonSerializer.Serialize(error, s_options);
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        Assert.Equal("selector_ambiguous", root.GetProperty("code").GetString());
        Assert.Equal("Multiple nodes matched.", root.GetProperty("message").GetString());
    }

    [Fact]
    public void ErrorDto_NullMessage_OmittedFromJson()
    {
        var error = new ErrorDto { Code = BridgeErrorCode.RootNotFound };

        var json = JsonSerializer.Serialize(error, s_options);
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        Assert.Equal("root_not_found", root.GetProperty("code").GetString());
        Assert.False(root.TryGetProperty("message", out _));
    }

    [Fact]
    public void BridgeResponse_Failure_Factory_ProducesTypedError()
    {
        var response = BridgeResponse.Failure(BridgeErrorCode.ElementNotEnabled, "Button is disabled.", "req-1");

        Assert.False(response.Ok);
        Assert.Equal("req-1", response.RequestId);
        Assert.NotNull(response.Error);
        Assert.Equal(BridgeErrorCode.ElementNotEnabled, response.Error.Code);
        Assert.Equal("Button is disabled.", response.Error.Message);
        Assert.Null(response.Nodes);
        Assert.Null(response.Delta);
    }

    [Fact]
    public void BridgeResponse_Failure_RoundTrips()
    {
        var response = BridgeResponse.Failure(BridgeErrorCode.ActionNotSupported, null, "req-2");

        var json = JsonSerializer.Serialize(response, s_options);
        var restored = JsonSerializer.Deserialize<BridgeResponse>(json, s_options);

        Assert.NotNull(restored);
        Assert.False(restored.Ok);
        Assert.NotNull(restored.Error);
        Assert.Equal(BridgeErrorCode.ActionNotSupported, restored.Error.Code);
        Assert.Null(restored.Error.Message);
    }
}
