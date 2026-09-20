#!/usr/bin/env python3
from __future__ import annotations
import argparse, csv, hashlib, json, py_compile, re, shutil, sqlite3, subprocess, sys, tempfile, xml.etree.ElementTree as ET
from pathlib import Path

ROOT=Path(__file__).resolve().parents[1]; DEFAULT_E=ROOT/'acceptance/evidence'; FIXTURE_TYPE='SOFTWARE ACCEPTANCE FIXTURE'

def sha(p): return hashlib.sha256(p.read_bytes()).hexdigest()
def run(cmd):
    p=subprocess.run(cmd,cwd=ROOT,text=True,capture_output=True); return p.returncode,p.stdout,p.stderr

def main():
    ap=argparse.ArgumentParser(); ap.add_argument('--archive-mode',action='store_true'); ap.add_argument('--json-out'); a=ap.parse_args()
    outpath=Path(a.json_out).resolve() if a.json_out else DEFAULT_E/'REV05-portable-acceptance.json'
    E=outpath.parent; E.mkdir(parents=True,exist_ok=True)
    checks=[]
    def ck(name,ok,detail=''): checks.append({'name':name,'passed':bool(ok),'detail':str(detail)})
    cat=ROOT/'CraneOptimizer.UI/Data/CanonicalCatalog.crane.db'; man=ROOT/'acceptance/catalog/catalog-manifest.json'; proj=ROOT/'acceptance/fixtures/REV05_SoftwareAcceptanceFixture/project.db'; fixture=ROOT/'acceptance/fixtures/REV05_SoftwareAcceptanceFixture'; run_dir=fixture/'Reports/GA/REV05-FIXTURE'
    tracked=[cat,man,proj,fixture/'Models/IFC/rev05_acceptance.ifc',run_dir/'ga_result_snapshot.fixture.json',run_dir/'lifting_results.fixture.csv',run_dir/'lifting_report.fixture.pdf']
    first=None
    for i in range(2):
        rc,o,e=run([sys.executable,'acceptance/catalog/generate_acceptance_catalog.py']); ck(f'catalog-generator-run-{i+1}',rc==0,'ok' if rc==0 else (e or o[-500:]))
        rc,o,e=run([sys.executable,'acceptance/fixtures/generate_acceptance_fixture.py']); ck(f'fixture-generator-run-{i+1}',rc==0,'ok' if rc==0 else (e or o[-500:]))
        hashes={p.relative_to(ROOT).as_posix():sha(p) for p in tracked}
        if first is None: first=hashes
        else: ck('byte-deterministic-software-acceptance-fixture',hashes==first,{'first':first,'second':hashes})
    for db in [cat,proj]:
        con=sqlite3.connect(db); v=con.execute('pragma integrity_check').fetchone()[0]; con.close(); ck('sqlite-integrity:'+db.name,v=='ok',v)
    con=sqlite3.connect(proj)
    ifc_row=con.execute("select RelativePath,FileHash,MetadataJson,IsCacheValid from ProjectIfcFiles where IsActive=1").fetchone()
    try: ifc_meta=json.loads(ifc_row[2] or '{}') if ifc_row else {}
    except Exception: ifc_meta={}
    fixture_ifc=fixture/'Models/IFC/rev05_acceptance.ifc'
    ck('project-cache-current-normalization-version',bool(ifc_row) and ifc_meta.get('ifcNormalizationCacheVersion')=='ifc-identity-grid-ssot-v3',ifc_meta)
    ck('project-cache-ifc-sha256-reconciles',bool(ifc_row) and ifc_row[1]==sha(fixture_ifc),{'db':ifc_row[1] if ifc_row else None,'file':sha(fixture_ifc)})
    ga_rows=con.execute("select Role,IncludeInGa,WeightTons,WeightTrustLevel,COG_X,COG_Y,COG_Z,ElementType from ProjectIfcElements order by Id").fetchall()
    ck('project-five-persisted-ga-ready-lift-assemblies',len(ga_rows)==5 and all(r[0]=='LiftAssembly' and r[1]==1 and float(r[2])>0 and r[3] in ('ExplicitQuantity','ExplicitProperty') and all(v is not None for v in r[4:7]) and bool(r[7]) for r in ga_rows),ga_rows)
    before=con.execute("select RunName,Status,TotalElements,SafeElements from ProjectOptimizationRuns where RunName='REV05-FIXTURE'").fetchone(); con.close()
    con=sqlite3.connect(proj); after=con.execute("select RunName,Status,TotalElements,SafeElements from ProjectOptimizationRuns where RunName='REV05-FIXTURE'").fetchone(); con.close(); ck('project-db-close-reopen-invariant',before==after and after==('REV05-FIXTURE','SoftwareAcceptanceFixtureSeed',5,5),{'before':before,'after':after})
    con=sqlite3.connect(cat); count=con.execute('select count(*) from TargetPoints').fetchone()[0]; ck('catalog-808-points',count==808,count)
    manifest=json.loads(man.read_text()); ck('catalog-fixture-type',manifest.get('fixtureType')==FIXTURE_TYPE,manifest.get('fixtureType'))
    snap=json.loads((run_dir/'ga_result_snapshot.fixture.json').read_text()); rows=list(csv.DictReader((run_dir/'lifting_results.fixture.csv').open()))
    ck('fixture-five-elements',len(rows)==5 and snap.get('totalElementCount')==5,{'csvRows':len(rows),'snapshot':snap.get('totalElementCount')})
    spec=con.execute("select cs.Id from CraneSpecifications cs join Cranes c on c.Id=cs.CraneId where c.Name='STC800-ACCEPTANCE-FIXTURE'").fetchone()[0]
    for sr,cr in zip(snap['elements'],rows):
        ok=all(abs(float(sr[k])-float(cr[k]))<1e-6 for k in ['radius_m','boom_m','weight_t','required_t','chart_capacity_t','effective_capacity_t','margin_t'])
        ck('fixture-csv-snapshot:'+sr['element'],ok)
        caps=con.execute('select Capacity from TargetPoints where CraneSpecificationId=? and abs(Radius-?)<1e-8 and abs(BoomLength-?)<1e-8',(spec,sr['radius_m'],sr['boom_m'])).fetchall()
        ck('fixture-catalog-snapshot:'+sr['element'],any(abs(x[0]-sr['chart_capacity_t'])<1e-6 for x in caps),caps)
    con.close(); pdf=(run_dir/'lifting_report.fixture.pdf').read_bytes(); ck('fixture-pdf-substantive',pdf.startswith(b'%PDF-1.4') and len(pdf)>1000,len(pdf))
    rc,o,e=run([sys.executable,'scripts/verify_rev05_acceptance.py']); ck('static-verifier',rc==0,'ok' if rc==0 else (e or o[-1500:]))
    with tempfile.TemporaryDirectory(prefix='rev05-source-evidence-') as td:
        rc,o,e=run([sys.executable,'scripts/generate_source_release_evidence.py','--out-dir',td]); ck('source-release-evidence-generator',rc==0,'ok' if rc==0 else (e or o[-500:]))
    py_files=list((ROOT/'scripts').glob('*.py'))+list((ROOT/'acceptance/catalog').glob('*.py'))+list((ROOT/'acceptance/fixtures').glob('*.py'))
    try:
        for p in py_files: py_compile.compile(str(p),doraise=True)
        ck('python-sources-compile',True,len(py_files))
    except Exception as ex: ck('python-sources-compile',False,ex)
    test_files=list((ROOT/'CraneOptimizer.Application.Tests').rglob('*.cs')); text='\n'.join(p.read_text(errors='ignore') for p in test_files); decl=len(re.findall(r'\[(?:Fact|Theory)(?:\([^\]]*\))?\]',text)); ck('regression-baseline-preserved',len(test_files)>=114 and decl>=1041,{'files':len(test_files),'declarations':decl})
    xmls=list(ROOT.rglob('*.csproj'))+[ROOT/'Directory.Build.props']
    for p in xmls:
        try: ET.parse(p); ck('xml:'+p.relative_to(ROOT).as_posix(),True)
        except Exception as ex: ck('xml:'+p.relative_to(ROOT).as_posix(),False,ex)
    jsons=list((ROOT/'acceptance').rglob('*.json'))+list((ROOT/'release').rglob('*.json'))+[ROOT/'global.json']
    for p in jsons:
        if p.resolve()==outpath.resolve(): continue
        try: json.loads(p.read_text(encoding='utf-8-sig')); ck('json:'+p.relative_to(ROOT).as_posix(),True)
        except Exception as ex: ck('json:'+p.relative_to(ROOT).as_posix(),False,ex)
    prohibited=[]
    for base in [ROOT/'acceptance',ROOT/'release',ROOT/'scripts']:
        for p in base.rglob('*'):
            if not p.is_file() or p.suffix.lower() in {'.db','.pdf','.pyc','.png','.jpg','.zip'}: continue
            s=p.read_text(encoding='utf-8-sig',errors='ignore')
            for phrase in ['NOT ' + 'MANUFACTURER LOAD-CHART DATA','NOT FOR' + ' LIFT PLANNING']:
                if phrase in s: prohibited.append(f'{p.relative_to(ROOT)}:{phrase}')
    ck('requested-markings-removed',not prohibited,prohibited[:20])
    if (ROOT/'.git').is_dir() and not a.archive_mode:
        rc,o,e=run(['git','diff','--check']); ck('git-diff-check',rc==0,e)
    else:
        ck('source-archive-mode-without-git',not (ROOT/'.git').exists(),'archive-mode')
        ck('source-archive-has-release-controls',(ROOT/'release/REV05-ACCEPTANCE-CHECKLIST.md').is_file() and (ROOT/'scripts/Invoke-REV05Release.ps1').is_file())
    failed=[c for c in checks if not c['passed']]
    out={'schemaVersion':3,'release':'REV05','fixtureType':FIXTURE_TYPE,'evidenceKind':'portable-software-acceptance-fixture-execution','archiveMode':bool(a.archive_mode or not (ROOT/'.git').is_dir()),'passed':not failed,'checkCount':len(checks),'failureCount':len(failed),'checks':checks,'limitations':['Windows WPF/native xBIM and Authenticode execution are performed by the included Windows release gate; this portable host does not execute Windows binaries.']}
    outpath.parent.mkdir(parents=True,exist_ok=True); outpath.write_text(json.dumps(out,indent=2)+'\n',newline='\n')
    print(json.dumps({'passed':out['passed'],'checkCount':len(checks),'failureCount':len(failed),'archiveMode':out['archiveMode']},indent=2)); return 0 if not failed else 1
if __name__=='__main__': raise SystemExit(main())
