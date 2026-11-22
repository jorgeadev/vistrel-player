# Third-party notices

Vistrel Player is licensed under the GNU General Public License v3.0 (see `LICENSE`). It includes or depends on the following components.

| Component | License | Notes |
|---|---|---|
| [LibVLC](https://www.videolan.org/vlc/libvlc.html) 3.0 (VideoLAN.LibVLC.Windows) | LGPL-2.1-or-later | Shipped as separate DLLs in `libvlc/` and loaded dynamically. Some plugins are GPL-licensed, which is compatible with this project's GPL-3.0 license. Source: https://code.videolan.org/videolan/vlc |
| [LibVLCSharp](https://code.videolan.org/videolan/LibVLCSharp) | LGPL-2.1-or-later | .NET bindings for LibVLC. |
| [Windows App SDK / WinUI 3](https://github.com/microsoft/WindowsAppSDK) | MIT | |
| [CommunityToolkit.Mvvm](https://github.com/CommunityToolkit/dotnet) | MIT | |
| [Serilog](https://serilog.net/) and Serilog.Sinks.File | Apache-2.0 | Local log files only. |
| [SharpDX](https://github.com/sharpdx/SharpDX) | MIT | Dependency of LibVLCSharp.WinUI. |

Planned for the AI subtitles release: [whisper.cpp](https://github.com/ggml-org/whisper.cpp) / Whisper.net (MIT) and OpenAI Whisper model weights (MIT).
