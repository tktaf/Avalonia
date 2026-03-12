using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using Avalonia.Automation.Peers;
using Avalonia.AutomationBridge.Protocol.Messages;
using Avalonia.Diagnostics.AutomationBridge.Snapshot;

namespace Avalonia.Diagnostics.AutomationBridge.Session;

/// <summary>
/// Per-connection session state that ties together the root and handle registries.
/// </summary>
/// <remarks>
/// <para>
/// One <see cref="AutomationBridgeSession"/> is created for each client connection.
/// It owns a <see cref="AutomationHandleRegistry"/> (handle assignment is session-local)
/// but shares a <see cref="AutomationRootRegistry"/> with the host process (roots are
/// global to the application lifetime).
/// </para>
/// <para>
/// Sessions are not thread-safe.  The connection owner must serialise calls or hold an
/// appropriate lock.
/// </para>
/// </remarks>
public sealed class AutomationBridgeSession : IDisposable
{
    private readonly AutomationRootRegistry _rootRegistry;
    private readonly AutomationHandleRegistry _handleRegistry = new();
    private bool _disposed;

    /// <summary>
    /// Initialises a new session that reads roots from <paramref name="rootRegistry"/>.
    /// </summary>
    /// <param name="rootRegistry">
    /// The shared registry of top-level automation roots.  Must not be null.
    /// </param>
    public AutomationBridgeSession(AutomationRootRegistry rootRegistry)
    {
        _rootRegistry = rootRegistry ?? throw new ArgumentNullException(nameof(rootRegistry));
    }

    /// <summary>
    /// Returns compact summaries for all currently known roots.
    /// </summary>
    /// <remarks>
    /// Children are not traversed; only the root nodes themselves are summarised.
    /// Handles are assigned on first access and remain stable for the session lifetime.
    /// </remarks>
    /// <returns>
    /// An ordered list of <see cref="NodeSummaryDto"/> instances, one per root.
    /// </returns>
    public IReadOnlyList<NodeSummaryDto> GetRoots()
    {
        ThrowIfDisposed();

        var roots = _rootRegistry.Roots;
        var result = new NodeSummaryDto[roots.Count];

        for (var i = 0; i < roots.Count; i++)
        {
            var peer = roots[i];
            var handle = _handleRegistry.GetOrAssignRootHandle(peer);
            result[i] = AutomationNodeSummaryBuilder.Build(peer, handle, rootId: handle);
        }

        return result;
    }

    /// <summary>
    /// Tries to resolve a previously assigned handle to its automation peer.
    /// </summary>
    /// <param name="handle">The handle to look up.</param>
    /// <param name="peer">
    /// When this method returns <see langword="true"/>, the peer that owns
    /// <paramref name="handle"/>; otherwise <see langword="null"/>.
    /// </param>
    /// <returns>
    /// <see langword="true"/> if the handle was found and the peer is still live;
    /// <see langword="false"/> if the handle is unknown or has been invalidated.
    /// </returns>
    public bool TryGetPeer(string handle, [NotNullWhen(true)] out AutomationPeer? peer)
    {
        ThrowIfDisposed();
        return _handleRegistry.TryGetPeer(handle, out peer);
    }

    /// <summary>
    /// Builds a compact summary for the node identified by <paramref name="handle"/>.
    /// </summary>
    /// <param name="handle">The session-local handle of the node to describe.</param>
    /// <returns>
    /// A <see cref="NodeSummaryDto"/>, or <see langword="null"/> if the handle is unknown
    /// or has been invalidated.
    /// </returns>
    public NodeSummaryDto? DescribeNode(string handle)
    {
        ThrowIfDisposed();

        if (!_handleRegistry.TryGetPeer(handle, out var peer))
            return null;

        var rootId = ResolveRootId(peer, fallback: handle);
        return AutomationNodeSummaryBuilder.Build(peer, handle, rootId);
    }

    /// <summary>
    /// Invalidates the handle for <paramref name="peer"/>, for example when a
    /// <c>ChildrenChanged</c> event indicates the peer has been removed from the tree.
    /// Subsequent <see cref="TryGetPeer"/> lookups using the former handle will fail.
    /// If the peer is not currently registered this is a no-op.
    /// </summary>
    /// <param name="peer">The peer whose handle should be invalidated.</param>
    public void InvalidatePeer(AutomationPeer peer)
    {
        ThrowIfDisposed();
        _handleRegistry.Invalidate(peer);
    }

    /// <summary>Disposes the session, releasing all held state.</summary>
    public void Dispose() => _disposed = true;

    // -------------------------------------------------------------------------
    // Private helpers
    // -------------------------------------------------------------------------

    private string ResolveRootId(AutomationPeer peer, string fallback)
    {
        // If the peer is itself a registered root, its own handle is the rootId.
        var roots = _rootRegistry.Roots;
        for (var i = 0; i < roots.Count; i++)
        {
            if (ReferenceEquals(roots[i], peer))
            {
                return _handleRegistry.TryGetHandle(peer, out var selfHandle)
                    ? selfHandle
                    : fallback;
            }
        }

        // Walk the automation root to find a registered root peer.
        var automationRoot = peer.GetAutomationRoot();
        if (automationRoot is not null
            && !ReferenceEquals(automationRoot, peer)
            && _handleRegistry.TryGetHandle(automationRoot, out var rootHandle))
        {
            return rootHandle;
        }

        return fallback;
    }

    private void ThrowIfDisposed()
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(AutomationBridgeSession));
    }
}
