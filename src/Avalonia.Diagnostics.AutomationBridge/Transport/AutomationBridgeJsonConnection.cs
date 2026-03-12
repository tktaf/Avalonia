using System;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.AutomationBridge.Protocol.Messages;

namespace Avalonia.Diagnostics.AutomationBridge.Transport;

internal static class AutomationBridgeJsonConnection
{
    public static async Task ProcessAsync(
        Stream stream,
        Func<BridgeRequest, BridgeResponse> dispatcher,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(stream);
        ArgumentNullException.ThrowIfNull(dispatcher);

        using var reader = new StreamReader(stream, leaveOpen: true);
        using var writer = new StreamWriter(stream, leaveOpen: true) { AutoFlush = true };

        while (!cancellationToken.IsCancellationRequested)
        {
            var line = await reader.ReadLineAsync(cancellationToken).ConfigureAwait(false);
            if (line is null)
                break;

            var response = Dispatch(dispatcher, line);
            await writer.WriteLineAsync(AutomationBridgeJsonSerializer.SerializeResponse(response))
                .ConfigureAwait(false);
        }
    }

    private static BridgeResponse Dispatch(Func<BridgeRequest, BridgeResponse> dispatcher, string json)
    {
        try
        {
            var request = AutomationBridgeJsonSerializer.DeserializeRequest(json);
            return dispatcher(request);
        }
        catch (JsonException e)
        {
            return BridgeResponse.Failure(BridgeErrorCode.InvalidRequest, e.Message);
        }
        catch (Exception e)
        {
            return BridgeResponse.Failure(BridgeErrorCode.InternalError, e.Message);
        }
    }
}
