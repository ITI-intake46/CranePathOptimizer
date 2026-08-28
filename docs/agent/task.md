# Current Task

## Phase 7B -- IFC Schema Contract & Normalized Identity Mapping

Implemented:

- IFC normalized import now keeps `ElementID` as IFC `GlobalId` and sets `ElementName` from raw IFC `IIfcProduct.Name` / preview name instead of letting Tekla assembly marks occupy the name slot.
- Project persistence no longer stores `DisplayName = ElementName ?? ElementMark`; save/reload preserves IFC Name separately from `ElementMark` and `AssemblyMark`.
- F5 Element Management now shows separate identity rows for `IFC GUID`, `IFC Name`, `IFC Type`, and `Material`, plus canonical rows for `Display Label / Lift Mark` and `Assembly Mark / Assembly Position`.
- `debug_ifc_check_assembly_mark_{timestamp}.csv` begins with the Phase 7B identity fields: `GlobalId`, `ElementID`, `ElementName`, `ElementType`, `RawName`, `RawTag`, `RawAssemblyPos`, `RawCastUnitPos`, `NormalizedAssemblyMark`, `ResolvedDisplayMark`, `CanonicalDisplayLabel`, `AssemblyMarkSource`, `AssemblyMarkConfidence`, `IsExplicitAssemblyMark`, `ValidationStatus`, `ValidationWarnings`; existing grouping/weight/COG/exporter/action columns remain appended.
- Added tests covering canonical resolver identity separation, report row identity separation, project persistence round-trip, existing IFC alias resolution, and GA assembly grouping.
- Documented the field ownership matrix, exporter schema map, normalized project contract, screenshot root cause, minimal mapping fixes, audit columns, and deferred Phase 7D/7E work in `docs/domain/ifc.md`.

Root cause fixed:

- The screenshot state `ID = W5102`, `Name = W5102 - Wall`, `Display Label = W5102`, `Assembly Mark = -` came from F5 using a readable-id helper that preferred marks over GUIDs and from persistence filling missing `ElementName` from `ElementMark`. `ASSEMBLY_POS` is now kept as assembly/display metadata and does not replace GUID or IFC Name.

Verified:

- `dotnet build CraneOptimizer.sln` passes with existing warning noise.
- `dotnet test CraneOptimizer.Application.Tests\CraneOptimizer.Application.Tests.csproj --no-build --filter "FullyQualifiedName~CanonicalMarkResolverTests|FullyQualifiedName~UserFacingReportsTests|FullyQualifiedName~IfcSessionStabilizationTests|FullyQualifiedName~OptimizationInputBuilderTests"` passes: 95 tests.

Carry-forward:

- Phase 7D: add UI/review workflows for low-confidence or ambiguous assembly mark mappings.
- Phase 7E: plan long-term schema cleanup and field-name migration. Do not delete existing identity fields before migration/compatibility work is designed.

## GA Result Snapshot Persistence

Implemented:

- Successful GA execution now produces a versioned production snapshot contract, `ga_result_snapshot.json`, beside the run's GA audit files under `<ProjectRoot>/Reports/GA/<OptimizationRunId>/`.
- The snapshot is mapped from the Application execution envelope before the UI result projection, preserving `OptimizationRunId`, selected `StageRunId`, requested/effective settings, stage summaries, load-chart provenance, final setup positions, full selected-position evaluation matrix, final ownership, and best-failed evidence.
- `ga_result_snapshot.sha256` is the authoritative external checksum. `ga_result_manifest.json` is a rebuildable lightweight discovery sidecar.
- Memory is only the current-session fast path (`IGaCurrentResultStore`). JSON is the durable post-GA source. CSV remains audit/research output and legacy fallback.
- Report building resolves one immutable GA snapshot source and uses it for preview, PDF, and Excel document generation; post-GA reports do not mix current dialog settings, current crane selection, history rows, and unrelated diagnostics.
- Grouped and individual modes persist exactly from effective run settings. `LiftUnitId` is the submitted GA identity; `SourceElementGlobalId` remains the true IFC identity when applicable, and display marks are never used for joins.

Verified:

- `dotnet build CraneOptimizer.sln -p:Configuration=Debug -p:Platform=x64 -v:minimal` passes with existing warning noise.
- `dotnet test CraneOptimizer.Application.Tests\CraneOptimizer.Application.Tests.csproj --filter GaResultSnapshotPersistenceTests -p:Configuration=Debug -p:Platform=x64 --no-restore -v:minimal` passes: 6 tests.

## GA Result Reopen Discovery and Restore

Implemented:

- Project activation now captures a `GaProjectActivationContext` containing project root, project GUID, project name, IFC path/model evidence when available, and model revision. Main project navigation awaits GA discovery instead of relying on current-session memory.
- `CranePlacementViewModel` owns saved-run UI state: `SavedRuns`, `SelectedSavedRun`, discovery/loading flags, source/classification labels, and `HasAvailableResult`. View Results and Lifting Summary use this availability signal, not only `Result != null`.
- Production JSON run discovery scans `<ProjectRoot>/Reports/GA/**/ga_result_snapshot.json` recursively, uses manifests for descriptors, deduplicates JSON/CSV artifacts by run ID, and keeps production JSON authoritative over legacy CSV.
- Classification is centralized in `IGaSnapshotClassificationService`: `Current`, `CurrentByIfcHash`, `CurrentProjectUnverified`, `Historic`, `Foreign`, `Corrupt`, and `Unsupported`. A null current model fingerprint is unverified, not historic; matching IFC SHA-256 can establish current equivalence.
- `IGaModelIdentityService` builds current model identity from project-owned IFC SHA-256 when possible, with deterministic element projection as fallback.
- `IGaLegacyCsvSnapshotAdapter` recursively discovers legacy `ga_audit_*.csv` and older non-audit GA CSV names, parses RFC4180-style quoted CSV, and reconstructs partial schema-0 snapshots with final outcomes, positions, available engineering values, and honest capability flags.
- `IGaSnapshotToUiResultMapper` restores `GaResultSnapshotDto` into the read-only GA result presentation used by the result window, preserving setup IDs/counts, final ownership, selected-point feasibility matrix, grouped identity fields, and saved engineering/load-chart values.
- Report Builder prefers the selected Analyze saved run, then current session, then provider-selected production/legacy sources. Preview, PDF, and Excel build from one resolved snapshot path. Legacy Basic Lifting Result no longer requires a full matrix; matrix-only reports still require matrix capability.

Verified:

- `dotnet test CraneOptimizer.Application.Tests\CraneOptimizer.Application.Tests.csproj -p:Configuration=Debug -p:Platform=x64 --filter GaResultSnapshotPersistenceTests -v:minimal` passes: 9 tests.
- `dotnet test CraneOptimizer.Application.Tests\CraneOptimizer.Application.Tests.csproj -p:Configuration=Debug -p:Platform=x64 --filter ReportBuilderViewModelFlowTests -v:minimal` passes: 2 tests.
- `dotnet build CraneOptimizer.sln -p:Configuration=Debug -p:Platform=x64 -v:minimal` passes with existing warning noise.

## GA Result Hardening

Implemented:

- View Results for persisted/read-only snapshots no longer moves or reconfigures the live scene crane when a saved setup is selected. Saved setup selection still updates the result presentation and marker.
- Lifting Summary opens the report builder directly from the resolved GA run path instead of opening the Results window to populate data.
- Report preview, PDF export, and Excel export share one immutable `ReportDocumentDto`; exports no longer rebuild from potentially changed live state after preview.
- The Optimize command awaits a saved-run refresh after a successful optimizer dialog result, so a newly saved `OptimizationRunId` appears in `SavedRuns` and becomes selected without manual Refresh.
- Saved-run combo fallback display now returns the formatted run summary instead of a raw type name when WPF falls back to `ToString()`.
- Report columns now carry evidence-scope/family metadata. Element List, Rendered Elements, and GA Readiness are sanitized so saved user templates cannot pull post-GA result columns into pre-GA reports.
- GA Readiness rows are current-model/readiness rows only; they are not enriched from `LiftingResults`, current-session snapshots, legacy CSV rows, or unrelated diagnostics.
- Lifting Result required-boom mapping now prefers saved required-boom evidence over generic boom length, and display mark fallback no longer uses technical IDs.

Verified:

- `dotnet build CraneOptimizer.Application.Tests\CraneOptimizer.Application.Tests.csproj -p:Configuration=Debug -p:Platform=x64 -p:OutDir=C:\Users\m7md\AppData\Local\Temp\CraneOptimizerVerify\out\ -v:minimal` passes with existing warning noise.
- `dotnet build CraneOptimizer.sln -p:Configuration=Debug -p:Platform=x64 -v:minimal` passes with existing warning noise.
- `dotnet test CraneOptimizer.Application.Tests\CraneOptimizer.Application.Tests.csproj --no-build -p:Configuration=Debug -p:Platform=x64 -p:OutDir=C:\Users\m7md\AppData\Local\Temp\CraneOptimizerVerify\out\ --filter "FullyQualifiedName~UserFacingReportsTests|FullyQualifiedName~ReportBuilderViewModelFlowTests" -v:minimal` passes: 60 tests.
- `dotnet test CraneOptimizer.Application.Tests\CraneOptimizer.Application.Tests.csproj --no-build -p:Configuration=Debug -p:Platform=x64 -p:OutDir=C:\Users\m7md\AppData\Local\Temp\CraneOptimizerVerify\out\ --filter "FullyQualifiedName~GaResultSnapshot|FullyQualifiedName~GaResultWorkspace|FullyQualifiedName~CranePlacementViewModel" -v:minimal` passes: 9 tests.
- Full `CraneOptimizer.Application.Tests` through the normal build output currently fails: 800 passed, 24 failed, 3 skipped. Remaining failures are outside the focused report/snapshot checks and include diagnostics CSV files not being emitted in some fixtures, SQLite `ProjectInfo.CompanyName` schema mismatch, coordinate-reference report assertions, one Hungarian performance guardrail, viewport hit-test, and two GA gating tests.
- Full `CraneOptimizer.Application.Tests` through the custom output path currently fails: 784 passed, 41 failed, 3 skipped. The larger custom-output failure set adds missing Xbim native geometry assemblies and AI namespace contract tests to the normal-output failure families.

Additional hardening in this pass:

- The production Analyze saved-runs panel no longer exposes a manual Refresh button; saved-run synchronization remains automatic through activation and post-GA persistence refresh.
- Read-only restored result setup selection updates the result presentation marker but does not call back into the live `CranePlacementViewModel` setup-selection path and does not move the authoritative scene crane.
- User-facing PDF export no longer emits horizontal column bands. Wide reports choose a larger landscape page, then a bounded custom-width landscape page when A1 is still too narrow, and keep all selected columns in one table with document identity in the footer.

## Single Source Report Selector and Report Reference Hardening

Implemented:

- The normal Report Builder selector now exposes only `Basic Element List` for current-model reporting and `GA Result Report` for post-GA reporting. GA Readiness and older lifting/debug templates remain internal compatibility/diagnostic templates.
- Basic Element List default columns now carry the shared engineering set: mark, name, type, material, length/width/height, volume, weight, COG X/Y/Z, assembly member count, included-in-GA, validation status, and blocking reasons. `ReadyForGa` is no longer a normal user-facing column.
- GA Result Report default columns now use the same shared user-facing identity/engineering columns plus final/best-failed GA result evidence. Internal fields such as `LiftUnitId`, source/global IDs, assignment reason/score, final evaluation IDs, assembly member IDs, and report reference are not selected as primary user-facing table columns.
- `ReportDocumentDto.ReportReference` is generated once by the report builder and is preserved across Preview, PDF, and Excel. PDF/Excel user metadata and preview summaries show `Report Reference` instead of `DocumentId`, build session IDs, source signatures, or input signatures.
- Report dialog initialization is async. `ReportBuilderViewModel` awaits template-store loading through `InitializeAsync` and no longer blocks the WPF thread with `GetAwaiter().GetResult()` while opening the dialog.

Verified:

- `dotnet build CraneOptimizer.Application.Tests\CraneOptimizer.Application.Tests.csproj -p:Configuration=Debug -p:Platform=x64 -p:OutDir=C:\Users\m7md\AppData\Local\Temp\CraneOptimizerVerify\out\ -v:minimal` passes with existing warning noise.
- `dotnet test CraneOptimizer.Application.Tests\CraneOptimizer.Application.Tests.csproj --no-build -p:Configuration=Debug -p:Platform=x64 -p:OutDir=C:\Users\m7md\AppData\Local\Temp\CraneOptimizerVerify\out\ --filter "FullyQualifiedName~UserFacingReportsTests|FullyQualifiedName~ReportBuilderViewModelFlowTests" -v:minimal` passes: 64 tests.
- `dotnet test CraneOptimizer.Application.Tests\CraneOptimizer.Application.Tests.csproj --no-build -p:Configuration=Debug -p:Platform=x64 -p:OutDir=C:\Users\m7md\AppData\Local\Temp\CraneOptimizerVerify\out\ --filter "FullyQualifiedName~GaResultSnapshotPersistenceTests|FullyQualifiedName~CranePlacementViewModel" -v:minimal` passes: 9 tests.
- `dotnet build CraneOptimizer.sln -p:Configuration=Debug -p:Platform=x64 -v:minimal` passes with existing warning noise.
- Full `CraneOptimizer.Application.Tests` through the normal build output currently fails: 801 passed, 23 failed, 3 skipped. Remaining failures are outside this focused report-selector/reference slice and include diagnostics CSV output expectations, SQLite `ProjectInfo.CompanyName` schema mismatch, coordinate-reference report assertions, viewport hit-test, IFC audit/grouping expectations, and GA gating tests.

Carry-forward:

- `ReportReference` currently uses the immutable report build time plus run/model token. A future additive metadata field should carry saved GA run completion time separately when the UX requires the reference timestamp to be the run completion timestamp.
- Manual WPF acceptance and real Preview/PDF/Excel visual comparison were not completed in this pass.

## E-01 -- Load-Chart Capacity Unit Safety

Implemented:

- Removed the numeric-magnitude `> 1000` kg/t inference from CSV/PDF load-chart capacity handling.
- `Auto` now resolves only from unambiguous explicit `kg` or `t` evidence in capacity values; unitless or conflicting evidence remains unresolved.
- The load-chart editor shows raw and normalized maximum capacity separately, requires an explicit unit selection when Auto is unresolved, and disables/guards **Save Crane** until resolution succeeds.
- `LoadChartCatalogService.BuildCraneDto` independently rejects unresolved charts so ambiguous capacities cannot cross the persistence boundary.
- Saved catalog capacities are treated as canonical metric tons when reopened; exact-PDF source JSON is retained as provenance while the editable grid is reconstructed from persisted canonical points.
- PDF primary-table adjudication no longer normalizes candidate capacity by a numeric threshold. Capacity agreement is used only when explicit unit evidence is available.
- Regression coverage includes unitless values below, at, and above the former threshold (`800`, `999`, `1000`, `1001`, `1200`, and larger values), explicit kg/t evidence, manual unit selection, conflicting evidence, CSV default Auto behavior, exact-visual charts, persistence blocking, and PDF candidate capacity interpretation.
- Existing catalog rows created under the old heuristic are deliberately not auto-migrated because the persisted value alone does not contain enough provenance to distinguish a legitimate ton value from a misinterpreted kilogram value; affected legacy entries require source-chart verification.

Verification in this handoff environment:

- `git diff --check`, repository-source safety scans, XAML XML parsing, and Git object/bundle integrity checks pass.
- The handoff environment does not contain the .NET SDK/MSBuild, so the Windows WPF build and xUnit suite could not be executed here. Run the focused E-01 tests and the full solution test suite on the normal Windows .NET 8 development machine before merge.
