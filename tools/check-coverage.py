#!/usr/bin/env python3
"""Fail CI when Folium C# coverage is missing or below the floor."""

import argparse
from pathlib import Path
import re
import sys
import xml.etree.ElementTree as ET


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("results", type=Path)
    parser.add_argument("--line-min", type=float, default=80.0)
    parser.add_argument("--branch-min", type=float, default=70.0)
    args = parser.parse_args()

    reports = sorted(args.results.rglob("coverage.cobertura*.xml"))
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

    try:
        line, branch = csharp_rates(production[0])
    except ValueError as error:
        print(f"Malformed Folium coverage report: {error}", file=sys.stderr)
        return 1

    print(f"Folium coverage: lines={line:.2f}% branches={branch:.2f}%")
    if line < args.line_min or branch < args.branch_min:
        print(f"Minimum: lines={args.line_min:.2f}% branches={args.branch_min:.2f}%", file=sys.stderr)
        return 1
    return 0


def csharp_rates(package: ET.Element) -> tuple[float, float]:
    """Return line and branch percentages for C# classes in one package.

    Markup is omitted. The unit-test job does not load the view, so .axaml
    lines would make the line floor unreachable.
    """

    line_hits = 0
    line_total = 0
    branch_hits = 0
    branch_total = 0
    saw_csharp = False
    for cls in package.findall("./classes/class"):
        filename = cls.get("filename") or ""
        if filename.endswith(".axaml") or filename.endswith(".xaml"):
            continue
        saw_csharp = True
        for line in cls.findall("./lines/line"):
            line_total += 1
            if int(line.get("hits") or "0") > 0:
                line_hits += 1
            if str(line.get("branch")).lower() != "true":
                continue
            covered, total = branch_counts(line.get("condition-coverage"))
            branch_hits += covered
            branch_total += total

    if not saw_csharp or line_total == 0 or branch_total == 0:
        raise ValueError("the Folium package has no C# line and branch data")

    return 100.0 * line_hits / line_total, 100.0 * branch_hits / branch_total


def branch_counts(value: str | None) -> tuple[int, int]:
    match = re.search(r"\((\d+)/(\d+)\)", value or "")
    if match is None:
        raise ValueError(f"missing branch counts in {value!r}")
    return int(match.group(1)), int(match.group(2))


if __name__ == "__main__":
    sys.exit(main())
