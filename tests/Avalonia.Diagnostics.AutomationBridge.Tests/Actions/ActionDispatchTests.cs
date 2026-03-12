using Avalonia.Automation.Provider;
using Avalonia.AutomationBridge.Protocol.Messages;
using Avalonia.Diagnostics.AutomationBridge.Actions;
using Avalonia.Diagnostics.AutomationBridge.Session;
using Avalonia.Diagnostics.AutomationBridge.Tests.Session;
using Xunit;

namespace Avalonia.Diagnostics.AutomationBridge.Tests.Actions;

public sealed class ActionDispatchTests
{
    [Fact]
    public void Dispatch_InvokesInvokeProvider()
    {
        var peer = new StubAutomationPeer();
        var provider = new StubInvokeProvider();
        peer.RegisterProvider<IInvokeProvider>(provider);
        var fixture = CreateFixture(peer);

        var response = AutomationActionDispatcher.Dispatch(
            fixture.Session,
            new BridgeRequest { Action = BridgeAction.Invoke, NodeId = fixture.NodeId });

        Assert.True(response.Ok);
        Assert.Equal(1, provider.CallCount);
    }

    [Fact]
    public void Dispatch_SetsWritableValue()
    {
        var peer = new StubAutomationPeer();
        var provider = new StubValueProvider(isReadOnly: false);
        peer.RegisterProvider<IValueProvider>(provider);
        var fixture = CreateFixture(peer);

        var response = AutomationActionDispatcher.Dispatch(
            fixture.Session,
            new BridgeRequest
            {
                Action = BridgeAction.SetValue,
                NodeId = fixture.NodeId,
                Value = "hello",
            });

        Assert.True(response.Ok);
        Assert.Equal("hello", provider.Value);
    }

    [Fact]
    public void Dispatch_TogglesToggleProvider()
    {
        var peer = new StubAutomationPeer();
        var provider = new StubToggleProvider();
        peer.RegisterProvider<IToggleProvider>(provider);
        var fixture = CreateFixture(peer);

        var response = AutomationActionDispatcher.Dispatch(
            fixture.Session,
            new BridgeRequest { Action = BridgeAction.Toggle, NodeId = fixture.NodeId });

        Assert.True(response.Ok);
        Assert.Equal(1, provider.CallCount);
    }

    [Fact]
    public void Dispatch_SelectsSelectionItem()
    {
        var peer = new StubAutomationPeer();
        var provider = new StubSelectionItemProvider();
        peer.RegisterProvider<ISelectionItemProvider>(provider);
        var fixture = CreateFixture(peer);

        var response = AutomationActionDispatcher.Dispatch(
            fixture.Session,
            new BridgeRequest { Action = BridgeAction.Select, NodeId = fixture.NodeId });

        Assert.True(response.Ok);
        Assert.Equal(1, provider.SelectCallCount);
    }

    [Fact]
    public void Dispatch_ExpandsExpandCollapseProvider()
    {
        var peer = new StubAutomationPeer();
        var provider = new StubExpandCollapseProvider();
        peer.RegisterProvider<IExpandCollapseProvider>(provider);
        var fixture = CreateFixture(peer);

        var response = AutomationActionDispatcher.Dispatch(
            fixture.Session,
            new BridgeRequest { Action = BridgeAction.Expand, NodeId = fixture.NodeId });

        Assert.True(response.Ok);
        Assert.Equal(1, provider.ExpandCallCount);
    }

    [Fact]
    public void Dispatch_CollapsesExpandCollapseProvider()
    {
        var peer = new StubAutomationPeer();
        var provider = new StubExpandCollapseProvider();
        peer.RegisterProvider<IExpandCollapseProvider>(provider);
        var fixture = CreateFixture(peer);

        var response = AutomationActionDispatcher.Dispatch(
            fixture.Session,
            new BridgeRequest { Action = BridgeAction.Collapse, NodeId = fixture.NodeId });

        Assert.True(response.Ok);
        Assert.Equal(1, provider.CollapseCallCount);
    }

    [Fact]
    public void Dispatch_SetsKeyboardFocus()
    {
        var peer = new StubAutomationPeer { KeyboardFocusable = true };
        var fixture = CreateFixture(peer);

        var response = AutomationActionDispatcher.Dispatch(
            fixture.Session,
            new BridgeRequest { Action = BridgeAction.SetFocus, NodeId = fixture.NodeId });

        Assert.True(response.Ok);
        Assert.Equal(1, peer.SetFocusCallCount);
    }

    [Fact]
    public void Dispatch_ScrollsScrollProvider()
    {
        var peer = new StubAutomationPeer();
        var provider = new StubScrollProvider();
        peer.RegisterProvider<IScrollProvider>(provider);
        var fixture = CreateFixture(peer);

        var response = AutomationActionDispatcher.Dispatch(
            fixture.Session,
            new BridgeRequest
            {
                Action = BridgeAction.Scroll,
                NodeId = fixture.NodeId,
                HorizontalAmount = "largeIncrement",
                VerticalAmount = "smallDecrement",
            });

        Assert.True(response.Ok);
        Assert.Equal(ScrollAmount.LargeIncrement, provider.LastHorizontalAmount);
        Assert.Equal(ScrollAmount.SmallDecrement, provider.LastVerticalAmount);
    }

    [Fact]
    public void Dispatch_SetsScrollPercent()
    {
        var peer = new StubAutomationPeer();
        var provider = new StubScrollProvider();
        peer.RegisterProvider<IScrollProvider>(provider);
        var fixture = CreateFixture(peer);

        var response = AutomationActionDispatcher.Dispatch(
            fixture.Session,
            new BridgeRequest
            {
                Action = BridgeAction.SetScrollPercent,
                NodeId = fixture.NodeId,
                HorizontalPercent = 25,
                VerticalPercent = 75,
            });

        Assert.True(response.Ok);
        Assert.Equal(25, provider.LastHorizontalPercent);
        Assert.Equal(75, provider.LastVerticalPercent);
    }

    [Fact]
    public void Dispatch_ReturnsActionNotSupported_WhenProviderMissing()
    {
        var fixture = CreateFixture(new StubAutomationPeer());

        var response = AutomationActionDispatcher.Dispatch(
            fixture.Session,
            new BridgeRequest { Action = BridgeAction.Invoke, NodeId = fixture.NodeId });

        Assert.False(response.Ok);
        Assert.Equal(BridgeErrorCode.ActionNotSupported, response.Error!.Code);
    }

    [Fact]
    public void Dispatch_ReturnsElementNotEnabled_WhenPeerIsDisabled()
    {
        var peer = new StubAutomationPeer { Enabled = false };
        var provider = new StubInvokeProvider();
        peer.RegisterProvider<IInvokeProvider>(provider);
        var fixture = CreateFixture(peer);

        var response = AutomationActionDispatcher.Dispatch(
            fixture.Session,
            new BridgeRequest { Action = BridgeAction.Invoke, NodeId = fixture.NodeId });

        Assert.False(response.Ok);
        Assert.Equal(BridgeErrorCode.ElementNotEnabled, response.Error!.Code);
        Assert.Equal(0, provider.CallCount);
    }

    private static ActionFixture CreateFixture(StubAutomationPeer node)
    {
        var root = new StubAutomationPeer { ControlType = Avalonia.Automation.Peers.AutomationControlType.Window };
        root.AddChild(node);

        var session = new AutomationBridgeSession(new AutomationRootRegistry(() => new[] { root }));
        _ = session.GetRoots();
        var nodeId = session.GetOrAssignHandle(node);

        return new ActionFixture(session, nodeId);
    }

    private sealed record ActionFixture(AutomationBridgeSession Session, string NodeId);

    private sealed class StubInvokeProvider : IInvokeProvider
    {
        public int CallCount { get; private set; }

        public void Invoke() => CallCount++;
    }

    private sealed class StubValueProvider : IValueProvider
    {
        public StubValueProvider(bool isReadOnly)
        {
            IsReadOnly = isReadOnly;
        }

        public bool IsReadOnly { get; }
        public string? Value { get; private set; }

        public void SetValue(string? value) => Value = value;
    }

    private sealed class StubToggleProvider : IToggleProvider
    {
        public int CallCount { get; private set; }
        public ToggleState ToggleState => ToggleState.Off;

        public void Toggle() => CallCount++;
    }

    private sealed class StubSelectionItemProvider : ISelectionItemProvider
    {
        public bool IsSelected => false;
        public ISelectionProvider? SelectionContainer => null;
        public int SelectCallCount { get; private set; }

        public void AddToSelection() { }

        public void RemoveFromSelection() { }

        public void Select() => SelectCallCount++;
    }

    private sealed class StubExpandCollapseProvider : IExpandCollapseProvider
    {
        public Avalonia.Automation.ExpandCollapseState ExpandCollapseState => Avalonia.Automation.ExpandCollapseState.Collapsed;
        public bool ShowsMenu => false;
        public int ExpandCallCount { get; private set; }
        public int CollapseCallCount { get; private set; }

        public void Expand() => ExpandCallCount++;

        public void Collapse() => CollapseCallCount++;
    }

    private sealed class StubScrollProvider : IScrollProvider
    {
        public bool HorizontallyScrollable => true;
        public double HorizontalScrollPercent => 0;
        public double HorizontalViewSize => 100;
        public bool VerticallyScrollable => true;
        public double VerticalScrollPercent => 0;
        public double VerticalViewSize => 100;
        public ScrollAmount LastHorizontalAmount { get; private set; }
        public ScrollAmount LastVerticalAmount { get; private set; }
        public double LastHorizontalPercent { get; private set; }
        public double LastVerticalPercent { get; private set; }

        public void Scroll(ScrollAmount horizontalAmount, ScrollAmount verticalAmount)
        {
            LastHorizontalAmount = horizontalAmount;
            LastVerticalAmount = verticalAmount;
        }

        public void SetScrollPercent(double horizontalPercent, double verticalPercent)
        {
            LastHorizontalPercent = horizontalPercent;
            LastVerticalPercent = verticalPercent;
        }
    }
}
