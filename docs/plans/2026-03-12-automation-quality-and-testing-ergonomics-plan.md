# Automation Quality and Testing Ergonomics Plan

## Goal

Improve Avalonia AutomationBridge so real app automation is more deterministic, cheaper to drive, and easier for app teams to author correctly.

## Non-goals

- Rework consumer apps in this milestone.
- Remove or weaken existing bridge behavior to simplify implementation.
- Introduce app-specific shortcuts or hardcoded selectors.

## Acceptance Criteria

- Queries can target higher-value UI state directly and can project only the requested fields.
- Node summaries and deltas expose more explicit automation-relevant state without bloating default responses.
- Action responses communicate observable completion more deterministically for same-node and closely related state changes.
- CLI workflows support low-friction automation-id-first inspection and waiting flows.
- Avalonia app authors have explicit guidance for bridge-friendly automation surfaces.
- Automated tests cover protocol, selection, snapshot, action, and CLI behavior changes.

## Relevant Code Surface

- `src/Avalonia.AutomationBridge.Protocol/Messages/SelectorDto.cs`
- `src/Avalonia.AutomationBridge.Protocol/Messages/NodeSummaryDto.cs`
- `src/Avalonia.AutomationBridge.Protocol/Messages/BridgeRequest.cs`
- `src/Avalonia.AutomationBridge.Protocol/Messages/DeltaDto.cs`
- `src/Avalonia.Diagnostics.AutomationBridge/Selection/AutomationSelectorEvaluator.cs`
- `src/Avalonia.Diagnostics.AutomationBridge/Snapshot/AutomationNodeSummaryBuilder.cs`
- `src/Avalonia.Diagnostics.AutomationBridge/Snapshot/AutomationDeltaBuilder.cs`
- `src/Avalonia.Diagnostics.AutomationBridge/Actions/AutomationActionDispatcher.cs`
- `src/Avalonia.Diagnostics.AutomationBridge/Transport/AutomationBridgeRequestDispatcher.cs`
- `src/tools/Avalonia.AutomationBridge.Cli/AutomationBridgeCliRunner.cs`
- `tests/Avalonia.Diagnostics.AutomationBridge.Tests/Selection/SelectorTests.cs`
- `tests/Avalonia.Diagnostics.AutomationBridge.Tests/Session/AutomationNodeSummaryBuilderTests.cs`
- `tests/Avalonia.Diagnostics.AutomationBridge.Tests/Snapshot/DeltaTests.cs`
- `tests/Avalonia.Diagnostics.AutomationBridge.Tests/Actions/ActionDispatchTests.cs`
- `tests/Avalonia.Diagnostics.AutomationBridge.Tests/Cli/AutomationBridgeCliTests.cs`

## Risks and Tradeoffs

- Adding state fields without projection controls would increase response size and token cost.
- Changing naming behavior too aggressively could break consumers depending on current labels.
- Action completion semantics must remain honest; fake “completed” responses without observable state change would make the bridge less trustworthy.
- CLI ergonomics depend on protocol shape, so issue ordering matters.

## Task List

### Task 1: Add selector filters and field projection

- **Issue:** `#16`
- **Objective:** Make queries target automation-relevant state precisely and return only requested fields.
- **Files:**
  - `src/Avalonia.AutomationBridge.Protocol/Messages/SelectorDto.cs`
  - `src/Avalonia.AutomationBridge.Protocol/Messages/BridgeRequest.cs`
  - `src/Avalonia.Diagnostics.AutomationBridge/Selection/AutomationSelectorEvaluator.cs`
  - `src/Avalonia.Diagnostics.AutomationBridge/Transport/AutomationBridgeRequestDispatcher.cs`
  - `tests/Avalonia.Diagnostics.AutomationBridge.Tests/Selection/SelectorTests.cs`
  - `tests/Avalonia.Diagnostics.AutomationBridge.Tests/Transport/RequestDispatcherTests.cs`
- **Tests:** Add or update tests first for `selected`, `offscreen`, and action-capability filtering plus requested-field projection behavior.
- **Commands:**
  - `dotnet test --project tests/Avalonia.Diagnostics.AutomationBridge.Tests/Avalonia.Diagnostics.AutomationBridge.Tests.csproj -v minimal -- --filter-class Avalonia.Diagnostics.AutomationBridge.Tests.Selection.SelectorTests`
  - `dotnet test --project tests/Avalonia.Diagnostics.AutomationBridge.Tests/Avalonia.Diagnostics.AutomationBridge.Tests.csproj -v minimal -- --filter-class Avalonia.Diagnostics.AutomationBridge.Tests.Transport.RequestDispatcherTests`

### Task 2: Expand summary and delta state semantics

- **Issue:** `#17`
- **Objective:** Expose explicit UI state that removes ambiguity without forcing broad re-queries.
- **Files:**
  - `src/Avalonia.AutomationBridge.Protocol/Messages/NodeSummaryDto.cs`
  - `src/Avalonia.AutomationBridge.Protocol/Messages/DeltaDto.cs`
  - `src/Avalonia.Diagnostics.AutomationBridge/Snapshot/AutomationNodeSummaryBuilder.cs`
  - `src/Avalonia.Diagnostics.AutomationBridge/Snapshot/AutomationDeltaBuilder.cs`
  - `tests/Avalonia.Diagnostics.AutomationBridge.Tests/Session/AutomationNodeSummaryBuilderTests.cs`
  - `tests/Avalonia.Diagnostics.AutomationBridge.Tests/Snapshot/DeltaTests.cs`
  - `tests/Avalonia.Diagnostics.AutomationBridge.Tests/Protocol/NodeSummarySerializationTests.cs`
  - `tests/Avalonia.Diagnostics.AutomationBridge.Tests/Protocol/DeltaSerializationTests.cs`
- **Tests:** Add or update tests first for selected, expanded, checked, state, and metadata projection/serialization.
- **Commands:**
  - `dotnet test --project tests/Avalonia.Diagnostics.AutomationBridge.Tests/Avalonia.Diagnostics.AutomationBridge.Tests.csproj -v minimal -- --filter-class Avalonia.Diagnostics.AutomationBridge.Tests.Session.AutomationNodeSummaryBuilderTests`
  - `dotnet test --project tests/Avalonia.Diagnostics.AutomationBridge.Tests/Avalonia.Diagnostics.AutomationBridge.Tests.csproj -v minimal -- --filter-class Avalonia.Diagnostics.AutomationBridge.Tests.Snapshot.DeltaTests`

### Task 3: Strengthen action completion semantics

- **Issue:** `#18`
- **Objective:** Make action responses reflect observable completion for same-node and related-node changes without overstating success.
- **Files:**
  - `src/Avalonia.Diagnostics.AutomationBridge/Actions/AutomationActionDispatcher.cs`
  - `src/Avalonia.Diagnostics.AutomationBridge/Snapshot/AutomationDeltaBuilder.cs`
  - `tests/Avalonia.Diagnostics.AutomationBridge.Tests/Actions/ActionDispatchTests.cs`
- **Tests:** Add or update tests first for related-node change detection, no-change outcomes, and preservation of event-driven completion behavior.
- **Commands:**
  - `dotnet test --project tests/Avalonia.Diagnostics.AutomationBridge.Tests/Avalonia.Diagnostics.AutomationBridge.Tests.csproj -v minimal -- --filter-class Avalonia.Diagnostics.AutomationBridge.Tests.Actions.ActionDispatchTests`

### Task 4: Improve CLI targeting and synchronization

- **Issue:** `#19`
- **Objective:** Make the CLI efficient for both humans and agents on top of the stronger protocol/query surface.
- **Files:**
  - `src/tools/Avalonia.AutomationBridge.Cli/AutomationBridgeCliRunner.cs`
  - `src/tools/Avalonia.AutomationBridge.Cli/Program.cs`
  - `tests/Avalonia.Diagnostics.AutomationBridge.Tests/Cli/AutomationBridgeCliTests.cs`
- **Tests:** Add or update tests first for automation-id-first commands, field projection, inspection helpers, and `wait-for` behavior.
- **Commands:**
  - `dotnet test --project tests/Avalonia.Diagnostics.AutomationBridge.Tests/Avalonia.Diagnostics.AutomationBridge.Tests.csproj -v minimal -- --filter-class Avalonia.Diagnostics.AutomationBridge.Tests.Cli.AutomationBridgeCliTests`

### Task 5: Document bridge-friendly app authoring conventions

- **Issue:** `#20`
- **Objective:** Give app teams a stable contract for exposing automation surfaces that work well with the bridge.
- **Files:**
  - `src/Avalonia.Diagnostics.AutomationBridge/README.md` or a new bridge-specific guidance doc under `docs/`
  - any nearby examples/tests updated to reflect the conventions
- **Tests:** Add or update example-oriented tests only where they enforce real bridge behavior, not documentation wording.
- **Commands:**
  - `dotnet test --project tests/Avalonia.Diagnostics.AutomationBridge.Tests/Avalonia.Diagnostics.AutomationBridge.Tests.csproj -v minimal`

## Final Verification Checklist

- `dotnet test --project tests/Avalonia.Diagnostics.AutomationBridge.Tests/Avalonia.Diagnostics.AutomationBridge.Tests.csproj -v minimal`
- `dotnet build src/Avalonia.Diagnostics.AutomationBridge/Avalonia.Diagnostics.AutomationBridge.csproj -warnaserror`
- `dotnet build src/tools/Avalonia.AutomationBridge.Cli/Avalonia.AutomationBridge.Cli.csproj -warnaserror`
- Fresh manual validation against a consumer app using targeted queries and actions:
  - automation-id-first query
  - selected-state query
  - action with immediate completion evidence
  - CLI wait/inspect flow
