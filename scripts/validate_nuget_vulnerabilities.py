#!/usr/bin/env python3
from __future__ import annotations
import argparse,json,sys
from pathlib import Path

def walk(obj,path='root'):
    if isinstance(obj,dict):
        vul=obj.get('vulnerabilities')
        if isinstance(vul,list) and vul:
            yield path,obj
        for k,v in obj.items():
            yield from walk(v,f'{path}.{k}')
    elif isinstance(obj,list):
        for i,v in enumerate(obj): yield from walk(v,f'{path}[{i}]')

def main():
    ap=argparse.ArgumentParser(); ap.add_argument('report'); a=ap.parse_args()
    p=Path(a.report); d=json.loads(p.read_text(encoding='utf-8-sig'))
    hits=list(walk(d))
    if hits:
        print('Vulnerable resolved NuGet packages detected:',file=sys.stderr)
        for path,obj in hits:
            print(path, json.dumps(obj,ensure_ascii=False),file=sys.stderr)
        return 2
    print('NuGet vulnerability gate: PASS (no vulnerability entries in resolved-package report)')
    return 0
if __name__=='__main__': raise SystemExit(main())
