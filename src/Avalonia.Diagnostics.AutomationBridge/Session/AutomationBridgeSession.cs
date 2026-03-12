using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using Avalonia.Automation.Peers;
using Avalonia.AutomationBridge.Protocol.Messages;
using Avalonia.Diagnostics.AutomationBridge.Snapshot;

namespace Avalonia.Diagnostics.AutomationBridge.Session;

/// <summary>
/// Per-connection session state that ties together the top-level peer source and the
/// session-local handle registry.
/// </summary>
/// <remarks>
/// <para>
/// One <see cref="AutomationBridgeSession"/> is created for each client connection.
/// It owns an <see cref="AutomationHandleRegistry"/> (handle assignment is session-local)
/// and holds a reference to an <see cref="ITopLevelPeerSource"/> that reflects the live
/// application window set on each call.
/// </para>
/// <para>
/// Sessions are not thread-safe.  The connection owner must serialise calls or hold an
/// appropriate lock.
/// </para>
/// </remarks>
public sealed class AutomationBridgeSession : IDisposable
{
    private readonly ITopLevelPeerSource _peerSource;
    private readonly AutomationHandleRegistry _handleRegistry = new();
    private bool _disposed;

    /// <summary>
    /// Initialises a new session backed by <paramref name="peerSource"/>.
    /// </summary>
    /// <param name="peerSource">
    /// The live source of top-level automation root peers.  Must not be null.
    /// </param>
    public AutomationBridgeSession(ITopLevelPeerSource peerSource)
    {
        _peerSource = peerSource ?? throw new ArgumentNullException(nameof(peerSource));
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

        var roots = _peerSource.GetPeers();
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

        return SummarizePeer(peer);
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

    internal bool TryGetRootPeer(string handle, [NotNullWhen(true)] out AutomationPeer? peer)
    {
        ThrowIfDisposed();

        if (!_handleRegistry.TryGetPeer(handle, out peer))
            return false;

        if (IsCurrentRoot(peer))
            return true;

        peer = null;
        return false;
    }

    internal string GetOrAssignHandle(AutomationPeer peer)
    {
        ThrowIfDisposed();

        return IsCurrentRoot(peer)
            ? _handleRegistry.GetOrAssignRootHandle(peer)
            : _handleRegistry.GetOrAssignNodeHandle(peer);
    }

    internal NodeSummaryDto SummarizePeer(AutomationPeer peer)
    {
        ThrowIfDisposed();

        var handle = GetOrAssignHandle(peer);
        var rootId = ResolveRootId(peer, handle);
        return AutomationNodeSummaryBuilder.Build(peer, handle, rootId);
    }

    /// <summary>Disposes the session, releasing all held state.</summary>
    public void Dispose() => _disposed = true;

    // -------------------------------------------------------------------------
    // Private helpers
    // -------------------------------------------------------------------------

    private string ResolveRootId(AutomationPeer peer, string fallback)
    {
        // If the peer is itself a current root, its own handle is the rootId.
        if (IsCurrentRoot(peer))
            return _handleRegistry.GetOrAssignRootHandle(peer);

        // Walk the automation tree to find a root that owns this peer.
        var automationRoot = peer.GetAutomationRoot();
        if (automationRoot is not null
            && !ReferenceEquals(automationRoot, peer)
            && IsCurrentRoot(automationRoot))
        {
            return _handleRegistry.GetOrAssignRootHandle(automationRoot);
        }

        return fallback;
    }

    private bool IsCurrentRoot(AutomationPeer peer)
    {
        var roots = _peerSource.GetPeers();

        for (var i = 0; i < roots.Count; i++)
        {
            if (ReferenceEquals(roots[i], peer))
                return true;
        }

        return false;
    }

    private void ThrowIfDisposed()
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(AutomationBridgeSession));
    }
}
