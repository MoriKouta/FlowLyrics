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
- Added a plain-lyrics auto-scroll setting that pauses after manual scrolling.
- Added an overlay `ALL` control that fits the complete lyric text into the current window.
- Added compact toggle selectors for text alignment and active-line position.

### Changed

- Advanced the `1.3.0` development cycle to confirmation build `1.3.0-dev.9`.
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
- Merged My Palettes into Custom Colors directly below Player UI.
- Replaced the visible-lines control with automatic lyric-window sizing.
- Replaced native Settings scrollbars with slim Player UI-colored faders.
- Unified the redundant Cancel and Close actions into one Close button.

### Fixed

- Improved VOL popup dismissal with continuous pointer monitoring and refined its horizontal alignment.
- Improved seek timestamp alignment above the pointer.
- Kept Color Preset names in the English dot font without changing their size.
- Restored WPF BAML and font packaging to the original `FlowLyrics.g.resources` layout so the portable EXE can initialize its application and windows correctly.
- Applied the Settings wordmark, version text, and Reverse Colors control after the visual tree is loaded so they are always visible.
- Improved the Settings version alignment and contrast.
- Fixed parentheses and exclamation marks rendering as plus signs in the dot font.
- Fixed accented Latin characters falling back to a mismatched font.
- Fixed the Custom Colors Reverse Colors control not appearing when the Color tab was opened after Settings startup.
- Fixed the VOL popup lingering after the pointer moved away or Settings was opened, and aligned it farther left above VOL.
- Kept the lyrics-source status dot unchanged when Reverse Colors is enabled.
- Removed the seek timestamp frame and centered the timestamp directly above the pointer.
- Fixed unreadable selection text in language, font, alignment, and other Settings dropdowns without tying it to Reverse Colors.
- Fixed named palette controls and the confirmed Behavior reset action not being inserted when tab headers had already been localized.
- Fixed Color Preset names so they switch between white and black with the Settings surface under Reverse Colors.
- Removed the remaining runtime slider-thumb outlines and preserved rounded rail gaps in Settings, seek, and volume sliders.
- Simplified the LRCLIB contribution hint to unobtrusive text links without a framed panel.
- Renamed the per-track reset action to Clear selection and cache and made it remove both the manual override and only that track's cache.
- Shifted the seek hover timestamp farther right to align above the pointer.
- Fixed Color Preset labels at their logical XAML source so Reverse Colors and the English dot font apply regardless of tab realization timing.
- Fixed the VOL hover gap between the button and popup so the slider remains reachable without delaying dismissal elsewhere.
- Removed transient Working and Updated messages after clearing a track selection/cache.

### Notes

- No `1.3.0` release, Git tag, or GitHub Release is created until the release is explicitly approved.
