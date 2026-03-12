using System.Text.Json;
using Avalonia.AutomationBridge.Protocol.Messages;
using Xunit;

namespace Avalonia.Diagnostics.AutomationBridge.Tests.Protocol;

public sealed class SelectorSerializationTests
{
    private static readonly JsonSerializerOptions s_options = ProtocolTestOptions.Default;

    [Fact]
    public void SelectorDto_RoundTrips_AllFields()
    {
        var original = new SelectorDto
        {
            Id = "SaveButton",
            AutomationId = "SaveButton",
            Name = "Save",
            NameSubstring = false,
            Role = "button",
            ClassName = "Button",
            Focused = null,
            Enabled = true,
            Within = "n10",
            ContainerId = "n10",
            Selected = true,
            Visible = true,
            HasAction = "invoke",
            Fields = ["name", "selected"],
            Path = ["MainWindow", "toolbar"],
            Nth = 0
        };

        var json = JsonSerializer.Serialize(original, s_options);
        var restored = JsonSerializer.Deserialize<SelectorDto>(json, s_options);

        Assert.NotNull(restored);
        Assert.Equal(original.Id, restored.Id);
        Assert.Equal(original.AutomationId, restored.AutomationId);
        Assert.Equal(original.Name, restored.Name);
        Assert.Equal(original.NameSubstring, restored.NameSubstring);
        Assert.Equal(original.Role, restored.Role);
        Assert.Equal(original.ClassName, restored.ClassName);
        Assert.Null(restored.Focused);
        Assert.Equal(original.Enabled, restored.Enabled);
        Assert.Equal(original.Within, restored.Within);
        Assert.Equal(original.ContainerId, restored.ContainerId);
        Assert.Equal(original.Selected, restored.Selected);
        Assert.Equal(original.Visible, restored.Visible);
        Assert.Equal(original.HasAction, restored.HasAction);
        Assert.Equal(original.Fields, restored.Fields);
        Assert.Equal(original.Path, restored.Path);
        Assert.Equal(0, restored.Nth);
    }

    [Fact]
    public void SelectorDto_StableJsonShape_CamelCaseFieldNames()
    {
        var selector = new SelectorDto
        {
            Role = "button",
            Name = "Save",
            Enabled = true
        };

        var json = JsonSerializer.Serialize(selector, s_options);
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        // Verify camelCase field names
        Assert.True(root.TryGetProperty("role", out var roleEl));
        Assert.Equal("button", roleEl.GetString());
        Assert.True(root.TryGetProperty("name", out var nameEl));
        Assert.Equal("Save", nameEl.GetString());
        Assert.True(root.TryGetProperty("enabled", out var enabledEl));
        Assert.True(enabledEl.GetBoolean());
    }

    [Fact]
    public void SelectorDto_NullOptionalFields_OmittedFromJson()
    {
        var selector = new SelectorDto { Role = "button" };

        var json = JsonSerializer.Serialize(selector, s_options);
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        Assert.False(root.TryGetProperty("id", out _));
        Assert.False(root.TryGetProperty("automationId", out _));
        Assert.False(root.TryGetProperty("name", out _));
        Assert.False(root.TryGetProperty("className", out _));
        Assert.False(root.TryGetProperty("focused", out _));
        Assert.False(root.TryGetProperty("enabled", out _));
        Assert.False(root.TryGetProperty("within", out _));
        Assert.False(root.TryGetProperty("containerId", out _));
        Assert.False(root.TryGetProperty("selected", out _));
        Assert.False(root.TryGetProperty("visible", out _));
        Assert.False(root.TryGetProperty("hasAction", out _));
        Assert.False(root.TryGetProperty("fields", out _));
        Assert.False(root.TryGetProperty("path", out _));
        Assert.False(root.TryGetProperty("nth", out _));
    }

    [Fact]
    public void SelectorDto_SubstringName_PreservesFlag()
    {
        var selector = new SelectorDto { Name = "ave", NameSubstring = true };

        var json = JsonSerializer.Serialize(selector, s_options);
        var restored = JsonSerializer.Deserialize<SelectorDto>(json, s_options);

        Assert.NotNull(restored);
        Assert.Equal("ave", restored.Name);
        Assert.True(restored.NameSubstring);
    }

    [Fact]
    public void SelectorDto_NameSubstring_OmittedFromJsonWhenFalse()
    {
        var selector = new SelectorDto { Name = "Save", NameSubstring = false };

        var json = JsonSerializer.Serialize(selector, s_options);
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        Assert.True(root.TryGetProperty("name", out _));
        Assert.False(root.TryGetProperty("nameSubstring", out _),
            "nameSubstring must be omitted from JSON when false (default) to avoid token noise.");
    }

    [Fact]
    public void SelectorDto_NameSubstring_IncludedInJsonWhenTrue()
    {
        var selector = new SelectorDto { Name = "Save", NameSubstring = true };

        var json = JsonSerializer.Serialize(selector, s_options);
        using var doc = JsonDocument.Parse(json);

        Assert.True(doc.RootElement.TryGetProperty("nameSubstring", out var el));
        Assert.True(el.GetBoolean());
    }
}
