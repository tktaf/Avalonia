using System.Globalization;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using Avalonia.AutomationBridge.Protocol.Messages;

namespace Avalonia.Tools.AutomationBridge.Cli;

public static class AutomationBridgeCliRunner
{
    private const int DefaultWaitTimeoutMs = 5000;
    private const int DefaultWaitIntervalMs = 100;

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
            if (string.Equals(options.Command, "help", StringComparison.Ordinal))
            {
                await stdout.WriteAsync(BuildHelpText()).ConfigureAwait(false);
                return 0;
            }

            var response = await ExecuteAsync(options, cancellationToken).ConfigureAwait(false);
            await WriteResponseAsync(response, options.Output, stdout).ConfigureAwait(false);
            return response.Ok ? 0 : 2;
        }
        catch (Exception e)
        {
            await stderr.WriteAsync(e.Message).ConfigureAwait(false);
            return 1;
        }
    }

    private static async Task<BridgeResponse> ExecuteAsync(
        CommandOptions options,
        CancellationToken cancellationToken)
    {
        return options.Command switch
        {
            BridgeAction.Roots => await SendAsync(
                options,
                new BridgeRequest { Action = BridgeAction.Roots },
                cancellationToken).ConfigureAwait(false),

            BridgeAction.Describe => await DescribeAsync(options, cancellationToken).ConfigureAwait(false),
            "inspect" => await InspectAsync(options, cancellationToken).ConfigureAwait(false),
            BridgeAction.Query => await QueryAsync(options, cancellationToken).ConfigureAwait(false),
            BridgeAction.Watch => await WatchAsync(options, cancellationToken).ConfigureAwait(false),
            "wait-for" => await WaitForAsync(options, cancellationToken).ConfigureAwait(false),

            BridgeAction.Invoke or
            BridgeAction.Toggle or
            BridgeAction.Select or
            BridgeAction.Expand or
            BridgeAction.Collapse or
            BridgeAction.SetFocus or
            BridgeAction.ShowContextMenu => await ExecuteNodeActionAsync(
                options,
                options.Command,
                request => request,
                cancellationToken).ConfigureAwait(false),

            BridgeAction.SetValue => await ExecuteNodeActionAsync(
                options,
                BridgeAction.SetValue,
                request => CopyRequest(
                    request,
                    value: ReadOption(options.CommandArgs, "--value", required: true)),
                cancellationToken).ConfigureAwait(false),

            BridgeAction.Scroll => await ExecuteNodeActionAsync(
                options,
                BridgeAction.Scroll,
                request => CopyRequest(
                    request,
                    horizontalAmount: ReadOption(options.CommandArgs, "--horizontal-amount", required: false),
                    verticalAmount: ReadOption(options.CommandArgs, "--vertical-amount", required: false)),
                cancellationToken).ConfigureAwait(false),

            BridgeAction.SetScrollPercent => await ExecuteNodeActionAsync(
                options,
                BridgeAction.SetScrollPercent,
                request => CopyRequest(
                    request,
                    horizontalPercent: TryReadDouble(options.CommandArgs, "--horizontal-percent"),
                    verticalPercent: TryReadDouble(options.CommandArgs, "--vertical-percent")),
                cancellationToken).ConfigureAwait(false),

            _ => throw new InvalidOperationException($"Unknown command '{options.Command}'."),
        };
    }

    private static async Task<BridgeResponse> DescribeAsync(
        CommandOptions options,
        CancellationToken cancellationToken)
    {
        var nodeId = ReadOption(options.CommandArgs, "--node-id", required: true);
        return await SendAsync(
            options,
            new BridgeRequest
            {
                Action = BridgeAction.Describe,
                NodeId = nodeId,
            },
            cancellationToken).ConfigureAwait(false);
    }

    private static async Task<BridgeResponse> InspectAsync(
        CommandOptions options,
        CancellationToken cancellationToken)
    {
        var resolution = await ResolveTargetNodeIdAsync(options, cancellationToken).ConfigureAwait(false);
        if (resolution.Failure is not null)
            return resolution.Failure;

        var response = await SendAsync(
            options,
            new BridgeRequest
            {
                Action = BridgeAction.Describe,
                NodeId = resolution.NodeId,
            },
            cancellationToken).ConfigureAwait(false);

        return ApplyFieldProjection(response, ParseFields(ReadOption(options.CommandArgs, "--fields", required: false)));
    }

    private static async Task<BridgeResponse> QueryAsync(
        CommandOptions options,
        CancellationToken cancellationToken)
    {
        var request = new BridgeRequest
        {
            Action = BridgeAction.Query,
            RootId = ReadOption(options.CommandArgs, "--root-id", required: true),
            Selector = BuildSelector(options.CommandArgs),
            MaxResults = TryReadInt(options.CommandArgs, "--max-results"),
        };

        return await SendAsync(options, request, cancellationToken).ConfigureAwait(false);
    }

    private static async Task<BridgeResponse> WatchAsync(
        CommandOptions options,
        CancellationToken cancellationToken)
    {
        var request = new BridgeRequest
        {
            Action = BridgeAction.Watch,
            RootId = ReadOption(options.CommandArgs, "--root-id", required: true),
            SinceRevision = TryReadLong(options.CommandArgs, "--since-revision"),
        };

        return await SendAsync(options, request, cancellationToken).ConfigureAwait(false);
    }

    private static async Task<BridgeResponse> WaitForAsync(
        CommandOptions options,
        CancellationToken cancellationToken)
    {
        var rootId = ReadOption(options.CommandArgs, "--root-id", required: true);
        var selector = BuildSelector(options.CommandArgs);
        var timeoutMs = TryReadInt(options.CommandArgs, "--timeout-ms") ?? DefaultWaitTimeoutMs;
        var intervalMs = TryReadInt(options.CommandArgs, "--interval-ms") ?? DefaultWaitIntervalMs;
        var deadline = DateTime.UtcNow.AddMilliseconds(timeoutMs);

        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var response = await SendAsync(
                options,
                new BridgeRequest
                {
                    Action = BridgeAction.Query,
                    RootId = rootId,
                    Selector = selector,
                    MaxResults = 1,
                },
                cancellationToken).ConfigureAwait(false);

            if (response.Ok)
                return response;

            if (!string.Equals(response.Error?.Code, BridgeErrorCode.NodeNotFound, StringComparison.Ordinal))
                return response;

            if (DateTime.UtcNow >= deadline)
            {
                return BridgeResponse.Failure(
                    BridgeErrorCode.NodeNotFound,
                    $"Timed out waiting for selector after {timeoutMs}ms.");
            }

            await Task.Delay(intervalMs, cancellationToken).ConfigureAwait(false);
        }
    }

    private static async Task<BridgeResponse> ExecuteNodeActionAsync(
        CommandOptions options,
        string action,
        Func<BridgeRequest, BridgeRequest> configure,
        CancellationToken cancellationToken)
    {
        var resolution = await ResolveTargetNodeIdAsync(options, cancellationToken).ConfigureAwait(false);
        if (resolution.Failure is not null)
            return resolution.Failure;

        var request = configure(new BridgeRequest
        {
            Action = action,
            NodeId = resolution.NodeId,
        });

        return await SendAsync(options, request, cancellationToken).ConfigureAwait(false);
    }

    private static async Task<TargetResolutionResult> ResolveTargetNodeIdAsync(
        CommandOptions options,
        CancellationToken cancellationToken)
    {
        var nodeId = ReadOption(options.CommandArgs, "--node-id", required: false);
        if (!string.IsNullOrEmpty(nodeId))
            return new TargetResolutionResult(nodeId, null);

        var rootId = ReadOption(options.CommandArgs, "--root-id", required: false)
            ?? throw new InvalidOperationException("Resolving a target by selector requires --root-id.");

        var response = await SendAsync(
            options,
            new BridgeRequest
            {
                Action = BridgeAction.Query,
                RootId = rootId,
                Selector = BuildSelector(options.CommandArgs),
                MaxResults = 1,
            },
            cancellationToken).ConfigureAwait(false);

        if (!response.Ok)
            return new TargetResolutionResult(null, response);

        var node = TryGetSingleNode(response, out var failure);
        return new TargetResolutionResult(node?.Id, failure);
    }

    private static SelectorDto BuildSelector(string[] args)
    {
        var baseSelector = ReadOption(args, "--selector-json", required: false) is { } selectorJson
            ? ParseSelector(selectorJson)
            : null;

        return new SelectorDto
        {
            Id = baseSelector?.Id,
            AutomationId = ReadOption(args, "--automation-id", required: false) ?? baseSelector?.AutomationId,
            Name = ReadOption(args, "--text", required: false)
                ?? ReadOption(args, "--name", required: false)
                ?? baseSelector?.Name,
            NameSubstring = ReadOption(args, "--text", required: false) is not null
                || TryReadBool(args, "--name-substring")
                || baseSelector?.NameSubstring == true,
            Role = ReadOption(args, "--role", required: false) ?? baseSelector?.Role,
            ClassName = ReadOption(args, "--class-name", required: false) ?? baseSelector?.ClassName,
            Focused = TryReadNullableBool(args, "--focused") ?? baseSelector?.Focused,
            Enabled = TryReadNullableBool(args, "--enabled") ?? baseSelector?.Enabled,
            Selected = TryReadNullableBool(args, "--selected") ?? baseSelector?.Selected,
            Visible = TryReadNullableBool(args, "--visible") ?? baseSelector?.Visible,
            HasAction = ReadOption(args, "--has-action", required: false) ?? baseSelector?.HasAction,
            State = MergeStateFilters(baseSelector?.State, ParseStateFilters(args)),
            Within = ReadOption(args, "--within", required: false) ?? baseSelector?.Within,
            ContainerId = ReadOption(args, "--container-id", required: false) ?? baseSelector?.ContainerId,
            Path = baseSelector?.Path,
            Nth = TryReadInt(args, "--nth") ?? baseSelector?.Nth,
            Fields = ParseFields(ReadOption(args, "--fields", required: false)) ?? baseSelector?.Fields,
        };
    }

    private static async Task<BridgeResponse> SendAsync(
        CommandOptions options,
        BridgeRequest request,
        CancellationToken cancellationToken)
    {
        using var client = new TcpClient();
        await client.ConnectAsync(options.Host, options.Port, cancellationToken).ConfigureAwait(false);

        using var stream = client.GetStream();
        using var writer = new StreamWriter(stream, new UTF8Encoding(false), leaveOpen: true) { AutoFlush = true };
        using var reader = new StreamReader(stream, Encoding.UTF8, leaveOpen: true);

        await writer.WriteLineAsync(JsonSerializer.Serialize(request, s_json)).ConfigureAwait(false);
        var line = await reader.ReadLineAsync(cancellationToken).ConfigureAwait(false);

        return JsonSerializer.Deserialize<BridgeResponse>(
                   line ?? throw new InvalidOperationException("Bridge closed the connection without a response."),
                   s_json)
               ?? throw new InvalidOperationException("Bridge response deserialized to null.");
    }

    private static async Task WriteResponseAsync(
        BridgeResponse response,
        OutputMode output,
        TextWriter stdout)
    {
        if (output == OutputMode.Json)
        {
            await stdout.WriteAsync(JsonSerializer.Serialize(response, s_json)).ConfigureAwait(false);
            return;
        }

        await stdout.WriteAsync(FormatPretty(response)).ConfigureAwait(false);
    }

    private static string FormatPretty(BridgeResponse response)
    {
        if (!response.Ok)
            return $"error {response.Error?.Code}: {response.Error?.Message}";

        if (response.Nodes is { Length: > 0 } nodes)
        {
            return string.Join(
                Environment.NewLine,
                nodes.Select(FormatNode));
        }

        if (response.Delta is { } delta)
        {
            return $"ok revision={delta.Revision} updated={delta.Updated.Length} added={delta.Added.Length} removed={delta.Removed.Length}"
                + (response.Completion is null ? string.Empty : $" completion={response.Completion.State}");
        }

        return response.Completion is null ? "ok" : $"ok completion={response.Completion.State}";
    }

    private static string FormatNode(NodeSummaryDto node)
    {
        var parts = new List<string> { node.Id, node.Role };

        if (!string.IsNullOrWhiteSpace(node.AutomationId))
            parts.Add($"automationId={node.AutomationId}");

        if (!string.IsNullOrWhiteSpace(node.Name))
            parts.Add($"name={node.Name}");

        if (node.Selected is not null)
            parts.Add($"selected={node.Selected.Value.ToString().ToLowerInvariant()}");

        if (node.Actions is { Length: > 0 })
            parts.Add($"actions={string.Join(",", node.Actions)}");

        if (node.State is { Count: > 0 } state)
            parts.Add($"state={string.Join(",", state.Select(pair => $"{pair.Key}:{pair.Value}"))}");

        return string.Join(" ", parts);
    }

    private static BridgeResponse ApplyFieldProjection(BridgeResponse response, string[]? fields)
    {
        if (fields is not { Length: > 0 } || response.Nodes is not { Length: > 0 } nodes)
            return response;

        var requested = new HashSet<string>(fields, StringComparer.OrdinalIgnoreCase);
        var projectedNodes = new NodeSummaryDto[nodes.Length];

        for (var i = 0; i < nodes.Length; i++)
        {
            var node = nodes[i];
            projectedNodes[i] = new NodeSummaryDto
            {
                Id = node.Id,
                RootId = node.RootId,
                Role = node.Role,
                Name = Include(requested, "name") ? node.Name : null,
                AutomationId = Include(requested, "automationId") ? node.AutomationId : null,
                ClassName = Include(requested, "className") ? node.ClassName : null,
                Enabled = Include(requested, "enabled") ? node.Enabled : null,
                Focused = Include(requested, "focused") ? node.Focused : null,
                Offscreen = Include(requested, "offscreen") ? node.Offscreen : null,
                Selected = Include(requested, "selected") ? node.Selected : null,
                Expanded = Include(requested, "expanded") ? node.Expanded : null,
                Checked = Include(requested, "checked") ? node.Checked : null,
                Value = Include(requested, "value") ? node.Value : null,
                Bounds = Include(requested, "bounds") ? node.Bounds : null,
                Actions = Include(requested, "actions") ? node.Actions : null,
                State = Include(requested, "state") ? node.State : null,
                Metadata = Include(requested, "metadata") ? node.Metadata : null,
            };
        }

        return new BridgeResponse
        {
            RequestId = response.RequestId,
            Ok = response.Ok,
            Error = response.Error,
            Nodes = projectedNodes,
            Delta = response.Delta,
            Completion = response.Completion,
        };
    }

    private static string BuildHelpText()
        => """
           Usage:
             bridge [--host HOST] [--port PORT] [--output json|pretty] <command> [options]

           Commands:
             help
             roots
             describe --node-id NODE_ID
             query --root-id ROOT_ID [--selector-json JSON] [--automation-id ID] [--text TEXT] [--fields csv]
             inspect (--node-id NODE_ID | --root-id ROOT_ID --automation-id ID)
             wait-for --root-id ROOT_ID [selector options such as --automation-id ID --text TEXT --selected true --state currentTab=Contract] [--timeout-ms N] [--interval-ms N]
             invoke|toggle|select|expand|collapse|set-focus|show-context-menu --root-id ROOT_ID --automation-id ID
             set-value --root-id ROOT_ID --automation-id ID --value VALUE
             scroll --root-id ROOT_ID --automation-id ID [--horizontal-amount AMOUNT] [--vertical-amount AMOUNT]
             set-scroll-percent --root-id ROOT_ID --automation-id ID [--horizontal-percent N] [--vertical-percent N]

           Examples:
             bridge roots
             bridge query --root-id w1 --automation-id player-profile-contract-tab-button --fields id,name,actions
             bridge inspect --root-id w1 --automation-id launch-franchise
             bridge wait-for --root-id w1 --text "Contract Details" --timeout-ms 5000
             bridge wait-for --root-id w1 --automation-id player-profile-contract-tab-button --selected true
             bridge wait-for --root-id w1 --automation-id player-profile --state currentTab=Contract
           """;

    private static CommandOptions Parse(string[] args)
    {
        var index = 0;
        var host = "127.0.0.1";
        var port = 9317;
        var output = OutputMode.Json;

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
                case "--output":
                    output = ParseOutputMode(ReadRequiredValue(args, ref index, "--output"));
                    break;
                case "--help":
                    return new CommandOptions(host, port, output, "help", Array.Empty<string>());
                default:
                    goto Command;
            }
        }

Command:
        if (index >= args.Length)
            return new CommandOptions(host, port, output, "help", Array.Empty<string>());

        var command = args[index++];
        if (string.Equals(command, "help", StringComparison.Ordinal))
            return new CommandOptions(host, port, output, "help", Array.Empty<string>());

        var commandArgs = args[index..];
        if (commandArgs.Any(arg => string.Equals(arg, "--help", StringComparison.Ordinal)))
            return new CommandOptions(host, port, output, "help", Array.Empty<string>());

        return new CommandOptions(host, port, output, command, commandArgs);
    }

    private static OutputMode ParseOutputMode(string value)
        => value.ToLowerInvariant() switch
        {
            "json" => OutputMode.Json,
            "pretty" => OutputMode.Pretty,
            _ => throw new InvalidOperationException($"Unknown output mode '{value}'."),
        };

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

    private static string? ReadOption(string[] args, string option, bool required)
    {
        for (var index = 0; index < args.Length; index++)
        {
            if (!string.Equals(args[index], option, StringComparison.Ordinal))
                continue;

            if (index + 1 >= args.Length)
                throw new InvalidOperationException($"Missing value for {option}.");

            return args[index + 1];
        }

        if (required)
            throw new InvalidOperationException($"Missing required option {option}.");

        return null;
    }

    private static bool TryReadBool(string[] args, string option)
    {
        var value = ReadOption(args, option, required: false);
        return value is not null && bool.Parse(value);
    }

    private static bool? TryReadNullableBool(string[] args, string option)
    {
        var value = ReadOption(args, option, required: false);
        return value is null ? null : bool.Parse(value);
    }

    private static int? TryReadInt(string[] args, string option)
    {
        var value = ReadOption(args, option, required: false);
        return value is null ? null : int.Parse(value, CultureInfo.InvariantCulture);
    }

    private static long? TryReadLong(string[] args, string option)
    {
        var value = ReadOption(args, option, required: false);
        return value is null ? null : long.Parse(value, CultureInfo.InvariantCulture);
    }

    private static double? TryReadDouble(string[] args, string option)
    {
        var value = ReadOption(args, option, required: false);
        return value is null ? null : double.Parse(value, CultureInfo.InvariantCulture);
    }

    private static string[]? ParseFields(string? csv)
    {
        if (string.IsNullOrWhiteSpace(csv))
            return null;

        return csv.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    }

    private static Dictionary<string, string>? ParseStateFilters(string[] args)
    {
        Dictionary<string, string>? filters = null;

        for (var index = 0; index < args.Length; index++)
        {
            if (!string.Equals(args[index], "--state", StringComparison.Ordinal))
                continue;

            if (index + 1 >= args.Length)
                throw new InvalidOperationException("Missing value for --state.");

            var pair = args[index + 1];
            var separatorIndex = pair.IndexOf('=');
            if (separatorIndex <= 0 || separatorIndex == pair.Length - 1)
                throw new InvalidOperationException($"State filter '{pair}' must be in key=value form.");

            filters ??= new Dictionary<string, string>(StringComparer.Ordinal);
            filters[pair[..separatorIndex]] = pair[(separatorIndex + 1)..];
            index++;
        }

        return filters;
    }

    private static IReadOnlyDictionary<string, string>? MergeStateFilters(
        IReadOnlyDictionary<string, string>? baseState,
        IReadOnlyDictionary<string, string>? overrideState)
    {
        if (baseState is null || baseState.Count == 0)
            return overrideState;

        if (overrideState is null || overrideState.Count == 0)
            return baseState;

        var merged = new Dictionary<string, string>(baseState, StringComparer.Ordinal);
        foreach (var pair in overrideState)
            merged[pair.Key] = pair.Value;

        return merged;
    }

    private static NodeSummaryDto? TryGetSingleNode(BridgeResponse response, out BridgeResponse? failure)
    {
        if (response.Nodes is not { Length: 1 })
        {
            failure = response.Nodes is { Length: > 1 }
                ? BridgeResponse.Failure(
                    BridgeErrorCode.SelectorAmbiguous,
                    "Selector did not resolve to exactly one node.",
                    response.RequestId)
                : BridgeResponse.Failure(
                    BridgeErrorCode.NodeNotFound,
                    "Selector did not resolve to exactly one node.",
                    response.RequestId);
            return null;
        }

        failure = null;
        return response.Nodes[0];
    }

    private static BridgeRequest CopyRequest(
        BridgeRequest request,
        string? value = null,
        string? horizontalAmount = null,
        string? verticalAmount = null,
        double? horizontalPercent = null,
        double? verticalPercent = null)
        => new()
        {
            RequestId = request.RequestId,
            Action = request.Action,
            RootId = request.RootId,
            NodeId = request.NodeId,
            Selector = request.Selector,
            MaxResults = request.MaxResults,
            Value = value ?? request.Value,
            HorizontalAmount = horizontalAmount ?? request.HorizontalAmount,
            VerticalAmount = verticalAmount ?? request.VerticalAmount,
            HorizontalPercent = horizontalPercent ?? request.HorizontalPercent,
            VerticalPercent = verticalPercent ?? request.VerticalPercent,
            SinceRevision = request.SinceRevision,
        };

    private static bool Include(HashSet<string> requested, string field)
        => requested.Contains(field);

    private sealed record CommandOptions(
        string Host,
        int Port,
        OutputMode Output,
        string Command,
        string[] CommandArgs);

    private sealed record TargetResolutionResult(string? NodeId, BridgeResponse? Failure);

    private enum OutputMode
    {
        Json,
        Pretty,
    }
}
