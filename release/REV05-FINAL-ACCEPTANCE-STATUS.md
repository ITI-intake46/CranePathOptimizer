# REV05 SOFTWARE ACCEPTANCE FIXTURE status

REV05 consolidates the corrective implementation and acceptance data into one versioned software-acceptance fixture. The repository is designed so the same canonical catalog, project, IFC assets, GA configuration and release controls are used throughout the acceptance chain.

## Executed on the portable host

- deterministic catalog/project generation;
- repeated byte-for-byte fixture regeneration;
- SQLite integrity checks and project close/reopen invariance;
- 808-point catalog identity and engineering-value checks;
- catalog → fixture GA snapshot → CSV/PDF arithmetic reconciliation;
- restored test-baseline inventory/declaration checks;
- source XML/JSON/Python validation and whitespace checks;
- source-archive reproduction without `.git`;
- package-level source provenance, SBOM, direct package inventory, dependency policy audit, deterministic Git-history ledger and SHA-256 manifests. The Windows gate additionally creates a native Git bundle.

## Windows gate included in the submission

`scripts/Invoke-REV05Release.ps1` is the fail-closed Windows x64 execution gate. It requires the exact pinned SDK, generates one NuGet lock per project, proves locked restore, builds/tests/publishes, executes the separately published xBIM AcceptanceHarness, runs GA/save/reopen/report reconciliation, audits resolved dependencies, generates an SPDX SBOM, signs first-party binaries for the selected mode, hashes all staged files and produces the release archive.

`scripts/Invoke-REV05CleanMachineReplay.ps1` executes both the delivered published IFC harness **and the delivered `CraneOptimizer.UI.exe` itself** from the staged package. The WPF application opens a writable copy of the supported fixture project, requires a visible main window, restored IFC scene elements, native xBIM shape instances, a real scene-selection round trip, native IFC close/reopen, **and at least one production GA-ready lift unit** before the replay can pass. Project IFC cache identity is SHA-256 based when a persisted hash exists, so copying or ZIP extraction cannot invalidate byte-identical cached element metadata merely because file timestamps changed.

## Fixture identity

All acceptance catalog/project artifacts use the direct label:

**SOFTWARE ACCEPTANCE FIXTURE**

The companion manifests and embedded `CatalogManifest` use the same fixture identity and version so no second catalog/seed truth exists.
