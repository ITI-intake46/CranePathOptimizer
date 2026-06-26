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
