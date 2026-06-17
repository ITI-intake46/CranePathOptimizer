# Architecture: Rendering Strategy & Element Status Coloring

This document explains the architectural rendering strategy implemented in the Mobile Crane Path Optimizer viewport. It details the in-place material override mechanism and the stress-testing strategies that enable high-performance rendering of models containing up to 7,000 precast elements.

---

## 1. Dynamic Visual Status Coloring (In-Place Material Override)

Dynamic element status representation (highlighting elements as liftable, overweight, out of radius, or selected) is implemented via an **in-place material override** directly on the base element geometries.

### Implementation Details
*   **Geometry Registry:** During the initial scene generation pass, the [ElementVisualBuilder.cs](file:///f:/06-ITI/00-Project/Main%20Project/CraneOptimizer/CraneOptimizer.UI/Features/F2_IfcViewer/ElementVisualBuilder.cs#L29) class populates a dictionary, `ElementModelRegistry` (mapping `ElementID` strings to list collections of WPF `GeometryModel3D` instances representing the element's sub-meshes).
*   **Scene Synchronization:** The [SceneRenderingManager.cs](file:///f:/06-ITI/00-Project/Main%20Project/CraneOptimizer/CraneOptimizer.UI/Core/Services/SceneRenderingManager.cs#L191) copies this registry into its internal `_renderedElementModels` dictionary.
*   **Material Application:** When optimization runs complete or selections change, [SceneRenderingManager.cs](file:///f:/06-ITI/00-Project/Main%20Project/CraneOptimizer/CraneOptimizer.UI/Core/Services/SceneRenderingManager.cs#L323-L341) updates the colors dynamically. It iterates over all active elements, checks their current status against the selection state and optimization status map, resolves the appropriate material from the centralized [ElementMaterialProvider.cs](file:///f:/06-ITI/00-Project/Main%20Project/CraneOptimizer/CraneOptimizer.UI/Features/F2_IfcViewer/ElementMaterialProvider.cs#L80-L112), and directly mutates the `model.Material` and `model.BackMaterial` properties of the cached `GeometryModel3D` instances in-place.
*   **Static Marker Layers:** As shown in [SelectedPointRenderer.cs](file:///f:/06-ITI/00-Project/Main%20Project/CraneOptimizer/CraneOptimizer.UI/Features/F8_GAOptimizer/Rendering/SelectedPointRenderer.cs#L41-L54), only the blue sphere setup marker is rendered on a dedicated viewport overlay layer (`GA_SelectedPoint`). The status colors of the building elements themselves remain strictly on the base elements layer.

### Architectural Advantages
1.  **No Mesh Redundancy:** Modifying material properties in-place does not require spawning overlay meshes or duplicating underlying vertex geometries. This keeps memory allocations flat during active user interactions.
2.  **No Z-Fighting:** By avoiding overlapping duplicate geometry, this in-place coloring strategy eliminates z-fighting and rendering artifacts.
3.  **BIM Viewer Alignment:** Elements retain their native spatial positions and structural rendering boundaries while dynamically carrying visual states, aligning with industry standards for BIM authoring viewers (e.g., Navisworks, Solibri).

---

## 2. 7,000-Element Stress-Test Rendering Strategy

To support smooth rendering and interactions in models with thousands of precast elements (stress-tested up to 7,000 elements), the rendering pipeline employs several key optimization patterns:

### A. Geometry Mesh Caching & Sharing
Tessellating complex IFC geometries using xBIM is a computationally intensive operation. To prevent redundant work, [ElementVisualBuilder.cs](file:///f:/06-ITI/00-Project/Main%20Project/CraneOptimizer/CraneOptimizer.UI/Features/F2_IfcViewer/ElementVisualBuilder.cs#L114-L121) maintains two static caches:
*   `MeshCache`: Stores medium-quality meshes, keyed by `MeshCacheKey` (containing the session ID, shape data hash, and scale factor).
*   `HighQualityMeshCache`: Stores high-quality detailed meshes, keyed by `HighQualityMeshCacheKey` (incorporating instance transforms and placement offsets).

Access to these static dictionaries is synchronized using dedicated lock structures (`MeshCacheGate` and `HighQualityMeshCacheGate`) to ensure thread safety when rendering models concurrently.

> [!NOTE]
> **Key Rewrite Deferred**: The refactoring of the `HighQualityMeshCacheKey` (to exclude translation offsets and share mesh instances across identical structural elements via instance transforms) is deferred and is not currently implemented.

### B. WPF Object Freezing
In WPF, any object inheriting from `Freezable` (such as `MeshGeometry3D`, `SolidColorBrush`, and `MaterialGroup`) incurs thread-affinity overhead. 

*   **Meshes & Materials**: In [ElementVisualBuilder.cs](file:///f:/06-ITI/00-Project/Main%20Project/CraneOptimizer/CraneOptimizer.UI/Features/F2_IfcViewer/ElementVisualBuilder.cs#L686) and [ElementMaterialProvider.cs](file:///f:/06-ITI/00-Project/Main%20Project/CraneOptimizer/CraneOptimizer.UI/Features/F2_IfcViewer/ElementMaterialProvider.cs#L108-L120), all meshes, brushes, and materials are explicitly frozen (`Freeze()`) immediately after construction.
*   **Fallback Box Freezing (Phase 1)**: Bounding-box and fallback/low-quality `MeshGeometry3D` elements built in `ElementVisualBuilder` (e.g. `BuildFallbackBox` and `BuildIndexedBoxMesh`) are frozen after their initial coordinate assignment. This prevents WPF change tracking and thread-affinity overhead on static geometries.

Freezing turns these objects read-only, allowing them to be shared across threads without marshalling, reducing the WPF render thread workload and lowering CPU overhead.

### B2. Render and Cache Diagnostics (Phase 1)
To verify cache performance, `RenderPerformanceStats` exposes cache hit ratio diagnostics in real-time:
*   Tracks total render duration, memory consumption delta, model and geometry counts.
*   **Cache Diagnostics**: Exposes `MeshCache` and `HighQualityMeshCache` hit ratios (total lookups, hits, misses, and hit ratio percentage).
*   **Logging**: `SceneRenderingManager` diagnostic logging prints these cache stats during execution.

### B3. Visual Tree Structure (Deferred)
To prevent scene graph bloat, flattening the `ModelVisual3D` hierarchy into merged visual groups is deferred. Each element continues to map to its own visual container node to support per-element material overrides.

### C. Level of Detail (LOD) Thresholds
Users can dynamically adjust the visualization fidelity through the UI (combobox bound to `RenderQualityCombo_SelectionChanged` in [IfcViewerControl.xaml.cs](file:///f:/06-ITI/00-Project/Main%20Project/CraneOptimizer/CraneOptimizer.UI/Features/F1_IfcViewerControl/Views/IfcViewerControl.xaml.cs#L417-L444)). This controls the meshing logic inside the builder:
*   **Low Quality:** Bypasses xBIM shape representation entirely. Elements are represented by lightweight, batched bounding boxes built via `BuildLargeModelBatch` in [ElementVisualBuilder.cs](file:///f:/06-ITI/00-Project/Main%20Project/CraneOptimizer/CraneOptimizer.UI/Features/F2_IfcViewer/ElementVisualBuilder.cs#L501-L538). This provides maximum performance for large models on low-spec hardware.
*   **Medium Quality:** Extends IFC shape geometry but applies simplified mesh triangulation.
*   **High Quality:** Generates detailed meshes, using caching keyed by transformation hashes to minimize redundant math.

### D. Memory Lifecycle & Disposal
To prevent memory leaks across different models, the geometry caches are bound to a session lifecycle. The `EnsureCacheSession` method in [ElementVisualBuilder.cs](file:///f:/06-ITI/00-Project/Main%20Project/CraneOptimizer/CraneOptimizer.UI/Features/F2_IfcViewer/ElementVisualBuilder.cs#L758-L792) detects when a new IFC model is opened (comparing current session GUIDs) and programmatically clears the static dictionary caches (`MeshCache.Clear()` and `HighQualityMeshCache.Clear()`). This releases unmanaged shape representation buffers and frees unmanaged DirectX resources.

### E. Batch Dispatcher Frames
To keep the UI highly responsive during heavy navigation interactions (such as rapid panning or zooming via the mouse wheel), clipping updates are queued asynchronously. As shown in [IfcViewerControl.xaml.cs](file:///f:/06-ITI/00-Project/Main%20Project/CraneOptimizer/CraneOptimizer.UI/Features/F1_IfcViewerControl/Views/IfcViewerControl.xaml.cs#L336-L343), when a mouse-wheel zoom event triggers, a background task is dispatched using `viewport.Dispatcher.BeginInvoke` with `DispatcherPriority.Background`. This deferral ensures that rendering computations are executed outside the hot interaction frames, preventing UI freeze and jitter.

### F. GPU Instancing [Planned Feature]
To further optimize scenes exceeding 7,000 elements, support for GPU instancing is planned. Leveraged via HelixToolkit's instanced mesh controls, identical duplicate objects (such as repeated column structures or shear walls) will share a single GPU draw call, minimizing API driver overhead.
