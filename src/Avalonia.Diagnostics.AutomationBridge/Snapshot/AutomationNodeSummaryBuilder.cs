using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Avalonia.Automation.Peers;
using Avalonia.Automation.Provider;
using Avalonia.AutomationBridge.Protocol.Messages;

namespace Avalonia.Diagnostics.AutomationBridge.Snapshot;

/// <summary>
/// Projects an <see cref="AutomationPeer"/> into a compact <see cref="NodeSummaryDto"/>.
/// </summary>
/// <remarks>
/// <para>
/// Only the fields defined on <see cref="NodeSummaryDto"/> are populated.  Child lists and
/// large text content are excluded.  Fields that cannot be determined cleanly (e.g. value when
/// no <see cref="IValueProvider"/> is present) are left null.
/// </para>
/// <para>
/// The builder is stateless; all methods are static.
/// </para>
/// </remarks>
public static class AutomationNodeSummaryBuilder
{
    private static readonly Regex s_structuredNameEntryPattern =
        new(@"(?:(?<=^)|(?<=, ))(?<key>[A-Za-z_][A-Za-z0-9_]*) = ", RegexOptions.Compiled);

    private static readonly string[] s_preferredLabelKeys =
    [
        "DisplayName",
        "Name",
        "Title",
        "Label",
        "Text",
        "Header",
        "AssetName",
        "Key",
        "Id",
    ];

    /// <summary>
    /// Builds a <see cref="NodeSummaryDto"/> from <paramref name="peer"/> using the supplied
    /// pre-assigned handle strings.
    /// </summary>
    /// <param name="peer">The automation peer to summarise.</param>
    /// <param name="handle">The session-local handle for this node (e.g. <c>n3</c>).</param>
    /// <param name="rootId">
    /// The session-local handle of the root that owns this node (e.g. <c>w1</c>).
    /// For root nodes this is the same as <paramref name="handle"/>.
    /// </param>
    /// <returns>A compact, protocol-ready summary of the node.</returns>
    public static NodeSummaryDto Build(AutomationPeer peer, string handle, string rootId)
    {
        var bounds = TryGetBounds(peer);
        var actions = GetActions(peer);
        var (name, metadata) = BuildNameAndMetadata(
            TryGetString(peer.GetName),
            TryGetString(peer.GetItemType),
            TryGetString(peer.GetHelpText));

        return new NodeSummaryDto
        {
            Id = handle,
            RootId = rootId,
            Role = TryGetRole(peer),
            Name = name,
            AutomationId = TryGetString(peer.GetAutomationId),
            ClassName = TryGetString(peer.GetClassName),
            Enabled = TryGetNullableBool(peer.IsEnabled),
            Focused = TryGetNullableBool(peer.HasKeyboardFocus),
            Offscreen = TryGetNullableBool(peer.IsOffscreen),
            Selected = GetSelected(peer),
            Expanded = GetExpanded(peer),
            Checked = GetChecked(peer),
            Value = TryGetValue(peer),
            Bounds = bounds,
            Actions = actions,
            State = GetState(peer),
            Metadata = metadata,
        };
    }

    // -------------------------------------------------------------------------
    // Role mapping
    // -------------------------------------------------------------------------

    /// <summary>Returns the protocol role string for <paramref name="peer"/>.</summary>
    internal static string GetRole(AutomationPeer peer) => GetRole(peer.GetAutomationControlType());

    internal static string GetRole(AutomationControlType type) => type switch
    {
        AutomationControlType.Button       => "button",
        AutomationControlType.Calendar     => "calendar",
        AutomationControlType.CheckBox     => "checkbox",
        AutomationControlType.ComboBox     => "combobox",
        AutomationControlType.ComboBoxItem => "comboboxitem",
        AutomationControlType.Edit         => "edit",
        AutomationControlType.Hyperlink    => "hyperlink",
        AutomationControlType.Image        => "image",
        AutomationControlType.ListItem     => "listitem",
        AutomationControlType.List         => "list",
        AutomationControlType.Menu         => "menu",
        AutomationControlType.MenuBar      => "menubar",
        AutomationControlType.MenuItem     => "menuitem",
        AutomationControlType.ProgressBar  => "progressbar",
        AutomationControlType.RadioButton  => "radiobutton",
        AutomationControlType.ScrollBar    => "scrollbar",
        AutomationControlType.Slider       => "slider",
        AutomationControlType.Spinner      => "spinner",
        AutomationControlType.StatusBar    => "statusbar",
        AutomationControlType.Tab          => "tab",
        AutomationControlType.TabItem      => "tabitem",
        AutomationControlType.Text         => "text",
        AutomationControlType.ToolBar      => "toolbar",
        AutomationControlType.ToolTip      => "tooltip",
        AutomationControlType.Tree         => "tree",
        AutomationControlType.TreeItem     => "treeitem",
        AutomationControlType.Custom       => "custom",
        AutomationControlType.Group        => "group",
        AutomationControlType.Thumb        => "thumb",
        AutomationControlType.DataGrid     => "datagrid",
        AutomationControlType.DataItem     => "dataitem",
        AutomationControlType.Document     => "document",
        AutomationControlType.SplitButton  => "splitbutton",
        AutomationControlType.Window       => "window",
        AutomationControlType.Pane         => "pane",
        AutomationControlType.Header       => "header",
        AutomationControlType.HeaderItem   => "headeritem",
        AutomationControlType.Table        => "table",
        AutomationControlType.TitleBar     => "titlebar",
        AutomationControlType.Separator    => "separator",
        AutomationControlType.Expander     => "expander",
        AutomationControlType.None         => "none",
        _                                  => type.ToString().ToLowerInvariant(),
    };

    // -------------------------------------------------------------------------
    // Action derivation
    // -------------------------------------------------------------------------

    internal static string[] GetActions(AutomationPeer peer)
    {
        var actions = new List<string>();

        if (TryGetProvider<IInvokeProvider>(peer) is not null)
            actions.Add(BridgeAction.Invoke);

        if (SupportsSetValue(TryGetProvider<IValueProvider>(peer)))
            actions.Add(BridgeAction.SetValue);

        if (TryGetProvider<IToggleProvider>(peer) is not null)
            actions.Add(BridgeAction.Toggle);

        if (TryGetProvider<ISelectionItemProvider>(peer) is not null)
            actions.Add(BridgeAction.Select);

        if (TryGetProvider<IExpandCollapseProvider>(peer) is not null)
        {
            actions.Add(BridgeAction.Expand);
            actions.Add(BridgeAction.Collapse);
        }

        if (TryGetBool(peer.IsKeyboardFocusable))
            actions.Add(BridgeAction.SetFocus);

        return actions.ToArray();
    }

    internal static bool? GetSelected(AutomationPeer peer)
    {
        try
        {
            var provider = TryGetProvider<ISelectionItemProvider>(peer);
            return provider?.IsSelected;
        }
        catch
        {
            return null;
        }
    }

    internal static bool? GetExpanded(AutomationPeer peer)
    {
        try
        {
            var provider = TryGetProvider<IExpandCollapseProvider>(peer);
            return provider?.ExpandCollapseState switch
            {
                null => null,
                Automation.ExpandCollapseState.Expanded => true,
                Automation.ExpandCollapseState.PartiallyExpanded => true,
                Automation.ExpandCollapseState.Collapsed => false,
                Automation.ExpandCollapseState.LeafNode => false,
                _ => null,
            };
        }
        catch
        {
            return null;
        }
    }

    internal static bool? GetChecked(AutomationPeer peer)
    {
        try
        {
            var provider = TryGetProvider<IToggleProvider>(peer);
            return provider?.ToggleState switch
            {
                null => null,
                ToggleState.On => true,
                ToggleState.Off => false,
                ToggleState.Indeterminate => null,
                _ => null,
            };
        }
        catch
        {
            return null;
        }
    }

    internal static IReadOnlyDictionary<string, string>? GetState(AutomationPeer peer)
        => ParseState(TryGetString(peer.GetItemStatus));

    internal static (string? Name, IReadOnlyDictionary<string, string>? Metadata) BuildNameAndMetadataForPatch(AutomationPeer peer)
        => BuildNameAndMetadata(
            TryGetString(peer.GetName),
            TryGetString(peer.GetItemType),
            TryGetString(peer.GetHelpText));

    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    private static string? NullIfEmpty(string? value)
        => string.IsNullOrEmpty(value) ? null : value;

    private static IReadOnlyDictionary<string, string>? ParseState(string? rawState)
    {
        rawState = NullIfEmpty(rawState);
        if (rawState is null)
            return null;

        Dictionary<string, string>? state = null;
        foreach (var segment in rawState.Split([';', '|', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (!TryParseStateSegment(segment.Trim(','), out var key, out var value))
                continue;

            state ??= new Dictionary<string, string>(StringComparer.Ordinal);
            state[key] = value;
        }

        return state is { Count: > 0 }
            ? state
            : new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["status"] = rawState,
            };
    }

    private static bool TryParseStateSegment(string segment, out string key, out string value)
    {
        key = string.Empty;
        value = string.Empty;

        if (string.IsNullOrWhiteSpace(segment))
            return false;

        var separatorIndex = segment.IndexOfAny(['=', ':']);
        if (separatorIndex >= 0)
        {
            key = segment[..separatorIndex].Trim();
            value = segment[(separatorIndex + 1)..].Trim();
            return key.Length > 0 && value.Length > 0;
        }

        if (segment.Contains(' ', StringComparison.Ordinal))
        {
            key = "status";
            value = segment;
            return true;
        }

        key = segment;
        value = bool.TrueString.ToLowerInvariant();
        return true;
    }

    internal static string TryGetRole(AutomationPeer peer)
    {
        try
        {
            return GetRole(peer.GetAutomationControlType());
        }
        catch
        {
            return "custom";
        }
    }

    private static string? TryGetString(Func<string?> getter)
    {
        try
        {
            return NullIfEmpty(getter());
        }
        catch
        {
            return null;
        }
    }

    private static bool TryGetBool(Func<bool> getter)
    {
        try
        {
            return getter();
        }
        catch
        {
            return false;
        }
    }

    private static bool? TryGetNullableBool(Func<bool> getter)
    {
        try
        {
            return getter();
        }
        catch
        {
            return null;
        }
    }

    private static bool SupportsSetValue(IValueProvider? provider)
    {
        if (provider is null)
            return false;

        try
        {
            return !provider.IsReadOnly;
        }
        catch
        {
            return false;
        }
    }

    internal static string? TryGetValue(AutomationPeer peer)
    {
        try
        {
            return TryGetProvider<IValueProvider>(peer)?.Value;
        }
        catch
        {
            return null;
        }
    }

    private static TProvider? TryGetProvider<TProvider>(AutomationPeer peer)
        where TProvider : class
    {
        try
        {
            return peer.GetProvider<TProvider>();
        }
        catch
        {
            return null;
        }
    }

    private static double[]? TryGetBounds(AutomationPeer peer)
    {
        try
        {
            var rect = peer.GetBoundingRectangle();
            return [rect.X, rect.Y, rect.Width, rect.Height];
        }
        catch
        {
            return null;
        }
    }

    private static (string? Name, IReadOnlyDictionary<string, string>? Metadata) BuildNameAndMetadata(
        string? rawName,
        string? itemType,
        string? helpText)
    {
        var name = string.IsNullOrEmpty(rawName) ? null : rawName;
        IReadOnlyDictionary<string, string>? metadata = null;

        if (!string.IsNullOrEmpty(rawName) && TryParseStructuredObjectName(rawName, out var parsedMetadata))
        {
            name = ChooseDisplayName(parsedMetadata!) ?? rawName;
            metadata = parsedMetadata;
        }

        metadata = MergeMetadata(metadata, itemType, helpText);
        return (name, metadata);
    }

    private static IReadOnlyDictionary<string, string>? MergeMetadata(
        IReadOnlyDictionary<string, string>? metadata,
        string? itemType,
        string? helpText)
    {
        if (metadata is null && itemType is null && helpText is null)
            return null;

        var merged = metadata is null
            ? new Dictionary<string, string>(StringComparer.Ordinal)
            : new Dictionary<string, string>(metadata, StringComparer.Ordinal);

        if (itemType is not null)
            merged["itemType"] = itemType;

        if (helpText is not null)
            merged["helpText"] = helpText;

        return merged;
    }

    private static bool TryParseStructuredObjectName(
        string rawName,
        out IReadOnlyDictionary<string, string>? metadata)
    {
        metadata = null;
        var separatorIndex = rawName.IndexOf(" { ", StringComparison.Ordinal);
        if (separatorIndex <= 0 || !rawName.EndsWith(" }", StringComparison.Ordinal))
            return false;

        var sourceType = rawName[..separatorIndex];
        var body = rawName[(separatorIndex + 3)..^2];
        var matches = s_structuredNameEntryPattern.Matches(body);
        if (matches.Count == 0)
            return false;

        var parsed = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["sourceType"] = sourceType,
        };

        for (var i = 0; i < matches.Count; i++)
        {
            var match = matches[i];
            var key = match.Groups["key"].Value;
            var valueStart = match.Index + match.Length;
            var valueEnd = i + 1 < matches.Count
                ? matches[i + 1].Index - 2
                : body.Length;
            var value = body[valueStart..valueEnd].Trim();
            parsed[key] = value;
        }

        metadata = parsed;
        return true;
    }

    private static string? ChooseDisplayName(IReadOnlyDictionary<string, string> metadata)
    {
        foreach (var key in s_preferredLabelKeys)
        {
            if (metadata.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value))
                return value;
        }

        foreach (var pair in metadata)
        {
            if (pair.Key == "sourceType")
                continue;

            if (!string.IsNullOrWhiteSpace(pair.Value))
                return pair.Value;
        }

        return null;
    }
}
