using System;
using System.Collections.Generic;
using Avalonia.AutomationBridge.Protocol.Messages;

namespace Avalonia.Diagnostics.AutomationBridge.Snapshot;

internal static class NodeSummaryProjection
{
    public static NodeSummaryDto[] Apply(NodeSummaryDto[] nodes, string[]? fields)
    {
        if (fields is not { Length: > 0 })
            return nodes;

        var requested = new HashSet<string>(fields, StringComparer.OrdinalIgnoreCase);
        var projected = new NodeSummaryDto[nodes.Length];

        for (var i = 0; i < nodes.Length; i++)
        {
            var node = nodes[i];
            projected[i] = new NodeSummaryDto
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

        return projected;
    }

    private static bool Include(HashSet<string> requested, string field)
        => requested.Contains(field);
}
