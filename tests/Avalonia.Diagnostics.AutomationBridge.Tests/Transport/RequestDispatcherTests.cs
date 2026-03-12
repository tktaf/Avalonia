using Avalonia.Automation;
using Avalonia.AutomationBridge.Protocol.Messages;
using Avalonia.Diagnostics.AutomationBridge.Session;
using Avalonia.Diagnostics.AutomationBridge.Tests.Session;
using Avalonia.Diagnostics.AutomationBridge.Transport;
using Xunit;

namespace Avalonia.Diagnostics.AutomationBridge.Tests.Transport;

public sealed class RequestDispatcherTests
{
    [Fact]
    public void Dispatch_Roots_ReturnsCurrentRootSummaries()
    {
        using var fixture = new DispatcherFixture();

        var response = AutomationBridgeRequestDispatcher.Dispatch(
            fixture.Session,
            new BridgeRequest { Action = BridgeAction.Roots, RequestId = "req-roots" });

        Assert.True(response.Ok);
        var root = Assert.Single(Assert.IsType<NodeSummaryDto[]>(response.Nodes));
        Assert.Equal("req-roots", response.RequestId);
        Assert.Equal(fixture.RootId, root.Id);
    }

    [Fact]
    public void Dispatch_Describe_ReturnsNodeSummary()
    {
        using var fixture = new DispatcherFixture();

        var response = AutomationBridgeRequestDispatcher.Dispatch(
            fixture.Session,
            new BridgeRequest
            {
                Action = BridgeAction.Describe,
                NodeId = fixture.ChildId,
            });

        Assert.True(response.Ok);
        var node = Assert.Single(Assert.IsType<NodeSummaryDto[]>(response.Nodes));
        Assert.Equal(fixture.ChildId, node.Id);
        Assert.Equal("Child", node.Name);
    }

    [Fact]
    public void Dispatch_Watch_ReturnsDeltaForRootRevision()
    {
        using var fixture = new DispatcherFixture();

        fixture.Child.Name = "Updated";
        fixture.Child.RaisePropertyChangedEvent(AutomationElementIdentifiers.NameProperty, "Child", "Updated");

        var response = AutomationBridgeRequestDispatcher.Dispatch(
            fixture.Session,
            new BridgeRequest
            {
                Action = BridgeAction.Watch,
                RootId = fixture.RootId,
                SinceRevision = 0,
            });

        Assert.True(response.Ok);
        var patch = Assert.Single(response.Delta!.Updated);
        Assert.Equal(fixture.ChildId, patch.Id);
        Assert.Equal("Updated", patch.Name);
    }

    [Fact]
    public void Dispatch_Query_UsesStructuredSelectorEvaluation()
    {
        using var fixture = new DispatcherFixture();

        var response = AutomationBridgeRequestDispatcher.Dispatch(
            fixture.Session,
            new BridgeRequest
            {
                Action = BridgeAction.Query,
                RootId = fixture.RootId,
                Selector = new SelectorDto { Name = "Child" },
            });

        Assert.True(response.Ok);
        var node = Assert.Single(Assert.IsType<NodeSummaryDto[]>(response.Nodes));
        Assert.Equal(fixture.ChildId, node.Id);
    }

    private sealed class DispatcherFixture : IDisposable
    {
        public DispatcherFixture()
        {
            Root = new StubRootAutomationPeer { ControlType = Avalonia.Automation.Peers.AutomationControlType.Window, Name = "Root" };
            Child = new StubAutomationPeer { Name = "Child" };
            Root.AddChild(Child);
            Session = new AutomationBridgeSession(new AutomationRootRegistry(() => new[] { Root }));
            RootId = Session.GetRoots()[0].Id;
            ChildId = Session.GetOrAssignHandle(Child);
            _ = Session.GetOrCreateDeltaBuilder(RootId);
        }

        public StubRootAutomationPeer Root { get; }
        public StubAutomationPeer Child { get; }
        public AutomationBridgeSession Session { get; }
        public string RootId { get; }
        public string ChildId { get; }

        public void Dispose() => Session.Dispose();
    }
}
