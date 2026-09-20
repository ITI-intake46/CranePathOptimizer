[CmdletBinding()]
param(
    [ValidateSet('Debug','Release')][string]$Configuration = 'Debug'
)
$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$root=(Resolve-Path $PSScriptRoot).Path
Set-Location $root

function Fail([string]$m){ throw "REV05 launcher failed: $m" }
function FreeGiB([string]$path){
    $item=Get-Item $path
    $drive=Get-PSDrive -Name $item.PSDrive.Name
    return [math]::Round($drive.Free/1GB,2)
}

$required=(Get-Content (Join-Path $root 'global.json') -Raw | ConvertFrom-Json).sdk.version
try { $resolved=(& dotnet --version 2>$null).Trim() } catch { Fail ".NET SDK is not available on PATH. This package expects SDK $required x64." }
if($LASTEXITCODE -ne 0 -or [string]::IsNullOrWhiteSpace($resolved)){ Fail ".NET SDK is not available on PATH. This package expects SDK $required x64." }
Write-Host "CraneOpt REV05 launchable source (framework-dependent developer build)" -ForegroundColor Cyan
Write-Host "Required SDK : $required"
Write-Host "Resolved SDK : $resolved"
if($resolved -ne $required){
    $installed=@(& dotnet --list-sdks 2>$null)
    Fail "global.json requires SDK $required but dotnet resolved $resolved. Installed SDKs:`n$($installed -join "`n")"
}
if(-not [Environment]::Is64BitOperatingSystem){ Fail 'Windows x64 is required.' }

$free=FreeGiB $root
Write-Host "Free space on source drive: $free GiB"
if($free -lt 0.35){
    Fail "The source drive has only $free GiB free. Free at least 350 MB before restore/build. The previous self-contained launcher needed much more space; REV05 now uses a framework-dependent developer build, but NuGet still requires working space."
}

# Best-effort removal of stale NuGet scratch left by interrupted restores. This does not delete the package cache.
try {
    & dotnet nuget locals temp --clear | Out-Host
} catch {
    Write-Warning "Could not clear NuGet temp cache; continuing because this is only cleanup: $($_.Exception.Message)"
}

$uiProject=Join-Path $root 'CraneOptimizer.UI/CraneOptimizer.UI.csproj'
Write-Host "Restoring only the application dependency graph (no self-contained runtime packs)..." -ForegroundColor Cyan
& dotnet restore $uiProject -p:RestoreLockedMode=false --disable-parallel
if($LASTEXITCODE -ne 0){
    Fail 'NuGet restore failed. If the message says a file is in use, close Visual Studio/other dotnet restore processes and run this launcher again. If it says disk space, free additional space and retry.'
}

Write-Host "Building current application: $Configuration / x64 / framework-dependent..." -ForegroundColor Cyan
& dotnet build $uiProject -c $Configuration -p:Platform=x64 -p:SelfContained=false --no-restore
if($LASTEXITCODE -ne 0){ Fail 'Build failed. Do not run a previous successful build; inspect the build errors above.' }

$outputDir=Join-Path $root "CraneOptimizer.UI/bin/x64/$Configuration/net8.0-windows"
$exe=Join-Path $outputDir 'CraneOptimizer.UI.exe'
$engine=Join-Path $outputDir 'Xbim.Geometry.Engine64.dll'
if(-not (Test-Path $exe)){ Fail "Build reported success but the expected application executable does not exist: $exe" }
if(-not (Test-Path $engine)){ Fail "Build reported success but the native IFC geometry engine is missing: $engine" }

# Verify that the runtime receives the exact canonical acceptance catalog and companion manifest.
$sourceCatalog=Join-Path $root 'CraneOptimizer.UI/Data/CanonicalCatalog.crane.db'
$outputCatalog=Join-Path $outputDir 'Data/CanonicalCatalog.crane.db'
$sourceManifest=Join-Path $root 'acceptance/catalog/catalog-manifest.json'
$outputManifest=Join-Path $outputDir 'Data/CanonicalCatalog.manifest.json'
foreach($requiredFile in @($sourceCatalog,$outputCatalog,$sourceManifest,$outputManifest)){
    if(-not (Test-Path $requiredFile)){ Fail "Required catalog provenance file is missing after build: $requiredFile" }
}
$sourceHash=(Get-FileHash $sourceCatalog -Algorithm SHA256).Hash
$outputHash=(Get-FileHash $outputCatalog -Algorithm SHA256).Hash
$catalogManifest=Get-Content $sourceManifest -Raw | ConvertFrom-Json
if($sourceHash -ne $outputHash){ Fail "Runtime canonical catalog differs from the source catalog. Source=$sourceHash Output=$outputHash" }
if($sourceHash -ne ([string]$catalogManifest.sha256).ToUpperInvariant()){ Fail "Source canonical catalog hash does not match acceptance/catalog/catalog-manifest.json." }
Copy-Item -LiteralPath $sourceManifest -Destination $outputManifest -Force
Write-Host "Canonical catalog provenance verified: $sourceHash" -ForegroundColor Green

# net8.0-windows may run on an installed .NET 8 Desktop Runtime or roll forward to the installed .NET 9 Desktop Runtime.
$runtimeLines=@(& dotnet --list-runtimes 2>$null)
$desktop=@($runtimeLines | Where-Object { $_ -match '^Microsoft\.WindowsDesktop\.App\s+(8|9)\.' })
if($desktop.Count -eq 0){
    Fail "No compatible Microsoft.WindowsDesktop.App 8.x/9.x runtime is installed. Installed runtimes:`n$($runtimeLines -join "`n")"
}
Write-Host "Compatible Windows Desktop runtime: $($desktop[0])" -ForegroundColor Green

$env:DOTNET_ROLL_FORWARD='Major'
Write-Host "Launching verified current build:`n$exe" -ForegroundColor Green
$p=Start-Process -FilePath $exe -WorkingDirectory (Split-Path $exe -Parent) -PassThru
Write-Host "CraneOptimizer.UI started with process id $($p.Id)." -ForegroundColor Green
