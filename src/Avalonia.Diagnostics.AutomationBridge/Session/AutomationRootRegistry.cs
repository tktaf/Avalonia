using System;
using System.Collections.Generic;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;

namespace Avalonia.Diagnostics.AutomationBridge.Session;

/// <summary>
/// Maintains the ordered list of top-level automation roots for a bridge session.
/// </summary>
/// <remarks>
/// Roots are discovered from the current <see cref="Application.ApplicationLifetime"/> on
/// demand so that sessions enumerate the live top-levels that Avalonia already knows about.
/// </remarks>
public sealed class AutomationRootRegistry
{
    private readonly Func<IReadOnlyList<AutomationPeer>> _rootProvider;

    /// <summary>
    /// Initializes a registry that reads roots from the current application lifetime.
    /// </summary>
    public AutomationRootRegistry()
        : this(GetCurrentRoots)
    {
    }

    /// <summary>
    /// Initializes a registry with an explicit root provider.
    /// </summary>
    /// <param name="rootProvider">Provides the current ordered set of root peers.</param>
    internal AutomationRootRegistry(Func<IReadOnlyList<AutomationPeer>> rootProvider)
    {
        _rootProvider = rootProvider ?? throw new ArgumentNullException(nameof(rootProvider));
    }

    /// <summary>The current ordered list of root peers.</summary>
    public IReadOnlyList<AutomationPeer> Roots => _rootProvider();

    private static IReadOnlyList<AutomationPeer> GetCurrentRoots()
    {
        var lifetime = Application.Current?.ApplicationLifetime;

        if (lifetime is IClassicDesktopStyleApplicationLifetime desktopLifetime)
        {
            return GetPeers(desktopLifetime.Windows);
        }

        if (lifetime is ISingleTopLevelApplicationLifetime { TopLevel: Control topLevel })
        {
            return new[] { ControlAutomationPeer.CreatePeerForElement(topLevel) };
        }

        return Array.Empty<AutomationPeer>();
    }

    private static IReadOnlyList<AutomationPeer> GetPeers(IReadOnlyList<Window> windows)
    {
        if (windows.Count == 0)
        {
            return Array.Empty<AutomationPeer>();
        }

        var roots = new AutomationPeer[windows.Count];

        for (var i = 0; i < windows.Count; i++)
        {
            roots[i] = ControlAutomationPeer.CreatePeerForElement(windows[i]);
        }

        return roots;
    }
}
