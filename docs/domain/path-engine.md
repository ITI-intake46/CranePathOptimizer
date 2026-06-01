# Domain: Path Engine (Planned)

The **Path Engine** is a planned module designed to compute safe travel paths for mobile cranes moving across the construction site.

---

## 1. Module Responsibility

Mobile cranes require significant horizontal clearance and stable ground when traveling:
* **Site Traversability Mapping:** Subdividing the site boundary into grid cells, identifying setup areas, delivery zones, storage yards, and obstacle spaces.
* **Travel Route Planning:** Finding a collision-free pathway for the crane to drive from site entrance to optimal setup coordinate.
* **Kinematic Constraints Validation:** Verifying that paths respect the crane's width, turning radius, and maximum ground slope capabilities.

---

## 2. Planned Data Structures

We plan to implement a spatial grid mapping coordinate layer:

```csharp
public class TraversableGridCell
{
    public double X { get; set; }
    public double Y { get; set; }
    public bool IsTraversable { get; set; }
    public double MaxBearingCapacity { get; set; } // Soil bearing support metric
    public double GroundSlope { get; set; }
}
```

---

## 3. Planned Routing Algorithm

* **Grid Generation:** Generate a 2D grid based on the site boundary points and obstacle polygons.
* **A* Pathfinding:** Search the grid from start $(X_{\text{start}}, Y_{\text{start}})$ to optimized solver coordinates $(X_{\text{opt}}, Y_{\text{opt}})$.
* **Constraint Filtering:** Filter search nodes if they overlap obstacles, cross unsafe ground, or exceed the maximum allowed slope.
* **Turning Radius Smoothing:** Run a Dubins path or coordinate smoothing sweep to ensure the crane's physical chassis can execute the turns.
