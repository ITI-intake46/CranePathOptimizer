# Viewport Interaction: Snapping, Selection, Filtering & Measurement

This document details the architecture, class structures, and interactive flows of the advanced visual systems residing in the WPF UI layer.

---

## 1. Object Snapping (`F15_ObjectSnap`)

The Object Snap (Osnap) system allows users to snap their mouse pointer to geometric elements (corners, midpoints, edges, centers) of either imported IFC building meshes or DXF site layout vector entities.

### Key Components

```
                    [OsnapService]
                          │
         ┌────────────────┴────────────────┐
         ▼                                 ▼
 [IfcMeshSnapExtractor]            [DxfSnapResolver]
 (Traverses Helix 3D meshes)      (Reads netDxf entity geometry)
```

*   **`OsnapService` ([OsnapService.cs](file:///f:/06-ITI/00-Project/Main%20Project/CraneOptimizer/CraneOptimizer.UI/Features/F15_ObjectSnap/Services/OsnapService.cs)):** 
    The central coordinator that registers snapping sources, tracks active snap modes (Endpoint, Midpoint, Center, Intersection), and processes snapping queries under cursor screen coordinates.
*   **`IfcMeshSnapExtractor` ([IfcMeshSnapExtractor.cs](file:///f:/06-ITI/00-Project/Main%20Project/CraneOptimizer/CraneOptimizer.UI/Features/F15_ObjectSnap/Services/IfcMeshSnapExtractor.cs)):** 
    Extracts snapping vertices from 3D visual representations of IFC models. It traverses Helix 3D `MeshGeometry3D` structures and extracts boundary vertices, edges, and centroids.
*   **`DxfSnapResolver` ([DxfSnapResolver.cs](file:///f:/06-ITI/00-Project/Main%20Project/CraneOptimizer/CraneOptimizer.UI/Features/F15_ObjectSnap/Services/DxfSnapResolver.cs)):** 
    Parses vector lines, circles, and arcs from aligned DXF layers and resolves their geometric snapping coordinates (e.g. line endpoints, circle centers).
*   **`OsnapGeometryBridge` ([OsnapGeometryBridge.cs](file:///f:/06-ITI/00-Project/Main%20Project/CraneOptimizer/CraneOptimizer.UI/Features/F15_ObjectSnap/Services/OsnapGeometryBridge.cs)):** 
    Transforms screen points to 3D world rays and projects snapping candidates back to screen space to find the closest candidate within a pixel tolerance (typically 10-15 pixels).

### Snapping Execution Flow

1. The user moves the cursor in the 3D viewport.
2. The active drawing interaction mode queries the `OsnapService` with the screen point.
3. The service maps the ray into the 3D scene:
   * **IFC Source:** `IfcMeshSnapExtractor` returns vertices near the cursor ray.
   * **DXF Source:** `DxfSnapResolver` calculates intersections and snapping targets under the aligned transform.
4. The closest point is snapped to, and `SnapIndicatorBuilder` renders a green diamond indicator around the cursor.

---

## 2. Storey Visibility & Filtering (`F16_Filter`)

The filtering system allows users to manage element clutter by toggling visibility based on storeys, structural categories, or custom material names.

### Key Components

*   **`ElementVisibilityFilterService` ([ElementVisibilityFilterService.cs](file:///f:/06-ITI/00-Project/Main%20Project/CraneOptimizer/CraneOptimizer.UI/Features/F16_Filter/Services/ElementVisibilityFilterService.cs)):** 
    Tracks visibility states (visible/hidden collections) per element and storey. It evaluates storey associations parsed during IFC import.
*   **`ElementWeightCalculator` ([ElementWeightCalculator.cs](file:///f:/06-ITI/00-Project/Main%20Project/CraneOptimizer/CraneOptimizer.UI/Features/F16_Filter/Services/ElementWeightCalculator.cs)):** 
    Heuristically estimates precast element weights from volume and material keywords (Steel, Aluminum, Glass, Timber) specifically for display in the UI element trees.
*   **`FilterHierarchyNode` ([FilterHierarchyNode.cs](file:///f:/06-ITI/00-Project/Main%20Project/CraneOptimizer/CraneOptimizer.UI/Features/F16_Filter/ViewModels/FilterHierarchyNode.cs)):** 
    ViewModel representing the hierarchical checkable tree of storeys and elements in the UI sidebar.

### Storey Visibility Flow

```
[UI Checkbox Clicked] ──> [FilterHierarchyNode] ──> [ElementVisibilityFilterService]
                                                               │
                                                               ▼
[SceneRenderingManager] <── [Triggers ElementsChanged] <── [Rebuilds Visible Element List]
```

---

## 3. Viewport Selection & Hover (`F17_Selection`)

Selection handling uses mouse raycasting to pick specific structural elements in the Helix 3D viewport.

### Key Components

*   **`SelectionHandler` ([SelectionHandler.cs](file:///f:/06-ITI/00-Project/Main%20Project/CraneOptimizer/CraneOptimizer.UI/Features/F17_Selection/Interaction/SelectionHandler.cs)):** 
    Subscribes to viewport mouse events. Resolves mouse hovers and clicks to identify target elements.
*   **`HoverHitCache` ([HoverHitCache.cs](file:///f:/06-ITI/00-Project/Main%20Project/CraneOptimizer/CraneOptimizer.UI/Features/F17_Selection/Services/HoverHitCache.cs)):** 
    Caches hit tests to avoid re-querying the visual tree on every mouse move, preventing UI lag when hovering over complex models.

### Selection and Direct Material Coloring

1. Clicking an element triggers the selection command.
2. `ViewportSelectionService` notifies the `SceneRenderingManager`.
3. The mesh is re-rendered with the highlight color (typically terracotta or selection cyan) as configured in `DirectMaterialColoringDesign` settings.

---

## 4. Measurement Tool (`F7_Zones`)

The **Measure** option in the Zones tab is a hybrid 3D/2D tool. It renders 3D helper lines in the scene and projects 2D overlays on top of the viewport to display distances.

### Interactive Measurement Flow

```
               [DrawingInteractionMode]
                          │
       (Mouse Click) ─────┼───── (Mouse Move)
                          │
                          ▼
            [DrawingViewModel.AddPoint]
            - Records clicked points in _measurePoints
            - Triggers MeasurementLabelsChanged
                          │
                          ▼
         [IfcViewerControl.RebuildMeasurementLabels]
         - Clears MeasurementOverlayCanvas
         - Adds 2D TextBlock labels for distances
         - Draws 3D dashed line via MeasurementPreviewBuilder
```

### Rendering Mechanics

*   **3D Visuals:**
    *   **`MeasurementPreviewBuilder` ([MeasurementPreviewBuilder.cs](file:///f:/06-ITI/00-Project/Main%20Project/CraneOptimizer/CraneOptimizer.UI/Features/F7_Zones/MeasurementPreviewBuilder.cs)):** Builds a temporary dashed line between the last clicked point and the moving cursor.
    *   **`MeasurementFinalBuilder` ([MeasurementFinalBuilder.cs](file:///f:/06-ITI/00-Project/Main%20Project/CraneOptimizer/CraneOptimizer.UI/Features/F7_Zones/MeasurementFinalBuilder.cs)):** Builds 3D thick line geometries for completed measurements.
*   **2D Labels Overlays:**
    *   `IfcViewerControl.xaml` features a `MeasurementOverlayCanvas` overlaying the 3D viewport.
    *   On `MeasurementLabelsChanged` event, the code-behind calls `UpdateMeasurementLabelPositions()`. It projects the 3D segment midpoints into screen coordinates using the Helix viewport projection camera, moving the 2D WPF Borders to overlay precisely on top of the 3D lines.
    *   Distance readouts snap to active coordinate scales and units.
*   **Status Bar Readout:**
    *   The active message is updated in `DrawingViewModel.MeasureText` (e.g. `📏 Click first point`, `Total: 14.50m`) to guide the user step-by-step.
