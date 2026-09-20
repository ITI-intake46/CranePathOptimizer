# CraneOpt REV05 — SOFTWARE ACCEPTANCE FIXTURE checklist

Use one package generated from one source commit. Do not substitute screenshots for machine-readable evidence.

1. **Source identity** — record source commit/tag, verify the deterministic Git-history ledger, source archive and package SHA-256 manifest; Windows runtime evidence additionally includes a native Git bundle.
2. **Build identity** — confirm SDK equals `global.json` (9.0.314), `win-x64`, framework-dependent deployment, and one `packages.lock.json` per project in Windows evidence.
3. **Regression baseline** — full restored suite plus focused acceptance suite; preserve TRX files and `REV05-TEST-BASELINE-RECONCILIATION.md`.
4. **Published IFC workflow** — confirm `Xbim.Geometry.Engine64.dll`, run the published AcceptanceHarness **and published WPF application**, require a visible main window, geometry, scene/product selection roundtrips, production reader mass normalization, close/reopen and negative-unit fail-closed evidence.
5. **Mass trace** — evidence must expose raw mass, detected unit, normalized tonnes, trust status, conflict state and GA eligibility for representative kg/tonne/converted-unit elements.
6. **Project / GA** — open `REV05_SoftwareAcceptanceFixture`, prove the restored project contains GA-ready lift units from the persisted SHA-256-bound IFC cache, execute acceptance GA, persist, close/reopen and require unchanged canonical snapshot payload.
7. **Reports** — require substantive production PDF plus detailed CSV and reconcile element identity, radius, boom, raw chart capacity, required/available capacity and safety margin to the saved snapshot/catalog.
8. **Catalog** — require exactly 808 finite positive points, matching embedded/companion version identity and exact first-run canonical copy behavior.
9. **Security / dependencies** — require remote-endpoint policy tests, resolved package inventory, vulnerability scan, SPDX SBOM, licence notices and zero release-gate command failures.
10. **Release controls** — require checksums, release manifest, source archive, deterministic Git-history ledger, signing evidence for the mode used and a clean-machine replay result; the Windows release evidence additionally includes a native Git bundle.

Portable preflight: `python scripts/run_rev05_portable_acceptance.py`.
Windows end-to-end gate: `pwsh ./scripts/Invoke-REV05Release.ps1 -SimulationMode` for the SOFTWARE ACCEPTANCE FIXTURE execution, or official mode with the organization signing certificate.
