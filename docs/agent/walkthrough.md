# Agent Walkthrough

## Phase 7 IFC Assembly Mark Flow

1. IFC import/audit uses `CentralIfcPropertyAliasResolver` as the single source for canonical identity. Do not add a second resolver for Tekla marks.
2. Generic and Tekla profiles both include uppercase Tekla-style aliases such as `ASSEMBLY_POS` and `CAST_UNIT_POS`; the resolver searches property names case-insensitively across all property sets.
3. `IfcDiagnosticAuditService.GenerateReportsAsync` emits three IFC audit artifacts under `<ProjectRoot>/Reports/IFC_Audit/`: the existing details JSON, existing elements CSV, and the Phase 7 `debug_ifc_check_assembly_mark_{timestamp}.csv`.
4. Normalized project/F5 elements keep their original `PrecastElement` records and canonical fields (`AssemblyMark`, source, confidence, explicit flag, validation status/warnings).
5. F8 gating still filters unsafe members first: `IncludeInGa`, role, positive trusted weight, parent-child double-count protection, and valid COG are checked before GA input building.
6. `OptimizationInputBuilder` then groups repeated explicit high-confidence assembly marks into synthetic GA lift units. These use `ElementID = ASM:{AssemblyMark}`, `Role = LiftAssembly`, summed weight, weighted COG, and `ChildGlobalIds` for traceability.
7. Weak fallbacks and placeholders such as generic `WALL`/`BEAM` are intentionally not grouped.

Keep report-layout work separate from this flow; Phase 6B report closure notes remain in `docs/reports/remaining-work-on-reports.md`.
## Phase 7B IFC Identity Contract

Use this contract whenever changing IFC import, persistence, F5, F8, reports, or diagnostics:

1. `ElementID` is the IFC `GlobalId` for imported elements. Do not replace it with `ASSEMBLY_POS`, `CAST_UNIT_POS`, `ElementMark`, or a user-facing display label.
2. `ElementName` is the IFC product/user object name (`IIfcProduct.Name`) or preview name fallback. Do not silently fill it from the assembly mark during project save/reload.
3. `AssemblyMark` is the normalized production assembly/cast-unit mark. It can drive F8 grouping and user-facing labels, but it is not a database identity.
4. F5 should show separate identity rows: IFC GUID, IFC Name, IFC Type, Material, Display Label / Lift Mark, Assembly Mark / Assembly Position, confidence, explicit flag, validation status, and warnings.
5. Reports and GA diagnostics may display `CanonicalMarkResolver.Resolve(...).DisplayLabel`, but row `ElementId` remains the true element identity.
6. The assembly audit CSV starts with `GlobalId, ElementID, ElementName, ElementType, RawName, RawTag, RawAssemblyPos, RawCastUnitPos, NormalizedAssemblyMark, ResolvedDisplayMark, CanonicalDisplayLabel, AssemblyMarkSource, AssemblyMarkConfidence, IsExplicitAssemblyMark, ValidationStatus, ValidationWarnings` and then appends grouping/weight/COG traceability columns.

Root cause note for future agents: the `ID = W5102` screenshot was not an IFC GUID overwrite in the importer. It came from F5 choosing a readable mark for the ID row plus persistence falling back from missing `ElementName` to `ElementMark`. Keep those responsibilities separate.

Deferred: Phase 7D may add review/correction UI for ambiguous marks; Phase 7E may clean legacy field names and fallbacks after a migration plan exists. Do not delete fields as part of Phase 7B-style work.

## GA Result Snapshot Flow

Use this contract when changing GA execution, result display, setup selection, reports, PDF/Excel export, or diagnostics:

1. Generate `OptimizationRunId` before GA starts. Multi-stage runs share that ID and preserve each stage's `StageRunId`, `StageIndex`, effective settings, timing, and selected-final flag.
2. After GA returns an Application-level `GaExecutionResult`, map `GaResultSnapshotDto` before reducing the result for UI display. Do not serialize `PlacementResult`, `GASettings`, `PlacementInput`, EF entities, or ViewModels directly.
3. Save the snapshot through `IGaResultSnapshotStore` to `<ProjectRoot>/Reports/GA/<OptimizationRunId>/ga_result_snapshot.json`. The same centralized output path service must also be used by GA CSV diagnostics; do not hardcode crane folders such as `32`.
4. Atomic save writes `ga_result_snapshot.json.tmp`, round-trips and validates it, computes SHA-256, moves it to `ga_result_snapshot.json`, writes `ga_result_snapshot.sha256`, and writes/rebuilds `ga_result_manifest.json`.
5. Completed snapshots are immutable. Current/Historic/Foreign/Corrupt/Unsupported classification is calculated during load and is never written back into historic snapshots.
6. `LiftUnits` stores static lift-unit/assembly metadata once. `Positions[].Evaluations` stores the full final selected setup position x submitted lift-unit matrix. `FinalOutcomes` stores one final resolution per lift unit by references to evaluation IDs.
7. Final setup counts use unique final ownership only: `IsFinalAssignedToPoint && LiftStatus == Liftable`. Do not count every feasible evaluation.
8. Post-GA reports resolve one `ResolvedGaResultSnapshot` in priority order: selected persisted run, matching current-session snapshot, latest current production JSON, latest historic same-project JSON with warning, legacy CSV adapter, no result.
9. Element List, Rendered Elements, and GA Readiness are current-model pre-GA reports. Basic Lifting Result, GA Result Report, Capacity Utilization Review, Failed Lifts Only, Crane Setup Summary, GA Assignment Review, and All Feasible Debug use the saved snapshot.
10. `CoverageRatio` is stored as a 0-to-1 ratio and displayed with `P2`. Report capacity values use saved `RequiredCapacityTon`, `AvailableCapacityTon`, `SafetyMarginTon`, and `CapacityUtilizationPercent`; do not derate again.
11. Report column definitions carry evidence scope and supported report-family metadata. Do not add post-GA columns to Element List or GA Readiness templates. Older user templates must be sanitized for the active family instead of enriching pre-GA rows from `LiftingResults`.
12. `RequiredBoomLength` is separate from generic boom length. Lifting reports map required boom from saved required-boom fields first, then best-failed required-boom evidence, and only use generic boom length as a legacy fallback.
13. Display marks fall back through `CanonicalMark`, `ElementMark`, and `ElementName`; technical IDs such as `LiftUnitId` and `SourceElementGlobalId` remain explicit ID columns and are not mark fallbacks.

## GA Result Reopen Flow

Use this lifecycle when changing project open/close, Analyze, View Results, or report entry points:

1. Project open captures `GaProjectActivationContext` once the project root and GUID are known. Run descriptor discovery may start before viewport rendering, then reclassify when model fingerprint or IFC SHA-256 evidence becomes available.
2. Run discovery scans `<ProjectRoot>/Reports/GA` recursively. Do not depend on `Reports/GA/History`, a fixed folder depth, a crane/spec folder, or a literal folder such as `32`.
3. The descriptor list is lightweight. Use `ga_result_manifest.json` where possible and only load the full `ga_result_snapshot.json` matrix when View Results, a selected-run open, or a post-GA report needs it.
4. Classification is shared: same project plus matching model fingerprint is `Current`; same project plus matching IFC SHA-256 is `CurrentByIfcHash`; same project with missing current model evidence is `CurrentProjectUnverified`; proven mismatch is `Historic`; different GUID is `Foreign`.
5. Auto-selection preserves the user's selected run when possible, then prefers current/current-by-IFC/unverified production JSON, then historic same-project JSON, then supported legacy CSV. Never auto-select foreign, corrupt, unsupported, failed, cancelled, or running runs.
6. `CranePlacementViewModel.HasAvailableResult` is the command signal for View Results and Lifting Summary. `Result != null` only means a result is already loaded into the current presentation.
7. Opening a persisted result resolves the selected descriptor through `IGaResultSnapshotProvider`, maps it through `IGaSnapshotToUiResultMapper`, marks it read-only, selects the first saved setup, and defaults the view mode to Final Assignment.
8. Legacy CSV snapshots are partial. Final Assignment and Basic Lifting Result may work when assignment rows exist; Selected Point Feasibility, Assignment Review, and All Feasible Debug require `HasPositionEvaluationMatrix`.
9. Project close cancels pending discovery/loading, clears saved-run UI state, clears restored result/selected setup/status maps, and removes the project entry from `IGaCurrentResultStore`.
10. Post-GA report preview/export uses one immutable resolved run. Do not combine rows from a snapshot with crane/settings/history metadata from another run.
11. Persisted/read-only results are presentation state. Selecting a historic setup must not move or rewrite the active scene crane; only current-session/live GA results may drive crane placement.
12. The Lifting Summary command is a report entry point. It should resolve GA data through the report provider path, not open the Results window as a side effect.
13. After a successful GA run, refresh the saved-run descriptors through the active project context so the new `OptimizationRunId` appears without requiring manual Refresh.
14. Once a report preview exists, PDF and Excel exports must use that same `ReportDocumentDto`. Export-before-preview may build once and cache the document. Preserve `DocumentId`, `ReportBuildSessionId`, input signature, and source signature across Preview, PDF, and Excel.
15. Production Analyze UI must not expose a manual saved-runs Refresh button. Discovery/refresh is driven by project activation, project close/open, and successful GA persistence.
16. PDF export must keep every selected table column together on the same physical page. Use larger landscape pages and then bounded custom-width landscape pages; do not restore horizontal `Columns X of Y` banding.

## Normal User-Facing Report Flow

Use this contract when changing the normal report selector, report metadata, Preview, PDF, or Excel:

1. The normal selector exposes only two engineering reports: `Basic Element List` for the current canonical model state and `GA Result Report` for the selected persisted/current GA run.
2. `GA Readiness` is not a normal selectable report. Its useful current-model evidence belongs in Element List through `Included in GA`, `Validation Status`, and `Blocking Reasons`; do not add `Ready for GA` back to the normal default table.
3. GA Result Report is one row per submitted lift unit from the selected snapshot. Do not drop failed outcomes and do not enrich missing schema-v1 shared fields from the current IFC/model.
4. Normal report table columns must use user-facing names with units, such as `Length (m)`, `Weight (t)`, and `COG X (m)`.
5. Do not put internal IDs or signatures in normal report metadata. `DocumentId`, `ReportBuildSessionId`, input signature, source signature, checksum values, evaluation IDs, and assignment scores remain diagnostics/test evidence.
6. Generate one `ReportReference` per immutable `ReportDocumentDto`. Preview, PDF, and Excel must display the same reference and export the same rows/metadata.
7. `ReportBuilderViewModel.InitializeAsync` must await template loading. Do not reintroduce `.Result`, `.Wait()`, or `.GetAwaiter().GetResult()` into the WPF report-opening path.
