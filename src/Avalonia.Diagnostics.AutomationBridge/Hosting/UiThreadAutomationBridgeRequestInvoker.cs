using Avalonia.AutomationBridge.Protocol.Messages;
using Avalonia.Threading;

namespace Avalonia.Diagnostics.AutomationBridge.Hosting;

internal sealed class UiThreadAutomationBridgeRequestInvoker : IAutomationBridgeRequestInvoker
{
    private readonly Func<bool> _checkAccess;
    private readonly Func<Func<BridgeResponse>, BridgeResponse> _invoke;

    public UiThreadAutomationBridgeRequestInvoker()
        : this(
            checkAccess: () => Dispatcher.UIThread.CheckAccess(),
            invoke: callback => Dispatcher.UIThread.Invoke(callback))
    {
    }

    internal UiThreadAutomationBridgeRequestInvoker(
        Func<bool> checkAccess,
        Func<Func<BridgeResponse>, BridgeResponse> invoke)
    {
        _checkAccess = checkAccess ?? throw new ArgumentNullException(nameof(checkAccess));
        _invoke = invoke ?? throw new ArgumentNullException(nameof(invoke));
    }

    public BridgeResponse Invoke(Func<BridgeResponse> callback)
    {
        ArgumentNullException.ThrowIfNull(callback);

        return _checkAccess()
            ? callback()
            : _invoke(callback);
    }
}
