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

    private static AutomationBridgeHostedService StartService(StubRootAutomationPeer root)
    {
        var service = new AutomationBridgeHostedService(new AutomationBridgeOptions
        {
            Port = 0,
            PeerSourceFactory = () => new[] { root },
        });

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
}
