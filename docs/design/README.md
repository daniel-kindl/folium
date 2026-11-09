# Design tokens

[tokens.json](tokens.json) is the machine-readable contract for the approved
[visual direction](../research/visual-identity.md). Version 1 records the design
values. The app defines compile-time Avalonia resources from these values. Tests
compare the semantic colors with this file. The app does not load the JSON at runtime.

The file uses a project-specific JSON format. It is not a DTCG interchange file.
The `version` field identifies the format. Keep names stable when values change.

## Colors

`color.light` and `color.dark` contain the same semantic names.
All colors are opaque sRGB `#RRGGBB` values.

| Name | Intended use |
| --- | --- |
| `background` | Screen background |
| `surface` | Content surface |
| `raised` | Raised surface and pressed neutral controls |
| `text` | Main text on neutral surfaces |
| `muted` | Supporting text on neutral surfaces |
| `primary` | Main action fill and selected navigation foreground |
| `onPrimary` | Text and icons on `primary` |
| `secondary` | Optional foreground emphasis on neutral surfaces |
| `accent` | Optional foreground emphasis on neutral surfaces |
| `border` | Relevant boundaries and focus indicators |
| `success`, `warning`, `error`, `info` | Feedback foregrounds on neutral surfaces |

Use text or a symbol with feedback colors. Secondary, accent, and feedback
colors do not define filled controls. The contrast checker checks text roles
against all three neutral surfaces, `onPrimary` against `primary`, and borders.
It does not prove rendered contrast or accessibility for complete controls.

## Typography and dimensions

`typography.styles` defines font size, line height, and weight for caption,
body, label, section, and title. Noto Sans is the selected family.
The font files are not bundled here. Follow the font version, license, and
fallback requirements in the visual direction before implementation.

Dimensions use Avalonia logical units. Weights use OpenType numeric weights.
Line heights are design targets. Do not enforce them with fixed-height text
containers. Text must wrap and reflow when users increase the font size.

Spacing names increase from `xs` to `xxl`. Layout tokens define normal and wide
gutters, minimum touch bounds, and minimum separation between interactive items.
The wide-layout breakpoint remains unspecified. Check physical touch sizes on Android.

`radius.control` applies to fields and buttons. `radius.group` applies to grouped
surfaces. `border.focusWidth` defines the two-unit focus ring. A neutral gap is
required, but its width remains unspecified. Do not infer a gap from this token.

`elevation` names surface roles, not shadow recipes or numeric Z coordinates.
Use `surface` for flat content and `raised` for dialogs and menus.
No shadow is specified.

## Icons and covers

`icon.size` defines inline, action, and empty-state sizes. `icon.roles` maps
semantic roles to Lucide names. These names are asset-selection guidance.
The app includes selected Lucide geometry for each role. The UI must still
provide accessible names for icon-only actions.

Cover frames must have consistent dimensions within each view and preserve image
aspect ratios. Exact cover dimensions remain unspecified. Do not add invented
cover sizes, motion durations, or responsive breakpoints to this contract.

## Avalonia integration

1. Shared resources define the semantic colors in Light and Dark theme dictionaries.
2. Shared styles reference changing brushes with `DynamicResource`.
3. The system theme preference is the initial setting.
4. FluentTheme remains the control foundation.

See [bundled design assets](assets.md) for font and icon licenses, versions, hashes
and regeneration steps.

Use a `Folium` prefix for future resource keys, such as `FoliumPrimaryBrush`,
`FoliumSpacingLg`, and `FoliumBodyFontSize`. Keep color values in the token file
and check generated resources against it if generation is introduced.
Do not add a theme service or a runtime JSON loader for these tokens.

The [visual quality gates](../research/visual-quality-gates.md) apply to these
resources and controls. Record each unrun gate as not run. Headless checks do not
prove Android TalkBack behavior or device rendering.

## Checks

Run these commands from the repository root:

```sh
python3 -m unittest discover -s docs/design -p 'test_*.py'
python3 docs/research/check_contrast.py
```

The contrast checker reads `tokens.json` directly. It resolves the path relative
to its own file, so its result does not depend on the working directory.
