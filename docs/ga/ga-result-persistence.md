# GA Result Persistence and Reopen Contract

`ga_result_snapshot.json` is the production GA result persistence format. Memory is only the current-session fast path, and CSV remains audit/research evidence plus legacy fallback.

## Save Location

Successful GA runs save beside the existing GA audit CSV files:

`<ProjectRoot>/Reports/GA/<OptimizationRunId>/ga_result_snapshot.json`

The same run folder also contains `ga_result_snapshot.sha256`, `ga_result_manifest.json`, and any existing `ga_audit_*.csv` diagnostics. Code must use the centralized project output path services and must not assume crane-specific folders or fixed folder depth.

## Discovery

On project activation, the application lists saved runs by recursively scanning `<ProjectRoot>/Reports/GA/**` for production snapshots and legacy GA CSV folders. Manifests are used for lightweight descriptors. The full position-by-lift-unit matrix is deserialized only when opening View Results or building/exporting a post-GA report.

Production JSON wins over legacy CSV when both describe the same `OptimizationRunId`. Completed snapshots are immutable; discovery and reports never rewrite a completed result when the project name, model, or crane catalog changes.

## Classification

Classification is calculated by `IGaSnapshotClassificationService`:

- `Current`: same project GUID and matching model fingerprint.
- `CurrentByIfcHash`: same project GUID and matching IFC SHA-256 when fingerprints are missing or differ.
- `CurrentProjectUnverified`: same project GUID, but current model evidence is not available yet.
- `Historic`: same project GUID with reliable saved/current model evidence that differs.
- `Foreign`: different project GUID.
- `Corrupt`: invalid JSON, validation, or checksum.
- `Unsupported`: unsupported schema or required unsupported contract values.

A null current model fingerprint is not a reason to mark a run historic.

## Restore

`IGaSnapshotToUiResultMapper` maps snapshots into the read-only GA result presentation. It restores saved setup positions, final setup ownership counts, final assignment rows, selected-point feasibility from the saved matrix, best-failed evidence, grouped lift identity, and saved load-chart engineering values. It never reruns the optimizer and never rebuilds capacities from the current crane catalog.

Technical joins use `LiftUnitId`, `SourceElementGlobalId`, parent/main assembly IDs, and evaluation IDs. Display marks remain display metadata and must not replace IFC or lift-unit identity.

Read-only restored results must not mutate the active project scene crane. Setup selection in the results window may update the saved-result table, status map, and selected setup marker, but historical/persisted results do not reposition or reconfigure the live crane in the current scene.

After a normal successful optimization returns from the optimizer dialog, the Analyze saved-run list is refreshed through the project-bound snapshot provider. The new `OptimizationRunId` should appear and become selected without requiring the user to press Refresh.

Lifting Summary opens the Lifting Result report builder from the selected/current resolved run. It must not open the results window merely to materialize report data.

Preview, PDF, and Excel exports use the same immutable `ReportDocumentDto` once preview has been generated. Export-before-preview builds and caches one document, then exports that document; exports do not rebuild rows from changing live UI state.

## Report Family Separation

Element List, Rendered Elements, and GA Readiness are current-model reports. Their template columns are limited to current-model/readiness evidence and older user templates are sanitized when loaded so post-GA columns cannot silently pull data from a saved run, diagnostics CSV, or current-session result.

Post-GA Lifting Result reports are snapshot-backed. They resolve one run through `IGaResultSnapshotProvider` and `IGaSnapshotReportAdapter`; all rows, metadata, warnings, preview, PDF export, and Excel export come from that single resolved source.

Saved engineering fields are used as-is. Required boom length maps from saved required-boom evidence, not generic boom length. Display marks fall back through mark/name fields only; technical IDs stay in explicit identity columns.

## Legacy CSV

Legacy discovery supports nested GA run folders and audit-prefixed filenames such as `ga_audit_run_summary.csv`, `ga_audit_positions.csv`, and `ga_audit_element_assignments.csv`. Legacy snapshots use schema version 0 and capability flags to identify missing data.

Legacy final assignment and basic engineering reports may be available from assignment CSV rows. Selected-point feasibility, assignment review, and all-feasible debug remain disabled unless a complete position/evaluation matrix exists.
