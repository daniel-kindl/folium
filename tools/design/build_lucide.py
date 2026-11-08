#!/usr/bin/env python3
"""Convert the selected Lucide SVG shapes to Avalonia geometry resources."""

from __future__ import annotations

import argparse
from pathlib import Path
from xml.etree import ElementTree
from xml.sax.saxutils import escape


ROOT = Path(__file__).resolve().parents[2]
SOURCE = ROOT / "docs/design/assets/lucide/source"
OUTPUT = ROOT / "src/Folium/Styles/Icons.axaml"
ICONS = {
    "FoliumLibraryIcon": "library",
    "FoliumBookIcon": "book-open",
    "FoliumAddIcon": "plus",
    "FoliumEditIcon": "pencil",
    "FoliumDeleteIcon": "trash",
    "FoliumSearchIcon": "search",
    "FoliumToReadIcon": "bookmark",
    "FoliumReadingIcon": "book-open",
    "FoliumReadIcon": "book-check",
    "FoliumTimeIcon": "clock",
    "FoliumErrorIcon": "circle-alert",
    "FoliumBackIcon": "arrow-left",
    "FoliumOverflowIcon": "ellipsis",
    "FoliumEmptyIcon": "book-open",
}
SVG_NAMESPACE = "{http://www.w3.org/2000/svg}"


def geometry(element: ElementTree.Element) -> str:
    """Convert one supported SVG element to a geometry object."""
    kind = element.tag.removeprefix(SVG_NAMESPACE)
    if kind == "path":
        data = escape(element.attrib["d"])
        return f"            <StreamGeometry>{data}</StreamGeometry>"
    if kind == "circle":
        center = f"{element.attrib['cx']},{element.attrib['cy']}"
        radius = element.attrib["r"]
        return (
            "            <EllipseGeometry "
            f'Center="{center}" RadiusX="{radius}" RadiusY="{radius}" />'
        )
    if kind == "line":
        start = f"{element.attrib['x1']},{element.attrib['y1']}"
        end = f"{element.attrib['x2']},{element.attrib['y2']}"
        return f'            <LineGeometry StartPoint="{start}" EndPoint="{end}" />'
    raise ValueError(f"Unsupported SVG element: {kind}")


def build() -> str:
    """Build a deterministic Avalonia resource dictionary."""
    lines = [
        '<ResourceDictionary xmlns="https://github.com/avaloniaui"',
        '                    xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">',
    ]
    for key, name in ICONS.items():
        root = ElementTree.parse(SOURCE / f"{name}.svg").getroot()
        if root.attrib.get("viewBox") != "0 0 24 24":
            raise ValueError(f"{name}.svg must use a 24 by 24 viewBox")
        if root.attrib.get("stroke-width") != "2":
            raise ValueError(f"{name}.svg must use the approved 2-unit stroke")
        if root.attrib.get("stroke-linecap") != "round" or root.attrib.get("stroke-linejoin") != "round":
            raise ValueError(f"{name}.svg must use round caps and joins")
        lines.extend([f'    <GeometryGroup x:Key="{key}">', "        <GeometryGroup.Children>"])
        lines.extend(geometry(child) for child in root)
        lines.extend(["        </GeometryGroup.Children>", "    </GeometryGroup>"])
    lines.append("</ResourceDictionary>")
    return "\n".join(lines) + "\n"


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--check", action="store_true", help="Fail if the generated file is stale.")
    args = parser.parse_args()
    expected = build()
    if args.check:
        if not OUTPUT.exists() or OUTPUT.read_text(encoding="utf-8") != expected:
            parser.error(f"{OUTPUT.relative_to(ROOT)} is stale. Run {Path(__file__).relative_to(ROOT)}.")
        return 0
    OUTPUT.write_text(expected, encoding="utf-8")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
