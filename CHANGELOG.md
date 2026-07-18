# Changelog

## Unreleased

### Added

- Added a synchronized Reverse Colors switch in Custom Colors and beside VOL.
- Added the new FlowLyrics application icon and dot-font Settings wordmark.
- Added current time, track duration, and a Spotify-style seek hover timestamp to the playback bar.
- Added corrected dot-font punctuation and common accented Latin letters.

### Changed

- Advanced the `1.3.0` development cycle to confirmation build `1.3.0-dev.5`.
- Regenerable lyrics caches are isolated by development build while settings, manual LRCLIB selections, and the Local LRC folder remain shared.
- Replaced the Settings image wordmark with a lightweight two-color dot-font title.
- Simplified the version display to high-contrast text without a badge frame.
- Player controls and the unlocked lock button now use the selected Player UI color for their outlines.
- Settings now opens as a non-modal window so playback controls remain available; pressing Settings again applies changes and closes it.
- Player controls now use the same translucent light surface as the unlocked lock button.
- Settings now uses a softer light-gray surface with restrained dark text.
- Plain lyrics now display as a continuously scrolling full text instead of estimated active-line timing.
- Reverse Colors uses a lightweight RGB inversion for overlay content while preserving the Player UI accent and Settings theme.
- Removed blend modes and all background-screen sampling.

### Fixed

- Restored WPF BAML and font packaging to the original `FlowLyrics.g.resources` layout so the portable EXE can initialize its application and windows correctly.
- Applied the Settings wordmark, version text, and Reverse Colors control after the visual tree is loaded so they are always visible.
- Improved the Settings version alignment and contrast.
- Fixed parentheses and exclamation marks rendering as plus signs in the dot font.
- Fixed accented Latin characters falling back to a mismatched font.

### Notes

- No `1.3.0` release, Git tag, or GitHub Release is created until the release is explicitly approved.
