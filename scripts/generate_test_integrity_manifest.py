#!/usr/bin/env python3
"""Generate a deterministic manifest of the complete current test source inventory."""
from __future__ import annotations
import hashlib, json, re
from pathlib import Path

ROOT=Path(__file__).resolve().parents[1]
TEST_ROOT=ROOT/'CraneOptimizer.Application.Tests'
OUT=ROOT/'acceptance/test-integrity-manifest.json'

def sha256(p:Path)->str:
    return hashlib.sha256(p.read_bytes()).hexdigest()

rows=[]; declarations=0
for p in sorted(TEST_ROOT.rglob('*.cs')):
    text=p.read_text(encoding='utf-8-sig',errors='ignore')
    count=len(re.findall(r'\[(?:Fact|Theory)(?:\s*\(|\s*\])',text))
    declarations += count
    rows.append({
        'path':p.relative_to(ROOT).as_posix(),
        'sha256':sha256(p),
        'factTheoryDeclarations':count,
        'bytes':p.stat().st_size,
    })
obj={
    'schemaVersion':1,
    'release':'REV05',
    'fixtureType':'SOFTWARE ACCEPTANCE FIXTURE',
    'testSourceFileCount':len(rows),
    'staticFactTheoryDeclarationCount':declarations,
    'reviewedBaselineMinimums':{'testSourceFiles':114,'factTheoryDeclarations':1041},
    'files':rows,
}
OUT.parent.mkdir(parents=True,exist_ok=True)
OUT.write_text(json.dumps(obj,indent=2)+'\n',encoding='utf-8',newline='\n')
print(json.dumps({k:obj[k] for k in ['testSourceFileCount','staticFactTheoryDeclarationCount']},indent=2))
