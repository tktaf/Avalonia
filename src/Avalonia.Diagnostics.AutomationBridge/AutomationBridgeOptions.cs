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
    /// Gets the <see cref="Hosting.AutomationBridgeHostedService"/> created when
    /// <see cref="AutomationBridgeAppBuilderExtensions.WithDevAutomationBridge"/> was called.
    /// Null until <c>WithDevAutomationBridge</c> has been invoked on an <see cref="AppBuilder"/>.
    /// </summary>
    /// <remarks>
    /// Exposed so tests can observe activation state without reaching into production behaviour.
    /// The setter is <c>internal</c> to prevent external mutation.
    /// </remarks>
    public Hosting.AutomationBridgeHostedService? RegisteredService { get; internal set; }
}
