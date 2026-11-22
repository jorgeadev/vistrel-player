# Releasing Vistrel Player

Releases go from `develop` to `main`, get a version tag, and are uploaded to the Microsoft Store.

## 1. Bump the version (on `develop`, through a pull request)

Update both, keeping them in step (the Store requires the fourth number to be `0`):

- `src/Vistrel.App/Package.appxmanifest` → `<Identity ... Version="0.2.0.0" />`
- `Directory.Build.props` → `<Version>0.2.0</Version>`

## 2. Release pull request

Open a pull request from `develop` into `main` titled `Release v0.2.0`. Merge it with **Create a merge commit** (not squash), so `main` and `develop` keep the same history.

## 3. Tag

```powershell
git checkout main; git pull
git tag -a v0.2.0 -m "Vistrel Player 0.2.0"
git push origin v0.2.0
```

The **Release** workflow then checks that the tag matches the manifest version and is on `main`, runs the tests, builds the Store upload for x64 and ARM64, and publishes a GitHub release with generated notes, the `.msixupload` file and its SHA-256 checksum. `0.x` versions and tags with a suffix (`v1.0.0-beta`) are marked as pre-releases.

## 4. Check the package locally (optional, recommended before the first submissions)

Download the `.msixupload` from the release, extract the `.msixbundle` (it's a zip), and run the Windows App Certification Kit from an **administrator** terminal:

```powershell
& "${env:ProgramFiles(x86)}\Windows Kits\10\App Certification Kit\appcert.exe" test `
  -appxpackagepath "C:\path\to\Vistrel.App_0.2.0.0_x64_ARM64.msixbundle" `
  -reportoutputpath "$env:TEMP\vistrel-wack.xml"
```

## 5. Submit to the Microsoft Store

In [Partner Center](https://partner.microsoft.com/dashboard) → **Apps and games** → **Vistrel Player** → **Start update** (or the first submission):

1. **Packages**: upload the `.msixupload` from the GitHub release.
2. **Store listings**: text and screenshots from [`docs/store/`](store/).
3. **Submission options → Notes for certification**: use the `runFullTrust` explanation in [`docs/store/listing.md`](store/listing.md).
4. Submit. Certification usually takes a few hours to 3 business days.

For test releases, set **Availability → Visibility** to **Private audience** so only the listed accounts can see and install the app.
