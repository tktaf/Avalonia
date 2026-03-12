using System.Collections.Generic;
using Avalonia.Automation.Peers;

namespace Avalonia.Diagnostics.AutomationBridge.Session;

/// <summary>
/// Maintains the ordered list of top-level automation roots for a bridge session.
/// </summary>
/// <remarks>
/// <para>
/// Roots are the peers that represent top-level windows or top-level controls.  They are
/// registered explicitly by the hosting layer (e.g. <c>AutomationBridgeHostedService</c>
/// reacting to lifetime events) rather than being discovered automatically.
/// </para>
/// <para>
/// The registry is not thread-safe; callers must synchronise access if roots can be added
/// or removed from multiple threads.
/// </para>
/// </remarks>
public sealed class AutomationRootRegistry
{
    private readonly List<AutomationPeer> _roots = new();

    /// <summary>The current ordered list of root peers.</summary>
    public IReadOnlyList<AutomationPeer> Roots => _roots;

    /// <summary>
    /// Registers <paramref name="peer"/> as a root.  If the peer is already registered
    /// this is a no-op; the registration order of previously added peers is preserved.
    /// </summary>
    /// <param name="peer">The root peer to register.</param>
    public void AddRoot(AutomationPeer peer)
    {
        if (!_roots.Contains(peer))
            _roots.Add(peer);
    }

    /// <summary>
    /// Removes <paramref name="peer"/> from the root list.  If the peer is not registered
    /// this is a no-op.
    /// </summary>
    /// <param name="peer">The root peer to remove.</param>
    public void RemoveRoot(AutomationPeer peer)
        => _roots.Remove(peer);
}
