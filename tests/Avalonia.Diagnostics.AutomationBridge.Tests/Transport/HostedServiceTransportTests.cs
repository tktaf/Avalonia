using System.IO;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using Avalonia.Automation;
using Avalonia.Automation.Provider;
using Avalonia.AutomationBridge.Protocol.Messages;
using Avalonia.Diagnostics.AutomationBridge;
using Avalonia.Diagnostics.AutomationBridge.Hosting;
using Avalonia.Diagnostics.AutomationBridge.Tests.Session;
using Xunit;

namespace Avalonia.Diagnostics.AutomationBridge.Tests.Transport;

public sealed class HostedServiceTransportTests
{
    private static readonly JsonSerializerOptions s_json = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true,
    };

    [Fact]
    public async Task Listener_ProcessesRootsRequestsOverLoopback()
    {
        var root = new StubRootAutomationPeer { ControlType = Avalonia.Automation.Peers.AutomationControlType.Window, Name = "Main" };

        using var service = StartService(root);
        var response = await SendAsync(
            service.BoundPort,
            new BridgeRequest { Action = BridgeAction.Roots, RequestId = "roots-1" });

        Assert.True(response.Ok);
        Assert.Equal("roots-1", response.RequestId);
        Assert.Single(Assert.IsType<NodeSummaryDto[]>(response.Nodes));
    }

    [Fact]
    public async Task Listener_ProcessesActionRequestsOverLoopback()
    {
        var root = new StubRootAutomationPeer { ControlType = Avalonia.Automation.Peers.AutomationControlType.Window };
        var button = new StubAutomationPeer { Name = "Invoke" };
        var provider = new StubInvokeProvider();
        button.RegisterProvider<IInvokeProvider>(provider);
        root.AddChild(button);

        using var service = StartService(root);
        var roots = await SendAsync(service.BoundPort, new BridgeRequest { Action = BridgeAction.Roots });
        var rootId = Assert.Single(Assert.IsType<NodeSummaryDto[]>(roots.Nodes)).Id;
        var query = await SendAsync(
            service.BoundPort,
            new BridgeRequest
            {
                Action = BridgeAction.Query,
                RootId = rootId,
                Selector = new SelectorDto { Name = "Invoke" },
            });

        var nodeId = Assert.Single(Assert.IsType<NodeSummaryDto[]>(query.Nodes)).Id;
        var invoke = await SendAsync(
            service.BoundPort,
            new BridgeRequest
            {
                Action = BridgeAction.Invoke,
                NodeId = nodeId,
            });

        Assert.True(invoke.Ok);
        Assert.Equal(1, provider.CallCount);
        Assert.NotNull(invoke.Delta);
    }

    [Fact]
    public async Task Listener_ProcessesRootsRequests_WhenPeerGetterThrows()
    {
        var root = new StubRootAutomationPeer
        {
            ControlType = Avalonia.Automation.Peers.AutomationControlType.Window,
            NameException = new InvalidOperationException("boom")
        };

        using var service = StartService(root);
        var response = await SendAsync(
            service.BoundPort,
            new BridgeRequest { Action = BridgeAction.Roots, RequestId = "roots-throwing-peer" });

        Assert.True(response.Ok);
        Assert.Equal("roots-throwing-peer", response.RequestId);

        var node = Assert.Single(Assert.IsType<NodeSummaryDto[]>(response.Nodes));
        Assert.Equal("window", node.Role);
        Assert.Null(node.Name);
    }

    [Fact]
    public async Task Listener_ReturnsStructuredError_WhenActionProviderThrows()
    {
        var root = new StubRootAutomationPeer { ControlType = Avalonia.Automation.Peers.AutomationControlType.Window };
        var item = new StubAutomationPeer { Name = "Pick me" };
        item.RegisterProvider<ISelectionItemProvider>(new ThrowingSelectionItemProvider());
        root.AddChild(item);

        using var service = StartService(root);
        var roots = await SendAsync(service.BoundPort, new BridgeRequest { Action = BridgeAction.Roots });
        var rootId = Assert.Single(Assert.IsType<NodeSummaryDto[]>(roots.Nodes)).Id;
        var query = await SendAsync(
            service.BoundPort,
            new BridgeRequest
            {
                Action = BridgeAction.Query,
                RootId = rootId,
                Selector = new SelectorDto { Name = "Pick me" },
            });

        var nodeId = Assert.Single(Assert.IsType<NodeSummaryDto[]>(query.Nodes)).Id;
        var select = await SendAsync(
            service.BoundPort,
            new BridgeRequest
            {
                Action = BridgeAction.Select,
                NodeId = nodeId,
                RequestId = "select-throw",
            });

        Assert.False(select.Ok);
        Assert.Equal("select-throw", select.RequestId);
        Assert.Equal(BridgeErrorCode.ActionFailed, select.Error!.Code);
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

    private static async Task<BridgeResponse> SendAsync(int port, BridgeRequest request)
    {
        using var client = new TcpClient();
        await client.ConnectAsync("127.0.0.1", port);

        using var stream = client.GetStream();
        using var writer = new StreamWriter(stream, new UTF8Encoding(false), leaveOpen: true);
        using var reader = new StreamReader(stream, Encoding.UTF8, leaveOpen: true);
        await writer.WriteLineAsync(JsonSerializer.Serialize(request, s_json));
        await writer.FlushAsync();

        var line = await reader.ReadLineAsync();
        Assert.False(string.IsNullOrEmpty(line));

        return JsonSerializer.Deserialize<BridgeResponse>(line!, s_json)!;
    }

    private sealed class StubInvokeProvider : IInvokeProvider
    {
        public int CallCount { get; private set; }

        public void Invoke() => CallCount++;
    }

    private sealed class InlineRequestInvoker : IAutomationBridgeRequestInvoker
    {
        public BridgeResponse Invoke(Func<BridgeResponse> callback) => callback();
    }

    private sealed class ThrowingSelectionItemProvider : ISelectionItemProvider
    {
        public bool IsSelected => false;
        public ISelectionProvider? SelectionContainer => null;

        public void AddToSelection() { }

        public void RemoveFromSelection() { }

        public void Select() => throw new InvalidOperationException("selection exploded");
    }
}
