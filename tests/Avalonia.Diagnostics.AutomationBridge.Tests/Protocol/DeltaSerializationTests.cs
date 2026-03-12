using System.Text.Json;
using Avalonia.AutomationBridge.Protocol.Messages;
using Xunit;

namespace Avalonia.Diagnostics.AutomationBridge.Tests.Protocol;

public sealed class DeltaSerializationTests
{
    private static readonly JsonSerializerOptions s_options = new()
    {
        WriteIndented = false,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
    };

    [Fact]
    public void DeltaDto_RoundTrips_AllFields()
    {
        var original = new DeltaDto
        {
            Revision = 17,
            Focus = "n55",
            Updated =
            [
                new NodePatchDto { Id = "n42", Enabled = false },
                new NodePatchDto { Id = "n55", Focused = true }
            ],
            Added = [],
            Removed = []
        };

        var json = JsonSerializer.Serialize(original, s_options);
        var restored = JsonSerializer.Deserialize<DeltaDto>(json, s_options);

        Assert.NotNull(restored);
        Assert.Equal(17, restored.Revision);
        Assert.Equal("n55", restored.Focus);
        Assert.Equal(2, restored.Updated.Length);
        Assert.Equal("n42", restored.Updated[0].Id);
        Assert.False(restored.Updated[0].Enabled);
        Assert.Equal("n55", restored.Updated[1].Id);
        Assert.True(restored.Updated[1].Focused);
        Assert.Empty(restored.Added);
        Assert.Empty(restored.Removed);
    }

    [Fact]
    public void DeltaDto_StableJsonShape_MatchesDesignSpec()
    {
        // Shape matches the design document example
        var delta = new DeltaDto
        {
            Revision = 17,
            Focus = "n55",
            Updated =
            [
                new NodePatchDto { Id = "n42", Enabled = false },
                new NodePatchDto { Id = "n55", Focused = true }
            ],
            Added = [],
            Removed = []
        };

        var json = JsonSerializer.Serialize(delta, s_options);
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        Assert.Equal(17, root.GetProperty("revision").GetInt64());
        Assert.Equal("n55", root.GetProperty("focus").GetString());

        var updated = root.GetProperty("updated");
        Assert.Equal(2, updated.GetArrayLength());

        var first = updated[0];
        Assert.Equal("n42", first.GetProperty("id").GetString());
        Assert.False(first.GetProperty("enabled").GetBoolean());

        var second = updated[1];
        Assert.Equal("n55", second.GetProperty("id").GetString());
        Assert.True(second.GetProperty("focused").GetBoolean());

        Assert.Equal(0, root.GetProperty("added").GetArrayLength());
        Assert.Equal(0, root.GetProperty("removed").GetArrayLength());
    }

    [Fact]
    public void DeltaDto_NoFocusChange_FocusOmittedFromJson()
    {
        var delta = new DeltaDto
        {
            Revision = 5,
            Focus = null,
            Updated = [],
            Added = ["n100"],
            Removed = []
        };

        var json = JsonSerializer.Serialize(delta, s_options);
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        Assert.False(root.TryGetProperty("focus", out _));
        Assert.Equal(1, root.GetProperty("added").GetArrayLength());
        Assert.Equal("n100", root.GetProperty("added")[0].GetString());
    }

    [Fact]
    public void NodePatchDto_SparseFields_UnchangedFieldsOmittedFromJson()
    {
        // Only the changed fields should appear; null fields are omitted
        var patch = new NodePatchDto { Id = "n10", Value = "hello" };

        var json = JsonSerializer.Serialize(patch, s_options);
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        Assert.Equal("n10", root.GetProperty("id").GetString());
        Assert.Equal("hello", root.GetProperty("value").GetString());
        Assert.False(root.TryGetProperty("enabled", out _));
        Assert.False(root.TryGetProperty("focused", out _));
        Assert.False(root.TryGetProperty("offscreen", out _));
        Assert.False(root.TryGetProperty("name", out _));
    }

    [Fact]
    public void BridgeResponse_WithDelta_RoundTrips()
    {
        var delta = new DeltaDto
        {
            Revision = 3,
            Focus = null,
            Updated = [new NodePatchDto { Id = "n7", Enabled = true }],
            Added = [],
            Removed = ["n2"]
        };

        var response = BridgeResponse.WithDelta(delta, "req-invoke");

        var json = JsonSerializer.Serialize(response, s_options);
        var restored = JsonSerializer.Deserialize<BridgeResponse>(json, s_options);

        Assert.NotNull(restored);
        Assert.True(restored.Ok);
        Assert.Equal("req-invoke", restored.RequestId);
        Assert.Null(restored.Error);
        Assert.NotNull(restored.Delta);
        Assert.Equal(3, restored.Delta.Revision);
        Assert.Single(restored.Delta.Updated);
        Assert.Single(restored.Delta.Removed);
        Assert.Equal("n2", restored.Delta.Removed[0]);
    }
}
