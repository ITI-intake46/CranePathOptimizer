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
