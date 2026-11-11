# Release Folium

Folium is not distributed on Google Play. Users install signed APKs from GitHub releases.

The single version source is `FoliumVersion` in `Directory.Build.props`. It must be strict SemVer `X.Y.Z`, with no prefix and no suffix. The user-visible version (`ApplicationDisplayVersion`) equals that value. The Android version code (`ApplicationVersion`) is:

```
major * 10000 + minor * 100 + patch
```

Minor and patch must be below 100. Major must be at most 209999, so the version code fits in a 32-bit Android integer. Example: `1.2.3` gives version code `10203`. `0.99.99` is `9999` and is less than `1.0.0`, which is `10000`.

## Make a release

Daniel starts the `Release` workflow on `main` and does nothing else to the repository. Only Daniel starts a release, and only from `main`. A tag push does not start a release.

1. On `main`, start the `Release` workflow. Leave the `bump` input at `auto`, or set it to `patch`, `minor`, or `major` to override the computed bump.
2. The `prepare` job checks that the run is on `main`. It finds the latest `vX.Y.Z` tag, reads the commits on `main` since that tag, and fails if there are no such commits. It works out the next version, writes `FoliumVersion` and a new `## X.Y.Z` CHANGELOG section, and leaves `## Unreleased` empty. It then runs the same restore, format, build, and test commands as CI, and checks that the version code is higher than earlier release tags.
3. Approve the deployment to the `release` environment. The `sign` job builds the signed APK from the same prepared tree.
4. Only after signing succeeds, the `release` job pushes one commit `chore(release): X.Y.Z` to `main`, tags that exact commit `vX.Y.Z`, and creates a normal GitHub release (not a draft and not a prerelease). The release has exactly one asset named `folium-X.Y.Z.apk`. The notes come from the new CHANGELOG section.
5. Check the asset on a device before you tell users to install it.

If the run fails or the environment approval is rejected before the push, the repository stays unchanged. The release job pushes the commit and the tag together with `git push --atomic`. If `main` moved, that atomic push is rejected, and neither ref is updated. The repository stays unchanged unless the run fails after the atomic push succeeds.

If the atomic push succeeds and `gh release create` then fails, `main` already has the release commit and the tag `vX.Y.Z`, but there is no GitHub release. Recover as follows:

1. Download the `folium-release-apk` artifact from the failed run. Artifacts are kept for 7 days.
2. Copy the CHANGELOG section for `X.Y.Z` from that release commit into a notes file. The section is already on `main`.
3. Create the GitHub release from the existing tag:

```sh
gh release create vX.Y.Z \
  --verify-tag \
  --title vX.Y.Z \
  --notes-file release-notes.md \
  folium-X.Y.Z.apk
```

`--verify-tag` requires the tag to exist already.

A push made with `GITHUB_TOKEN` does not start CI, so the `chore(release): X.Y.Z` commit on `main` gets no CI run of its own. The same tree was already built and tested in the `prepare` job. If `main` moves during the run, the workflow fails and it does not force-push.

The APK is built from the same files that the release commit contains.

### How the next version is chosen

The starting point is the latest `vX.Y.Z` tag, not a version that you type in.

- A breaking change (`!` in the title, or a `BREAKING CHANGE:` footer) raises major. While the major version is 0, it raises minor instead.
- A `feat` commit raises minor.
- Anything else raises patch: `fix`, `perf`, `refactor`, `chore`, `docs`, `ci`, `build`, `test`, Dependabot bumps, and titles that do not follow Conventional Commits.
- An explicit `bump` input of `patch`, `minor`, or `major` overrides that result. An explicit `major` bump from a `0.Y.Z` release becomes `1.0.0`.

The CHANGELOG section uses any notes already under `## Unreleased`, then the commit titles since the tag, grouped as Features, Fixes, and Other changes. The tool strips the type prefix, the scope, and a trailing `(#NN)`, and it links each pull request.

## Check the asset

Download `folium-X.Y.Z.apk` from the release. Verify the certificate, then install it over the previous release:

```sh
apksigner verify --print-certs folium-X.Y.Z.apk
adb install -r folium-X.Y.Z.apk
```

`apksigner verify --print-certs` must show the release certificate, not the debug certificate. After `adb install -r`, the personal library file `library.json` must still be present.

## Signing setup

Daniel created the protected `release` environment, the release key, and the environment secrets. No agent created or changed GitHub environments, rulesets, or repository settings. No agent created, read, or printed the key or the secrets. The first signed GitHub release is `v0.1.0`. The keystore file must not enter git.

The `release` environment has these protection rules:

- Required reviewers: Daniel Kindl
- Deployment branches: `main` only

The four signing secrets stay on that environment, not as repository secrets:

- `ANDROID_RELEASE_KEYSTORE_BASE64`: the Base64 text of the keystore file
- `ANDROID_RELEASE_KEYSTORE_PASSWORD`: the keystore password
- `ANDROID_RELEASE_KEY_ALIAS`: the key alias, for example `folium`
- `ANDROID_RELEASE_KEY_PASSWORD`: the key password

The `sign` job still waits for Daniel's approval of the `release` environment. That gate protects the signing key. Removing the required reviewer is a settings change only Daniel makes.

`main` is not protected today, so `GITHUB_TOKEN` can push the release commit and the tag. A push by `GITHUB_TOKEN` does not start CI. That is fine, because the same restore, format, build, and test gates already ran inside the release. If `main` later gets a ruleset or branch protection, Daniel must allow the `Release` workflow to push to `main` and to create tags. Agents must not create or change that setting.

Keep a private backup of the keystore and the passwords. The workflow decodes the keystore to a temporary file, signs the APK, and deletes the file. Secret values must not appear in logs. The key alias is passed as the signing property value. The key and store passwords are passed with the `env:` prefix, which the .NET Android documentation allows only for those two password properties.

If you need to replace the key, create a PKCS12 keystore on a private computer, encode it, and update the environment secrets:

```sh
keytool -genkeypair -v \
  -keystore folium-release.keystore \
  -storetype PKCS12 \
  -alias folium \
  -keyalg RSA \
  -keysize 2048 \
  -validity 10000

base64 -w0 folium-release.keystore
```

## If the key is lost

Users must uninstall Folium and install the new APK. The signing certificate is different, so Android rejects an update. Uninstall removes the personal library file `library.json`. Tell users to export a personal library backup before they uninstall, when that is possible.
