using System.Net;
using System.Net.Sockets;
using Avalonia.AutomationBridge.Protocol.Messages;
using Avalonia.Diagnostics.AutomationBridge.Session;
using Avalonia.Diagnostics.AutomationBridge.Transport;

namespace Avalonia.Diagnostics.AutomationBridge.Hosting;

/// <summary>
/// Hosted service for the dev-only automation bridge.
/// </summary>
/// <remarks>
/// <para>
/// The service owns the loopback listener, a shared <see cref="AutomationBridgeSession"/>,
/// and the JSON request pipeline used by the local CLI.
/// </para>
/// <para>
/// A single session is shared across client connections so node handles remain valid across
/// separate CLI invocations while the service stays running.
/// </para>
/// </remarks>
public sealed class AutomationBridgeHostedService : IDisposable
{
    private readonly AutomationBridgeOptions _options;
    private readonly object _sessionGate = new();
    private CancellationTokenSource? _stopTokenSource;
    private TcpListener? _listener;
    private Task? _acceptLoop;
    private AutomationBridgeSession? _session;
    // 0 = stopped, 1 = running.  All transitions are done via Interlocked to
    // ensure atomicity even if Start/Stop are called concurrently.
    private int _runState;
    private int _boundPort;

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
    public bool IsRunning => Volatile.Read(ref _runState) == 1;

    /// <summary>Gets the loopback port currently bound by the listener, or 0 when stopped.</summary>
    public int BoundPort => Volatile.Read(ref _boundPort);

    /// <summary>
    /// Starts the bridge service.
    /// </summary>
    /// <remarks>
    /// Idempotent and thread-safe: calling <c>Start</c> on an already-running service is a no-op.
    /// </remarks>
    public void Start()
    {
        if (Interlocked.CompareExchange(ref _runState, 1, 0) != 0)
            return;

        try
        {
            _listener = new TcpListener(IPAddress.Loopback, _options.Port);
            _listener.Start();
            Volatile.Write(ref _boundPort, ((IPEndPoint)_listener.LocalEndpoint).Port);
            _session = CreateSession();
            _stopTokenSource = new CancellationTokenSource();
            _acceptLoop = AcceptLoopAsync(_stopTokenSource.Token);
        }
        catch
        {
            Interlocked.Exchange(ref _runState, 0);
            Volatile.Write(ref _boundPort, 0);
            _listener?.Stop();
            _listener = null;
            _session?.Dispose();
            _session = null;
            _stopTokenSource?.Dispose();
            _stopTokenSource = null;
            throw;
        }
    }

    /// <summary>
    /// Stops the bridge service.
    /// </summary>
    /// <remarks>
    /// Idempotent and thread-safe: calling <c>Stop</c> on an already-stopped service is a no-op.
    /// </remarks>
    public void Stop()
    {
        if (Interlocked.CompareExchange(ref _runState, 0, 1) != 1)
            return;

        Volatile.Write(ref _boundPort, 0);
        _stopTokenSource?.Cancel();
        _listener?.Stop();

        try
        {
            _acceptLoop?.GetAwaiter().GetResult();
        }
        catch (OperationCanceledException)
        {
        }

        _acceptLoop = null;
        _listener = null;
        _session?.Dispose();
        _session = null;
        _stopTokenSource?.Dispose();
        _stopTokenSource = null;
    }

    public void Dispose()
    {
        Stop();
        GC.SuppressFinalize(this);
    }

    private async Task AcceptLoopAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            TcpClient client;

            try
            {
                client = await _listener!.AcceptTcpClientAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (ObjectDisposedException)
            {
                break;
            }
            catch (SocketException) when (cancellationToken.IsCancellationRequested)
            {
                break;
            }

            _ = HandleClientAsync(client, cancellationToken);
        }
    }

    private async Task HandleClientAsync(TcpClient client, CancellationToken cancellationToken)
    {
        using var _ = client;
        using var stream = client.GetStream();
        await AutomationBridgeJsonConnection.ProcessAsync(stream, Dispatch, cancellationToken)
            .ConfigureAwait(false);
    }

    private AutomationBridgeSession CreateSession()
    {
        var peerSource = _options.PeerSourceFactory is null
            ? new AutomationRootRegistry()
            : new AutomationRootRegistry(_options.PeerSourceFactory);

        return new AutomationBridgeSession(peerSource);
    }

    private BridgeResponse Dispatch(BridgeRequest request)
    {
        lock (_sessionGate)
        {
            return AutomationBridgeRequestDispatcher.Dispatch(
                _session ?? throw new ObjectDisposedException(nameof(AutomationBridgeHostedService)),
                request);
        }
    }
}
