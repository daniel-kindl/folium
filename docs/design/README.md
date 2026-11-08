# Design tokens

[tokens.json](tokens.json) is the machine-readable contract for the approved
[visual direction](../research/visual-identity.md). Version 1 records the proposed
values. The app does not load this file. This change adds no theme, styles,
assets, dependencies, layouts, or interactions.

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
They are not bundled geometry or accessible labels. Future implementation must
convert the selected icons and preserve license notices.

Cover frames must have consistent dimensions within each view and preserve image
aspect ratios. Exact cover dimensions remain unspecified. Do not add invented
cover sizes, motion durations, or responsive breakpoints to this contract.

## Future Avalonia integration

1. Define shared resources from this contract in a ResourceDictionary.
2. Put semantic brushes in Light and Dark theme dictionaries.
3. Reference changing brushes with `DynamicResource`.
4. Keep the system theme preference as the initial setting.
5. Map the resources into FluentTheme and shared styles in a separate UI change.

Use a `Folium` prefix for future resource keys, such as `FoliumPrimaryBrush`,
`FoliumSpacingLg`, and `FoliumBodyFontSize`. Keep color values in the token file
and check generated resources against it if generation is introduced.
Do not add a theme service or a runtime JSON loader for these tokens.

The [visual quality gates](../research/visual-quality-gates.md) apply when those
resources and controls exist. Theme switching, fonts, icons, focus states,
TalkBack, and device rendering remain unverified.

## Checks

Run these commands from the repository root:

```sh
python3 -m unittest discover -s docs/design -p 'test_*.py'
python3 docs/research/check_contrast.py
```

The contrast checker reads `tokens.json` directly. It resolves the path relative
to its own file, so its result does not depend on the working directory.
