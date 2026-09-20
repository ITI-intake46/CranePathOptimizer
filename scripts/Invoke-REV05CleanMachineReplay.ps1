[CmdletBinding()]
param(
    [Parameter(Mandatory=$true)][string]$PackageRoot,
    [int]$TimeoutSeconds = 120
)
$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
function Fail([string]$m){ throw "REV05 clean-machine replay failed: $m" }

$PackageRoot=(Resolve-Path $PackageRoot).Path
$runtimeLines=@(& dotnet --list-runtimes 2>$null)
$desktop=@($runtimeLines | Where-Object { $_ -match '^Microsoft\.WindowsDesktop\.App\s+(8|9)\.' })
if($LASTEXITCODE -ne 0 -or $desktop.Count -eq 0){ Fail "Framework-dependent REV05 requires Microsoft.WindowsDesktop.App 8.x or 9.x. Installed runtimes:`n$($runtimeLines -join "`n")" }
$appRoot=Join-Path $PackageRoot 'Application'
$ui=Join-Path $appRoot 'CraneOptimizer.UI.exe'
$engine=Join-Path $appRoot 'Xbim.Geometry.Engine64.dll'
$h=Join-Path $PackageRoot 'Acceptance/AcceptanceHarness/CraneOptimizer.AcceptanceHarness.exe'
$hEngine=Join-Path (Split-Path $h) 'Xbim.Geometry.Engine64.dll'
$fixture=Join-Path $PackageRoot 'Acceptance/REV05_SoftwareAcceptanceFixture'
$fxIfc=Join-Path $fixture 'Models/IFC'
$evRoot=Join-Path $PackageRoot 'Evidence'
New-Item -ItemType Directory -Force -Path $evRoot | Out-Null

foreach($required in @($ui,$engine,$h,$hEngine,(Join-Path $fixture 'project.db'),(Join-Path $fxIfc 'rev05_acceptance.ifc'))){
    if(!(Test-Path $required)){ Fail "Required artifact missing: $required" }
}

# 1. Delivered engineering harness: native xBIM geometry + reader units + close/reopen.
$hEv=Join-Path $evRoot 'clean-machine-ifc-harness.json'
& $h $fxIfc $hEv
if($LASTEXITCODE -ne 0){ Fail "AcceptanceHarness exit $LASTEXITCODE" }
$hj=Get-Content $hEv -Raw | ConvertFrom-Json
if(-not $hj.passed){ Fail 'AcceptanceHarness evidence did not pass.' }

# 2. Delivered WPF application itself: copy project to a writable temp location, launch visible UI,
# open the supported project folder, restore IFC, require native geometry, selection, close/reopen.
$tempRoot=Join-Path ([IO.Path]::GetTempPath()) ("CraneOpt-REV05-clean-"+[Guid]::NewGuid().ToString('N'))
$tempProject=Join-Path $tempRoot 'REV05_SoftwareAcceptanceFixture'
New-Item -ItemType Directory -Force -Path $tempRoot | Out-Null
Copy-Item $fixture $tempProject -Recurse -Force
$uiEv=Join-Path $evRoot 'clean-machine-ui-ifc.json'
try {
    $psi=[System.Diagnostics.ProcessStartInfo]::new($ui)
    $psi.WorkingDirectory=$appRoot
    $psi.UseShellExecute=$false
    foreach($a in @('--acceptance-project',$tempProject,'--acceptance-evidence',$uiEv,'--acceptance-hold-seconds','3','--acceptance-timeout-seconds',[string]$TimeoutSeconds)){
        [void]$psi.ArgumentList.Add($a)
    }
    $p=[System.Diagnostics.Process]::Start($psi)
    if($null -eq $p){ Fail 'Failed to start CraneOptimizer.UI.exe.' }
    if(-not $p.WaitForExit($TimeoutSeconds*1000)){
        try { $p.Kill($true) } catch {}
        Fail "CraneOptimizer.UI.exe exceeded $TimeoutSeconds seconds."
    }
    if($p.ExitCode -ne 0){ Fail "CraneOptimizer.UI.exe acceptance mode exited $($p.ExitCode)." }
    if(!(Test-Path $uiEv)){ Fail 'UI acceptance evidence file was not created.' }
    $uj=Get-Content $uiEv -Raw | ConvertFrom-Json
    if(-not $uj.passed){ Fail "UI acceptance evidence not passed: $($uj | ConvertTo-Json -Depth 6 -Compress)" }
    if(-not $uj.mainWindowVisible -or $uj.sceneElementCount -le 0 -or $uj.nativeShapeInstanceCount -le 0 -or -not $uj.selectionRoundTrip -or -not $uj.closeReopenNativeSession){
        Fail 'UI evidence does not prove visible window + scene elements + native geometry + selection + close/reopen.'
    }
    if(-not $uj.gaReady -or $uj.finalGaLiftUnits -le 0){
        Fail "UI evidence does not prove a runnable GA project: gaReady=$($uj.gaReady), finalGaLiftUnits=$($uj.finalGaLiftUnits)."
    }
}
finally {
    Remove-Item $tempRoot -Recurse -Force -ErrorAction SilentlyContinue
}

@{
    schemaVersion=1
    release='REV05'
    fixtureType='SOFTWARE ACCEPTANCE FIXTURE'
    passed=$true
    harnessEvidence=(Split-Path $hEv -Leaf)
    uiEvidence=(Split-Path $uiEv -Leaf)
    application='Application/CraneOptimizer.UI.exe'
    nativeEngine='Application/Xbim.Geometry.Engine64.dll'
    completedAtUtc=[DateTime]::UtcNow.ToString('o')
} | ConvertTo-Json -Depth 5 | Set-Content (Join-Path $evRoot 'clean-machine-replay.json') -Encoding UTF8

Write-Host 'REV05 clean-machine application + IFC replay passed.' -ForegroundColor Green
