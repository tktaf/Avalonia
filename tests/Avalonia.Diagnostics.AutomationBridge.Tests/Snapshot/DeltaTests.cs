using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Automation.Provider;
using Avalonia.AutomationBridge.Protocol.Messages;
using Avalonia.Diagnostics.AutomationBridge.Actions;
using Avalonia.Diagnostics.AutomationBridge.Session;
using Avalonia.Diagnostics.AutomationBridge.Snapshot;
using Avalonia.Diagnostics.AutomationBridge.Tests.Session;
using Avalonia.Platform;
using Xunit;

namespace Avalonia.Diagnostics.AutomationBridge.Tests.Snapshot;

public sealed class DeltaTests
{
    [Fact]
    public void ChildrenChanged_EmitsAddedHandles()
    {
        var root = new StubRootAutomationPeer { ControlType = AutomationControlType.Window };
        var existing = new StubAutomationPeer { Name = "Existing" };
        root.AddChild(existing);
        using var session = new AutomationBridgeSession(new AutomationRootRegistry(() => new AutomationPeer[] { root }));
        var rootId = session.GetRoots()[0].Id;
        var builder = session.GetOrCreateDeltaBuilder(rootId);

        var added = new StubAutomationPeer { Name = "Added" };
        root.AddChild(added);
        root.RaiseChildrenChanged();

        var response = builder.GetDelta(0);
        var delta = Assert.IsType<DeltaDto>(response.Delta);

        Assert.Equal(1, delta.Revision);
        Assert.Single(delta.Added);
        Assert.Empty(delta.Removed);
    }

    [Fact]
    public void ChildrenChanged_EmitsRemovedHandles()
    {
        var root = new StubRootAutomationPeer { ControlType = AutomationControlType.Window };
        var child = new StubAutomationPeer { Name = "Child" };
        root.AddChild(child);
        using var session = new AutomationBridgeSession(new AutomationRootRegistry(() => new AutomationPeer[] { root }));
        var rootId = session.GetRoots()[0].Id;
        var builder = session.GetOrCreateDeltaBuilder(rootId);
        var childHandle = session.GetOrAssignHandle(child);

        child.SetParent(null);
        root.RemoveChild(child);
        root.RaiseChildrenChanged();

        var response = builder.GetDelta(0);
        var delta = Assert.IsType<DeltaDto>(response.Delta);

        Assert.Single(delta.Removed);
        Assert.Equal(childHandle, delta.Removed[0]);
    }

    [Fact]
    public void PropertyChanged_EmitsCompactFieldUpdatesOnly()
    {
        var root = new StubRootAutomationPeer { ControlType = AutomationControlType.Window };
        var child = new StubAutomationPeer { Name = "Old" };
        root.AddChild(child);
        using var session = new AutomationBridgeSession(new AutomationRootRegistry(() => new AutomationPeer[] { root }));
        var rootId = session.GetRoots()[0].Id;
        var builder = session.GetOrCreateDeltaBuilder(rootId);

        child.Name = "New";
        child.RaisePropertyChangedEvent(AutomationElementIdentifiers.NameProperty, "Old", "New");

        var response = builder.GetDelta(0);
        var delta = Assert.IsType<DeltaDto>(response.Delta);
        var patch = Assert.Single(delta.Updated);

        Assert.Equal("New", patch.Name);
        Assert.Null(patch.Value);
        Assert.Null(patch.Enabled);
        Assert.Null(patch.Focused);
        Assert.Null(patch.Offscreen);
    }

    [Fact]
    public void FocusChanged_UpdatesFocusHandleWithoutUnrelatedData()
    {
        var root = new StubRootAutomationPeer { ControlType = AutomationControlType.Window };
        var child = new StubAutomationPeer { Name = "Focused" };
        root.AddChild(child);
        using var session = new AutomationBridgeSession(new AutomationRootRegistry(() => new AutomationPeer[] { root }));
        var rootId = session.GetRoots()[0].Id;
        var builder = session.GetOrCreateDeltaBuilder(rootId);
        var childHandle = session.GetOrAssignHandle(child);

        root.SetFocusPeer(child);
        root.RaiseFocusChanged();

        var response = builder.GetDelta(0);
        var delta = Assert.IsType<DeltaDto>(response.Delta);

        Assert.Equal(childHandle, delta.Focus);
        Assert.Empty(delta.Updated);
        Assert.Empty(delta.Added);
        Assert.Empty(delta.Removed);
    }

    [Fact]
    public void MutatingActions_ReportAcceptedCompletion_WhenNoImmediateDeltaWasPublished()
    {
        var root = new StubRootAutomationPeer { ControlType = AutomationControlType.Window };
        var child = new StubAutomationPeer();
        var provider = new StubInvokeProvider();
        child.RegisterProvider<IInvokeProvider>(provider);
        root.AddChild(child);
        using var session = new AutomationBridgeSession(new AutomationRootRegistry(() => new AutomationPeer[] { root }));
        _ = session.GetRoots();
        var nodeId = session.GetOrAssignHandle(child);

        var response = AutomationActionDispatcher.Dispatch(
            session,
            new BridgeRequest { Action = BridgeAction.Invoke, NodeId = nodeId });
        var delta = Assert.IsType<DeltaDto>(response.Delta);

        Assert.Equal(1, provider.CallCount);
        Assert.Equal(BridgeActionCompletionState.Accepted, response.Completion?.State);
        Assert.Equal(0, delta.Revision);
        Assert.Empty(delta.Updated);
        Assert.Empty(delta.Added);
        Assert.Empty(delta.Removed);
    }

    [Fact]
    public void StaleRevisions_ReturnStaleRevisionError()
    {
        var root = new StubRootAutomationPeer { ControlType = AutomationControlType.Window };
        var child = new StubAutomationPeer { Name = "Old" };
        root.AddChild(child);
        using var session = new AutomationBridgeSession(new AutomationRootRegistry(() => new AutomationPeer[] { root }));
        var rootId = session.GetRoots()[0].Id;
        var builder = session.GetOrCreateDeltaBuilder(rootId);

        child.Name = "One";
        child.RaisePropertyChangedEvent(AutomationElementIdentifiers.NameProperty, "Old", "One");
        child.Name = "Two";
        child.RaisePropertyChangedEvent(AutomationElementIdentifiers.NameProperty, "One", "Two");

        var response = builder.GetDelta(0);

        Assert.False(response.Ok);
        Assert.Equal(BridgeErrorCode.StaleRevision, response.Error!.Code);
    }

    private sealed class StubInvokeProvider : IInvokeProvider
    {
        public int CallCount { get; private set; }

        public void Invoke() => CallCount++;
    }
}
