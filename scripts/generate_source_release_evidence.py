#!/usr/bin/env python3
from __future__ import annotations
import argparse, hashlib, json, re, subprocess, xml.etree.ElementTree as ET
from pathlib import Path

ROOT=Path(__file__).resolve().parents[1]
FIXTURE_TYPE='SOFTWARE ACCEPTANCE FIXTURE'

def git(*args):
    try: return subprocess.check_output(['git',*args],cwd=ROOT,text=True,stderr=subprocess.DEVNULL).strip()
    except Exception: return ''

def package_refs():
    out=[]
    for p in sorted(ROOT.rglob('*.csproj')):
        if any(x in p.parts for x in ('bin','obj','artifacts')): continue
        root=ET.parse(p).getroot()
        for node in root.iter():
            if node.tag.split('}')[-1]!='PackageReference': continue
            name=node.attrib.get('Include') or node.attrib.get('Update')
            ver=node.attrib.get('Version')
            if not ver:
                for ch in node:
                    if ch.tag.split('}')[-1]=='Version': ver=(ch.text or '').strip()
            if name:
                out.append({'project':p.relative_to(ROOT).as_posix(),'id':name,'version':ver or ''})
    return out

def spdx_id(name,ver): return 'SPDXRef-Package-'+re.sub(r'[^A-Za-z0-9.-]','-',f'{name}-{ver}')

def main():
    ap=argparse.ArgumentParser(); ap.add_argument('--out-dir',default='acceptance/evidence'); a=ap.parse_args()
    outdir=(ROOT/a.out_dir).resolve() if not Path(a.out_dir).is_absolute() else Path(a.out_dir); outdir.mkdir(parents=True,exist_ok=True)
    refs=package_refs(); unique={ (r['id'],r['version']) for r in refs }
    wildcard=[r for r in refs if not r['version'] or any(x in r['version'] for x in ['*','[',']','(',')',','])]
    banned=[r for r in refs if r['id'].lower()=='sqlitepclraw.lib.e_sqlite3' and r['version']=='2.1.10']
    commit=git('rev-parse','HEAD') or 'SOURCE-ARCHIVE'
    tag=git('describe','--tags','--exact-match','HEAD')
    inv={'schemaVersion':1,'release':'REV05','fixtureType':FIXTURE_TYPE,'sourceCommit':commit,'sourceTag':tag or None,'projectCount':len({r['project'] for r in refs}),'directReferenceCount':len(refs),'uniqueDirectPackageCount':len(unique),'references':refs}
    (outdir/'REV05-direct-package-inventory.json').write_text(json.dumps(inv,indent=2)+'\n',newline='\n')
    audit={'schemaVersion':1,'release':'REV05','fixtureType':FIXTURE_TYPE,'passed':not wildcard and not banned,'checks':{
        'allDirectVersionsPinned':not wildcard,
        'blockedSQLitePCLRaw_2_1_10Absent':not banned,
        'sqlitePCLRawDirectVersion':next((r['version'] for r in refs if r['id'].lower()=='sqlitepclraw.lib.e_sqlite3'),None),
        'globalJsonPinned':json.loads((ROOT/'global.json').read_text())['sdk']['version'],
        'runtimeIdentifier':'win-x64'
    },'wildcardOrUnpinned':wildcard,'blockedReferences':banned,
    'scope':'source-declared direct package references; Windows release gate performs the resolved/transitive vulnerability scan'}
    (outdir/'REV05-dependency-policy-audit.json').write_text(json.dumps(audit,indent=2)+'\n',newline='\n')
    ns='https://craneopt.local/spdx/rev05/'+hashlib.sha256(commit.encode()).hexdigest()[:24]
    rootid='SPDXRef-Package-CraneOpt-REV05'
    packages=[{'name':'CraneOpt','SPDXID':rootid,'versionInfo':'REV05','downloadLocation':'NOASSERTION','filesAnalyzed':False,'licenseConcluded':'NOASSERTION','licenseDeclared':'NOASSERTION','supplier':'Organization: CraneOpt project owner','externalRefs':[{'referenceCategory':'OTHER','referenceType':'source-commit','referenceLocator':commit}]}]
    relationships=[{'spdxElementId':'SPDXRef-DOCUMENT','relationshipType':'DESCRIBES','relatedSpdxElement':rootid}]
    for name,ver in sorted(unique,key=lambda x:x[0].lower()):
        pid=spdx_id(name,ver); packages.append({'name':name,'SPDXID':pid,'versionInfo':ver,'downloadLocation':'NOASSERTION','filesAnalyzed':False,'licenseConcluded':'NOASSERTION','licenseDeclared':'NOASSERTION','supplier':'NOASSERTION','externalRefs':[{'referenceCategory':'PACKAGE-MANAGER','referenceType':'purl','referenceLocator':f'pkg:nuget/{name}@{ver}'}]}); relationships.append({'spdxElementId':rootid,'relationshipType':'DEPENDS_ON','relatedSpdxElement':pid})
    sbom={'spdxVersion':'SPDX-2.3','dataLicense':'CC0-1.0','SPDXID':'SPDXRef-DOCUMENT','name':'CraneOpt REV05 source-declared dependency SBOM','documentNamespace':ns,'creationInfo':{'created':'2026-09-20T00:00:00Z','creators':['Tool: CraneOpt REV05 source evidence generator']},'documentComment':'SOFTWARE ACCEPTANCE FIXTURE source SBOM. Windows release mode replaces/augments this with the resolved transitive package inventory.','packages':packages,'relationships':relationships}
    (outdir/'REV05-source-sbom.spdx.json').write_text(json.dumps(sbom,indent=2)+'\n',newline='\n')
    print(json.dumps({'passed':audit['passed'],'directReferences':len(refs),'uniquePackages':len(unique),'outDir':str(outdir)},indent=2))
    return 0 if audit['passed'] else 1
if __name__=='__main__': raise SystemExit(main())
