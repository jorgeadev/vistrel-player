# Contributing to Vistrel Player

Thanks for helping! This guide explains how changes get into the app.

## Branches

| Branch | Purpose | Who merges |
|---|---|---|
| `develop` | Default branch. All contributions target it. | Maintainer, after review and green CI |
| `main` | Released code only. Each Store release is merged from `develop` and tagged (`v1.0.0`). | Maintainer |

Both branches are protected: no direct pushes, no force pushes, no deletion. Every change goes through a pull request that passes CI (x64 and ARM64 builds, tests, vulnerable-package scan).

## Making a change

1. Fork the repo (or create a branch if you have write access) from `develop`.
   Name it by type: `feature/subtitle-tracks`, `fix/seek-on-ended`, `docs/readme-build`.
2. Build and test locally (see the README):
   ```powershell
   dotnet test --solution VistrelPlayer.slnx
   dotnet build src/Vistrel.App -p:Platform=x64 -p:WindowsPackageType=None
   ```
3. Open a pull request into `develop`. Describe what changed and how you tested it; add a screenshot for UI changes.
4. Keep pull requests small and focused. One feature or fix per PR is easiest to review.

## Code guidelines

- C# with nullable reference types; the build treats warnings as errors.
- UI code talks to `IPlaybackEngine`, never to LibVLC directly.
- Treat every media, subtitle and playlist file as untrusted: cap sizes and counts, never execute content.
- No telemetry or network calls without an explicit opt-in setting and an update to `PRIVACY.md`.
- Add or update tests in `tests/` for logic changes.
- Keep new dependencies to a minimum and say why in the PR. Licenses must be GPL-3.0-compatible.

## Reporting bugs and security issues

- Bugs and feature ideas: open a GitHub issue.
- Security vulnerabilities: **do not open a public issue.** See [SECURITY.md](SECURITY.md).

By contributing, you agree that your contributions are licensed under the GPL-3.0 license.
