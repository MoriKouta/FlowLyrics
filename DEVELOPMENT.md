# FlowLyrics development

## Version policy

- The current public target is fixed at `1.3.0`.
- Every confirmation build increments `1.3.0-dev.N`.
- Regenerable cache data is isolated by `BuildInfo.CacheNamespace`.
- Settings, manual LRCLIB selections, and the Local LRC folder stay outside the development cache namespace.
- Development changes are recorded under `Unreleased` in `CHANGELOG.md`.
- Do not create a stable version, Git tag, or GitHub Release until the maintainer explicitly approves publication.

## Project layout

- `FlowLyrics/` contains the canonical editable application source.
- `FlowLyrics.g.resources` contains the original WPF BAML and dot font in the resource layout required by `Application.LoadComponent`.
- The application icon is embedded separately; the Settings wordmark is rendered from the bundled dot font.
- Never use a portable package, executable, `bin`, `obj`, or `publish` output as the source of a later development build. If this source tree is unavailable, stop instead of reconstructing it from a binary.

## Building

On Windows with the .NET 10 SDK, the project uses the Windows Desktop framework references supplied by the SDK:

```powershell
dotnet restore .\FlowLyrics\FlowLyrics.csproj
dotnet build .\FlowLyrics\FlowLyrics.csproj -c Release
```

The compatibility workspace can set `UseRecoveredReferences=true` to compile against its local Windows Desktop reference set. This is a build-environment compatibility path only; the repository source tree remains the sole development source.
