using System.Collections.Generic;
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
        var rect = peer.GetBoundingRectangle();
        // Always emit bounds; an all-zero rect is still a valid (if unpositioned) answer.
        var bounds = new double[] { rect.X, rect.Y, rect.Width, rect.Height };

        return new NodeSummaryDto
        {
            Id = handle,
            RootId = rootId,
            Role = GetRole(peer.GetAutomationControlType()),
            Name = NullIfEmpty(peer.GetName()),
            AutomationId = NullIfEmpty(peer.GetAutomationId()),
            ClassName = NullIfEmpty(peer.GetClassName()),
            Enabled = peer.IsEnabled(),
            Focused = peer.HasKeyboardFocus(),
            Offscreen = peer.IsOffscreen(),
            Value = peer.GetProvider<IValueProvider>()?.Value,
            Bounds = bounds,
            Actions = GetActions(peer),
        };
    }

    // -------------------------------------------------------------------------
    // Role mapping
    // -------------------------------------------------------------------------

    private static string GetRole(AutomationControlType type) => type switch
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

    private static string[] GetActions(AutomationPeer peer)
    {
        var actions = new List<string>();

        if (peer.GetProvider<IInvokeProvider>() is not null)
            actions.Add("invoke");

        if (peer.GetProvider<IValueProvider>() is { IsReadOnly: false })
            actions.Add("setValue");

        if (peer.GetProvider<IToggleProvider>() is not null)
            actions.Add("toggle");

        if (peer.GetProvider<ISelectionItemProvider>() is not null)
            actions.Add("select");

        if (peer.GetProvider<IExpandCollapseProvider>() is not null)
        {
            actions.Add("expand");
            actions.Add("collapse");
        }

        if (peer.IsKeyboardFocusable())
            actions.Add("setFocus");

        return actions.ToArray();
    }

    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    private static string? NullIfEmpty(string? value)
        => string.IsNullOrEmpty(value) ? null : value;
}
