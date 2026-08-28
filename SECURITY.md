# Security Policy — CraneOptimizer
## Project: INT46-SV-AECI-03-CraneOpt

---

## Known CVE Exposure

### CVE-2025-6965 — SQLite Memory Corruption (SQLite < 3.50.2)

| Field           | Value |
|-----------------|-------|
| **CVE ID**      | CVE-2025-6965 |
| **Severity**    | High |
| **Affected**    | SQLite versions < 3.50.2 |
| **Transitive dependency** | `SQLitePCLRaw.bundle_e_sqlite3` v2.1.10 (via `Microsoft.EntityFrameworkCore.Sqlite` 9.0.14) |
| **Status**      | Upstream fix pending in `SQLitePCLRaw` package |
| **Mitigations** | Database access is local-only (no network socket). Attack surface requires local file access. |

**Action required:** Update `SQLitePCLRaw.bundle_e_sqlite3` to a version bundling SQLite ≥ 3.50.2 when the upstream package releases the fix. Track: https://github.com/ericsink/SQLitePCL.raw/releases

---

## Reporting Vulnerabilities

This is an academic graduation project (ITI Intake 46, Supervisor: AECI).
Security findings should be reported to the project team directly.

---

## Dependency Lock

`global.json` pins the .NET SDK to 8.x (latestMinor policy).
To enable NuGet package lock files and prevent transitive dependency drift, run:

```bash
dotnet restore --use-lock-file
```

and commit the generated `packages.lock.json` files.
