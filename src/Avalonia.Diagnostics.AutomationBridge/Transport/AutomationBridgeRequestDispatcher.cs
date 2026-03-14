using System;
using System.IO;
using Avalonia.Automation.Peers;
using Avalonia.AutomationBridge.Protocol.Messages;
using Avalonia.Controls;
using Avalonia.Diagnostics.AutomationBridge.Actions;
using Avalonia.Diagnostics.AutomationBridge.Selection;
using Avalonia.Diagnostics.AutomationBridge.Session;
using Avalonia.Media.Imaging;

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
            BridgeAction.Screenshot => Screenshot(session, request),
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

    private static BridgeResponse Screenshot(AutomationBridgeSession session, BridgeRequest request)
    {
        // Accept either nodeId or rootId — rootId captures the entire window.
        var handle = request.NodeId ?? request.RootId;
        if (string.IsNullOrEmpty(handle) || !session.TryGetPeer(handle, out var peer))
        {
            return BridgeResponse.Failure(
                BridgeErrorCode.NodeNotFound,
                $"Node '{handle}' was not found.",
                request.RequestId);
        }

        if (peer is not ControlAutomationPeer controlPeer)
        {
            return BridgeResponse.Failure(
                BridgeErrorCode.ActionNotSupported,
                "Screenshot requires a control-backed automation peer.",
                request.RequestId);
        }

        try
        {
            var control = controlPeer.Owner;
            var bounds = control.Bounds;
            var pixelSize = PixelSize.FromSize(bounds.Size, 1.0);

            if (pixelSize.Width <= 0 || pixelSize.Height <= 0)
            {
                return BridgeResponse.Failure(
                    BridgeErrorCode.ActionFailed,
                    "Control has zero or negative size.",
                    request.RequestId);
            }

            using var bitmap = new RenderTargetBitmap(pixelSize);
            bitmap.Render(control);

            using var ms = new MemoryStream();
            bitmap.Save(ms);
            var base64 = Convert.ToBase64String(ms.ToArray());

            return BridgeResponse.WithScreenshot(
                base64,
                new[] { bounds.X, bounds.Y, bounds.Width, bounds.Height },
                request.RequestId);
        }
        catch (Exception e)
        {
            return BridgeResponse.Failure(
                BridgeErrorCode.ActionFailed,
                $"Screenshot failed: {e.Message}",
                request.RequestId);
        }
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
