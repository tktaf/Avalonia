using System;
using System.Collections.Generic;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;

namespace Avalonia.Diagnostics.AutomationBridge.Session;

/// <summary>
/// An <see cref="ITopLevelPeerSource"/> that enumerates root peers from the current application
/// lifetime on each call, so sessions always reflect the live window set.
/// </summary>
/// <remarks>
/// <para>
/// On each <see cref="GetPeers"/> call the registry reads
/// <see cref="Application.ApplicationLifetime"/>:
/// <list type="bullet">
///   <item><see cref="IClassicDesktopStyleApplicationLifetime"/> — returns a peer for every open window.</item>
///   <item><see cref="ISingleTopLevelApplicationLifetime"/> — returns a single peer when a top-level is present.</item>
///   <item>Any other value (including <see langword="null"/>) — returns an empty list.</item>
/// </list>
/// </para>
/// <para>
/// An internal constructor accepting a <see cref="Func{TResult}"/> delegate is available for
/// unit tests that need to inject a controlled peer list without a running application.
/// </para>
/// <para>
/// The registry is not thread-safe; callers must synchronise access if roots can change
/// concurrently.
/// </para>
/// </remarks>
public sealed class AutomationRootRegistry : ITopLevelPeerSource
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

    /// <summary>The current ordered list of root peers, sourced from the application lifetime.</summary>
    public IReadOnlyList<AutomationPeer> Roots => _rootProvider();

    /// <inheritdoc/>
    IReadOnlyList<AutomationPeer> ITopLevelPeerSource.GetPeers() => Roots;

    // -------------------------------------------------------------------------
    // Private helpers
    // -------------------------------------------------------------------------

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
