using Avalonia.Automation.Provider;
using Avalonia.Automation;
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

    [Theory]
    [InlineData(BridgeAction.Expand)]
    [InlineData(BridgeAction.Collapse)]
    public void Dispatch_ReturnsActionNotSupported_ForLeafNodeExpandCollapse(string action)
    {
        var peer = new StubAutomationPeer();
        var provider = new StubExpandCollapseProvider(Avalonia.Automation.ExpandCollapseState.LeafNode);
        peer.RegisterProvider<IExpandCollapseProvider>(provider);
        var fixture = CreateFixture(peer);

        var response = AutomationActionDispatcher.Dispatch(
            fixture.Session,
            new BridgeRequest { Action = action, NodeId = fixture.NodeId });

        Assert.False(response.Ok);
        Assert.Equal(BridgeErrorCode.ActionNotSupported, response.Error!.Code);
        Assert.Equal(0, provider.ExpandCallCount);
        Assert.Equal(0, provider.CollapseCallCount);
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
        Assert.Equal(BridgeActionCompletionState.Accepted, response.Completion?.State);
    }

    [Fact]
    public void Dispatch_ReturnsAcceptedCompletion_WhenNoObservableDeltaWasPublished()
    {
        var peer = new StubAutomationPeer();
        var provider = new StubInvokeProvider();
        peer.RegisterProvider<IInvokeProvider>(provider);
        var fixture = CreateFixture(peer);

        var response = AutomationActionDispatcher.Dispatch(
            fixture.Session,
            new BridgeRequest { Action = BridgeAction.Invoke, NodeId = fixture.NodeId });

        Assert.True(response.Ok);
        Assert.Equal(BridgeActionCompletionState.Accepted, response.Completion?.State);
        Assert.NotNull(response.Delta);
        Assert.Empty(response.Delta!.Updated);
        Assert.Empty(response.Delta.Added);
        Assert.Empty(response.Delta.Removed);
    }

    [Fact]
    public void Dispatch_ReturnsCompletedCompletion_WhenActionPublishesDelta()
    {
        var peer = new StubAutomationPeer();
        var provider = new RaisingValueProvider(peer);
        peer.RegisterProvider<IValueProvider>(provider);
        var fixture = CreateFixture(peer);

        var response = AutomationActionDispatcher.Dispatch(
            fixture.Session,
            new BridgeRequest
            {
                Action = BridgeAction.SetValue,
                NodeId = fixture.NodeId,
                Value = "changed",
            });

        Assert.True(response.Ok);
        Assert.Equal(BridgeActionCompletionState.Completed, response.Completion?.State);
        Assert.NotNull(response.Delta);
        Assert.Single(response.Delta!.Updated);
        Assert.Equal("changed", response.Delta.Updated[0].Value);
    }

    [Fact]
    public void Dispatch_AggregatesMultipleDeltaEvents_FromOneAction()
    {
        var peer = new StubAutomationPeer { Name = "Original" };
        var provider = new MultiEventRaisingValueProvider(peer);
        peer.RegisterProvider<IValueProvider>(provider);
        var fixture = CreateFixture(peer);

        var response = AutomationActionDispatcher.Dispatch(
            fixture.Session,
            new BridgeRequest
            {
                Action = BridgeAction.SetValue,
                NodeId = fixture.NodeId,
                Value = "changed",
            });

        Assert.True(response.Ok);
        Assert.Equal(BridgeActionCompletionState.Completed, response.Completion?.State);
        Assert.NotNull(response.Delta);
        var patch = Assert.Single(response.Delta!.Updated);
        Assert.Equal("Renamed", patch.Name);
        Assert.Equal("changed", patch.Value);
    }

    [Fact]
    public void Dispatch_PreservesClearToNullFields_WhenAggregatingMultipleDeltaEvents()
    {
        var peer = new StubAutomationPeer
        {
            Name = "Original",
            ItemStatus = "busy; modal=true",
        };
        var provider = new MultiEventClearingValueProvider(peer);
        peer.RegisterProvider<IValueProvider>(provider);
        var fixture = CreateFixture(peer);

        var response = AutomationActionDispatcher.Dispatch(
            fixture.Session,
            new BridgeRequest
            {
                Action = BridgeAction.SetValue,
                NodeId = fixture.NodeId,
                Value = "changed",
            });

        Assert.True(response.Ok);
        Assert.Equal(BridgeActionCompletionState.Completed, response.Completion?.State);
        Assert.NotNull(response.Delta);
        var patch = Assert.Single(response.Delta!.Updated);
        var cleared = Assert.IsType<string[]>(patch.Cleared);
        Assert.Equal("changed", patch.Value);
        Assert.Null(patch.State);
        Assert.Equal([NodePatchField.State], cleared);
    }

    [Fact]
    public void Dispatch_PreservesSuccessfulCompletion_WhenValueGetterThrowsDuringDeltaCapture()
    {
        var peer = new StubAutomationPeer();
        var provider = new ThrowingGetterRaisingValueProvider(peer);
        peer.RegisterProvider<IValueProvider>(provider);
        var fixture = CreateFixture(peer);

        var response = AutomationActionDispatcher.Dispatch(
            fixture.Session,
            new BridgeRequest
            {
                Action = BridgeAction.SetValue,
                NodeId = fixture.NodeId,
                Value = "changed",
            });

        Assert.True(response.Ok);
        Assert.Equal(BridgeActionCompletionState.Completed, response.Completion?.State);
        Assert.NotNull(response.Delta);
        Assert.Single(response.Delta!.Updated);
        Assert.Null(response.Delta.Updated[0].Value);
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

    [Fact]
    public void Dispatch_ReturnsStructuredError_WhenActionProviderThrows()
    {
        var peer = new StubAutomationPeer();
        var provider = new ThrowingSelectionItemProvider();
        peer.RegisterProvider<ISelectionItemProvider>(provider);
        var fixture = CreateFixture(peer);

        var response = AutomationActionDispatcher.Dispatch(
            fixture.Session,
            new BridgeRequest { Action = BridgeAction.Select, NodeId = fixture.NodeId, RequestId = "select-throw" });

        Assert.False(response.Ok);
        Assert.Equal("select-throw", response.RequestId);
        Assert.Equal(BridgeErrorCode.ActionFailed, response.Error!.Code);
        Assert.Contains("select", response.Error.Message!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Dispatch_ReturnsStructuredError_WhenEnabledCheckThrows()
    {
        var peer = new StubAutomationPeer
        {
            EnabledException = new InvalidOperationException("enabled exploded"),
        };
        peer.RegisterProvider<IInvokeProvider>(new StubInvokeProvider());
        var fixture = CreateFixture(peer);

        var response = AutomationActionDispatcher.Dispatch(
            fixture.Session,
            new BridgeRequest { Action = BridgeAction.Invoke, NodeId = fixture.NodeId, RequestId = "invoke-enabled-throw" });

        Assert.False(response.Ok);
        Assert.Equal("invoke-enabled-throw", response.RequestId);
        Assert.Equal(BridgeErrorCode.ActionFailed, response.Error!.Code);
        Assert.Contains("invoke", response.Error.Message!, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData(BridgeAction.Expand)]
    [InlineData(BridgeAction.Collapse)]
    public void Dispatch_ReturnsStructuredError_WhenExpandCollapseStateGetterThrows(string action)
    {
        var peer = new StubAutomationPeer();
        peer.RegisterProvider<IExpandCollapseProvider>(new ThrowingExpandCollapseStateProvider());
        var fixture = CreateFixture(peer);

        var response = AutomationActionDispatcher.Dispatch(
            fixture.Session,
            new BridgeRequest { Action = action, NodeId = fixture.NodeId, RequestId = "expand-state-throw" });

        Assert.False(response.Ok);
        Assert.Equal("expand-state-throw", response.RequestId);
        Assert.Equal(BridgeErrorCode.ActionFailed, response.Error!.Code);
        Assert.Contains(action, response.Error.Message!, StringComparison.OrdinalIgnoreCase);
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

    private sealed class RaisingValueProvider : IValueProvider
    {
        private readonly StubAutomationPeer _peer;

        public RaisingValueProvider(StubAutomationPeer peer)
        {
            _peer = peer;
        }

        public bool IsReadOnly => false;
        public string? Value { get; private set; }

        public void SetValue(string? value)
        {
            Value = value;
            _peer.RaisePropertyChangedEvent(ValuePatternIdentifiers.ValueProperty, null, value);
        }
    }

    private sealed class ThrowingGetterRaisingValueProvider : IValueProvider
    {
        private readonly StubAutomationPeer _peer;

        public ThrowingGetterRaisingValueProvider(StubAutomationPeer peer)
        {
            _peer = peer;
        }

        public bool IsReadOnly => false;

        public string? Value => throw new InvalidOperationException("value exploded");

        public void SetValue(string? value)
            => _peer.RaisePropertyChangedEvent(ValuePatternIdentifiers.ValueProperty, null, value);
    }

    private sealed class MultiEventRaisingValueProvider : IValueProvider
    {
        private readonly StubAutomationPeer _peer;

        public MultiEventRaisingValueProvider(StubAutomationPeer peer)
        {
            _peer = peer;
        }

        public bool IsReadOnly => false;
        public string? Value { get; private set; }

        public void SetValue(string? value)
        {
            _peer.Name = "Renamed";
            _peer.RaisePropertyChangedEvent(AutomationElementIdentifiers.NameProperty, "Original", "Renamed");
            Value = value;
            _peer.RaisePropertyChangedEvent(ValuePatternIdentifiers.ValueProperty, null, value);
        }
    }

    private sealed class MultiEventClearingValueProvider : IValueProvider
    {
        private readonly StubAutomationPeer _peer;

        public MultiEventClearingValueProvider(StubAutomationPeer peer)
        {
            _peer = peer;
        }

        public bool IsReadOnly => false;
        public string? Value { get; private set; }

        public void SetValue(string? value)
        {
            _peer.ItemStatus = null;
            _peer.RaisePropertyChangedEvent(AutomationElementIdentifiers.ItemStatusProperty, "busy; modal=true", null);
            Value = value;
            _peer.RaisePropertyChangedEvent(ValuePatternIdentifiers.ValueProperty, null, value);
        }
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

    private sealed class ThrowingSelectionItemProvider : ISelectionItemProvider
    {
        public bool IsSelected => false;
        public ISelectionProvider? SelectionContainer => null;

        public void AddToSelection() { }

        public void RemoveFromSelection() { }

        public void Select() => throw new InvalidOperationException("selection exploded");
    }

    private sealed class StubExpandCollapseProvider : IExpandCollapseProvider
    {
        public StubExpandCollapseProvider(
            Avalonia.Automation.ExpandCollapseState expandCollapseState = Avalonia.Automation.ExpandCollapseState.Collapsed)
        {
            ExpandCollapseState = expandCollapseState;
        }

        public Avalonia.Automation.ExpandCollapseState ExpandCollapseState { get; }
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

    private sealed class ThrowingExpandCollapseStateProvider : IExpandCollapseProvider
    {
        public Avalonia.Automation.ExpandCollapseState ExpandCollapseState
            => throw new InvalidOperationException("expand state exploded");

        public bool ShowsMenu => false;
        public void Expand() { }
        public void Collapse() { }
    }
}
