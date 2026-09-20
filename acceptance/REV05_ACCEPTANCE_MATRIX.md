# REV05 SOFTWARE ACCEPTANCE FIXTURE — acceptance matrix

This matrix maps every open REV02 re-review item to the consolidated next-submission implementation and evidence path.

| Gate | REV05 implementation / evidence |
| --- | --- |
| R-01 / E-01 capacity units | Current import/save path requires explicit unit evidence; no magnitude guessing. Existing focused tests remain in the restored suite. |
| R-02 / E-03 GA numeric inputs | Live field validation runs on input change, blocks Run, and rejects negative/non-finite rope/accessory values before optimization. |
| R-03 / E-02 minimum envelope | Direct and indexed resolvers reject below-minimum radius and boom; focused regression tests protect both paths. |
| R-04 / T-01 regression integrity | `acceptance/regression-baseline.json` enforces at least 114 test source files and 1,041 `[Fact]/[Theory]` declarations. All ten deleted files named by the re-review are present. See `release/REV05-TEST-BASELINE-RECONCILIATION.md`. |
| E-04 / T-03 IFC mass units | Production `IfcReaderRepository` delegates to strict explicit-unit → project-unit resolution. kg, tonne, converted pound, missing-unit, conflicting-unit, malformed-explicit and malformed-project fixtures are included. Published harness emits raw mass, detected unit, normalized mass, trust status, conflict state and GA eligibility. |
| E-05 load-chart validity | Save validation rejects every non-finite/non-positive engineering radius, boom length or capacity and validates the crane envelope. |
| E-06 operating condition / lines | Existing filtering remains active before capacity selection; regression coverage is retained. |
| R-05 / T-06 catalog consistency | One versioned `CanonicalCatalog.crane.db` contains exactly 808 finite positive points and an embedded `CatalogManifest`. First run copies the exact canonical bytes; stale/legacy catalogs are SHA-256-backed up and reconciled to the canonical identity. Fixture type is `SOFTWARE ACCEPTANCE FIXTURE`. |
| R-06 / T-04 acceptance project | `acceptance/fixtures/REV05_SoftwareAcceptanceFixture/` is a supported project folder with `project.db`, project-owned geometry-bearing IFCs, five expected element rows, zones, crane snapshot, 94 selected-crane chart rows, setup, viewport state and persisted run state. IFC cache identity is SHA-256 based when a persisted hash exists, so ZIP/copy timestamp drift cannot force a fresh extraction and discard persisted GA metadata. |
| T-05 coherent outputs | The fixture generator creates deterministic fixture snapshot/PDF/CSV for portable reconciliation. Windows release mode executes the real GA, persists/reopens the production v3 snapshot and exports production PDF/CSV; the verifier reconciles chart radius/boom/raw capacity/required capacity/available capacity/margin back to the canonical catalog. |
| R-07 AI destination security | Destination security is classified from the configured URI. Loopback is local; remote endpoints are remote regardless of provider brand. Remote HTTP and unconsented project-context transmission are blocked. |
| R-08 persistence | Writable catalog is under per-user LocalAppData. Existing data is preserved through verified pre-reconciliation and pre-migration backups; backup failure is fatal. |
| R-09 / T-08 release controls | Exact source commit/tag, deterministic Git-history ledger, source archive, SHA-256 manifest, SPDX source SBOM, direct-package inventory, dependency policy audit, licence notices and release manifest are included by the portable package builder. Windows release mode additionally generates a native Git bundle, resolved/transitive package inventory, NuGet vulnerability output, package locks, Authenticode evidence and published binaries. |
| R-10 / T-07 documentation/build | `global.json`, README, framework-dependent `win-x64` publish model, RID, Windows release script and CI workflow are aligned to .NET SDK 9.0.314. Windows gate generates one lock file per project, then proves locked restore. |

## Minimum acceptance demonstration mapping

1. **Clean checkout / restore / build / full tests / publish** — `scripts/Invoke-REV05Release.ps1` and `.github/workflows/rev05-acceptance.yml`.
2. **Published clean-machine IFC geometry/import/selection/reopen** — separately published `CraneOptimizer.AcceptanceHarness` plus the **published `CraneOptimizer.UI.exe`** in acceptance mode through `scripts/Invoke-REV05CleanMachineReplay.ps1`; the application must show its main window, restore scene elements, create native shapes, complete scene selection, close/reopen the native IFC session, and prove that the reopened project still exposes production GA-ready lift units.
3. **Mass traceability** — harness JSON includes `rawMass`, `detectedUnit`, `normalizedMassTons`, `trustStatus`, `unitConflict` and `gaEligible` for representative elements.
4. **Project → GA → save → close/reopen** — Windows GA acceptance mode persists the production v3 snapshot and `save_reopen_verification.json` from the same project folder.
5. **PDF/CSV reconciliation** — runtime verifier cross-checks selected chart radius/boom/raw capacity, required/available capacity and margin against the canonical catalog and saved GA evaluation.

## Portable fixture execution

`python scripts/run_rev05_portable_acceptance.py` regenerates the catalog/project twice, proves byte determinism, validates SQLite integrity, reopens `project.db`, reconciles catalog → fixture snapshot → CSV/PDF, verifies the restored regression baseline, validates source XML/JSON/Python, checks the acceptance label policy and runs successfully both from a Git working tree and from an extracted source archive without `.git`.
