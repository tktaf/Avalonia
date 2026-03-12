using System.IO;
using System.Text.Json;
using Avalonia.Automation;
using Avalonia.Automation.Provider;
using Avalonia.AutomationBridge.Protocol.Messages;
using Avalonia.Diagnostics.AutomationBridge;
using Avalonia.Diagnostics.AutomationBridge.Hosting;
using Avalonia.Diagnostics.AutomationBridge.Tests.Session;
using Avalonia.Tools.AutomationBridge.Cli;
using Xunit;

namespace Avalonia.Diagnostics.AutomationBridge.Tests.Cli;

public sealed class AutomationBridgeCliTests
{
    private static readonly JsonSerializerOptions s_json = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true,
    };

    [Fact]
    public async Task RootsCommand_PrintsResponseJson()
    {
        var root = new StubRootAutomationPeer { ControlType = Avalonia.Automation.Peers.AutomationControlType.Window, Name = "Main" };

        using var service = StartService(root);
        using var stdout = new StringWriter();
        using var stderr = new StringWriter();

        var exitCode = await AutomationBridgeCliRunner.RunAsync(
            new[] { "--port", service.BoundPort.ToString(), "roots" },
            stdout,
            stderr,
            CancellationToken.None);

        Assert.Equal(0, exitCode);
        Assert.Equal(string.Empty, stderr.ToString());
        var response = JsonSerializer.Deserialize<BridgeResponse>(stdout.ToString(), s_json)!;
        Assert.True(response.Ok);
        Assert.Single(Assert.IsType<NodeSummaryDto[]>(response.Nodes));
    }

    [Fact]
    public async Task InvokeCommand_AttachesAndReturnsDelta()
    {
        var root = new StubRootAutomationPeer { ControlType = Avalonia.Automation.Peers.AutomationControlType.Window };
        var button = new StubAutomationPeer { Name = "Invoke" };
        var provider = new StubInvokeProvider();
        button.RegisterProvider<IInvokeProvider>(provider);
        root.AddChild(button);

        using var service = StartService(root);
        var rootResponse = await SendCliAsync(service.BoundPort, "roots");
        var rootId = Assert.Single(Assert.IsType<NodeSummaryDto[]>(rootResponse.Nodes)).Id;
        var queryResponse = await SendCliAsync(
            service.BoundPort,
            "query",
            "--root-id", rootId,
            "--selector-json", "{\"name\":\"Invoke\"}");
        var nodeId = Assert.Single(Assert.IsType<NodeSummaryDto[]>(queryResponse.Nodes)).Id;

        using var stdout = new StringWriter();
        using var stderr = new StringWriter();
        var exitCode = await AutomationBridgeCliRunner.RunAsync(
            new[] { "--port", service.BoundPort.ToString(), "invoke", "--node-id", nodeId },
            stdout,
            stderr,
            CancellationToken.None);

        Assert.Equal(0, exitCode);
        Assert.Equal(1, provider.CallCount);
        var response = JsonSerializer.Deserialize<BridgeResponse>(stdout.ToString(), s_json)!;
        Assert.True(response.Ok);
        Assert.NotNull(response.Delta);
    }

    [Fact]
    public async Task HelpCommand_PrintsUsageAndExamples()
    {
        using var stdout = new StringWriter();
        using var stderr = new StringWriter();

        var exitCode = await AutomationBridgeCliRunner.RunAsync(
            ["help"],
            stdout,
            stderr,
            CancellationToken.None);

        Assert.Equal(0, exitCode);
        Assert.Equal(string.Empty, stderr.ToString());
        Assert.Contains("Usage:", stdout.ToString(), StringComparison.Ordinal);
        Assert.Contains("wait-for", stdout.ToString(), StringComparison.Ordinal);
        Assert.Contains("inspect", stdout.ToString(), StringComparison.Ordinal);
        Assert.Contains("--selected", stdout.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task QueryCommand_SupportsAutomationIdAndFieldProjection()
    {
        var root = new StubRootAutomationPeer { ControlType = Avalonia.Automation.Peers.AutomationControlType.Window };
        var option = new StubAutomationPeer
        {
            Name = "The Analyst",
            AutomationId = "gm-archetype-analyst",
            ItemStatus = "currentTab=Contract",
        };
        option.RegisterProvider<ISelectionItemProvider>(new StubSelectionItemProvider(isSelected: true));
        root.AddChild(option);

        using var service = StartService(root);
        var roots = await SendCliAsync(service.BoundPort, "roots");
        var rootId = Assert.Single(Assert.IsType<NodeSummaryDto[]>(roots.Nodes)).Id;

        var response = await SendCliAsync(
            service.BoundPort,
            "query",
            "--root-id", rootId,
            "--automation-id", "gm-archetype-analyst",
            "--fields", "name,selected,state");
        var node = Assert.Single(Assert.IsType<NodeSummaryDto[]>(response.Nodes));

        Assert.Equal("The Analyst", node.Name);
        Assert.True(node.Selected);
        Assert.Equal("Contract", node.State!["currentTab"]);
        Assert.Null(node.AutomationId);
        Assert.Null(node.Actions);
    }

    [Fact]
    public async Task InspectCommand_ResolvesNodeByAutomationId()
    {
        var root = new StubRootAutomationPeer { ControlType = Avalonia.Automation.Peers.AutomationControlType.Window };
        var button = new StubAutomationPeer
        {
            Name = "Launch Franchise",
            AutomationId = "launch-franchise",
        };
        button.RegisterProvider<IInvokeProvider>(new StubInvokeProvider());
        root.AddChild(button);

        using var service = StartService(root);
        var roots = await SendCliAsync(service.BoundPort, "roots");
        var rootId = Assert.Single(Assert.IsType<NodeSummaryDto[]>(roots.Nodes)).Id;

        var response = await SendCliAsync(
            service.BoundPort,
            "inspect",
            "--root-id", rootId,
            "--automation-id", "launch-franchise");
        var node = Assert.Single(Assert.IsType<NodeSummaryDto[]>(response.Nodes));

        Assert.Equal("Launch Franchise", node.Name);
        Assert.Equal("launch-franchise", node.AutomationId);
    }

    [Fact]
    public async Task InspectCommand_PreservesStructuredSelectorFailures()
    {
        var root = new StubRootAutomationPeer { ControlType = Avalonia.Automation.Peers.AutomationControlType.Window };

        using var service = StartService(root);
        var roots = await SendCliAsync(service.BoundPort, "roots");
        var rootId = Assert.Single(Assert.IsType<NodeSummaryDto[]>(roots.Nodes)).Id;

        using var stdout = new StringWriter();
        using var stderr = new StringWriter();
        var exitCode = await AutomationBridgeCliRunner.RunAsync(
            ["--port", service.BoundPort.ToString(), "inspect", "--root-id", rootId, "--automation-id", "missing-node"],
            stdout,
            stderr,
            CancellationToken.None);

        Assert.Equal(2, exitCode);
        Assert.Equal(string.Empty, stderr.ToString());
        var response = JsonSerializer.Deserialize<BridgeResponse>(stdout.ToString(), s_json)!;
        Assert.False(response.Ok);
        Assert.Equal(BridgeErrorCode.NodeNotFound, response.Error!.Code);
    }

    [Fact]
    public async Task InvokeCommand_PreservesStructuredSelectorFailures()
    {
        var root = new StubRootAutomationPeer { ControlType = Avalonia.Automation.Peers.AutomationControlType.Window };
        root.AddChild(new StubAutomationPeer { Name = "Save", AutomationId = "save-primary" });
        root.AddChild(new StubAutomationPeer { Name = "Save", AutomationId = "save-secondary" });

        using var service = StartService(root);
        var roots = await SendCliAsync(service.BoundPort, "roots");
        var rootId = Assert.Single(Assert.IsType<NodeSummaryDto[]>(roots.Nodes)).Id;

        using var stdout = new StringWriter();
        using var stderr = new StringWriter();
        var exitCode = await AutomationBridgeCliRunner.RunAsync(
            ["--port", service.BoundPort.ToString(), "invoke", "--root-id", rootId, "--text", "Save"],
            stdout,
            stderr,
            CancellationToken.None);

        Assert.Equal(2, exitCode);
        Assert.Equal(string.Empty, stderr.ToString());
        var response = JsonSerializer.Deserialize<BridgeResponse>(stdout.ToString(), s_json)!;
        Assert.False(response.Ok);
        Assert.Equal(BridgeErrorCode.SelectorAmbiguous, response.Error!.Code);
    }

    [Fact]
    public async Task WaitForCommand_ReturnsWhenNodeAppears()
    {
        var root = new StubRootAutomationPeer { ControlType = Avalonia.Automation.Peers.AutomationControlType.Window };

        using var service = StartService(root);
        var roots = await SendCliAsync(service.BoundPort, "roots");
        var rootId = Assert.Single(Assert.IsType<NodeSummaryDto[]>(roots.Nodes)).Id;

        _ = Task.Run(async () =>
            {
                await Task.Delay(150, TestContext.Current.CancellationToken);
                root.AddChild(new StubAutomationPeer
                {
                    Name = "Contract Details",
                    AutomationId = "contract-details",
                });
            },
            TestContext.Current.CancellationToken);

        using var stdout = new StringWriter();
        using var stderr = new StringWriter();
        var exitCode = await AutomationBridgeCliRunner.RunAsync(
            ["--port", service.BoundPort.ToString(), "wait-for", "--root-id", rootId, "--automation-id", "contract-details", "--timeout-ms", "2000", "--interval-ms", "50"],
            stdout,
            stderr,
            CancellationToken.None);

        Assert.Equal(0, exitCode);
        Assert.Equal(string.Empty, stderr.ToString());
        var response = JsonSerializer.Deserialize<BridgeResponse>(stdout.ToString(), s_json)!;
        Assert.True(response.Ok);
        Assert.Equal("Contract Details", Assert.Single(Assert.IsType<NodeSummaryDto[]>(response.Nodes)).Name);
    }

    [Fact]
    public async Task WaitForCommand_SupportsStateConditions()
    {
        var root = new StubRootAutomationPeer { ControlType = Avalonia.Automation.Peers.AutomationControlType.Window };
        var option = new StubAutomationPeer
        {
            Name = "The Analyst",
            AutomationId = "gm-archetype-analyst",
        };
        var provider = new MutableSelectionItemProvider();
        option.RegisterProvider<ISelectionItemProvider>(provider);
        root.AddChild(option);

        using var service = StartService(root);
        var roots = await SendCliAsync(service.BoundPort, "roots");
        var rootId = Assert.Single(Assert.IsType<NodeSummaryDto[]>(roots.Nodes)).Id;

        _ = Task.Run(async () =>
            {
                await Task.Delay(150, TestContext.Current.CancellationToken);
                provider.IsSelected = true;
            },
            TestContext.Current.CancellationToken);

        using var stdout = new StringWriter();
        using var stderr = new StringWriter();
        var exitCode = await AutomationBridgeCliRunner.RunAsync(
            ["--port", service.BoundPort.ToString(), "wait-for", "--root-id", rootId, "--automation-id", "gm-archetype-analyst", "--selected", "true", "--timeout-ms", "2000", "--interval-ms", "50"],
            stdout,
            stderr,
            CancellationToken.None);

        Assert.Equal(0, exitCode);
        Assert.Equal(string.Empty, stderr.ToString());
        var response = JsonSerializer.Deserialize<BridgeResponse>(stdout.ToString(), s_json)!;
        Assert.True(response.Ok);
        Assert.True(Assert.Single(Assert.IsType<NodeSummaryDto[]>(response.Nodes)).Selected);
    }

    [Fact]
    public async Task WaitForCommand_PreservesNonRetryableBridgeErrors()
    {
        var root = new StubRootAutomationPeer { ControlType = Avalonia.Automation.Peers.AutomationControlType.Window };
        root.AddChild(new StubAutomationPeer { Name = "Save", AutomationId = "save-primary" });
        root.AddChild(new StubAutomationPeer { Name = "Save", AutomationId = "save-secondary" });

        using var service = StartService(root);
        var roots = await SendCliAsync(service.BoundPort, "roots");
        var rootId = Assert.Single(Assert.IsType<NodeSummaryDto[]>(roots.Nodes)).Id;

        using var stdout = new StringWriter();
        using var stderr = new StringWriter();
        var exitCode = await AutomationBridgeCliRunner.RunAsync(
            ["--port", service.BoundPort.ToString(), "wait-for", "--root-id", rootId, "--text", "Save", "--timeout-ms", "2000", "--interval-ms", "50"],
            stdout,
            stderr,
            CancellationToken.None);

        Assert.Equal(2, exitCode);
        Assert.Equal(string.Empty, stderr.ToString());
        var response = JsonSerializer.Deserialize<BridgeResponse>(stdout.ToString(), s_json)!;
        Assert.False(response.Ok);
        Assert.Equal(BridgeErrorCode.SelectorAmbiguous, response.Error!.Code);
    }

    [Fact]
    public async Task PrettyOutput_PrintsCompactHumanReadableNodeSummary()
    {
        var root = new StubRootAutomationPeer
        {
            ControlType = Avalonia.Automation.Peers.AutomationControlType.Window,
            Name = "Main",
        };

        using var service = StartService(root);
        using var stdout = new StringWriter();
        using var stderr = new StringWriter();
        var exitCode = await AutomationBridgeCliRunner.RunAsync(
            ["--port", service.BoundPort.ToString(), "--output", "pretty", "roots"],
            stdout,
            stderr,
            CancellationToken.None);

        Assert.Equal(0, exitCode);
        Assert.Equal(string.Empty, stderr.ToString());
        Assert.Contains("window", stdout.ToString(), StringComparison.Ordinal);
        Assert.Contains("Main", stdout.ToString(), StringComparison.Ordinal);
    }

    private static AutomationBridgeHostedService StartService(StubRootAutomationPeer root)
    {
        var service = new AutomationBridgeHostedService(
            new AutomationBridgeOptions
            {
                Port = 0,
                PeerSourceFactory = () => new[] { root },
            },
            new InlineRequestInvoker());

        service.Start();
        return service;
    }

    private static async Task<BridgeResponse> SendCliAsync(int port, params string[] args)
    {
        using var stdout = new StringWriter();
        using var stderr = new StringWriter();

        var fullArgs = new List<string> { "--port", port.ToString() };
        fullArgs.AddRange(args);
        var exitCode = await AutomationBridgeCliRunner.RunAsync(
            fullArgs.ToArray(),
            stdout,
            stderr,
            CancellationToken.None);

        Assert.Equal(0, exitCode);
        Assert.Equal(string.Empty, stderr.ToString());
        return JsonSerializer.Deserialize<BridgeResponse>(stdout.ToString(), s_json)!;
    }

    private sealed class StubInvokeProvider : IInvokeProvider
    {
        public int CallCount { get; private set; }

        public void Invoke() => CallCount++;
    }

    private sealed class StubSelectionItemProvider : ISelectionItemProvider
    {
        public StubSelectionItemProvider(bool isSelected)
        {
            IsSelected = isSelected;
        }

        public bool IsSelected { get; }
        public ISelectionProvider? SelectionContainer => null;
        public void AddToSelection() { }
        public void RemoveFromSelection() { }
        public void Select() { }
    }

    private sealed class MutableSelectionItemProvider : ISelectionItemProvider
    {
        public bool IsSelected { get; set; }
        public ISelectionProvider? SelectionContainer => null;
        public void AddToSelection() => IsSelected = true;
        public void RemoveFromSelection() => IsSelected = false;
        public void Select() => IsSelected = true;
    }

    private sealed class InlineRequestInvoker : IAutomationBridgeRequestInvoker
    {
        public BridgeResponse Invoke(Func<BridgeResponse> callback) => callback();
    }
}
