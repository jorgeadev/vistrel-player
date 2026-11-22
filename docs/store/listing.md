# Microsoft Store listing: Vistrel Player

Copy these into Partner Center → Vistrel Player → submission. Field limits are noted in brackets.

## Properties

| Field | Value |
|---|---|
| Category | **Photo & video** (subcategory: none) |
| Privacy policy URL | `https://jorgeadev.github.io/vistrel-player/PRIVACY` |
| Website | `https://github.com/jorgeadev/vistrel-player` |
| Support contact | `https://github.com/jorgeadev/vistrel-player/issues` |
| Product declarations | Leave "This app has been tested to meet accessibility guidelines" **unchecked** until the accessibility pass is done |
| System requirements | Defaults (no special hardware) |

## Pricing and availability

| Field | Value for the test release |
|---|---|
| Markets | All markets |
| Visibility | **Private audience**, with your own Microsoft account in the audience list (switch to *Public* for the real launch) |
| Pricing | **Free** |
| Free trial | No free trial |

## Age ratings (IARC questionnaire)

Choose the category **All Other App Types** and answer **No** to every content question (violence, sex, gambling, user-generated content shared with other users, location sharing, digital purchases, unrestricted internet browsing). The app only plays files the user already has. Expected rating: **3+ / Everyone**.

## Store listing (English)

**Product name**: Vistrel Player

**Short description** [270 characters]

> A fast, private video player for Windows. Plays almost any format, with subtitle and audio track switching and resume where you left off. No ads, no accounts, no tracking.

**Description** [10,000 characters]

> Vistrel Player is a fast, clean video player that respects your privacy.
>
> Open a video by double-clicking it, dropping it on the window or picking it from your PC. Vistrel Player handles the formats you actually have, including MKV, MP4, WebM, AVI, MOV and TS, with hardware decoding for smooth 4K playback.
>
> Watching something with several audio languages or subtitle tracks? Switch between them from the control bar, load your own subtitle file, or nudge subtitles and audio earlier or later when they are out of sync.
>
> Close the app halfway through a movie and pick it up later exactly where you stopped. Your recent videos are waiting on the start screen.
>
> Private by design: no ads, no account, no analytics, and nothing about what you watch ever leaves your PC.
>
> Vistrel Player is free and open source.
>
> Coming soon: automatic subtitles generated on your PC by a local AI model, with nothing uploaded.

**What's new in this version** [1,500 characters]

> First preview release: playback of common video and audio formats, audio and subtitle track selection, external subtitle files, subtitle and audio delay, resume where you left off, recent files, and keyboard shortcuts.

**Product features** [up to 20, 200 characters each]

1. Plays MKV, MP4, WebM, AVI, MOV, TS and more, with hardware decoding
2. Switch audio and subtitle tracks; load external .srt, .vtt, .ass and .ssa files
3. Fix out-of-sync subtitles or audio with one key
4. Resume every video where you left off
5. Recent videos on the start screen
6. Playback speed from 0.25× to 4×, volume up to 200%
7. Keyboard shortcuts for everything (Space, arrows, J/L, V/B, G/H, F, M)
8. Clean dark design that keeps the focus on the video
9. No ads, no accounts, no tracking
10. Free and open source (GPL-3.0)

**Keywords** [up to 7]

`video player`, `mkv player`, `media player`, `subtitles`, `movie player`, `4k video`, `open source`

**Copyright and trademark info**

> © 2026 JorgeADev. Vistrel Player is open source under the GPL-3.0 license.

**Additional license terms**: `https://github.com/jorgeadev/vistrel-player/blob/main/LICENSE`

**Screenshots** (Desktop, 1920×1080, in this order): files in [`screenshots/`](screenshots/)

1. `vistrel-1-start.png`: *Pick up where you left off*
2. `vistrel-2-subtitles.png`: *Subtitles in every format*
3. `vistrel-3-playing.png`: *Smooth, hardware-accelerated playback*

**Store logos and display images** (all in `branding/store/`, regenerate with `npm run store-art` in `branding/`):

| Partner Center slot | File |
|---|---|
| 9:16 Poster art (1440×2160) | `poster-art-1440x2160.png` |
| 1:1 Box art (2160×2160) | `box-art-2160x2160.png` |
| Store display image 1:1, 300×300 | `app-tile-300x300.png` |
| Store display image 1:1, 150×150 | `app-tile-150x150.png` |
| Store display image 1:1, 71×71 | `app-tile-71x71.png` |
| 16:9 Super hero art (3840×2160) | `super-hero-art-3840x2160.png` (no product name, as the Store requires) |

| Xbox: Branded key art (584×800) | `xbox-branded-key-art-584x800.png` (optional; only shown on Xbox) |
| Xbox: Titled hero art (1920×1080) | `xbox-titled-hero-art-1920x1080.png` (optional; only shown on Xbox) |

Trailers are optional; skip them for now.

## Submission options → Notes for certification

Paste this; it explains the restricted capability so testers don't have to ask:

> Vistrel Player is a desktop media player built with the Windows App SDK (WinUI 3). The `runFullTrust` capability is required for all packaged Windows App SDK desktop apps. The app uses it to play the local media files the user opens (file picker, drag and drop, or file association) through the bundled LibVLC engine. It does not request broadFileSystemAccess, does not run elevated, has no accounts and does not use the network except to play a stream the user opens.
>
> To test: open any local video (for example an MP4 or MKV). Use the audio and subtitle buttons in the control bar to switch tracks, close the app mid-video and reopen the same file to see it resume.
