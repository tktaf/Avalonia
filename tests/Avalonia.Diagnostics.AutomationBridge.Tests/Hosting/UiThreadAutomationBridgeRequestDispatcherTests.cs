using Avalonia.AutomationBridge.Protocol.Messages;
using Avalonia.Diagnostics.AutomationBridge.Hosting;
using Xunit;

namespace Avalonia.Diagnostics.AutomationBridge.Tests.Hosting;

public sealed class UiThreadAutomationBridgeRequestInvokerTests
{
    [Fact]
    public void Invoke_RunsInline_WhenAlreadyOnUiThread()
    {
        var dispatcher = new UiThreadAutomationBridgeRequestInvoker(
            checkAccess: () => true,
            invoke: _ => throw new InvalidOperationException("dispatcher invoke should not run"));

        var response = dispatcher.Invoke(() => BridgeResponse.Success("inline"));

        Assert.True(response.Ok);
        Assert.Equal("inline", response.RequestId);
    }

    [Fact]
    public void Invoke_UsesDispatcher_WhenNotOnUiThread()
    {
        var invokeCallCount = 0;
        var dispatcher = new UiThreadAutomationBridgeRequestInvoker(
            checkAccess: () => false,
            invoke: callback =>
            {
                invokeCallCount++;
                return callback();
            });

        var response = dispatcher.Invoke(() => BridgeResponse.Success("marshaled"));

        Assert.True(response.Ok);
        Assert.Equal("marshaled", response.RequestId);
        Assert.Equal(1, invokeCallCount);
    }
}
