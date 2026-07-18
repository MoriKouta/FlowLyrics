# Changelog

## Unreleased

### Added

- Added named user color palettes with version-persistent storage and portable `.flowpalette` import/export.
- Added explicit title-only LRCLIB search, duration-prioritized candidate ordering, and contribution links for LRCLIB and LRCGET.
- Added a confirmed Reset All Settings action inside the Behavior tab.
- Added a synchronized Reverse Colors switch in Custom Colors and beside VOL.
- Added the new FlowLyrics application icon and dot-font Settings wordmark.
- Added current time, track duration, and a Spotify-style seek hover timestamp to the playback bar.
- Added corrected dot-font punctuation and common accented Latin letters.

### Changed

- Advanced the `1.3.0` development cycle to confirmation build `1.3.0-dev.7`.
- Reverse Colors now uses the dark Settings surface when off and the light surface when on; the Language selector remains dark and readable in the light state.
- Slider rails now keep a small rounded gap around borderless thumbs across Settings, seek, and volume controls.
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
- Reverse Colors now switches the Settings and LRCLIB candidate windows between coordinated light and dark surfaces while preserving the Player UI accent.
- Plain lyrics use a denser automatic layout to keep more lines visible in small windows.

### Fixed

- Improved VOL popup dismissal with continuous pointer monitoring and refined its horizontal alignment.
- Improved seek timestamp alignment above the pointer.
- Made Color Preset names consistently black in the English dot font.
- Restored WPF BAML and font packaging to the original `FlowLyrics.g.resources` layout so the portable EXE can initialize its application and windows correctly.
- Applied the Settings wordmark, version text, and Reverse Colors control after the visual tree is loaded so they are always visible.
- Improved the Settings version alignment and contrast.
- Fixed parentheses and exclamation marks rendering as plus signs in the dot font.
- Fixed accented Latin characters falling back to a mismatched font.
- Fixed the Custom Colors Reverse Colors control not appearing when the Color tab was opened after Settings startup.
- Fixed the VOL popup lingering after the pointer moved away or Settings was opened, and aligned it farther left above VOL.
- Kept the lyrics-source status dot unchanged when Reverse Colors is enabled.
- Removed the seek timestamp frame and centered the timestamp directly above the pointer.

### Notes

- No `1.3.0` release, Git tag, or GitHub Release is created until the release is explicitly approved.
