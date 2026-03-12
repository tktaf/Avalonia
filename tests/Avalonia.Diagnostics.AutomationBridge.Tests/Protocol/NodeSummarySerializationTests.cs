using System.Text.Json;
using Avalonia.AutomationBridge.Protocol.Messages;
using Xunit;

namespace Avalonia.Diagnostics.AutomationBridge.Tests.Protocol;

public sealed class NodeSummarySerializationTests
{
    private static readonly JsonSerializerOptions s_options = ProtocolTestOptions.Default;

    [Fact]
    public void NodeSummaryDto_RoundTrips_AllFields()
    {
        var original = new NodeSummaryDto
        {
            Id = "n42",
            RootId = "w1",
            Role = "button",
            Name = "Save",
            AutomationId = "SaveButton",
            ClassName = "Button",
            Enabled = true,
            Focused = false,
            Offscreen = false,
            Selected = true,
            Expanded = false,
            Checked = null,
            Value = null,
            Bounds = [120, 40, 88, 32],
            Actions = ["invoke"],
            Metadata = new Dictionary<string, string>
            {
                ["DisplayName"] = "Save",
                ["Key"] = "save-primary",
            }
        };

        var json = JsonSerializer.Serialize(original, s_options);
        var restored = JsonSerializer.Deserialize<NodeSummaryDto>(json, s_options);

        Assert.NotNull(restored);
        Assert.Equal(original.Id, restored.Id);
        Assert.Equal(original.RootId, restored.RootId);
        Assert.Equal(original.Role, restored.Role);
        Assert.Equal(original.Name, restored.Name);
        Assert.Equal(original.AutomationId, restored.AutomationId);
        Assert.Equal(original.ClassName, restored.ClassName);
        Assert.Equal(original.Enabled, restored.Enabled);
        Assert.Equal(original.Focused, restored.Focused);
        Assert.Equal(original.Offscreen, restored.Offscreen);
        Assert.Equal(original.Selected, restored.Selected);
        Assert.Equal(original.Expanded, restored.Expanded);
        Assert.Equal(original.Checked, restored.Checked);
        Assert.Null(restored.Value);
        Assert.Equal(original.Bounds, restored.Bounds);
        Assert.Equal(original.Actions, restored.Actions);
        Assert.Equal(original.Metadata, restored.Metadata);
    }

    [Fact]
    public void NodeSummaryDto_StableJsonShape_MatchesDesignSpec()
    {
        var node = new NodeSummaryDto
        {
            Id = "n42",
            RootId = "w1",
            Role = "button",
            Name = "Save",
            AutomationId = "SaveButton",
            ClassName = "Button",
            Enabled = true,
            Focused = false,
            Offscreen = false,
            Selected = true,
            Value = null,
            Bounds = [120.0, 40.0, 88.0, 32.0],
            Actions = ["invoke"],
            Metadata = new Dictionary<string, string>
            {
                ["DisplayName"] = "Save",
            }
        };

        var json = JsonSerializer.Serialize(node, s_options);
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        Assert.Equal("n42", root.GetProperty("id").GetString());
        Assert.Equal("w1", root.GetProperty("rootId").GetString());
        Assert.Equal("button", root.GetProperty("role").GetString());
        Assert.Equal("Save", root.GetProperty("name").GetString());
        Assert.Equal("SaveButton", root.GetProperty("automationId").GetString());
        Assert.Equal("Button", root.GetProperty("className").GetString());
        Assert.True(root.GetProperty("enabled").GetBoolean());
        Assert.False(root.GetProperty("focused").GetBoolean());
        Assert.False(root.GetProperty("offscreen").GetBoolean());
        Assert.True(root.GetProperty("selected").GetBoolean());
        // value is null so omitted under WhenWritingNull
        Assert.False(root.TryGetProperty("value", out _));
        var bounds = root.GetProperty("bounds");
        Assert.Equal(4, bounds.GetArrayLength());
        Assert.Equal("invoke", root.GetProperty("actions")[0].GetString());
        Assert.Equal("Save", root.GetProperty("metadata").GetProperty("DisplayName").GetString());
    }

    [Fact]
    public void NodeSummaryDto_NullOptionalFields_OmittedFromJson()
    {
        var node = new NodeSummaryDto
        {
            Id = "n1",
            RootId = "w1",
            Role = "pane",
            Enabled = true,
            Focused = false,
            Offscreen = false,
            Actions = []
        };

        var json = JsonSerializer.Serialize(node, s_options);
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        Assert.False(root.TryGetProperty("name", out _));
        Assert.False(root.TryGetProperty("automationId", out _));
        Assert.False(root.TryGetProperty("className", out _));
        Assert.False(root.TryGetProperty("value", out _));
        Assert.False(root.TryGetProperty("bounds", out _));
        Assert.False(root.TryGetProperty("selected", out _));
        Assert.False(root.TryGetProperty("expanded", out _));
        Assert.False(root.TryGetProperty("checked", out _));
        Assert.False(root.TryGetProperty("metadata", out _));
    }

    [Fact]
    public void NodeSummaryDto_NullOptionalFields_AreOmittedWithoutSerializerSpecificOptions()
    {
        var node = new NodeSummaryDto
        {
            Id = "n1",
            RootId = "w1",
            Role = "pane",
            Enabled = true,
            Focused = false,
            Offscreen = false,
            Actions = []
        };

        var json = JsonSerializer.Serialize(node);
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        Assert.False(root.TryGetProperty("name", out _));
        Assert.False(root.TryGetProperty("automationId", out _));
        Assert.False(root.TryGetProperty("className", out _));
        Assert.False(root.TryGetProperty("value", out _));
        Assert.False(root.TryGetProperty("bounds", out _));
        Assert.False(root.TryGetProperty("selected", out _));
        Assert.False(root.TryGetProperty("expanded", out _));
        Assert.False(root.TryGetProperty("checked", out _));
        Assert.False(root.TryGetProperty("metadata", out _));
    }

    [Fact]
    public void NodeSummaryDto_Bounds_RejectsNonFourElementArray()
    {
        Assert.Throws<ArgumentException>(() => new NodeSummaryDto
        {
            Id = "n1",
            RootId = "w1",
            Role = "pane",
            Enabled = true,
            Focused = false,
            Offscreen = false,
            Actions = [],
            Bounds = [10, 20, 30] // 3 elements — invalid
        });
    }

    [Fact]
    public void NodeSummaryDto_Bounds_AcceptsNull()
    {
        var node = new NodeSummaryDto
        {
            Id = "n1",
            RootId = "w1",
            Role = "pane",
            Enabled = true,
            Focused = false,
            Offscreen = false,
            Actions = [],
            Bounds = null
        };

        Assert.Null(node.Bounds);
    }

    [Fact]
    public void NodeSummaryDto_Bounds_InvalidJsonPayload_ThrowsArgumentException()
    {
        const string json = """
            {
              "id": "n1",
              "rootId": "w1",
              "role": "pane",
              "enabled": true,
              "focused": false,
              "offscreen": false,
              "bounds": [10, 20, 30],
              "actions": []
            }
            """;

        var exception = Assert.Throws<ArgumentException>(() => JsonSerializer.Deserialize<NodeSummaryDto>(json, s_options));
        Assert.Contains("Bounds must be null or exactly", exception.Message);
    }
}
