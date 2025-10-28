"""
Bounding-box (AABB) based spatial containment utilities.

This module derives 3D axis-aligned bounding boxes from AECDM-like element dictionaries
and classifies candidate elements relative to a container AABB as FullyContained/PartiallyContained/Outside.

Supported element shapes for BBox derivation:
- Direct bbox: element["bbox"] = {"min": {"x":..,"y":..,"z":..}, "max": {...}}
- Mesh vertices: element["geometry"]["mesh"]["vertices"] = [{"x":..,"y":..,"z":..}, ...]
- 2D boundary + height: element["room_boundary_2d"] = [{"x":..,"y":..}, ...] + base_elevation + height
- Location + size: element["location"] + element["size"]

Author: ChatGPT
"""

from typing import Dict, Any, List, Optional, Tuple

class BBox3D:
    def __init__(self, minx: float, miny: float, minz: float, maxx: float, maxy: float, maxz: float, *, name: str = ""):
        self.minx = float(minx); self.miny = float(miny); self.minz = float(minz)
        self.maxx = float(maxx); self.maxy = float(maxy); self.maxz = float(maxz)
        if self.minx > self.maxx: self.minx, self.maxx = self.maxx, self.minx
        if self.miny > self.maxy: self.miny, self.maxy = self.maxy, self.miny
        if self.minz > self.maxz: self.minz, self.maxz = self.maxz, self.minz
        self.name = name

    def volume(self) -> float:
        return max(0.0, (self.maxx - self.minx)) * max(0.0, (self.maxy - self.miny)) * max(0.0, (self.maxz - self.minz))

    def to_dict(self) -> Dict[str, float]:
        return {
            "minx": self.minx, "miny": self.miny, "minz": self.minz,
            "maxx": self.maxx, "maxy": self.maxy, "maxz": self.maxz,
            "volume": self.volume(),
            "name": self.name
        }


class ContainmentType:
    FullyContained = "FullyContained"
    PartiallyContained = "PartiallyContained"
    Outside = "Outside"


def _maybe_get(d: Dict[str, Any], *path, default=None):
    cur = d
    for key in path:
        if not isinstance(cur, dict) or key not in cur:
            return default
        cur = cur[key]
    return cur


def _bbox_from_direct_dict(bbox_dict: Dict[str, Any], name: str) -> Optional[BBox3D]:
    if not isinstance(bbox_dict, dict):
        return None
    minp = bbox_dict.get("min") or bbox_dict.get("Min") or {}
    maxp = bbox_dict.get("max") or bbox_dict.get("Max") or {}
    try:
        return BBox3D(minp.get("x"), minp.get("y"), minp.get("z"),
                      maxp.get("x"), maxp.get("y"), maxp.get("z"),
                      name=name)
    except Exception:
        return None


def _bbox_from_mesh_vertices(elem: Dict[str, Any], name: str) -> Optional[BBox3D]:
    verts = _maybe_get(elem, "geometry", "mesh", "vertices") or _maybe_get(elem, "geometry", "vertices")
    if not isinstance(verts, list) or not verts:
        return None
    xs = []; ys = []; zs = []
    for v in verts:
        try:
            xs.append(float(v.get("x", v.get("X"))))
            ys.append(float(v.get("y", v.get("Y"))))
            zs.append(float(v.get("z", v.get("Z"))))
        except Exception:
            continue
    if not xs or not ys or not zs:
        return None
    return BBox3D(min(xs), min(ys), min(zs), max(xs), max(ys), max(zs), name=name)


def _bbox_from_boundary_height(elem: Dict[str, Any], name: str) -> Optional[BBox3D]:
    boundary = elem.get("room_boundary_2d") or elem.get("boundary_2d") or _maybe_get(elem, "room", "boundary_2d")
    if not isinstance(boundary, list) or len(boundary) < 3:
        return None
    xs = []; ys = []
    for p in boundary:
        try:
            xs.append(float(p.get("x", p.get("X"))))
            ys.append(float(p.get("y", p.get("Y"))))
        except Exception:
            continue
    if not xs or not ys:
        return None
    base = elem.get("base_elevation", _maybe_get(elem, "room", "base_elevation"))
    height = elem.get("height", _maybe_get(elem, "room", "height"))
    if base is None or height is None:
        return None
    minz = float(base)
    maxz = float(base) + float(height)
    return BBox3D(min(xs), min(ys), minz, max(xs), max(ys), maxz, name=name)


def _bbox_from_location_size(elem: Dict[str, Any], name: str) -> Optional[BBox3D]:
    loc = elem.get("location"); size = elem.get("size")
    if not isinstance(loc, dict) or not isinstance(size, dict):
        return None
    try:
        cx = float(loc.get("x", loc.get("X"))); cy = float(loc.get("y", loc.get("Y"))); cz = float(loc.get("z", loc.get("Z", 0)))
        sx = abs(float(size.get("x", size.get("X", 0)))); sy = abs(float(size.get("y", size.get("Y", 0)))); sz = abs(float(size.get("z", size.get("Z", 0))))
    except Exception:
        return None
    halfx, halfy, halfz = sx * 0.5, sy * 0.5, sz * 0.5
    return BBox3D(cx - halfx, cy - halfy, cz - halfz, cx + halfx, cy + halfy, cz + halfz, name=name)


def _element_name(elem: Dict[str, Any]) -> str:
    for key in ("name", "element_name", "displayName", "Display Name", "Element Name", "Type Name", "Family and Type"):
        v = elem.get(key)
        if v: return str(v)
    props = _maybe_get(elem, "properties", "results")
    if isinstance(props, list):
        for prop in props:
            n = str(prop.get("name", "")).lower()
            if n in {"name", "element name", "display name", "type name", "family and type"}:
                val = prop.get("value")
                if val: return str(val)
    return str(elem.get("externalId") or elem.get("id") or "Unknown")


def compute_bbox_from_element(element: Dict[str, Any]) -> Optional[BBox3D]:
    name = _element_name(element)
    bb = _bbox_from_direct_dict(element.get("bbox") or element.get("BBox") or element.get("boundingBox") or element.get("BoundingBox") or {}, name)
    if bb: return bb
    bb = _bbox_from_mesh_vertices(element, name)
    if bb: return bb
    bb = _bbox_from_boundary_height(element, name)
    if bb: return bb
    bb = _bbox_from_location_size(element, name)
    if bb: return bb
    return None


def classify_containment_aabb(container: BBox3D, elem: BBox3D, eps: float = 1e-4) -> str:
    inside_x = elem.minx >= container.minx - eps and elem.maxx <= container.maxx + eps
    inside_y = elem.miny >= container.miny - eps and elem.maxy <= container.maxy + eps
    inside_z = elem.minz >= container.minz - eps and elem.maxz <= container.maxz + eps
    if inside_x and inside_y and inside_z:
        return ContainmentType.FullyContained
    overlap_x = elem.maxx >= container.minx - eps and elem.minx <= container.maxx + eps
    overlap_y = elem.maxy >= container.miny - eps and elem.miny <= container.maxy + eps
    overlap_z = elem.maxz >= container.minz - eps and elem.minz <= container.maxz + eps
    if overlap_x and overlap_y and overlap_z:
        return ContainmentType.PartiallyContained
    return ContainmentType.Outside


def analyze_containment_by_bboxes(container_element: Dict[str, Any],
                                  candidate_elements: List[Dict[str, Any]],
                                  *, eps: float = 1e-4) -> Dict[str, Any]:
    results: Dict[str, Any] = {
        "container": None,
        "contained": [],
        "skipped": [],
        "summary": {
            "total_candidates": len(candidate_elements),
            "fully_contained": 0,
            "partially_contained": 0
        },
        "grouped_by_category": {}
    }

    container_bb = compute_bbox_from_element(container_element)
    if not container_bb:
        return {
            "error": "No bounding box could be derived for container element",
            "container_element": container_element
        }

    results["container"] = {
        "name": container_bb.name,
        "bbox": container_bb.to_dict()
    }

    for elem in candidate_elements:
        bb = compute_bbox_from_element(elem)
        if not bb:
            results["skipped"].append({
                "id": elem.get("id") or elem.get("externalId"),
                "name": _element_name(elem),
                "reason": "No geometry/bbox found"
            })
            continue

        ctype = classify_containment_aabb(container_bb, bb, eps=eps)

        if ctype != ContainmentType.Outside:
            cat = elem.get("category") or elem.get("Category") or "Uncategorized"
            rec = {
                "id": elem.get("id") or elem.get("externalId"),
                "externalId": elem.get("externalId"),
                "name": _element_name(elem),
                "category": cat,
                "containment_type": ctype,
                "bbox": bb.to_dict()
            }
            results["contained"].append(rec)

            if ctype == ContainmentType.FullyContained:
                results["summary"]["fully_contained"] += 1
            elif ctype == ContainmentType.PartiallyContained:
                results["summary"]["partially_contained"] += 1

            results["grouped_by_category"].setdefault(cat, []).append(rec)

    return results


def format_text_report(analysis: Dict[str, Any]) -> str:
    if "error" in analysis:
        return f"Error: {analysis['error']}"

    container = analysis["container"]
    summary = analysis["summary"]
    grouped = analysis["grouped_by_category"]

    lines = []
    lines.append("=== SPATIAL CONTAINMENT (AABB) ===")
    lines.append(f"Container: {container['name']}")
    cb = container["bbox"]
    lines.append(f"  BBox: ({cb['minx']:.2f}, {cb['miny']:.2f}, {cb['minz']:.2f}) -> ("
                 f"{cb['maxx']:.2f}, {cb['maxy']:.2f}, {cb['maxz']:.2f}) | Volume: {cb['volume']:.2f}")
    lines.append("")
    lines.append("=== RESULTS ===")
    lines.append(f"  Candidates: {summary['fully_contained'] + summary['partially_contained']} of total {analysis['summary']['total_candidates']} intersected")
    lines.append(f"    - Fully contained: {summary['fully_contained']}")
    lines.append(f"    - Partially contained: {summary['partially_contained']}")
    lines.append("")

    if not grouped:
        lines.append("No contained elements.")
    else:
        for cat, items in grouped.items():
            lines.append(f"📂 {cat} ({len(items)}):")
            for it in items:
                bb = it["bbox"]
                lines.append(f"   - [{it['containment_type']}] {it['name']} (ID: {it.get('id')})")
                lines.append(f"       BBox: ({bb['minx']:.2f}, {bb['miny']:.2f}, {bb['minz']:.2f}) -> ("
                             f"{bb['maxx']:.2f}, {bb['maxy']:.2f}, {bb['maxz']:.2f}) | Volume: {bb['volume']:.2f}")
            lines.append("")

    if analysis.get("skipped"):
        lines.append("=== SKIPPED ===")
        for s in analysis["skipped"]:
            lines.append(f"  - {s['name']} (ID: {s.get('id')}) -> {s['reason']}")
    return "\n".join(lines)


def format_schedule_data(analysis: Dict[str, Any], *, include_partial=True) -> List[Dict[str, Any]]:
    if "error" in analysis:
        return []
    container_name = analysis["container"]["name"]
    schedule: List[Dict[str, Any]] = []
    for item in analysis["contained"]:
        if not include_partial and item["containment_type"] != ContainmentType.FullyContained:
            continue
        schedule.append({
            "room_name": container_name,
            "element_name": item["name"],
            "containment_type": item["containment_type"],
            "id": item.get("id"),
            "externalId": item.get("externalId")
        })
    return schedule
