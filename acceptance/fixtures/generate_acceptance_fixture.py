#!/usr/bin/env python3
from __future__ import annotations
import csv, hashlib, json, re, sqlite3, math
from datetime import datetime, timezone
from pathlib import Path

ROOT=Path(__file__).resolve().parents[2]
FIX=ROOT/'acceptance/fixtures/REV05_SoftwareAcceptanceFixture'
IFC=FIX/'Models/IFC/rev05_acceptance.ifc'
DB=FIX/'project.db'
CAT=ROOT/'CraneOptimizer.UI/Data/CanonicalCatalog.crane.db'
RUN='REV05-FIXTURE'
REPORT_DIR=FIX/'Reports/GA'/RUN
FIXTURE_TYPE='SOFTWARE ACCEPTANCE FIXTURE'
PROJECT_GUID='11111111111111111111111111111111'
IFC_CACHE_VERSION='ifc-identity-grid-ssot-v3'

IFC_TEXT="""ISO-10303-21;
HEADER;
FILE_DESCRIPTION(('ViewDefinition [ReferenceView_V1.2]'),'2;1');
FILE_NAME('rev05_acceptance.ifc','2026-09-20T00:00:00',('CraneOpt Acceptance'),('OpenAI'),'CraneOpt REV05 Fixture Generator','CraneOpt','');
FILE_SCHEMA(('IFC4'));
ENDSEC;
DATA;
#10=IFCSIUNIT(*,.LENGTHUNIT.,$,.METRE.);
#11=IFCSIUNIT(*,.AREAUNIT.,$,.SQUARE_METRE.);
#12=IFCSIUNIT(*,.VOLUMEUNIT.,$,.CUBIC_METRE.);
#13=IFCSIUNIT(*,.MASSUNIT.,.KILO.,.GRAM.);
#14=IFCUNITASSIGNMENT((#10,#11,#12,#13));
#20=IFCCARTESIANPOINT((0.,0.,0.));
#21=IFCAXIS2PLACEMENT3D(#20,$,$);
#22=IFCGEOMETRICREPRESENTATIONCONTEXT($,'Model',3,1.E-05,#21,$);
#30=IFCPROJECT('2O2Fr$t4X7Zf8NOew3FNr2',$,'REV05 Software Acceptance Fixture',$,$,$,$,(#22),#14);
#40=IFCCARTESIANPOINT((0.,0.));
#41=IFCAXIS2PLACEMENT2D(#40,$);
#42=IFCRECTANGLEPROFILEDEF(.AREA.,'BeamProfile',#41,0.4,0.6);
#43=IFCDIRECTION((0.,0.,1.));
#44=IFCEXTRUDEDAREASOLID(#42,#21,#43,6.0);
#45=IFCSHAPEREPRESENTATION(#22,'Body','SweptSolid',(#44));
#46=IFCPRODUCTDEFINITIONSHAPE($,$,(#45));
#60=IFCDIMENSIONALEXPONENTS(0,1,0,0,0,0,0);
#61=IFCMEASUREWITHUNIT(IFCMASSMEASURE(1000.),#13);
#62=IFCCONVERSIONBASEDUNIT(#60,.MASSUNIT.,'tonne',#61);
#63=IFCMEASUREWITHUNIT(IFCMASSMEASURE(0.45359237),#13);
#64=IFCCONVERSIONBASEDUNIT(#60,.MASSUNIT.,'pound',#63);
#100=IFCCARTESIANPOINT((4.,0.,0.));
#101=IFCAXIS2PLACEMENT3D(#100,$,$);
#102=IFCLOCALPLACEMENT($,#101);
#103=IFCBEAM('3t3TDZl_D9NOIWB0BSjzJI',$,'B-KG-PROJECT',$,$,#102,#46,'B-KG',.BEAM.);
#104=IFCQUANTITYWEIGHT('GrossWeight',$,$,8500.,$);
#105=IFCELEMENTQUANTITY('0J$yP20jH5F8wHjB4yJb5V',$,'BaseQuantities',$,$,(#104));
#106=IFCRELDEFINESBYPROPERTIES('0u4wgLeFP5S9c0mG8bLzVd',$,$,$,(#103),#105);
#110=IFCCARTESIANPOINT((6.,0.,0.));
#111=IFCAXIS2PLACEMENT3D(#110,$,$);
#112=IFCLOCALPLACEMENT($,#111);
#113=IFCBEAM('1xS3BCk291UvhgP2a6eflT',$,'B-TONNE-EXPLICIT',$,$,#112,#46,'B-TONNE',.BEAM.);
#114=IFCQUANTITYWEIGHT('GrossWeight',$,#62,5.0,$);
#115=IFCELEMENTQUANTITY('2O5qsTnOf6Xg8$2g8usJFL',$,'BaseQuantities',$,$,(#114));
#116=IFCRELDEFINESBYPROPERTIES('1RO7BRc1jB8x9o2mQHkc2M',$,$,$,(#113),#115);
#120=IFCCARTESIANPOINT((8.,0.,0.));
#121=IFCAXIS2PLACEMENT3D(#120,$,$);
#122=IFCLOCALPLACEMENT($,#121);
#123=IFCBEAM('0YvctVUKr2A9XkQZxR2W3q',$,'B-LB-EXPLICIT',$,$,#122,#46,'B-LB',.BEAM.);
#124=IFCQUANTITYWEIGHT('GrossWeight',$,#64,8818.490487,$);
#125=IFCELEMENTQUANTITY('2P2P4Ukxj1aO3fLk8Uz4vs',$,'BaseQuantities',$,$,(#124));
#126=IFCRELDEFINESBYPROPERTIES('0f7oMzkMz9TAbu0pH7uJPm',$,$,$,(#123),#125);
#130=IFCCARTESIANPOINT((10.,0.,0.));
#131=IFCAXIS2PLACEMENT3D(#130,$,$);
#132=IFCLOCALPLACEMENT($,#131);
#133=IFCBEAM('3J0f$_Dhv7eA5qURx2A4lH',$,'B-KG-EXPLICIT',$,$,#132,#46,'B-KG2',.BEAM.);
#134=IFCQUANTITYWEIGHT('GrossWeight',$,#13,3000.,$);
#135=IFCELEMENTQUANTITY('1yRtXFMgT6OQ2B_QYr2FbK',$,'BaseQuantities',$,$,(#134));
#136=IFCRELDEFINESBYPROPERTIES('2yQOOPW_jDcgXQqP5KjXtC',$,$,$,(#133),#135);
#140=IFCCARTESIANPOINT((12.,0.,0.));
#141=IFCAXIS2PLACEMENT3D(#140,$,$);
#142=IFCLOCALPLACEMENT($,#141);
#143=IFCBEAM('1DA9$Kf2H1WhX8C0x7YV4F',$,'B-KG-INHERITED',$,$,#142,#46,'B-KG3',.BEAM.);
#144=IFCQUANTITYWEIGHT('GrossWeight',$,$,2000.,$);
#145=IFCELEMENTQUANTITY('0Fc9dGqvP8ePo6B3XmsW2N',$,'BaseQuantities',$,$,(#144));
#146=IFCRELDEFINESBYPROPERTIES('2tE0xqEoD5jxmI2gNnsB_k',$,$,$,(#143),#145);
ENDSEC;
END-ISO-10303-21;
"""
MISSING_UNIT_IFC=IFC_TEXT.replace('#14=IFCUNITASSIGNMENT((#10,#11,#12,#13));','#14=IFCUNITASSIGNMENT((#10,#11,#12));').replace("#114=IFCQUANTITYWEIGHT('GrossWeight',$,#62,5.0,$);","#114=IFCQUANTITYWEIGHT('GrossWeight',$,$,5.0,$);")
# Named "tonne" but deliberately based on a LENGTH unit. A resolver must reject it.
MALFORMED_EXPLICIT_UNIT_IFC=IFC_TEXT.replace("#61=IFCMEASUREWITHUNIT(IFCMASSMEASURE(1000.),#13);","#61=IFCMEASUREWITHUNIT(IFCMASSMEASURE(1000.),#10);")
MALFORMED_PROJECT_UNIT_IFC=IFC_TEXT.replace("#13=IFCSIUNIT(*,.MASSUNIT.,.KILO.,.GRAM.);","#13=IFCCONVERSIONBASEDUNIT(#60,.MASSUNIT.,'tonne',#61);").replace("#61=IFCMEASUREWITHUNIT(IFCMASSMEASURE(1000.),#13);","#61=IFCMEASUREWITHUNIT(IFCMASSMEASURE(1000.),#10);").replace("#114=IFCQUANTITYWEIGHT('GrossWeight',$,#62,5.0,$);","#114=IFCQUANTITYWEIGHT('GrossWeight',$,#13,5.0,$);")

def write_ifc():
    IFC.parent.mkdir(parents=True,exist_ok=True); IFC.write_bytes(IFC_TEXT.encode('ascii'))
    (IFC.parent/'rev05_missing_mass_unit.ifc').write_bytes(MISSING_UNIT_IFC.encode('ascii'))
    (IFC.parent/'rev05_malformed_explicit_mass_unit.ifc').write_bytes(MALFORMED_EXPLICIT_UNIT_IFC.encode('ascii'))
    (IFC.parent/'rev05_malformed_project_mass_unit.ifc').write_bytes(MALFORMED_PROJECT_UNIT_IFC.encode('ascii'))

def parse_snapshot_schema():
    text=(ROOT/'CraneOptimizer.Infrastructure/Migrations/CreatedProjectDb/CreatedProjectDbContextModelSnapshot.cs').read_text(encoding='utf-8-sig')
    entities=[]
    marker='modelBuilder.Entity("'
    pos=0
    while True:
        i=text.find(marker,pos)
        if i<0: break
        name_start=i+len(marker); name_end=text.find('"',name_start)
        block_end=text.find('\n            modelBuilder.Entity(',name_end)
        if block_end<0: block_end=text.find('#pragma warning restore',name_end)
        block=text[name_end:block_end]
        mtable=re.search(r'b\.ToTable\("([^"]+)"',block)
        if not mtable:
            pos=name_end; continue
        table=mtable.group(1)
        props=[]
        for pm in re.finditer(r'b\.Property<([^>]+)>\("([^"]+)"\)(.*?)(?=\n\s*b\.Property<|\n\s*b\.HasKey|\n\s*b\.HasIndex|\n\s*b\.ToTable)',block,re.S):
            cstype,col,tail=pm.groups()
            tm=re.search(r'\.HasColumnType\("([^"]+)"\)',tail)
            sqltype=tm.group(1) if tm else ('INTEGER' if any(x in cstype for x in ['int','bool','long']) else 'REAL' if 'double' in cstype else 'TEXT')
            props.append((col,sqltype))
        km=re.search(r'b\.HasKey\("([^"]+)"\)',block)
        key=km.group(1) if km else None
        entities.append((table,props,key))
        pos=block_end
    return entities

def create_project_db():
    if DB.exists(): DB.unlink()
    con=sqlite3.connect(DB); c=con.cursor()
    for table,props,key in parse_snapshot_schema():
        defs=[]
        for col,typ in props:
            decl=f'"{col}" {typ}'
            if col==key:
                if typ.upper()=='INTEGER' and col=='Id': decl+=' PRIMARY KEY AUTOINCREMENT'
                else: decl+=' PRIMARY KEY'
            defs.append(decl)
        c.execute(f'CREATE TABLE "{table}" ({", ".join(defs)})')
    c.execute('CREATE TABLE "__EFMigrationsHistory" (MigrationId TEXT NOT NULL PRIMARY KEY, ProductVersion TEXT NOT NULL)')
    migrations=[p.name.split('_',1)[0]+'_'+p.name.split('_',1)[1][:-3] for p in sorted((ROOT/'CraneOptimizer.Infrastructure/Migrations/CreatedProjectDb').glob('*.cs')) if not p.name.endswith('Designer.cs') and 'ModelSnapshot' not in p.name]
    for m in migrations: c.execute('INSERT INTO "__EFMigrationsHistory" VALUES (?,?)',(m,'9.0.14'))
    now='2026-09-20T00:00:00Z'
    c.execute('''INSERT INTO ProjectInfo(Id,AppVersion,AreaUnit,CacheVersion,CompanyName,Country,CreatedAtUtc,Description,LastOpenedAtUtc,LengthUnit,Name,ProjectGuid,ProjectImagePath,SchemaVersion,SiteLocation,UnitSystem)
                 VALUES(1,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?)''',('REV05','m2',1,'CraneOpt Acceptance','SOFTWARE ACCEPTANCE FIXTURE',now,FIXTURE_TYPE,now,'m','REV05 Software Acceptance Fixture',PROJECT_GUID,None,1,'Software Acceptance Fixture Site','Metric'))
    sha=hashlib.sha256(IFC.read_bytes()).hexdigest(); size=IFC.stat().st_size
    # Use columns present in latest snapshot.
    file_cols=[r[1] for r in c.execute('pragma table_info(ProjectIfcFiles)').fetchall()]
    file_vals={k:None for k in file_cols}; file_vals.update({
      'Id':1,'BuildingName':'REV05 Software Acceptance Fixture','ElementCount':5,'FileHash':sha,'FileName':IFC.name,
      'FileSizeBytes':size,'ImportedAtUtc':now,'IsActive':1,'IsCacheValid':1,'LastWriteUtc':now,
      'MetadataJson':json.dumps({'acceptanceFixture':True,'fixtureType':FIXTURE_TYPE,'ifcNormalizationCacheVersion':IFC_CACHE_VERSION},sort_keys=True),'ProjectInfoId':1,
      'RelativePath':'Models/IFC/'+IFC.name,'Role':'Primary','SchemaVersion':'IFC4','TransformJson':'{}'})
    qcols=list(file_cols)
    c.execute(f'INSERT INTO ProjectIfcFiles ({",".join(chr(34)+k+chr(34) for k in qcols)}) VALUES ({",".join("?" for _ in qcols)})',[file_vals[k] for k in qcols])
    # Elements are deterministic and reconcile with the IFC mass quantities.
    rows=[
      ('3t3TDZl_D9NOIWB0BSjzJI','B-KG-PROJECT',4.0,8.5,'IfcQuantityWeight.ProjectUnit','ExplicitQuantity'),
      ('1xS3BCk291UvhgP2a6eflT','B-TONNE-EXPLICIT',6.0,5.0,'IfcQuantityWeight.ExplicitUnit','ExplicitQuantity'),
      ('0YvctVUKr2A9XkQZxR2W3q','B-LB-EXPLICIT',8.0,4.0,'IfcQuantityWeight.ExplicitUnit','ExplicitQuantity'),
      ('3J0f$_Dhv7eA5qURx2A4lH','B-KG-EXPLICIT',10.0,3.0,'IfcQuantityWeight.ExplicitUnit','ExplicitQuantity'),
      ('1DA9$Kf2H1WhX8C0x7YV4F','B-KG-INHERITED',12.0,2.0,'IfcQuantityWeight.ProjectUnit','ExplicitQuantity'),
    ]
    cols=[r[1] for r in c.execute('pragma table_info(ProjectIfcElements)').fetchall()]
    for i,(gid,name,x,wt,source,trust) in enumerate(rows,1):
        vals={k:None for k in cols}; vals.update({
          'Id':i,'AssemblyMark':name,'AssemblyMarkConfidence':'High','AssemblyMarkIsExplicit':1,'AssemblyMarkSource':'AcceptanceFixture',
          'BBoxMinX':x-0.2,'BBoxMaxX':x+0.2,'BBoxMinY':-0.3,'BBoxMaxY':0.3,'BBoxMinZ':0.0,'BBoxMaxZ':6.0,
          'COG_X':x,'COG_Y':0.0,'COG_Z':3.0,'CachedAtUtc':now,'ChildGlobalIdsJson':'[]','DisplayName':name,'ElementType':'Beam','EntityLabel':100+i*10+3,
          'ExporterProfile':'Generic','ExtractionWarningsJson':'[]','GlobalId':gid,'HeightM':6.0,'HiddenInternalGeometryOwnerGlobalIdsJson':'[]','IfcFileId':1,
          'IncludeInElementTree':1,'IncludeInGa':1,'IncludeInRender':1,'IsLiftable':1,'LengthM':6.0,'Mark':name,'Material':'Concrete','Name':name,
          'NormalizationDecisionReason':'Software acceptance fixture','ParentGlobalId':None,'ParentGlobalIdSource':None,'ProjectInfoId':1,'RawIfcType':'IfcBeam','ResolvedCategory':'Beam',
          'Role':'LiftAssembly','SourceSystem':'REV05Fixture','StoreyName':'Acceptance','ValidationStatus':'Valid','ValidationWarningsJson':'[]','VisibleGeometryOwnerGlobalIdsJson':json.dumps([gid]),
          'VolumeM3':1.44,'WeightSource':source,'WeightTons':wt,'WeightTrustLevel':trust,'WidthM':0.4
        })
        qcols=[k for k in cols if k in vals]
        c.execute(f'INSERT INTO ProjectIfcElements ({",".join([chr(34)+k+chr(34) for k in qcols])}) VALUES ({",".join("?" for _ in qcols)})',[vals[k] for k in qcols])
    vertices=[{'X':-5.0,'Y':-5.0,'Z':0.0},{'X':20.0,'Y':-5.0,'Z':0.0},{'X':20.0,'Y':15.0,'Z':0.0},{'X':-5.0,'Y':15.0,'Z':0.0}]
    c.execute('INSERT INTO SiteZones(Id,Area,Name,ProjectInfoId,SortOrder,VerticesJson,ZoneGuid,ZoneType) VALUES(1,500.0,?,1,0,?,?,?)',('Acceptance Allowed Zone',json.dumps(vertices),'22222222222222222222222222222222','Allowed'))

    # Persist a coherent crane/load-chart/project-state snapshot. The project therefore opens
    # with the same acceptance crane and engineering lookup points that are used by the GA
    # fixture instead of relying on an external catalog at reopen time.
    cat=sqlite3.connect(CAT)
    crane=cat.execute("""
        SELECT c.Id,c.Name,c.Manufacturer,c.CraneType,c.VehicleLength,c.VehicleWidth,c.VehicleHeight,
               c.OutriggerLength,c.OutriggerWidth,cs.Id,cs.MaxCapacity,cs.MaxRadius,cs.MinRadius,
               cs.MaxBoomLength,cs.MinBoomLength,cs.CounterWeight,cs.MachineRotation,cs.CraneDerating,
               cs.OutriggerConfig,cs.LoadChartCapacityPolicy,cs.LoadChartSourceDescription,cs.LoadChartStorageMode
        FROM Cranes c JOIN CraneSpecifications cs ON cs.CraneId=c.Id
        WHERE c.Name='STC800-ACCEPTANCE-FIXTURE'
    """).fetchone()
    if crane is None: raise RuntimeError('Acceptance crane missing from canonical catalog')
    (crane_id,crane_name,manufacturer,crane_type,veh_len,veh_w,veh_h,out_len,out_w,spec_id,max_cap,max_rad,min_rad,max_boom,min_boom,cw,rotation,derating,outrigger,policy,source_desc,storage)=crane
    config_json=json.dumps({'acceptanceFixture':True,'fixtureType':FIXTURE_TYPE,'capacityPolicy':policy,'storageMode':storage,'sourceDescription':source_desc},sort_keys=True)
    c.execute('''INSERT INTO ProjectCraneSnapshots(
        Id,ConfigurationJson,CounterWeightTons,CraneName,CraneType,DeratingRatio,MachineRotation,Manufacturer,
        MaxBoomLengthM,MaxCapacityTons,MaxHookHeightM,MaxRadiusM,MinBoomLengthM,MinRadiusM,Model,OutriggerConfig,
        OutriggerLengthM,OutriggerWidthM,ProjectInfoId,SnapshottedAtUtc,SourceCraneId,VehicleHeightM,VehicleLengthM,VehicleWidthM)
        VALUES(1,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?, ?,?,?,?)''',
        (config_json,cw,crane_name,str(crane_type),derating,rotation,manufacturer,max_boom,max_cap,max_boom,max_rad,min_boom,min_rad,
         crane_name,outrigger,out_len,out_w,1,now,str(crane_id),veh_h,veh_len,veh_w))

    chart_rows=cat.execute('''SELECT Radius,BoomLength,Capacity,NumberOfLines,OperatingConditionLabel,VisualColumnHeader,VisualColumnIndex
                              FROM TargetPoints WHERE CraneSpecificationId=? ORDER BY BoomLength,Radius,Id''',(spec_id,)).fetchall()
    if len(chart_rows)!=94: raise RuntimeError(f'Expected 94 STC acceptance chart points, got {len(chart_rows)}')
    for idx,(radius,boom,capacity,lines,condition,visual_header,visual_idx) in enumerate(chart_rows,1):
        c.execute('''INSERT INTO ProjectCraneLoadChartSnapshots(
            Id,BoomLengthM,CapacityTons,CraneSnapshotId,Notes,NumberOfLines,OperatingConditionLabel,RadiusM,VisualColumnHeader,VisualColumnIndex)
            VALUES(?,?,?,?,?,?,?,?,?,?)''',
            (idx,boom,capacity,1,FIXTURE_TYPE,lines,condition,radius,visual_header,visual_idx))

    c.execute('''INSERT INTO CraneSetups(
        Id,BoomAngle,BoomLength,ConfigurationJson,CraneName,CraneType,CurrentCapacity,CurrentRadius,IsSelected,IsValidPlacement,
        PivotOffsetX,PivotOffsetY,PivotOffsetZ,PositionX,PositionY,PositionZ,ProjectInfoId,Rotation,SceneCraneGuid,SlewAngle,SourceCraneId,SourceCraneSpecificationId)
        VALUES(1,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?)''',
        (75.0,15.95,config_json,crane_name,str(crane_type),51.0,4.0,1,1,0.0,0.0,0.0,0.0,0.0,0.0,1,0.0,
         '33333333333333333333333333333333',0.0,str(crane_id),spec_id))

    seed_result={
        'fixtureKind':'SoftwareAcceptanceFixtureSeed',
        'fixtureType':FIXTURE_TYPE,
        'reportDirectory':f'Reports/GA/{RUN}',
        'totalElements':5,
        'expectedWeightsTons':[8.5,5.0,4.0,3.0,2.0],
        'catalogPointCount':808,
        'selectedCraneChartPointCount':94
    }
    seed_settings={'randomSeed':42,'populationSize':16,'maxGenerations':8,'maxCranePositions':2,'wireLengthM':2.0,'additionalWeightTons':0.5,
                   'deratingRatio':0.9,'safetyFactor':0.95,'acceptanceFixture':True,'fixtureType':FIXTURE_TYPE}
    seed_positions=[{'setupIndex':1,'x':0.0,'y':0.0,'z':0.0,'boomLengthM':15.95}]
    c.execute('''INSERT INTO ProjectOptimizationRuns(
        Id,AverageUtilizationPercent,CoverageRatio,CranePositionsJson,CraneSnapshotId,FinishedAtUtc,ProjectInfoId,ResultSummaryJson,RunName,
        SafeElements,SettingsJson,StartedAtUtc,Status,TotalElements,UnsafeElements)
        VALUES(1,?,?,?,?,?,?,?,?,?,?,?,?,?,?)''',
        (42.0,1.0,json.dumps(seed_positions,sort_keys=True),1,now,1,json.dumps(seed_result,sort_keys=True),RUN,5,
         json.dumps(seed_settings,sort_keys=True),now,'SoftwareAcceptanceFixtureSeed',5,0))

    camera={'position':{'x':25.0,'y':-25.0,'z':18.0},'lookAt':{'x':6.0,'y':0.0,'z':3.0},'up':{'x':0.0,'y':0.0,'z':1.0}}
    c.execute('''INSERT INTO ProjectViewportStates(Id,CameraJson,OriginX,OriginY,OriginZ,ProjectInfoId,RenderQualityLabel,ShowCraneRadius,StateKey,UpdatedAtUtc)
                 VALUES(1,?,0.0,0.0,0.0,1,'Acceptance',1,'Main',?)''',(json.dumps(camera,sort_keys=True),now))
    cat.close()

    con.commit(); con.close()

def write_deterministic_pdf(path:Path, lines:list[str]):
    def esc(t): return t.replace('\\','\\\\').replace('(','\\(').replace(')','\\)')
    content=['BT','/F1 11 Tf','50 790 Td']
    for i,line in enumerate(lines):
        if i: content.append('0 -15 Td')
        content.append(f'({esc(line)}) Tj')
    content.append('ET')
    stream='\n'.join(content).encode('ascii','replace')
    objects=[
      b'<< /Type /Catalog /Pages 2 0 R >>',
      b'<< /Type /Pages /Kids [3 0 R] /Count 1 >>',
      b'<< /Type /Page /Parent 2 0 R /MediaBox [0 0 595 842] /Resources << /Font << /F1 4 0 R >> >> /Contents 5 0 R >>',
      b'<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica >>',
      b'<< /Length '+str(len(stream)).encode()+b' >>\nstream\n'+stream+b'\nendstream']
    out=bytearray(b'%PDF-1.4\n%REV05\n'); offsets=[0]
    for i,obj in enumerate(objects,1): offsets.append(len(out)); out+=f'{i} 0 obj\n'.encode()+obj+b'\nendobj\n'
    xref=len(out); out+=f'xref\n0 {len(objects)+1}\n'.encode()+b'0000000000 65535 f \n'
    for off in offsets[1:]: out+=f'{off:010d} 00000 n \n'.encode()
    out+=f'trailer\n<< /Size {len(objects)+1} /Root 1 0 R >>\nstartxref\n{xref}\n%%EOF\n'.encode()
    path.write_bytes(bytes(out))

def build_results():
    cat=sqlite3.connect(CAT)
    spec=cat.execute("SELECT cs.Id FROM CraneSpecifications cs JOIN Cranes c ON c.Id=cs.CraneId WHERE c.Name='STC800-ACCEPTANCE-FIXTURE'").fetchone()[0]
    weights=[8.5,5.0,4.0,3.0,2.0]; radii=[4.0,6.0,8.0,10.0,12.0]
    rows=[]
    for i,(r,w) in enumerate(zip(radii,weights),1):
        cap=cat.execute('SELECT Capacity FROM TargetPoints WHERE CraneSpecificationId=? AND abs(Radius-?)<1e-9 AND abs(BoomLength-15.95)<1e-9',(spec,r)).fetchone()[0]
        eff=cap*0.9*0.95; req=w+0.5; margin=eff-req
        rows.append({'element':f'B-{i:02d}','radius_m':r,'boom_m':15.95,'weight_t':w,'accessories_t':0.5,'required_t':req,'chart_capacity_t':cap,'effective_capacity_t':round(eff,4),'margin_t':round(margin,4),'status':'Liftable' if margin>=0 else 'Overweight'})
    cat.close()
    REPORT_DIR.mkdir(parents=True,exist_ok=True)
    snap={
      'schemaVersion':'REV05-software-acceptance-fixture-v1','optimizationRunId':RUN,'projectGuid':PROJECT_GUID,'projectName':'REV05 Software Acceptance Fixture',
      'crane':'STC800-ACCEPTANCE-FIXTURE','cranePosition':{'x':0.0,'y':0.0,'z':0.0},'boomLengthM':15.95,
      'deratingRatio':0.9,'safetyFactor':0.95,'wireLengthM':2.0,'additionalWeightTons':0.5,
      'acceptanceFixture':True,'fixtureType':FIXTURE_TYPE,'elements':rows,'liftedElementCount':sum(r['status']=='Liftable' for r in rows),'totalElementCount':len(rows)
    }
    (REPORT_DIR/'ga_result_snapshot.fixture.json').write_text(json.dumps(snap,indent=2)+"\n")
    with open(REPORT_DIR/'lifting_results.fixture.csv','w',newline='') as f:
        w=csv.DictWriter(f,fieldnames=list(rows[0]),lineterminator='\n'); w.writeheader(); w.writerows(rows)
    pdf=REPORT_DIR/'lifting_report.fixture.pdf'
    pdf_lines=['CraneOpt REV05 - SOFTWARE ACCEPTANCE FIXTURE Report','Fixture type: SOFTWARE ACCEPTANCE FIXTURE','Project: REV05 Software Acceptance Fixture','Crane: STC800-ACCEPTANCE-FIXTURE','Boom: 15.95 m | Derating: 0.90 | Safety: 0.95 | Accessories: 0.50 t']
    pdf_lines += [f"{r['element']} radius={r['radius_m']:.2f} weight={r['weight_t']:.2f} chart={r['chart_capacity_t']:.2f} effective={r['effective_capacity_t']:.2f} margin={r['margin_t']:.2f} {r['status']}" for r in rows]
    pdf_lines += [f'IFC SHA-256: {hashlib.sha256(IFC.read_bytes()).hexdigest()}',f'Catalog SHA-256: {hashlib.sha256(CAT.read_bytes()).hexdigest()}','Catalog target points: 808',f'Run ID: {RUN}',FIXTURE_TYPE]
    write_deterministic_pdf(pdf,pdf_lines)
    manifest={'acceptanceFixture':True,'fixtureType':FIXTURE_TYPE,'runId':RUN,'files':{}}
    for p in [IFC,IFC.parent/'rev05_missing_mass_unit.ifc',IFC.parent/'rev05_malformed_explicit_mass_unit.ifc',IFC.parent/'rev05_malformed_project_mass_unit.ifc',DB,REPORT_DIR/'ga_result_snapshot.fixture.json',REPORT_DIR/'lifting_results.fixture.csv',pdf]:
        manifest['files'][str(p.relative_to(FIX)).replace('\\','/')]={'sha256':hashlib.sha256(p.read_bytes()).hexdigest(),'bytes':p.stat().st_size}
    (FIX/'acceptance-fixture-manifest.json').write_text(json.dumps(manifest,indent=2)+"\n")

write_ifc(); create_project_db(); build_results()
print('fixture',FIX)
print('db bytes',DB.stat().st_size,'ifc bytes',IFC.stat().st_size)
