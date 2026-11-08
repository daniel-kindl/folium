# Folium visual identity proposal

Status: owner-approved direction, subject to refinement during development. Research for [issue #10](https://github.com/daniel-kindl/folium/issues/10), checked 2026-11-03.

## Recommended direction

Use paper neutrals, forest green actions, Lucide outline icons, and locally bundled Noto Sans. Keep Avalonia FluentTheme as the control foundation. Use Android navigation and touch conventions rather than reproducing every Material control.

This is a design preference. Warm surfaces suggest a personal library, while restrained chrome lets book covers supply most color. No logo, app assets, dependencies, or UI changes form part of this proposal. The owner approved this direction on 2026-11-03 in the current project conversation: “this is a good start, we will probably refine during development. I approve”. This approves the direction, not a claim of tested implementation compatibility.

## Verified baseline and compatibility

The repository pins .NET SDK 10.0.403 and Avalonia, Avalonia.Android, and Avalonia.Themes.Fluent 12.1.5. These are the target versions, not a claim about the latest Avalonia release.

The proposed fonts and icons are assets, not .NET packages. They have no .NET target-framework dependency. Integration with the pinned runtime still needs a device proof. Avoid an icon wrapper package or SVG renderer until a demonstrated need justifies one.

[Avalonia PathIcon](https://docs.avaloniaui.net/controls/media/pathicon) renders filled stream geometry and supports recoloring. SVG files are not interchangeable with path data. Lucide uses strokes, primitives, and attributes. Convert the selected assets to filled outlines offline, preserving viewBox and stroke appearance, before creating geometry resources. Do not copy an SVG `d` attribute alone and assume equivalent rendering. Fluent and Material filled silhouettes can reduce that conversion work.

## Icon comparison

Versions below identify upstream assets checked today. They do not recommend installing npm packages into this .NET application.

| Pack and checked revision | License and maintenance evidence | Fit and coverage | Avalonia, performance, and bundle trade-off |
| --- | --- | --- | --- |
| [Lucide 1.67.0](https://github.com/lucide-icons/lucide/releases/tag/1.67.0) | [ISC plus MIT for inherited Feather icons](https://github.com/lucide-icons/lucide/blob/main/LICENSE). Release published 2026-11-03. Preserve the complete license notices. | Quiet, consistent outlines. Library, book-open, plus, pencil, trash, search, bookmark, clock, book-check, and circle-alert cover the required roles. | Recommended. Ship only selected converted paths. Stroke conversion requires visual review. No font atlas or runtime SVG dependency is necessary. |
| [Material Symbols](https://developers.google.com/fonts/docs/material_symbols), [source snapshot 10ecc147](https://github.com/google/material-design-icons/commit/10ecc14790612ca8fd164e3f4ffe9c16fc50d5b7) | Apache-2.0. Source commit dated 2026-10-23. The old repository release 4.0.0 does not identify the current Symbols catalog. | Strongest Android visual association. local_library, menu_book, add, edit, delete, search, bookmark, schedule, and check_circle cover the roles. | Reasonable alternative. Download individual SVGs and freeze the commit. Avoid bundling the full variable icon font. Choose one optical size and weight before converting. |
| [Fluent UI System Icons SVG 1.1.345](https://github.com/microsoft/fluentui-system-icons/blob/main/packages/svg-icons/package.json) | [MIT](https://github.com/microsoft/fluentui-system-icons/blob/main/LICENSE). [Source snapshot 9bfae3d7](https://github.com/microsoft/fluentui-system-icons/commit/9bfae3d782fda4c8764f2128cec4d0dda160b283), 2026-11-03. | Closest to existing Fluent controls. Book, Book Open, Library, Add, Edit, Delete, Search, Bookmark, and Checkmark Circle cover the roles. Regular and filled forms support selection. | Lowest visual integration risk. Select 24-unit regular assets consistently. Numerous size/style variants make whole-catalog bundling wasteful. No Avalonia wrapper compatibility is assumed. |
| [Phosphor core 2.0.8](https://github.com/phosphor-icons/core/releases/tag/v2.0.8) | [MIT](https://github.com/phosphor-icons/core/blob/main/LICENSE). Latest published core release is dated 2024-02-01, a weaker release-cadence signal than Lucide. Recheck maintenance before adoption. | Friendly, expressive shapes. books, book-open, plus, pencil-simple, trash, magnifying-glass, bookmark, clock, and check-circle cover the roles. | Good alternative if a softer identity is preferred. Six weights add choices and asset cost. Select regular only. SVG conversion remains necessary. |

Coverage means an available visual metaphor, not a dedicated icon for every domain state. Use text labels for “To read”, “Reading”, and “Read”. Pair them with bookmark, book-open, and book-check. Reuse book-open in an empty state instead of importing an illustration pack. Use arrow-left and overflow icons for navigation, with accessible labels.

Bundle impact depends on the selected files and conversion output. No APK-size or frame-time measurements exist for these alternatives. Selected cached geometry should avoid repeated SVG parsing, but this is an engineering inference, not a benchmark.

## Typography comparison

| Family and checked release | License, coverage, and weights | Android fit and cost |
| --- | --- | --- |
| [Noto Sans 2.015](https://github.com/notofonts/latin-greek-cyrillic/releases/tag/NotoSans-v2.015) | [OFL-1.1](https://github.com/notofonts/latin-greek-cyrillic/blob/main/OFL.txt). Latin, Greek, Cyrillic family. Multiple weights include 400, 500, and 600. | Recommended for international book metadata and neutral UI. Bundle three upright static TTF weights from the pinned release. Other scripts need fallback fonts. |
| [Inter 4.1](https://github.com/rsms/inter/releases/tag/v4.1) | [OFL-1.1](https://rsms.me/inter/). Extended Latin coverage, 100–900 weights, and text-oriented optical designs. | Clear alternative with a more contemporary UI character. Local static weights avoid a variable-axis integration requirement. It does not replace a global fallback strategy. |
| [Roboto 3.016](https://github.com/googlefonts/roboto-3-classic/releases/tag/v3.016) | [OFL-1.1](https://github.com/googlefonts/roboto-3-classic/blob/main/OFL.txt). Latin, Greek, Cyrillic coverage and multiple weights. The repository distinguishes Android builds and metrics. | Strong Android familiarity. System fallback avoids asset bytes but cannot guarantee the same font or metrics on every device. Bundle pinned files for reproducibility. |

All three are candidates for Czech, including carons and acute accents. Coverage claims come from upstream families, not a rendered Folium font test. Before approval for shipping, inspect the chosen binary's character map and render “Příliš žluťoučký kůň úpěl ďábelské ódy” at each weight. Test combining marks, long titles, Greek, Cyrillic, and fallback for Arabic/CJK metadata. Do not claim universal Unicode coverage.

[Avalonia supports embedded fonts](https://docs.avaloniaui.net/docs/styling/custom-fonts) through AvaloniaResource and `avares://` references. Keep licenses beside bundled assets. Bundling costs APK bytes and font memory, while system fallback varies by device. Neither cost nor shaping quality has been measured here. Do not download fonts at runtime or add analytics.

## Theme and token proposal

[Avalonia theme variants](https://docs.avaloniaui.net/docs/styling/theme-variants) provide Light, Dark, system preference inheritance, and RequestedThemeVariant overrides. FluentTheme supports both variants. Keep Default as the initial preference; later offer explicit Light and Dark choices.

Define semantic brushes once in application ResourceDictionary theme dictionaries. Reference changing brushes with DynamicResource. Keep spacing, sizes, and geometry as shared resources. Map colors into FluentTheme palettes and shared control styles, including focus, validation, selection, and popup states. Do not create a separate theme service or hardcode colors in each view. Verify the exact palette keys against pinned 12.1.5 before implementation.

| Token | Light | Dark |
| --- | --- | --- |
| Background | #FAF8F3 | #171C18 |
| Surface | #FFFFFF | #202922 |
| Raised | #F1EFE8 | #29352D |
| Text | #202922 | #F1F3EC |
| Muted text | #536157 | #B8C4B8 |
| Primary | #315C43 | #AED4B3 |
| On primary | #FFFFFF | #173823 |
| Secondary | #5B5269 | #CEC0DF |
| Accent | #835A27 | #E8BE82 |
| Border / focus | #6B786E | #87998A |
| Success | #28613C | #A2D5AF |
| Warning | #805600 | #E8C36B |
| Error | #A52F35 | #FFB3B6 |
| Information | #285E86 | #A9CEE9 |

Use semantic colors as foregrounds on neutral surfaces, always with text or symbols. Do not put text on book-cover artwork. Secondary and accent are optional foreground emphasis, not additional filled-button backgrounds.

[WCAG 2.2](https://www.w3.org/TR/WCAG22/) requires 4.5:1 for ordinary text and 3:1 for relevant non-text indicators. The local [contrast check](check_contrast.py) applies the sRGB luminance formula to opaque token pairs. Across background, surface, and raised surfaces, muted text has minimum ratios 5.68:1 light and 7.08:1 dark. Border/focus minima are 4.02:1 and 4.24:1. On-primary text has 7.66:1 and 7.91:1 against primary. The checker covers text, semantic colors, and primary foregrounds at 4.5:1, and borders at 3:1. These results validate the listed pairs, not full WCAG conformance.

Selected navigation uses primary foreground plus an underline or shape marker. Pressed neutral controls use Raised with Text; primary controls retain Primary and On primary and add an inset outline. Use a two-unit focus ring with a neutral gap. Do not introduce opacity overlays without recomputing the resulting contrast. Disabled controls must remain identifiable; explain unavailable actions when needed.

## Small component vocabulary

Values are proposed Avalonia logical units, not raw screen pixels. Confirm their physical touch size against Android density.

- Type size/line height/weight: caption 14/20/400, body 16/24/400, label 16/24/500, section 20/28/600, title 28/36/600. Avoid fixed-height text containers.
- Spacing: 4, 8, 12, 16, 24, 32. Screen gutters 16, growing to 24 on wider layouts.
- Corners: 8 for fields and buttons, 12 for grouped surfaces. Place covers in consistent frames within each view, independent of the selected book. Preserve the image aspect ratio within each frame. Allow title and author text to wrap and reflow separately.
- Elevation: flat content; one raised surface level for dialogs or menus. Use borders and spacing before shadows.
- Icons: 24 for actions, 20 beside text, 48 for empty states. Interactive bounds are at least 48 × 48 with eight-unit separation.

Keep buttons, fields, status chips, book rows, cover cells, and state panels as shared styles or templates. Start from built-in controls so keyboard and automation behavior remain available.

## Mobile experience and sample screens

[Android recommends 48dp touch targets](https://developer.android.com/guide/topics/ui/accessibility/apps). This exceeds WCAG 2.2's 24 CSS-pixel AA target minimum, but the units are not interchangeable. Test actual touch bounds on the phone.

Library: use a labelled bottom navigation area for Library and Search when those destinations exist. Place the primary add action near the bottom and above system insets. Prefer a list with title, author, status text, and small cover at large text sizes. Offer a cover grid when titles still fit. Keep one-handed actions reachable and preserve Android Back behavior.

Search: keep a persistent labelled input, visible clear action, results count, and the same book row. Distinguish local results from any future network lookup. An offline message must not imply the local library is unavailable.

Book details: show cover, wrapping title and author, labelled reading status, and visible edit action. Keep delete in a labelled overflow action with confirmation. This describes presentation only, not new book functionality.

Empty states explain the state and provide one useful action. Loading states reserve space and expose progress semantics. Errors retain entered data and provide retry where possible. Never rely on green/red alone or on gesture-only actions.

[Avalonia automation peers](https://docs.avaloniaui.net/docs/app-development/accessibility) expose roles, names, and states. Label icon buttons with AutomationProperties, hide decorative covers from duplicate announcements, and check reading order. Android API support does not prove TalkBack behavior. Test TalkBack, keyboard focus, system font scaling through 200%, landscape, narrow width, and reduced motion on a physical device. Reflow or use lists instead of clipping enlarged text.

For enforceable checks, CI coverage, screenshot baselines and Android verification, see [visual quality gates](visual-quality-gates.md). The research PR activates palette contrast checks only; renderer and device gates start with the visual implementation.

## Separate implementation issue

After owner selection, implement shared theme dictionaries, styles, the selected icon subset with license notices, and bundled font weights. Record exact upstream hashes and asset sizes. Prove theme switching, Czech/fallback rendering, touch bounds, TalkBack, enlarged text, and contrast in every control state on the USB device. Measure APK growth and scrolling behavior. Do not add book features, a full Material framework, branding production, telemetry, or remote asset downloads.

The approved direction is Lucide, Noto Sans, and the forest-and-paper palette. Material/Roboto and Fluent icons/Inter remain alternatives if device evidence justifies a change. Final accessibility and performance claims require implementation evidence.

The reviewed browser mockup is a visual aid, not the app implementation. It uses illustrative covers, Unicode placeholder icons, and Noto Sans only when installed, otherwise system fonts. Its cover lettering uses Georgia. The owner requested consistent book preview dimensions and approved the direction after that change. Its fixed text slots do not define the accessible app layout.
