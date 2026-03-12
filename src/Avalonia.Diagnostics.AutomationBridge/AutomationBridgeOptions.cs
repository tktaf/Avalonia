namespace Avalonia.Diagnostics.AutomationBridge;

/// <summary>
/// Configuration options for the dev-only automation bridge.
/// </summary>
public sealed class AutomationBridgeOptions
{
    /// <summary>
    /// TCP port the bridge will listen on when started.
    /// Defaults to 9317.
    /// </summary>
    public int Port { get; set; } = 9317;

    /// <summary>
    /// Optional callback invoked by
    /// <see cref="AutomationBridgeAppBuilderExtensions.WithDevAutomationBridge"/> immediately
    /// after the hosted-service instance is created (before <c>AfterSetup</c> fires).
    /// </summary>
    /// <remarks>
    /// Intended for tests and diagnostics that need to capture the service instance without
    /// creating a circular dependency between the options type and the service type.
    /// Production code rarely needs this hook.
    /// </remarks>
    public Action<Hosting.AutomationBridgeHostedService>? OnServiceRegistered { get; set; }
}
