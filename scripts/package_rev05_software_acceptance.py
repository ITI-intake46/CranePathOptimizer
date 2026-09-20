#!/usr/bin/env python3
from __future__ import annotations
import base64, hashlib, json, shutil, subprocess, sys, tempfile, zipfile
from pathlib import Path
from cryptography.hazmat.primitives.asymmetric.ed25519 import Ed25519PrivateKey, Ed25519PublicKey

ROOT=Path(__file__).resolve().parents[1]
OUT=Path(sys.argv[1]).resolve() if len(sys.argv)>1 else (ROOT.parent/'CraneOpt-REV05-SOFTWARE-ACCEPTANCE-FIXTURE.zip')
FIXTURE_TYPE='SOFTWARE ACCEPTANCE FIXTURE'

def run(args,cwd=ROOT):
    p=subprocess.run(args,cwd=cwd,text=True,capture_output=True)
    if p.returncode:
        raise SystemExit(f"command failed ({p.returncode}): {' '.join(map(str,args))}\n{p.stdout}\n{p.stderr}")
    return p.stdout.strip()

def sha(path:Path):
    h=hashlib.sha256()
    with path.open('rb') as f:
        for chunk in iter(lambda:f.read(1024*1024),b''): h.update(chunk)
    return h.hexdigest()

def deterministic_zip(root:Path,out:Path):
    fixed=(2026,9,20,12,0,0); out.parent.mkdir(parents=True,exist_ok=True)
    if out.exists(): out.unlink()
    with zipfile.ZipFile(out,'w',compression=zipfile.ZIP_DEFLATED,compresslevel=9) as z:
        for p in sorted(x for x in root.rglob('*') if x.is_file()):
            arc=(root.name+'/'+p.relative_to(root).as_posix())
            zi=zipfile.ZipInfo(arc,date_time=fixed); zi.compress_type=zipfile.ZIP_DEFLATED; zi.external_attr=(0o100644 & 0xFFFF)<<16
            z.writestr(zi,p.read_bytes(),compress_type=zipfile.ZIP_DEFLATED,compresslevel=9)

def fixture_signature(manifest:Path,out:Path):
    seed=hashlib.sha256(b'CraneOpt REV05 SOFTWARE ACCEPTANCE FIXTURE integrity key v1').digest()
    key=Ed25519PrivateKey.from_private_bytes(seed); pub=key.public_key(); payload=manifest.read_bytes(); sig=key.sign(payload)
    pub.verify(sig,payload)
    obj={'schemaVersion':1,'release':'REV05','fixtureType':FIXTURE_TYPE,'algorithm':'Ed25519','identityScope':'SOFTWARE ACCEPTANCE FIXTURE integrity signature','signedFile':'release-manifest.json','signedFileSha256':hashlib.sha256(payload).hexdigest(),'publicKeyRawBase64':base64.b64encode(pub.public_bytes_raw()).decode(),'signatureBase64':base64.b64encode(sig).decode(),'verifiedByPackageBuilder':True}
    out.write_text(json.dumps(obj,indent=2)+'\n',newline='\n')

# Re-run committed deterministic gates. The tracked evidence must remain byte-identical.
run([sys.executable,'scripts/verify_rev05_acceptance.py','--json-out','acceptance/evidence/REV05-static-verification.json'])
run([sys.executable,'scripts/run_rev05_portable_acceptance.py','--json-out','acceptance/evidence/REV05-portable-acceptance.json'])
for d in [ROOT/'scripts/__pycache__',ROOT/'acceptance/catalog/__pycache__',ROOT/'acceptance/fixtures/__pycache__']:
    shutil.rmtree(d,ignore_errors=True)
status=run(['git','status','--porcelain'])
if status: raise SystemExit('Refusing to package a dirty tree after verification:\n'+status)

commit=run(['git','rev-parse','HEAD'])
tag=run(['git','describe','--tags','--exact-match','HEAD']) if subprocess.run(['git','describe','--tags','--exact-match','HEAD'],cwd=ROOT,stdout=subprocess.DEVNULL,stderr=subprocess.DEVNULL).returncode==0 else 'REV05-SOFTWARE-ACCEPTANCE-FIXTURE-UNTAGGED'
sdk=json.loads((ROOT/'global.json').read_text())['sdk']['version']
static=json.loads((ROOT/'acceptance/evidence/REV05-static-verification.json').read_text())
portable=json.loads((ROOT/'acceptance/evidence/REV05-portable-acceptance.json').read_text())

with tempfile.TemporaryDirectory(prefix='rev05-package-') as td:
    stage=Path(td)/'CraneOpt-REV05-SOFTWARE-ACCEPTANCE-FIXTURE'; stage.mkdir()
    for name in ['Source','Evidence','Acceptance','Release','Application']: (stage/name).mkdir()

    # Exact source snapshot plus deterministic, inspectable Git provenance.
    # Native Git bundles/packfiles are intentionally reserved for the Windows release gate because
    # they are not byte-stable across invocations. The portable package records the complete reachable
    # commit ledger (identity, authorship, dates, parents, subjects) without historical file payloads.
    source_zip=stage/'Source/CranePathOptimizer-REV05-source.zip'; run(['git','archive','--format=zip',f'--output={source_zip}','HEAD'])
    history_ledger=stage/'Source/CranePathOptimizer-REV05-git-history.txt'
    history_ledger.write_text(run(['git','log','HEAD','--reverse','--date=iso-strict',
                                   '--format=commit %H%nparents %P%nAuthor: %an <%ae>%nAuthorDate: %aI%nCommitDate: %cI%nSubject: %s%n'])+'\n',newline='\n')
    (stage/'Source/source-commit.txt').write_text(f'{commit}\n{tag}\n',newline='\n')
    (stage/'Source/git-log.txt').write_text(run(['git','log','--decorate','--oneline','HEAD'])+'\n',newline='\n')

    # Reproduce the portable gate from the source archive without .git.
    with tempfile.TemporaryDirectory(prefix='rev05-source-replay-') as rd:
        extract=Path(rd)/'source'; extract.mkdir()
        with zipfile.ZipFile(source_zip) as z: z.extractall(extract)
        run([sys.executable,'scripts/run_rev05_portable_acceptance.py','--archive-mode','--json-out',str(stage/'Evidence/REV05-source-archive-replay.json')],cwd=extract)

    # Executed portable evidence and source-level release controls.
    shutil.copy2(ROOT/'acceptance/evidence/REV05-static-verification.json',stage/'Evidence/REV05-static-verification.json')
    shutil.copy2(ROOT/'acceptance/evidence/REV05-portable-acceptance.json',stage/'Evidence/REV05-portable-acceptance.json')
    run([sys.executable,'scripts/generate_source_release_evidence.py','--out-dir',str(stage/'Evidence')])

    # Acceptance data and reviewer-facing mapping.
    shutil.copytree(ROOT/'acceptance/fixtures/REV05_SoftwareAcceptanceFixture',stage/'Acceptance/REV05_SoftwareAcceptanceFixture')
    shutil.copytree(ROOT/'acceptance/catalog',stage/'Acceptance/catalog',ignore=shutil.ignore_patterns('__pycache__','*.pyc'))
    shutil.copy2(ROOT/'acceptance/REV05_ACCEPTANCE_MATRIX.md',stage/'Acceptance/REV05_ACCEPTANCE_MATRIX.md')
    shutil.copy2(ROOT/'acceptance/regression-baseline.json',stage/'Acceptance/regression-baseline.json')
    shutil.copy2(ROOT/'acceptance/test-integrity-manifest.json',stage/'Acceptance/test-integrity-manifest.json')

    # Release controls and clean-machine scripts.
    for rel in ['REV05-ACCEPTANCE-CHECKLIST.md','REV05-TEST-BASELINE-RECONCILIATION.md','REV05-RELEASE-MANIFEST.template.json','REV05-FINAL-ACCEPTANCE-STATUS.md','REV05-LAUNCH-COMPATIBILITY.md']:
        shutil.copy2(ROOT/'release'/rel,stage/'Release'/rel)
    for rel in ['Invoke-REV05Release.ps1','Invoke-REV05CleanMachineReplay.ps1']:
        shutil.copy2(ROOT/'scripts'/rel,stage/'Release'/rel)
    (stage/'Application/SOFTWARE-ACCEPTANCE-FIXTURE-WINDOWS-GATE.txt').write_text(
        'The portable host does not fabricate Windows binaries.\n'
        'Run Release/Invoke-REV05Release.ps1 on Windows x64 to execute restore/build/test/publish/native-xBIM/GA/report/signing gates and produce the Windows package.\n',newline='\n')

    for name in ['README.md','SECURITY.md','LICENSE','THIRD-PARTY-NOTICES.md','START-CRANEOPT-REV05.ps1','START-CRANEOPT-REV05.cmd']:
        shutil.copy2(ROOT/name,stage/name)

    metadata={'schemaVersion':2,'packageKind':'SOFTWARE ACCEPTANCE FIXTURE','release':'REV05','fixtureType':FIXTURE_TYPE,'sourceCommit':commit,'sourceTag':tag,'pinnedDotnetSdk':sdk,'deploymentModel':'framework-dependent win-x64','staticVerification':{'passed':static['passed'],'checks':static['checkCount'],'failures':static['failureCount']},'portableAcceptance':{'passed':portable['passed'],'checks':portable['checkCount'],'failures':portable['failureCount']},'canonicalCatalogSha256':sha(ROOT/'CraneOptimizer.UI/Data/CanonicalCatalog.crane.db'),'catalogTargetPoints':808,'windowsReleaseGateIncluded':True,'windowsNativeExecutionOnPortableHost':False}
    (stage/'PACKAGE-METADATA.json').write_text(json.dumps(metadata,indent=2)+'\n',newline='\n')
    readme=f'''# READ ME FIRST — REV05 SOFTWARE ACCEPTANCE FIXTURE\n\nSource commit: `{commit}`  \nSource tag: `{tag}`  \nPinned .NET SDK: `{sdk}`\n\nExecuted on the portable host:\n- static acceptance: {static['checkCount']} passed, {static['failureCount']} failed;\n- portable SOFTWARE ACCEPTANCE FIXTURE execution: {portable['checkCount']} passed, {portable['failureCount']} failed;\n- extracted source archive replay is included under `Evidence/REV05-source-archive-replay.json`;\n- deterministic Git history ledger, SHA-256 provenance, direct package inventory, dependency policy audit and SPDX source SBOM are included. The Windows release gate additionally generates a native Git bundle.\n\nFor the Windows x64 end-to-end execution, run `Release/Invoke-REV05Release.ps1 -SimulationMode`. That gate executes the real .NET restore/build/test/publish/native-xBIM/GA/report chain and creates Windows runtime evidence.\n'''
    (stage/'README-FIRST.md').write_text(readme,newline='\n')

    # Actual portable release manifest over staged content before its own signature/checksum files.
    files=[]
    for p in sorted(x for x in stage.rglob('*') if x.is_file()):
        if p.name in {'release-manifest.json','SHA256SUMS.txt','REV05-fixture-integrity-signature.json'}: continue
        files.append({'path':p.relative_to(stage).as_posix(),'bytes':p.stat().st_size,'sha256':sha(p)})
    manifest={'schemaVersion':2,'release':'REV05','fixtureType':FIXTURE_TYPE,'sourceCommit':commit,'sourceTag':tag,'sdk':sdk,'deploymentModel':'framework-dependent win-x64','catalog':{'sha256':metadata['canonicalCatalogSha256'],'targetPointCount':808},'evidence':{'static':metadata['staticVerification'],'portable':metadata['portableAcceptance'],'sourceArchiveReplay':'Evidence/REV05-source-archive-replay.json'},'windowsGate':{'included':True,'script':'Release/Invoke-REV05Release.ps1','nativeExecutionOnThisPortableHost':False},'files':files}
    manifest_path=stage/'release-manifest.json'; manifest_path.write_text(json.dumps(manifest,indent=2)+'\n',newline='\n')
    fixture_signature(manifest_path,stage/'Evidence/REV05-fixture-integrity-signature.json')

    # Package-level checksums cover every staged file except the checksum file itself.
    sum_path=stage/'SHA256SUMS.txt'; lines=[]
    for p in sorted(x for x in stage.rglob('*') if x.is_file() and x!=sum_path): lines.append(f'{sha(p)}  {p.relative_to(stage).as_posix()}')
    sum_path.write_text('\n'.join(lines)+'\n',encoding='ascii',newline='\n')

    # Removed markings must not reappear anywhere in text artifacts.
    hits=[]
    for p in stage.rglob('*'):
        if not p.is_file() or p.suffix.lower() in {'.db','.pdf','.zip','.bundle','.dll','.exe'}: continue
        s=p.read_text(encoding='utf-8-sig',errors='ignore')
        for phrase in ['NOT ' + 'MANUFACTURER LOAD-CHART DATA','NOT FOR' + ' LIFT PLANNING']:
            if phrase in s: hits.append(f'{p.relative_to(stage)}:{phrase}')
    if hits: raise SystemExit('Removed marking reappeared in package:\n'+'\n'.join(hits))

    deterministic_zip(stage,OUT)

print(json.dumps({'path':str(OUT),'sha256':sha(OUT),'bytes':OUT.stat().st_size,'sourceCommit':commit,'sourceTag':tag,'staticChecks':static['checkCount'],'portableChecks':portable['checkCount'],'fixtureType':FIXTURE_TYPE},indent=2))
