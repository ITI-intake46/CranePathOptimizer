#!/usr/bin/env python3
"""Fail-closed REV05 SOFTWARE ACCEPTANCE FIXTURE verifier.

The default mode verifies source/data/package invariants without .NET.  Windows release
mode adds --require-runtime-evidence after the real restore/build/test/publish/xBIM/GA
execution and requires the production snapshot/report/save-reopen chain.
"""
from __future__ import annotations
import argparse, csv, hashlib, json, math, re, sqlite3, sys
from pathlib import Path

ROOT=Path(__file__).resolve().parents[1]
FIXTURE_TYPE='SOFTWARE ACCEPTANCE FIXTURE'
CAT=ROOT/'CraneOptimizer.UI/Data/CanonicalCatalog.crane.db'
CAT_MAN=ROOT/'acceptance/catalog/catalog-manifest.json'
PROJECT=ROOT/'acceptance/fixtures/REV05_SoftwareAcceptanceFixture'
PDB=PROJECT/'project.db'
IFC=PROJECT/'Models/IFC/rev05_acceptance.ifc'
MISSING_IFC=PROJECT/'Models/IFC/rev05_missing_mass_unit.ifc'
MALFORMED_EXPLICIT_IFC=PROJECT/'Models/IFC/rev05_malformed_explicit_mass_unit.ifc'
MALFORMED_PROJECT_IFC=PROJECT/'Models/IFC/rev05_malformed_project_mass_unit.ifc'
BASELINE=ROOT/'acceptance/regression-baseline.json'

class Gate:
    def __init__(self): self.checks=[]; self.failures=[]; self.warnings=[]
    def check(self,name,cond,detail=''):
        self.checks.append({'name':name,'passed':bool(cond),'detail':str(detail)})
        if not cond: self.failures.append(f'{name}: {detail}')
    def warn(self,msg): self.warnings.append(msg)

def rel(p:Path)->str:
    try: return p.relative_to(ROOT).as_posix()
    except Exception: return p.name

def sha256(p:Path)->str:
    h=hashlib.sha256()
    with p.open('rb') as f:
        for b in iter(lambda:f.read(1024*1024),b''): h.update(b)
    return h.hexdigest()

def finite_positive(x):
    try: return math.isfinite(float(x)) and float(x)>0
    except Exception: return False

def count_fact_theory(files):
    n=0
    for p in files:
        s=p.read_text(encoding='utf-8-sig',errors='ignore')
        n += len(re.findall(r'\[(?:Fact|Theory)(?:\s*\(|\s*\])',s))
    return n

def catalog_gate(g:Gate):
    g.check('T06 canonical catalog exists',CAT.is_file(),rel(CAT))
    g.check('T06 catalog manifest exists',CAT_MAN.is_file(),rel(CAT_MAN))
    if not CAT.is_file() or not CAT_MAN.is_file(): return
    man=json.loads(CAT_MAN.read_text())
    actual=sha256(CAT)
    g.check('T06 manifest hash matches canonical catalog',man.get('sha256')==actual,{'manifest':man.get('sha256'),'actual':actual})
    g.check('T06 fixture identity is exact',man.get('fixtureType')==FIXTURE_TYPE,man.get('fixtureType'))
    g.check('T06 catalog manifest count is 808',man.get('targetPointCount')==808,man.get('targetPointCount'))
    con=sqlite3.connect(CAT)
    try:
        integrity=con.execute('pragma integrity_check').fetchone()[0]
        g.check('T06 canonical catalog SQLite integrity',integrity=='ok',integrity)
        pts=con.execute('select Radius,BoomLength,Capacity,CraneSpecificationId from TargetPoints').fetchall()
        g.check('T06 exactly 808 catalog target points',len(pts)==808,len(pts))
        bad=[r for r in pts if not all(finite_positive(v) for v in r[:3])]
        g.check('E05 all engineering chart points finite/positive',not bad,{'badCount':len(bad),'sample':bad[:3]})
        cranes=con.execute('select count(*) from Cranes').fetchone()[0]; specs=con.execute('select count(*) from CraneSpecifications').fetchone()[0]
        g.check('T06 catalog has crane/spec metadata',cranes==2 and specs==2,{'cranes':cranes,'specs':specs})
        embedded=dict(con.execute("select Key,Value from CatalogManifest").fetchall())
        g.check('T06 embedded catalog identity matches companion manifest',embedded.get('CatalogId')==man.get('catalogId') and embedded.get('CatalogVersion')==man.get('catalogVersion') and embedded.get('FixtureType')==FIXTURE_TYPE,embedded)
        for row in con.execute('select Id,MinRadius,MaxRadius,MinBoomLength,MaxBoomLength,MaxCapacity from CraneSpecifications'):
            sid,mnr,mxr,mnb,mxb,cap=row
            ok=all(finite_positive(x) for x in [mnr,mxr,mnb,mxb,cap]) and mnr<=mxr and mnb<=mxb
            g.check(f'E02 spec {sid} valid min/max envelope',ok,row)
        desc=' '.join(str(x[0] or '') for x in con.execute('select LoadChartSourceDescription from CraneSpecifications'))
        g.check('T06 load-chart provenance identifies SOFTWARE ACCEPTANCE FIXTURE',FIXTURE_TYPE in desc,desc[:250])
    finally: con.close()

def project_gate(g:Gate):
    for p,n in [(PDB,'project.db'),(IFC,'geometry IFC'),(MISSING_IFC,'missing-unit IFC'),(MALFORMED_EXPLICIT_IFC,'malformed explicit-unit IFC'),(MALFORMED_PROJECT_IFC,'malformed project-unit IFC')]:
        g.check(f'T04 {n} exists',p.is_file(),rel(p))
    if not PDB.is_file(): return
    con=sqlite3.connect(PDB)
    try:
        integrity=con.execute('pragma integrity_check').fetchone()[0]
        g.check('T04 project database SQLite integrity',integrity=='ok',integrity)
        qc=lambda t: con.execute(f'select count(*) from "{t}"').fetchone()[0]
        expected={'ProjectInfo':1,'ProjectIfcFiles':1,'ProjectIfcElements':5,'SiteZones':1,'ProjectCraneSnapshots':1,'ProjectCraneLoadChartSnapshots':94,'CraneSetups':1,'ProjectViewportStates':1}
        for t,n in expected.items(): g.check(f'T04 {t} populated',qc(t)==n,{'actual':qc(t),'expected':n})
        active=con.execute('select RelativePath,FileHash,ElementCount,MetadataJson,IsCacheValid from ProjectIfcFiles where IsActive=1').fetchone()
        g.check('T04 active project IFC points to copied asset',bool(active and active[0]=='Models/IFC/rev05_acceptance.ifc'),active)
        if active and IFC.is_file():
            g.check('T04 project IFC hash reconciles',active[1]==sha256(IFC),{'db':active[1],'file':sha256(IFC)})
            g.check('T04 element count reconciles',active[2]==5,active[2])
            try: metadata=json.loads(active[3] or '{}')
            except Exception: metadata={}
            g.check('T04 portable project cache carries current normalization version',metadata.get('ifcNormalizationCacheVersion')=='ifc-identity-grid-ssot-v3',metadata)
            g.check('T04 portable project cache is explicitly valid',active[4]==1,active[4])
        ws=con.execute('select GlobalId,WeightTons,WeightTrustLevel,IncludeInGa,Role,COG_X,COG_Y,COG_Z,ElementType from ProjectIfcElements order by Id').fetchall()
        exp=[8.5,5.0,4.0,3.0,2.0]
        g.check('T03 five positive GA-eligible normalized masses',len(ws)==5 and all(r[3]==1 and finite_positive(r[1]) for r in ws),ws)
        g.check('T05 acceptance project persists five production GA-ready lift assemblies',len(ws)==5 and all(r[3]==1 and r[4]=='LiftAssembly' and r[2] in ('ExplicitQuantity','ExplicitProperty') and all(v is not None and math.isfinite(float(v)) for v in r[5:8]) and bool(r[8]) for r in ws),ws)
        if len(ws)==5: g.check('T03 normalized masses deterministic',all(abs(float(ws[i][1])-exp[i])<1e-6 for i in range(5)),ws)
        runs=con.execute('select RunName,Status,TotalElements,SafeElements,CoverageRatio from ProjectOptimizationRuns order by Id').fetchall()
        g.check('T04 project contains saved run state',len(runs)>=1,runs)
        g.check('T04 fixture run has five safe elements',any(r[0]=='REV05-FIXTURE' and r[2]==5 and r[3]==5 for r in runs),runs)
    finally: con.close()
    if IFC.is_file():
        txt=IFC.read_text(encoding='ascii',errors='replace')
        g.check('T02 acceptance IFC contains swept-solid geometry','IFCEXTRUDEDAREASOLID' in txt and 'IFCPRODUCTDEFINITIONSHAPE' in txt,'')
        g.check('T03 acceptance IFC has project kg mass unit','.MASSUNIT.,.KILO.,.GRAM.' in txt,'')
        g.check('T03 acceptance IFC has explicit tonne converted unit',".MASSUNIT.,'tonne'" in txt,'')
        g.check('T03 acceptance IFC has converted pound unit',".MASSUNIT.,'pound'" in txt,'')
    if MISSING_IFC.is_file():
        txt=MISSING_IFC.read_text(encoding='ascii',errors='replace')
        g.check('T03 missing-unit fixture omits project mass unit','IFCUNITASSIGNMENT((#10,#11,#12));' in txt,'')

def regression_gate(g:Gate):
    data=json.loads(BASELINE.read_text())
    files=list((ROOT/'CraneOptimizer.Application.Tests').rglob('*.cs')); decl=count_fact_theory(files)
    g.check('T01 test source file baseline restored',len(files)>=data['minimumTestSourceFiles'],{'files':len(files),'minimum':data['minimumTestSourceFiles']})
    g.check('T01 Fact/Theory declaration baseline restored',decl>=data['minimumStaticFactTheoryDeclarations'],{'declarations':decl,'minimum':data['minimumStaticFactTheoryDeclarations']})
    for test_rel in data['requiredPreviouslyDeletedFiles']+data['requiredRev05AcceptanceTests']:
        g.check('T01 required test present: '+Path(test_rel).name,(ROOT/test_rel).is_file(),test_rel)
    manifest_path=ROOT/'acceptance/test-integrity-manifest.json'
    g.check('T01 complete test-integrity manifest exists',manifest_path.is_file(),rel(manifest_path))
    if manifest_path.is_file():
        man=json.loads(manifest_path.read_text(encoding='utf-8-sig'))
        current={p.relative_to(ROOT).as_posix():sha256(p) for p in files}
        recorded={row.get('path'):row.get('sha256') for row in man.get('files',[]) if isinstance(row,dict)}
        g.check('T01 test manifest file count matches inventory',man.get('testSourceFileCount')==len(files)==len(recorded),{'manifest':man.get('testSourceFileCount'),'current':len(files),'recorded':len(recorded)})
        g.check('T01 test manifest declaration count matches inventory',man.get('staticFactTheoryDeclarationCount')==decl,{'manifest':man.get('staticFactTheoryDeclarationCount'),'current':decl})
        g.check('T01 every current test source hash is inventoried',recorded==current,{'missing':sorted(set(current)-set(recorded))[:10],'extra':sorted(set(recorded)-set(current))[:10],'hashMismatches':[k for k in set(current)&set(recorded) if current[k]!=recorded[k]][:10]})
    g.check('T01 baseline reconciliation document exists',(ROOT/'release/REV05-TEST-BASELINE-RECONCILIATION.md').is_file(),'')

def source_gate(g:Gate):
    reader=(ROOT/'CraneOptimizer.Infrastructure/Repositories/IfcReaderRepository.cs').read_text(encoding='utf-8-sig')
    resolver=(ROOT/'CraneOptimizer.Infrastructure/Ifc/IfcMassUnitResolver.cs').read_text(encoding='utf-8-sig')
    audit=(ROOT/'CraneOptimizer.Infrastructure/Repositories/IfcDiagnosticAuditService.cs').read_text(encoding='utf-8-sig')
    cap=(ROOT/'CraneOptimizer.Application/Optimization/GeneticAlgorithms/Services/LoadChartCapacityResolver.cs').read_text(encoding='utf-8-sig')
    validator=(ROOT/'CraneOptimizer.UI/Features/F4_LoadChart/Services/LoadChartCraneValidator.cs').read_text(encoding='utf-8-sig')
    bootstrap=(ROOT/'CraneOptimizer.Infrastructure/Persistence/CatalogDatabaseBootstrapper.cs').read_text(encoding='utf-8-sig')
    app=(ROOT/'CraneOptimizer.UI/App.xaml.cs').read_text(encoding='utf-8-sig')
    dialog=(ROOT/'CraneOptimizer.UI/Features/F8_GAOptimizer/Views/CraneOptimizerDialog.xaml.cs').read_text(encoding='utf-8-sig')
    workflow=(ROOT/'CraneOptimizer.UI/Features/F8_GAOptimizer/Workflows/CraneOptimizerDialogWorkflow.cs').read_text(encoding='utf-8-sig')
    policy=(ROOT/'CraneOptimizer.AI/Runtime/DefaultAiRequestPolicy.cs').read_text(encoding='utf-8-sig')
    classifier=(ROOT/'CraneOptimizer.AI/Runtime/AiEndpointSecurityClassifier.cs').read_text(encoding='utf-8-sig')
    infra=(ROOT/'CraneOptimizer.Infrastructure/CraneOptimizer.Infrastructure.csproj').read_text(encoding='utf-8-sig')
    ui=(ROOT/'CraneOptimizer.UI/CraneOptimizer.UI.csproj').read_text(encoding='utf-8-sig')
    report=(ROOT/'CraneOptimizer.Application/Reports/Builders/OptimizationSummaryReportBuilder.cs').read_text(encoding='utf-8-sig')
    harness=(ROOT/'CraneOptimizer.AcceptanceHarness/Program.cs').read_text(encoding='utf-8-sig')
    release=(ROOT/'scripts/Invoke-REV05Release.ps1').read_text(encoding='utf-8-sig')
    clean_replay=(ROOT/'scripts/Invoke-REV05CleanMachineReplay.ps1').read_text(encoding='utf-8-sig')
    launcher=(ROOT/'START-CRANEOPT-REV05.ps1').read_text(encoding='utf-8-sig')
    solution=(ROOT/'CraneOptimizer.sln').read_text(encoding='utf-8-sig')

    g.check('E04 reader delegates quantity units to strict resolver','IfcMassUnitResolver' in reader and 'ResolveQuantity' in reader,'')
    g.check('E04 reader has no hard-coded quantity-as-kg path',not re.search(r'quantity\.Value[^\n]{0,120}MassUnit\.Kilogram',reader,re.I),'')
    g.check('E04 explicit quantity unit precedes project unit','ExplicitQuantityUnit' in resolver and 'ProjectMassUnit' in resolver,'')
    g.check('E04 diagnostic conversion delegates to strict resolver','IfcMassUnitResolver.TryGetKilogramsPerUnit' in audit,'')
    g.check('E04 conversion units validate declared factor/base','TryGetKilogramsPerUnit(unitName' not in resolver,'')
    g.check('E02 minimum radius enforced','BelowMinimumOperatingRadius' in cap and 'MinRadius' in cap,'')
    g.check('E02 minimum boom enforced','BelowMinimumBoomLength' in cap and 'MinBoomLength' in cap,'')
    g.check('E05 every chart point finite/positive','double.IsFinite' in validator and all(k in validator for k in ['Radius','BoomLength','Capacity']),'')
    g.check('E03 live field validation wired','TxtWireLength.TextChanged += SafetyInput_TextChanged' in dialog and 'TxtAdditionalWt.TextChanged += SafetyInput_TextChanged' in dialog and 'ValidateSafetyInputs' in dialog,'')
    g.check('E03 safety validation happens before settings/optimization','ValidateSafetyInputs' in workflow and 'IsFinite' in workflow,'')
    g.check('R07 endpoint classification uses URI, not provider brand','AiEndpointSecurityClassifier.IsLocalEndpoint' in policy and 'AiEndpointSecurityClassifier' in classifier and 'ProviderKind == AiProviderKind.Ollama' not in policy,'')
    g.check('R08 catalog bootstrapper has explicit System.IO import for Windows compilation','using System.IO;' in bootstrap,'')
    g.check('R08 per-user immutable/writable catalog lifecycle','CanonicalCatalog.crane.db' in bootstrap and 'LocalApplicationData' in bootstrap,'')
    g.check('R08 canonical catalog startup identity is hash-bound to companion manifest','CanonicalCatalog.manifest.json' in bootstrap and 'ReadAndValidateCanonicalIdentity' in bootstrap and 'ComputeSha256(CanonicalCatalogPath)' in bootstrap,'')
    g.check('R08 UI always deploys canonical catalog and companion manifest','CopyToOutputDirectory>Always' in ui and 'CanonicalCatalog.manifest.json' in ui and r'acceptance\catalog\catalog-manifest.json' in ui,'')
    g.check('R08 launcher verifies runtime catalog hash before startup','Get-FileHash $sourceCatalog' in launcher and 'Get-FileHash $outputCatalog' in launcher and 'Canonical catalog provenance verified' in launcher,'')
    g.check('R08 stale catalog reconciliation is backed up','pre-catalog-reconcile' in bootstrap and 'HasSameManagedCatalogIdentity' in bootstrap,'')
    g.check('R08 pre-migration backup fail-closed','CreateVerifiedPreMigrationBackup' in bootstrap and 'zero bytes' in bootstrap.lower(),'')
    g.check('R08 app backs up before migration',app.find('CreateVerifiedPreMigrationBackup')>=0 and app.find('CreateVerifiedPreMigrationBackup')<app.find('Migrate'),'')
    g.check('R05 obsolete hard-coded crane seed namespace removed from startup','F90_CraneSeedData' not in app,'')
    g.check('T02 UI and infrastructure require native xBIM engine','Xbim.Geometry.Engine64.dll' in infra and 'Xbim.Geometry.Engine64.dll' in ui,'')
    g.check('T02 native xBIM engine is sourced from the Interop payload package',
            'xbim.geometry.engine.interop\\5.1.820\\**\\Xbim.Geometry.Engine64.dll' in infra and
            'xbim.geometry.engine.interop\\5.1.820\\**\\Xbim.Geometry.Engine64.dll' in ui, '')
    g.check('T02 native engine is not incorrectly searched in Xbim.Geometry meta-package',
            '$(NuGetPackageRoot)xbim.geometry\\5.1.820\\**\\Xbim.Geometry.Engine64.dll' not in infra and
            '$(NuGetPackageRoot)xbim.geometry\\5.1.820\\**\\Xbim.Geometry.Engine64.dll' not in ui, '')
    g.check('T08 vulnerable SQLite 2.1.10 not directly referenced','SQLitePCLRaw.lib.e_sqlite3" Version="2.1.10' not in infra,'')
    g.check('T07 exact SDK 9.0.314 pinned',(ROOT/'global.json').is_file() and '9.0.314' in (ROOT/'global.json').read_text(),'')
    g.check('T07 developer application build is framework-dependent x64','<PlatformTarget>x64</PlatformTarget>' in ui and '<SelfContained>false</SelfContained>' in ui and '<RuntimeIdentifier>win-x64</RuntimeIdentifier>' not in ui,'')
    g.check('T07 developer launcher restores/builds/starts current UI graph without self-contained runtime packs','CraneOptimizer.UI/CraneOptimizer.UI.csproj' in launcher and '-p:SelfContained=false' in launcher and 'CraneOptimizer.UI.exe' in launcher and 'Xbim.Geometry.Engine64.dll' in launcher and 'dotnet nuget locals temp --clear' in launcher.replace('& ','') ,'')
    g.check('T07 developer launcher resolves the actual x64 build output directory','CraneOptimizer.UI/bin/x64/$Configuration/net8.0-windows' in launcher and "$outputDir" in launcher,'')
    g.check('T07 solution includes standalone acceptance harness','CraneOptimizer.AcceptanceHarness' in solution,'')
    props=(ROOT/'Directory.Build.props').read_text(encoding='utf-8-sig')
    g.check('T07 lock generation/determinism configured','RestorePackagesWithLockFile' in props and 'ContinuousIntegrationBuild' in props,'')
    for key in ['SelectedChartRadius','SelectedChartBoomLength','RawChartCapacity','SafetyMargin','SelectedNumberOfLines','SelectedOperatingConditionLabel']:
        g.check('T05 report provenance column '+key,key in report,'')
    for key in ['rawMass','detectedUnit','normalizedMassTons','trustStatus','gaEligible','selection-product-roundtrip','close-reopen']:
        g.check('T02/T03 published harness evidence '+key,key in harness,'')
    g.check('T02 release publishes/runs harness','publish-acceptance-harness.log' in release and 'published-ifc-harness.json' in release,'')
    g.check('T02 clean-machine replay executes published WPF application','CraneOptimizer.UI.exe' in clean_replay and '--acceptance-project' in clean_replay and 'clean-machine-ui-ifc.json' in clean_replay,'')
    g.check('T02 WPF acceptance mode proves visible geometry/selection/reopen','--acceptance-project' in app and 'sceneElementCount' in app and 'nativeShapeInstanceCount' in app and 'selectionRoundTrip' in app and 'closeReopenNativeSession' in app,'')
    g.check('T05 WPF acceptance mode proves reopened project has GA-ready lift units','gaReadinessCandidateElements' in app and 'finalGaLiftUnits' in app and 'GaLiftUnitInputPreparation.CountLiftUnits' in app and '&& gaReady' in app,'')
    project_state=(ROOT/'CraneOptimizer.UI/Features/F10_ProjectManagement/Services/ProjectStateService.cs').read_text(encoding='utf-8-sig')
    g.check('T04 portable IFC cache identity uses SHA-256 rather than archive timestamp when hash exists','ComputeSha256Hex(absolutePath)' in project_state and 'entity.FileHash' in project_state and 'Legacy project databases may not have a hash' in project_state,'')
    g.check('T04 new project saves persist IFC SHA-256 for portable cache identity','FileHash = metadata.Sha256' in project_state and 'ComputeSha256Hex(filePath)' in project_state,'')
    g.check('T04 project-file metadata constructor paths supply SHA-256 field after schema extension',
            'new ProjectFileMetadata(prior.FileSizeBytes, prior.LastWriteUtc, string.Empty)' in project_state and
            'new ProjectFileMetadata(0, null, string.Empty)' in project_state and
            project_state.count('new ProjectFileMetadata(') == 3, '')
    g.check('E04 stale kilogram-only IFC quantity comment removed','stores IIfcQuantityWeight values in kilograms' not in reader,'')
    g.check('T07 obsolete Xbim.WindowsUI direct dependency removed','Xbim.WindowsUI' not in infra and 'Xbim.WindowsUI' not in ui,'')
    g.check('T07 release requires lock for every csproj','missingLocks' in release and 'Get-ChildItem $Root -Recurse -Filter *.csproj' in release,'')
    g.check('T08 release performs vulnerability gate','dotnet-vulnerabilities.json' in release and 'validate_nuget_vulnerabilities.py' in release,'')
    g.check('T08 release creates SBOM and Git bundle','sbom.spdx.json' in release and 'git bundle create' in release,'')


def fixture_label_gate(g:Gate):
    forbidden=['NOT ' + 'MANUFACTURER LOAD-CHART DATA','NOT FOR' + ' LIFT PLANNING']
    binary_ext={'.db','.pdf','.png','.jpg','.jpeg','.dll','.exe','.zip','.pyc','.ico','.woff','.ttf','.trx'}
    ignored_parts={'.git','bin','obj','artifacts','.vs','__pycache__'}
    hits=[]; direct_label_hits=0
    for p in ROOT.rglob('*'):
        if not p.is_file() or any(part in ignored_parts for part in p.parts) or p.suffix.lower() in binary_ext:
            continue
        try: s=p.read_text(encoding='utf-8-sig',errors='ignore')
        except Exception: continue
        direct_label_hits += s.count(FIXTURE_TYPE)
        for f in forbidden:
            if f in s: hits.append(f'{p.relative_to(ROOT)}: {f}')
    g.check('REV05 requested fixture-label policy has no removed markings anywhere in source/release text',not hits,hits[:20])
    g.check('REV05 direct SOFTWARE ACCEPTANCE FIXTURE identity is consistently present',direct_label_hits>=20,direct_label_hits)
    g.check('REV05 direct fixture label appears in catalog manifest',FIXTURE_TYPE in CAT_MAN.read_text(),rel(CAT_MAN))


def runtime_gate(g:Gate,required:bool):
    ga_root=PROJECT/'Reports/GA'; prod=list(ga_root.glob('*/ga_result_snapshot.json')) if ga_root.exists() else []
    if not prod:
        if required: g.check('Runtime production GA snapshot exists',False,'Run scripts/Invoke-REV05Release.ps1 on Windows.')
        else: g.warn('Windows .NET/xBIM/production-GA evidence is required by --require-runtime-evidence and is produced by the Windows release gate.')
        return
    g.check('Runtime exactly one production snapshot',len(prod)==1,len(prod))
    if len(prod)!=1: return
    snap_path=prod[0]; run_dir=snap_path.parent; snap=json.loads(snap_path.read_text(encoding='utf-8-sig'))
    g.check('Runtime snapshot is production v3',snap.get('schemaVersion')==3 and snap.get('snapshotType')=='CraneOptimizer.GaResult',snap.get('schemaVersion'))
    run=snap.get('run',{}); summary=snap.get('summary',{}); units=snap.get('liftUnits',[]); outs=snap.get('finalOutcomes',[]); positions=snap.get('positions',[])
    total=summary.get('totalTargetLiftUnitCount'); lifted=summary.get('liftedLiftUnitCount')
    g.check('Runtime snapshot completed',run.get('status')=='Completed' and bool(run.get('completedAtUtc')),run)
    g.check('Runtime row counts reconcile',total==len(units)==len(outs) and lifted==sum(1 for x in outs if x.get('isLifted')),{'total':total,'units':len(units),'outcomes':len(outs),'lifted':lifted})
    g.check('Runtime IFC hash reconciles',snap.get('model',{}).get('ifcSha256')==sha256(IFC),snap.get('model',{}).get('ifcSha256'))
    evals={e.get('evaluationId'):e for p in positions for e in p.get('evaluations',[])}
    con=sqlite3.connect(CAT)
    try:
        spec=con.execute('select cs.Id,cs.MinRadius,cs.MinBoomLength from CraneSpecifications cs join Cranes c on c.Id=cs.CraneId where c.Name=?',(snap.get('crane',{}).get('craneName'),)).fetchone()
        g.check('Runtime crane exists in canonical catalog',spec is not None,snap.get('crane',{}).get('craneName'))
        if spec:
            sid,minr,minb=spec
            for out in outs:
                if not out.get('isLifted'): continue
                ev=evals.get(out.get('finalEvaluationId')); rid=out.get('liftUnitId')
                if not ev: g.check('Runtime lifted outcome has evaluation '+str(rid),False,out); continue
                r,b,req,avail=[ev.get(k) for k in ['radiusM','boomLengthM','requiredCapacityTon','availableCapacityTon']]
                ok=all(x is not None and math.isfinite(float(x)) for x in [r,b,req,avail]) and float(r)>=minr-1e-9 and float(b)>=minb-1e-9 and float(req)<=float(avail)+1e-9
                g.check('Runtime envelope/capacity valid '+str(rid),ok,ev)
                sr,sb,raw=[ev.get(k) for k in ['selectedChartRadiusM','selectedChartBoomLengthM','rawChartCapacityTon']]
                if sr is not None and sb is not None and raw is not None:
                    rows=con.execute('select Capacity from TargetPoints where CraneSpecificationId=? and abs(Radius-?)<1e-7 and abs(BoomLength-?)<1e-7',(sid,float(sr),float(sb))).fetchall()
                    g.check('Runtime catalog chart lookup '+str(rid),bool(rows) and any(abs(float(x[0])-float(raw))<1e-6 for x in rows),{'selected':[sr,sb],'raw':raw,'catalog':rows[:5]})
    finally: con.close()
    pdf=run_dir/'lifting_report.pdf'; csv_path=run_dir/'csv/element_lift_details.csv'; vr=run_dir/'save_reopen_verification.json'; prov=run_dir/'report_provenance.json'
    g.check('Runtime substantive PDF exists',pdf.is_file() and pdf.stat().st_size>=1024,pdf.stat().st_size if pdf.exists() else 0)
    g.check('Runtime detailed CSV exists',csv_path.is_file() and csv_path.stat().st_size>=100,csv_path.stat().st_size if csv_path.exists() else 0)
    csv_rows=[]
    if csv_path.is_file():
        csv_rows=list(csv.DictReader(csv_path.open(encoding='utf-8-sig')))
        required_cols={'ElementId','Radius','BoomLength','RequiredCapacity','AvailableCapacity','SafetyMargin','SelectedChartRadius','SelectedChartBoomLength','RawChartCapacity','SelectedNumberOfLines','SelectedOperatingConditionLabel'}
        g.check('T05 runtime CSV provenance schema',bool(csv_rows) and required_cols.issubset(set(csv_rows[0])),list(csv_rows[0]) if csv_rows else [])
        final_by_lift={}
        for out in outs:
            ev=evals.get(out.get('finalEvaluationId'))
            if ev: final_by_lift[str(out.get('liftUnitId'))]=ev
        g.check('T05 runtime CSV row count equals final outcomes',len(csv_rows)==len(final_by_lift),{'csvRows':len(csv_rows),'finalOutcomes':len(final_by_lift)})
        def f(v):
            try: return float(v)
            except Exception: return None
        def close(a,b,tol=1e-6):
            aa,bb=f(a),f(b); return aa is not None and bb is not None and abs(aa-bb)<=tol
        field_map={
            'Radius':'radiusM','BoomLength':'boomLengthM','RequiredCapacity':'requiredCapacityTon',
            'AvailableCapacity':'availableCapacityTon','SafetyMargin':'safetyMarginTon',
            'SelectedChartRadius':'selectedChartRadiusM','SelectedChartBoomLength':'selectedChartBoomLengthM',
            'RawChartCapacity':'rawChartCapacityTon'
        }
        for row in csv_rows:
            rid=str(row.get('ElementId','')); ev=final_by_lift.get(rid)
            g.check('T05 CSV row maps to final evaluation '+rid,ev is not None,row)
            if ev is None: continue
            for csv_key,snap_key in field_map.items():
                if ev.get(snap_key) is None: continue
                g.check(f'T05 CSV {csv_key} reconciles '+rid,close(row.get(csv_key),ev.get(snap_key)),{'csv':row.get(csv_key),'snapshot':ev.get(snap_key)})
            if ev.get('selectedNumberOfLines') is not None:
                g.check('T05 CSV lines reconcile '+rid,str(row.get('SelectedNumberOfLines','')).strip()==str(ev.get('selectedNumberOfLines')),{'csv':row.get('SelectedNumberOfLines'),'snapshot':ev.get('selectedNumberOfLines')})
            if ev.get('selectedOperatingConditionLabel') is not None:
                g.check('T05 CSV operating condition reconciles '+rid,str(row.get('SelectedOperatingConditionLabel','')).strip()==str(ev.get('selectedOperatingConditionLabel')).strip(),{'csv':row.get('SelectedOperatingConditionLabel'),'snapshot':ev.get('selectedOperatingConditionLabel')})
    g.check('T05 report provenance sidecar exists',prov.is_file(),rel(prov))
    if prov.is_file():
        pr=json.loads(prov.read_text(encoding='utf-8-sig'))
        g.check('T05 provenance optimization run matches snapshot',pr.get('optimizationRunId')==run.get('optimizationRunId'),{'provenance':pr.get('optimizationRunId'),'snapshot':run.get('optimizationRunId')})
        g.check('T05 provenance row count matches CSV',pr.get('rowCount')==len(csv_rows),{'provenance':pr.get('rowCount'),'csv':len(csv_rows)})
        g.check('T05 provenance PDF hash binds exact report bytes',pdf.is_file() and pr.get('pdf',{}).get('sha256')==sha256(pdf),pr.get('pdf'))
        g.check('T05 provenance CSV hash binds exact report bytes',csv_path.is_file() and pr.get('csv',{}).get('sha256')==sha256(csv_path),pr.get('csv'))
        prow={str(r.get('ElementId','')):r for r in pr.get('rows',[]) if isinstance(r,dict)}
        for row in csv_rows:
            rid=str(row.get('ElementId','')); rr=prow.get(rid)
            g.check('T05 provenance row exists '+rid,rr is not None,rid)
            if rr is None: continue
            for k in ['Radius','BoomLength','RequiredCapacity','AvailableCapacity','SafetyMargin','SelectedChartRadius','SelectedChartBoomLength','RawChartCapacity']:
                if k in rr and str(rr[k]).strip()!='':
                    g.check(f'T05 provenance {k} matches CSV '+rid,close(row.get(k),rr.get(k)),{'csv':row.get(k),'provenance':rr.get(k)})
    if vr.is_file():
        v=json.loads(vr.read_text()); g.check('Runtime save/reopen invariant',v.get('snapshotReopened') is True and v.get('canonicalPayloadHashUnchanged') is True,v)
        g.check('T05 save/reopen evidence references report provenance',bool(v.get('reportProvenance')),v.get('reportProvenance'))
    else: g.check('Runtime save/reopen evidence exists',False,vr)


def release_assets_gate(g:Gate):
    required=['README.md','SECURITY.md','LICENSE','THIRD-PARTY-NOTICES.md','release/REV05-ACCEPTANCE-CHECKLIST.md','release/REV05-TEST-BASELINE-RECONCILIATION.md','release/REV05-RELEASE-MANIFEST.template.json','release/REV05-FINAL-ACCEPTANCE-STATUS.md','release/REV05-LAUNCH-COMPATIBILITY.md','scripts/Invoke-REV05Release.ps1','scripts/Invoke-REV05CleanMachineReplay.ps1','scripts/run_rev05_portable_acceptance.py','scripts/package_rev05_software_acceptance.py','scripts/verify_rev05_acceptance.py','scripts/generate_source_release_evidence.py','scripts/generate_test_integrity_manifest.py','START-CRANEOPT-REV05.ps1','START-CRANEOPT-REV05.cmd','acceptance/test-integrity-manifest.json','CraneOptimizer.AcceptanceHarness/CraneOptimizer.AcceptanceHarness.csproj','CraneOptimizer.AcceptanceHarness/Program.cs','.github/workflows/rev05-acceptance.yml']
    for rel in required: g.check('Release asset exists: '+rel,(ROOT/rel).is_file(),rel)


def main():
    ap=argparse.ArgumentParser(); ap.add_argument('--require-runtime-evidence',action='store_true'); ap.add_argument('--json-out')
    a=ap.parse_args(); g=Gate()
    catalog_gate(g); project_gate(g); regression_gate(g); source_gate(g); fixture_label_gate(g); release_assets_gate(g); runtime_gate(g,a.require_runtime_evidence)
    out={'schemaVersion':2,'release':'REV05','fixtureType':FIXTURE_TYPE,'passed':not g.failures,'checkCount':len(g.checks),'failureCount':len(g.failures),'warningCount':len(g.warnings),'checks':g.checks,'failures':g.failures,'warnings':g.warnings}
    print(json.dumps(out,indent=2))
    if a.json_out:
        p=Path(a.json_out); p.parent.mkdir(parents=True,exist_ok=True); p.write_text(json.dumps(out,indent=2)+'\n',newline='\n')
    return 0 if not g.failures else 1
if __name__=='__main__': raise SystemExit(main())
