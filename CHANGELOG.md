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
- Added a persistent Show All Lyrics setting beside plain-lyrics auto-scroll.
- Added compact rounded dropdowns with selected-item dots and chevrons for language, font, and other selectors.
- Added dot-style volume and contrast icons to the player.
- Added compact toggle selectors for text alignment and active-line position.

### Changed

- Advanced the `1.3.0` development cycle to confirmation build `1.3.0-dev.16`.
- Reverse Colors now uses the dark Settings surface when off and the light surface when on; the Language selector remains dark and readable in the light state.
- Slider rails now keep a small rounded gap around borderless thumbs across Settings, seek, and volume controls.
- Regenerable lyrics caches are isolated by development build while settings, manual LRCLIB selections, and the Local LRC folder remain shared.
- Replaced the generated volume symbol with the supplied `volume.png` artwork as a lightweight color-aware mask, and changed Reverse Colors to a bold dotted `R`.
- Updated the LRCLIB/LRCGET contribution prompt in every supported language to invite users to create synchronized lyrics when no good match exists.
- Reduced the lock, Reverse Colors, volume, and Settings controls as a coordinated compact group; optically centered the dotted `R`; and refined the volume popup outline.
- Narrowed the volume popup surface while preserving its vertical control range, and optically shifted the lock and Settings glyphs left inside their compact buttons.
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
- Moved full-lyrics display out of the player and into Settings, next to plain-lyrics auto-scroll.
- Simplified Reset Settings into a compact single-row control.

### Fixed

- Deferred expensive lyric and player layout refreshes until interactive window resizing finishes for smoother native resizing.
- Reduced Show All Lyrics rendering work from every frame across every line to updates only when the active line changes.
- Rebuilt the volume symbol on one uniform dot grid and enlarged the evenly spaced Reverse Colors dots.
- Fixed Show All Lyrics becoming blank when opening, applying, or closing Settings by invalidating the full-layout cache before rebuilding lyric controls.
- Rebalanced and reduced the dot contrast and volume icons using consistent dot size and spacing.
- Matched the volume popup surface to the translucent player-button surface in both normal and Reverse Colors modes.
- Kept Show All Lyrics enabled when Settings is closed from the title bar or reopened.
- Made compact dropdown text follow Reverse Colors and moved the selected-value dot before its label.
- Rounded the LRCLIB full-search and title-only action buttons.
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
- Kept Show All Lyrics active when Settings is opened, previewed, or closed.

### Notes

- No `1.3.0` release, Git tag, or GitHub Release is created until the release is explicitly approved.
