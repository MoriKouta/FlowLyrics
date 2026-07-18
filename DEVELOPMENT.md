# FlowLyrics development

## Version policy

- The current public target is fixed at `1.3.0`.
- Every confirmation build increments `1.3.0-dev.N`.
- Regenerable cache data is isolated by `BuildInfo.CacheNamespace`.
- Settings, manual LRCLIB selections, and the Local LRC folder stay outside the development cache namespace.
- Development changes are recorded under `Unreleased` in `CHANGELOG.md`.
- Do not create a stable version, Git tag, or GitHub Release until the maintainer explicitly approves publication.

## Project layout

- `FlowLyrics/` contains the application source reconstructed from the `v1.2.9` portable build.
- `FlowLyrics.g.resources` contains the original WPF BAML and dot font in the resource layout required by `Application.LoadComponent`.
- Branding files are embedded separately so the tray icon and Settings wordmark can be loaded at runtime.

## Building

On Windows with the .NET 10 SDK, the project uses the Windows Desktop framework references supplied by the SDK:

```powershell
dotnet restore .\FlowLyrics\FlowLyrics.csproj
dotnet build .\FlowLyrics\FlowLyrics.csproj -c Release
```

The recovery workspace can set `UseRecoveredReferences=true` to build against reference assemblies extracted from the known-good `v1.2.9` self-contained executable.

The portable EXE is rebuilt from the known-good self-contained `v1.2.9` host so its .NET 10.0.10 runtime remains unchanged. The main assembly must remain the first bundle entry.
