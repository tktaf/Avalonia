using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using Avalonia.Automation.Peers;

namespace Avalonia.Diagnostics.AutomationBridge.Session;

/// <summary>
/// Maps automation peers to stable, session-local handle strings for the lifetime of a single
/// bridge connection.
/// </summary>
/// <remarks>
/// <para>
/// Root peers (top-level windows or top-levels) are assigned handles of the form <c>w1</c>,
/// <c>w2</c>, …  Non-root nodes are assigned handles of the form <c>n1</c>, <c>n2</c>, …
/// </para>
/// <para>
/// The registry is intentionally not thread-safe.  Session state is owned by a single
/// connection and must be accessed from the connection's logical thread or a lock held by the
/// session owner.
/// </para>
/// </remarks>
public sealed class AutomationHandleRegistry
{
    private readonly Dictionary<AutomationPeer, string> _peerToHandle =
        new(ReferenceEqualityComparer.Instance);

    private readonly Dictionary<string, AutomationPeer> _handleToPeer =
        new(StringComparer.Ordinal);

    private int _windowCounter;
    private int _nodeCounter;

    /// <summary>
    /// Returns the existing root handle for <paramref name="peer"/>, or assigns and caches a
    /// new one in the form <c>w&lt;n&gt;</c>.
    /// </summary>
    /// <param name="peer">The root peer to look up or register.</param>
    /// <returns>The stable handle string.</returns>
    public string GetOrAssignRootHandle(AutomationPeer peer)
        => GetOrAssign(peer, 'w', ref _windowCounter);

    /// <summary>
    /// Returns the existing node handle for <paramref name="peer"/>, or assigns and caches a
    /// new one in the form <c>n&lt;n&gt;</c>.
    /// </summary>
    /// <param name="peer">The node peer to look up or register.</param>
    /// <returns>The stable handle string.</returns>
    public string GetOrAssignNodeHandle(AutomationPeer peer)
        => GetOrAssign(peer, 'n', ref _nodeCounter);

    /// <summary>
    /// Tries to resolve a previously assigned handle back to its peer.
    /// </summary>
    /// <param name="handle">The handle string to look up.</param>
    /// <param name="peer">
    /// When this method returns <see langword="true"/>, the peer that owns
    /// <paramref name="handle"/>; otherwise <see langword="null"/>.
    /// </param>
    /// <returns>
    /// <see langword="true"/> if the handle is known and the peer is still live;
    /// <see langword="false"/> otherwise.
    /// </returns>
    public bool TryGetPeer(string handle, [NotNullWhen(true)] out AutomationPeer? peer)
        => _handleToPeer.TryGetValue(handle, out peer);

    /// <summary>
    /// Tries to retrieve the handle previously assigned to <paramref name="peer"/>.
    /// </summary>
    /// <param name="peer">The peer to look up.</param>
    /// <param name="handle">
    /// When this method returns <see langword="true"/>, the assigned handle string;
    /// otherwise <see langword="null"/>.
    /// </param>
    /// <returns><see langword="true"/> if the peer has an assigned handle; otherwise <see langword="false"/>.</returns>
    public bool TryGetHandle(AutomationPeer peer, [NotNullWhen(true)] out string? handle)
        => _peerToHandle.TryGetValue(peer, out handle);

    /// <summary>
    /// Removes <paramref name="peer"/> from the registry, invalidating its handle.
    /// Any subsequent lookup by the former handle will fail.
    /// If the peer is not currently registered this is a no-op.
    /// </summary>
    /// <param name="peer">The peer whose handle should be invalidated.</param>
    public void Invalidate(AutomationPeer peer)
    {
        if (_peerToHandle.Remove(peer, out var handle))
            _handleToPeer.Remove(handle);
    }

    // -------------------------------------------------------------------------
    // Private helpers
    // -------------------------------------------------------------------------

    private string GetOrAssign(AutomationPeer peer, char prefix, ref int counter)
    {
        if (_peerToHandle.TryGetValue(peer, out var existing))
            return existing;

        var handle = $"{prefix}{++counter}";
        _peerToHandle[peer] = handle;
        _handleToPeer[handle] = peer;
        return handle;
    }
}
