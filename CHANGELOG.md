# Changelog

Notable changes are recorded here. Versions follow [Semantic Versioning](https://semver.org/).

## Unreleased

- Stop the version tests from pinning FoliumVersion to 0.1.0, so a later release can pass the test suite.
- Start a signed release from main with one workflow run. The workflow works out the next version, writes the CHANGELOG, signs the APK, and publishes the GitHub release after signing succeeds.
- Check GitHub for stable releases and install signed APK updates through the Android package installer.

## 0.1.0

- Build a signed release APK and publish it to a GitHub release as `folium-X.Y.Z.apk`. The Android version code is derived from one SemVer value.
- Search saved books by title or author, and sort the library offline. The default order is title A-Z, with the local book ID as the tie-break.
- Apply the approved Folium visual design with light and dark themes, bundled Noto Sans and Lucide icons, and system text scaling on Android.
- Review and import Open Library editions with editable offline metadata and optional covers.
- Search Open Library by title and preview edition metadata and covers.
- Add, edit, and remove books in an offline personal library.
- Establish the Android application foundation and development workflow.
