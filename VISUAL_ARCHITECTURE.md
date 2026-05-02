# Visual Architecture & Diagrams

## System Architecture

```
┌─────────────────────────────────────────────────────────────────┐
│                        MAIN APPLICATION                          │
├─────────────────────────────────────────────────────────────────┤
│                                                                   │
│  ┌────────────────────────────────────────────────────────────┐  │
│  │  MainViewModel                                             │  │
│  │  ├─ CraneSetup (ViewModel)                                │  │
│  │  ├─ OptimizeCommand → OpenOptimizerDialog()              │  │
│  │  └─ [Scene, Drawing, IFCViewer, etc.]                    │  │
│  └────────────────────────────────────────────────────────────┘  │
│           │                              │                        │
│           ├─ passes CraneSetup          │                        │
│           │                              │                        │
│           ▼                              ▼                        │
│  ┌─────────────────────────┐  ┌─────────────────────────────┐   │
│  │  CranesTabView          │  │  CraneOptimizerDialog       │   │
│  │  (UI - XAML/CodeBehind) │  │  (UI - Window)              │   │
│  │                          │  │                             │   │
│  │  ├─ Crane Picker        │  │  ├─ Results List           │   │
│  │  ├─ Zone Indicator      │  │  ├─ Point Preview          │   │
│  │  ├─ Lock Indicator 🔒   │  │  ├─ [Apply Button]         │   │
│  │  ├─ Unlock Button       │  │  └─ Stats Display          │   │
│  │  └─ Drag Handling       │  │                             │   │
│  └─────────────────────────┘  └─────────────────────────────┘   │
│           │                              │                        │
│           └──────────────┬───────────────┘                        │
│                          │                                         │
│                          ▼                                         │
│  ┌─────────────────────────────────────────────────────────────┐  │
│  │  CraneSetupViewModel                                        │  │
│  │  ├─ CurrentPosition: DomainPoint3D                         │  │
│  │  ├─ IsPositionLocked: bool ─────────► UI Binding          │  │
│  │  ├─ IsPositionValid: bool                                  │  │
│  │  ├─ IsDraggingActive: bool                                 │  │
│  │  │                                                          │  │
│  │  ├─ BeginDrag()           [Check IsPositionLocked]         │  │
│  │  ├─ UpdateDrag()          [Validate zone]                  │  │
│  │  ├─ EndDrag()             [Finalize position]              │  │
│  │  │                                                          │  │
│  │  ├─ LockPosition()        [Called by Apply button]         │  │
│  │  └─ UnlockPosition()      [Called by Unlock button]        │  │
│  └─────────────────────────────────────────────────────────────┘  │
│           │                                                        │
│           ├─ Rendering Service (3D updates)                       │
│           ├─ Scene Service (model/elements)                       │
│           └─ Zone Constraint (validation)                         │
│                                                                    │
└─────────────────────────────────────────────────────────────────┘
```

---

## State Machine Diagram

```
                    ┌──────────────────┐
                    │  NO CRANE        │
                    │  SELECTED        │
                    └────────┬─────────┘
                             │ SelectedCrane = value
                             │ (from dropdown)
                             ▼
                    ┌──────────────────┐
                    │  CRANE LOADED    │
                    │  Position: (0,0) │
                    │  Locked: false   │
                    └────────┬─────────┘
                             │ BeginDrag()
                             │ [Check: not locked]
                             │ ✓ Allowed
                             ▼
                    ┌──────────────────┐
                    │  DRAGGING        │
                    │  Real-time pos   │
                    │  Zone feedback   │
                    └────────┬─────────┘
                             │ EndDrag()
                             │ Zone clamp
                             ▼
                    ┌──────────────────┐
                    │  POSITIONED      │
                    │  Custom location │
                    │  Locked: false   │
                    └────────┬─────────┘
                             │ RunOptimizer()
                             │ (GA analyzes)
                             ▼
                    ┌──────────────────┐
                    │  OPTIMIZED       │
                    │  Results ready   │
                    │  Preview mode    │
                    └────────┬─────────┘
                             │ Apply
                             │ LockPosition(x,y)
                             ▼
                    ┌──────────────────┐
                    │  LOCKED 🔒       │
                    │  GA position     │
                    │  No drag allowed │
                    └────────┬─────────┘
                             │ UnlockPosition()
                             │ OR
                             │ Select new crane
                             ▼
                    ┌──────────────────┐
                    │  POSITIONED      │
                    │  (back to drag)  │
                    │  Locked: false   │
                    └──────────────────┘
```

---

## Data Flow Diagram

```
┌─────────────────────────────────────────────────────────────────┐
│                    USER INTERACTIONS                             │
├─────────────────────────────────────────────────────────────────┤
│                                                                   │
│  DRAG ACTION (Unlocked)                                          │
│  ├─ MouseDown → BeginDrag()                                     │
│  │  └─ Check: !IsPositionLocked → ✓ Start                       │
│  │                                                               │
│  ├─ MouseMove → UpdateDrag(worldPoint)                          │
│  │  ├─ Validate zone → IsPositionValid                          │
│  │  ├─ Clamp to zone                                            │
│  │  └─ MoveCraneVisualTo(point) → 3D update                     │
│  │                                                               │
│  └─ MouseUp → EndDrag()                                         │
│     └─ Finalize position                                        │
│                                                                   │
│  OPTIMIZE ACTION                                                │
│  ├─ Click "Run Optimizer"                                       │
│  │  └─ CraneOptimizerDialog opens                               │
│  │     └─ RunOptimizationAsync() → GA service                   │
│  │        └─ Returns CranePlacementResult                       │
│  │                                                               │
│  ├─ Review results                                              │
│  │  └─ Select point from PointsList                             │
│  │     └─ ShowPoint() preview                                   │
│  │                                                               │
│  └─ Click "Apply"                                               │
│     └─ BtnApply_Click()                                         │
│        ├─ Get selected point coords                             │
│        ├─ Create DomainPoint3D(x, y, 0)                         │
│        └─ CraneSetup.LockPosition(point)                        │
│           ├─ Set CurrentPosition                                │
│           ├─ MoveCraneVisualTo(point)                           │
│           ├─ IsPositionLocked = true                            │
│           └─ IsDraggingActive = false                           │
│                                                                   │
│  UNLOCK ACTION                                                  │
│  ├─ Click "Unlock Position" button                              │
│  │  └─ UnlockPositionCommand.Execute()                          │
│  │     └─ UnlockPosition()                                      │
│  │        └─ IsPositionLocked = false                           │
│  │                                                               │
│  └─ Can now drag again                                          │
│                                                                   │
│  NEW CRANE ACTION                                               │
│  ├─ Select different crane                                      │
│  │  └─ OnCraneChanged()                                         │
│  │     ├─ IsPositionLocked = false (Auto-unlock)               │
│  │     ├─ Rebuild crane visual                                  │
│  │     └─ Reset position to (0,0)                               │
│  │                                                               │
│  └─ Ready to position new crane                                 │
│                                                                   │
└─────────────────────────────────────────────────────────────────┘
        │                    │                    │
        ▼                    ▼                    ▼
    ┌────────┐          ┌────────┐         ┌────────┐
    │ ViewM  │          │ Dialog │         │ Render │
    │  Update│          │ Result │         │ Service│
    └────────┘          └────────┘         └────────┘
```

---

## Component Interaction Diagram

```
                          ┌─────────────────────────┐
                          │  CraneOptimizerDialog   │
                          │                         │
                          │  [Apply Button] ────────┼─► Get SelectedPoint
                          │     │                   │
                          │     └──────┬────────────┤
                          │            │            │
                          └────────────┼────────────┘
                                       │
                                       │ SelectedPoint Data
                                       │ (X, Y coordinates)
                                       ▼
        ┌──────────────────────────────────────────────────────┐
        │         Create DomainPoint3D(X, Y, 0)                │
        │                     │                                │
        │                     ▼                                │
        │      ┌────────────────────────────────┐             │
        │      │ CraneSetup.LockPosition(point) │             │
        │      └────────────────────────────────┘             │
        └──────────────────────────────────────────────────────┘
                                │
                ┌───────────────┼───────────────┐
                │               │               │
                ▼               ▼               ▼
        ┌──────────────┐ ┌──────────────┐ ┌──────────────┐
        │ Set          │ │ Visual       │ │ Flag         │
        │ CurrentPos   │ │ Transform    │ │ IsLocked=T   │
        │              │ │              │ │              │
        │ Position =   │ │ Movement to  │ │ Prevents     │
        │ locked_point │ │ new coords   │ │ drag starts  │
        └──────┬───────┘ └──────┬───────┘ └──────┬───────┘
               │                │                │
               └────────────────┼────────────────┘
                                │
                    ┌───────────┴───────────┐
                    │                       │
                    ▼                       ▼
            ┌──────────────┐        ┌──────────────┐
            │   XAML       │        │   ViewModel  │
            │   Binding    │        │   Property   │
            │   Updates    │        │   Changed    │
            └──────────────┘        └──────────────┘
                    │                       │
        ┌───────────┴────────────┬──────────┴──────────┐
        │                        │                     │
        ▼                        ▼                     ▼
    ┌────────┐         ┌────────────────┐      ┌─────────┐
    │Lock    │         │Unlock Btn      │      │Zone     │
    │Ind 🔒  │         │Enabled=true    │      │Status   │
    │Orange  │         │                │      │Green/Red│
    │banner  │         │                │      │         │
    └────────┘         └────────────────┘      └─────────┘
```

---

## Sequence Diagram: Lock Flow

```
Actor         CraneOptimizerDialog    CraneSetupViewModel    XAML Binding
  │                    │                       │                    │
  │                    │                       │                    │
  ├─ Click Apply ─────►│                       │                    │
  │                    │                       │                    │
  │                    ├─ Get SelectedPoint    │                    │
  │                    │ (X,Y from GA result)  │                    │
  │                    │                       │                    │
  │                    ├─ Create Point3D(X,Y)  │                    │
  │                    │                       │                    │
  │                    ├─ LockPosition(point) ─────────────────────►│
  │                    │                       │                    │
  │                    │                   ┌──┴─────────────────┐   │
  │                    │                   │ Set property:      │   │
  │                    │                   │ IsPositionLocked=T │   │
  │                    │                   │ CurrentPosition=pt │   │
  │                    │                   └──┬─────────────────┘   │
  │                    │                       │                    │
  │                    │                       ├─ OnPropertyChanged()
  │                    │                       │    ('IsPositionLocked')
  │                    │                       │                    │
  │                    │                       │    Update Binding ─┤
  │                    │                       │    Lock Indicator  │
  │                    │                       │    Unlock Btn Ena  │
  │                    │                       │                    │
  │                    │◄─────────────────────────┐                 │
  │                    │ (Return from method)     │                 │
  │                    │                       │                    │
  │                    ├─ Close Dialog                              │
  │                    │                       │                    │
  │◄─ Dialog Closed    │                       │                    │
  │                    │                       │                    │
  └────────────────────►(User cannot drag)     │                    │
                       (Position Locked!)      │                    │
```

---

## Sequence Diagram: Unlock Flow

```
Actor         CranesTabView          CraneSetupViewModel    XAML Binding
  │               │                       │                    │
  │               │                       │                    │
  ├─ Click ──────►│ UnlockPositionCommand │                    │
  │ Unlock        │ Execute()             │                    │
  │               │                       │                    │
  │               ├─ UnlockPosition() ───►│                    │
  │               │                       │                    │
  │               │                   ┌──┴─────────────────┐   │
  │               │                   │ Set property:      │   │
  │               │                   │ IsPositionLocked=F │   │
  │               │                   └──┬─────────────────┘   │
  │               │                       │                    │
  │               │                       ├─ OnPropertyChanged()
  │               │                       │    ('IsPositionLocked')
  │               │                       │                    │
  │               │                       │    Update Binding ─┤
  │               │                       │    Hide Indicator  │
  │               │                       │    Disable Unlock  │
  │               │                       │                    │
  │               │◄─────────────────────────┐                 │
  │               │ (Return from method)     │                 │
  │               │                       │                    │
  │◄─────────────────────────────────────────┘                 │
  │ (Can now drag)                          │                    │
  │
  └────────────────────────────►(User can drag again)
                                (Position Unlocked!)
```

---

## UI State Transitions

```
┌──────────────────────────────────────────────────────────┐
│               CRANE PICKER POPUP                         │
├──────────────────────────────────────────────────────────┤
│                                                          │
│  UNLOCKED STATE:                                         │
│  ┌──────────────────────────────────────────────────┐   │
│  │ Select a Crane                                   │   │
│  │ ┌────────────────────────────────────────────┐  │   │
│  │ │ Crane List [Scrollable]                    │  │   │
│  │ │  🏗 Crane A (Mfr · Type)                  │  │   │
│  │ │  🏗 Crane B (Mfr · Type)                  │  │   │
│  │ │  🏗 Crane C (Mfr · Type) ← Selected       │  │   │
│  │ └────────────────────────────────────────────┘  │   │
│  │ ┌────────────────────────────────────────────┐  │   │
│  │ │ ✅ Inside Zone                             │  │   │
│  │ └────────────────────────────────────────────┘  │   │
│  │ ───────────────────────────────────────────     │   │
│  │ Active: Crane C                                 │   │
│  └──────────────────────────────────────────────────┘   │
│                                                          │
│  LOCKED STATE:                                           │
│  ┌──────────────────────────────────────────────────┐   │
│  │ Select a Crane                                   │   │
│  │ ┌────────────────────────────────────────────┐  │   │
│  │ │ Crane List [Scrollable]                    │  │   │
│  │ │  🏗 Crane A (Mfr · Type)                  │  │   │
│  │ │  🏗 Crane B (Mfr · Type)                  │  │   │
│  │ │  🏗 Crane C (Mfr · Type) ← Selected       │  │   │
│  │ └────────────────────────────────────────────┘  │   │
│  │ ┌────────────────────────────────────────────┐  │   │
│  │ │ ✅ Inside Zone                             │  │   │
│  │ └────────────────────────────────────────────┘  │   │
│  │ ┌────────────────────────────────────────────┐  │   │
│  │ │ 🔒 Position Locked (GA Optimized)         │  │   │ ◄── Orange
│  │ └────────────────────────────────────────────┘  │   │      Background
│  │ ───────────────────────────────────────────     │   │
│  │ Active: Crane C                                 │   │
│  └──────────────────────────────────────────────────┘   │
│                                                          │
└──────────────────────────────────────────────────────────┘
```

---

## 3D Viewport Interaction

```
┌─────────────────────────────────────────────────┐
│            3D VIEWPORT                           │
├─────────────────────────────────────────────────┤
│                                                 │
│  ┌─────────────────────────────────────────┐   │
│  │  Building Model (IFC Elements)          │   │
│  │  ┌─────────────────────────────────────┐│   │
│  │  │  [Precast Elements]                 ││   │
│  │  │  ├─ Element 1 (Liftable)  ✅        ││   │
│  │  │  ├─ Element 2 (Overweight) ❌       ││   │
│  │  │  └─ Element 3 (OutOfRadius) ❌      ││   │
│  │  └─────────────────────────────────────┘│   │
│  │                                         │   │
│  │      🏗 [CRANE] ◄─ Draggable            │   │
│  │      (when unlocked)                    │   │
│  │      │                                  │   │
│  │      ├─ Current position: (45.2, 32.1) │   │
│  │      ├─ Can drag to new position       │   │
│  │      └─ Zone validation: Green ✅      │   │
│  │                                         │   │
│  │  [Green Zone] ─────────┐                │   │
│  │  ┌─────────────────────┴─────┐          │   │
│  │  │  Valid placement area     │          │   │
│  │  │  ├─ Covers all elements   │          │   │
│  │  │  └─ Best positions inside │          │   │
│  │  └───────────────────────────┘          │   │
│  └─────────────────────────────────────────┘   │
│                                                 │
│  STATUS INDICATORS:                             │
│  ├─ Zone Status: ✅ Green (Inside)              │
│  ├─ Drag Status: ❌ Cannot Drag (Locked)       │
│  ├─ Lock Status: 🔒 Position Locked            │
│  └─ Position: (45.2, 32.1, 0.0)                │
│                                                 │
└─────────────────────────────────────────────────┘
```

---

## Memory & Performance

```
┌─────────────────────────────────────────────────┐
│          MEMORY ALLOCATION                       │
├─────────────────────────────────────────────────┤
│                                                 │
│  CraneSetupViewModel Fields:                   │
│  ├─ _craneVisual: ModelVisual3D                │
│  │  └─ Reference to 3D visual object           │
│  │  └─ Size: 8 bytes (pointer)                 │
│  │                                             │
│  ├─ _isDragging: bool                          │
│  │  └─ Size: 1 byte                            │
│  │                                             │
│  ├─ _isPositionLocked: bool                    │
│  │  └─ Size: 1 byte ◄─── NEW                   │
│  │                                             │
│  ├─ _lastValidPosition: DomainPoint3D          │
│  │  └─ (X, Y, Z as doubles)                    │
│  │  └─ Size: 24 bytes                          │
│  │                                             │
│  ├─ _currentPosition: DomainPoint3D?           │
│  │  └─ Size: 24 bytes                          │
│  │                                             │
│  └─ Other properties: ~60 bytes                │
│                                                │
│  TOTAL OVERHEAD: ~2 bytes per crane            │
│                                                │
└─────────────────────────────────────────────────┘


┌─────────────────────────────────────────────────┐
│          PERFORMANCE METRICS                     │
├─────────────────────────────────────────────────┤
│                                                 │
│  Drag Operation:                                │
│  ├─ BeginDrag(): O(1) - Simple flag check      │
│  ├─ UpdateDrag(): O(n) - Where n = zones      │
│  ├─ EndDrag(): O(1) - Finalize position      │
│  └─ Total: < 1ms per frame                     │
│                                                │
│  Lock Operation:                                │
│  ├─ LockPosition(): O(1) - Property set       │
│  ├─ Visual update: O(1) - Transform only      │
│  └─ Total: < 1ms                              │
│                                                │
│  Unlock Operation:                              │
│  ├─ UnlockPosition(): O(1) - Property set     │
│  └─ Total: < 1ms                              │
│                                                │
│  No impact on:                                  │
│  ├─ 3D mesh rendering                          │
│  ├─ GA optimization                            │
│  └─ Overall application performance            │
│                                                │
└─────────────────────────────────────────────────┘
```

---

## This completes the visual architecture documentation.
