using Avalonia.Diagnostics.AutomationBridge;
using Avalonia.Diagnostics.AutomationBridge.Hosting;
using Avalonia.Controls.ApplicationLifetimes;
using Xunit;

namespace Avalonia.Diagnostics.AutomationBridge.Tests.Hosting;

/// <summary>
/// Proves that the bridge activates only when
/// <see cref="AutomationBridgeAppBuilderExtensions.WithDevAutomationBridge"/> is called
/// and that it can coexist with other <see cref="AppBuilder"/> customisation.
/// </summary>
/// <remarks>
/// <para>
/// Tests that need to trigger the AfterSetup phase invoke
/// <c>builder.AfterSetupCallback(builder)</c> rather than <c>AppBuilder.Setup()</c>.
/// Full platform initialisation (windowing, rendering, text-shaping subsystems) is not
/// available in this unit-test environment, making <c>Setup()</c> infeasible without a
/// heavyweight dependency on Avalonia.Headless and its Skia/HarfBuzz transitive chain.
/// <c>AfterSetupCallback</c> is the public surface the builder exposes for exactly this
/// kind of targeted hook testing.
/// </para>
/// </remarks>
public sealed class ActivationTests
{
    // ---------------------------------------------------------------------------
    // Registration guard
    // ---------------------------------------------------------------------------

    [Fact]
    public void WithDevAutomationBridge_RegistersService_ViaCallback()
    {
        IAutomationBridgeHostedService? capturedService = null;
        var opts = new AutomationBridgeOptions
        {
            Port = 0,
            OnServiceRegistered = svc => capturedService = svc
        };

        AppBuilder.Configure<StubApplication>()
            .WithDevAutomationBridge(opts);

        // The service must be created as soon as the extension is called —
        // before AfterSetup fires — so callers can inspect the instance.
        Assert.NotNull(capturedService);
    }

    [Fact]
    public void WithDevAutomationBridge_ServiceNotRunning_BeforeSetupCompletes()
    {
        IAutomationBridgeHostedService? capturedService = null;
        var opts = new AutomationBridgeOptions
        {
            Port = 0,
            OnServiceRegistered = svc => capturedService = svc
        };

        AppBuilder.Configure<StubApplication>()
            .WithDevAutomationBridge(opts);

        // AfterSetupCallback has not been invoked yet; service must be dormant.
        Assert.False(capturedService!.IsRunning);
    }

    // ---------------------------------------------------------------------------
    // Activation
    // ---------------------------------------------------------------------------

    [Fact]
    public void WithDevAutomationBridge_ServiceStarts_WhenAfterSetupFires()
    {
        IAutomationBridgeHostedService? capturedService = null;
        var opts = new AutomationBridgeOptions
        {
            Port = 0,
            OnServiceRegistered = svc => capturedService = svc
        };

        var builder = AppBuilder.Configure<StubApplication>()
            .WithDevAutomationBridge(opts);

        // Simulate the AfterSetup phase that runs during AppBuilder.Setup().
        builder.AfterSetupCallback(builder);

        Assert.True(capturedService!.IsRunning);
        capturedService.Stop();
    }

    [Fact]
    public void WithDevAutomationBridge_ServiceRespectsSuppliedPort()
    {
        IAutomationBridgeHostedService? capturedService = null;
        var opts = new AutomationBridgeOptions
        {
            Port = 19317,
            OnServiceRegistered = svc => capturedService = svc
        };

        AppBuilder.Configure<StubApplication>()
            .WithDevAutomationBridge(opts);

        Assert.Equal(19317, capturedService!.Options.Port);
    }

    // ---------------------------------------------------------------------------
    // Non-activation guard
    // ---------------------------------------------------------------------------

    [Fact]
    public void WithoutWithDevAutomationBridge_NoServiceCreated()
    {
        IAutomationBridgeHostedService? capturedService = null;
        var opts = new AutomationBridgeOptions
        {
            Port = 0,
            OnServiceRegistered = svc => capturedService = svc
        };

        // A builder that never had WithDevAutomationBridge called.
        _ = AppBuilder.Configure<StubApplication>();

        // OnServiceRegistered is never invoked unless WithDevAutomationBridge is called.
        Assert.Null(capturedService);
    }

    // ---------------------------------------------------------------------------
    // Coexistence
    // ---------------------------------------------------------------------------

    [Fact]
    public void WithDevAutomationBridge_CoexistsWith_OtherAfterSetupCallbacks()
    {
        IAutomationBridgeHostedService? capturedService = null;
        var opts = new AutomationBridgeOptions
        {
            Port = 0,
            OnServiceRegistered = svc => capturedService = svc
        };
        var otherCallbackFired = false;

        var builder = AppBuilder.Configure<StubApplication>()
            .AfterSetup(_ => otherCallbackFired = true)
            .WithDevAutomationBridge(opts);

        builder.AfterSetupCallback(builder);

        Assert.True(otherCallbackFired, "pre-existing AfterSetup callback must still fire");
        Assert.True(capturedService!.IsRunning, "bridge service must still start");
        capturedService.Stop();
    }

    [Fact]
    public void WithDevAutomationBridge_AfterOtherAfterSetup_BothFire()
    {
        IAutomationBridgeHostedService? capturedService = null;
        var opts = new AutomationBridgeOptions
        {
            Port = 0,
            OnServiceRegistered = svc => capturedService = svc
        };
        var otherCallbackFired = false;

        var builder = AppBuilder.Configure<StubApplication>()
            .WithDevAutomationBridge(opts)
            .AfterSetup(_ => otherCallbackFired = true);

        builder.AfterSetupCallback(builder);

        Assert.True(capturedService!.IsRunning);
        Assert.True(otherCallbackFired);
        capturedService.Stop();
    }

    [Fact]
    public void WithDevAutomationBridge_DefaultOptions_UsesDefaultPort()
    {
        IAutomationBridgeHostedService? capturedService = null;
        // Use an explicit options instance (with all defaults) to capture the service.
        var opts = new AutomationBridgeOptions
        {
            OnServiceRegistered = svc => capturedService = svc
        };

        AppBuilder.Configure<StubApplication>()
            .WithDevAutomationBridge(opts);

        // Default port is 9317 per AutomationBridgeOptions.
        Assert.Equal(9317, capturedService!.Options.Port);
    }

    [Fact]
    public void RegisterStopOnExit_StopsService_WhenLifetimeExits()
    {
        var lifetime = new StubControlledApplicationLifetime();
        using var service = new AutomationBridgeHostedService(new AutomationBridgeOptions { Port = 0 });
        service.Start();

        AutomationBridgeLifetimeRegistration.RegisterStopOnExit(service, lifetime);
        lifetime.RaiseExit();

        Assert.False(service.IsRunning);
    }

    // ---------------------------------------------------------------------------
    // Idempotency of the service itself
    // ---------------------------------------------------------------------------

    [Fact]
    public void Start_IsIdempotent()
    {
        var opts = new AutomationBridgeOptions { Port = 0 };
        using var service = new AutomationBridgeHostedService(opts);

        service.Start();
        service.Start(); // second call must be a no-op

        Assert.True(service.IsRunning);
    }

    [Fact]
    public void Stop_IsIdempotent()
    {
        var opts = new AutomationBridgeOptions { Port = 0 };
        using var service = new AutomationBridgeHostedService(opts);

        service.Start();
        service.Stop();
        service.Stop(); // second call must be a no-op

        Assert.False(service.IsRunning);
    }

    [Fact]
    public void Service_CanBeStopped_AfterStart()
    {
        var opts = new AutomationBridgeOptions { Port = 0 };
        using var service = new AutomationBridgeHostedService(opts);

        service.Start();
        Assert.True(service.IsRunning);

        service.Stop();
        Assert.False(service.IsRunning);
    }

    // ---------------------------------------------------------------------------
    // Helpers
    // ---------------------------------------------------------------------------

    /// <summary>
    /// Minimal <see cref="Application"/> subclass that requires no platform services.
    /// Used only to satisfy the <c>TApp : Application, new()</c> constraint on
    /// <see cref="AppBuilder.Configure{TApp}()"/>.
    /// </summary>
    private sealed class StubApplication : Application { }

#pragma warning disable CS0067
    private sealed class StubControlledApplicationLifetime : IControlledApplicationLifetime
    {
        public event EventHandler<ControlledApplicationLifetimeStartupEventArgs>? Startup;
        public event EventHandler<ControlledApplicationLifetimeExitEventArgs>? Exit;

        public void Shutdown(int exitCode = 0)
        {
        }

        public void RaiseExit() => Exit?.Invoke(this, new ControlledApplicationLifetimeExitEventArgs(0));
    }
#pragma warning restore CS0067
}
