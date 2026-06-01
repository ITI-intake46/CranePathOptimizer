# Domain: Lifting Engine (Planned)

The **Lifting Engine** is a planned module designed to verify the physical safety and clearance clearances of crane lifts throughout the erection process.

---

## 1. Module Responsibility

While the current MVP limits lifting evaluation to simple load-chart capacity checks, the full Lifting Engine will perform dynamic spatial validation:
* **Sequence-Based Obstacle Modeling:** As elements are erected (in the order specified by `SortOrder`), they become active 3D obstacles for subsequent lifts.
* **Boom & Jib Clearance Verification:** Checking if the physical crane boom or jib collides with already-installed precast pieces during slewing or trolley movement.
* **Hook Line Collision Checking:** Checking the hoist rope trajectory to ensure it does not scrape or catch against structural components.

---

## 2. Planned Data Structures

To support dynamic clearances, we plan to implement the following entities:

```csharp
public class LiftSequenceStep
{
    public int StepNumber { get; set; }
    public ElementRecord TargetElement { get; set; }
    public double PickRadius { get; set; }
    public double PlaceRadius { get; set; }
    public double RequiredClearance { get; set; }
    public List<Point3D> TravelTrajectory { get; set; } = new();
}
```

---

## 3. Planned Trajectory Algorithm

1. **Start Coordinate:** Crane pickup point (trailer route location).
2. **Intermediate Waypoints:** Hoisting the precast element above the maximum height of any obstacle in the horizontal path.
3. **End Coordinate:** Installation coordinate $(X, Y, Z)$ parsed from the IFC file.
4. **Collision Sweep:** Defining a 3D bounding prism representing the load + rigging lines, and checking for intersections with the boundary volumes of existing structures.
