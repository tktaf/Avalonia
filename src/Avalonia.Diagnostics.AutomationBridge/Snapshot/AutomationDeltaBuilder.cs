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
    private Dictionary<string, NodePatchDto>? _capturedUpdated;
    private List<string>? _capturedAdded;
    private List<string>? _capturedRemoved;
    private string? _capturedFocus;
    private long? _capturedStartingRevision;
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

    public void BeginActionCapture(long startingRevision)
    {
        _capturedStartingRevision = startingRevision;
        _capturedUpdated = new Dictionary<string, NodePatchDto>(StringComparer.Ordinal);
        _capturedAdded = [];
        _capturedRemoved = [];
        _capturedFocus = null;
    }

    public DeltaDto CompleteAction(AutomationPeer peer, long startingRevision)
    {
        try
        {
            if (_revision == startingRevision)
                return EmptyDelta(startingRevision);

            if (_capturedStartingRevision == startingRevision
                && _capturedUpdated is not null
                && _capturedAdded is not null
                && _capturedRemoved is not null)
            {
                return new DeltaDto
                {
                    Revision = _revision,
                    Updated = [.. _capturedUpdated.Values],
                    Added = [.. _capturedAdded],
                    Removed = [.. _capturedRemoved],
                    Focus = _capturedFocus,
                };
            }

            return _lastDelta;
        }
        finally
        {
            EndActionCapture();
        }
    }

    public void EndActionCapture()
    {
        _capturedStartingRevision = null;
        _capturedUpdated = null;
        _capturedAdded = null;
        _capturedRemoved = null;
        _capturedFocus = null;
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

        CaptureActionDelta(_lastDelta);

        return _lastDelta;
    }

    private void CaptureActionDelta(DeltaDto delta)
    {
        if (_capturedStartingRevision is not long startingRevision
            || delta.Revision <= startingRevision
            || _capturedUpdated is null
            || _capturedAdded is null
            || _capturedRemoved is null)
        {
            return;
        }

        foreach (var patch in delta.Updated)
        {
            _capturedUpdated[patch.Id] = _capturedUpdated.TryGetValue(patch.Id, out var existing)
                ? MergePatch(existing, patch)
                : patch;
        }

        MergeHandles(_capturedAdded, _capturedRemoved, delta.Added);
        MergeHandles(_capturedRemoved, _capturedAdded, delta.Removed);

        if (delta.Focus is not null)
            _capturedFocus = delta.Focus;
    }

    private static NodePatchDto MergePatch(NodePatchDto existing, NodePatchDto update)
        => new()
        {
            Id = existing.Id,
            Enabled = update.Enabled ?? existing.Enabled,
            Focused = update.Focused ?? existing.Focused,
            Value = update.Value ?? existing.Value,
            Offscreen = update.Offscreen ?? existing.Offscreen,
            Selected = update.Selected ?? existing.Selected,
            Expanded = update.Expanded ?? existing.Expanded,
            Checked = update.Checked ?? existing.Checked,
            Name = update.Name ?? existing.Name,
            State = update.State ?? existing.State,
            Metadata = update.Metadata ?? existing.Metadata,
        };

    private static void MergeHandles(List<string> target, List<string> opposite, IEnumerable<string> handles)
    {
        foreach (var handle in handles)
        {
            opposite.Remove(handle);
            if (!target.Exists(existing => string.Equals(existing, handle, StringComparison.Ordinal)))
                target.Add(handle);
        }
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
            Selected = summary.Selected,
            Expanded = summary.Expanded,
            Checked = summary.Checked,
            Value = summary.Value,
            State = summary.State,
            Metadata = summary.Metadata,
        };
    }

    private NodePatchDto? BuildPatch(AutomationPeer peer, AutomationProperty property)
    {
        var id = _session.GetOrAssignHandle(peer);

        if (property == AutomationElementIdentifiers.NameProperty)
        {
            var (name, metadata) = AutomationNodeSummaryBuilder.BuildNameAndMetadataForPatch(peer);
            return new NodePatchDto
            {
                Id = id,
                Name = name,
                Metadata = metadata,
            };
        }

        if (property == ValuePatternIdentifiers.ValueProperty)
        {
            return new NodePatchDto
            {
                Id = id,
                Value = AutomationNodeSummaryBuilder.TryGetValue(peer),
            };
        }

        if (property == SelectionItemPatternIdentifiers.IsSelectedProperty)
        {
            return new NodePatchDto
            {
                Id = id,
                Selected = AutomationNodeSummaryBuilder.GetSelected(peer),
            };
        }

        if (property == ExpandCollapsePatternIdentifiers.ExpandCollapseStateProperty)
        {
            return new NodePatchDto
            {
                Id = id,
                Expanded = AutomationNodeSummaryBuilder.GetExpanded(peer),
            };
        }

        if (property == TogglePatternIdentifiers.ToggleStateProperty)
        {
            return new NodePatchDto
            {
                Id = id,
                Checked = AutomationNodeSummaryBuilder.GetChecked(peer),
            };
        }

        if (property == AutomationElementIdentifiers.ItemStatusProperty)
        {
            return new NodePatchDto
            {
                Id = id,
                State = AutomationNodeSummaryBuilder.GetState(peer),
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
