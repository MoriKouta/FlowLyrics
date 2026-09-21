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

Personal Sync uses one time/lyric editing surface. The selected row owns its align/resume action; drag a lyric handle onto the current-position marker to create a reversible alignment. Clicking a lyric time requests a source seek. More exposes lyric hold and whole-track alignment; Adjustment points exposes detailed edits. Ctrl+Z/Ctrl+Y remain available. Close saves edits atomically; opening and closing without edits creates no profile. Current Track keeps source details visible and has no duplicate Timing action.

Glow is a retained blurred visual behind sharp glyphs, excluded from text measurement and fit. Its radius never changes layout padding or viewport margins. Parent viewport/window boundaries still limit effect bleed.

Diagnostics write `AUDIO WRITE` only at actual volume/mute calls, including timestamp, source, action, value and reason. `UIA scan-start/scan-end` includes scan duration, whole-process CPU delta and working set; these metrics do not prove audio dropouts or isolate UIA CPU. Successful credits use a 30-second visible recheck interval, with immediate revalidation on window-state changes and no minimized scans. Search logs include stage start/response, candidate count, first-safe and total elapsed time. No dummy query cache busting is used.
