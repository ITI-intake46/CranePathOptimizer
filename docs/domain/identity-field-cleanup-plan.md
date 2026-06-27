# Phase 7E — Legacy Identity Field Cleanup Audit & Migration Plan

## Objective
This document provides a comprehensive audit of legacy and duplicate identity fields within the Crane Optimizer application. Following the stabilization of the IFC Identity Contract in Phase 7B and the UI alignment in Phase 7D, this audit identifies safe cleanup candidates, assesses persistence compatibility, and outlines a future migration strategy to remove technical debt without breaking existing project files or external CSV integrations.

## 1. Legacy Identity Field Inventory

| Field Name | Class / File | Layer | Current Meaning | Writers | Readers | Persistence Status | Recommended Classification |
|---|---|---|---|---|---|---|---|
| `ElementID` | `PrecastElement`, `ProjectIfcElementEntity.GlobalId` | Domain/Entity | Stable IFC GlobalId | `IfcReaderRepository` | All systems | Persisted | **Canonical Keep** |
| `ElementName` | `PrecastElement`, `ProjectIfcElementEntity.DisplayName` | Domain/Entity | Raw IFC product name | `IfcReaderRepository` | `CanonicalMarkResolver`, UI | Persisted | **Canonical Keep** |
| `ElementType` | `PrecastElement`, `ProjectIfcElementEntity.ElementType` | Domain/Entity | IFC Product Type string | `IfcReaderRepository` | Diagnostics | Persisted | **Canonical Keep** |
| `ElementMark` | `PrecastElement`, `ProjectIfcElementEntity.Mark` | Domain/Entity | Legacy part mark | `IfcReaderRepository` | `CanonicalMarkResolver` (fallback) | Persisted | **Legacy Compatibility Keep** |
| `ElementRevitId` | `PrecastElement`, `ProjectIfcElementEntity.Name` | Domain/Entity | Legacy Revit ID string | `IfcReaderRepository` | String heuristics | Persisted (as `Name`) | **Obsolete Candidate** |
| `AssemblyMark` | `PrecastElement`, `ProjectIfcElementEntity.AssemblyMark` | Domain/Entity | Canonical lift assembly mark | `CentralIfcPropertyAliasResolver` | `CanonicalMarkResolver` | Persisted | **Canonical Keep** |
| `AssemblyMarkSource` | `PrecastElement`, `ProjectIfcElementEntity.AssemblyMarkSource` | Domain/Entity | Source property name | `CentralIfcPropertyAliasResolver` | Diagnostics, UI | Persisted | **Canonical Keep** |
| `AssemblyMarkConfidence`| `PrecastElement`, `ProjectIfcElementEntity.AssemblyMarkConfidence`| Domain/Entity | Confidence of the mark extraction | `CentralIfcPropertyAliasResolver` | UI (Review Status) | Persisted | **Canonical Keep** |
| `AssemblyMarkIsExplicit`| `PrecastElement`, `ProjectIfcElementEntity.AssemblyMarkIsExplicit`| Domain/Entity | True if resolved from explicit property| `CentralIfcPropertyAliasResolver` | `CanonicalMarkResolver` | Persisted | **Canonical Keep** |
| `ResolvedDisplayMark` | `PrecastElement` | Domain | Old fallback identity (deprecated) | `IfcReaderRepository` | `CanonicalMarkResolver` (fallback) | NOT Persisted | **Delete Candidate Later** |
| `CanonicalMark` / `DisplayLabel` | `ElementReportRowDto`, `LiftingResultSourceRowDto` | DTOs | Resolved final display identity | `CanonicalMarkResolver` | Reports, UI, CSV | Derived at runtime | **Canonical Keep** |
| `ParentAssemblyName` | `PrecastElement` | Domain | Name of parent assembly | `IfcReaderRepository` | `CanonicalMarkResolver` | NOT Persisted | **Obsolete Candidate** |
| `ChildGlobalIds` | `PrecastElement`, `ProjectIfcElementEntity` | Domain/Entity | IFC GUIDs of children in assembly | `IfcReaderRepository` | `CranePlacementEngine` | Persisted | **Canonical Keep** |
| `SourceSystem` | `PrecastElement`, `ProjectIfcElementEntity` | Domain/Entity | Identified source platform | `IfcReaderRepository` | UI | Persisted | **Canonical Keep** |
| `ExporterProfile` | `PrecastElement`, `ProjectIfcElementEntity` | Domain/Entity | Profile used for alias mapping | `IfcReaderRepository` | UI | Persisted | **Canonical Keep** |
| `ValidationStatus` | `PrecastElement`, `ProjectIfcElementEntity` | Domain/Entity | Output of validation rules | `CentralIfcPropertyAliasResolver` | UI | Persisted | **Canonical Keep** |
| `ValidationWarnings` | `PrecastElement`, `ProjectIfcElementEntity` | Domain/Entity | Reasons for low confidence | `CentralIfcPropertyAliasResolver` | UI | Persisted | **Canonical Keep** |
| `RawAssemblyPos` | N/A | N/A | Handled via exporter profile aliases | N/A | N/A | N/A | **Internal Mapping Only** |
| `RawCastUnitPos` | N/A | N/A | Handled via exporter profile aliases | N/A | N/A | N/A | **Internal Mapping Only** |

---

## 2. Canonical vs Legacy Map

**AssemblyMark (Canonical Display Target)**
*   **Tekla Raw**: `ASSEMBLY_POS`, `CAST_UNIT_POS`, `ASSEMBLY_PREFIX`, `CAST_UNIT_PREFIX`, etc.
*   **Revit Raw**: `Assembly Mark`, `Assembly Name`
*   **Legacy Overlap**: `ElementMark` (historically used as fallback when AssemblyMark was missing).
*   **Display Consumer**: `CanonicalMarkResolver.DisplayLabel` (Primary Priority).

**ElementName (Canonical Base Identity)**
*   **Source**: Raw IFC Name property.
*   **Rule**: Must not be replaced by `AssemblyMark` or `ElementMark`.
*   **Display Consumer**: 4th level fallback in `CanonicalMarkResolver`.

**Fields that MUST NEVER be written from AssemblyMark:**
*   `ElementID` (IFC GlobalId)
*   `ElementName` (Raw IFC Name)
*   `ElementType`

---

## 3. Cleanup Decision Matrix

| Field / Concept | Recommendation | Why | Risk | Migration Needed? | Tests Needed? | Target Phase |
|---|---|---|---|---|---|---|
| `ResolvedDisplayMark` | Delete Candidate Later | Legacy derived field not persisted in DB. Handled better by `CanonicalMarkResolver`. | Low | No | Yes | 7E-4 |
| `ElementRevitId` | Migrate data then remove | Conceptually replaced; currently maps confusingly to `ProjectIfcElementEntity.Name`. | Medium | Yes (EF Core Schema) | Yes | 7E-3 |
| `ElementMark` | Keep for backward compat | Contains raw Part Marks which may be needed for deep debugging. Safely bypassed by Canonical resolver. | None | No | No | N/A |
| `ParentAssemblyName`| Delete Candidate Later | Not persisted, only used as a deep fallback in `CanonicalMarkResolver`. | Low | No | Yes | 7E-4 |

---

## 4. Persistence Compatibility Audit

*   **Persisted Identity Fields:** `GlobalId`, `Name` (mapped to `ElementRevitId`), `DisplayName` (mapped to `ElementName`), `Mark` (mapped to `ElementMark`), `ElementType`, `AssemblyMark`, `AssemblyMarkSource`, `AssemblyMarkConfidence`, `AssemblyMarkIsExplicit`, `SourceSystem`, `ExporterProfile`, `ValidationStatus`, `ValidationWarningsJson`, `ChildGlobalIdsJson`.
*   **Restored Fields:** All of the above are mapped 1:1 in `PrecastElementMapper.FromEntity`.
*   **Derived at Runtime:** `ResolvedDisplayMark` and `ParentAssemblyName` are **not** restored from the DB. `CanonicalMark` is derived strictly at the UI/Report boundary via `CanonicalMarkResolver`.
*   **Backward Compatibility (Old Projects):** Old projects missing `AssemblyMark` data will have `NULL` values in the DB. `CanonicalMarkResolver` safely falls back to `ElementMark` -> `ElementName` -> `ElementID`. They can open and function normally.
*   **New Projects:** Populate `AssemblyMark` reliably using Phase 2 infrastructure.
*   **Duplicate Persisted Concepts:** `ElementRevitId` mapped to `ProjectIfcElementEntity.Name` vs `ElementName` mapped to `ProjectIfcElementEntity.DisplayName`. This is confusing domain mapping that requires a formal EF Core migration to fix safely.
*   **Can fields be deleted now?** Deleting `ProjectIfcElementEntity.Name` or `Mark` requires an EF Core migration. Deleting public domain properties (`ElementRevitId`, `ResolvedDisplayMark`) requires test updates.

---

## 5. DTO Compatibility Audit

*   **Needed for backward compatibility:** `ElementMark` is still populated in `PlacementResult.ElementLiftResult` and `ElementReportRowDto`. It is necessary to preserve legacy CSV header contracts for external analytics scripts.
*   **Active Display Fields:** `CanonicalMark`, `IsExplicitMark`, `MarkConfidence`.
*   **Duplicates:** `ElementMark` duplicates the display intent of `CanonicalMark`, but is safe to keep silently in the background.
*   **Safe to Hide:** `ElementMark` can be hidden from new UI panels (already done in F5/F8), but should not be deleted from DTOs.

---

## 6. Safe Immediate Cleanup Candidates

**Conclusion: There are no 100% risk-free public property deletions for this phase.**
Because any property removal requires modifying `CanonicalMarkResolver`, `PrecastElement`, `ProjectIfcElementEntity`, or `PrecastElementMapper` (and thus fixing failing unit tests or breaking EF Core), no schemas will be mutated in Phase 7E-1. Safe cleanups are restricted to dead comments or unused local string allocations, but none of significant value exist without touching the schema.

---

## 7. Future Migration Plan

*   **Phase 7E-1 (Current):** Audit and classification only.
*   **Phase 7E-2:** Deprecate and remove `ResolvedDisplayMark` and `ParentAssemblyName` from `PrecastElement` and `CanonicalMarkResolver`. Update all failing unit tests.
*   **Phase 7E-3:** Execute EF Core DB Migration to rename `ProjectIfcElementEntity.Name` to `RevitId` (or drop it), and align `PrecastElement.ElementRevitId` for graceful removal without destroying legacy project files.
*   **Phase 7E-4:** Remove the obsolete properties from the codebase entirely, ensuring reports and CSVs are purely driven by `CanonicalMark` and `ElementMark`.

> [!NOTE]
> This plan ensures external analytics files, old saved projects, and active F5/F8 display logic remain completely unbroken during the cleanup process.
