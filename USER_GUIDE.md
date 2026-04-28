# Crane Dragging & Locking Feature - User Guide

## Feature Overview

This feature enables a complete crane positioning workflow:

```
┌─────────────────────────────────────────────────────────────┐
│  CRANE POSITIONING WORKFLOW                                 │
└─────────────────────────────────────────────────────────────┘

1. SELECT CRANE
   └─> Choose from database

2. DRAG TO POSITION (Unlocked)
   └─> Click & drag crane
   └─> Real-time zone validation
       ├─ Green = Valid position
       └─ Red = Outside zone

3. RUN OPTIMIZATION
   └─> Click "Run Optimizer"
   └─> GA analyzes placements
   └─> Shows best positions

4. APPLY RESULT (Locks Position)
   └─> Select optimal point
   └─> Click "Apply"
   └─> Position locked 🔒

5. RE-POSITION IF NEEDED
   └─> Click "Unlock Position"
   └─> Can now drag again
   └─> Or run optimizer again
```

## UI Components

### Crane Picker Popup

**When Unlocked:**
```
┌─────────────────────────────┐
│  Select a Crane             │
│  ┌─────────────────────────┐│
│  │ 🏗 Crane Name           ││
│  │  Manufacturer · Type    ││
│  └─────────────────────────┘│
│  ┌─────────────────────────┐│
│  │ ✅ Inside Zone           ││ ← Green when valid
│  └─────────────────────────┘│
│  ─────────────────────────── │
│  Active: CraneX              │
└─────────────────────────────┘
```

**When Locked:**
```
┌─────────────────────────────┐
│  Select a Crane             │
│  ┌─────────────────────────┐│
│  │ 🏗 Crane Name           ││
│  │  Manufacturer · Type    ││
│  └─────────────────────────┘│
│  ┌─────────────────────────┐│
│  │ ✅ Inside Zone           ││
│  └─────────────────────────┘│
│  ┌─────────────────────────┐│
│  │ 🔒 Position Locked       ││ ← Orange indicator
│  │    (GA Optimized)        ││
│  └─────────────────────────┘│
│  ─────────────────────────── │
│  Active: CraneX              │
└─────────────────────────────┘
```

### MANAGE Section

```
┌────────────────────────────┐
│  MANAGE                    │
├────────────────────────────┤
│  [✖ Remove Selected Crane] │
│  [🗑 Remove All Cranes]    │
│  [🔓 Unlock Position]      │  ← Active when locked
│  [◯ Show Swing Radius]    │
│  [◯ Show Capacity Rings]   │
└────────────────────────────┘
```

## State Diagram

```
                    ┌─────────────────┐
                    │   No Crane      │
                    │   Selected      │
                    └────────┬────────┘
                             │ Select crane
                             ▼
                    ┌─────────────────┐
                    │   UNLOCKED      │
                    │                 │
                    │  - Draggable    │
                    │  - Positionable │
                    └────────┬────────┘
                             │ Run GA & Apply
                             ▼
                    ┌─────────────────┐
                    │   LOCKED        │
                    │  (GA Result)    │
                    │                 │
                    │  - Not dragable │
                    │  - Position set │
                    └────────┬────────┘
                             │ Click "Unlock Position"
                             ▼ or Select new crane
                    ┌─────────────────┐
                    │   UNLOCKED      │
                    │   (Resume drag) │
                    └─────────────────┘
```

## Visual Feedback

### Zone Validation (During Drag)

**Inside Zone (Valid):**
```
┌────────────────────┐
│ ✅ Inside Zone     │  ← Green background
│  Position Valid    │
└────────────────────┘
```

**Outside Zone (Invalid):**
```
┌────────────────────┐
│ ❌ Outside Zone    │  ← Red background
│  Position Clamped  │
└────────────────────┘
```

### Lock Status (When Locked)

```
┌────────────────────┐
│ 🔒 Position Locked │  ← Orange background
│ (GA Optimized)     │    Shows in popup
└────────────────────┘
```

## Step-by-Step Usage

### Step 1: Select and Position Crane (Pre-Optimization)

```
1. Open F3_CraneSetup tab
2. Click "MOBILE CRANE" button
3. Select crane from database
4. Move mouse over 3D viewport
5. Click on crane model to start dragging
6. Drag to desired position
7. Release mouse to finalize
8. Check zone indicator:
   - ✅ Green = Ready for analysis
   - ❌ Red = Adjust position
```

### Step 2: Run Genetic Algorithm

```
1. Click "Run Optimizer" button in OPTIMIZE section
2. Optimizer dialog opens
3. Select crane from dropdown (if not already selected)
4. Configure GA parameters:
   - Population Size (default 30)
   - Max Generations (default 30)
   - Mutation Rate (default 0.1)
5. Click "Run" button
6. Monitor progress in status bar
7. Results display when complete
```

### Step 3: Review and Apply Results

```
1. View optimization results:
   - Fitness score
   - Coverage ratio
   - List of candidate positions
2. Click on different points to preview
3. Verify which point works best
4. Click "Apply" to accept
   ⚠️  This LOCKS the position!
```

### Step 4: Position is Now Locked

```
✅ Position locked to GA result
✅ 🔒 indicator shows in crane picker
✅ Drag is disabled
✅ Can still view but not reposition
```

### Step 5: To Resume Dragging

```
Option A: Click "Unlock Position" button
   └─> Returns to dragging mode
   └─> Can re-run optimizer if needed

Option B: Select different crane
   └─> Automatically unlocks current crane
   └─> New crane ready to position
```

## Coordinate System

- **X-Y Plane:** Ground plane coordinates
  - X: Easting (left-right)
  - Y: Northing (front-back)
  - Z: Elevation (up-down)

- **Position Format:** `(X, Y, 0)`
  - Ground level: Z=0
  - Crane base sits on ground
  - GA results automatically converted

## Keyboard Shortcuts (If Implemented)

*To be configured in ApplicationSettings:*
- `Ctrl+L` → Lock/Unlock position toggle
- `Delete` → Remove selected crane
- `Escape` → Cancel ongoing drag

## Troubleshooting

### Position Won't Drag
- ✅ Check if position is locked (🔒 indicator visible)
- ✅ Solution: Click "Unlock Position"
- ✅ Check if a crane is selected
- ✅ Click directly on the crane model in viewport

### Lock Button Disabled
- ✅ Button only enables when position is locked
- ✅ Run optimizer and apply to lock position
- ✅ Or select different crane to reset state

### GA Results Won't Apply
- ✅ Select a point from the results list first
- ✅ Check that point list is populated
- ✅ Verify crane is still selected

## Performance Notes

- **Dragging:** No mesh rebuild (Transform-based = fast)
- **Zone Validation:** Real-time feedback during drag
- **GA Optimization:** Runs on background thread
- **Position Lock:** Instant (no processing needed)

## Future Enhancements

- [ ] Drag history/undo capability
- [ ] Position presets/favorites
- [ ] Multi-crane position locking
- [ ] Auto-save positions
- [ ] Position constraints/snapping
- [ ] Advanced zone visualization during drag
