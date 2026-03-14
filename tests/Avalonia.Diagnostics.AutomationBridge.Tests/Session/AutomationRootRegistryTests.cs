using System;
using System.Collections.Generic;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Diagnostics.AutomationBridge.Session;
using Avalonia.UnitTests;
using Xunit;

namespace Avalonia.Diagnostics.AutomationBridge.Tests.Session;

/// <summary>
/// Tests for <see cref="AutomationRootRegistry"/>.
/// Proves top-level roots are enumerated without dumping full trees.
/// </summary>
public sealed class AutomationRootRegistryTests
{
    [Fact]
    public void Roots_IsEmpty_WhenApplicationHasNoLifetime()
    {
        using var app = UnitTestApplication.Start(TestServices.StyledWindow);
        var registry = new AutomationRootRegistry();

        Assert.Empty(registry.Roots);
    }

    [Fact]
    public void Roots_ReturnWindowPeers_FromClassicDesktopLifetime()
    {
        using var app = UnitTestApplication.Start(TestServices.StyledWindow);
        var window1 = new Window();
        var window2 = new Window();
        Application.Current!.ApplicationLifetime = new StubClassicDesktopStyleApplicationLifetime(window1, window2);

        var registry = new AutomationRootRegistry();
        var roots = registry.Roots;

        Assert.Equal(2, roots.Count);
        Assert.Same(window1, Assert.IsType<WindowAutomationPeer>(roots[0]).Owner);
        Assert.Same(window2, Assert.IsType<WindowAutomationPeer>(roots[1]).Owner);
    }

    [Fact]
    public void Roots_ReturnTopLevelPeer_FromSingleTopLevelLifetime()
    {
        using var app = UnitTestApplication.Start(TestServices.StyledWindow);
        var topLevel = new Window();
        Application.Current!.ApplicationLifetime = new StubSingleTopLevelApplicationLifetime(topLevel);

        var registry = new AutomationRootRegistry();
        var roots = registry.Roots;

        var peer = Assert.Single(roots);
        Assert.Same(topLevel, Assert.IsType<WindowAutomationPeer>(peer).Owner);
    }

    [Fact]
    public void Roots_IsEmpty_WhenSingleTopLevelLifetimeHasNoTopLevel()
    {
        using var app = UnitTestApplication.Start(TestServices.StyledWindow);
        Application.Current!.ApplicationLifetime = new StubSingleTopLevelApplicationLifetime(null);

        var registry = new AutomationRootRegistry();
        Assert.Empty(registry.Roots);
    }

    private sealed class StubClassicDesktopStyleApplicationLifetime : IClassicDesktopStyleApplicationLifetime
    {
        public StubClassicDesktopStyleApplicationLifetime(params Window[] windows)
        {
            Windows = windows;
            MainWindow = windows.Length > 0 ? windows[0] : null;
        }

        public event EventHandler<ControlledApplicationLifetimeStartupEventArgs>? Startup
        {
            add { }
            remove { }
        }

        public event EventHandler<ShutdownRequestedEventArgs>? ShutdownRequested
        {
            add { }
            remove { }
        }

        public event EventHandler<ControlledApplicationLifetimeExitEventArgs>? Exit
        {
            add { }
            remove { }
        }
        public string[]? Args => null;
        public ShutdownMode ShutdownMode { get; set; }
        public Window? MainWindow { get; set; }
        public IReadOnlyList<Window> Windows { get; }

        public void Shutdown(int exitCode = 0) { }

        public bool TryShutdown(int exitCode = 0) => true;
    }

    private sealed class StubSingleTopLevelApplicationLifetime : ISingleTopLevelApplicationLifetime
    {
        public StubSingleTopLevelApplicationLifetime(TopLevel? topLevel)
        {
            TopLevel = topLevel;
        }

        public TopLevel? TopLevel { get; }
    }
}
