# Release Folium

Folium is not distributed on Google Play. Users install signed APKs from GitHub releases.

The single version source is `FoliumVersion` in `Directory.Build.props`. It must be strict SemVer `X.Y.Z`, with no prefix and no suffix. The user-visible version (`ApplicationDisplayVersion`) equals that value. The Android version code (`ApplicationVersion`) is:

```
major * 10000 + minor * 100 + patch
```

Minor and patch must be below 100. Major must be at most 209999, so the version code fits in a 32-bit Android integer. Example: `1.2.3` gives version code `10203`. `0.99.99` is `9999` and is less than `1.0.0`, which is `10000`.

## Make a release

1. Set `FoliumVersion` in `Directory.Build.props` to the new `X.Y.Z` value.
2. Move the notes under `## Unreleased` in `CHANGELOG.md` to a new `## X.Y.Z` section. The release fails if that section is missing or empty.
3. Merge the change to `main`.
4. On `main`, start the `Release` workflow and set the `version` input to `X.Y.Z`. Only Daniel starts a release, and only from `main`. The input must match `FoliumVersion`. A tag push does not start a release.
5. The `validate` job first checks that the run is on `main`. It then runs the same restore, format, build, and test commands as CI, and checks that the version code is higher than earlier `vX.Y.Z` release tags.
6. Approve the deployment to the `release` environment. The `sign` job then builds the signed APK. The `release` job creates a normal GitHub release (not a draft and not a prerelease). The tag `vX.Y.Z` is created on the commit that was built. The release has exactly one asset named `folium-X.Y.Z.apk`.
7. Check the asset on a device before you tell users to install it.

## Check the asset

Download `folium-X.Y.Z.apk` from the release. Verify the certificate, then install it over the previous release:

```sh
apksigner verify --print-certs folium-X.Y.Z.apk
adb install -r folium-X.Y.Z.apk
```

`apksigner verify --print-certs` must show the release certificate, not the debug certificate. After `adb install -r`, the personal library file `library.json` must still be present.

## One-time signing setup

Daniel creates the protected `release` environment, the release key, and the environment secrets. No agent creates or changes GitHub environments, rulesets, or repository settings. No agent creates, reads, or prints the key or the secrets. The keystore file must not enter git.

In the GitHub repository settings, create an environment named `release` with these protection rules:

- Required reviewers: Daniel Kindl
- Deployment branches: `main` only

Create a PKCS12 keystore on a private computer. Choose a strong password. Example:

```sh
keytool -genkeypair -v \
  -keystore folium-release.keystore \
  -storetype PKCS12 \
  -alias folium \
  -keyalg RSA \
  -keysize 2048 \
  -validity 10000
```

Encode the keystore as one line of Base64:

```sh
base64 -w0 folium-release.keystore
```

Add these secrets to the `release` environment. Do not add them as repository secrets:

- `ANDROID_RELEASE_KEYSTORE_BASE64`: the Base64 text of the keystore file
- `ANDROID_RELEASE_KEYSTORE_PASSWORD`: the keystore password
- `ANDROID_RELEASE_KEY_ALIAS`: the key alias, for example `folium`
- `ANDROID_RELEASE_KEY_PASSWORD`: the key password

Keep a private backup of the keystore and the passwords. The workflow decodes the keystore to a temporary file, signs the APK, and deletes the file. Secret values must not appear in logs. The key alias is passed as the signing property value. The key and store passwords are passed with the `env:` prefix, which the .NET Android documentation allows only for those two password properties.

## If the key is lost

Users must uninstall Folium and install the new APK. The signing certificate is different, so Android rejects an update. Uninstall removes the personal library file `library.json`. Tell users to export a personal library backup before they uninstall, when that is possible.

