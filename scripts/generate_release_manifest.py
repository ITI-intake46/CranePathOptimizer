#!/usr/bin/env python3
from __future__ import annotations
import argparse,datetime,hashlib,json,os,sqlite3,subprocess
from pathlib import Path

def sha(p):
    h=hashlib.sha256()
    with p.open('rb') as f:
        for b in iter(lambda:f.read(1024*1024),b''): h.update(b)
    return h.hexdigest()
def git(*args):
    try:return subprocess.check_output(['git',*args],text=True,stderr=subprocess.DEVNULL).strip()
    except:return ''
def main():
    ap=argparse.ArgumentParser(); ap.add_argument('stage'); ap.add_argument('out'); ap.add_argument('--mode',choices=['official','fixture'],default='official'); a=ap.parse_args()
    root=Path(__file__).resolve().parents[1]; stage=Path(a.stage); out=Path(a.out)
    cat=root/'CraneOptimizer.UI/Data/CanonicalCatalog.crane.db'; con=sqlite3.connect(cat); points=con.execute('select count(*) from TargetPoints').fetchone()[0]; con.close()
    runtime=list((root/'acceptance/fixtures/REV05_SoftwareAcceptanceFixture/Reports/GA').glob('*/ga_result_snapshot.json'))
    files=[]
    for p in sorted(stage.rglob('*')):
        if p.is_file() and p.resolve()!=out.resolve(): files.append({'path':p.relative_to(stage).as_posix(),'bytes':p.stat().st_size,'sha256':sha(p)})
    d={'schemaVersion':2,'release':'REV05','fixtureType':'SOFTWARE ACCEPTANCE FIXTURE','generatedAtUtc':datetime.datetime.now(datetime.timezone.utc).isoformat(),'mode':a.mode,'deploymentModel':'framework-dependent win-x64','sdk':'9.0.314','sourceCommit':os.getenv('CRANEOPT_GIT_COMMIT') or git('rev-parse','HEAD'),'sourceTag':os.getenv('CRANEOPT_GIT_TAG') or git('describe','--tags','--exact-match'),'sourceTreeStatus':'clean-required-for-official','catalog':{'sha256':sha(cat),'targetPointCount':points,'fixtureType':'SOFTWARE ACCEPTANCE FIXTURE'},'acceptance':{'productionSnapshotCount':len(runtime),'project':'acceptance/fixtures/REV05_SoftwareAcceptanceFixture'},'files':files}
    out.parent.mkdir(parents=True,exist_ok=True); out.write_text(json.dumps(d,indent=2)+'\n',encoding='utf-8')
    print(out)
if __name__=='__main__': raise SystemExit(main())
