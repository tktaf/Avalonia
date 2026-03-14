# Automation Bridge Authoring

`Avalonia.Diagnostics.AutomationBridge` can only expose the automation surface your app provides. If a screen has stable identifiers, clean names, and meaningful state, the bridge is reliable and cheap to query. If it does not, callers fall back to broad text scans and inference.

This document describes the app-side conventions that make the bridge useful for both manual CLI sessions and agent-driven automation.

## Prefer Stable `AutomationId` Values

Every actionable control should expose a stable `AutomationId`.

Good candidates:

- primary and secondary command buttons
- text inputs and searchable filters
- tabs
- list and grid row affordances
- modal confirmation and dismissal buttons
- wizard navigation buttons

Treat `AutomationId` as part of the automation contract. It should describe the domain meaning of the control, not its incidental visual placement.

Good:

- `launch-franchise`
- `player-profile-contract-tab-button`
- `franchise-gm-name-textbox`

Bad:

- `button1`
- `left-panel-button`
- `stackpanel-child-3`

## Keep Node Names Human-Readable

The bridge now sanitizes common structured object `ToString()` output into a smaller label plus metadata, but apps should still prefer explicit names over relying on object formatting.

Set a clear automation-facing name when the default content would otherwise come from:

- a view model object
- a DTO record
- a generated display object

Prefer labels such as:

- `The Analyst`
- `Boston Minutemen`
- `Contract Details`

Avoid exposing full object dumps as the primary automation name.

## Publish Meaningful State

The bridge exposes generic state such as:

- `selected`
- `expanded`
- `checked`

Apps should make these states observable through normal Avalonia automation peers and providers instead of forcing callers to infer them from surrounding text.

Examples:

- selected tabs should expose selection semantics
- expander rows should expose expand/collapse semantics
- toggle controls should expose toggle state

## Use Automation Metadata Deliberately

When a node represents a repeated card, row, or item, attach stable metadata through the normal automation surface instead of packing everything into the display name.

Useful sources:

- `AutomationProperties.AutomationId`
- `AutomationProperties.ItemType`
- `AutomationProperties.ItemStatus`
- `AutomationProperties.HelpText`

Recommended uses:

- item type such as `team-option`, `player-row`, or `transaction-card`
- item status such as `busy`, `inactive`, or `week-4`
- concise help text for extra context that should not become the primary node label

## Repeated Rows, Cards, and List Items

Repeated containers are where automation quality usually falls apart first.

For repeated content:

- give each actionable child a stable `AutomationId`
- make the row label concise and domain-meaningful
- expose selection state when the row is selectable
- expose item identity through metadata instead of positional naming

Prefer:

- row label: `James Brown`
- action id: `player-row-open-profile`
- metadata: item type `player-row`, item status `wr-3`

Avoid:

- row label derived from raw object `ToString()`
- ids that depend on render order
- automation that only works after scanning large text dumps

## Wizards, Tabs, and Modals

These surfaces need explicit automation treatment because callers often synchronize on them.

For wizard screens:

- expose stable ids for next, back, and submit actions
- name each step clearly
- expose selected step controls through selection semantics where applicable

For tabs:

- each tab should have a stable `AutomationId`
- the active tab should expose `selected = true`

For modals:

- confirm and dismiss actions need stable ids
- modal content should have a clear title/name
- disabled background affordances are useful, but not a substitute for identifying the modal itself

## Async Commands

Async commands are much easier to automate when the UI publishes observable state after the command starts.

Prefer surfaces that change one of:

- visible status text
- selected/expanded state
- enabled/disabled state
- item status metadata

Do not rely on silent command execution where the only proof of progress is a later screen dump.

## Query Efficiency Guidance

Apps that follow these conventions let bridge clients stay precise:

- query by `automationId`
- filter by `selected`, `enabled`, `visible`, or `hasAction`
- request only the fields they need

That is materially cheaper than broad `role=text` scans over dense screens.

## Summary

If an app wants to be automation-friendly:

- stable `AutomationId` values are required for important actions
- names should be concise and human-readable
- repeated items should publish identity and status through metadata, not object dumps
- meaningful UI state should be exposed directly through automation semantics

The bridge is strongest when the app surface is authored intentionally instead of being treated as an afterthought.
