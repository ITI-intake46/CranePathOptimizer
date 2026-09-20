#!/usr/bin/env python3
from __future__ import annotations
import argparse,datetime,hashlib,json,re
from pathlib import Path

def packages(obj):
    found={}
    def walk(x):
        if isinstance(x,dict):
            ident=x.get('id') or x.get('name')
            ver=x.get('resolvedVersion') or x.get('version')
            if ident and ver and ('requestedVersion' in x or 'resolvedVersion' in x): found[(ident,ver)]=x
            for v in x.values(): walk(v)
        elif isinstance(x,list):
            for v in x: walk(v)
    walk(obj); return found

def sid(name,ver): return 'SPDXRef-Package-'+re.sub(r'[^A-Za-z0-9.-]','-',name+'-'+ver)

def main():
    ap=argparse.ArgumentParser(); ap.add_argument('package_json'); ap.add_argument('out'); ap.add_argument('--name',default='CraneOpt-REV05'); ap.add_argument('--source-commit',default='NOASSERTION'); a=ap.parse_args()
    data=json.loads(Path(a.package_json).read_text(encoding='utf-8-sig'))
    pkgs=packages(data)
    created=datetime.datetime.now(datetime.timezone.utc).replace(microsecond=0).isoformat().replace('+00:00','Z')
    ns='https://craneopt.local/spdx/'+hashlib.sha256((a.name+a.source_commit+created).encode()).hexdigest()[:24]
    out={'spdxVersion':'SPDX-2.3','dataLicense':'CC0-1.0','SPDXID':'SPDXRef-DOCUMENT','name':a.name,'documentNamespace':ns,
         'creationInfo':{'created':created,'creators':['Tool: CraneOpt REV05 release pipeline']},'packages':[],'relationships':[]}
    rootid='SPDXRef-Package-CraneOpt'
    out['packages'].append({'name':'CraneOpt','SPDXID':rootid,'versionInfo':'REV05','downloadLocation':'NOASSERTION','filesAnalyzed':False,'licenseConcluded':'NOASSERTION','licenseDeclared':'NOASSERTION','supplier':'Organization: CraneOpt project owner','externalRefs':[{'referenceCategory':'OTHER','referenceType':'source-commit','referenceLocator':a.source_commit}]})
    out['relationships'].append({'spdxElementId':'SPDXRef-DOCUMENT','relationshipType':'DESCRIBES','relatedSpdxElement':rootid})
    for (name,ver),meta in sorted(pkgs.items(),key=lambda x:x[0][0].lower()):
        pid=sid(name,ver)
        out['packages'].append({'name':name,'SPDXID':pid,'versionInfo':ver,'downloadLocation':'NOASSERTION','filesAnalyzed':False,'licenseConcluded':'NOASSERTION','licenseDeclared':'NOASSERTION','supplier':'NOASSERTION','externalRefs':[{'referenceCategory':'PACKAGE-MANAGER','referenceType':'purl','referenceLocator':f'pkg:nuget/{name}@{ver}'}]})
        out['relationships'].append({'spdxElementId':rootid,'relationshipType':'DEPENDS_ON','relatedSpdxElement':pid})
    Path(a.out).write_text(json.dumps(out,indent=2)+'\n',encoding='utf-8')
    print(f'SBOM packages: {len(pkgs)} -> {a.out}')
if __name__=='__main__': raise SystemExit(main())
