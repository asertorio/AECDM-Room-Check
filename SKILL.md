---
name: electrical-room-schedule
description: Analyze and schedule electrical elements by room location in Autodesk AEC Data Model projects. Use when users request electrical equipment schedules, room-by-room electrical inventories, or spatial analysis of electrical elements within rooms. Works with ACC projects and requires AECDM MCP tools.
---

## Overview

This skill determines which elements (e.g., electrical equipment) are located within each room of a model by comparing **3D axis-aligned bounding boxes (AABBs)** and then allows users to visualize the results in the **Autodesk Platform Services (APS) Viewer**.

---

## Workflow

### Step 1: Authentication and Project Selection
1. `aecdm:GetToken`
2. `aecdm:GetHubs`
3. `aecdm:GetProjects` (with hub ID)
4. Choose the target project.

### Step 2: Select the Model (ElementGroup)
1. `aecdm:GetElementGroupsByProject` (project ID)
2. User selects which ElementGroup (model) to analyze.
3. Record the ElementGroup ID for subsequent calls.

### Step 3: Retrieve Candidate Elements (Electrical)
Fetch electrical elements from the selected model:

```python
aecdm:GetElementsByElementGroupWithCategoryFilter
  elementGroupId: <selected group ID>
  category: "'Electrical Equipment'"
```
Note the electrical Equipment has been wrapped in singe quotes becasuse of the space in the category name.
Store as `electrical_elements`.

### Step 4: Retrieve Room Elements
Fetch rooms or spaces from the same model:

```python
aecdm:GetElementsByElementGroupWithCategoryFilter
  elementGroupId: <selected group ID>
  category: "Rooms"  # fallback: "Spaces", "Areas"
```

Store as `room_elements`.

### Step 5: Perform 3D AABB Containment

Use the Python module `spatial_analysis_bbox.py` to classify which elements fall within which rooms.

```python
from spatial_analysis_bbox import (
    analyze_containment_by_bboxes,
    format_text_report,
    format_schedule_data,
    compute_bbox_from_element,
)

room_bboxes = []
for room in room_elements:
    bb = compute_bbox_from_element(room)
    if bb:
        room_bboxes.append((room, bb))

all_results = []
for room, _ in room_bboxes:
    analysis = analyze_containment_by_bboxes(
        container_element=room,
        candidate_elements=electrical_elements,
        eps=1e-4
    )
    all_results.append(analysis)

# Generate human-readable report
for analysis in all_results:
    print(format_text_report(analysis))

# Export combined schedule for visualization
combined_schedule = []
for analysis in all_results:
    combined_schedule.extend(
        format_schedule_data(analysis, include_partial=True)
    )

# Save to JSON for viewer use
import json
with open("containment_schedule.json", "w") as f:
    json.dump(combined_schedule, f, indent=2)
```

---

### Step 6: Interactive Visualization in APS Viewer

After the 3D containment results are generated, visualize them interactively using the **APS Viewer**.

#### 1. Open the Viewer
Use the provided file `viewer_room_filter.html`.

#### 2. Provide Required Inputs
In the toolbar:
- **Access Token** → Paste a valid APS 2-legged token with `viewables:read` scope.
- **Model URN (base64)** → Provide the base64-encoded URN for the model's derivative manifest.
- **Upload JSON** → Upload `containment_schedule.json` created in Step 5.

#### 3. Explore Rooms
Once the data loads:
- The **room list** dropdown will be populated automatically.
- Choose a room and click **Show Room** → isolates and zooms to all elements within that room.
- Click **Show All** → resets the view to the full model.

#### 4. Behavior
- Uses `model.getExternalIdMapping()` to map `externalId` → `dbId`.
- Calls `viewer.isolate()` and `viewer.fitToView()` for the filtered element set.
- Works fully offline once viewer scripts are cached, requiring only token and URN access.

#### 5. Example Usage
```bash
1. Run the AABB analysis → outputs containment_schedule.json
2. Open viewer_room_filter.html in browser
3. Enter token + URN
4. Upload JSON file
5. Select "Room 101" → isolates all electrical equipment contained in Room 101
```

#### 6. Integration in Claude Skill
When this viewer is embedded in a Claude skill:
- The `containment_schedule.json` file can be generated dynamically from prior skill steps.
- The skill can then render or open the viewer page with the data preloaded.
- User can interactively navigate by saying “Show me Room X”.

---

## Example Schedule Output

```json
[
  {"room_name": "Room 101", "element_name": "Panel A", "containment_type": "FullyContained", "externalId": "abc123"},
  {"room_name": "Room 101", "element_name": "Light 02", "containment_type": "PartiallyContained", "externalId": "def456"}
]
```

---

## Visualization Options

- **APS Viewer (HTML)** → Load `viewer_room_filter.html` and isolate elements visually.
- **Spreadsheet Export** → Use `xlsx:CreateWorkbook` to produce room schedules.
- **Highlight / Isolate Elements** → Works via externalId mapping in APS Viewer.
- **Full 3D Review** → Combine room-level containment with visual QA.

---

## Limitations

- Bounding boxes are **approximate**; add a mesh-intersection pass for higher precision.
- The viewer must load via HTTPS to access Autodesk CDN libraries.
- Ensure all elements and rooms share the same coordinate system (ElementGroup).

---

## Summary

This version of the **Electrical Room Schedule** skill links automated containment analysis with an interactive model viewer.  
It enables users to:
- Compute 3D containment relationships with AABBs.
- Export and visualize results in the APS Viewer.
- Interactively explore “Room → Elements” relationships within Claude.

