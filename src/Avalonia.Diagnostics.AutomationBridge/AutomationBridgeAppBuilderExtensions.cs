using Avalonia.Diagnostics.AutomationBridge.Hosting;

namespace Avalonia.Diagnostics.AutomationBridge;

/// <summary>
/// <see cref="AppBuilder"/> extension methods for the dev-only automation bridge.
/// </summary>
public static class AutomationBridgeAppBuilderExtensions
{
    /// <summary>
    /// Registers the dev automation bridge so that it starts when the
    /// <see cref="AppBuilder"/> setup phase completes.
    /// </summary>
    /// <param name="builder">The <see cref="AppBuilder"/> to configure.</param>
    /// <param name="options">
    /// Optional bridge options. When <see langword="null"/> a default
    /// <see cref="AutomationBridgeOptions"/> instance is used.
    /// </param>
    /// <returns>The same <see cref="AppBuilder"/> instance for fluent chaining.</returns>
    /// <remarks>
    /// The method is intentionally named <c>WithDevAutomationBridge</c> (not
    /// <c>WithAutomationBridge</c>) to signal that it is a development-time tool and must
    /// never be called in production builds without an explicit guard.
    /// </remarks>
    public static AppBuilder WithDevAutomationBridge(
        this AppBuilder builder,
        AutomationBridgeOptions? options = null)
    {
        var opts = options ?? new AutomationBridgeOptions();
        var service = new AutomationBridgeHostedService(opts);

        // Store back so callers (and tests) can reach the instance without
        // requiring a separate service-locator lookup.
        opts.RegisteredService = service;

        return builder.AfterSetup(_ => service.Start());
    }
}
