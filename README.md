# CraneOpt REV05 — reproducible acceptance submission

CraneOpt is a Windows x64 / .NET 8 WPF mobile-crane optimization application. REV05 uses SDK **9.0.314** and a **framework-dependent `win-x64` deployment model**. This avoids downloading large self-contained .NET runtime packs during normal restore/build while keeping the release RID and dependency locks explicit. A compatible Microsoft Windows Desktop runtime 8.x or 9.x must be installed on machines that run the published application.


## Launch immediately on the reviewed Windows workstation

REV05 deliberately pins **.NET SDK 9.0.314**, matching the SDK observed on the Windows validation workstation, while the application target framework remains `net8.0-windows`. Normal developer builds are framework-dependent x64 and do not set a project RuntimeIdentifier. `RollForward=Major` permits execution on a compatible installed .NET 8 or .NET 9 Windows Desktop runtime.

From the extracted repository root, run:

```powershell
.\START-CRANEOPT-REV05.cmd
```

The launcher clears stale NuGet temporary scratch data, restores only the `CraneOptimizer.UI` dependency graph without self-contained runtime packs, builds the current UI project as framework-dependent x64, verifies the native xBIM engine, and starts `CraneOptimizer.UI`. In Visual Studio, open `CraneOptimizer.sln`, allow NuGet restore to finish, select **x64** when available, then use **Rebuild Solution** before F5. Do not run a previously built executable after a failed build.

For a quick environment check:

```powershell
dotnet --version
```

It must resolve to `9.0.314` because `global.json` and the acceptance release gate use that exact toolchain.

## SOFTWARE ACCEPTANCE FIXTURE scope

The repository contains one deterministic, versioned **SOFTWARE ACCEPTANCE FIXTURE** under `acceptance/`. The catalog, project, IFC models, GA fixture outputs, and reconciliation evidence are generated together so the next submission can be reproduced as one coherent software-acceptance case.

The supported project cache is portable across copy/ZIP extraction: when a persisted IFC SHA-256 is available, cache identity is content-hash based rather than timestamp based. The fixture database carries the current normalization-cache version and five persisted `LiftAssembly` rows with explicit trusted weights, valid COGs, and `IncludeInGa=true`, so reopening the supplied project must retain production GA-ready lift units instead of silently re-extracting the IFC and losing persisted GA metadata.

## One-command REV05 acceptance release (Windows x64)

Open PowerShell from a clean checkout and run:

```powershell
pwsh ./scripts/Invoke-REV05Release.ps1 -SigningCertificateThumbprint <YOUR_CODE_SIGNING_CERT_THUMBPRINT>
```

For the SOFTWARE ACCEPTANCE FIXTURE run, use `-SimulationMode`; it may use a temporary self-signed code-signing certificate while still executing the real restore/build/test/publish/xBIM/GA/report pipeline on Windows. Official mode requires the organization signing certificate. Both modes fail closed when native xBIM geometry, tests, runtime evidence, dependency audit, catalog integrity, or report reconciliation fails.

The pipeline performs, in order:

1. validates the exact SDK, repository state and regression baseline;
2. deterministically regenerates the 808-point canonical acceptance catalog and supported project folder;
3. generates and locks NuGet restore inputs for `win-x64`, then repeats restore in locked mode;
4. builds Release/x64 and runs the full test suite plus focused REV05 acceptance tests;
5. publishes the framework-dependent `win-x64` app and proves `Xbim.Geometry.Engine64.dll` is present;
6. exercises the real xBIM IFC geometry + reader path, including kg, tonne, converted-unit and missing-unit cases;
7. runs the real GA from the supplied `project.db`, saves a production v3 GA snapshot, reopens it through the production snapshot store, writes project run state and exports substantive application PDF/CSV reports;
8. independently reconciles the project, IFC hash, catalog chart lookups, GA snapshot, capacities/margins and report rows;
9. performs a transitive NuGet vulnerability audit, creates an SPDX SBOM, records license/package evidence, hashes the staged release, signs executable binaries and verifies the signatures;
10. creates the source archive, Git bundle, release manifest and final acceptance ZIP.

Expected official output:

`artifacts/release/CraneOpt-REV05-acceptance-submission.zip`

## Manual clean-machine demonstration

After the pipeline passes, copy the generated release package to a Windows x64 machine that has not been used to build CraneOpt. Follow `release/REV05-ACCEPTANCE-CHECKLIST.md`. The demonstration imports `acceptance/fixtures/REV05_SoftwareAcceptanceFixture/Models/IFC/rev05_acceptance.ifc`, verifies visible geometry and element identity, shows raw/detected/normalized mass evidence, opens the project folder, runs/reopens the GA result and reconciles the generated PDF/CSV to the canonical catalog.

## Project format

The supported project format is a **folder containing `project.db`** and project-owned assets (`Models/IFC`, optional `Models/DXF`, `Reports`, etc.). A tiny standalone JSON file named `.craneopt` is not the REV05 acceptance project format.

## Canonical catalog lifecycle

The immutable shipped artifact is `CraneOptimizer.UI/Data/CanonicalCatalog.crane.db`. On first run it is copied byte-for-byte to the user's local application-data location. The writable catalog is never migrated in place until a non-empty, SHA-256-verified pre-migration backup succeeds. The acceptance gate verifies exactly 808 finite positive target points and byte/provenance identity.

## Additional evidence

See `acceptance/REV05_ACCEPTANCE_MATRIX.md`, `release/REV05-ACCEPTANCE-CHECKLIST.md`, `SECURITY.md`, `THIRD-PARTY-NOTICES.md`, and the generated `artifacts/release/evidence/` directory.

## REV05 acceptance execution

The repository contains two complementary gates:

1. `python scripts/run_rev05_portable_acceptance.py` executes deterministic SOFTWARE ACCEPTANCE FIXTURE generation, database-integrity, report-reconciliation and source/release checks on a portable host, including archive-mode reproduction without a `.git` directory.
2. `scripts/Invoke-REV05Release.ps1` is the Windows x64 fail-closed gate for exact SDK restore/build/test, native xBIM published execution, GA save/reopen/report reconciliation, dependency audit, SBOM, signing and final packaging. Use `-SimulationMode` for the SOFTWARE ACCEPTANCE FIXTURE Windows run; official mode additionally requires the organization signing certificate and an exact source tag.
