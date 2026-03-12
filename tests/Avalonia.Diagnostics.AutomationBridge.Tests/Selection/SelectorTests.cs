using System.Linq;
using Avalonia.Automation.Peers;
using Avalonia.Automation.Provider;
using Avalonia.AutomationBridge.Protocol.Messages;
using Avalonia.Diagnostics.AutomationBridge.Selection;
using Avalonia.Diagnostics.AutomationBridge.Session;
using Avalonia.Diagnostics.AutomationBridge.Tests.Session;
using Xunit;

namespace Avalonia.Diagnostics.AutomationBridge.Tests.Selection;

public sealed class SelectorTests
{
    [Fact]
    public void Evaluate_FindsNode_ByExactName()
    {
        var fixture = CreateFixture();
        var response = AutomationSelectorEvaluator.Evaluate(
            fixture.Session,
            fixture.RootId,
            new SelectorDto { Name = "Save" });
        var nodes = Assert.IsType<NodeSummaryDto[]>(response.Nodes);

        Assert.True(response.Ok);
        Assert.Single(nodes);
        Assert.Equal("Save", nodes[0].Name);
    }

    [Fact]
    public void Evaluate_FindsNode_ByNameSubstring()
    {
        var fixture = CreateFixture();
        var response = AutomationSelectorEvaluator.Evaluate(
            fixture.Session,
            fixture.RootId,
            new SelectorDto { Name = "save", NameSubstring = true },
            maxResults: 2);
        var nodes = Assert.IsType<NodeSummaryDto[]>(response.Nodes);

        Assert.True(response.Ok);
        Assert.Equal(2, nodes.Length);
        Assert.Equal(new[] { "Save", "Save As" }, nodes.Select(x => x.Name));
    }

    [Fact]
    public void Evaluate_FindsNode_ByAutomationId()
    {
        var fixture = CreateFixture();
        var response = AutomationSelectorEvaluator.Evaluate(
            fixture.Session,
            fixture.RootId,
            new SelectorDto { Id = "save-secondary" });
        var nodes = Assert.IsType<NodeSummaryDto[]>(response.Nodes);

        Assert.True(response.Ok);
        Assert.Single(nodes);
        Assert.Equal("Save As", nodes[0].Name);
    }

    [Fact]
    public void Evaluate_FindsNode_ByAutomationIdAlias()
    {
        var fixture = CreateFixture();
        var response = AutomationSelectorEvaluator.Evaluate(
            fixture.Session,
            fixture.RootId,
            new SelectorDto { AutomationId = "save-secondary" });
        var nodes = Assert.IsType<NodeSummaryDto[]>(response.Nodes);

        Assert.True(response.Ok);
        Assert.Single(nodes);
        Assert.Equal("Save As", nodes[0].Name);
    }

    [Fact]
    public void Evaluate_FindsNode_ByRole()
    {
        var fixture = CreateFixture();
        var response = AutomationSelectorEvaluator.Evaluate(
            fixture.Session,
            fixture.RootId,
            new SelectorDto { Role = "edit" });
        var nodes = Assert.IsType<NodeSummaryDto[]>(response.Nodes);

        Assert.True(response.Ok);
        Assert.Single(nodes);
        Assert.Equal("Search", nodes[0].Name);
    }

    [Fact]
    public void Evaluate_FindsNode_ByClassName()
    {
        var fixture = CreateFixture();
        var response = AutomationSelectorEvaluator.Evaluate(
            fixture.Session,
            fixture.RootId,
            new SelectorDto { ClassName = "ToolbarButton" },
            maxResults: 2);
        var nodes = Assert.IsType<NodeSummaryDto[]>(response.Nodes);

        Assert.True(response.Ok);
        Assert.Equal(2, nodes.Length);
    }

    [Fact]
    public void Evaluate_FindsNode_ByFocusedState()
    {
        var fixture = CreateFixture();
        var response = AutomationSelectorEvaluator.Evaluate(
            fixture.Session,
            fixture.RootId,
            new SelectorDto { Focused = true });
        var nodes = Assert.IsType<NodeSummaryDto[]>(response.Nodes);

        Assert.True(response.Ok);
        Assert.Single(nodes);
        Assert.Equal("Search", nodes[0].Name);
    }

    [Fact]
    public void Evaluate_FindsNode_ByEnabledState()
    {
        var fixture = CreateFixture();
        var response = AutomationSelectorEvaluator.Evaluate(
            fixture.Session,
            fixture.RootId,
            new SelectorDto { Enabled = false });
        var nodes = Assert.IsType<NodeSummaryDto[]>(response.Nodes);

        Assert.True(response.Ok);
        Assert.Single(nodes);
        Assert.Equal("Cancel", nodes[0].Name);
    }

    [Fact]
    public void Evaluate_FindsNode_BySelectedState()
    {
        var fixture = CreateFixture();
        var response = AutomationSelectorEvaluator.Evaluate(
            fixture.Session,
            fixture.RootId,
            new SelectorDto { Selected = true });
        var nodes = Assert.IsType<NodeSummaryDto[]>(response.Nodes);

        Assert.True(response.Ok);
        Assert.Single(nodes);
        Assert.Equal("Save As", nodes[0].Name);
    }

    [Fact]
    public void Evaluate_FindsNode_ByVisibleState()
    {
        var fixture = CreateFixture();
        var response = AutomationSelectorEvaluator.Evaluate(
            fixture.Session,
            fixture.RootId,
            new SelectorDto { Visible = false });
        var nodes = Assert.IsType<NodeSummaryDto[]>(response.Nodes);

        Assert.True(response.Ok);
        Assert.Single(nodes);
        Assert.Equal("Cancel", nodes[0].Name);
    }

    [Fact]
    public void Evaluate_FindsNode_BySupportedAction()
    {
        var fixture = CreateFixture();
        var response = AutomationSelectorEvaluator.Evaluate(
            fixture.Session,
            fixture.RootId,
            new SelectorDto { HasAction = "select" });
        var nodes = Assert.IsType<NodeSummaryDto[]>(response.Nodes);

        Assert.True(response.Ok);
        Assert.Single(nodes);
        Assert.Equal("Save As", nodes[0].Name);
    }

    [Fact]
    public void Evaluate_RestrictsMatches_WithWithinHandle()
    {
        var fixture = CreateFixture();
        var withinHandle = fixture.Session.GetOrAssignHandle(fixture.Toolbar);
        var response = AutomationSelectorEvaluator.Evaluate(
            fixture.Session,
            fixture.RootId,
            new SelectorDto
            {
                Name = "save",
                NameSubstring = true,
                Within = withinHandle,
            },
            maxResults: 2);
        var nodes = Assert.IsType<NodeSummaryDto[]>(response.Nodes);

        Assert.True(response.Ok);
        Assert.Single(nodes);
        Assert.Equal("Save As", nodes[0].Name);
    }

    [Fact]
    public void Evaluate_RestrictsMatches_WithContainerIdAlias()
    {
        var fixture = CreateFixture();
        var withinHandle = fixture.Session.GetOrAssignHandle(fixture.Toolbar);
        var response = AutomationSelectorEvaluator.Evaluate(
            fixture.Session,
            fixture.RootId,
            new SelectorDto
            {
                Name = "save",
                NameSubstring = true,
                ContainerId = withinHandle,
            },
            maxResults: 2);
        var nodes = Assert.IsType<NodeSummaryDto[]>(response.Nodes);

        Assert.True(response.Ok);
        Assert.Single(nodes);
        Assert.Equal("Save As", nodes[0].Name);
    }

    [Fact]
    public void Evaluate_FindsNode_ByPath()
    {
        var fixture = CreateFixture();
        var response = AutomationSelectorEvaluator.Evaluate(
            fixture.Session,
            fixture.RootId,
            new SelectorDto
            {
                Name = "Save As",
                Path = new[] { "Main Window", "toolbar" },
            });
        var nodes = Assert.IsType<NodeSummaryDto[]>(response.Nodes);

        Assert.True(response.Ok);
        Assert.Single(nodes);
        Assert.Equal("Save As", nodes[0].Name);
    }

    [Fact]
    public void Evaluate_SelectsDeterministicNthMatch()
    {
        var fixture = CreateFixture();
        var response = AutomationSelectorEvaluator.Evaluate(
            fixture.Session,
            fixture.RootId,
            new SelectorDto { Name = "save", NameSubstring = true, Nth = 1 });
        var nodes = Assert.IsType<NodeSummaryDto[]>(response.Nodes);

        Assert.True(response.Ok);
        Assert.Single(nodes);
        Assert.Equal("Save As", nodes[0].Name);
    }

    [Fact]
    public void Evaluate_ReturnsSelectorAmbiguous_WhenMultipleNodesMatchWithoutNth()
    {
        var fixture = CreateFixture();
        var response = AutomationSelectorEvaluator.Evaluate(
            fixture.Session,
            fixture.RootId,
            new SelectorDto { Name = "save", NameSubstring = true });

        Assert.False(response.Ok);
        Assert.Equal(BridgeErrorCode.SelectorAmbiguous, response.Error!.Code);
    }

    [Fact]
    public void Evaluate_ReturnsRootNotFound_WhenRootHandleIsUnknown()
    {
        var fixture = CreateFixture();
        var response = AutomationSelectorEvaluator.Evaluate(
            fixture.Session,
            "w999",
            new SelectorDto { Name = "Save" });

        Assert.False(response.Ok);
        Assert.Equal(BridgeErrorCode.RootNotFound, response.Error!.Code);
    }

    [Fact]
    public void Evaluate_ProjectsRequestedFields()
    {
        var fixture = CreateFixture();
        var response = AutomationSelectorEvaluator.Evaluate(
            fixture.Session,
            fixture.RootId,
            new SelectorDto
            {
                AutomationId = "save-secondary",
                Fields = ["name", "selected"],
            });
        var node = Assert.Single(Assert.IsType<NodeSummaryDto[]>(response.Nodes));

        Assert.True(response.Ok);
        Assert.Equal("Save As", node.Name);
        Assert.True(node.Selected);
        Assert.Null(node.AutomationId);
        Assert.Null(node.ClassName);
        Assert.Null(node.Enabled);
        Assert.Null(node.Actions);
        Assert.NotNull(node.RootId);
        Assert.NotNull(node.Role);
    }

    private static SelectorFixture CreateFixture()
    {
        var root = new StubAutomationPeer
        {
            ControlType = AutomationControlType.Window,
            Name = "Main Window",
            ClassName = "Window",
        };
        var dialog = new StubAutomationPeer
        {
            ControlType = AutomationControlType.Group,
            Name = "Dialog",
            ClassName = "Panel",
        };
        var toolbar = new StubAutomationPeer
        {
            ControlType = AutomationControlType.Group,
            Name = "Toolbar",
            ClassName = "Panel",
        };
        var save = new StubAutomationPeer
        {
            ControlType = AutomationControlType.Button,
            AutomationId = "save-primary",
            Name = "Save",
            ClassName = "ToolbarButton",
            Enabled = true,
        };
        var saveAs = new StubAutomationPeer
        {
            ControlType = AutomationControlType.Button,
            AutomationId = "save-secondary",
            Name = "Save As",
            ClassName = "ToolbarButton",
            Enabled = true,
            Offscreen = false,
        };
        saveAs.RegisterProvider<ISelectionItemProvider>(new StubSelectionItemProvider(isSelected: true));
        var cancel = new StubAutomationPeer
        {
            ControlType = AutomationControlType.Button,
            AutomationId = "cancel",
            Name = "Cancel",
            ClassName = "DialogButton",
            Enabled = false,
            Offscreen = true,
        };
        var search = new StubAutomationPeer
        {
            ControlType = AutomationControlType.Edit,
            AutomationId = "search",
            Name = "Search",
            ClassName = "SearchBox",
            Enabled = true,
            KeyboardFocus = true,
        };

        root.AddChild(dialog);
        root.AddChild(toolbar);
        dialog.AddChild(save);
        dialog.AddChild(cancel);
        toolbar.AddChild(saveAs);
        toolbar.AddChild(search);

        var session = new AutomationBridgeSession(
            new AutomationRootRegistry(() => new AutomationPeer[] { root }));
        var rootId = session.GetRoots().Single().Id;

        return new SelectorFixture(session, rootId, toolbar);
    }

    private sealed record SelectorFixture(
        AutomationBridgeSession Session,
        string RootId,
        StubAutomationPeer Toolbar);

    private sealed class StubSelectionItemProvider : ISelectionItemProvider
    {
        public StubSelectionItemProvider(bool isSelected)
        {
            IsSelected = isSelected;
        }

        public bool IsSelected { get; }
        public ISelectionProvider? SelectionContainer => null;
        public void AddToSelection() { }
        public void RemoveFromSelection() { }
        public void Select() { }
    }
}
