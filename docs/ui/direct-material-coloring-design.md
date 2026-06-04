# Direct Material Coloring Design

This document records the current IFC element visualization pipeline and the refactoring direction for BIM-style direct material coloring. It is based on the current C# implementation; source code remains the source of truth.

## Current Rendering Architecture

The IFC visualization path starts in `IfcReaderRepository`, which opens the IFC model with xBIM, creates a geometry context, extracts `PrecastElement` metadata, and retains the current model/session for rendering.

Rendering is orchestrated by `SceneRenderingManager`. It listens to `ISceneService.ElementsChanged`, `ISceneService.ElementsSelectionChanged`, filter changes, and crane/zone changes. Element rendering is pushed through `IRenderingService.Render(..., "Elements")`, which clears the `"Elements"` layer and adds the batch visual produced by `ElementVisualBuilder`.

`ElementVisualBuilder` has three geometry paths:

| Path | Geometry source | Material source | Notes |
|---|---|---|---|
| Medium/High IFC mesh | xBIM `ShapeInstances()` and `ShapeGeometry()` | `ElementMaterialProvider` | One visible `GeometryModel3D` per shape instance; models are registered by `ElementID`. |
| Low quality | Lightweight box meshes from element bounding boxes | `ElementMaterialProvider` | One visible `GeometryModel3D` per element for fast rendering and dynamic material updates. |
| Fallback | Bounding-box mesh when no xBIM context is available | `ElementMaterialProvider` | One visible `GeometryModel3D` per element. |

The rendered `GeometryModel3D` instances are stored in `ElementVisualBuilder.ElementModelRegistry`. `SceneRenderingManager` copies that registry into `_renderedElementModels` after each render.

## Existing Visualization Flow

Status resolution happens in `SceneRenderingManager` from `ElementLiftResult` data. The active `GAViewMode` determines whether the status comes from selected-point feasibility, final assignment, or best failed status. Selection is read from the scene and takes visual precedence.

The status then flows through:

1. `SceneRenderingManager` builds `(PrecastElement element, LiftStatus? status, bool isSelected)` tuples.
2. `ElementVisualBuilder` resolves `ElementVisualizationStatus` through `ElementMaterialProvider.GetStatus(...)`.
3. `ElementMaterialProvider.GetMaterial(...)` returns a cached frozen WPF material.
4. `GeometryModel3D.Material` and `BackMaterial` are assigned directly on the element geometry.

Selection visualization is also direct material coloring. It does not create halos, outlines, or duplicate colored meshes.

`SelectedPointRenderer` only renders the selected GA point marker on `"GA_SelectedPoint"`. It clears the legacy `"GA_StatusOverlay"` layer defensively but no longer builds status overlay geometry.

## Technique Assessment

| Technique | Current status | Evidence |
|---|---|---|
| Overlay meshes | Removed for status visualization | `SelectedPointRenderer` is marker-only; status colors update base element materials. |
| Duplicate geometry for status | Removed for status visualization | No separate status layer is rendered. Each visible element has one base visual representation. |
| Material replacement | Primary technique | `SceneRenderingManager.UpdateElementMaterials()` updates `GeometryModel3D.Material` and `BackMaterial` in place. |
| Bounding-box rendering | Retained only for low-quality/fallback geometry | Low quality and no-IFC fallback intentionally use box geometry as the base representation, not an overlay. |
| Visual layers | Retained for scene organization | `"Elements"`, `"Cranes"`, `"CraneRadius"`, and `"GA_SelectedPoint"` remain separate rendering layers. |
| Transparent hit-test boxes | Disabled by default | The feature flag `UseSpatialHitTestingInsteadOfTransparentBoxes` defaults to `true`; fallback transparent boxes are picking support, not status visualization. |

## Problems With The Previous Overlay Approach

The previous status overlay design rendered extra colored geometry over or around IFC elements. That created a debugging-style appearance rather than a professional BIM viewer appearance.

Problems:

- Duplicate meshes increased visual tree size, memory use, and draw overhead.
- Transparent overlays risked z-fighting, fuzzy edges, halos, and reduced geometry readability.
- Status changes required rebuilding or clearing overlay layers instead of updating materials.
- Status color logic was easier to scatter across renderers.
- The viewer could show more than one visible representation of the same element.

## Proposed Architecture

The active architecture should remain direct material coloring:

1. Keep geometry generation independent from visualization mode.
2. Cache or reuse mesh geometry by session and quality.
3. Keep a registry from `ElementID` to visible `GeometryModel3D` instances.
4. Resolve semantic visual state through `ElementVisualizationStatus`.
5. Resolve colors and materials only through `ElementMaterialProvider`.
6. Update only `Material` and `BackMaterial` when GA results, selected point, selection, or visualization mode changes.
7. Keep overlays out of element status display. Retain only non-element scene helpers such as selected point markers and crane radius visuals.

## Status-To-Color Mapping

| `ElementVisualizationStatus` | RGB | Intended appearance |
|---|---:|---|
| `Default` | `180,180,180` | Neutral concrete/model gray |
| `Liftable` | `120,220,120` | Muted BIM success green |
| `Overweight` | `220,90,90` | Controlled warning red |
| `OutOfRadius` | `120,120,120` | Muted unavailable gray |
| `HeightIssue` | `240,180,80` | Amber clearance warning |
| `Selected` | `100,160,255` | Professional blue selection tint |

`LiftabilityColorStrategy` currently maps `LiftStatus.Liftable`, `Overweight`, `OutOfRadius`, and `HeightFail` into these visualization statuses. `SpecialCase` and `NotCharted` currently fall back to `Default`; this can be refined by a future visualization strategy without changing geometry generation.

## Affected Classes

| File/class | Responsibility |
|---|---|
| `CraneOptimizer.UI/Features/F2_IfcViewer/ElementVisualizationStatus.cs` | Element visual status enum. |
| `CraneOptimizer.UI/Features/F2_IfcViewer/IElementColorStrategy.cs` | Strategy contract for visualization modes. |
| `CraneOptimizer.UI/Features/F2_IfcViewer/LiftabilityColorStrategy.cs` | Current liftability/status mapping strategy. |
| `CraneOptimizer.UI/Features/F2_IfcViewer/ElementMaterialProvider.cs` | Centralized color palette, material creation, material caching, active strategy. |
| `CraneOptimizer.UI/Features/F2_IfcViewer/ElementVisualBuilder.cs` | Geometry creation, material assignment, element model registry, spatial hit-test registry. |
| `CraneOptimizer.UI/Core/Services/SceneRenderingManager.cs` | Status-map resolution and dynamic material updates. |
| `CraneOptimizer.UI/Core/Interfaces/ISceneRenderingManager.cs` | Exposes `UpdateElementMaterials(...)`. |
| `CraneOptimizer.UI/Features/F8_GAOptimizer/Rendering/SelectedPointRenderer.cs` | Selected GA point marker only; clears legacy status overlay layer. |
| `CraneOptimizer.UI/Features/F8_GAOptimizer/Views/CraneOptimizerDialog.xaml.cs` | Calls material update for selected GA point results. |

## Required Refactoring Steps

Completed:

1. Introduced `ElementVisualizationStatus`.
2. Introduced `IElementColorStrategy` and `LiftabilityColorStrategy`.
3. Introduced centralized `ElementMaterialProvider`.
4. Routed visible element material creation through `ElementMaterialProvider`.
5. Added an element-to-`GeometryModel3D` registry for dynamic updates.
6. Added `SceneRenderingManager.UpdateElementMaterials(...)`.
7. Removed status overlay mesh rendering from `SelectedPointRenderer`.
8. Changed low-quality rendering to keep one visible model per element so materials can update without rebuilding geometry.

Recommended follow-up:

1. Replace static `ElementMaterialProvider` strategy state with DI if multiple independent viewports require different visualization modes at the same time.
2. Add explicit mappings for `LiftStatus.SpecialCase` and `LiftStatus.NotCharted`.
3. Consider a non-geometry selection outline only if direct selected tint is insufficient for dense models.

## Performance Impact

Positive impacts:

- Status changes and selection changes now update materials in place instead of rebuilding IFC geometry.
- Mesh geometry remains cached by IFC render session and quality.
- Material instances are cached and frozen, reducing brush/material allocations.
- Removing status overlays avoids an additional visible element geometry set.

Trade-offs:

- Low-quality mode now creates one box `GeometryModel3D` per visible element rather than one merged mesh per status/material/type group. This slightly increases model count but is required for per-element material updates without rebuilding.
- Medium/high quality already create one visible model per shape instance, so status updates are cheap but each element may map to multiple models.
- Transparent hit-test boxes can still be enabled by feature flag for picking fallback; this increases visual tree size but does not affect normal default rendering.

## Risks

- Frozen parent model groups cannot be used if child `GeometryModel3D.Material` must change later. The low-quality batch group must remain mutable.
- If future code bypasses `ElementMaterialProvider`, color logic will become scattered again.
- If a future batching optimization merges multiple elements into one geometry model, dynamic per-element material updates will no longer be possible for that batch.
- Selection currently overrides status. This is professional and readable, but users cannot see selected element failure status by color alone while selected.

## Future Extensibility

Future modes should replace the active color strategy without modifying geometry:

- `[Planned Feature]` Liftability Mode
- `[Planned Feature]` Weight Classification Mode
- `[Planned Feature]` Storey Mode
- `[Planned Feature]` Activity Sequence Mode
- `[Planned Feature]` Construction Phase Mode

The extension point is `IElementColorStrategy`. A future strategy can map the same `PrecastElement` plus analysis state to a visualization status or, if more categories are needed, the provider can be extended to support strategy-owned palettes while preserving the geometry registry and in-place material update model.

## Migration Report

The status visualization has moved from an overlay concept to base-geometry material coloring.

Removed or retired:

- Duplicate status overlay meshes.
- Thick transparent status overlays.
- Status-specific geometry layers.
- Low-quality merged status batches that prevented per-element material updates.

Retained:

- The `"Elements"` layer as the single visible IFC element representation.
- The selected GA point marker layer.
- Spatial hit-test registry for picking.
- Transparent hit-test boxes as an off-by-default compatibility fallback.

## Final Recommendation

Do not retain overlay rendering for status visualization. Direct material coloring should be the standard for IFC element state display.

Selection should also remain direct material coloring for now. If dense models later need stronger selection affordance, prefer a thin screen-space outline or edge-emphasis technique over duplicate element geometry, halos, or expanded overlay meshes.
