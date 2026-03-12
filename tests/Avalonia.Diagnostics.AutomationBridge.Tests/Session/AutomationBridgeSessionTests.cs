using System.Collections.Generic;
using Avalonia.Automation.Peers;
using Avalonia.Diagnostics.AutomationBridge.Session;
using Xunit;

namespace Avalonia.Diagnostics.AutomationBridge.Tests.Session;

/// <summary>
/// Tests for <see cref="AutomationBridgeSession"/>.
/// Proves root enumeration without tree dumps, stable handle assignment, clean invalidation
/// of disappearing peers, and liveness — that <see cref="AutomationBridgeSession.GetRoots"/> reflects the current state of
/// its <see cref="ITopLevelPeerSource"/> on every call.
/// </summary>
public sealed class AutomationBridgeSessionTests
{
    // -------------------------------------------------------------------------
    // Root enumeration
    // -------------------------------------------------------------------------

    [Fact]
    public void GetRoots_ReturnsEmptyList_WhenNoRootsRegistered()
    {
        var rootRegistry = new AutomationRootRegistry(System.Array.Empty<AutomationPeer>);
        using var session = new AutomationBridgeSession(rootRegistry);

        var roots = session.GetRoots();

        Assert.Empty(roots);
    }

    [Fact]
    public void GetRoots_ReturnsSummary_ForEachRoot()
    {
        var root1 = new StubAutomationPeer { Name = "Window1" };
        var root2 = new StubAutomationPeer { Name = "Window2" };
        var rootRegistry = new AutomationRootRegistry(() => new AutomationPeer[] { root1, root2 });

        using var session = new AutomationBridgeSession(rootRegistry);
        var roots = session.GetRoots();

        Assert.Equal(2, roots.Count);
        Assert.Equal("Window1", roots[0].Name);
        Assert.Equal("Window2", roots[1].Name);
    }

    [Fact]
    public void GetRoots_DoesNotTraverseChildren()
    {
        // A root with child nodes — GetRoots must not walk children.
        var root = new StubAutomationPeer { Name = "Root" };
        var child = new StubAutomationPeer { Name = "Child" };
        root.AddChild(child);

        var rootRegistry = new AutomationRootRegistry(() => new AutomationPeer[] { root });

        using var session = new AutomationBridgeSession(rootRegistry);
        var roots = session.GetRoots();

        // Only one summary returned — the root itself, not the child
        Assert.Single(roots);
        Assert.Equal("Root", roots[0].Name);
    }

    [Fact]
    public void GetRoots_AssignsRootHandles_WithWPrefix()
    {
        var peer = new StubAutomationPeer();
        var rootRegistry = new AutomationRootRegistry(() => new AutomationPeer[] { peer });

        using var session = new AutomationBridgeSession(rootRegistry);
        var roots = session.GetRoots();

        Assert.Matches(@"^w\d+$", roots[0].Id);
    }

    [Fact]
    public void GetRoots_RootId_EqualsId_ForRootNodes()
    {
        var peer = new StubAutomationPeer();
        var rootRegistry = new AutomationRootRegistry(() => new AutomationPeer[] { peer });

        using var session = new AutomationBridgeSession(rootRegistry);
        var roots = session.GetRoots();

        // For a root node, Id and RootId must be the same handle
        Assert.Equal(roots[0].Id, roots[0].RootId);
    }

    // -------------------------------------------------------------------------
    // Liveness — session reads the peer source live on every call
    // -------------------------------------------------------------------------

    [Fact]
    public void GetRoots_ReflectsLiveSourceChanges_AfterSessionCreation()
    {
        // Arrange: session is created with an empty source, then the source
        // changes — GetRoots must reflect the current state, not a snapshot.
        AutomationPeer[] currentPeers = System.Array.Empty<AutomationPeer>();
        var source = new AutomationRootRegistry(() => currentPeers);
        using var session = new AutomationBridgeSession(source);

        Assert.Empty(session.GetRoots());

        // Act: source gains a new root after session was created
        var lateRoot = new StubAutomationPeer { Name = "LateWindow" };
        currentPeers = new AutomationPeer[] { lateRoot };

        // Assert: next GetRoots call sees the new root immediately
        var roots = session.GetRoots();
        Assert.Single(roots);
        Assert.Equal("LateWindow", roots[0].Name);
    }

    [Fact]
    public void GetRoots_ReflectsSourceBecomingEmpty_AfterWindowClose()
    {
        // Arrange: session starts with one root
        var peer = new StubAutomationPeer { Name = "Window" };
        AutomationPeer[] currentPeers = new AutomationPeer[] { peer };
        var source = new AutomationRootRegistry(() => currentPeers);
        using var session = new AutomationBridgeSession(source);

        Assert.Single(session.GetRoots());

        // Act: the window is closed — source becomes empty
        currentPeers = System.Array.Empty<AutomationPeer>();

        // Assert: session reflects the absence of roots
        Assert.Empty(session.GetRoots());
    }

    [Fact]
    public void GetRoots_AcceptsAnyITopLevelPeerSource_NotOnlyRegistry()
    {
        // Proves AutomationBridgeSession is coupled to ITopLevelPeerSource,
        // not specifically to AutomationRootRegistry.
        var peer = new StubAutomationPeer { Name = "Standalone" };
        var customSource = new LambdaTopLevelPeerSource(() => new AutomationPeer[] { peer });

        using var session = new AutomationBridgeSession(customSource);
        var roots = session.GetRoots();

        Assert.Single(roots);
        Assert.Equal("Standalone", roots[0].Name);
    }

    // -------------------------------------------------------------------------
    // Handle stability
    // -------------------------------------------------------------------------

    [Fact]
    public void GetRoots_AssignsSameHandle_OnSubsequentCalls()
    {
        var peer = new StubAutomationPeer();
        var rootRegistry = new AutomationRootRegistry(() => new AutomationPeer[] { peer });

        using var session = new AutomationBridgeSession(rootRegistry);
        var firstCall = session.GetRoots();
        var secondCall = session.GetRoots();

        Assert.Equal(firstCall[0].Id, secondCall[0].Id);
    }

    [Fact]
    public void TryGetPeer_ResolvesHandle_AssignedViaGetRoots()
    {
        var peer = new StubAutomationPeer();
        var rootRegistry = new AutomationRootRegistry(() => new AutomationPeer[] { peer });

        using var session = new AutomationBridgeSession(rootRegistry);
        var roots = session.GetRoots();
        var handle = roots[0].Id;

        var found = session.TryGetPeer(handle, out var resolved);

        Assert.True(found);
        Assert.Same(peer, resolved);
    }

    [Fact]
    public void TryGetPeer_ReturnsFalse_ForUnknownHandle()
    {
        var rootRegistry = new AutomationRootRegistry();
        using var session = new AutomationBridgeSession(rootRegistry);

        var found = session.TryGetPeer("w99", out _);

        Assert.False(found);
    }

    // -------------------------------------------------------------------------
    // Peer invalidation
    // -------------------------------------------------------------------------

    [Fact]
    public void InvalidatePeer_CleansHandle_SoTryGetPeerFails()
    {
        var peer = new StubAutomationPeer();
        var rootRegistry = new AutomationRootRegistry(() => new AutomationPeer[] { peer });

        using var session = new AutomationBridgeSession(rootRegistry);
        var roots = session.GetRoots();
        var handle = roots[0].Id;

        session.InvalidatePeer(peer);

        var found = session.TryGetPeer(handle, out _);
        Assert.False(found);
    }

    [Fact]
    public void InvalidatePeer_IsNoOp_ForUnregisteredPeer()
    {
        var rootRegistry = new AutomationRootRegistry();
        using var session = new AutomationBridgeSession(rootRegistry);
        var peer = new StubAutomationPeer();

        // Must not throw
        session.InvalidatePeer(peer);
    }

    // -------------------------------------------------------------------------
    // Describe node
    // -------------------------------------------------------------------------

    [Fact]
    public void DescribeNode_ReturnsSummary_ForKnownHandle()
    {
        var peer = new StubAutomationPeer { Name = "OK" };
        var rootRegistry = new AutomationRootRegistry(() => new AutomationPeer[] { peer });

        using var session = new AutomationBridgeSession(rootRegistry);
        var roots = session.GetRoots();
        var handle = roots[0].Id;

        var dto = session.DescribeNode(handle);

        Assert.NotNull(dto);
        Assert.Equal("OK", dto!.Name);
    }

    [Fact]
    public void DescribeNode_ReturnsNull_ForUnknownHandle()
    {
        var rootRegistry = new AutomationRootRegistry();
        using var session = new AutomationBridgeSession(rootRegistry);

        var dto = session.DescribeNode("w99");

        Assert.Null(dto);
    }

    // -------------------------------------------------------------------------
    // Disposal
    // -------------------------------------------------------------------------

    [Fact]
    public void GetRoots_ThrowsObjectDisposedException_AfterDispose()
    {
        var rootRegistry = new AutomationRootRegistry();
        var session = new AutomationBridgeSession(rootRegistry);
        session.Dispose();

        Assert.Throws<ObjectDisposedException>(() => session.GetRoots());
    }

    [Fact]
    public void TryGetPeer_ThrowsObjectDisposedException_AfterDispose()
    {
        var rootRegistry = new AutomationRootRegistry();
        var session = new AutomationBridgeSession(rootRegistry);
        session.Dispose();

        Assert.Throws<ObjectDisposedException>(() => session.TryGetPeer("w1", out _));
    }

    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    /// <summary>
    /// A minimal <see cref="ITopLevelPeerSource"/> backed by a lambda,
    /// used to prove <see cref="AutomationBridgeSession"/> accepts any implementation
    /// of the interface, not only <see cref="AutomationRootRegistry"/>.
    /// </summary>
    private sealed class LambdaTopLevelPeerSource : ITopLevelPeerSource
    {
        private readonly System.Func<IReadOnlyList<AutomationPeer>> _getPeers;

        public LambdaTopLevelPeerSource(System.Func<IReadOnlyList<AutomationPeer>> getPeers)
            => _getPeers = getPeers;

        public IReadOnlyList<AutomationPeer> GetPeers() => _getPeers();
    }
}
