# FlowLyrics

**English** | [日本語](README_ja.md)

## Download

[Download the latest version of FlowLyrics](https://github.com/MoriKouta/FlowLyrics/releases/latest)

On the Release page, download `FlowLyrics-vX.X.X-win-x64-portable.zip` from **Assets**. The automatically generated `Source code (zip)` and `Source code (tar.gz)` files are not the Windows application.

FlowLyrics is a Windows-only desktop overlay that detects the song currently playing in Spotify for Windows and displays synchronized lyrics in a transparent, always-on-top window.

It does not require the Spotify Web API, a Spotify Developer account, Client ID, or password. FlowLyrics reads the title, artist, and playback position published by Spotify through Windows Global System Media Transport Controls (SMTC). Lyrics are searched through LRCLIB and cached locally on your PC.

## Features

- Automatically follows song changes, pause/resume, and seeking in Spotify for Windows
- Displays timestamped synchronized lyrics from LRCLIB
- Runs multiple lyric searches in parallel to handle common title and artist variations
- Can display plain lyrics with clearly labeled approximate timing when synchronized lyrics are unavailable
- Fully transparent background or a colored semi-transparent panel
- Always-on-top display with 1–12 lyric lines
- Current-line position can be placed at the top, center, or bottom
- Automatic wrapping and font-size reduction for long lyrics
- 10 color presets, custom colors, and randomized color themes
- Independent styling for the current, previous, and next lines
- Adjustable font, opacity, outline, shadow, line spacing, padding, and corner radius
- Independently show or hide the frame, song title, progress bar, and playback controls
- Control previous track, play/pause, and next track from the overlay
- Click-through window locking so mouse input reaches applications behind the overlay
- Drag to move and resize from any corner
- Per-song lyric timing offset
- Register your own `.lrc` files
- Open the local LRC folder from Settings and automatically detect file additions or updates
- System tray support, Windows startup option, and optional global shortcuts
- Multilingual interface with English as the default language
- No Spotify credentials, password, Developer account, or Client ID required

## Requirements

- Windows 10 version 1809 or later, or Windows 11
- Spotify desktop app for Windows
- Internet connection only when searching for new lyrics
- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0) only when running from source

Both the Microsoft Store and Spotify.com versions are supported. FlowLyrics detects media sessions whose application ID contains `spotify`.

## Quick Start

1. Download `FlowLyrics-vX.X.X-win-x64-portable.zip`from the latest Release.
2. Double-click `FlowLyrics.exe`. No additional .NET installation is required.
3. Start playing a song in Spotify for Windows.
4. Drag the lyric window to the desired location and resize it from any corner.
5. Open Settings using the three-dot button or the right-click menu.
6. Press `Ctrl + Alt + L` to lock the overlay after positioning it.

Windows SmartScreen may appear on first launch. FlowLyrics is currently an unsigned personal build. Verify that you downloaded it from this repository before choosing **More info** and running the application.

## Controls

| Action | Function |
| --- | --- |
| Drag the window | Move the overlay |
| Drag any corner | Resize the overlay |
| Right-click | Open Settings, refresh lyrics, or adjust lyric timing |
| Bottom controls | Previous track / Play or pause / Next track |
| `Ctrl + Alt + L` | Lock or unlock the overlay |
| `Ctrl + Alt + K` | Show or hide the overlay |
| Double-click the tray icon | Show or hide the overlay |

When locked, mouse input passes through the overlay to applications behind it, including Nuke and web browsers. Unlock it using the global shortcut or the system tray menu. Global shortcuts can be disabled in Settings if they conflict with another application.

## When Lyrics Are Not Displayed

1. Check whether the song title appears in the Windows volume or media panel.
2. Close FlowLyrics, restart Spotify, and then launch FlowLyrics again.
3. Use **Refresh Lyrics** from the right-click menu.
4. When you have a timestamped LRC file, open the folder from **Settings > Local LRC** and add it there.

The local LRC folder is:

```text
%APPDATA%\FlowLyrics\lyrics-cache
```

Name a file `Artist - Song Title.lrc` or `Song Title.lrc` to let FlowLyrics match it automatically to the current track. Added, updated, and removed files are detected while the application is running.

## Data Location

Settings and cached lyrics are stored in:

```text
%APPDATA%\FlowLyrics
```

Spotify login information is not stored.

## Limitations

- FlowLyrics does not follow playback occurring only on a phone or another PC.
- It may not appear above games using exclusive fullscreen mode.
- Detection is unavailable when Spotify or Windows does not publish a media session.
- A manual LRC file is required when LRCLIB has no synchronized lyrics for a track.
- Playback controls only work when Spotify permits the corresponding Windows media-session command.

## Privacy

Only the song title, artist, album, and track duration are sent to LRCLIB when searching for lyrics. Audio data, Spotify account information, and playback-history lists are not collected or transmitted. FlowLyrics does not use its own server or database.

## Technical Overview

- C# / WPF / .NET 10
- SMTC access through `Windows.Media.Control`
- LRCLIB REST API
- Click-through behavior using Win32 extended window styles
- No external NuGet packages

See `THIRD_PARTY_NOTICES.md` for licenses and external-service notices.

## Disclaimer

FlowLyrics is an independent, unofficial application and is not affiliated with, sponsored by, or endorsed by Spotify AB or LRCLIB. Spotify is a trademark of Spotify AB. Lyrics and music remain the property of their respective rights holders.
