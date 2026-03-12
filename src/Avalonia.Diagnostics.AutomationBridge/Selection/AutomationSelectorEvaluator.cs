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
        if (!string.IsNullOrEmpty(selector.Within))
        {
            if (!session.TryGetPeer(selector.Within, out var withinPeer)
                || !IsDescendantOrSelf(rootPeer, withinPeer))
            {
                return BridgeResponse.Failure(
                    BridgeErrorCode.NodeNotFound,
                    $"Node '{selector.Within}' was not found within root '{rootId}'.",
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
                new[] { session.SummarizePeer(matches[nth]) },
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
            matches.Take(limit).Select(session.SummarizePeer).ToArray(),
            requestId);
    }

    private static IEnumerable<AutomationPeer> EnumerateDepthFirst(AutomationPeer root)
    {
        yield return root;

        foreach (var child in root.GetChildren())
        {
            foreach (var descendant in EnumerateDepthFirst(child))
            {
                yield return descendant;
            }
        }
    }

    private static bool Matches(AutomationPeer peer, AutomationPeer scopeRoot, SelectorDto selector)
    {
        if (selector.Id is not null
            && !string.Equals(peer.GetAutomationId(), selector.Id, StringComparison.Ordinal))
        {
            return false;
        }

        if (selector.Name is not null)
        {
            var peerName = peer.GetName();
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
                AutomationNodeSummaryBuilder.GetRole(peer),
                selector.Role,
                StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (selector.ClassName is not null
            && !string.Equals(peer.GetClassName(), selector.ClassName, StringComparison.Ordinal))
        {
            return false;
        }

        if (selector.Focused is bool focused && peer.HasKeyboardFocus() != focused)
        {
            return false;
        }

        if (selector.Enabled is bool enabled && peer.IsEnabled() != enabled)
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
        var current = peer.GetParent();

        while (current is not null)
        {
            ancestors.Add(current);
            if (ReferenceEquals(current, scopeRoot))
                break;

            current = current.GetParent();
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
        return peer.GetName().Contains(segment, StringComparison.OrdinalIgnoreCase)
            || AutomationNodeSummaryBuilder.GetRole(peer)
                .Contains(segment, StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsDescendantOrSelf(AutomationPeer root, AutomationPeer peer)
    {
        var current = peer;

        while (current is not null)
        {
            if (ReferenceEquals(current, root))
                return true;

            current = current.GetParent();
        }

        return false;
    }
}
