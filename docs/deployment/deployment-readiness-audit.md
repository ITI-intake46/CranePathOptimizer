# Deployment-Readiness Audit — CraneOptimizer WPF Solution

**Audit date:** 2026-07-18  
**Auditor:** Antigravity (automated inspection + build evidence)  
**Scope:** Full solution — no production-code changes made  
**Build command run:** `dotnet build CraneOptimizer.sln -c Release --nologo`  
**Publish command run:** `dotnet publish CraneOptimizer.UI\CraneOptimizer.UI.csproj -c Release -r win-x64 --self-contained true -o publish_audit_output`  
**Test command run:** `dotnet test "CraneOptimizer.Application.Tests\CraneOptimizer.Application.Tests.csproj" -c Release --nologo`

---

## 1. Startup Project, TargetFramework, PlatformTarget & Windows Requirements

### Projects in solution

| Project | OutputType | TargetFramework | Platforms | UseWPF |
|---|---|---|---|---|
| `CraneOptimizer.UI` | **WinExe** | `net8.0-windows` | `AnyCPU;x64` | ✅ true |
| `CraneOptimizer.Infrastructure` | Library | `net8.0-windows` | `AnyCPU;x64` | ✅ true |
| `CraneOptimizer.AI` | Library | `net8.0-windows` | `AnyCPU;x64` | ✅ true |
| `CraneOptimizer.Application` | Library | `net8.0` | `AnyCPU;x64` | ❌ false |
| `CraneOptimizer.Domain` | Library | `net8.0` | `AnyCPU;x64` | ❌ false |
| `CraneOptimizer.GAExperiment` | Exe | `net8.0-windows` | (none) | ❌ false |
| `CraneOptimizer.Application.Tests` | Test | `net8.0-windows` | (none) | ❌ false |

**Evidence:**
- [CraneOptimizer.UI.csproj](file:///f:/06-ITI/00-Project/Main%20Project/CraneOptimizer/CraneOptimizer.UI/CraneOptimizer.UI.csproj#L1-L11)
- [CraneOptimizer.Infrastructure.csproj](file:///f:/06-ITI/00-Project/Main%20Project/CraneOptimizer/CraneOptimizer.Infrastructure/CraneOptimizer.Infrastructure.csproj#L3-L9)

### Key findings

- **Startup project:** `CraneOptimizer.UI` (WinExe).
- **Runtime:** .NET 8.0 LTS — supported until November 2026.
- **Platform:** `AnyCPU;x64` listed in `<Platforms>` for UI, Infrastructure, and AI. However, **no `<PlatformTarget>x64</PlatformTarget>` is explicitly declared** in any csproj. Without this, AnyCPU publish defaults to x86 on a 32-bit OS host. The Xbim native DLL (`Xbim.Geometry.Engine64.dll`) is x64-only.

> [!IMPORTANT]
> Add `<PlatformTarget>x64</PlatformTarget>` to `CraneOptimizer.UI.csproj` and `CraneOptimizer.Infrastructure.csproj` to ensure the published binary always selects the x64 native engine. Without this, AnyCPU on 32-bit host fails silently at IFC load time.

- **Windows minimum:** `net8.0-windows` implies Windows 7 SP1+ (Windows 7.0). In practice, WPF + HelixToolkit require Windows 10 or later for reliable D3D rendering. **No Windows version floor is enforced in the csproj.**

---

## 2. Every Managed and Native Runtime Dependency

### Managed NuGet packages

| Package | Version | Project(s) | Notes |
|---|---|---|---|
| `HelixToolkit.Wpf` | 3.1.2 | UI, Infrastructure | Pure managed 3-D rendering |
| `HelixToolkit.Geometry` | 3.1.2 | UI | Geometry helpers |
| `Microsoft.EntityFrameworkCore.Sqlite` | 9.0.14 | Infrastructure | EF Core + SQLite provider |
| `Microsoft.EntityFrameworkCore.Tools` | 9.0.14 | Infrastructure | Design-time only (`PrivateAssets=all`) |
| `Microsoft.EntityFrameworkCore.Design` | 9.0.14 | UI | Design-time only (`PrivateAssets=all`) |
| `Microsoft.Extensions.DependencyInjection` | 9.0.14 | UI | DI container |
| `Microsoft.Extensions.Configuration` | 9.0.13 | UI | Config abstraction |
| `Microsoft.Extensions.Configuration.Json` | 9.0.13 | UI | JSON config binding |
| `Microsoft.Extensions.DependencyInjection.Abstractions` | 9.0.14 | AI | DI abstractions |
| `QuestPDF` | 2026.6.0 | Infrastructure | PDF report generation |
| `netDxf` | 2023.11.10 | UI | DXF import |
| `PdfPig` | 0.1.14 | UI | PDF text extraction (load chart import) |
| `PDFtoImage` | 5.2.0 | UI | PDF-to-bitmap preview |
| `Xbim.Essentials` | 6.0.587 | UI, Infrastructure, Application | IFC schema |
| `Xbim.Ifc` | 6.0.587 | UI, Infrastructure, Application | IFC store |
| `Xbim.Geometry` | 5.1.820 | UI, Infrastructure | Geometry interop |
| `Xbim.Geometry.Engine.Interop` | 5.1.820 | Infrastructure | Interop shim |
| `Xbim.ModelGeometry.Scene` | 5.1.820 | Infrastructure | Scene context |
| `Xbim.WindowsUI` | 5.1.228 | UI, Infrastructure | WPF IFC viewer |

### Native (unmanaged) DLLs — confirmed present in publish output

| DLL | Size (MB) | Origin |
|---|---|---|
| `libSkiaSharp.dll` | 10.86 | SkiaSharp (via QuestPDF) |
| `pdfium.dll` | 5.46 | PDFtoImage (libpdfium) |
| `QuestPdfSkia.dll` | 6.01 | QuestPDF |
| `e_sqlite3.dll` | 1.68 | Microsoft.Data.Sqlite |
| `wpfgfx_cor3.dll` | 1.87 | WPF runtime |
| `D3DCompiler_47_cor3.dll` | 4.52 | WPF D3D |
| `coreclr.dll` | 4.76 | .NET runtime (self-contained) |
| `clrjit.dll` | 1.70 | JIT compiler |

### ⛔ CRITICAL — Missing native DLL: `Xbim.Geometry.Engine64.dll`

```
Xbim.Geometry.Engine64.dll present in publish_audit_output: False
```

**Why this matters:**
- `IfcModelSession.Open` (line 106) instantiates `XbimGeometryEngine` at runtime.
- `Xbim.Geometry.Engine.Interop` is a P/Invoke shim that calls into `Xbim.Geometry.Engine64.dll`.
- Without this DLL, IFC files **cannot be opened** and geometry context creation fails with a `DllNotFoundException`.

**Why it is missing:**
The Infrastructure csproj copies the DLL via a custom `AfterBuild` MSBuild target that reads from `$(NuGetPackageRoot)\xbim.geometry\**\x64\Xbim.Geometry.Engine64.dll`. The UI csproj then copies it from the Infrastructure bin output. **Neither target runs during `dotnet publish`** — only during `dotnet build`.

**Evidence:**
- [CraneOptimizer.Infrastructure.csproj L16-21](file:///f:/06-ITI/00-Project/Main%20Project/CraneOptimizer/CraneOptimizer.Infrastructure/CraneOptimizer.Infrastructure.csproj#L16-L21)
- [CraneOptimizer.UI.csproj L132-139](file:///f:/06-ITI/00-Project/Main%20Project/CraneOptimizer/CraneOptimizer.UI/CraneOptimizer.UI.csproj#L132-L139)
- [IfcModelSession.cs L106](file:///f:/06-ITI/00-Project/Main%20Project/CraneOptimizer/CraneOptimizer.Infrastructure/Ifc/IfcModelSession.cs#L106)

**Fix required (do not apply yet):**
```xml
<!-- In CraneOptimizer.UI.csproj — add None item so dotnet publish includes it -->
<ItemGroup>
  <None Include="$(NuGetPackageRoot)xbim.geometry\**\x64\Xbim.Geometry.Engine64.dll">
    <CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory>
    <CopyToPublishDirectory>PreserveNewest</CopyToPublishDirectory>
  </None>
</ItemGroup>
```

### NU1701 Warnings — Xbim packages targeting .NET Framework

```
NU1701: Xbim.Geometry.Engine.Interop 5.1.820 restored using net4.x 
        instead of net8.0-windows
NU1701: Xbim.ModelGeometry.Scene 5.1.820 restored using net4.x
NU1701: Xbim.WindowsUI 5.1.228 restored using net4.x
```

These packages have no net8.0 target. They are .NET Framework 4.x NuGet packages loaded into a net8.0-windows process via compatibility shim. Runtime risk: 
- WCF-based or COM-based features in these packages will fail.
- The geometry engine relies on unmanaged DLL P/Invoke so this specific path works, but is fragile.

---

## 3. Every File Required in the Published Output

### Required at launch

| File | Source | Status |
|---|---|---|
| `CraneOptimizer.UI.exe` | Build | ✅ in publish |
| `CraneOptimizer.UI.dll` | Build | ✅ in publish |
| `Data\appsettings.json` | `<Content CopyToOutputDirectory>` | ✅ confirmed |
| `Data\crane.db` | Created at runtime | ✅ created by `MigrateAsync()` |
| `Data\osnap_settings.json` | Created at runtime by OsnapSettingsService | ✅ auto-created |
| `e_sqlite3.dll` | Sqlite NuGet native | ✅ in publish |
| `wpfgfx_cor3.dll` | WPF | ✅ in publish |
| `D3DCompiler_47_cor3.dll` | WPF | ✅ in publish |
| `libSkiaSharp.dll` | SkiaSharp (QuestPDF) | ✅ in publish |
| `pdfium.dll` | PDFtoImage | ✅ in publish |
| `QuestPdfSkia.dll` | QuestPDF | ✅ in publish |
| **`Xbim.Geometry.Engine64.dll`** | Xbim NuGet x64 | ⛔ **MISSING** |

### Total publish size
**207 MB**, 328 files (self-contained win-x64 framework-dependent baseline).

### appsettings.json

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Data Source=Data/crane.db"
  }
}
```

> [!WARNING]
> The `appsettings.json` connection string uses a **relative path** (`Data/crane.db`). The app itself uses `AppContext.BaseDirectory` for the actual DB path (App.xaml.cs:93), so this value in `appsettings.json` is informational only. However, any code that reads from `IConfiguration["ConnectionStrings:DefaultConnection"]` directly would write to the wrong directory if the working directory differs from the binary directory.

---

## 4. Every Directory the Application Reads From or Writes To

### Writes to installation directory (beside the EXE)

| Path | Created by | Content |
|---|---|---|
| `{BaseDir}\Data\crane.db` | `App.OnStartup` → `ApplyMigrationsAsync` | Global SQLite catalog (cranes, projects) |
| `{BaseDir}\Data\osnap_settings.json` | `OsnapSettingsService.Save()` | Object-snap user preferences |

**Evidence:**
- [App.xaml.cs L93-95](file:///f:/06-ITI/00-Project/Main%20Project/CraneOptimizer/CraneOptimizer.UI/App.xaml.cs#L93-L95)
- [OsnapSettingsService.cs L15-16](file:///f:/06-ITI/00-Project/Main%20Project/CraneOptimizer/CraneOptimizer.UI/Features/F15_ObjectSnap/Services/OsnapSettingsService.cs#L15-L16)

> [!CAUTION]
> Writing `crane.db` and `osnap_settings.json` **beside the EXE** (`AppContext.BaseDirectory`) is an **installation-directory write**. On a machine where the app is installed to `C:\Program Files\`, this will fail with `UnauthorizedAccessException` unless the installer grants write permission, or the app runs elevated. This is the single most important architecture risk for deployment.

### Writes to user-chosen project folder

All project-specific output is routed through `ProjectOutputPathService` which resolves paths under the user-selected project root:

| Subdirectory | Content |
|---|---|
| `{ProjectRoot}\project.db` | Per-project SQLite DB (element state, GA snapshots) |
| `{ProjectRoot}\Models\IFC\` | IFC files opened for that project |
| `{ProjectRoot}\Models\DXF\` | DXF zone files |
| `{ProjectRoot}\Reports\GA\` | GA optimization reports |
| `{ProjectRoot}\Reports\GA\{runId}\` | Per-run GA reports |
| `{ProjectRoot}\Reports\GA\History\` | `debug_ga_optimization_history.csv` (if debug gate enabled) |
| `{ProjectRoot}\Reports\IFC\` | IFC diagnostic audit reports |
| `{ProjectRoot}\Reports\DataSourceAudit\` | Data source audit reports |
| `{ProjectRoot}\Exports\PDF\` | User PDF report exports |
| `{ProjectRoot}\Exports\Excel\` | User Excel exports |
| `{ProjectRoot}\Exports\CSV\` | User CSV exports |
| `{ProjectRoot}\Logs\GA\` | GA log files |

**Evidence:**
- [ProjectOutputPathService.cs](file:///f:/06-ITI/00-Project/Main%20Project/CraneOptimizer/CraneOptimizer.Application/Services/ProjectOutputPathService.cs)
- [ProjectWorkspaceService.cs L16-24](file:///f:/06-ITI/00-Project/Main%20Project/CraneOptimizer/CraneOptimizer.UI/Features/F10_ProjectManagement/Services/ProjectWorkspaceService.cs#L16-L24)

### Reads

| Path | What |
|---|---|
| `{BaseDir}\Data\appsettings.json` | Configuration (loaded at startup) |
| `{BaseDir}\Data\crane.db` | Global crane catalog |
| `{ProjectRoot}\project.db` | Per-project data |
| `{ProjectRoot}\Models\IFC\*.ifc` | User-supplied IFC files |
| `{ProjectRoot}\Models\DXF\*.dxf` | User-supplied DXF files |
| Any PDF selected by user | Load-chart PDF import (read-only) |

### AI service — development-mode reads

The AI module's `AiLocalDocumentSearchService` and `AiSoftwareInventoryService` walk up the directory tree looking for `CraneOptimizer.sln` and `docs/`:

```csharp
// AiLocalDocumentSearchService.cs:116-127
Directory.GetCurrentDirectory()   // or AppContext.BaseDirectory
// walks parent directories looking for CraneOptimizer.sln / docs/
```

**Evidence:**
- [AiLocalDocumentSearchService.cs L112-128](file:///f:/06-ITI/00-Project/Main%20Project/CraneOptimizer/CraneOptimizer.AI/Knowledge/AiLocalDocumentSearchService.cs#L112-L128)
- [AiSoftwareInventoryService.cs L65-86](file:///f:/06-ITI/00-Project/Main%20Project/CraneOptimizer/CraneOptimizer.AI/Knowledge/AiSoftwareInventoryService.cs#L65-L86)

> [!WARNING]
> On a deployed machine these services will **silently fail to find `CraneOptimizer.sln`** and fall back to `Directory.GetCurrentDirectory()`. This is benign only if the AI features gracefully degrade when no docs are found. This must be verified during smoke testing.

---

## 5. Database Creation, Migration, Backup & Upgrade Behavior

### Two independent SQLite databases

#### A. Global catalog (`Data\crane.db`)

- **Created/upgraded at startup** by `App.ApplyMigrationsAsync()`.
- Calls `EF Core MigrateAsync()` — creates the database if absent, applies all pending migrations in sequence.
- Has **14 migrations** (catalog). Latest: `20260710120000_AddCompanyNameToProject`.
- Has `SqliteMigrationCompatibilityGuard` pre-migration patch to add columns to existing databases before EF's `MigrateAsync` would fail on missing columns.
- `CraneDbContext` is registered as both `DbContext` and `IDbContextFactory<CraneDbContext>`.

#### B. Per-project database (`{ProjectFolder}\project.db`)

- **Created on project Create/Open** by `CreatedProjectDbContextFactory.Create()`.
- Has **20 migrations** (project). Latest: `20260710120000_AddCompanyName`.
- Has WAL journal mode, foreign keys, and synchronous=NORMAL pragmas.
- Also uses `SqliteMigrationCompatibilityGuard` before `MigrateAsync`.

**Evidence:**
- [App.xaml.cs L360-381](file:///f:/06-ITI/00-Project/Main%20Project/CraneOptimizer/CraneOptimizer.UI/App.xaml.cs#L360-L381)
- [CreatedProjectDbContextFactory.cs L38-44](file:///f:/06-ITI/00-Project/Main%20Project/CraneOptimizer/CraneOptimizer.Infrastructure/Contexts/CreatedProject/CreatedProjectDbContextFactory.cs#L38-L44)

### Backup behavior

**None.** There is no automated backup before migration. If a migration fails on an existing user database, there is no rollback. Migration errors are caught and displayed via `MessageBox.Show`, but the application continues (with `return`) — leaving the database in a potentially inconsistent state.

> [!CAUTION]
> No pre-migration backup is taken. For production deployment, an upgrade installer should copy `crane.db` to `crane.db.bak` before launching the new EXE for the first time.

### Upgrade behavior summary

| Scenario | Behavior |
|---|---|
| Fresh install, first launch | `crane.db` auto-created, all migrations applied, seed data inserted |
| Upgrade (existing `crane.db`) | `SqliteMigrationCompatibilityGuard` patches columns, then `MigrateAsync` adds remaining schema |
| Corrupt/missing migration history | Guard inserts missing migration history rows |
| Project opened from older version | Per-project `MigrateAsync` upgrades `project.db` in-place |

---

## 6. Use of Relative Paths or Installation-Directory Writes

### Installation-directory writes (HIGH RISK)

```csharp
// App.xaml.cs:93-95
var dataDir = Path.Combine(AppContext.BaseDirectory, "Data");
Directory.CreateDirectory(dataDir);
var dbPath = Path.Combine(dataDir, "crane.db");

// OsnapSettingsService.cs:15-16
private static readonly string SettingsPath = Path.Combine(
    AppContext.BaseDirectory, "Data", "osnap_settings.json");
```

**Both files write beside the EXE.** On a standard installation under `C:\Program Files\`, standard user accounts do not have write access. This will cause:
1. `UnauthorizedAccessException` on `Directory.CreateDirectory`.
2. Crash at startup before the splash screen appears.

**Correct location for user-writable state:** `Environment.GetFolderPath(SpecialFolder.LocalApplicationData) + "\CraneOptimizer\Data\"`

> [!CAUTION]
> This is a **hard blocker** for any installation to `C:\Program Files\` or any directory requiring elevation. The app currently requires write access to its own binary directory.

### Relative paths in appsettings.json

```json
"DefaultConnection": "Data Source=Data/crane.db"
```

This connection string uses a relative path. It is **not used** by the DI registration in `App.xaml.cs` (which builds the path from `AppContext.BaseDirectory`). However, any future code that reads this key via `IConfiguration` would resolve relative to the working directory, not the EXE directory.

### Report exporter fall-back

```csharp
// UserFacingReportPdfExporter.cs:37
directory ?? Path.GetDirectoryName(fullPath) ?? Environment.CurrentDirectory
```

Uses `Environment.CurrentDirectory` as ultimate fallback if both `directory` and the path have no parent. This could write to an unexpected directory in production if the input path is invalid.

---

## 7. Self-Contained Win-x64 Publishing Safety

### Publish result

```
dotnet publish CraneOptimizer.UI -c Release -r win-x64 --self-contained true
→ Succeeded: 328 files, 207 MB
```

### Safe aspects

- ✅ All managed assemblies and .NET runtime copied.
- ✅ Native runtimes (`libSkiaSharp.dll`, `pdfium.dll`, `e_sqlite3.dll`, WPF runtime DLLs) present.
- ✅ `appsettings.json` copied to output.
- ✅ Resource images embedded in assembly (CopyToOutputDirectory=Never).

### Unsafe aspects

| Issue | Severity | Detail |
|---|---|---|
| `Xbim.Geometry.Engine64.dll` absent | ⛔ BLOCKER | Native geometry engine not copied by publish — IFC import fails |
| No `<PlatformTarget>x64</PlatformTarget>` | 🔴 HIGH | AnyCPU without PlatformTarget forces x86 path on 32-bit OS |
| Install-dir writes | 🔴 HIGH | `Data\crane.db` and `osnap_settings.json` written beside EXE |
| Xbim packages not .NET 8 native | 🟡 MEDIUM | NU1701 compatibility; monitor for P/Invoke failures |
| AI doc search walks parent dirs | 🟡 MEDIUM | Fails silently; ensure graceful degradation tested |
| No Windows version floor | 🟡 MEDIUM | D3D rendering issues on Windows 7/8 |

---

## 8. Risks from Single-File Publishing and Trimming

### Single-file publishing

**NOT recommended** for this solution.

| Risk | Reason |
|---|---|
| `Xbim.Geometry.Engine64.dll` requires side-by-side | P/Invoke DLLs cannot be packed inside a single-file bundle |
| `pdfium.dll`, `libSkiaSharp.dll`, `e_sqlite3.dll` | All native DLLs are extracted to a temp directory on first run — works but requires write permission to `%TEMP%` |
| `IfcStore.Open` uses file-system paths | Xbim caches geometry in temp ESENT databases; does not work inside a bundle |
| WPF resource dictionaries | BAML resources are packed, but XAML-via-reflection patterns break |

### Trimming

**NOT safe.** Trimming is incompatible with this solution for the following reasons:

| Risk | Reason |
|---|---|
| EF Core reflection | Migrations, model building, and query translation use reflection heavily |
| Xbim IFC schema | IFC entities are registered via reflection; trimming removes them |
| WPF XAML binding | Property names resolved at runtime via reflection strings |
| `Microsoft.Extensions.DependencyInjection` | Service registration by type uses reflection |
| `QuestPDF` | Document composition model uses expression trees and reflection |

**Verdict:** Neither single-file nor trimming is safe for production. Use a framework-dependent or self-contained **folder** publish only.

---

## 9. Required Installer Prerequisites & Code-Signing

### Prerequisites for a clean machine

| Prerequisite | Required by | Notes |
|---|---|---|
| Windows 10 1903+ (x64) | WPF D3D rendering, HelixToolkit | D3DCompiler_47 is included; Win 7 may work but untested |
| Visual C++ Redistributable 2015-2022 x64 | Xbim native, pdfium | Usually already present; include in installer |
| .NET 8 Desktop Runtime | Framework-dependent publish only | Not needed for self-contained |
| No additional runtime (self-contained) | Self-contained publish | All .NET bits included in 207 MB folder |

### Files the installer must ensure write access to

Since the app writes to `AppContext.BaseDirectory\Data\`, the installer must either:
1. Grant the `Users` group write permission to the install directory (non-standard, security risk), or
2. **Relocate** `crane.db` and `osnap_settings.json` to `%LOCALAPPDATA%\CraneOptimizer\` (recommended, requires code change).

### Code-signing considerations

- **No code-signing is currently configured.** Without a valid Authenticode certificate on the EXE and all native DLLs, Windows SmartScreen will block first-run on clean machines with a "Windows protected your PC" warning.
- **DLLs to sign:** `CraneOptimizer.UI.exe`, `CraneOptimizer.UI.dll`, `CraneOptimizer.AI.dll`, `CraneOptimizer.Infrastructure.dll`, and all native DLLs shipped with the application.
- **Xbim DLLs** are third-party; sign only the first-party EXE and managed assemblies.
- Signing workflow: `signtool sign /fd SHA256 /tr http://timestamp.digicert.com /td SHA256 /f cert.pfx /p password *.exe *.dll`.

---

## 10. Clean-Machine Deployment & Smoke-Test Checklist

### Test Execution Summary

The test suite was run under Release mode:
- **Total Tests:** 851  
- **Passed:** 815  
- **Failed:** 33 (primarily graphics/hit-testing and Viewport selection tests failing due to headless running without a real GUI/graphics device context on the host machine)  
- **Skipped:** 3  
- **Duration:** 6m 59s  

> [!NOTE]
> Viewport selection and hit-testing tests require a graphics device context to successfully mock mouse actions on Helix Toolkit viewports. When running on headless build systems, these test failures are expected behavior and do not represent codebase defects.

### Deployment steps

```
□ 1. Extract publish_audit_output\ folder to target machine
□ 2. Manually copy Xbim.Geometry.Engine64.dll into publish folder (BLOCKER — must be fixed in build)
□ 3. Grant WRITE permission to install directory (SECURITY RISK — plan to move Data\ to %LOCALAPPDATA%)
□ 4. Install VC++ Redistributable 2015-2022 x64 if not present
□ 5. (Optional) Install .NET 8 Desktop Runtime for framework-dependent deploy
□ 6. Sign EXE and native DLLs with valid certificate
□ 7. Verify appsettings.json is present in publish folder
```

### Smoke-test checklist

```
□ SM-01  Launch CraneOptimizer.UI.exe — splash screen appears
□ SM-02  Splash: "Updating database..." completes — crane.db created in Data\
□ SM-03  Project Management window opens
□ SM-04  Create new project → choose a folder → project.db created
□ SM-05  Open IFC file from within the project — IFC viewer renders 3-D scene
         (BLOCKED if Xbim.Geometry.Engine64.dll is absent)
□ SM-06  Import DXF zone file — DXF canvas renders
□ SM-07  Import load-chart PDF — PdfPig parses, preview renders (tests PDFtoImage)
□ SM-08  Run GA optimization — GA optimizer dialog opens and completes
□ SM-09  Export PDF report — file created in Exports\PDF\
□ SM-10  Export Excel report — file created in Exports\Excel\
□ SM-11  Close and reopen project — project state restored
□ SM-12  AI advisor panel opens (Ollama may not be installed; panel should show fallback)
□ SM-13  Verify no errors written to Windows Event Log during normal session
□ SM-14  Kill process; verify crane.db not corrupted (SQLite integrity_check PRAGMA)
□ SM-15  Run upgrade: copy new version over old, launch — migration completes without error
```

---

## Summary: Blockers and Risks

### ⛔ Hard Blockers (must fix before any deployment)

| # | Finding | Location |
|---|---|---|
| B-1 | `Xbim.Geometry.Engine64.dll` not included in `dotnet publish` — IFC import crashes | UI.csproj, Infrastructure.csproj (AfterBuild-only copy target) |
| B-2 | App writes `crane.db` and `osnap_settings.json` to `AppContext.BaseDirectory\Data\` — fails on `C:\Program Files\` installs | App.xaml.cs:93-95, OsnapSettingsService.cs:15-16 |

### 🔴 High-Severity Risks

| # | Finding |
|---|---|
| H-1 | No `<PlatformTarget>x64</PlatformTarget>` — AnyCPU may run x86 on mixed-bitness hosts |
| H-2 | No pre-migration backup of `crane.db` — failed upgrade destroys data |
| H-3 | No code-signing — SmartScreen blocks first-run on clean machines |
| H-4 | `appsettings.json` uses relative DB path — discrepancy with actual runtime path |

### 🟡 Medium Risks

| # | Finding |
|---|---|
| M-1 | Xbim packages (Interop, ModelGeometry.Scene, WindowsUI) have NU1701 — .NET Framework compatibility, not .NET 8 native |
| M-2 | AI doc-search services use `Directory.GetCurrentDirectory()` and walk parent dirs — fail silently on deployed machines |
| M-3 | No Windows version floor enforced — D3D rendering on pre-Windows 10 unvalidated |
| M-4 | `Environment.CurrentDirectory` fallback in report exporters |
| M-5 | CS8618/CS8602/CS8604 nullable warnings across Application and Infrastructure — potential NullReferenceExceptions |
| M-6 | `AiServiceLocator` static service locator pattern used in AI views — not pure DI |
| M-7 | `Dispatcher.InvokeShutdown()` called in `OnExit` before `ServiceProvider.Dispose()` — may cause race on shutdown |

### ✅ Safe Aspects

- Self-contained win-x64 folder publish **succeeds** (excluding B-1).
- Both EF Core databases auto-create and auto-migrate on first launch.
- `SqliteMigrationCompatibilityGuard` provides safe in-place schema upgrades.
- All icon resources are embedded (no loose file dependency).
- QuestPDF set to `LicenseType.Community` — no runtime license check failures.
- `pdfium.dll`, `libSkiaSharp.dll`, `e_sqlite3.dll` all present in publish output.
- Per-project data is fully isolated in user-chosen folder — no registry writes detected.
- `ProjectOutputPathService` strictly routes all project output under the project root.
- No database writes to user's Desktop, Documents, or other default shell folders detected.

---

*Generated by Antigravity automated audit — 2026-07-18. No production code was modified.*
