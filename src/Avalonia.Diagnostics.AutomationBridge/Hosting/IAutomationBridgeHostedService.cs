namespace Avalonia.Diagnostics.AutomationBridge.Hosting;

/// <summary>Minimal hosted-service contract exposed through bridge options and tests.</summary>
public interface IAutomationBridgeHostedService
{
    /// <summary>Gets the configured bridge options.</summary>
    AutomationBridgeOptions Options { get; }

    /// <summary>Gets whether the service is currently running.</summary>
    bool IsRunning { get; }

    /// <summary>Gets the bound loopback port, or 0 when stopped.</summary>
    int BoundPort { get; }

    /// <summary>Starts the hosted service.</summary>
    void Start();

    /// <summary>Stops the hosted service.</summary>
    void Stop();
}
