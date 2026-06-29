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
      │                              (e.g., assembly_graph_classification_audit.csv)
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
`Reports/IFC_Audit/assembly_graph_classification_audit.csv`

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
The unit and integration test suite in [IfcUnitNormalizationTests.cs](file:///f:/06-ITI/00-Project/Main%20Project/CraneOptimizer/CraneOptimizer.Application.Tests/IFC/Audit/IfcUnitNormalizationTests.cs) covers unit scaling, georeferenced coordinate translation, and assembly graph classification.
