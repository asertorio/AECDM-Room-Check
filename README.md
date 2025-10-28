# Electrical Room Check (for Claude)

This Claude custom skill analyzes spatial containment of elements (e.g. *electrical equipment*) within rooms using 3D **Axis-Aligned Bounding Boxes (AABB)**, and visualizes results in the **Autodesk Platform Services (APS) Viewer**.

---

## 🧠 Overview

This skill lets Claude automatically:
1. Retrieve **AEC Data Model** elements (Rooms + Electrical Equipment)
2. Compute **containment relationships** via Python AABB analysis
3. Generate a JSON schedule of results
4. Load the schedule in an **interactive APS Viewer** to visualize which elements are within each room.

It’s designed for **AEC workflows** where users want to explore room-by-room electrical layouts.

---

## 📦 File Structure

| File | Purpose |
|------|----------|
| `SKILL.md` | Claude skill manifest (metadata, workflow, and prompts) |
| `spatial_analysis_bbox.py` | Performs AABB-based containment analysis |
| `viewer_room_filter.html` | APS Viewer UI for visualization |
| `README.md` | This guide |

---

## ⚙️ Prerequisites

Before using this skill:
1. You must have **Claude Pro** or **Claude for Work** access with **custom skills enabled**.  
   👉 See: [How to create custom skills](https://support.claude.com/en/articles/12512198-how-to-create-custom-skills)
2. Ensure you have:
   - Autodesk account and API credentials (client ID & secret)

---

## 🧩 Step 1 — Add the Skill to Claude

1. Go to **Settings → Custom Skills → New Skill**.  
2. Upload the following:
   - **Manifest**: `SKILL.md`
   - **Python File**: `spatial_analysis_bbox.py`
   - **Viewer File** (optional reference): `viewer_room_filter.html`
3. Save and name your skill something like `Electrical Room Schedule`.

---

## 🧠 Step 2 — How It Works

### 1. Retrieve Model Data
Claude uses AECDM API endpoints (via skill blocks such as `aecdm:GetElementsByElementGroupWithCategoryFilter`) to fetch:
- Room geometry (`category: "Rooms"`)
- Electrical equipment (`category: "'Electrical Equipment'"`)

Other categories could be swapped in for Electrical Equipment.
> Note: Category names containing spaces must be wrapped in single quotes.

### 2. Run Containment Analysis
Claude executes `spatial_analysis_bbox.py`, which:
- Derives bounding boxes from AECDM geometry
- Checks which elements are *fully* or *partially* contained in each room
- Produces a structured JSON result (`containment_schedule.json`)

### 3. Visualize in APS Viewer
Open `viewer_room_filter.html` in a browser and:
1. Paste your **Access Token**  
2. Paste your **Model URN (base64)**  
3. Upload `containment_schedule.json`

Then:
- Select a room → isolates contained elements in the 3D viewer
- Click **Show All** → resets the model view

---

## 📊 Step 3 — Example Output

**Containment Schedule JSON**
```json
[
  {
    "room_name": "Room 101",
    "element_name": "Panel A",
    "containment_type": "FullyContained",
    "externalId": "abc123"
  },
  {
    "room_name": "Room 101",
    "element_name": "Light 02",
    "containment_type": "PartiallyContained",
    "externalId": "def456"
  }
]
```

**Example Workflow**

```bash
python spatial_analysis_bbox.py
# -> Generates containment_schedule.json
open viewer_room_filter.html
# -> Paste token & URN, upload JSON, explore model
```

---

## 🌐 Step 4 — Running Inside Claude

Once installed:
1. Say:  
   **“Run room containment for electrical equipment in project Alpha.”**
2. Claude will:
   - Authenticate via AECDM APIs  
   - Retrieve elements  
   - Run AABB analysis  
   - Produce a schedule and viewer link  
3. You can follow up with:  
   **“Show me Room 204.”**  
   → Claude will filter visualization to that room.

---

## 🧩 Advanced Integration

You can extend this skill by:
- Adding new category filters (e.g., HVAC, Plumbing)
- Generating XLSX summaries via Claude skill block `xlsx:CreateWorkbook`
- Combining with **ACC Connect**, **Power Automate**, or **Workato** for automated reports

---

## ⚠️ Notes & Limitations

- Bounding box containment is **approximate** — for precision, extend with mesh intersection logic.
- Viewer must run under **HTTPS** to load Autodesk CDN libraries.
- Model and analysis data must share the same coordinate system (ElementGroup).
- Large models may require pre-filtering for performance.

---

## 🧾 Licensing & Attribution

- APS Viewer © Autodesk, used under [Developer API Terms](https://aps.autodesk.com/terms)
- This example code is provided for educational purposes — modify freely.

- Great work done by Autodesk Developer Advocated:
   - Zhong Wu to get the room comparison feature with the AECDM working:
https://github.com/JohnOnSoftware/aps-aecdm-mcp-dotnet/blob/main/mcp-server-aecdm/AECDMTools.cs#L583

   - Zhong Wu & Joao Martins: The AEC Data Model .NET MCP https://github.com/autodesk-platform-services/aps-aecdm-mcp-dotnet/tree/main
   
---

## ✅ Quick Start Summary

| Step | Action |
|------|--------|
| 1️⃣ | Upload skill files into Claude as a zip file |
| 2️⃣ | Run skill → generates `containment_schedule.json` |
| 3️⃣ | Open `viewer_room_filter.html` |
| 4️⃣ | Enter token & URN, upload JSON |
| 5️⃣ | Explore room-by-room element isolation |

