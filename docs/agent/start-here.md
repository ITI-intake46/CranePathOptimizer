# Onboarding & IFC Processing Guide (Start Here)

Welcome to the Mobile Crane Path Optimizer codebase! This document provides an entry point to understanding the IFC extraction, normalization, and assembly graph classification pipeline.

---

## 1. IFC Ingestion & Normalization Pipeline

The application processes building models from IFC files in a decoupled, two-stage pipeline:

```
[Raw IFC File]
      │
      ▼
[1. IfcDiagnosticAuditService] ────→ Generates diagnostic CSV/JSON reports
      │                              (e.g., debug_ifc_assembly_graph_classification_audit.csv)
      ▼
[2. IfcNormalizationDecisionService] ──→ Normalizes elements and assigns roles
      │
      ▼
[3. IfcReaderRepository] ──────────→ Maps normalized previews to PrecastElement
                                     domain schemas and persists them to SQLite
```

1. **Diagnostic Ingestion & Audit (`IfcDiagnosticAuditService`):**
   Opens the IFC model session, extracts georeferencing metadata/units, maps decomposition hierarchies, and runs validation diagnostics on weights, volumes, and placements.
2. **Normalization & Role Decision (`IfcNormalizationDecisionService`):**
   Assigns element roles (`LiftableElement`, `GroupingOnly`, `LiftAssembly`, etc.) and determines candidate categories (Wall, Beam, Column, Slab) using rules.
3. **Domain Mapping & Extraction (`IfcReaderRepository`):**
   Translates normalized previews into `PrecastElement` domain objects, resolving georeferenced coordinates and canonical assembly identity.

---

## 2. Assembly Graph & Child Classification (Phase 3)

The assembly graph classification layer parses structural relationships to identify how parent assemblies are formed and classifies each child component.

### Parent-Child Relationship Sources
Relationships are resolved with the following confidence hierarchy:
1. **Explicit IFC Relations (Trusted):** `IfcRelAggregates` (via `IsDecomposedBy`) and `IfcRelNests` (via `IsNestedBy`).
2. **Tekla Explicit Parent Property (Trusted Fallback):** Custom string properties containing parent GUIDs (e.g. `Assembly GUID`, `Parent Assembly GUID`, `Cast Unit GUID`) matched against parent assemblies in the model.
3. **AssemblyMark Equality (Metadata Only):** Equal marks are NEVER used as a relationship/grouping source.

### Child Classification Categories
Every child element in an assembly is classified into one of:
*   `MainPart`: The primary load-bearing element of the assembly (e.g. the main precast concrete wall panel).
*   `VisibleSecondaryPart`: Secondary structural members (beams, columns, slabs) that are not the main part.
*   `HiddenWeightContributor`: Reinforcement bars, meshes, tendons, and strands.
*   `AccessoryWeightContributor`: Embedded plates, anchors, bolts, inserts, welds, connection parts, paint, and grout.
*   `IgnoredReference`: Helper lines, grids, openings, annotations, or spaces.
*   `UnknownNeedsReview`: Fallback classification if the element category is unrecognized.

### Main Part Detection & Inference
The system detects the `MainPart` of an assembly using:
*   **Explicit Detection:** Searches property sets for properties named `IsMainPart`, `MainPart`, `IsMain`, or `CastUnitMainPart` with a true-like value.
*   **Inference Heuristics:**
    *   Finds structural candidates (non-hidden/non-accessory elements).
    *   If there is only one structural child, it is selected as the main part (`InferredHighConfidence`).
    *   If there are multiple, the heaviest is selected if it is at least **1.5x** the weight of any runner-up, its category aligns with the parent assembly name or mark meaning, and there is no competing similar child of the same category/weight.
    *   If no candidate meets the high-confidence criteria, the heaviest is selected as a fallback with `InferredLowConfidence` and raises a `NoMainPartInferred` warning.

---

## 3. Diagnostic Reports & Verification

### Assembly Graph Classification Audit Report
Every IFC import generates a detailed report at:
`Reports/IFC_Audit/debug_ifc_assembly_graph_classification_audit.csv`

Columns:
- `ParentGlobalId`
- `ParentGlobalIdSource` (e.g. `IfcDecomposition`, `IfcRelNests`, `TeklaCastUnit`)
- `AssemblyMark`
- `MainPartGlobalId`
- `MainPartName`
- `MainPartCategory`
- `MainPartDetectionMethod` (e.g. `ExplicitMainPart`, `InferredHighConfidence`, `InferredLowConfidence`)
- `ChildCount`, `VisibleSecondaryPartCount`, `HiddenWeightContributorCount`, `AccessoryWeightContributorCount`, `IgnoredReferenceCount`
- `HasSecondaryParts` (true/false)
- `HasHiddenContributors` (true/false)
- `Warnings` (e.g., `MultipleMainPartsDetected`, `NoMainPartInferred`, `MixedCategoryButValidAssembly`)

### Unit Tests
The unit and integration test suite in [IfcUnitNormalizationTests.cs](file:///f:/06-ITI/00-Project/Main%20Project/CraneOptimizer/CraneOptimizer.Application.Tests/IFC/Audit/IfcUnitNormalizationTests.cs) and [AssemblyRollupTests.cs](file:///f:/06-ITI/00-Project/Main%20Project/CraneOptimizer/CraneOptimizer.Application.Tests/IFC/Audit/AssemblyRollupTests.cs) covers unit scaling, georeferenced coordinate translation, assembly graph classification, load-time reconstruction, and GA lift unit rollup calculations.

---

## 4. Assembly Lift Unit Rollup (Phase 4)

To support precast element assembly lifting in Genetic Algorithms (GA) optimization runs, the system rolls up child components into a single representative lift unit:

### GA Lift Unit Identity (Option E)
- The representative lift unit's `LiftUnitId` (or `ElementId` in the GA solver) is mapped to the physical **`MainPartGlobalId`** of the assembly (e.g. the main precast concrete wall panel).
- The `ParentAssemblyGlobalId` and child member list `AssemblyMemberIds` are stored on the representative lift unit.
- Siblings and parent container nodes are excluded from independent GA lift targets.
- WPF viewport highlights propagate GA status maps from the main part `LiftUnitId` to all member child element IDs.

### Weight Rollup Calculations
- Uses the trusted parent assembly explicit weight if available.
- If missing or zero, falls back to the sum of all eligible children (structural parts, accessory parts, reinforcing parts).
- Generates warnings (`WeightMismatchWarning` or `SevereWeightMismatch`) if parent explicit weight differs from the sum of children by more than 1% or 5% respectively.

### COG Rollup (Weighted Centroid)
- Evaluates only children with physical geometries and weights.
- Reinforcing or accessory parts lacking direct geometries will fall back to the Main Part COG if their weight represents $\le$ 2% of the total assembly weight.
- If any major structural/secondary child lacks a physical COG, an `UnsafeCogRollup` warning is flagged.

### Load-Time Classification Reconstruction (Option C)
- Classifications are rebuilt in memory at runtime after project load (`ReadElements`) using persisted child-parent relationship fields, avoiding SQLite database schema migrations.

### Assembly Lift Unit Rollup Audit Report
Generated at import-time under:
`Reports/IFC_Audit/debug_ifc_assembly_lift_unit_rollup_audit.csv`
Includes columns for LiftUnitId, parent details, member GUIDs, contributor counts, weight sources/values, weighted COG, sources, and safety/mismatch warnings.

---

## 5. GA Entry Gating & Unrolling (Phase 4.5)

To ensure that precast assembly elements are gated and promoted correctly under both grouped and ungrouped modes, the system applies the following normalization and routing rules:

### Mixed Assembly Gating Promotion
- Generic mixed assemblies (containing structural main parts combined with secondary components of different classes) are promoted to `IfcElementRole.LiftAssembly` and `IncludeInGa = true` during IFC import/normalization if they possess:
  - Trusted parent-child hierarchy (e.g. `IfcRelAggregates` or `IfcRelNests`).
  - A valid structural main part (`MainPartGlobalId`).
  - A trusted explicit weight.
  - A safe rolled-up COG (no `UnsafeCogRollup` warning).
- This replaces the rigid single-category check, allowing valid assemblies to enter the GA solver naturally without manual patches.

### Sibling Exclusions (Grouped ON)
- When `EnableGroupedLifts` is `ON`, the parent assembly container is excluded from independent lifting.
- All sibling components are marked as `Role = Excluded` and `IncludeInGa = false` with the reason `"Swallowed as child component of valid Lift Assembly."`.
- Only the representative GA lift unit (with `LiftUnitId = MainPartGlobalId` carrying rolled-up weight and child-weighted centroid COG) enters the GA solver inputs.

### Container Exclusions & Unrolling (Grouped OFF)
- When `EnableGroupedLifts` is `OFF`, the parent assembly container (which lacks direct geometry) is explicitly excluded from GA candidate lists.
- Child components are unrolled and enter the GA solver independently as individual physical lift units (e.g., the concrete wall and secondary steel beams are lifted separately with their raw individual properties).

---

## 6. Reports & F5 Panel Integration (Phase 5)

To present rolled-up assembly properties to users in reports and user interface views, the system provides integrated fields and filters:

### F5 Panel Assembly Summary
- When selecting the parent container, the main part, or any child component of a valid assembly in the Element Management pane, an **"Assembly Summary"** group is displayed in the properties list.
- The summary shows:
  - **Assembly**: The parent assembly mark (e.g. `W1`).
  - **Main Part**: The category name of the main part in uppercase (e.g. `WALL`).
  - **Has Secondary Parts**: `"Yes"` or `"No"`.
  - **Included Hidden Contributors**: `"Rebar weight included"` (if reinforcing/accessory parts are present) or `"None"`.
  - **Total Lift Weight**: The aggregate rolled-up weight (e.g. `9.28t`) formatted dynamically in unit settings format with a `t` suffix.
  - **Profile**: The profile dimension of the main part formatted as `Width*Length` in settings unit (e.g., `200*4000`), derived using `UnitSystem` display formatting.

### F8 / GA Reports Integration
- The default **Basic Lifting Result** report template and available columns list include four new columns mapping rolled-up metadata:
  - `ParentAssemblyGlobalId`: The ID of the parent container assembly.
  - `AssemblyLiftWeight`: The aggregate rolled-up lift weight of the assembly.
  - `AssemblyLiftCOG`: The aggregate weighted COG coordinate values.
  - `AssemblyMemberIds`: The list of all member element IDs included in the assembly.
- The solver maps the `MainPartGlobalId` as the `ElementId` (per Option E) along with the rolled-up weight/COG.

### Reportable Element Filtering
- Auxiliary elements classified as `HiddenWeightContributor` (reinforcement bars/meshes) or `AccessoryWeightContributor` (inserts/plates) are excluded from user-facing reports (such as normal element lists and lifting reports) to keep user-facing views clean, while their weights are preserved in rolled-up assembly totals.

