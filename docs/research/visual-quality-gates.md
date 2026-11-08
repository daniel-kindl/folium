# Visual quality gates

This document defines verification for the approved Folium visual direction in
[visual-identity.md](visual-identity.md). Issue #10 and PR #12 cover research and
decision-making, not implementation. Do not report a gate as passed until the
corresponding UI/assets exist and have been tested.

## Active now: required CI

- Run `python3 -B -m unittest discover -s docs/design -p 'test_*.py'`.
  This checks token constraints and rejection of invalid palettes and low contrast.

- Run `python3 docs/research/check_contrast.py` on every PR. This checks the
  [canonical token file](../design/tokens.json) light/dark opaque semantic foreground pairs against background,
  surface, and raised tokens (WCAG 2.2 AA: 4.5:1 text, 3:1 relevant non-text).
- Assert known reference ratios (black/white 21:1, identical colors 1:1) so a
  broken formula fails before palette verification.
- Existing CI continues to restore, audit, format, build, and test the app.

These checks do **not** prove rendered contrast, screen-reader accessibility,
or that the app currently uses the proposed tokens.

## Required with the theme implementation

Implement these as failing automated tests or checks alongside the first
production theme/assets PR, not as a speculative validator over unstyled AXAML:

1. **Semantic resources.** Resolve every required light/dark theme resource;
   assert expected semantic brush values. Require `DynamicResource` for
   theme-changing brushes. Reject new ad-hoc color literals in view files
   through a narrowly scoped AXAML validator with explicit allowlisted
   exceptions (e.g. custom illustrations). Do not flag resource dictionaries
   that define the approved colors.
2. **Controls and interaction.** Headless tests load real AXAML in light and
   dark modes; exercise navigation, bindings, enabled/disabled state, keyboard
   focus, and theme switching. Verify accessible names for icon-only actions
   and labels for form fields.
3. **Accessibility contrast.** Evaluate *actual effective* foreground/background
   combinations across default, focus, pressed, selected, validation, and
   disabled states where WCAG requires contrast. Alpha/composited colors must
   be calculated after blending; token contrast alone is insufficient.
4. **Fonts and icons.** Check that selected files are bundled, licenses and
   upstream revisions/hashes are recorded, and no runtime download occurs.
   Inspect Noto Sans glyph availability for Czech; test fallback for Greek,
   Cyrillic, Arabic and CJK metadata. Measure APK-size delta.
5. **Layout.** Headless view tests at narrow phone width, landscape, and
   100%/150%/200% text scaling. Assert critical elements remain reachable
   without clipping. Check navigation and action touch target dimensions.
6. **Visual regressions.** Capture approved golden screenshots for library
   (empty/populated), search (results/loading/error), and book details
   (short/long text), light and dark. Pin fonts, scale, operating system,
   renderer, and viewport in screenshot CI. Separate snapshots for
   materially different layouts. Review and explicitly approve baseline
   updates; avoid unbounded pixel tolerances or automatically refreshing
   expected images.

Do not mark screenshot comparison or runtime resource validation as active
until tests and baselines are committed and run in CI. Use Avalonia.Headless
for deterministic UI/interaction checks; confirm the screenshot capture
backend supports the actual render target before relying on image diffs.

## Required manual Android release verification

On a real Android phone or representative emulator, record evidence for
TalkBack reading order/labels, system Back, touch bounds (48dp target),
light/dark and system preference, 200% font scaling, landscape, reduced motion,
cover rendering, performance while scrolling, and APK-size change. Validate
color contrast where Android rendering differs from headless screenshots.

These manual gates are blocking for the first visual-system implementation
release, not for this research-only PR. Each gate needs an owner, recorded
result and a link to evidence; failures become issues rather than unverified
claims.
