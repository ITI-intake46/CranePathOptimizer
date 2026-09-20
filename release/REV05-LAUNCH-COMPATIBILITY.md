# REV05 launch compatibility correction

The original REV04 package could fail before compilation when its exact SDK was not installed. The first REV05 launcher then used a self-contained developer build, which forced NuGet to download large .NET 8 runtime packs. On the reviewed workstation that exposed low-disk-space and concurrent-cache-lock failures before CraneOpt itself compiled.

This corrected REV05 launch path separates developer launch from the acceptance release gate:

- `global.json` resolves the reviewed workstation toolchain: **.NET SDK 9.0.314**.
- The application target remains `net8.0-windows`, x64.
- **Normal Visual Studio / launcher builds are framework-dependent** and do not set a RuntimeIdentifier in the project. They therefore do not restore the large `Microsoft.WindowsDesktop.App.Runtime.win-x64` / `Microsoft.NETCore.App.Runtime.win-x64` self-contained packs that caused the observed disk-space failure.
- `Directory.Build.props` sets `RollForward=Major`, so the net8 application can use a compatible installed .NET 8 or .NET 9 Windows Desktop runtime.
- `START-CRANEOPT-REV05.cmd` clears only NuGet's temporary scratch cache, restores **only the UI dependency graph**, builds the current UI project, then refuses to launch unless the newly built `CraneOptimizer.UI.exe` and `Xbim.Geometry.Engine64.dll` both exist.
- The official Windows acceptance gate still performs the complete solution restore/build/test sequence, generates one RID-aware lock file per project, and publishes the application/harness as **framework-dependent `win-x64`**.

## Visual Studio launch

1. Extract the ZIP fully to a normal writable folder.
2. Ensure the source drive has at least ~350 MB free (more is preferable for a full release run).
3. Open `CraneOptimizer.sln`.
4. Confirm `dotnet --version` returns `9.0.314`.
5. Allow NuGet restore to complete.
6. Select `CraneOptimizer.UI` as startup project and x64 when available.
7. Use **Build > Rebuild Solution** once before F5.

Or, from the extracted repository root, double-click `START-CRANEOPT-REV05.cmd`.

Do not choose **Run the last successful build** after a failed build; that would execute a stale binary and invalidate acceptance evidence.

## xBIM native engine restore path

`Xbim.Geometry` 5.1.820 is a small meta-package. The native `Xbim.Geometry.Engine64.dll` payload is supplied by `Xbim.Geometry.Engine.Interop` 5.1.820. REV05 therefore locates the native engine under the Interop package in the NuGet global-packages folder for build, publish, tests, and the acceptance harness. The release remains fail-closed if that payload is absent.
