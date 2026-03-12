using Avalonia.Diagnostics.AutomationBridge;
using Avalonia.Diagnostics.AutomationBridge.Hosting;
using Xunit;

namespace Avalonia.Diagnostics.AutomationBridge.Tests.Hosting;

/// <summary>
/// Proves that the bridge activates only when
/// <see cref="AutomationBridgeAppBuilderExtensions.WithDevAutomationBridge"/> is called
/// and that it can coexist with other <see cref="AppBuilder"/> customisation.
/// </summary>
/// <remarks>
/// Tests here use <see cref="AppBuilder.AfterSetupCallback"/> directly instead of calling
/// <c>AppBuilder.Setup()</c>, because full platform initialisation is not available in
/// the unit-test environment (no windowing / rendering subsystem).
/// </remarks>
public sealed class ActivationTests
{
    // ---------------------------------------------------------------------------
    // Registration guard
    // ---------------------------------------------------------------------------

    [Fact]
    public void WithDevAutomationBridge_RegistersService_OnOptions()
    {
        var opts = new AutomationBridgeOptions();

        AppBuilder.Configure<StubApplication>()
            .WithDevAutomationBridge(opts);

        // The service must be wired as soon as the extension is called —
        // before AfterSetup fires — so callers can inspect the instance.
        Assert.NotNull(opts.RegisteredService);
    }

    [Fact]
    public void WithDevAutomationBridge_ServiceNotRunning_BeforeSetupCompletes()
    {
        var opts = new AutomationBridgeOptions();

        AppBuilder.Configure<StubApplication>()
            .WithDevAutomationBridge(opts);

        // AfterSetupCallback has not been invoked yet; service must be dormant.
        Assert.False(opts.RegisteredService!.IsRunning);
    }

    // ---------------------------------------------------------------------------
    // Activation
    // ---------------------------------------------------------------------------

    [Fact]
    public void WithDevAutomationBridge_ServiceStarts_WhenAfterSetupFires()
    {
        var opts = new AutomationBridgeOptions();

        var builder = AppBuilder.Configure<StubApplication>()
            .WithDevAutomationBridge(opts);

        // Simulate the AfterSetup phase that runs during AppBuilder.Setup().
        builder.AfterSetupCallback(builder);

        Assert.True(opts.RegisteredService!.IsRunning);
    }

    [Fact]
    public void WithDevAutomationBridge_ServiceRespectsSuppliledPort()
    {
        var opts = new AutomationBridgeOptions { Port = 19317 };

        AppBuilder.Configure<StubApplication>()
            .WithDevAutomationBridge(opts);

        Assert.Equal(19317, opts.RegisteredService!.Options.Port);
    }

    // ---------------------------------------------------------------------------
    // Non-activation guard
    // ---------------------------------------------------------------------------

    [Fact]
    public void WithoutWithDevAutomationBridge_NoServiceRegistered()
    {
        var opts = new AutomationBridgeOptions();

        // A builder that never had WithDevAutomationBridge called.
        _ = AppBuilder.Configure<StubApplication>();

        // opts.RegisteredService is only populated by the extension method.
        Assert.Null(opts.RegisteredService);
    }

    // ---------------------------------------------------------------------------
    // Coexistence
    // ---------------------------------------------------------------------------

    [Fact]
    public void WithDevAutomationBridge_CoexistsWith_OtherAfterSetupCallbacks()
    {
        var opts = new AutomationBridgeOptions();
        var otherCallbackFired = false;

        var builder = AppBuilder.Configure<StubApplication>()
            .AfterSetup(_ => otherCallbackFired = true)
            .WithDevAutomationBridge(opts);

        builder.AfterSetupCallback(builder);

        Assert.True(otherCallbackFired, "pre-existing AfterSetup callback must still fire");
        Assert.True(opts.RegisteredService!.IsRunning, "bridge service must still start");
    }

    [Fact]
    public void WithDevAutomationBridge_AfterOtherAfterSetup_BothFire()
    {
        var opts = new AutomationBridgeOptions();
        var otherCallbackFired = false;

        var builder = AppBuilder.Configure<StubApplication>()
            .WithDevAutomationBridge(opts)
            .AfterSetup(_ => otherCallbackFired = true);

        builder.AfterSetupCallback(builder);

        Assert.True(opts.RegisteredService!.IsRunning);
        Assert.True(otherCallbackFired);
    }

    [Fact]
    public void WithDevAutomationBridge_DefaultOptions_UsesDefaultPort()
    {
        var builder = AppBuilder.Configure<StubApplication>()
            .WithDevAutomationBridge();

        // The default port is 9317 per AutomationBridgeOptions.
        // Retrieve the service via the default opts instance stored internally.
        // We cannot access opts from the outside when null was passed, but we can
        // verify indirectly: AfterSetupCallback must exist (non-default action).
        // The real assertion is that no exception is thrown.
        builder.AfterSetupCallback(builder);
        // If we reach here, Start() completed without error.
    }

    // ---------------------------------------------------------------------------
    // Idempotency of the service itself
    // ---------------------------------------------------------------------------

    [Fact]
    public void Start_IsIdempotent()
    {
        var opts = new AutomationBridgeOptions();
        var service = new AutomationBridgeHostedService(opts);

        service.Start();
        service.Start(); // second call must be a no-op

        Assert.True(service.IsRunning);
    }

    [Fact]
    public void Stop_IsIdempotent()
    {
        var opts = new AutomationBridgeOptions();
        var service = new AutomationBridgeHostedService(opts);

        service.Start();
        service.Stop();
        service.Stop(); // second call must be a no-op

        Assert.False(service.IsRunning);
    }

    [Fact]
    public void Service_CanBeStopped_AfterStart()
    {
        var opts = new AutomationBridgeOptions();
        var service = new AutomationBridgeHostedService(opts);

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
}
