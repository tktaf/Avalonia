using Avalonia;
using Avalonia.Automation.Peers;
using Avalonia.Automation.Provider;
using Avalonia.Diagnostics.AutomationBridge.Snapshot;
using Xunit;

namespace Avalonia.Diagnostics.AutomationBridge.Tests.Session;

/// <summary>
/// Tests for <see cref="AutomationNodeSummaryBuilder"/>.
/// Proves summaries stay limited to the protocol fields and are correctly projected.
/// </summary>
public sealed class AutomationNodeSummaryBuilderTests
{
    [Fact]
    public void Build_SetsId_FromHandle()
    {
        var peer = new StubAutomationPeer();

        var dto = AutomationNodeSummaryBuilder.Build(peer, "n1", "w1");

        Assert.Equal("n1", dto.Id);
    }

    [Fact]
    public void Build_SetsRootId_FromRootHandle()
    {
        var peer = new StubAutomationPeer();

        var dto = AutomationNodeSummaryBuilder.Build(peer, "n1", "w2");

        Assert.Equal("w2", dto.RootId);
    }

    [Fact]
    public void Build_SetsRole_FromControlType()
    {
        var peer = new StubAutomationPeer { ControlType = AutomationControlType.Button };

        var dto = AutomationNodeSummaryBuilder.Build(peer, "n1", "w1");

        Assert.Equal("button", dto.Role);
    }

    [Theory]
    [InlineData(AutomationControlType.CheckBox, "checkbox")]
    [InlineData(AutomationControlType.Edit, "edit")]
    [InlineData(AutomationControlType.List, "list")]
    [InlineData(AutomationControlType.ListItem, "listitem")]
    [InlineData(AutomationControlType.Window, "window")]
    [InlineData(AutomationControlType.Expander, "expander")]
    [InlineData(AutomationControlType.None, "none")]
    public void Build_MapsControlType_ToLowercaseRole(AutomationControlType type, string expectedRole)
    {
        var peer = new StubAutomationPeer { ControlType = type };

        var dto = AutomationNodeSummaryBuilder.Build(peer, "n1", "w1");

        Assert.Equal(expectedRole, dto.Role);
    }

    [Fact]
    public void Build_SetsName_FromPeer()
    {
        var peer = new StubAutomationPeer { Name = "Submit" };

        var dto = AutomationNodeSummaryBuilder.Build(peer, "n1", "w1");

        Assert.Equal("Submit", dto.Name);
    }

    [Fact]
    public void Build_SetsName_ToNull_WhenEmpty()
    {
        var peer = new StubAutomationPeer { Name = "" };

        var dto = AutomationNodeSummaryBuilder.Build(peer, "n1", "w1");

        Assert.Null(dto.Name);
    }

    [Fact]
    public void Build_SanitizesStructuredObjectNames_IntoCleanLabelAndMetadata()
    {
        var peer = new StubAutomationPeer
        {
            Name = "PlayStyleOptionViewModel { RelationshipModeId = general_manager, OwnerScope = single_team, DisplayName = General Manager }",
        };

        var dto = AutomationNodeSummaryBuilder.Build(peer, "n1", "w1");

        Assert.Equal("General Manager", dto.Name);
        Assert.NotNull(dto.Metadata);
        Assert.Equal("PlayStyleOptionViewModel", dto.Metadata!["sourceType"]);
        Assert.Equal("general_manager", dto.Metadata["RelationshipModeId"]);
        Assert.Equal("single_team", dto.Metadata["OwnerScope"]);
        Assert.Equal("General Manager", dto.Metadata["DisplayName"]);
    }

    [Fact]
    public void Build_SetsAutomationId_FromPeer()
    {
        var peer = new StubAutomationPeer { AutomationId = "submit-btn" };

        var dto = AutomationNodeSummaryBuilder.Build(peer, "n1", "w1");

        Assert.Equal("submit-btn", dto.AutomationId);
    }

    [Fact]
    public void Build_SetsAutomationId_ToNull_WhenEmpty()
    {
        var peer = new StubAutomationPeer { AutomationId = "" };

        var dto = AutomationNodeSummaryBuilder.Build(peer, "n1", "w1");

        Assert.Null(dto.AutomationId);
    }

    [Fact]
    public void Build_SetsClassName_FromPeer()
    {
        var peer = new StubAutomationPeer { ClassName = "Button" };

        var dto = AutomationNodeSummaryBuilder.Build(peer, "n1", "w1");

        Assert.Equal("Button", dto.ClassName);
    }

    [Fact]
    public void Build_SetsEnabled_True_WhenPeerIsEnabled()
    {
        var peer = new StubAutomationPeer { Enabled = true };

        var dto = AutomationNodeSummaryBuilder.Build(peer, "n1", "w1");

        Assert.True(dto.Enabled);
    }

    [Fact]
    public void Build_SetsEnabled_False_WhenPeerIsDisabled()
    {
        var peer = new StubAutomationPeer { Enabled = false };

        var dto = AutomationNodeSummaryBuilder.Build(peer, "n1", "w1");

        Assert.False(dto.Enabled);
    }

    [Fact]
    public void Build_SetsFocused_WhenPeerHasKeyboardFocus()
    {
        var peer = new StubAutomationPeer { KeyboardFocus = true };

        var dto = AutomationNodeSummaryBuilder.Build(peer, "n1", "w1");

        Assert.True(dto.Focused);
    }

    [Fact]
    public void Build_SetsValue_FromValueProvider()
    {
        var peer = new StubAutomationPeer();
        peer.RegisterProvider<IValueProvider>(new StubValueProvider("hello"));

        var dto = AutomationNodeSummaryBuilder.Build(peer, "n1", "w1");

        Assert.Equal("hello", dto.Value);
    }

    [Fact]
    public void Build_SetsValue_ToNull_WhenNoValueProvider()
    {
        var peer = new StubAutomationPeer();

        var dto = AutomationNodeSummaryBuilder.Build(peer, "n1", "w1");

        Assert.Null(dto.Value);
    }

    [Fact]
    public void Build_SetsBounds_AsXYWidthHeight()
    {
        var peer = new StubAutomationPeer { BoundingRectangle = new Rect(10, 20, 100, 50) };

        var dto = AutomationNodeSummaryBuilder.Build(peer, "n1", "w1");

        Assert.NotNull(dto.Bounds);
        Assert.Equal(4, dto.Bounds!.Length);
        Assert.Equal(10, dto.Bounds[0]);
        Assert.Equal(20, dto.Bounds[1]);
        Assert.Equal(100, dto.Bounds[2]);
        Assert.Equal(50, dto.Bounds[3]);
    }

    [Fact]
    public void Build_SetsSelected_FromSelectionItemProvider()
    {
        var peer = new StubAutomationPeer();
        peer.RegisterProvider<ISelectionItemProvider>(new StubSelectionItemProvider(isSelected: true));

        var dto = AutomationNodeSummaryBuilder.Build(peer, "n1", "w1");

        Assert.True(dto.Selected);
    }

    [Fact]
    public void Build_SetsExpanded_FromExpandCollapseProvider()
    {
        var peer = new StubAutomationPeer();
        peer.RegisterProvider<IExpandCollapseProvider>(
            new StubExpandCollapseProvider(Avalonia.Automation.ExpandCollapseState.Expanded));

        var dto = AutomationNodeSummaryBuilder.Build(peer, "n1", "w1");

        Assert.True(dto.Expanded);
    }

    [Fact]
    public void Build_SetsChecked_FromToggleProvider()
    {
        var peer = new StubAutomationPeer();
        peer.RegisterProvider<IToggleProvider>(new StubToggleProvider(ToggleState.On));

        var dto = AutomationNodeSummaryBuilder.Build(peer, "n1", "w1");

        Assert.True(dto.Checked);
    }

    [Fact]
    public void Build_ParsesState_FromItemStatus()
    {
        var peer = new StubAutomationPeer
        {
            ItemStatus = "busy; modal; currentStep=7; currentTab=Contract; navigationContext=Players",
        };

        var dto = AutomationNodeSummaryBuilder.Build(peer, "n1", "w1");

        Assert.NotNull(dto.State);
        Assert.Equal("true", dto.State!["busy"]);
        Assert.Equal("true", dto.State["modal"]);
        Assert.Equal("7", dto.State["currentStep"]);
        Assert.Equal("Contract", dto.State["currentTab"]);
        Assert.Equal("Players", dto.State["navigationContext"]);
    }

    // -------------------------------------------------------------------------
    // Actions
    // -------------------------------------------------------------------------

    [Fact]
    public void Build_IncludesInvokeAction_WhenInvokeProviderPresent()
    {
        var peer = new StubAutomationPeer();
        peer.RegisterProvider<IInvokeProvider>(new StubInvokeProvider());

        var dto = AutomationNodeSummaryBuilder.Build(peer, "n1", "w1");

        Assert.NotNull(dto.Actions);
        Assert.Contains("invoke", dto.Actions!);
    }

    [Fact]
    public void Build_IncludesSetValueAction_WhenValueProviderIsWritable()
    {
        var peer = new StubAutomationPeer();
        peer.RegisterProvider<IValueProvider>(new StubValueProvider("x", isReadOnly: false));

        var dto = AutomationNodeSummaryBuilder.Build(peer, "n1", "w1");

        Assert.NotNull(dto.Actions);
        Assert.Contains("setValue", dto.Actions!);
    }

    [Fact]
    public void Build_ExcludesSetValueAction_WhenValueProviderIsReadOnly()
    {
        var peer = new StubAutomationPeer();
        peer.RegisterProvider<IValueProvider>(new StubValueProvider("x", isReadOnly: true));

        var dto = AutomationNodeSummaryBuilder.Build(peer, "n1", "w1");

        Assert.NotNull(dto.Actions);
        Assert.DoesNotContain("setValue", dto.Actions!);
    }

    [Fact]
    public void Build_IncludesToggleAction_WhenToggleProviderPresent()
    {
        var peer = new StubAutomationPeer();
        peer.RegisterProvider<IToggleProvider>(new StubToggleProvider());

        var dto = AutomationNodeSummaryBuilder.Build(peer, "n1", "w1");

        Assert.NotNull(dto.Actions);
        Assert.Contains("toggle", dto.Actions!);
    }

    [Fact]
    public void Build_IncludesSelectAction_WhenSelectionItemProviderPresent()
    {
        var peer = new StubAutomationPeer();
        peer.RegisterProvider<ISelectionItemProvider>(new StubSelectionItemProvider());

        var dto = AutomationNodeSummaryBuilder.Build(peer, "n1", "w1");

        Assert.NotNull(dto.Actions);
        Assert.Contains("select", dto.Actions!);
    }

    [Fact]
    public void Build_IncludesExpandAction_WhenExpandCollapseProviderPresent()
    {
        var peer = new StubAutomationPeer();
        peer.RegisterProvider<IExpandCollapseProvider>(
            new StubExpandCollapseProvider(Avalonia.Automation.ExpandCollapseState.Collapsed));

        var dto = AutomationNodeSummaryBuilder.Build(peer, "n1", "w1");

        Assert.NotNull(dto.Actions);
        Assert.Contains("expand", dto.Actions!);
    }

    [Fact]
    public void Build_IncludesSetFocusAction_WhenKeyboardFocusable()
    {
        var peer = new StubAutomationPeer { KeyboardFocusable = true };

        var dto = AutomationNodeSummaryBuilder.Build(peer, "n1", "w1");

        Assert.NotNull(dto.Actions);
        Assert.Contains("setFocus", dto.Actions!);
    }

    [Fact]
    public void Build_ActionsIsEmpty_WhenNoProvidersPresent()
    {
        var peer = new StubAutomationPeer();

        var dto = AutomationNodeSummaryBuilder.Build(peer, "n1", "w1");

        Assert.NotNull(dto.Actions);
        Assert.Empty(dto.Actions!);
    }

    // -------------------------------------------------------------------------
    // Stub provider helpers
    // -------------------------------------------------------------------------

    private sealed class StubValueProvider : IValueProvider
    {
        public StubValueProvider(string? value, bool isReadOnly = false)
        {
            Value = value;
            IsReadOnly = isReadOnly;
        }

        public bool IsReadOnly { get; }
        public string? Value { get; }
        public void SetValue(string? value) { }
    }

    private sealed class StubInvokeProvider : IInvokeProvider
    {
        public void Invoke() { }
    }

    private sealed class StubToggleProvider : IToggleProvider
    {
        public StubToggleProvider(ToggleState toggleState = ToggleState.Off)
        {
            ToggleState = toggleState;
        }

        public ToggleState ToggleState { get; }
        public void Toggle() { }
    }

    private sealed class StubSelectionItemProvider : ISelectionItemProvider
    {
        public StubSelectionItemProvider(bool isSelected = false)
        {
            IsSelected = isSelected;
        }

        public bool IsSelected { get; }
        public ISelectionProvider? SelectionContainer => null;
        public void AddToSelection() { }
        public void RemoveFromSelection() { }
        public void Select() { }
    }

    private sealed class StubExpandCollapseProvider : IExpandCollapseProvider
    {
        public StubExpandCollapseProvider(Avalonia.Automation.ExpandCollapseState expandCollapseState)
        {
            ExpandCollapseState = expandCollapseState;
        }

        public Avalonia.Automation.ExpandCollapseState ExpandCollapseState { get; }
        public bool ShowsMenu => false;
        public void Expand() { }
        public void Collapse() { }
    }
}
