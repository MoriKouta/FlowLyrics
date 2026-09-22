# Changelog

## Unreleased

### Changed

- Match neutral R/S icon brightness, compact Repeat/Settings dot geometry and utility button metrics; prevent overlapping player controls in narrow windows.
- Reduce nested editor card/button rounding and share dark tooltip styling while keeping active states distinct.
- Add compact PLAYER CONTROLS visibility toggles with live preview, saved choices, and an explicit Cancel action that restores the prior settings.

- Add observed GSMTC Off/List/Track Repeat and a one-shot Stop After Track action in Personal Sync, with capability checks, repeat-off confirmation, conservative natural-end detection and pause/stop fallback.

- Keep Personal Sync open across track/source changes, preserve confirmed edits, discard unfinished gestures, and populate the next track after lyric rendering. Add contextual global alignment and nudges to the inspector.

- Restore fixed English visual headings and Flow Dots independently of localized body fonts; keep the overlay's user-selected lyric font untouched.

- Preserve existing sync points and lyric holds when nudging the global offset from the overlay's timing commands or the sync history window.

- Unify technical UI fonts across all windows, load the bundled Flow Dots through a valid resource URI, and localize Personal Sync, search, settings headings and status labels in all ten languages. Keep provider values separate from UI labels and let longer actions wrap.

- Remove Personal Sync lyric-row overflow menus. Keep one primary action per row and put re-sync, resume and deletion on the selected object's inspector, with a compact lower inspector on narrow windows.

- Make Personal Sync's primary alignment translate the entire existing timeline without adding rewind anchors; preserve hold durations and negative shifted edit coordinates across saves.

- Render lyric glow in a separate sibling layer outside the scrolling text viewport, keeping glyph layout unchanged at every strength and reserving a fixed outer window bleed without reducing logical content size.

- Keep Personal Sync rows fixed while showing hover actions, replace inline expansion with contextual menus, and add a proportional vertical seek rail with draggable sync points and lyric-hold ranges. First alignment sets the global offset; later points preserve earlier timing. Add a responsive inspector, visible correction values, temporary follow suspension, and one-step drag undo.

- Preserve the selected media session through transient metadata, confirm consecutive reads after a short event-driven debounce, and hide previous lyrics without an idle-player flash. Speculate exact cache reads during stabilization, reuse up to 64 parsed lyric results, resolve Personal Sync before first paint, and record first-render timings.

- Prioritize evidenced search identities before weak hints, preserve safe early exit, and log query stages, candidate counts and first-safe/total latency. Log actual volume/mute writes and UIA scan costs without changing audio control logic; reuse successful visible Spotify credits for 30 seconds.

- Integrate Personal Sync lyric rows and playback coordinates in one editing surface, with row-local align/resume, drag-to-current alignment, click-to-seek, Ctrl+Z/Ctrl+Y, and optional hold/point controls. Keep non-destructive save-on-close and untouched-profile behavior.
- Keep the Personal Sync Settings entry and player S button; remove the duplicate Current Track timing action. Keep lyrics details visible and reduce Local LRC to a compact choose/use row, preserving files, saved LRCLIB selections and older cache-only imports.
- Separate the retained Gaussian glow silhouette from text layout: radius no longer changes fit, wrapping, glyph position or viewport margins; allow effect bleed outside text controls. Unify ID/search/candidate/preview and disclosure controls.
- Added `1.3.1-dev.11` with conservative recording identity, explicit release/title/CV aliases, source-aware duration validation, and complete artist-credit display.
- Repair runtime BAML attachment of Glow, My Palettes and Reverse Colors. The Color tab now exposes Text Effects and Surface cards independently of tab order or localization, with loaded-window and settings/palette round-trip regression coverage.
- Retain confirmed Spotify credits in a bounded session-memory cache while the same raw track is minimized, expose Spotify window state, and refresh credits on return without restoring or focusing Spotify.
- Bypass the client HTTP cache for explicit lyric refresh, candidate search and ID loading. Add direct LRCLIB ID preview/manual selection and localized guidance about server-side search caching, without artificial cache-busting queries.
- Recognize explicit quoted Soundtrack release suffixes, retain existing persistence keys and legacy fallbacks, and expose title aliases, search artists, release/edition identity and inference evidence in diagnostics. Upgrade automatic-cache matcher validation to version 8.
- Wait for Personal Sync persistence before closing the editor so an asynchronous save failure can keep the window open. Add loaded-window hold/resume/edit/undo/redo/reopen and no-op regressions.
- Require matching title or an evidenced alias, strong artist identity, compatible edition and instrumental state, and valid duration for every automatic LRCLIB selection. Album agreement affects ranking without being required.
- Retain complete raw title/artist/album values and source identity separately from display and search metadata. Browser artist credits and bilingual titles use only evidence present in the provider metadata.
- Distinguish named remixers, language variants, re-recordings, and other edition markers; cross-script artist differences alone no longer establish identity.
- Keep the two-second duration tolerance for audio/unknown sources. A longer browser video may differ by at most 90 seconds and 30% of the recording duration, only with strong title/artist identity and no edition conflict.
- Separate overlay title and artist text, and bound wrapped artist credits to two lines in both the overlay and Settings without splitting artist names.
- Add optional Spotify now-playing UI Automation artist-credit enrichment after inspecting GSMTC fields. Preserve raw metadata and the original matching candidate, validate the current title/process/credit region, and fall back without blocking media polling. Diagnostics include raw album artist, subtitle, genres, track number, display artist, and enrichment source.
- Started the `1.3.1` development cycle with confirmation build `1.3.1-dev.1`.
- Added the `1.3.1-dev.2` confirmation build with generic Windows Media Session support.
- Rebuilt the same implementation as `1.3.1-dev.3` for a fresh downloadable confirmation package.
- Added `1.3.1-dev.4` with the PLAYER source selector at the top of Lyrics, immediate source switching, and collapsed exclusion controls.
- Added `1.3.1-dev.5` with resilient LRCLIB requests, verified multi-player audio/seek fallbacks, and refined compact controls.
- Added `1.3.1-dev.6` with provider-scoped Apple Music and browser metadata repair plus a neutral EXCLUDE toggle.
- Added `1.3.1-dev.7` with ranked multilingual YouTube metadata interpretations, corrected populated Apple Music fields, dotted EXCLUDE disclosure, and anchored plain-lyrics auto-scroll resume.
- Added `1.3.1-dev.8` with non-destructive Personal Sync profiles, source-aware fallback, line alignment, undo/redo, advanced anchors and lyric-hold ranges, profile management, and diagnostics.
- Added `1.3.1-dev.9` with a movable lyrics-first Personal Sync editor, searchable visual sync history, a compact player `S` control, and customizable behind-text Glow.
- Added `1.3.1-dev.10` with a press-to-hold/select-to-resume Personal Sync workflow, top-to-bottom visual timing flow, button-only point editing, source-aware history, matched `S`/`R` player glyphs, and guaranteed separate Glow/Text Effects/Surface controls.
- Replaced Spotify-only GSMTC discovery with a platform-neutral provider contract, stable AUTO selection, Preferred Player fallback, source blacklist, metadata stabilization, and capability-aware controls.
- Added player/source labels, a live Media Session Diagnostics window, raw/normalized metadata inspection, and clipboard-safe diagnostics.
- Reworked LRCLIB candidate search into sequential full-fields, title/artist, and title-only requests with exact query encoding, ID deduplication, safe metadata normalization fallback, and detailed bounded HTTP diagnostics.
- Made lyrics cache and manual LRCLIB overrides player-independent while retaining and migrating legacy keys.
- Centered the volume popup precisely and made the active Reverse Colors button visually explicit.
- Stabilized Apple Music timelines, normalized non-zero media timeline origins, restored optimistic seeking, and prevented stale post-seek positions from snapping back.
- Earlier development builds auto-applied the highest-scoring usable LRCLIB candidate after strict checks failed; dev.11 removes that fallback and requires every automatic result to pass the safety gates.
- Generalized per-session volume and mute control from Spotify to the selected player, including Apple Music, TIDAL, VLC, and major browsers.
- Made Lyrics Only a persistent visual mode that preserves and disables the underlying component choices instead of clearing them.
- Kept Personal Sync data in a separate atomic JSON store so LRCLIB responses, local LRC files, and lyrics-cache timestamps are never rewritten.
- Split text effects from window surfaces in Settings, and extended curated, random, saved, imported, and exported palettes with Glow color, strength, and opacity.

### Fixed

- Removed the unsafe BEST MATCH fallback and its cache-validation bypass. MatcherVersion 7 rejects old automatic cache entries while retaining explicit manual selections and local LRC data.
- Preserve version/featured-artist suffixes during search normalization and keep ambiguous metadata-role interpretations as search hints unless independent artist evidence is available.
- Retried slow, rate-limited, malformed, and transient LRCLIB responses with longer bounded timeouts, per-request coalescing, server backoff, and partial-result preservation.
- Matched packaged audio sessions by their real process AUMID so Apple Music and other Store players can use per-session volume controls.
- Sent track-relative GSMTC seek positions first and added a conservative UI Automation range fallback for players that expose a timeline but reject the system seek command.
- Device-pixel calibrated the entire volume popup frame directly over the volume button and removed the layout margin that could bias its visible surface.
- Changed EXCLUDE to a frameless accordion header with leading `▶`/`▼` state icons and unframed details.
- Removed the native blue hover surface from EXCLUDE and kept its label white in both accordion states.
- Recovered Apple Music albums embedded in its artist field and conservatively extracted credited YouTube titles/artists from official-video naming patterns.
- Moved Lyrics Only directly above Border Width and made a second press restore the previously selected component states.
- Prevented Personal Sync from creating a saved profile when its editor is opened and closed without an edit.
- Placed the Personal Sync editor beside the lyric overlay when screen space permits, while retaining a normal draggable window for manual placement.

### Tests

- Added recording-identity, title-credit, CV/unit-credit, edition, duration, raw/display preservation, simulated LRCLIB/cache, and WPF layout regressions, including actual Settings BAML construction.
- Added automated coverage for provider metadata repair, metadata normalization, source-independent identity, LRCLIB query encoding and best-match fallback, immediate source selection, Apple-style timeline jitter and seeking, audio-session identity matching, capability propagation, and legacy cache migration.
- Added Personal Sync coverage for no-op mapping, positive/negative offsets, arbitrary seeking, anchors, lyric holds, source precedence, lyrics-ID mismatch protection, persistence, and timestamp immutability.
- Added Glow normalization/preset coverage and a persistence regression test for untouched Personal Sync profiles.

## 1.3.0 - 2026-07-20

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

- Released FlowLyrics `1.3.0`.
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
