# FlowLyrics

[English](README.md) | [日本語](README.ja.md)

FlowLyrics is a customizable, always-on-top lyrics overlay for Spotify on Windows. It follows the track playing in the Spotify desktop app, displays synchronized lyrics, and can become click-through when locked.

> This package is the `1.3.0-dev.5` confirmation build. It is not the final `1.3.0` release.

It does not require a Spotify Developer account, Client ID, or account password. Playback information comes from Windows Global System Media Transport Controls (SMTC), and lyrics are searched through LRCLIB.

## Features

- Automatically follows track changes, playback, pause, and seeking in Spotify for Windows
- Safer LRCLIB matching that validates title, artist, version, and duration before automatic use
- Japanese-script preference that prevents romanized Japanese lyrics from being auto-applied
- Editable progressive LRCLIB candidate search, metadata preview, and persistent per-track manual selection
- Per-track cache clearing and a one-minute limit for “not found” cache entries
- Synchronized lyrics from LRCLIB with local caching and automatic retries
- Continuous full-text scrolling when only plain lyrics are available
- Local `.lrc` support with automatic file-change detection
- Transparent or colored background, always-on-top mode, and click-through lock mode
- Fluid wrapping and automatic font scaling for narrow or small windows
- 1–12 visible lines with adjustable active-line position, alignment, spacing, and opacity
- Ten curated color presets, custom colors, coordinated random palettes, a shared Player UI / Settings accent, and synchronized Reverse Colors controls
- Adjustable font, outline, shadow, background, border, padding, and corner radius
- Spotify previous, play/pause, next, seek, mute, and volume controls
- Current time, track duration, and a timestamp preview when hovering over the seek bar
- Non-modal Settings window so playback controls remain usable while customizing the overlay
- Four-corner resizing, tray controls, Windows startup, and optional global shortcuts
- UI languages: English, Japanese, Simplified Chinese, Traditional Chinese, Korean, Spanish, French, German, Brazilian Portuguese, and Russian

## Requirements

- Windows 10 version 1809 or later, or Windows 11
- Spotify desktop app for Windows
- Internet access when searching for lyrics that are not already cached

The portable build is self-contained and does not require a separate .NET installation.

## Install and run

1. Download `FlowLyrics-v1.3.0-dev.5-win-x64-portable.zip` from the provided development build.
2. Extract the ZIP to a folder you can write to.
3. Run `FlowLyrics.exe`.
4. Start playing a track in the Spotify desktop app.
5. Open Settings with the three-dot button and choose your language and appearance.

Windows SmartScreen may appear because the current personal build is not code-signed. Verify where you downloaded the file from before choosing to run it.

## Controls

| Action | Result |
| --- | --- |
| Drag the overlay | Move the window |
| Drag any corner grip | Resize the window |
| Three-dot button or right-click | Open Settings and lyric actions |
| Player controls | Previous, play/pause, next, and seek |
| Hover VOL | Open the vertical Spotify-only volume slider |
| Click VOL | Mute or unmute Spotify only |
| Click REV | Reverse overlay colors while preserving the Player UI accent |
| `Ctrl + Alt + L` | Lock or unlock the overlay |
| `Ctrl + Alt + K` | Show or hide the overlay |
| Double-click the tray icon | Show or hide the overlay |

Global shortcuts can be disabled in Settings if they conflict with another application.

When locked, the lyrics area passes clicks through to applications behind it. Player controls remain usable while they are visible.

VOL uses Spotify's Windows shared-mode audio sessions and searches every active output device. It never changes the system-wide master volume. Windows does not expose per-app attenuation for an exclusive-mode stream, so the slider is disabled while Spotify is using exclusive output.

## Lyrics selection and local LRC files

Open **Settings > Lyrics** to inspect the current Spotify metadata and the LRCLIB record currently in use. If the lyrics or timing are wrong, or the automatic matcher finds more than one safe possibility, choose **Choose from LRCLIB**. You can edit Title, Artist, Album, and Keyword—including an English or romanized title—before searching.

Manual LRCLIB selections are remembered for the same Spotify track. Choosing **Use these lyrics** removes the previous cache for that track, stores the selected LRCLIB record, applies it immediately, and closes the chooser. Use **Reset manual selection** when you want to return to automatic matching.

Open the LRC folder from **Settings > Lyrics > Local LRC**, then place timestamped `.lrc` files in it. The default location is:

```text
%APPDATA%\FlowLyrics\lyrics-cache
```

Use either filename format:

```text
Artist - Title.lrc
Title.lrc
```

Adding, replacing, or removing an LRC file is detected automatically while FlowLyrics is running. The same folder also contains FlowLyrics cache `.json` files; storing `.lrc` files alongside them is expected and safe.

## Troubleshooting

If no track or lyrics appear:

1. Confirm that the current Spotify track appears in the Windows media panel.
2. Restart Spotify, then restart FlowLyrics.
3. Open **Settings > Lyrics**. If candidates are available, review the artist, version, and duration before choosing one.
4. Search again with an English or romanized title, or add a timestamped local LRC file.

FlowLyrics deliberately leaves ambiguous results unselected. A duration difference over two seconds, a clearly different artist, or Live/Instrumental/Remix/Cover/Karaoke/TV Size mismatch prevents automatic use. LRCLIB rate limits or temporary network failures may also delay a result; FlowLyrics pauses duplicate requests, respects the server retry interval, and retries automatically.

## Data and privacy

FlowLyrics sends the track title, artist, album, and duration to LRCLIB when it searches for lyrics. It does not send audio, Spotify credentials, account passwords, or a complete listening history. Settings, hashed lyric cache files, logs, and manual LRCLIB selections are stored locally in:

```text
%APPDATA%\FlowLyrics
```

## Limitations

- FlowLyrics follows a Spotify media session on the same Windows PC; it cannot follow playback occurring only on a phone or another computer.
- Exclusive fullscreen applications may appear above the overlay.
- Detection and playback controls depend on what Spotify exposes through Windows SMTC.
- Spotify Track ID is not exposed by every SMTC session. In that case FlowLyrics uses a stable key derived from normalized title, artist, album, and duration for cache and manual selections.
- Tracks without synchronized lyrics require plain-lyrics fallback or a local LRC file.

## Build from source

Install the [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0), then run:

```powershell
dotnet run --project FlowLyrics.csproj
```

To create the self-contained Windows x64 build:

```powershell
dotnet publish FlowLyrics.csproj -c Release -r win-x64 --self-contained true `
  -p:PublishSingleFile=true `
  -p:IncludeNativeLibrariesForSelfExtract=true `
  -p:PublishTrimmed=false
```

Run the matching and cache regression tests with:

```powershell
dotnet run --project FlowLyrics.Tests/FlowLyrics.Tests.csproj -c Release
```

## License and services

FlowLyrics is released under the MIT License. See `LICENSE` and `THIRD_PARTY_NOTICES.md` for license and external-service information.
