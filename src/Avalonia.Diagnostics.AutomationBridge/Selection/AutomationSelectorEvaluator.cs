using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia.Automation.Peers;
using Avalonia.AutomationBridge.Protocol.Messages;
using Avalonia.Diagnostics.AutomationBridge.Session;
using Avalonia.Diagnostics.AutomationBridge.Snapshot;

namespace Avalonia.Diagnostics.AutomationBridge.Selection;

/// <summary>
/// Resolves structured selectors over an automation subtree with deterministic ordering.
/// </summary>
public static class AutomationSelectorEvaluator
{
    /// <summary>
    /// Evaluates <paramref name="selector"/> within the root identified by <paramref name="rootId"/>.
    /// </summary>
    public static BridgeResponse Evaluate(
        AutomationBridgeSession session,
        string rootId,
        SelectorDto? selector,
        int maxResults = 1,
        string? requestId = null)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentException.ThrowIfNullOrEmpty(rootId);

        if (!session.TryGetRootPeer(rootId, out var rootPeer))
        {
            return BridgeResponse.Failure(
                BridgeErrorCode.RootNotFound,
                $"Root '{rootId}' was not found.",
                requestId);
        }

        selector ??= new SelectorDto();

        var scopePeer = rootPeer;
        var scopeHandle = selector.ContainerId ?? selector.Within;
        if (!string.IsNullOrEmpty(scopeHandle))
        {
            if (!session.TryGetPeer(scopeHandle, out var withinPeer)
                || !IsDescendantOrSelf(rootPeer, withinPeer))
            {
                return BridgeResponse.Failure(
                    BridgeErrorCode.NodeNotFound,
                    $"Node '{scopeHandle}' was not found within root '{rootId}'.",
                    requestId);
            }

            scopePeer = withinPeer;
        }

        var matches = EnumerateDepthFirst(scopePeer)
            .Where(peer => Matches(peer, scopePeer, selector))
            .ToList();

        if (selector.Nth is int nth)
        {
            if (nth < 0 || nth >= matches.Count)
            {
                return BridgeResponse.Failure(
                    BridgeErrorCode.NodeNotFound,
                    "No node matched the selector.",
                    requestId);
            }

            return BridgeResponse.WithNodes(
                NodeSummaryProjection.Apply(new[] { session.SummarizePeer(matches[nth]) }, selector.Fields),
                requestId);
        }

        if (matches.Count == 0)
        {
            return BridgeResponse.Failure(
                BridgeErrorCode.NodeNotFound,
                "No node matched the selector.",
                requestId);
        }

        var limit = Math.Max(1, maxResults);
        if (limit == 1 && matches.Count > 1)
        {
            return BridgeResponse.Failure(
                BridgeErrorCode.SelectorAmbiguous,
                "Selector matched more than one node; specify 'nth' or request more results.",
                requestId);
        }

        return BridgeResponse.WithNodes(
            NodeSummaryProjection.Apply(matches.Take(limit).Select(session.SummarizePeer).ToArray(), selector.Fields),
            requestId);
    }

    private static IEnumerable<AutomationPeer> EnumerateDepthFirst(AutomationPeer root)
    {
        yield return root;

        foreach (var child in TryGetChildren(root))
        {
            foreach (var descendant in EnumerateDepthFirst(child))
            {
                yield return descendant;
            }
        }
    }

    private static bool Matches(AutomationPeer peer, AutomationPeer scopeRoot, SelectorDto selector)
    {
        var automationId = selector.AutomationId ?? selector.Id;
        if (automationId is not null
            && !string.Equals(TryGetString(peer.GetAutomationId), automationId, StringComparison.Ordinal))
        {
            return false;
        }

        if (selector.Name is not null)
        {
            var peerName = TryGetString(peer.GetName);
            if (peerName is null)
                return false;

            if (selector.NameSubstring)
            {
                if (!peerName.Contains(selector.Name, StringComparison.OrdinalIgnoreCase))
                    return false;
            }
            else if (!string.Equals(peerName, selector.Name, StringComparison.Ordinal))
            {
                return false;
            }
        }

        if (selector.Role is not null
            && !string.Equals(
                AutomationNodeSummaryBuilder.TryGetRole(peer),
                selector.Role,
                StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (selector.ClassName is not null
            && !string.Equals(TryGetString(peer.GetClassName), selector.ClassName, StringComparison.Ordinal))
        {
            return false;
        }

        if (selector.Focused is bool focused
            && TryGetBool(peer.HasKeyboardFocus) != focused)
        {
            return false;
        }

        if (selector.Enabled is bool enabled
            && TryGetBool(peer.IsEnabled) != enabled)
        {
            return false;
        }

        if (selector.Selected is bool selected
            && AutomationNodeSummaryBuilder.GetSelected(peer) != selected)
        {
            return false;
        }

        if (selector.Visible is bool visible
            && TryGetBool(peer.IsOffscreen) != !visible)
        {
            return false;
        }

        if (selector.HasAction is not null
            && !AutomationNodeSummaryBuilder.GetActions(peer)
                .Contains(selector.HasAction, StringComparer.OrdinalIgnoreCase))
        {
            return false;
        }

        if (selector.Path is { Length: > 0 } path && !MatchesPath(peer, scopeRoot, path))
        {
            return false;
        }

        return true;
    }

    private static bool MatchesPath(AutomationPeer peer, AutomationPeer scopeRoot, IReadOnlyList<string> path)
    {
        var ancestors = new List<AutomationPeer>();
        var current = TryGetParent(peer);

        while (current is not null)
        {
            ancestors.Add(current);
            if (ReferenceEquals(current, scopeRoot))
                break;

            current = TryGetParent(current);
        }

        ancestors.Reverse();

        if (ancestors.Count < path.Count)
            return false;

        for (var i = 0; i < path.Count; i++)
        {
            if (!MatchesPathSegment(ancestors[i], path[i]))
                return false;
        }

        return true;
    }

    private static bool MatchesPathSegment(AutomationPeer peer, string segment)
    {
        return (TryGetString(peer.GetName)?.Contains(segment, StringComparison.OrdinalIgnoreCase) ?? false)
            || (AutomationNodeSummaryBuilder.TryGetRole(peer)?.Contains(segment, StringComparison.OrdinalIgnoreCase) ?? false);
    }

    private static string? TryGetString(Func<string?> getter)
    {
        try
        {
            return getter();
        }
        catch
        {
            return null;
        }
    }

    private static bool? TryGetBool(Func<bool> getter)
    {
        try
        {
            return getter();
        }
        catch
        {
            return null;
        }
    }

    private static bool IsDescendantOrSelf(AutomationPeer root, AutomationPeer peer)
    {
        var current = peer;

        while (current is not null)
        {
            if (ReferenceEquals(current, root))
                return true;

            current = TryGetParent(current);
        }

        return false;
    }

    private static IReadOnlyList<AutomationPeer> TryGetChildren(AutomationPeer peer)
    {
        try
        {
            return peer.GetChildren();
        }
        catch
        {
            return Array.Empty<AutomationPeer>();
        }
    }

    private static AutomationPeer? TryGetParent(AutomationPeer peer)
    {
        try
        {
            return peer.GetParent();
        }
        catch
        {
            return null;
        }
    }
}
