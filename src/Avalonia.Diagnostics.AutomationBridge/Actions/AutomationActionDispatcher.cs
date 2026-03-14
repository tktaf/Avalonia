using System;
using Avalonia.Automation;
using Avalonia.Automation.Provider;
using Avalonia.AutomationBridge.Protocol.Messages;
using Avalonia.Diagnostics.AutomationBridge.Session;

namespace Avalonia.Diagnostics.AutomationBridge.Actions;

/// <summary>
/// Dispatches bridge actions onto provider-backed automation interfaces.
/// </summary>
public static class AutomationActionDispatcher
{
    public static BridgeResponse Dispatch(AutomationBridgeSession session, BridgeRequest request)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(request);

        if (string.IsNullOrEmpty(request.NodeId)
            || !session.TryGetPeer(request.NodeId, out var peer))
        {
            return BridgeResponse.Failure(
                BridgeErrorCode.NodeNotFound,
                $"Node '{request.NodeId}' was not found.",
                request.RequestId);
        }

        try
        {
            if (!peer.IsEnabled())
            {
                return BridgeResponse.Failure(
                    BridgeErrorCode.ElementNotEnabled,
                    $"Node '{request.NodeId}' is disabled.",
                    request.RequestId);
            }

            var deltaBuilder = session.GetOrCreateDeltaBuilderForPeer(peer);
            var startingRevision = deltaBuilder.CurrentRevision;
            deltaBuilder.BeginActionCapture(startingRevision);
            var beforeActionSummary = session.SummarizePeer(peer);

            var response = request.Action switch
            {
                BridgeAction.Invoke => Invoke(peer, request),
                BridgeAction.SetValue => SetValue(peer, request),
                BridgeAction.Toggle => Toggle(peer, request),
                BridgeAction.Select => Select(peer, request),
                BridgeAction.Expand => Expand(peer, request),
                BridgeAction.Collapse => Collapse(peer, request),
                BridgeAction.SetFocus => SetFocus(peer, request),
                BridgeAction.ShowContextMenu => ShowContextMenu(peer, request),
                BridgeAction.Scroll => Scroll(peer, request),
                BridgeAction.SetScrollPercent => SetScrollPercent(peer, request),
                _ => Unsupported(request, $"Action '{request.Action}' is not supported."),
            };

            if (!response.Ok)
            {
                deltaBuilder.EndActionCapture();
                return response;
            }

            var delta = deltaBuilder.CompleteAction(peer, startingRevision);
            if (delta.Revision == startingRevision)
                delta = deltaBuilder.ReconcileActionState(peer, beforeActionSummary);

            var completionState = delta.Revision == startingRevision
                ? BridgeActionCompletionState.Accepted
                : BridgeActionCompletionState.Completed;

            return BridgeResponse.WithCompletion(delta, completionState, request.RequestId);
        }
        catch (Exception e)
        {
            session.GetOrCreateDeltaBuilderForPeer(peer).EndActionCapture();

            return BridgeResponse.Failure(
                BridgeErrorCode.ActionFailed,
                $"Action '{request.Action}' failed: {e.Message}",
                request.RequestId);
        }
    }

    private static BridgeResponse Invoke(Avalonia.Automation.Peers.AutomationPeer peer, BridgeRequest request)
    {
        var provider = peer.GetProvider<IInvokeProvider>();
        if (provider is not null)
        {
            provider.Invoke();
            return BridgeResponse.Success(request.RequestId);
        }

        // Fall back to selection for nodes like ListBoxItem that support
        // ISelectionItemProvider but not IInvokeProvider.  When a caller
        // "invokes" a list item, the intuitive expectation is selection.
        var selectionProvider = peer.GetProvider<ISelectionItemProvider>();
        if (selectionProvider is not null)
        {
            selectionProvider.Select();
            return BridgeResponse.Success(request.RequestId);
        }

        return Unsupported(request, "Invoke is not supported for this node.");
    }

    private static BridgeResponse SetValue(Avalonia.Automation.Peers.AutomationPeer peer, BridgeRequest request)
    {
        var provider = peer.GetProvider<IValueProvider>();
        if (provider is null || provider.IsReadOnly)
            return Unsupported(request, "SetValue is not supported for this node.");

        provider.SetValue(request.Value);
        return BridgeResponse.Success(request.RequestId);
    }

    private static BridgeResponse Toggle(Avalonia.Automation.Peers.AutomationPeer peer, BridgeRequest request)
    {
        var provider = peer.GetProvider<IToggleProvider>();
        if (provider is null)
            return Unsupported(request, "Toggle is not supported for this node.");

        provider.Toggle();
        return BridgeResponse.Success(request.RequestId);
    }

    private static BridgeResponse Select(Avalonia.Automation.Peers.AutomationPeer peer, BridgeRequest request)
    {
        var provider = peer.GetProvider<ISelectionItemProvider>();
        if (provider is null)
            return Unsupported(request, "Select is not supported for this node.");

        provider.Select();
        return BridgeResponse.Success(request.RequestId);
    }

    private static BridgeResponse Expand(Avalonia.Automation.Peers.AutomationPeer peer, BridgeRequest request)
    {
        var provider = peer.GetProvider<IExpandCollapseProvider>();
        if (provider is null)
            return Unsupported(request, "Expand is not supported for this node.");

        var state = GetExpandCollapseState(provider, request, BridgeAction.Expand);
        if (!state.Ok)
            return state.Response!;

        if (state.Value == Avalonia.Automation.ExpandCollapseState.LeafNode)
            return Unsupported(request, "Expand is not supported for this node.");

        provider.Expand();
        return BridgeResponse.Success(request.RequestId);
    }

    private static BridgeResponse Collapse(Avalonia.Automation.Peers.AutomationPeer peer, BridgeRequest request)
    {
        var provider = peer.GetProvider<IExpandCollapseProvider>();
        if (provider is null)
            return Unsupported(request, "Collapse is not supported for this node.");

        var state = GetExpandCollapseState(provider, request, BridgeAction.Collapse);
        if (!state.Ok)
            return state.Response!;

        if (state.Value == Avalonia.Automation.ExpandCollapseState.LeafNode)
            return Unsupported(request, "Collapse is not supported for this node.");

        provider.Collapse();
        return BridgeResponse.Success(request.RequestId);
    }

    private static BridgeResponse SetFocus(Avalonia.Automation.Peers.AutomationPeer peer, BridgeRequest request)
    {
        if (!peer.IsKeyboardFocusable())
            return Unsupported(request, "SetFocus is not supported for this node.");

        peer.SetFocus();
        return BridgeResponse.Success(request.RequestId);
    }

    private static BridgeResponse ShowContextMenu(Avalonia.Automation.Peers.AutomationPeer peer, BridgeRequest request)
    {
        if (!peer.ShowContextMenu())
            return Unsupported(request, "ShowContextMenu is not supported for this node.");

        return BridgeResponse.Success(request.RequestId);
    }

    private static BridgeResponse Scroll(Avalonia.Automation.Peers.AutomationPeer peer, BridgeRequest request)
    {
        var provider = peer.GetProvider<IScrollProvider>();
        if (provider is null)
            return Unsupported(request, "Scroll is not supported for this node.");

        provider.Scroll(
            ParseScrollAmount(request.HorizontalAmount),
            ParseScrollAmount(request.VerticalAmount));

        return BridgeResponse.Success(request.RequestId);
    }

    private static BridgeResponse SetScrollPercent(Avalonia.Automation.Peers.AutomationPeer peer, BridgeRequest request)
    {
        var provider = peer.GetProvider<IScrollProvider>();
        if (provider is null)
            return Unsupported(request, "SetScrollPercent is not supported for this node.");

        provider.SetScrollPercent(
            request.HorizontalPercent ?? ScrollPatternIdentifiers.NoScroll,
            request.VerticalPercent ?? ScrollPatternIdentifiers.NoScroll);

        return BridgeResponse.Success(request.RequestId);
    }

    private static ScrollAmount ParseScrollAmount(string? value)
    {
        return value?.ToLowerInvariant() switch
        {
            "largedecrement" => ScrollAmount.LargeDecrement,
            "smalldecrement" => ScrollAmount.SmallDecrement,
            "largeincrement" => ScrollAmount.LargeIncrement,
            "smallincrement" => ScrollAmount.SmallIncrement,
            null => ScrollAmount.NoAmount,
            "" => ScrollAmount.NoAmount,
            _ => ScrollAmount.NoAmount,
        };
    }

    private static ExpandCollapseStateResult GetExpandCollapseState(
        IExpandCollapseProvider provider,
        BridgeRequest request,
        string action)
    {
        try
        {
            return new ExpandCollapseStateResult(provider.ExpandCollapseState, null);
        }
        catch (Exception e)
        {
            return new ExpandCollapseStateResult(
                null,
                BridgeResponse.Failure(
                    BridgeErrorCode.ActionFailed,
                    $"Action '{action}' failed: {e.Message}",
                    request.RequestId));
        }
    }

    private static BridgeResponse Unsupported(BridgeRequest request, string message)
        => BridgeResponse.Failure(BridgeErrorCode.ActionNotSupported, message, request.RequestId);

    private sealed record ExpandCollapseStateResult(
        Avalonia.Automation.ExpandCollapseState? Value,
        BridgeResponse? Response)
    {
        public bool Ok => Response is null;
    }
}
