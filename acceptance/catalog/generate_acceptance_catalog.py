#!/usr/bin/env python3
"""Generate the deterministic REV05 SOFTWARE ACCEPTANCE FIXTURE catalog.

The catalog is a controlled, versioned software-acceptance dataset used to verify
catalog identity, first-run provisioning, load-chart lookup, GA/report reconciliation,
and release controls as one reproducible acceptance case.
"""
from __future__ import annotations
import csv, hashlib, json, sqlite3
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
LEGACY_POINTS = ROOT / "acceptance/catalog/stc800_legacy_regression_points.csv"
OUT = ROOT / "CraneOptimizer.UI/Data/CanonicalCatalog.crane.db"
MANIFEST = ROOT / "acceptance/catalog/catalog-manifest.json"
MIGRATIONS = [
    "20260224012129_InitialCreate",
    "20260427211132_AddProjectTables",
    "20260428183336_AddCSVToCeaneSpecification",
    "20260519000100_NormalizeUnitSystemStorage",
    "20260521194249_AddProjectFolderpathtoProject",
    "20260528181000_AddFullFidelityLoadChartMetadata",
    "20260603120000_AddProjectConcreteDensity",
    "20260608165000_AddLoadChartNumberOfLinesMetadata",
    "20260609011000_AddLoadChartOperatingConditionLabel",
    "20260710120000_AddCompanyNameToProject",
]
FIXTURE_TYPE = "SOFTWARE ACCEPTANCE FIXTURE"

def parse_legacy_points():
    pts=[]
    with LEGACY_POINTS.open(newline="", encoding="utf-8") as f:
        reader=csv.DictReader(f)
        for row in reader:
            pts.append((float(row["radius_m"]), float(row["boom_length_m"]), float(row["capacity_tons"])))
    if len(pts) != 94:
        raise SystemExit(f"Expected 94 legacy STC regression points, found {len(pts)}")
    return pts

def ltm_acceptance_points():
    # 21 boom lengths × 34 radii = 714 deterministic engineering-test points.
    pts=[]
    for bi in range(21):
        boom = 20.0 + 3.0*bi
        for radius in range(3,37):
            cap = 300.0 / (1.0 + radius/6.0) * max(0.55, 1.0 - (boom-20.0)*0.004)
            cap = max(1.0, round(cap,3))
            pts.append((float(radius), boom, cap))
    assert len(pts)==714
    return pts

def create_schema(c):
    c.executescript("""
    PRAGMA foreign_keys=ON;
    CREATE TABLE Cranes (
      Id INTEGER NOT NULL CONSTRAINT PK_Cranes PRIMARY KEY AUTOINCREMENT,
      Name TEXT NOT NULL, Manufacturer TEXT NOT NULL, CraneType INTEGER NOT NULL,
      VehicleLength REAL NOT NULL, VehicleWidth REAL NOT NULL, VehicleHeight REAL NOT NULL,
      OutriggerLength REAL NULL, OutriggerWidth REAL NULL
    );
    CREATE TABLE CraneSpecifications (
      Id INTEGER NOT NULL CONSTRAINT PK_CraneSpecifications PRIMARY KEY AUTOINCREMENT,
      CraneId INTEGER NOT NULL, MaxCapacity REAL NOT NULL, MaxRadius REAL NOT NULL,
      MinRadius REAL NOT NULL, MaxBoomLength REAL NOT NULL, MinBoomLength REAL NOT NULL,
      CounterWeight REAL NOT NULL, MachineRotation REAL NULL, CraneDerating REAL NULL,
      OutriggerConfig TEXT NULL, ExactLoadChartRawGridJson TEXT NULL,
      LoadChartCapacityPolicy TEXT NULL, LoadChartSourceDescription TEXT NULL,
      LoadChartStorageMode TEXT NULL,
      CONSTRAINT FK_CraneSpecifications_Cranes_CraneId FOREIGN KEY(CraneId) REFERENCES Cranes(Id) ON DELETE CASCADE
    );
    CREATE INDEX IX_CraneSpecifications_CraneId ON CraneSpecifications(CraneId);
    CREATE TABLE TargetPoints (
      Id INTEGER NOT NULL CONSTRAINT PK_TargetPoints PRIMARY KEY AUTOINCREMENT,
      Radius REAL NOT NULL, BoomLength REAL NOT NULL, Capacity REAL NOT NULL,
      CraneSpecificationId INTEGER NOT NULL, NumberOfLines INTEGER NULL,
      OperatingConditionLabel TEXT NULL, VisualColumnHeader TEXT NULL, VisualColumnIndex INTEGER NULL,
      CONSTRAINT FK_TargetPoints_CraneSpecifications_CraneSpecificationId FOREIGN KEY(CraneSpecificationId) REFERENCES CraneSpecifications(Id) ON DELETE CASCADE
    );
    CREATE INDEX IX_TargetPoints_CraneSpecificationId ON TargetPoints(CraneSpecificationId);
    CREATE TABLE Projects (
      Id TEXT NOT NULL CONSTRAINT PK_Projects PRIMARY KEY, Name TEXT NOT NULL,
      CreatedDate TEXT NOT NULL, LastModifiedDate TEXT NOT NULL, LastOpenedDate TEXT NOT NULL,
      IsFavorite INTEGER NOT NULL, IsDeleted INTEGER NOT NULL, Notes TEXT NOT NULL,
      ProjectFolderPath TEXT NOT NULL, DisplayLengthUnit INTEGER NOT NULL,
      DisplayMassUnit INTEGER NOT NULL, DisplayAreaUnit INTEGER NOT NULL,
      UnitDecimalPlaces INTEGER NOT NULL, ConcreteDensityKgPerM3 REAL NOT NULL DEFAULT 2500.0,
      CompanyName TEXT NULL
    );
    CREATE INDEX IX_Projects_IsFavorite ON Projects(IsFavorite);
    CREATE INDEX IX_Projects_LastOpenedDate ON Projects(LastOpenedDate);
    CREATE TABLE ProjectIfcFiles (
      Id TEXT NOT NULL CONSTRAINT PK_ProjectIfcFiles PRIMARY KEY, ProjectId TEXT NOT NULL,
      FileName TEXT NOT NULL, FilePath TEXT NOT NULL, [Exists] INTEGER NOT NULL,
      CONSTRAINT FK_ProjectIfcFiles_Projects_ProjectId FOREIGN KEY(ProjectId) REFERENCES Projects(Id) ON DELETE CASCADE
    );
    CREATE INDEX IX_ProjectIfcFiles_ProjectId ON ProjectIfcFiles(ProjectId);
    CREATE TABLE "__EFMigrationsHistory" (MigrationId TEXT NOT NULL CONSTRAINT PK___EFMigrationsHistory PRIMARY KEY, ProductVersion TEXT NOT NULL);
    CREATE TABLE CatalogManifest (
      Key TEXT NOT NULL PRIMARY KEY, Value TEXT NOT NULL
    );
    """)

def add_crane(c, name, manufacturer, max_capacity, min_radius, max_radius, min_boom, max_boom, points, source):
    c.execute("INSERT INTO Cranes(Name,Manufacturer,CraneType,VehicleLength,VehicleWidth,VehicleHeight,OutriggerLength,OutriggerWidth) VALUES(?,?,?,?,?,?,?,?)",
              (name, manufacturer, 0, 14.635, 2.75, 3.429, 6.1, 7.6))
    crane_id=c.lastrowid
    c.execute("""INSERT INTO CraneSpecifications(CraneId,MaxCapacity,MaxRadius,MinRadius,MaxBoomLength,MinBoomLength,CounterWeight,MachineRotation,CraneDerating,OutriggerConfig,ExactLoadChartRawGridJson,LoadChartCapacityPolicy,LoadChartSourceDescription,LoadChartStorageMode)
                 VALUES(?,?,?,?,?,?,?,?,?,?,?,?,?,?)""",
              (crane_id,max_capacity,max_radius,min_radius,max_boom,min_boom,4.0,360.0,1.0,FIXTURE_TYPE,None,"ExactStep","%s | %s"%(FIXTURE_TYPE,source),"EngineeringPoints"))
    spec_id=c.lastrowid
    c.executemany("INSERT INTO TargetPoints(Radius,BoomLength,Capacity,CraneSpecificationId,NumberOfLines,OperatingConditionLabel,VisualColumnHeader,VisualColumnIndex) VALUES(?,?,?,?,?,?,?,?)",
                  [(r,b,cap,spec_id,None,FIXTURE_TYPE,f"{b:g} m",None) for r,b,cap in points])

OUT.parent.mkdir(parents=True, exist_ok=True)
if OUT.exists(): OUT.unlink()
con=sqlite3.connect(OUT)
try:
    c=con.cursor(); create_schema(c)
    legacy=parse_legacy_points()
    add_crane(c,"STC800-ACCEPTANCE-FIXTURE","Sany / SOFTWARE ACCEPTANCE FIXTURE",80.0,3.0,36.0,11.8,45.0,legacy,
              "94 deterministic points transcribed from the historical built-in STC800 software seed for regression compatibility")
    ltm=ltm_acceptance_points()
    add_crane(c,"LTM1200-5.1-ACCEPTANCE-FIXTURE","Liebherr / SOFTWARE ACCEPTANCE FIXTURE",300.0,3.0,36.0,20.0,80.0,ltm,
              "714 deterministic points generated by the REV05 SOFTWARE ACCEPTANCE FIXTURE generator")
    for m in MIGRATIONS:
        c.execute('INSERT INTO "__EFMigrationsHistory"(MigrationId,ProductVersion) VALUES(?,?)',(m,"9.0.14"))
    meta={
      "CatalogId":"CraneOpt-REV05-SOFTWARE-ACCEPTANCE-808",
      "CatalogVersion":"REV05-acceptance-1",
      "FixtureType":FIXTURE_TYPE,
      "PointCount":"808",
      "CraneCount":"2",
      "Provenance":"Generated by acceptance/catalog/generate_acceptance_catalog.py; 94 historical regression-seed points + 714 deterministic software-acceptance fixture points",
    }
    c.executemany("INSERT INTO CatalogManifest(Key,Value) VALUES(?,?)",meta.items())
    con.commit()
    count=c.execute('select count(*) from TargetPoints').fetchone()[0]
    assert count==808
finally: con.close()
sha=hashlib.sha256(OUT.read_bytes()).hexdigest()
manifest={
  "catalogId":"CraneOpt-REV05-SOFTWARE-ACCEPTANCE-808",
  "catalogVersion":"REV05-acceptance-1",
  "fixtureType":FIXTURE_TYPE,
  "targetPointCount":808,
  "craneCount":2,
  "sha256":sha,
  "file":"CraneOptimizer.UI/Data/CanonicalCatalog.crane.db",
  "provenance":[
    "94 points are transcribed from the repository's historical built-in STC800 seed for regression continuity.",
    "714 points are deterministic values generated by the REV05 software-acceptance fixture generator.",
    "The complete 808-point artifact is the canonical SOFTWARE ACCEPTANCE FIXTURE catalog for this submission."
  ]
}
MANIFEST.write_text(json.dumps(manifest,indent=2)+"\n",encoding="utf-8")
print(json.dumps(manifest,indent=2))
