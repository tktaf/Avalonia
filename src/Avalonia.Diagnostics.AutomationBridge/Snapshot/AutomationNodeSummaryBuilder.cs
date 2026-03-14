using System;
using System.Collections.Generic;
using Avalonia.Automation.Peers;
using Avalonia.Automation.Provider;
using Avalonia.AutomationBridge.Protocol.Messages;
using Avalonia.Controls;
using Avalonia.Controls.Automation.Peers;

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
    /// <param name="fields">
    /// Optional response projection fields. When provided, only the requested summary properties are
    /// materialized; <c>id</c>, <c>rootId</c>, and <c>role</c> are always included.
    /// </param>
    /// <returns>A compact, protocol-ready summary of the node.</returns>
    public static NodeSummaryDto Build(
        AutomationPeer peer,
        string handle,
        string rootId,
        IReadOnlyCollection<string>? fields = null)
    {
        var requestedFields = fields is { Count: > 0 }
            ? new HashSet<string>(fields, StringComparer.OrdinalIgnoreCase)
            : null;
        var includeAllFields = requestedFields is null;
        var includeName = includeAllFields || requestedFields!.Contains("name");
        var includeMetadata = includeAllFields || requestedFields!.Contains("metadata");

        string? name = null;
        IReadOnlyDictionary<string, string>? metadata = null;
        if (includeName || includeMetadata)
        {
            var rawName = TryGetString(peer.GetName);
            var itemType = includeMetadata ? TryGetString(peer.GetItemType) : null;
            var helpText = includeMetadata ? TryGetString(peer.GetHelpText) : null;
            (name, metadata) = BuildNameAndMetadata(rawName, itemType, helpText);
        }

        return new NodeSummaryDto
        {
            Id = handle,
            RootId = rootId,
            Role = TryGetRole(peer),
            Name = includeName ? name : null,
            AutomationId = ShouldInclude(requestedFields, includeAllFields, "automationId")
                ? TryGetString(peer.GetAutomationId)
                : null,
            ClassName = ShouldInclude(requestedFields, includeAllFields, "className")
                ? TryGetString(peer.GetClassName)
                : null,
            Enabled = ShouldInclude(requestedFields, includeAllFields, "enabled")
                ? TryGetNullableBool(peer.IsEnabled)
                : null,
            Focused = ShouldInclude(requestedFields, includeAllFields, "focused")
                ? TryGetNullableBool(peer.HasKeyboardFocus)
                : null,
            Offscreen = ShouldInclude(requestedFields, includeAllFields, "offscreen")
                ? TryGetNullableBool(peer.IsOffscreen)
                : null,
            Selected = ShouldInclude(requestedFields, includeAllFields, "selected")
                ? GetSelected(peer)
                : null,
            Expanded = ShouldInclude(requestedFields, includeAllFields, "expanded")
                ? GetExpanded(peer)
                : null,
            Checked = ShouldInclude(requestedFields, includeAllFields, "checked")
                ? GetChecked(peer)
                : null,
            Value = ShouldInclude(requestedFields, includeAllFields, "value")
                ? TryGetValue(peer)
                : null,
            Bounds = ShouldInclude(requestedFields, includeAllFields, "bounds")
                ? TryGetBounds(peer)
                : null,
            Actions = ShouldInclude(requestedFields, includeAllFields, "actions")
                ? GetActions(peer)
                : null,
            State = ShouldInclude(requestedFields, includeAllFields, "state")
                ? GetState(peer)
                : null,
            Metadata = includeMetadata ? metadata : null,
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

        if (SupportsExpandCollapse(peer))
        {
            actions.Add(BridgeAction.Expand);
            actions.Add(BridgeAction.Collapse);
        }

        if (TryGetBool(peer.IsKeyboardFocusable))
            actions.Add(BridgeAction.SetFocus);

        if (SupportsShowContextMenu(peer))
            actions.Add(BridgeAction.ShowContextMenu);

        if (TryGetProvider<IScrollProvider>(peer) is not null)
        {
            actions.Add(BridgeAction.Scroll);
            actions.Add(BridgeAction.SetScrollPercent);
        }

        if (peer is ControlAutomationPeer)
            actions.Add(BridgeAction.Screenshot);

        return actions.ToArray();
    }

    private static bool SupportsShowContextMenu(AutomationPeer peer)
    {
        try
        {
            if (peer is not ControlAutomationPeer controlPeer)
                return false;

            for (Control? control = controlPeer.Owner; control is not null; control = control.Parent as Control)
            {
                if (control.ContextMenu is not null)
                    return true;
            }
        }
        catch
        {
            return false;
        }

        return false;
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
            return GetExpandCollapseState(peer) switch
            {
                null => null,
                Automation.ExpandCollapseState.Expanded => true,
                Automation.ExpandCollapseState.PartiallyExpanded => true,
                Automation.ExpandCollapseState.Collapsed => false,
                Automation.ExpandCollapseState.LeafNode => null,
                _ => null,
            };
        }
        catch
        {
            return null;
        }
    }

    internal static bool SupportsExpandCollapse(AutomationPeer peer)
        => GetExpandCollapseState(peer) is { } state
           && state != Automation.ExpandCollapseState.LeafNode;

    private static Automation.ExpandCollapseState? GetExpandCollapseState(AutomationPeer peer)
    {
        try
        {
            return TryGetProvider<IExpandCollapseProvider>(peer)?.ExpandCollapseState;
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

    private static bool ShouldInclude(HashSet<string>? requestedFields, bool includeAllFields, string field)
        => includeAllFields || requestedFields!.Contains(field);

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
        => TryGetRoleOrNull(peer) ?? "custom";

    internal static string? TryGetRoleOrNull(AutomationPeer peer)
    {
        try
        {
            return GetRole(peer.GetAutomationControlType());
        }
        catch
        {
            return null;
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
        if (!TrySplitStructuredSegments(body, out var segments))
            return false;

        var parsed = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["sourceType"] = sourceType,
        };

        foreach (var segment in segments)
        {
            if (!TryParseStructuredSegment(segment, out var key, out var value))
                return false;

            parsed[key] = value;
        }

        metadata = parsed;
        return true;
    }

    private static bool TrySplitStructuredSegments(string body, out List<string> segments)
    {
        segments = [];
        var start = 0;
        var delimiterDepth = 0;
        var inQuotes = false;

        for (var i = 0; i < body.Length; i++)
        {
            var current = body[i];

            if (current == '"' && !IsEscaped(body, i))
            {
                inQuotes = !inQuotes;
                continue;
            }

            if (inQuotes)
                continue;

            switch (current)
            {
                case '{':
                case '[':
                case '(':
                    delimiterDepth++;
                    break;
                case '}':
                case ']':
                case ')':
                    delimiterDepth--;
                    if (delimiterDepth < 0)
                        return false;
                    break;
                case ',' when delimiterDepth == 0:
                    segments.Add(body[start..i].Trim());
                    start = i + 1;
                    break;
            }
        }

        if (inQuotes || delimiterDepth != 0)
            return false;

        segments.Add(body[start..].Trim());
        segments.RemoveAll(string.IsNullOrWhiteSpace);
        return segments.Count > 0;
    }

    private static bool TryParseStructuredSegment(string segment, out string key, out string value)
    {
        key = string.Empty;
        value = string.Empty;

        var separatorIndex = FindTopLevelEquals(segment);
        if (separatorIndex <= 0 || separatorIndex >= segment.Length - 1)
            return false;

        key = segment[..separatorIndex].Trim();
        value = segment[(separatorIndex + 1)..].Trim();
        return key.Length > 0 && value.Length > 0;
    }

    private static int FindTopLevelEquals(string segment)
    {
        var delimiterDepth = 0;
        var inQuotes = false;

        for (var i = 0; i < segment.Length; i++)
        {
            var current = segment[i];

            if (current == '"' && !IsEscaped(segment, i))
            {
                inQuotes = !inQuotes;
                continue;
            }

            if (inQuotes)
                continue;

            switch (current)
            {
                case '{':
                case '[':
                case '(':
                    delimiterDepth++;
                    break;
                case '}':
                case ']':
                case ')':
                    delimiterDepth--;
                    break;
                case '=' when delimiterDepth == 0:
                    return i;
            }
        }

        return -1;
    }

    private static bool IsEscaped(string value, int index)
        => index > 0 && value[index - 1] == '\\';

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
