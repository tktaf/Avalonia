using Avalonia.Diagnostics.AutomationBridge.Session;
using Xunit;

namespace Avalonia.Diagnostics.AutomationBridge.Tests.Session;

/// <summary>
/// Tests for <see cref="AutomationRootRegistry"/>.
/// Proves top-level roots are enumerated without dumping full trees.
/// </summary>
public sealed class AutomationRootRegistryTests
{
    [Fact]
    public void Roots_IsEmpty_WhenNoneRegistered()
    {
        var registry = new AutomationRootRegistry();

        Assert.Empty(registry.Roots);
    }

    [Fact]
    public void AddRoot_RegistersRoot_InOrder()
    {
        var registry = new AutomationRootRegistry();
        var p1 = new StubAutomationPeer();
        var p2 = new StubAutomationPeer();

        registry.AddRoot(p1);
        registry.AddRoot(p2);

        Assert.Equal(2, registry.Roots.Count);
        Assert.Same(p1, registry.Roots[0]);
        Assert.Same(p2, registry.Roots[1]);
    }

    [Fact]
    public void AddRoot_IsIdempotent_ForSamePeer()
    {
        var registry = new AutomationRootRegistry();
        var peer = new StubAutomationPeer();

        registry.AddRoot(peer);
        registry.AddRoot(peer);

        Assert.Single(registry.Roots);
    }

    [Fact]
    public void RemoveRoot_RemovesRoot()
    {
        var registry = new AutomationRootRegistry();
        var peer = new StubAutomationPeer();
        registry.AddRoot(peer);

        registry.RemoveRoot(peer);

        Assert.Empty(registry.Roots);
    }

    [Fact]
    public void RemoveRoot_IsNoOp_ForUnregisteredPeer()
    {
        var registry = new AutomationRootRegistry();
        var peer = new StubAutomationPeer();

        // Must not throw
        registry.RemoveRoot(peer);
    }

    [Fact]
    public void RemoveRoot_RemovesOnlySpecifiedPeer()
    {
        var registry = new AutomationRootRegistry();
        var p1 = new StubAutomationPeer();
        var p2 = new StubAutomationPeer();
        registry.AddRoot(p1);
        registry.AddRoot(p2);

        registry.RemoveRoot(p1);

        Assert.Single(registry.Roots);
        Assert.Same(p2, registry.Roots[0]);
    }
}
