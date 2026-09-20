[CmdletBinding()]
param(
    [string]$SigningCertificateThumbprint,
    [string]$TimestampUrl = "http://timestamp.digicert.com",
    [switch]$SimulationMode,
    [switch]$SkipUiSmoke
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

function Fail([string]$Message) { throw "REV05 RELEASE GATE FAILED: $Message" }
function Run([string]$File, [string[]]$Arguments, [string]$Log) {
    Write-Host "> $File $($Arguments -join ' ')" -ForegroundColor Cyan
    $dir = Split-Path -Parent $Log; if ($dir) { New-Item -ItemType Directory -Force -Path $dir | Out-Null }
    & $File @Arguments 2>&1 | Tee-Object -FilePath $Log
    if ($LASTEXITCODE -ne 0) { Fail "$File exited $LASTEXITCODE. See $Log" }
}

$Root = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
Set-Location $Root
if (-not $IsWindows) { Fail 'Official acceptance execution requires Windows x64.' }
if ([Environment]::Is64BitOperatingSystem -ne $true) { Fail 'A 64-bit Windows OS is required.' }

$Artifacts = Join-Path $Root 'artifacts/release'
$Evidence = Join-Path $Artifacts 'evidence'
$Publish = Join-Path $Artifacts 'publish/win-x64'
$Stage = Join-Path $Artifacts 'stage'
Remove-Item $Artifacts -Recurse -Force -ErrorAction SilentlyContinue
New-Item -ItemType Directory -Force -Path $Evidence,$Publish,$Stage | Out-Null

# Source provenance must be exact before generators/build outputs touch the tree.
$gitStatus = (& git status --porcelain)
if ($gitStatus) { Fail "Official source tree must start clean. Commit the REV05 patch before release. Dirty entries:`n$gitStatus" }
$Commit = (& git rev-parse HEAD).Trim()
$Tag = (& git describe --tags --exact-match 2>$null)
if (-not $SimulationMode -and [string]::IsNullOrWhiteSpace($Tag)) { Fail 'Official mode requires the source commit to have an exact Git tag.' }
if ([string]::IsNullOrWhiteSpace($Tag)) { $Tag = 'REV05-SOFTWARE-ACCEPTANCE-FIXTURE-UNTAGGED' }
$env:CRANEOPT_GIT_COMMIT = $Commit
$env:CRANEOPT_GIT_TAG = $Tag
@{ commit=$Commit; tag=$Tag; initialTree='clean'; mode=($(if($SimulationMode){'simulation'}else{'official'})) } | ConvertTo-Json | Set-Content (Join-Path $Evidence 'source-provenance.json') -Encoding UTF8

# Exact SDK from global.json.
$requiredSdk = (Get-Content global.json -Raw | ConvertFrom-Json).sdk.version
$actualSdk = (& dotnet --version).Trim()
if ($actualSdk -ne $requiredSdk) { Fail "SDK mismatch. Required $requiredSdk, got $actualSdk." }
& dotnet --info | Set-Content (Join-Path $Evidence 'dotnet-info.txt') -Encoding UTF8

# Deterministically reconstruct the committed acceptance data and ensure it does not drift.
Run 'python' @('acceptance/catalog/generate_acceptance_catalog.py') (Join-Path $Evidence 'generate-catalog.log')
Run 'python' @('acceptance/fixtures/generate_acceptance_fixture.py') (Join-Path $Evidence 'generate-fixture.log')
$generatorDiff = (& git diff --exit-code -- acceptance CraneOptimizer.UI/Data/CanonicalCatalog.crane.db)
if ($LASTEXITCODE -ne 0) { $generatorDiff | Set-Content (Join-Path $Evidence 'generator-drift.diff'); Fail 'Committed acceptance data is not reproducible from its generators.' }
Run 'python' @('scripts/verify_rev05_acceptance.py','--json-out',(Join-Path $Evidence 'static-verification.json')) (Join-Path $Evidence 'static-verification.log')

# First restore creates/refreshes lock files; the second restore proves they are sufficient.
Run 'dotnet' @('restore','CraneOptimizer.sln','--use-lock-file','-r','win-x64','-p:RestoreLockedMode=false') (Join-Path $Evidence 'restore-create-locks.log')
Run 'dotnet' @('restore','CraneOptimizer.AcceptanceHarness/CraneOptimizer.AcceptanceHarness.csproj','--use-lock-file','-r','win-x64','-p:RestoreLockedMode=false') (Join-Path $Evidence 'restore-harness-create-lock.log')
$projects = @(Get-ChildItem $Root -Recurse -Filter *.csproj | Where-Object { $_.FullName -notmatch '[\/](bin|obj|artifacts)[\/]' })
$missingLocks = @($projects | Where-Object { -not (Test-Path (Join-Path $_.DirectoryName 'packages.lock.json')) })
if ($missingLocks.Count -gt 0) { Fail ("Missing packages.lock.json for: " + (($missingLocks | ForEach-Object { $_.FullName.Substring($Root.Length+1).Replace('\','/') }) -join ', ')) }
$locks = @(Get-ChildItem $Root -Recurse -Filter packages.lock.json | Where-Object { $_.FullName -notmatch '[\/](bin|obj|artifacts)[\/]' })
$lockIndex = foreach ($lock in $locks) { [pscustomobject]@{ path=$lock.FullName.Substring($Root.Length+1).Replace('\\','/'); sha256=(Get-FileHash $lock.FullName -Algorithm SHA256).Hash.ToLowerInvariant() } }
$lockIndex | ConvertTo-Json | Set-Content (Join-Path $Evidence 'package-lock-index.json') -Encoding UTF8
$lockEvidenceRoot = Join-Path $Evidence 'package-locks'
foreach ($lock in $locks) {
    $rel = $lock.FullName.Substring($Root.Length + 1)
    $dest = Join-Path $lockEvidenceRoot $rel
    New-Item -ItemType Directory -Force -Path (Split-Path -Parent $dest) | Out-Null
    Copy-Item $lock.FullName $dest -Force
}
Run 'dotnet' @('restore','CraneOptimizer.sln','--locked-mode','-r','win-x64') (Join-Path $Evidence 'restore-locked.log')
Run 'dotnet' @('restore','CraneOptimizer.AcceptanceHarness/CraneOptimizer.AcceptanceHarness.csproj','--locked-mode','-r','win-x64') (Join-Path $Evidence 'restore-harness-locked.log')

Run 'dotnet' @('build','CraneOptimizer.sln','-c','Release','-p:Platform=x64','--no-restore') (Join-Path $Evidence 'build-release-x64.log')

$trxDir = Join-Path $Evidence 'test-results'; New-Item -ItemType Directory -Force -Path $trxDir | Out-Null
Run 'dotnet' @('test','CraneOptimizer.Application.Tests/CraneOptimizer.Application.Tests.csproj','-c','Release','-p:Platform=x64','--no-restore','--logger',"trx;LogFileName=REV05-full.trx",'--results-directory',$trxDir) (Join-Path $Evidence 'test-full.log')
Run 'dotnet' @('test','CraneOptimizer.Application.Tests/CraneOptimizer.Application.Tests.csproj','-c','Release','-p:Platform=x64','--no-restore','--filter','FullyQualifiedName~Acceptance','--logger',"trx;LogFileName=REV05-acceptance.trx",'--results-directory',$trxDir) (Join-Path $Evidence 'test-focused-acceptance.log')

Run 'dotnet' @('publish','CraneOptimizer.UI/CraneOptimizer.UI.csproj','-c','Release','-r','win-x64','--self-contained','false','-p:Platform=x64','--no-restore','-o',$Publish) (Join-Path $Evidence 'publish-win-x64.log')
$engine = Join-Path $Publish 'Xbim.Geometry.Engine64.dll'
if (-not (Test-Path $engine) -or (Get-Item $engine).Length -le 0) { Fail 'Published Xbim.Geometry.Engine64.dll is missing/empty.' }

$harnessPublish = Join-Path $Artifacts 'acceptance-harness'
Run 'dotnet' @('publish','CraneOptimizer.AcceptanceHarness/CraneOptimizer.AcceptanceHarness.csproj','-c','Release','-r','win-x64','--self-contained','false','-p:Platform=x64','--no-restore','-o',$harnessPublish) (Join-Path $Evidence 'publish-acceptance-harness.log')
$harnessExe=Join-Path $harnessPublish 'CraneOptimizer.AcceptanceHarness.exe'
$harnessEngine=Join-Path $harnessPublish 'Xbim.Geometry.Engine64.dll'
if (-not (Test-Path $harnessExe) -or -not (Test-Path $harnessEngine)) { Fail 'Published acceptance harness/native xBIM engine missing.' }
$fixtureIfcDir=Join-Path $Root 'acceptance/fixtures/REV05_SoftwareAcceptanceFixture/Models/IFC'
Run $harnessExe @($fixtureIfcDir,(Join-Path $Evidence 'published-ifc-harness.json')) (Join-Path $Evidence 'published-ifc-harness.log')
if ($SimulationMode) {
  $clean=Join-Path $Artifacts 'clean-machine-sim'; Remove-Item $clean -Recurse -Force -ErrorAction SilentlyContinue; New-Item -ItemType Directory -Force -Path $clean | Out-Null
  Copy-Item "$harnessPublish\*" $clean -Recurse -Force
  Run (Join-Path $clean 'CraneOptimizer.AcceptanceHarness.exe') @($fixtureIfcDir,(Join-Path $Evidence 'clean-directory-ifc-harness.json')) (Join-Path $Evidence 'clean-directory-ifc-harness.log')
}

# Smoke proves the delivered package starts, but never substitutes for the IFC tests above.
if (-not $SkipUiSmoke) {
    $exe = Join-Path $Publish 'CraneOptimizer.UI.exe'; if (-not (Test-Path $exe)) { Fail 'Published UI executable missing.' }
    $p = Start-Process -FilePath $exe -PassThru
    Start-Sleep -Seconds 10
    if ($p.HasExited) { Fail "Published application exited during 10-second smoke with code $($p.ExitCode)." }
    Stop-Process -Id $p.Id -Force
    @{ started=$true; aliveAfterSeconds=10; closedByHarness=$true } | ConvertTo-Json | Set-Content (Join-Path $Evidence 'ui-smoke.json') -Encoding UTF8
} elseif (-not $SimulationMode) { Fail '-SkipUiSmoke is permitted only in SimulationMode.' }

# Replace prior production evidence but retain the committed SOFTWARE ACCEPTANCE FIXTURE seed evidence.
$gaRoot = Join-Path $Root 'acceptance/fixtures/REV05_SoftwareAcceptanceFixture/Reports/GA'
Get-ChildItem $gaRoot -Directory | Where-Object { $_.Name -ne 'REV05-FIXTURE' } | Remove-Item -Recurse -Force
Remove-Item (Join-Path $Root 'artifacts/acceptance') -Recurse -Force -ErrorAction SilentlyContinue
Run 'dotnet' @('run','--project','CraneOptimizer.GAExperiment/CraneOptimizer.GAExperiment.csproj','-c','Release','--no-build','--','acceptance/ga/REV05-GAExperimentConfig.json') (Join-Path $Evidence 'ga-acceptance-run.log')
Run 'python' @('scripts/verify_rev05_acceptance.py','--require-runtime-evidence','--json-out',(Join-Path $Evidence 'runtime-reconciliation.json')) (Join-Path $Evidence 'runtime-reconciliation.log')

# Resolved dependency graph + vulnerability gate.
$packageJson = Join-Path $Evidence 'dotnet-packages.json'
& dotnet list CraneOptimizer.sln package --include-transitive --format json | Set-Content $packageJson -Encoding UTF8
if ($LASTEXITCODE -ne 0) { Fail 'dotnet list package failed.' }
$vulnJson = Join-Path $Evidence 'dotnet-vulnerabilities.json'
& dotnet list CraneOptimizer.sln package --vulnerable --include-transitive --format json | Set-Content $vulnJson -Encoding UTF8
if ($LASTEXITCODE -ne 0) { Fail 'dotnet vulnerability audit command failed.' }
Run 'python' @('scripts/validate_nuget_vulnerabilities.py',$vulnJson) (Join-Path $Evidence 'vulnerability-gate.log')
Run 'python' @('scripts/generate_sbom.py',$packageJson,(Join-Path $Evidence 'sbom.spdx.json'),'--name','CraneOpt-REV05','--source-commit',$Commit) (Join-Path $Evidence 'sbom-generation.log')

# Sign first-party executable assemblies only; never rewrite third-party binaries.
$firstParty = @(Get-ChildItem $Publish -File | Where-Object { $_.Name -match '^CraneOptimizer\..*\.(exe|dll)$' })
if ($firstParty.Count -eq 0) { Fail 'No first-party published assemblies found for signing.' }
$cert = $null
if ($SimulationMode) {
    if ($SigningCertificateThumbprint) { $cert = Get-Item "Cert:\CurrentUser\My\$SigningCertificateThumbprint" }
    else {
        $cert = New-SelfSignedCertificate -Type CodeSigningCert -Subject 'CN=CraneOpt REV05 SOFTWARE ACCEPTANCE FIXTURE' -CertStoreLocation 'Cert:\CurrentUser\My' -NotAfter (Get-Date).AddDays(7)
        $SigningCertificateThumbprint = $cert.Thumbprint
    }
} else {
    if ([string]::IsNullOrWhiteSpace($SigningCertificateThumbprint)) { Fail 'Official mode requires -SigningCertificateThumbprint.' }
    $cert = Get-Item "Cert:\CurrentUser\My\$SigningCertificateThumbprint" -ErrorAction Stop
}
foreach ($f in $firstParty) {
    $params = @{ FilePath=$f.FullName; Certificate=$cert; HashAlgorithm='SHA256' }
    if (-not $SimulationMode) { $params.TimestampServer=$TimestampUrl }
    $sig = Set-AuthenticodeSignature @params
    if ($sig.Status -notin @('Valid','UnknownError')) { Fail "Signing failed for $($f.Name): $($sig.Status) $($sig.StatusMessage)" }
}
$signatureEvidence = foreach ($f in $firstParty) { $s=Get-AuthenticodeSignature $f.FullName; [pscustomobject]@{ file=$f.Name; status=$s.Status.ToString(); subject=$s.SignerCertificate.Subject; thumbprint=$s.SignerCertificate.Thumbprint; simulation=[bool]$SimulationMode } }
$signatureEvidence | ConvertTo-Json | Set-Content (Join-Path $Evidence 'signatures.json') -Encoding UTF8
if (-not $SimulationMode -and @($signatureEvidence | Where-Object status -ne 'Valid').Count -gt 0) { Fail 'At least one official first-party signature is not Valid.' }

# Stage one coherent release: app + project/fixtures + source with generated locks + evidence/provenance.
New-Item -ItemType Directory -Force -Path (Join-Path $Stage 'Application'),(Join-Path $Stage 'Acceptance'),(Join-Path $Stage 'Evidence'),(Join-Path $Stage 'Source'),(Join-Path $Stage 'Release') | Out-Null
Copy-Item "$Publish\*" (Join-Path $Stage 'Application') -Recurse -Force
Copy-Item $harnessPublish (Join-Path $Stage 'Acceptance/AcceptanceHarness') -Recurse -Force
Copy-Item 'acceptance/fixtures/REV05_SoftwareAcceptanceFixture' (Join-Path $Stage 'Acceptance/REV05_SoftwareAcceptanceFixture') -Recurse -Force
Copy-Item 'acceptance/catalog' (Join-Path $Stage 'Acceptance/catalog') -Recurse -Force
Copy-Item 'acceptance/REV05_ACCEPTANCE_MATRIX.md' (Join-Path $Stage 'Acceptance') -Force
Copy-Item "$Evidence\*" (Join-Path $Stage 'Evidence') -Recurse -Force
Copy-Item -Path @('README.md','SECURITY.md','LICENSE','THIRD-PARTY-NOTICES.md','START-CRANEOPT-REV05.ps1','START-CRANEOPT-REV05.cmd',(Join-Path $Root 'release/REV05-ACCEPTANCE-CHECKLIST.md')) -Destination $Stage -Force
Copy-Item 'scripts/Invoke-REV05CleanMachineReplay.ps1' (Join-Path $Stage 'Release/Invoke-REV05CleanMachineReplay.ps1') -Force

# Git-native provenance plus a build-source tree that includes generated package locks.
$sourceZip = Join-Path $Stage 'Source/source.zip'
& git archive --format=zip "--output=$sourceZip" HEAD
if ($LASTEXITCODE -ne 0) { Fail 'git archive failed.' }
$sourceBundle = Join-Path $Stage 'Source/source.bundle'
& git bundle create $sourceBundle --all
if ($LASTEXITCODE -ne 0) { Fail 'git bundle failed.' }
$sourceTree = Join-Path $Stage 'Source/tree'; New-Item -ItemType Directory -Force -Path $sourceTree | Out-Null
$exclude = @('.git','bin','obj','artifacts')
Get-ChildItem $Root -Force | Where-Object { $exclude -notcontains $_.Name } | ForEach-Object { Copy-Item $_.FullName $sourceTree -Recurse -Force }

# Exercise the staged package itself, including the delivered WPF application acceptance mode.
Run 'pwsh' @('-NoProfile','-ExecutionPolicy','Bypass','-File',(Join-Path $Stage 'Release/Invoke-REV05CleanMachineReplay.ps1'),'-PackageRoot',$Stage) (Join-Path $Evidence 'staged-package-replay.log')
# Replay writes evidence inside the staged package; mirror it back into the release evidence root too.
foreach($f in @('clean-machine-ifc-harness.json','clean-machine-ui-ifc.json','clean-machine-replay.json')) {
    $src=Join-Path $Stage "Evidence/$f"; if(Test-Path $src){ Copy-Item $src (Join-Path $Evidence $f) -Force }
}

Copy-Item (Join-Path $Evidence 'sbom.spdx.json') (Join-Path $Stage 'sbom.spdx.json') -Force
$mode = if ($SimulationMode) { 'fixture' } else { 'official' }
Run 'python' @('scripts/generate_release_manifest.py',$Stage,(Join-Path $Stage 'release-manifest.json'),'--mode',$mode) (Join-Path $Evidence 'manifest-generation.log')

$sumPath = Join-Path $Stage 'SHA256SUMS.txt'
$lines = Get-ChildItem $Stage -Recurse -File | Where-Object { $_.FullName -ne $sumPath } | Sort-Object FullName | ForEach-Object {
    $rel=$_.FullName.Substring($Stage.Length+1).Replace('\\','/'); $hash=(Get-FileHash $_.FullName -Algorithm SHA256).Hash.ToLowerInvariant(); "$hash  $rel"
}
$lines | Set-Content $sumPath -Encoding ASCII

$zipName = if ($SimulationMode) { 'CraneOpt-REV05-SOFTWARE-ACCEPTANCE-FIXTURE-WINDOWS.zip' } else { 'CraneOpt-REV05-acceptance-submission.zip' }
$zip = Join-Path $Artifacts $zipName
Compress-Archive -Path "$Stage\*" -DestinationPath $zip -CompressionLevel Optimal -Force
$zipHash=(Get-FileHash $zip -Algorithm SHA256).Hash.ToLowerInvariant()
@{ path=$zip; sha256=$zipHash; bytes=(Get-Item $zip).Length; mode=$mode; sourceCommit=$Commit; sourceTag=$Tag } | ConvertTo-Json | Set-Content (Join-Path $Artifacts 'package.json') -Encoding UTF8
Write-Host "REV05 RELEASE GATE PASSED: $zip" -ForegroundColor Green
Write-Host "SHA-256: $zipHash" -ForegroundColor Green
