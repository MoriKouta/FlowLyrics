# FlowLyrics project instructions

## Scope

This repository contains FlowLyrics, a Windows desktop lyrics overlay application.

Keep changes focused on the requested task. Do not perform unrelated refactors,
renames, formatting passes, dependency upgrades, or UI changes unless required.

Inspect the relevant implementation and existing tests before modifying code.

## Git and working tree

- Work on the currently checked-out development branch unless the user explicitly requests a branch change.
- Do not push directly to `main`.
- Do not force-push, rewrite history, reset, clean, or discard existing user changes unless explicitly requested.
- Check `git status` before making substantial changes.
- Do not commit generated build output or local test assets.

## Local test assets

Machine-local test material lives under:

`.local/`

Typical locations:

- `.local/assets/exr/`
- `.local/assets/screenshots/`
- `.local/assets/audio/`
- `.local/assets/metadata/`
- `.local/logs/`
- `.local/scratch/`

These files are intentionally not version controlled.

Never add, copy, move, stage, commit, or upload `.local/` files to GitHub unless
the user explicitly requests a specific file to become a repository fixture.

When a task depends on local media or test material, inspect the available files
first rather than assuming filenames or contents.

Executables under `.local/builds/` are comparison baselines only. Do not
decompile them or reconstruct development source from them; editable Git source
is the sole development source.

## Build and tests

Primary build command:

`dotnet build .\FlowLyrics.csproj -c Release`

Primary test command:

`dotnet test .\FlowLyrics.Tests\FlowLyrics.Tests.csproj -c Release`

For focused changes, run the most relevant tests first.

For changes to shared metadata processing, lyrics matching, caching, media-session
handling, or provider normalization, run the complete FlowLyrics test project
before considering the task complete.

If a command cannot be run or fails because of the environment, report that
clearly instead of claiming validation succeeded.

## Implementation expectations

- Follow the existing C# style and project structure.
- Prefer small, reviewable changes.
- Preserve existing public behavior unless the task explicitly changes it.
- Add or update regression tests for bug fixes when practical.
- Avoid introducing one-off hard-coded exceptions when a general rule can solve
  the same class of problems.
- Do not add production dependencies unless they are necessary for the requested task.

## Lyrics and metadata matching

Raw provider metadata and interpreted metadata are different concepts.

Preserve original provider metadata whenever practical. Normalized titles,
aliases, performer identities, and other inferred values should be derived data,
not destructive replacements of the original values.

Automatic LRCLIB lyrics selection should prefer no automatic match over confidently
selecting the wrong recording.

Do not silently weaken title, artist, duration, version, or instrumental
validation merely to obtain a lyrics result.

Provider-specific metadata differences such as:

- single vs album release titles
- soundtrack or anime suffixes
- unit names vs voice-actor credits
- character names with CV credits
- browser or media-session metadata role ambiguity

should normally be handled through reusable normalization, aliases, identity
resolution, or candidate generation rather than song-specific hard-coded rules.

Matching behavior changes should include regression coverage for representative
metadata variants.

## Completion

Before finishing a coding task:

1. Review the resulting diff.
2. Run the relevant build/tests when possible.
3. Report what changed.
4. Report what validation was run.
5. Mention remaining uncertainty or unverified behavior.

Do not claim a task is complete when required validation has not been performed.
