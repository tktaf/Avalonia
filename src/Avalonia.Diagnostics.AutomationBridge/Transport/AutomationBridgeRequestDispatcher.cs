using System;
using Avalonia.AutomationBridge.Protocol.Messages;
using Avalonia.Diagnostics.AutomationBridge.Actions;
using Avalonia.Diagnostics.AutomationBridge.Selection;
using Avalonia.Diagnostics.AutomationBridge.Session;

namespace Avalonia.Diagnostics.AutomationBridge.Transport;

/// <summary>
/// Dispatches protocol requests onto an in-process automation session.
/// </summary>
public static class AutomationBridgeRequestDispatcher
{
    public static BridgeResponse Dispatch(AutomationBridgeSession session, BridgeRequest request)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(request);

        return request.Action switch
        {
            BridgeAction.Roots => BridgeResponse.WithNodes(session.GetRoots().ToArray(), request.RequestId),
            BridgeAction.Describe => Describe(session, request),
            BridgeAction.Query => Query(session, request),
            BridgeAction.Watch => Watch(session, request),
            _ => AutomationActionDispatcher.Dispatch(session, request),
        };
    }

    private static BridgeResponse Describe(AutomationBridgeSession session, BridgeRequest request)
    {
        if (string.IsNullOrEmpty(request.NodeId))
        {
            return BridgeResponse.Failure(
                BridgeErrorCode.NodeNotFound,
                "A node handle is required for describe requests.",
                request.RequestId);
        }

        var summary = session.DescribeNode(request.NodeId);
        return summary is null
            ? BridgeResponse.Failure(
                BridgeErrorCode.NodeNotFound,
                $"Node '{request.NodeId}' was not found.",
                request.RequestId)
            : BridgeResponse.WithNodes(new[] { summary }, request.RequestId);
    }

    private static BridgeResponse Query(AutomationBridgeSession session, BridgeRequest request)
    {
        if (string.IsNullOrEmpty(request.RootId))
        {
            return BridgeResponse.Failure(
                BridgeErrorCode.RootNotFound,
                "A root handle is required for query requests.",
                request.RequestId);
        }

        return AutomationSelectorEvaluator.Evaluate(
            session,
            request.RootId,
            request.Selector,
            request.MaxResults ?? 1,
            request.RequestId);
    }

    private static BridgeResponse Watch(AutomationBridgeSession session, BridgeRequest request)
    {
        if (string.IsNullOrEmpty(request.RootId))
        {
            return BridgeResponse.Failure(
                BridgeErrorCode.RootNotFound,
                "A root handle is required for watch requests.",
                request.RequestId);
        }

        try
        {
            return session
                .GetOrCreateDeltaBuilder(request.RootId)
                .GetDelta(request.SinceRevision, request.RequestId);
        }
        catch (InvalidOperationException)
        {
            return BridgeResponse.Failure(
                BridgeErrorCode.RootNotFound,
                $"Root '{request.RootId}' was not found.",
                request.RequestId);
        }
    }
}
