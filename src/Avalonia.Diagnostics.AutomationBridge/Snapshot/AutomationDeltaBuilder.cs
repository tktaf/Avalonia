using System;
using System.Collections.Generic;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Automation.Provider;
using Avalonia.AutomationBridge.Protocol.Messages;
using Avalonia.Diagnostics.AutomationBridge.Session;
using Avalonia.Utilities;

namespace Avalonia.Diagnostics.AutomationBridge.Snapshot;

/// <summary>
/// Tracks compact revision deltas for a single automation root using peer change events.
/// </summary>
internal sealed class AutomationDeltaBuilder : IDisposable
{
    private readonly AutomationBridgeSession _session;
    private readonly AutomationPeer _rootPeer;
    private readonly IRootProvider? _rootProvider;
    private readonly HashSet<AutomationPeer> _knownPeers = new(ReferenceEqualityComparer.Instance);
    private DeltaDto _lastDelta;
    private long _revision;

    public AutomationDeltaBuilder(AutomationBridgeSession session, AutomationPeer rootPeer)
    {
        _session = session ?? throw new ArgumentNullException(nameof(session));
        _rootPeer = rootPeer ?? throw new ArgumentNullException(nameof(rootPeer));
        _rootProvider = rootPeer as IRootProvider;
        _lastDelta = EmptyDelta(0);

        foreach (var peer in EnumerateSubtree(rootPeer))
        {
            TrackPeer(peer);
        }

        if (_rootProvider is not null)
        {
            _rootProvider.FocusChanged += RootFocusChanged;
        }
    }

    public long CurrentRevision => _revision;

    public DeltaDto CompleteAction(AutomationPeer peer, long startingRevision)
    {
        if (_revision != startingRevision)
            return _lastDelta;

        return PublishDelta(
            updated: new[] { BuildPatch(peer) },
            added: Array.Empty<string>(),
            removed: Array.Empty<string>(),
            focus: null);
    }

    public BridgeResponse GetDelta(long? sinceRevision, string? requestId = null)
    {
        if (sinceRevision is null || sinceRevision == _revision)
            return BridgeResponse.WithDelta(EmptyDelta(_revision), requestId);

        if (sinceRevision == _revision - 1)
            return BridgeResponse.WithDelta(_lastDelta, requestId);

        return BridgeResponse.Failure(
            BridgeErrorCode.StaleRevision,
            $"Revision '{sinceRevision}' is stale; current revision is '{_revision}'.",
            requestId);
    }

    public void Dispose()
    {
        if (_rootProvider is not null)
            _rootProvider.FocusChanged -= RootFocusChanged;

        foreach (var peer in _knownPeers)
        {
            peer.ChildrenChanged -= PeerChildrenChanged;
            peer.PropertyChanged -= PeerPropertyChanged;
        }
    }

    private void PeerChildrenChanged(object? sender, EventArgs e)
    {
        var currentPeers = new HashSet<AutomationPeer>(EnumerateSubtree(_rootPeer), ReferenceEqualityComparer.Instance);
        var added = new List<string>();
        var removed = new List<string>();

        foreach (var peer in currentPeers)
        {
            if (_knownPeers.Add(peer))
            {
                TrackPeer(peer);
                added.Add(_session.GetOrAssignHandle(peer));
            }
        }

        var stalePeers = new List<AutomationPeer>();
        foreach (var peer in _knownPeers)
        {
            if (!currentPeers.Contains(peer))
            {
                stalePeers.Add(peer);
            }
        }

        foreach (var peer in stalePeers)
        {
            if (_session.TryGetHandle(peer, out var handle))
                removed.Add(handle);

            UntrackPeer(peer);
            _knownPeers.Remove(peer);
            _session.InvalidatePeer(peer);
        }

        PublishDelta(Array.Empty<NodePatchDto>(), added.ToArray(), removed.ToArray(), null);
    }

    private void PeerPropertyChanged(object? sender, AutomationPropertyChangedEventArgs e)
    {
        if (sender is not AutomationPeer peer)
            return;

        var patch = BuildPatch(peer, e.Property);
        if (patch is null)
            return;

        PublishDelta(new[] { patch }, Array.Empty<string>(), Array.Empty<string>(), null);
    }

    private void RootFocusChanged(object? sender, EventArgs e)
    {
        var focusPeer = _rootProvider?.GetFocus();
        var focusHandle = focusPeer is null ? null : _session.GetOrAssignHandle(focusPeer);

        PublishDelta(Array.Empty<NodePatchDto>(), Array.Empty<string>(), Array.Empty<string>(), focusHandle);
    }

    private void TrackPeer(AutomationPeer peer)
    {
        _knownPeers.Add(peer);
        peer.ChildrenChanged += PeerChildrenChanged;
        peer.PropertyChanged += PeerPropertyChanged;
    }

    private void UntrackPeer(AutomationPeer peer)
    {
        peer.ChildrenChanged -= PeerChildrenChanged;
        peer.PropertyChanged -= PeerPropertyChanged;
    }

    private DeltaDto PublishDelta(NodePatchDto[] updated, string[] added, string[] removed, string? focus)
    {
        _lastDelta = new DeltaDto
        {
            Revision = ++_revision,
            Updated = updated,
            Added = added,
            Removed = removed,
            Focus = focus,
        };

        return _lastDelta;
    }

    private NodePatchDto BuildPatch(AutomationPeer peer)
    {
        var summary = _session.SummarizePeer(peer);
        return new NodePatchDto
        {
            Id = summary.Id,
            Enabled = summary.Enabled,
            Focused = summary.Focused,
            Name = summary.Name,
            Offscreen = summary.Offscreen,
            Value = summary.Value,
        };
    }

    private NodePatchDto? BuildPatch(AutomationPeer peer, AutomationProperty property)
    {
        var id = _session.GetOrAssignHandle(peer);

        if (property == AutomationElementIdentifiers.NameProperty)
        {
            return new NodePatchDto { Id = id, Name = peer.GetName() };
        }

        if (property == ValuePatternIdentifiers.ValueProperty)
        {
            return new NodePatchDto
            {
                Id = id,
                Value = peer.GetProvider<IValueProvider>()?.Value,
            };
        }

        return null;
    }

    private static IEnumerable<AutomationPeer> EnumerateSubtree(AutomationPeer root)
    {
        yield return root;

        foreach (var child in root.GetChildren())
        {
            foreach (var descendant in EnumerateSubtree(child))
            {
                yield return descendant;
            }
        }
    }

    private static DeltaDto EmptyDelta(long revision) => new()
    {
        Revision = revision,
        Updated = Array.Empty<NodePatchDto>(),
        Added = Array.Empty<string>(),
        Removed = Array.Empty<string>(),
        Focus = null,
    };
}
