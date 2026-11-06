#!/usr/bin/env python3
"""Fail CI when Folium's Cobertura coverage is missing or below the floor."""

import argparse
from pathlib import Path
import sys
import xml.etree.ElementTree as ET


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("results", type=Path)
    parser.add_argument("--line-min", type=float, default=80.0)
    parser.add_argument("--branch-min", type=float, default=70.0)
    args = parser.parse_args()

    reports = sorted(args.results.rglob("coverage.cobertura.xml"))
    if len(reports) != 1:
        print(f"Expected one Cobertura report, found {len(reports)}", file=sys.stderr)
        return 1

    root = ET.parse(reports[0]).getroot()
    packages = root.findall("./packages/package")
    production = [p for p in packages if p.get("name") == "Folium"]
    if len(production) != 1:
        names = [p.get("name") for p in packages]
        print(f"Expected one Folium package; found {names!r}", file=sys.stderr)
        return 1

    package = production[0]
    line = float(package.attrib["line-rate"]) * 100
    branch = float(package.attrib["branch-rate"]) * 100
    print(f"Folium coverage: lines={line:.2f}% branches={branch:.2f}%")
    if line < args.line_min or branch < args.branch_min:
        print(f"Minimum: lines={args.line_min:.2f}% branches={args.branch_min:.2f}%", file=sys.stderr)
        return 1
    return 0


if __name__ == "__main__":
    sys.exit(main())
