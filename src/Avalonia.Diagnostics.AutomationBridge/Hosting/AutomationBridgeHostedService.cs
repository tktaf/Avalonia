namespace Avalonia.Diagnostics.AutomationBridge.Hosting;

/// <summary>
/// Skeleton hosted service for the dev-only automation bridge.
/// </summary>
/// <remarks>
/// <para>
/// This service is intentionally minimal: it tracks whether it has been started so that
/// tests and diagnostics can confirm activation without exposing production behaviour.
/// </para>
/// <para>
/// Future tasks will add the actual TCP/WebSocket listener, request dispatch, and
/// selector/action pipeline inside <see cref="Start"/> / <see cref="Stop"/>.
/// </para>
/// </remarks>
public sealed class AutomationBridgeHostedService
{
    private readonly AutomationBridgeOptions _options;
    private volatile bool _isRunning;

    /// <summary>Initializes a new instance with the supplied options.</summary>
    /// <param name="options">Bridge configuration; must not be null.</param>
    public AutomationBridgeHostedService(AutomationBridgeOptions options)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
    }

    /// <summary>Gets the configured options.</summary>
    public AutomationBridgeOptions Options => _options;

    /// <summary>
    /// Gets a value indicating whether the service has been started and has not yet been stopped.
    /// </summary>
    public bool IsRunning => _isRunning;

    /// <summary>
    /// Starts the bridge service.
    /// </summary>
    /// <remarks>
    /// Idempotent: calling <c>Start</c> on an already-running service is a no-op.
    /// TODO (subsequent tasks): open TCP listener on <see cref="AutomationBridgeOptions.Port"/>.
    /// </remarks>
    public void Start()
    {
        if (_isRunning)
            return;

        _isRunning = true;
        // TODO: start actual listener in subsequent tasks.
    }

    /// <summary>
    /// Stops the bridge service.
    /// </summary>
    /// <remarks>
    /// Idempotent: calling <c>Stop</c> on an already-stopped service is a no-op.
    /// TODO (subsequent tasks): close TCP listener and active sessions.
    /// </remarks>
    public void Stop()
    {
        if (!_isRunning)
            return;

        _isRunning = false;
        // TODO: teardown in subsequent tasks.
    }
}
