<p align="center"><img src="branding/store/store-logo-300.png" width="120" alt="Vistrel Player logo"></p>

# Vistrel Player

A fast, private video player for Windows 10 and 11, built with WinUI 3 and LibVLC. Coming next: automatic subtitles generated on your PC by a local Whisper model, with nothing uploaded.

**Status:** early development (v0.1). Plays local video and audio files.

## Features so far

- Plays almost any format (MP4, MKV, WebM, AVI, MOV, TS and more) with hardware decoding
- Open with the file picker, drag and drop, "Open with" or file associations
- Seek bar, volume up to 200%, mute, playback speed from 0.25× to 4×, full screen
- Keyboard shortcuts: Space/K play-pause · ←/→ 5 s · J/L 30 s · ↑/↓ volume · M mute · F/F11 full screen · Esc exit full screen · Ctrl+O open
- Mica window background on Windows 11 (acrylic on Windows 10)
- No telemetry; local logs only ([privacy policy](PRIVACY.md))

## Build and run

Requirements: Windows 10 22H2 or later, the [.NET 10 SDK](https://dotnet.microsoft.com/download), and Visual Studio 2026 or its Build Tools with the Windows App SDK / WinUI workload (needed for MSIX packaging).

```powershell
# Unit tests
dotnet test --solution VistrelPlayer.slnx

# Run the app without packaging (quickest for development)
dotnet build src/Vistrel.App -p:Platform=x64 -p:WindowsPackageType=None
src/Vistrel.App/bin/x64/Debug/net10.0-windows10.0.19041.0/VistrelPlayer.exe "C:\path\to\video.mp4"

# Build an MSIX package (unsigned, for testing)
dotnet build src/Vistrel.App -c Release -p:Platform=x64 -p:GenerateAppxPackageOnBuild=true `
  -p:UapAppxPackageBuildMode=SideloadOnly -p:AppxBundle=Never -p:AppxPackageDir="$PWD\artifacts\msix\"
```

Use `-p:Platform=ARM64` for ARM64 PCs.

## Project layout

| Path | Contents |
|---|---|
| `src/Vistrel.App` | WinUI 3 app: window, view models, MSIX manifest and assets |
| `src/Vistrel.Core` | Engine-independent playback interface and helpers |
| `src/Vistrel.Playback.Vlc` | `IPlaybackEngine` implemented with LibVLC |
| `src/Vistrel.Subtitles` | Subtitle parsing and writing (SRT so far), with limits for untrusted files |
| `tests/` | xUnit tests |
| `branding/` | Logo SVGs and the script that generates every MSIX icon (`npm install && npm run assets`) |

## Publishing to the Microsoft Store

1. Reserve the name "Vistrel Player" in [Partner Center](https://partner.microsoft.com/dashboard).
2. Copy the package identity (Name, Publisher, PublisherDisplayName) from **Product identity** into `src/Vistrel.App/Package.appxmanifest`.
3. Build with `-p:UapAppxPackageBuildMode=StoreUpload -p:AppxBundle=Always -p:AppxBundlePlatforms="x64|arm64"` and upload the `.msixupload`.
4. Use `PRIVACY.md` (published on GitHub Pages) as the privacy policy URL.

## License

GPL-3.0. See [LICENSE](LICENSE) and [third-party notices](THIRD-PARTY-NOTICES.md). The Vistrel name and logo are not covered by the code license.
