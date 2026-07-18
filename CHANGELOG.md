# Changelog

## Unreleased

### Added

- Added lightweight per-color blend modes: Normal, Auto, Invert, Screen, and Overlay.
- Added global and per-color blend-mode scopes in Custom Colors.
- Added the new FlowLyrics application icon and dot-font Settings wordmark.
- Added current time, track duration, and a Spotify-style seek hover timestamp to the playback bar.
- Added corrected dot-font punctuation and common accented Latin letters.

### Changed

- Advanced the `1.3.0` development cycle to confirmation build `1.3.0-dev.4`.
- Regenerable lyrics caches are isolated by development build while settings, manual LRCLIB selections, and the Local LRC folder remain shared.
- Replaced the Settings image wordmark with a lightweight two-color dot-font title.
- Simplified the version display to high-contrast text without a badge frame.
- Player controls and the unlocked lock button now use the selected Player UI color for their outlines.
- Settings now opens as a non-modal window so playback controls remain available; pressing Settings again applies changes and closes it.

### Fixed

- Restored WPF BAML and font packaging to the original `FlowLyrics.g.resources` layout so the portable EXE can initialize its application and windows correctly.
- Applied the Settings wordmark, version text, and blend-mode controls after the visual tree is loaded so they are always visible.
- Improved the Settings version alignment and contrast.
- Fixed parentheses and exclamation marks rendering as plus signs in the dot font.
- Fixed accented Latin characters falling back to a mismatched font.

### Notes

- No `1.3.0` release, Git tag, or GitHub Release is created until the release is explicitly approved.
