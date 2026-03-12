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
    private static readonly string[] s_patchFieldOrder =
    [
        NodePatchField.Enabled,
        NodePatchField.Focused,
        NodePatchField.Value,
        NodePatchField.Offscreen,
        NodePatchField.Selected,
        NodePatchField.Expanded,
        NodePatchField.Checked,
        NodePatchField.Name,
        NodePatchField.State,
        NodePatchField.Metadata,
    ];

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
            if (_capturedRemoved.Contains(patch.Id))
                continue;

            _capturedUpdated[patch.Id] = _capturedUpdated.TryGetValue(patch.Id, out var existing)
                ? MergePatch(existing, patch)
                : patch;
        }

        MergeAddedHandles(_capturedAdded, _capturedRemoved, delta.Added);
        MergeRemovedHandles(_capturedRemoved, _capturedAdded, _capturedUpdated, delta.Removed);

        if (delta.Focus is not null)
            _capturedFocus = delta.Focus;
    }

    private static NodePatchDto MergePatch(NodePatchDto existing, NodePatchDto update)
    {
        var cleared = new HashSet<string>(StringComparer.Ordinal);
        MergeCleared(cleared, existing.Cleared);

        return new NodePatchDto
        {
            Id = existing.Id,
            Enabled = MergeNullableValue(existing.Enabled, update.Enabled, NodePatchField.Enabled, update.Cleared, cleared),
            Focused = MergeNullableValue(existing.Focused, update.Focused, NodePatchField.Focused, update.Cleared, cleared),
            Value = MergeReferenceValue(existing.Value, update.Value, NodePatchField.Value, update.Cleared, cleared),
            Offscreen = MergeNullableValue(existing.Offscreen, update.Offscreen, NodePatchField.Offscreen, update.Cleared, cleared),
            Selected = MergeNullableValue(existing.Selected, update.Selected, NodePatchField.Selected, update.Cleared, cleared),
            Expanded = MergeNullableValue(existing.Expanded, update.Expanded, NodePatchField.Expanded, update.Cleared, cleared),
            Checked = MergeNullableValue(existing.Checked, update.Checked, NodePatchField.Checked, update.Cleared, cleared),
            Name = MergeReferenceValue(existing.Name, update.Name, NodePatchField.Name, update.Cleared, cleared),
            State = MergeReferenceValue(existing.State, update.State, NodePatchField.State, update.Cleared, cleared),
            Metadata = MergeReferenceValue(existing.Metadata, update.Metadata, NodePatchField.Metadata, update.Cleared, cleared),
            Cleared = ToClearedArray(cleared),
        };
    }

    private static bool? MergeNullableValue(
        bool? existing,
        bool? update,
        string field,
        string[]? updateCleared,
        HashSet<string> cleared)
    {
        if (ContainsField(updateCleared, field))
        {
            cleared.Add(field);
            return null;
        }

        if (update is not null)
        {
            cleared.Remove(field);
            return update;
        }

        return cleared.Contains(field) ? null : existing;
    }

    private static T? MergeReferenceValue<T>(
        T? existing,
        T? update,
        string field,
        string[]? updateCleared,
        HashSet<string> cleared)
        where T : class
    {
        if (ContainsField(updateCleared, field))
        {
            cleared.Add(field);
            return null;
        }

        if (update is not null)
        {
            cleared.Remove(field);
            return update;
        }

        return cleared.Contains(field) ? null : existing;
    }

    private static void MergeCleared(HashSet<string> target, string[]? cleared)
    {
        if (cleared is null)
            return;

        foreach (var field in cleared)
            target.Add(field);
    }

    private static bool ContainsField(string[]? cleared, string field)
        => cleared is not null
           && Array.Exists(cleared, existing => string.Equals(existing, field, StringComparison.Ordinal));

    private static string[]? ToClearedArray(HashSet<string> cleared)
    {
        if (cleared.Count == 0)
            return null;

        var ordered = new List<string>(cleared.Count);
        foreach (var field in s_patchFieldOrder)
        {
            if (cleared.Contains(field))
                ordered.Add(field);
        }

        return ordered.Count == 0 ? [.. cleared] : [.. ordered];
    }

    private static void MergeAddedHandles(List<string> added, List<string> removed, IEnumerable<string> handles)
    {
        foreach (var handle in handles)
        {
            removed.Remove(handle);
            if (!added.Exists(existing => string.Equals(existing, handle, StringComparison.Ordinal)))
                added.Add(handle);
        }
    }

    private static void MergeRemovedHandles(
        List<string> removed,
        List<string> added,
        Dictionary<string, NodePatchDto> updated,
        IEnumerable<string> handles)
    {
        foreach (var handle in handles)
        {
            updated.Remove(handle);

            if (added.Remove(handle))
                continue;

            if (!removed.Exists(existing => string.Equals(existing, handle, StringComparison.Ordinal)))
                removed.Add(handle);
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
            Cleared = BuildClearedFields(
                (NodePatchField.Enabled, summary.Enabled is null),
                (NodePatchField.Focused, summary.Focused is null),
                (NodePatchField.Value, summary.Value is null),
                (NodePatchField.Offscreen, summary.Offscreen is null),
                (NodePatchField.Selected, summary.Selected is null),
                (NodePatchField.Expanded, summary.Expanded is null),
                (NodePatchField.Checked, summary.Checked is null),
                (NodePatchField.Name, summary.Name is null),
                (NodePatchField.State, summary.State is null),
                (NodePatchField.Metadata, summary.Metadata is null)),
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
                Cleared = BuildClearedFields(
                    (NodePatchField.Name, name is null),
                    (NodePatchField.Metadata, metadata is null)),
            };
        }

        if (property == AutomationElementIdentifiers.HelpTextProperty
            || property == AutomationElementIdentifiers.ItemTypeProperty)
        {
            var (_, metadata) = AutomationNodeSummaryBuilder.BuildNameAndMetadataForPatch(peer);
            return new NodePatchDto
            {
                Id = id,
                Metadata = metadata,
                Cleared = BuildClearedFields((NodePatchField.Metadata, metadata is null)),
            };
        }

        if (property == ValuePatternIdentifiers.ValueProperty)
        {
            var value = AutomationNodeSummaryBuilder.TryGetValue(peer);
            return new NodePatchDto
            {
                Id = id,
                Value = value,
                Cleared = BuildClearedFields((NodePatchField.Value, value is null)),
            };
        }

        if (property == SelectionItemPatternIdentifiers.IsSelectedProperty)
        {
            var selected = AutomationNodeSummaryBuilder.GetSelected(peer);
            return new NodePatchDto
            {
                Id = id,
                Selected = selected,
                Cleared = BuildClearedFields((NodePatchField.Selected, selected is null)),
            };
        }

        if (property == ExpandCollapsePatternIdentifiers.ExpandCollapseStateProperty)
        {
            var expanded = AutomationNodeSummaryBuilder.GetExpanded(peer);
            return new NodePatchDto
            {
                Id = id,
                Expanded = expanded,
                Cleared = BuildClearedFields((NodePatchField.Expanded, expanded is null)),
            };
        }

        if (property == TogglePatternIdentifiers.ToggleStateProperty)
        {
            var checkedState = AutomationNodeSummaryBuilder.GetChecked(peer);
            return new NodePatchDto
            {
                Id = id,
                Checked = checkedState,
                Cleared = BuildClearedFields((NodePatchField.Checked, checkedState is null)),
            };
        }

        if (property == AutomationElementIdentifiers.ItemStatusProperty)
        {
            var state = AutomationNodeSummaryBuilder.GetState(peer);
            return new NodePatchDto
            {
                Id = id,
                State = state,
                Cleared = BuildClearedFields((NodePatchField.State, state is null)),
            };
        }

        return null;
    }

    private static string[]? BuildClearedFields(params (string Field, bool ShouldClear)[] fields)
    {
        List<string>? cleared = null;

        foreach (var (field, shouldClear) in fields)
            AddClearedField(ref cleared, field, shouldClear);

        return cleared?.ToArray();
    }

    private static void AddClearedField(ref List<string>? cleared, string field, bool shouldClear)
    {
        if (!shouldClear)
            return;

        cleared ??= [];
        cleared.Add(field);
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
