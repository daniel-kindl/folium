# Bundled design assets

The app bundles Noto Sans and selected Lucide icons. It does not download them.

## Noto Sans

The app uses the Regular, Medium and SemiBold upright faces from Noto Sans 2.015. The license file is beside the font files. `assets.json` records the upstream tag and archive hash. It also records each bundled file hash.

The regular face character map contains Czech, Greek and Cyrillic glyphs. Device rendering remains to test. Test Greek, Cyrillic, Arabic and CJK metadata on Android to check font fallback.

## Lucide

The app uses only the icon roles listed in `tokens.json`. The source SVG files are in `assets/lucide/source`. The license file has the ISC notice and the MIT notice for inherited Feather icons.

The design research proposed filled outlines. This implementation keeps Lucide strokes in Avalonia `Path` shapes. Avalonia supports stroke width, round caps and round joins. This preserves the source appearance and avoids a runtime SVG parser.

Run `python3 tools/design/build_lucide.py` to generate `src/Folium/Styles/Icons.axaml`. Add `--check` to check the generated file. The generator accepts only 24 by 24 SVGs with 2-unit strokes, round caps and round joins. It stops on unsupported SVG elements.

`assets.json` records the upstream revision and SHA-256 hash of each selected SVG. Tests check those hashes, the bundled font files, the license files and the generated icon resources.
