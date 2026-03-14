using Avalonia.AutomationBridge.Protocol.Messages;

namespace Avalonia.Diagnostics.AutomationBridge.Hosting;

internal interface IAutomationBridgeRequestInvoker
{
    BridgeResponse Invoke(Func<BridgeResponse> callback);
}
