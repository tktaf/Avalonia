using Avalonia.Controls.ApplicationLifetimes;

namespace Avalonia.Diagnostics.AutomationBridge.Hosting;

internal static class AutomationBridgeLifetimeRegistration
{
    public static void RegisterStopOnExit(
        IAutomationBridgeHostedService service,
        IControlledApplicationLifetime? lifetime)
    {
        ArgumentNullException.ThrowIfNull(service);

        if (lifetime is null)
            return;

        EventHandler<ControlledApplicationLifetimeExitEventArgs>? exitHandler = null;
        exitHandler = (_, _) =>
        {
            lifetime.Exit -= exitHandler;
            service.Stop();
        };

        lifetime.Exit += exitHandler;
    }
}
