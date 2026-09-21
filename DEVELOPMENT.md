# FlowLyrics development

## Version policy

- The current stable release is `1.3.0`.
- The current development target is fixed at `1.3.1`.
- Every confirmation build increments `1.3.1-dev.N`.
- Regenerable cache data is isolated by `BuildInfo.CacheNamespace`.
- Settings, manual LRCLIB selections, and the Local LRC folder stay outside the development cache namespace.
- Development changes are recorded under `Unreleased` in `CHANGELOG.md`.
- Do not create a stable version, Git tag, or GitHub Release until the maintainer explicitly approves publication.

## Project layout

- `FlowLyrics/` contains the canonical editable application source.
- `FlowLyrics.g.resources` contains the original WPF BAML and dot font in the resource layout required by `Application.LoadComponent`.
- The application icon is embedded separately; the Settings wordmark is rendered from the bundled dot font.
- The Reverse Colors controls are injected after WPF load so the recovered BAML layout and editable source stay compatible; both controls bind to the same `AppSettings.ReverseColors` value.
- Reverse Colors only transforms existing RGB values. It does not sample the desktop or use backdrop compositing.
- Never use a portable package, executable, `bin`, `obj`, or `publish` output as the source of a later development build. If this source tree is unavailable, stop instead of reconstructing it from a binary.

## Building

On Windows with the .NET 10 SDK, the project uses the Windows Desktop framework references supplied by the SDK:

```powershell
dotnet restore .\FlowLyrics.csproj
dotnet build .\FlowLyrics.csproj -c Release
dotnet test .\FlowLyrics.Tests\FlowLyrics.Tests.csproj -c Release --logger "console;verbosity=normal"
```

The compatibility workspace can set `UseRecoveredReferences=true` to compile against its local Windows Desktop reference set. This is a build-environment compatibility path only; the repository source tree remains the sole development source.

If a running local app locks the normal output, use `--artifacts-path .local/builds/validation` for build/test, or publish to `.local/builds/local-current`. Keep these outputs out of Git.

## Runtime UI checks

The editable XAML is not recompiled by this project. Windows load the embedded BAML and attach newer controls in C#. Verify changes by opening the window and selecting its tabs, including after localization; constructor-only tests do not exercise `Loaded` or the selected content's visual tree. Runtime Settings attachment uses named BAML controls and the completed logical tree, not translated labels or fixed tab/row indices.

## Metadata and saved identity

Raw GSMTC fields remain separate from repaired/canonical, display and search data. Recording identity is a comparison of evidenced title aliases, artist relationships, edition/instrumental state and source-aware duration. Album agreement is a ranking hint.

`StableIdentityKey` retains its existing title/artist/album/rounded-duration hash; `StableSourceMetadataKey` names this persistence role explicitly. Display enrichment and added search aliases do not change it. Single and album releases can match the same LRCLIB recording while keeping separate saved source metadata keys. They are not automatically merged: a Personal Sync source may have a different timeline. Existing cache-key/provider-ID fallbacks remain available, and looking up another release does not remove saved selections or profiles. Any future shared recording key must add a fallback migration before changing stored keys.

Automatic lyric caches use matcher version 8. Manual selections, local LRC, settings and Personal Sync are retained independently. Explicit refresh/search/ID load bypass FlowLyrics' HTTP response cache without adding cache-busting parameters. LRCLIB's server-side cache can still return the same response; direct ID loading is available for newly published records.

Spotify UI Automation is optional and read-only. Confirmed credits are cached only in session memory under raw source/title/album/artist/duration identity, with no persistent-key changes. Minimized/offscreen windows reuse only that exact track's credit and do not trigger tree scans or window restoration. Visible windows are rechecked. No OAuth or Spotify API dependency is required.

## dev.11 timing UI and diagnostics

Technical UI uses `LocalizedUiFont`: the bundled Flow Dots for English, the existing CJK families for Japanese/Chinese/Korean, and Segoe UI for the other supported languages. Resource fonts created from C# require a base URI plus a relative resource reference. `LocalizationRuntimeTests` checks the resolved glyph resource, all ten languages, label coverage, language changes and provider values that happen to equal UI keys. The optional `FLOWLYRICS_NATIVE_CAPTURE_DIR` captures actual test windows; transparent overlays get an opaque test backdrop to exclude unrelated desktop content.

`EditorUiTranslations` completes the editor/search/shared labels previously limited to English/Japanese. Diagnostics payload field names, raw metadata, product names, palette names and exception details remain technical data. Normal headings, actions, matching summaries and cache/selection statuses go through `LocalizationService`; use `HasTranslation` to detect missing resources without rejecting valid identical spellings.

Track changes are driven by GSMTC metadata events with a short debounce and two consistent reads (at least 110 ms apart); 550 ms polling remains reconciliation. Pending metadata keeps the selected player while hiding old lyrics. A missing session has a 400 ms grace period. Exact provisional metadata starts a cache-only lookup; only a matching stable identity can consume it. Parsed positive lyrics use a 64-track LRU, invalidated by source/manual/cache changes and local-LRC watcher events. Source and exact duration are part of the in-process key; persistent keys and matcher safety are unchanged.

`lyrics-performance` logs buffer stage timestamps until first render, including `TrackChangeToFirstLyricsMs`, metadata/cache/network/profile stages, and full-UI completion. Profile data is preloaded and resolved before lyrics render. UI Automation runs outside the metadata/cache critical path. `FLOWLYRICS_PERF_REPORT` optionally writes the cached-track WPF benchmark report; this uses synthetic GSMTC inputs and is not a live-player latency guarantee.

Lyric glow is rendered only by `LyricGlowOverlay`, a sibling of the foreground ScrollViewer. It reuses the exact `OutlinedText.GlyphGeometry` and its transformed position; the silhouette is clipped before blur, while the halo is outside the viewport clip. A fixed 48-DIP transparent window perimeter enlarges the native surface without reducing the saved logical viewport. Window placement/size persistence excludes this perimeter. Glow strength never changes text measurement, wrapping, spacing or AutoFit. `FLOWLYRICS_NATIVE_CAPTURE_DIR` enables actual desktop captures of the dedicated glow fixtures; generated images remain local.

Personal Sync keeps lyric rows at a fixed height and reveals compact actions on hover, selection or keyboard focus. Align to now always shifts the entire existing timeline by the difference between now and the selected lyric's mapped playback position. It moves offsets and playback coordinates together, preserving lyric timestamps, hold lengths and relative edits. Only the explicit local re-sync action creates an anchor. Negative edit coordinates after a global shift are preserved across saves. Rows expose only primary alignment; local re-sync and selected-hold resume are explicit inspector actions. A separate, proportional duration rail supports seek preview/release, point dragging and hold-range handles; lyric rows remain equally spaced and show their mapped times. Holds continue to display one lyric, preserving existing profile semantics. Range-end edits keep the linked resume point synchronized. Each drag is one Undo operation. Wide windows show a contextual right inspector; narrow windows can open it below the timeline. Selecting a point/range opens its inspector and retains that target while choosing a lyric. There are no lyric-row overflow menus. Manual scrolling suspends follow for five seconds. Close still saves atomically and untouched profiles are not saved. Space toggles playback, arrows select lyrics, Enter aligns, Delete removes the selected point/range, and Ctrl+Z/Ctrl+Y undo/redo. The focused seek rail uses arrows/Home/End for source seeking.

Diagnostics write `AUDIO WRITE` only at actual volume/mute calls, including timestamp, source, action, value and reason. `UIA scan-start/scan-end` includes scan duration, whole-process CPU delta and working set; these metrics do not prove audio dropouts or isolate UIA CPU. Successful credits use a 30-second visible recheck interval, with immediate revalidation on window-state changes and no minimized scans. Search logs include stage start/response, candidate count, first-safe and total elapsed time. No dummy query cache busting is used.
