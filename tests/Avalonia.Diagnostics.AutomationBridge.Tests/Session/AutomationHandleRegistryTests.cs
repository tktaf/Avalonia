using Avalonia.Automation.Peers;
using Avalonia.Diagnostics.AutomationBridge.Session;
using Xunit;

namespace Avalonia.Diagnostics.AutomationBridge.Tests.Session;

/// <summary>
/// Tests for <see cref="AutomationHandleRegistry"/>.
/// Proves handle assignment is stable within a session and that disappearing peers
/// can be cleanly invalidated.
/// </summary>
public sealed class AutomationHandleRegistryTests
{
    // -------------------------------------------------------------------------
    // Root handle assignment
    // -------------------------------------------------------------------------

    [Fact]
    public void GetOrAssignRootHandle_AssignsW1_ForFirstRoot()
    {
        var registry = new AutomationHandleRegistry();
        var peer = new StubAutomationPeer();

        var handle = registry.GetOrAssignRootHandle(peer);

        Assert.Equal("w1", handle);
    }

    [Fact]
    public void GetOrAssignRootHandle_AssignsIncreasingNumbers_ForMultipleRoots()
    {
        var registry = new AutomationHandleRegistry();
        var p1 = new StubAutomationPeer();
        var p2 = new StubAutomationPeer();

        var h1 = registry.GetOrAssignRootHandle(p1);
        var h2 = registry.GetOrAssignRootHandle(p2);

        Assert.Equal("w1", h1);
        Assert.Equal("w2", h2);
    }

    [Fact]
    public void GetOrAssignRootHandle_IsStable_ForSamePeer()
    {
        var registry = new AutomationHandleRegistry();
        var peer = new StubAutomationPeer();

        var h1 = registry.GetOrAssignRootHandle(peer);
        var h2 = registry.GetOrAssignRootHandle(peer);

        Assert.Equal(h1, h2);
    }

    // -------------------------------------------------------------------------
    // Node handle assignment
    // -------------------------------------------------------------------------

    [Fact]
    public void GetOrAssignNodeHandle_AssignsN1_ForFirstNode()
    {
        var registry = new AutomationHandleRegistry();
        var peer = new StubAutomationPeer();

        var handle = registry.GetOrAssignNodeHandle(peer);

        Assert.Equal("n1", handle);
    }

    [Fact]
    public void GetOrAssignNodeHandle_IsStable_ForSamePeer()
    {
        var registry = new AutomationHandleRegistry();
        var peer = new StubAutomationPeer();

        var h1 = registry.GetOrAssignNodeHandle(peer);
        var h2 = registry.GetOrAssignNodeHandle(peer);

        Assert.Equal(h1, h2);
    }

    [Fact]
    public void RootAndNodeCounters_AreIndependent()
    {
        var registry = new AutomationHandleRegistry();
        var root = new StubAutomationPeer();
        var node = new StubAutomationPeer();

        var rootHandle = registry.GetOrAssignRootHandle(root);
        var nodeHandle = registry.GetOrAssignNodeHandle(node);

        Assert.Equal("w1", rootHandle);
        Assert.Equal("n1", nodeHandle);
        Assert.NotEqual(rootHandle, nodeHandle);
    }

    // -------------------------------------------------------------------------
    // Reverse lookup
    // -------------------------------------------------------------------------

    [Fact]
    public void TryGetPeer_ReturnsTrue_AndCorrectPeer_ForKnownHandle()
    {
        var registry = new AutomationHandleRegistry();
        var peer = new StubAutomationPeer();
        var handle = registry.GetOrAssignRootHandle(peer);

        var found = registry.TryGetPeer(handle, out var resolved);

        Assert.True(found);
        Assert.Same(peer, resolved);
    }

    [Fact]
    public void TryGetPeer_ReturnsFalse_ForUnknownHandle()
    {
        var registry = new AutomationHandleRegistry();

        var found = registry.TryGetPeer("w99", out var resolved);

        Assert.False(found);
        Assert.Null(resolved);
    }

    [Fact]
    public void TryGetHandle_ReturnsTrue_AndCorrectHandle_ForKnownPeer()
    {
        var registry = new AutomationHandleRegistry();
        var peer = new StubAutomationPeer();
        var assigned = registry.GetOrAssignRootHandle(peer);

        var found = registry.TryGetHandle(peer, out var handle);

        Assert.True(found);
        Assert.Equal(assigned, handle);
    }

    [Fact]
    public void TryGetHandle_ReturnsFalse_ForUnregisteredPeer()
    {
        var registry = new AutomationHandleRegistry();
        var peer = new StubAutomationPeer();

        var found = registry.TryGetHandle(peer, out _);

        Assert.False(found);
    }

    // -------------------------------------------------------------------------
    // Invalidation
    // -------------------------------------------------------------------------

    [Fact]
    public void Invalidate_RemovesPeer_SoHandleLookupFails()
    {
        var registry = new AutomationHandleRegistry();
        var peer = new StubAutomationPeer();
        var handle = registry.GetOrAssignNodeHandle(peer);

        registry.Invalidate(peer);

        var found = registry.TryGetPeer(handle, out _);
        Assert.False(found);
    }

    [Fact]
    public void Invalidate_RemovesPeer_SoPeerLookupFails()
    {
        var registry = new AutomationHandleRegistry();
        var peer = new StubAutomationPeer();
        registry.GetOrAssignNodeHandle(peer);

        registry.Invalidate(peer);

        var found = registry.TryGetHandle(peer, out _);
        Assert.False(found);
    }

    [Fact]
    public void Invalidate_IsNoOp_ForUnregisteredPeer()
    {
        var registry = new AutomationHandleRegistry();
        var peer = new StubAutomationPeer();

        // Must not throw
        registry.Invalidate(peer);
    }

    [Fact]
    public void AfterInvalidation_NewHandleCanBeAssigned_WithNextSequenceNumber()
    {
        var registry = new AutomationHandleRegistry();
        var peer = new StubAutomationPeer();

        registry.GetOrAssignNodeHandle(peer);  // n1
        registry.Invalidate(peer);
        var newHandle = registry.GetOrAssignNodeHandle(peer);

        // Counter does not rewind; next handle uses the next available number
        Assert.Equal("n2", newHandle);
    }
}
