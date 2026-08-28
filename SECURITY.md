# Security Policy & Dependency Integrity — CraneOptimizer
## Project: INT46-SV-AECI-03-CraneOpt

---

## Known CVE Exposure & Mitigation Policy

### CVE-2025-6965 — SQLite Memory Corruption (SQLite < 3.50.2)

| Field           | Value |
|-----------------|-------|
| **CVE ID**      | CVE-2025-6965 |
| **Severity**    | High |
| **Affected**    | Native SQLite versions < 3.50.2 |
| **Transitive dependency** | `SQLitePCLRaw.bundle_e_sqlite3` (via `Microsoft.EntityFrameworkCore.Sqlite` 9.0.14) |
| **Status**      | Tracked; `packages.lock.json` lockfiles generated for deterministic build reproducibility |
| **Mitigations** | Application database (`crane.db` and `.craneopt` files) operates strictly local-only with no open network ports or remote SQL ingestion endpoints. |

---

## Dependency Pinning & Lockfiles (Requirement R-09)

1. **SDK Version Pinning**: `.NET SDK 9.0.315` pinned via `global.json` (`latestMinor` policy).
2. **NuGet Lockfiles**: `packages.lock.json` locked across all 7 projects to prevent transitive dependency drift.
3. **Build Command**: Standard reproducible build enforced via `dotnet restore --locked-mode` / `dotnet build CraneOptimizer.sln --configuration Release`.
