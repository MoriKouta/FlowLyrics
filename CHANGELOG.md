# Changelog

## Unreleased

### Added

- Added lightweight per-color blend modes: Normal, Auto, Invert, Screen, and Overlay.
- Added global and per-color blend-mode scopes in Custom Colors.
- Added the new FlowLyrics icon and settings wordmark.

### Changed

- Advanced the `1.3.0` development cycle to confirmation build `1.3.0-dev.3`.
- Regenerable lyrics caches are isolated by development build while settings, manual LRCLIB selections, and the Local LRC folder remain shared.
- Improved the settings version badge alignment and contrast.

### Fixed

- Restored WPF BAML and font packaging to the original `FlowLyrics.g.resources` layout so the portable EXE can initialize its application and windows correctly.
- Applied the Settings wordmark, version badge, and blend-mode controls after the visual tree is loaded so they are always visible.
- Improved the version badge alignment and changed its text to white on a dark surface with an accent border.

### Notes

- No `1.3.0` release, Git tag, or GitHub Release is created until the release is explicitly approved.
