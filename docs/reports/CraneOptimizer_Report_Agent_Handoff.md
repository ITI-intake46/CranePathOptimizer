# Crane Optimizer — Reports and GA Data Handoff

## Purpose

This is the standing implementation handoff for future work on the **Crane Optimizer** report workflow. A coding agent must read it before changing canonical lift-unit creation, Basic Element List, GA input/saved-run data, Preview, PDF, or Excel.

Visible product name:

```text
Crane Optimizer
```

---

# 1. Project and Report Scope

Crane Optimizer is a .NET 8 WPF engineering application using MVVM, Clean Architecture, xBIM/IFC processing, saved project/run data, and GA-based mobile-crane setup optimization.

Approved normal report templates:

1. **Basic Element List** — `builtin.element.basic-list.v1`
2. **GA Result Report** — `builtin.ga.complete-result.v1`

Do not add or restore other report templates without explicit approval.

---

# 2. Engineering Identity Rules

Use `LiftUnitId` as the engineering identity for:

- Basic-to-GA joins;
- saved-run lookup;
- setup assignment;
- COG parity;
- material parity;
- duplicate detection;
- tests and diagnostics.

Never use these as identity:

- Element Mark;
- visible `No.`;
- row index;
- import order;
- setup order;
- Element Name.

Element Marks may repeat.

At IFC/domain level:

- `ElementID` is the IFC GlobalId where applicable;
- `ElementName` is the raw IFC Name;
- Assembly Mark is separate from identity.

---

# 3. IFC Canonicalization and Orphan Data

The acceptance IFC contains:

```text
357 valid assembly-level lift units
109 disconnected IfcBuildingElementPart records
```

The 109 disconnected records are approximately:

```text
103 × InsideShell
6 × corobel
```

They are not duplicate GlobalIds. They are orphan/incomplete IFC products because they lack sufficient standalone lift evidence, including combinations of:

- no parent assembly;
- no spatial containment;
- no geometric representation;
- no trusted properties or quantities;
- no trusted weight;
- no valid standalone lifting role.

Expected result:

```text
Canonical lift units: 357
Basic report rows: 357
GA candidate source rows: 357
Excluded IFC orphan artifacts: 109
Duplicate LiftUnitIds: 0
```

Do not delete source IFC objects. Exclude them during canonical lift-unit creation and record an anomaly/exclusion reason.

Do not blanket-exclude every `IfcBuildingElementPart`. A standalone part can be legitimate in another IFC. Use evidence-based classification, for example:

```csharp
bool isOrphanArtifact =
    element is IIfcBuildingElementPart
    && !hasParentAssembly
    && !hasSpatialContainment
    && !hasRepresentation
    && !hasTrustedPropertiesOrQuantities;
```

Assembly child parts contribute to the parent lift unit and must not also become standalone report rows.

---

# 4. Physical COG Contract

## Final user decision

Keep the GA production COG path unchanged. The Basic Element List may calculate COG independently a second time. A single source of truth is not mandatory for this specific correction, but the result must be identical.

Required parity:

```text
Basic Element List COG
=
GA input COG
=
saved submitted lift-unit COG
=
GA Result Report COG
```

Compare by `LiftUnitId`.

## COG timing

The GA does not calculate a new physical COG after optimization finishes. The expected flow is:

```text
Run pressed
→ GA input preparation
→ assembly/member rollup
→ physical COG assigned
→ ElementStaticCache copies COG
→ GA generations execute
→ cached COG is used for reach/capacity calculations
→ result saved
```

“Same COG as GA” means Basic must reproduce the COG-producing GA input-preparation behavior. It does not mean calculating from final crane position, Required Radius, final outcome rows, or rounded PDF values.

## Do not change GA

Do not functionally modify:

- GA input behavior;
- GA assembly rollup;
- `ElementStaticCache`;
- chromosome generation;
- population evolution;
- fitness;
- setup optimization;
- assignment logic;
- Required Radius;
- Required Boom Length;
- Required Capacity;
- load-chart interpolation.

The goal is to make Basic match GA, not GA match Basic.

## Basic COG implementation

Preferred Basic path:

```text
current project elements
→ isolated/deep-cloned input
→ same GA grouping/rollup operation
→ do not start optimizer
→ read physical ElementCOG
→ map to Basic COG X/Y/Z
```

The Basic operation must not mutate scene data, saved runs, optimizer state, or project revision.

If direct safe reuse is impossible, a Basic-only implementation may mirror GA behavior exactly. It must match:

- assembly identity;
- member selection;
- main-part rule;
- reinforcement exclusion;
- reference-geometry exclusion;
- duplicate prevention;
- nested-assembly handling;
- member order;
- weight source;
- member COG source;
- unit conversion;
- coordinate transform;
- invalid-value behavior;
- fallback sequence;
- accumulation precision;
- rounding timing.

## Grouped assembly equation

```text
COG X = Σ(member weight × member COG X) / Σ(member weights)
COG Y = Σ(member weight × member COG Y) / Σ(member weights)
COG Z = Σ(member weight × member COG Z) / Σ(member weights)
```

Do not use assembly origin, unweighted average, bounding-box center, viewport center, report geometric center, `(0,0,0)`, or crane-relative coordinates.

## Coordinate frame

Both reports display physical COG in project/model coordinates, metres.

Required Radius remains separate, typically:

```text
sqrt((COG X - Setup X)^2 + (COG Y - Setup Y)^2)
```

Do not replace displayed COG with coordinate differences.

## COG acceptance

```text
Compared LiftUnitIds: <actual>
COG X mismatches: 0
COG Y mismatches: 0
COG Z mismatches: 0
Missing LiftUnitIds: 0
Duplicate LiftUnitIds: 0
```

Compare unrounded values. Use tolerance `1e-9` only where serialization requires it.

---

# 5. Weight and PAD Rules

There are 17 real `PAD ROOF` assemblies. They are not garbage.

Their explicit masses include values such as:

```text
0.8 kg = 0.0008 t
0.5 kg = 0.0005 t
```

They previously appeared as `0.00 t` only because of display rounding.

Use adaptive precision consistently in Preview, PDF, and Excel:

```text
Weight >= 0.01 t  → 0.00 t
0 < Weight < 0.01 t → 0.0000 t
```

A trusted small positive weight must not be marked missing or zero.

Do not silently exclude small components from analysis unless an approved business rule exists.

---

# 6. Wire Length and Accessory Rules

There are two distinct wire lengths.

## Crane hoist wire

```text
Field: HoistWireLengthM
Meaning: wire above the hook, belonging to the crane
Current new-run value: 0.0 m
Visible: Crane Hoist Wire Length: 0.00 m
```

## Rigging wire rope

```text
Field: RiggingWireRopeLengthM
Meaning: rigging wire below the hook
GA UI Wire Length (m) = 2.0
Visible: Wire Rope Length: 2.00 m
```

## No fallback

- Do not copy one value into the other.
- Do not use one ambiguous field for both.
- Explicit zero displays as zero.
- Genuinely absent legacy data displays `Not Provided`.
- Zero is not missing.

## Accessory weight

Current new-run values:

```text
Hook Weight = 0.00 t
Total Lifting Accessories Weight = 0.00 t
```

Do not derive weight from wire length. Do not interpret metres as tonnes.

With zero accessory weight, the unchanged Required Capacity formula must receive zero additional load.

A PAD weighing `0.0008 t` must not produce `Required Capacity = 2.00 t`.

When wire/accessory mapping changes, run a fresh GA analysis because the old result is no longer valid.

---

# 7. Saved GA Run Rules

The GA Result Report must use the exact run selected by the user.

Do not automatically switch to the newest run.

At submission/persistence, preserve immutable shared lift-unit data:

- LiftUnitId;
- Element Mark;
- Element Name;
- Element Type;
- Material Type;
- Material Grade internally;
- dimensions;
- volume;
- weight;
- physical COG.

GA outcomes contribute result-specific fields only.

Join outcome rows to saved submitted lift units by `LiftUnitId`.

Keep internal run identity, model revision, hashes, fingerprints, and compatibility checks, but do not show normal users:

- GA Run GUID;
- ProductionJson;
- Historic;
- Current;
- historic/current explanatory text.

---

# 8. Basic Element List Contract

## Metadata

Visible metadata must contain exactly:

```text
Report Reference
Report Name
Items Included
Project
Created On
```

Do not display Generated On, GA Analysis Date, GA Run, GUID, Historic/Current, or ProductionJson.

## Final visible columns

```text
No.
Element Mark
Element Name
Element Type
Material Type
Material Grade
Length (m)
Width (m)
Height (m)
Volume (m³)
Weight
COG X (m)
COG Y (m)
COG Z (m)
Included in Analysis
Analysis Exclusion Reason
```

## Analysis fields

`Included in Analysis` values:

```text
Yes
No
```

When included:

```text
Analysis Exclusion Reason = —
```

When excluded, show the actual production reason, such as:

- No Trusted Weight;
- No Trusted Physical COG;
- IFC Orphan Artifact;
- Reference Geometry;
- Below Minimum Lift Weight;
- Unsupported Lift Unit.

Do not invent reasons.

## Numbering

Basic uses one global display sequence:

```text
1..BasicRowCount
```

The visible number is never identity.

---

# 9. GA Result Report Metadata and Introduction

## Metadata

Visible fields:

```text
Report Reference
Report Name
Items Included
Project
Created On
```

## Two-page introduction

### Page 1

- title/branding;
- Report Information;
- Summary;
- Failure Breakdown.

### Page 2

- Crane / Analysis Information;
- Lifting Accessories;
- Capacity Note;
- Final GA Setup Points.

Do not force everything onto one crowded page.

## Major headings

```text
Report Information
Summary
Failure Breakdown
Crane / Analysis Information
Lifting Accessories
Final GA Setup Points
```

`Failure Breakdown` and `Lifting Accessories` are separate sections, not ordinary rows mixed into another block.

Use clear spacing and keep-together behavior.

---

# 10. GA Summary Contract

Visible summary order:

```text
Total Items
Liftable
Not Lifted
Final Setup Count
Average Capacity Utilization
Maximum Capacity Utilization
```

Then a separate `Failure Breakdown` section.

Show only non-zero categories:

- Out of Radius;
- Overweight;
- Height Failure;
- Missing Load Chart Coverage.

Do not display `No Chart Data`.

`Missing Load Chart Coverage` means the selected load chart has no usable capacity entry for the required radius/boom combination.

Required reconciliation:

```text
Liftable + Not Lifted = Total Items
sum of setup assigned counts = Liftable
sum of failure categories = Not Lifted
Final Setup Count = saved setup count
```

---

# 11. Crane and Accessories Sections

## Crane / Analysis Information

```text
Model
Specification
Manufacturer
Crane Type
Counterweight
Outrigger Configuration
Working Radius
Boom Length
Maximum Capacity
Safety Factor
Derating Ratio
Crane Hoist Wire Length
```

Keep the block together.

## Lifting Accessories

```text
Wire Rope Length
Total Lifting Accessories Weight
```

Keep it under its own heading and do not create a separate normal Excel sheet for it.

---

# 12. Final GA Setup Points

Visible columns:

```text
Setup Reference
X (m)
Y (m)
Z (m)
Assigned Lift Count
Assigned Percentage
Zone
```

Remove `Setup No.`.

Hide Zone when every value is blank or `—`.

Keep the heading, table header, and first row together.

---

# 13. GA Successful Setup Tables

## Numbering

Each setup table starts independently at 1:

```text
Point_1: 1..Point_1RowCount
Point_2: 1..Point_2RowCount
Point_3: 1..Point_3RowCount
```

Do not use Basic row number, import index, or one global sequence across setups.

## Final normal GA columns

```text
No.
Element Mark
Element Name
Element Type
Material Type
Weight
COG X (m)
COG Y (m)
COG Z (m)
Required Radius (m)
Selected Chart Radius (m)
Required Boom Length (m)
Selected Chart Boom Length (m)
Required Capacity (t)
Adjusted Allowable Capacity (t)
Manufacturer Chart Capacity (t)
Capacity Utilization (%)
```

Do not display in normal GA output:

- Material Grade;
- Length;
- Width;
- Height;
- Volume;
- Hook / Rigging Weight;
- Reach Status.

Material Grade remains internal and visible in Basic, but the user explicitly removed it from GA output.

---

# 14. Failed Lifts Table

## Numbering

Failed Lifts starts independently:

```text
1..FailedRowCount
```

## Visible columns

```text
No.
Element Mark
Element Name
Element Type
Material Type
Weight
COG X (m)
COG Y (m)
COG Z (m)
Required Radius (m)
Selected Chart Radius (m)
Required Boom Length (m)
Selected Chart Boom Length (m)
Required Capacity (t)
Adjusted Allowable Capacity (t)
Manufacturer Chart Capacity (t)
Capacity Utilization (%)
Failure Category
Failure Reason
Suggested Action
```

Do not display Material Grade, Hook/Rigging Weight, Reach Status, or Lift Status.

User-facing failure categories:

- Out of Radius;
- Overweight;
- Height Failure;
- Missing Load Chart Coverage.

Use professional suggested-action wording, for example:

```text
Use a higher-capacity crane or verify the element weight.
```

A failed row must not split across pages. Move the full row to the next page and repeat the header when needed.

---

# 15. Capacity Fields

Final names and order:

```text
Required Capacity (t)
Adjusted Allowable Capacity (t)
Manufacturer Chart Capacity (t)
Capacity Utilization (%)
```

- Adjusted Allowable Capacity is the previous Available Capacity value.
- Manufacturer Chart Capacity is the previous Raw Chart Capacity value.

Add one note:

```text
Adjusted Allowable Capacity includes the selected safety factor and derating ratio.
```

Do not change calculations; only labels and order.

---

# 16. PDF Layout Rules

The main remaining report concern is GA detail-table readability.

Do not reduce font size further.

Allowed improvements:

- more vertical pages;
- fewer rows per page;
- readable minimum font size;
- landscape orientation;
- controlled column widths;
- wrapped headers;
- repeated headers;
- consistent margins;
- keep-together sections and rows.

Prefer roughly 25–35 detail rows per page where needed for readability.

At 100% PDF zoom, users must be able to read Element Mark, Material Type, capacities, and failure diagnostics.

Do not compress 80–100 rows onto a page, overlap text, silently truncate values, place horizontal table segments side by side, or orphan section headings.

Footer:

```text
Crane Optimizer
Report Name
Page X of Y
```

Do not include Report Reference in the footer.

---

# 17. Excel Workbook Contract

## Visible sheets

```text
Report Summary
Final Setups
Setup Point_1
Setup Point_2
...
Failed Lifts
```

Remove visible sheets:

```text
Crane
Lifting Accessories
```

Do not create empty setup sheets.

## Report Summary sections

- Report Information;
- Summary;
- Failure Breakdown;
- Crane / Analysis Information;
- Lifting Accessories.

## GA Excel fields

Display Material Type and do not display Material Grade in normal GA setup/failed sheets.

Excel may retain dimensions and volume for audit if approved, but must not restore Material Grade.

## Numbering

- each setup sheet starts at 1;
- Failed Lifts starts at 1;
- Basic Excel uses one global sequence.

## Formatting

- auto-fit visible content;
- cap width around 40–50 characters;
- wrap text above the cap;
- auto-expand row height;
- wrap headers;
- vertical center;
- freeze header row;
- filters;
- freeze identification columns where practical;
- landscape print settings;
- repeat print headers.

Do not use unlimited auto-fit.

---

# 18. Preview / PDF / Excel Parity

Use one immutable/frozen report document per report request.

Internally prove:

```text
Preview selected run ID
=
PDF selected run ID
=
Excel selected run ID
```

The ID stays hidden from the end user.

Outputs must agree on Created On, summary counts, setup coordinates, assignments, numbering, Material Type, Weight, COG, wire values, accessory weight, capacities, and utilization.

Do not reload a different run inside an exporter.

---

# 19. Current Accepted State

The latest accepted iteration visibly achieved:

- Material Grade removed from GA output;
- Material Type displayed;
- two-page introduction;
- separate Failure Breakdown;
- separate Lifting Accessories;
- corrected hoist/rigging mapping;
- zero accessory weight;
- false 2-ton capacity addition removed;
- fresh GA run;
- per-setup numbering;
- failed numbering;
- visible COG parity;
- failed rows no longer splitting incorrectly.

Do not regress these.

Primary remaining work:

- GA PDF table readability;
- Excel evidence and verification;
- unrounded LiftUnitId parity evidence;
- minor label/column spacing.

---

# 20. Allowed Work

A future agent may work on:

- Basic report DTOs and mapping;
- GA report DTOs and mapping;
- saved submitted lift-unit snapshot persistence;
- metadata and section structure;
- Preview rendering;
- PDF pagination/layout;
- Excel workbook layout;
- display labels and numbering;
- diagnostics/parity tooling;
- report-focused tests;
- wire/accessory mapping when explicitly requested;
- orphan-artifact filtering when explicitly requested;
- Basic GA-equivalent COG calculation only when a regression is proven.

---

# 21. Forbidden / Out-of-Scope Work

Do not modify without explicit approval:

- GA optimizer equations;
- chromosome generation;
- population evolution;
- fitness logic;
- setup search;
- assignment algorithm;
- Required Radius formula;
- Required Boom Length formula;
- load-chart interpolation;
- safety-factor formula;
- derating formula;
- capacity-adjustment formula;
- accepted COG behavior;
- unrelated startup/home/project UI;
- AI Companion/Copilot behavior;
- Ollama integration;
- AI prompts/settings;
- unrelated domain features.

The uploaded AI Companion code is outside this report task and is not an authoritative report data source.

Legacy AI DTO fields such as:

```text
AdditionalWeight
WireLength
```

must not be used as authoritative engineering persistence fields.

Use explicit report/run fields:

```text
HoistWireLengthM
RiggingWireRopeLengthM
HookWeightTons
TotalLiftingAccessoriesWeightTons
```

Do not edit generated files in `bin` or `obj`. Do not edit generated XAML/MSBuild output as production source.

---

# 22. Mandatory Tests and Evidence

## COG parity

```text
Compared LiftUnitIds: <actual>
COG X mismatches: 0
COG Y mismatches: 0
COG Z mismatches: 0
Missing LiftUnitIds: 0
Duplicate LiftUnitIds: 0
```

## Material Type parity

```text
Compared LiftUnitIds: <actual>
Material Type mismatches: 0
Missing valid Material Type values: 0
```

## Wire/accessory proof

```text
Saved HoistWireLengthM: 0.0
Saved RiggingWireRopeLengthM: 2.0
Saved HookWeightTons: 0.0
Saved TotalLiftingAccessoriesWeightTons: 0.0
Wire length used as weight: No
```

## Numbering proof

For every setup and Failed Lifts:

```text
First No.: 1
Last No.: row count
Gaps: 0
Duplicates: 0
```

## Reconciliation

```text
sum of setup assigned counts = Liftable
Liftable + Not Lifted = Total Items
sum of failure categories = Not Lifted
```

Tests must also cover canonical count, orphan exclusion, duplicate identity, adaptive small-weight formatting, Basic inclusion fields, metadata labels, selected-run consistency, PDF pagination, and Excel sheet/format rules.

---

# 23. Manual Acceptance Workflow

1. Build the full solution.
2. Run focused tests.
3. Run the full test suite.
4. Open the acceptance project.
5. Confirm canonical and orphan counts.
6. Confirm wire/accessory inputs.
7. Run a fresh GA analysis when engineering inputs changed.
8. Select the exact saved run.
9. Generate Preview.
10. Export fresh Basic PDF.
11. Export fresh Basic Excel if supported.
12. Export fresh GA PDF.
13. Export fresh GA Excel.
14. Extract PDF text.
15. Render every PDF page.
16. Inspect every page visually.
17. Inspect every Excel sheet programmatically and visually.
18. Run full LiftUnitId parity.
19. Save screenshots, diagnostics, and logs.

Do not reuse old PDFs as acceptance evidence. Do not claim completion from code inspection alone.

---

# 24. Required Final Agent Response

Return:

1. Root cause.
2. Exact production path changed.
3. Files modified.
4. Confirmation of engineering files not changed.
5. Focused test results.
6. Full test results.
7. Canonical/orphan counts.
8. COG parity.
9. Material Type parity.
10. Wire/accessory persistence proof.
11. Numbering validation.
12. New-run reconciliation when applicable.
13. Fresh Basic PDF path.
14. Fresh GA PDF path.
15. Fresh Excel paths.
16. PDF text-inspection result.
17. PDF visual-inspection notes.
18. Excel inspection result.
19. Screenshots.
20. Remaining limitations.
21. One concise English git commit message.

Do not say fixed, complete, production ready, ready to merge, or all tests passed without evidence.

---

# 25. Definition of Done

A report task is done only when:

- requested scope is implemented;
- unrelated engineering logic is unchanged;
- Preview/PDF/Excel are consistent;
- selected-run identity is consistent internally;
- metadata and numbering are correct;
- capacities retain correct numeric values;
- wire and accessory units are not confused;
- COG parity remains correct;
- orphan artifacts do not become lift units;
- report pages are readable;
- failed rows paginate correctly;
- Excel is usable;
- focused and full tests pass;
- fresh artifacts are generated and inspected;
- all claims are backed by evidence.

---

# 26. Recommended Git Commit

```text
feat(reports): finalize Basic and GA report consistency, inputs, and UX
```

Suggested summary:

```text
- aligned Basic physical COG with GA input preparation without changing GA behavior;
- excluded orphan IFC artifacts from canonical lift units;
- simplified Basic inclusion fields and report metadata;
- corrected small positive weight formatting;
- corrected hoist/rigging persistence and zero accessory weight;
- removed the false 2-ton capacity addition and regenerated GA results;
- simplified GA summary, setup numbering, capacity labels, and detail columns;
- removed Material Grade from GA output and retained Material Type;
- separated report introduction sections and improved pagination;
- preserved LiftUnitId as internal identity across Preview, PDF, and Excel.
```
