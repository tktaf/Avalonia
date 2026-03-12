using System.Globalization;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using Avalonia.AutomationBridge.Protocol.Messages;

namespace Avalonia.Tools.AutomationBridge.Cli;

public static class AutomationBridgeCliRunner
{
    private static readonly JsonSerializerOptions s_json = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true,
    };

    public static async Task<int> RunAsync(
        string[] args,
        TextWriter stdout,
        TextWriter stderr,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(args);
        ArgumentNullException.ThrowIfNull(stdout);
        ArgumentNullException.ThrowIfNull(stderr);

        try
        {
            var options = Parse(args);
            var response = await SendAsync(options, cancellationToken).ConfigureAwait(false);
            await stdout.WriteAsync(JsonSerializer.Serialize(response, s_json)).ConfigureAwait(false);
            return response.Ok ? 0 : 2;
        }
        catch (Exception e)
        {
            await stderr.WriteAsync(e.Message).ConfigureAwait(false);
            return 1;
        }
    }

    private static async Task<BridgeResponse> SendAsync(CommandOptions options, CancellationToken cancellationToken)
    {
        using var client = new TcpClient();
        await client.ConnectAsync(options.Host, options.Port, cancellationToken).ConfigureAwait(false);

        using var stream = client.GetStream();
        using var writer = new StreamWriter(stream, new UTF8Encoding(false), leaveOpen: true) { AutoFlush = true };
        using var reader = new StreamReader(stream, Encoding.UTF8, leaveOpen: true);

        await writer.WriteLineAsync(JsonSerializer.Serialize(options.Request, s_json)).ConfigureAwait(false);
        var line = await reader.ReadLineAsync(cancellationToken).ConfigureAwait(false);

        return JsonSerializer.Deserialize<BridgeResponse>(line ?? throw new InvalidOperationException("Bridge closed the connection without a response."), s_json)
            ?? throw new InvalidOperationException("Bridge response deserialized to null.");
    }

    private static CommandOptions Parse(string[] args)
    {
        var index = 0;
        var host = "127.0.0.1";
        var port = 9317;

        while (index < args.Length && args[index].StartsWith("--", StringComparison.Ordinal))
        {
            switch (args[index])
            {
                case "--host":
                    host = ReadRequiredValue(args, ref index, "--host");
                    break;
                case "--port":
                    port = int.Parse(ReadRequiredValue(args, ref index, "--port"), CultureInfo.InvariantCulture);
                    break;
                default:
                    goto Command;
            }
        }

Command:
        if (index >= args.Length)
            throw new InvalidOperationException("A command is required.");

        var command = args[index++];
        return new CommandOptions(host, port, BuildRequest(command, args, index));
    }

    private static BridgeRequest BuildRequest(string command, string[] args, int index)
    {
        return command switch
        {
            BridgeAction.Roots => new BridgeRequest { Action = BridgeAction.Roots },
            BridgeAction.Describe => new BridgeRequest
            {
                Action = BridgeAction.Describe,
                NodeId = ReadOption(args, ref index, "--node-id", required: true),
            },
            BridgeAction.Query => new BridgeRequest
            {
                Action = BridgeAction.Query,
                RootId = ReadOption(args, ref index, "--root-id", required: true),
                Selector = ParseSelector(
                    ReadOption(args, ref index, "--selector-json", required: true)
                    ?? throw new InvalidOperationException("Missing required option --selector-json.")),
                MaxResults = TryReadInt(args, ref index, "--max-results"),
            },
            BridgeAction.Watch => new BridgeRequest
            {
                Action = BridgeAction.Watch,
                RootId = ReadOption(args, ref index, "--root-id", required: true),
                SinceRevision = TryReadLong(args, ref index, "--since-revision"),
            },
            BridgeAction.Invoke or
            BridgeAction.Toggle or
            BridgeAction.Select or
            BridgeAction.Expand or
            BridgeAction.Collapse or
            BridgeAction.SetFocus or
            BridgeAction.ShowContextMenu => new BridgeRequest
            {
                Action = command,
                NodeId = ReadOption(args, ref index, "--node-id", required: true),
            },
            BridgeAction.SetValue => new BridgeRequest
            {
                Action = BridgeAction.SetValue,
                NodeId = ReadOption(args, ref index, "--node-id", required: true),
                Value = ReadOption(args, ref index, "--value", required: true),
            },
            BridgeAction.Scroll => new BridgeRequest
            {
                Action = BridgeAction.Scroll,
                NodeId = ReadOption(args, ref index, "--node-id", required: true),
                HorizontalAmount = ReadOption(args, ref index, "--horizontal-amount", required: false),
                VerticalAmount = ReadOption(args, ref index, "--vertical-amount", required: false),
            },
            BridgeAction.SetScrollPercent => new BridgeRequest
            {
                Action = BridgeAction.SetScrollPercent,
                NodeId = ReadOption(args, ref index, "--node-id", required: true),
                HorizontalPercent = TryReadDouble(args, ref index, "--horizontal-percent"),
                VerticalPercent = TryReadDouble(args, ref index, "--vertical-percent"),
            },
            _ => throw new InvalidOperationException($"Unknown command '{command}'."),
        };
    }

    private static SelectorDto ParseSelector(string json)
        => JsonSerializer.Deserialize<SelectorDto>(json, s_json)
           ?? throw new InvalidOperationException("Selector JSON deserialized to null.");

    private static string ReadRequiredValue(string[] args, ref int index, string option)
    {
        index++;
        if (index >= args.Length)
            throw new InvalidOperationException($"Missing value for {option}.");

        return args[index++];
    }

    private static string? ReadOption(string[] args, ref int index, string option, bool required)
    {
        var start = index;

        while (index < args.Length)
        {
            if (string.Equals(args[index], option, StringComparison.Ordinal))
                return ReadRequiredValue(args, ref index, option);

            index++;
        }

        index = start;
        if (required)
            throw new InvalidOperationException($"Missing required option {option}.");

        return null;
    }

    private static int? TryReadInt(string[] args, ref int index, string option)
    {
        var value = ReadOption(args, ref index, option, required: false);
        return value is null ? null : int.Parse(value, CultureInfo.InvariantCulture);
    }

    private static long? TryReadLong(string[] args, ref int index, string option)
    {
        var value = ReadOption(args, ref index, option, required: false);
        return value is null ? null : long.Parse(value, CultureInfo.InvariantCulture);
    }

    private static double? TryReadDouble(string[] args, ref int index, string option)
    {
        var value = ReadOption(args, ref index, option, required: false);
        return value is null ? null : double.Parse(value, CultureInfo.InvariantCulture);
    }

    private sealed record CommandOptions(string Host, int Port, BridgeRequest Request);
}
