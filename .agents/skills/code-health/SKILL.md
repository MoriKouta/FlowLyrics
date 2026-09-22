---
name: code-health
description: Audit FlowLyrics architecture, refactoring opportunities, technical debt and maintainability, including health checks after a large feature series. Use for these engineering tasks, not routine feature edits or cosmetic formatting.
---

# FlowLyrics code health

Follow the current user request and repository AGENTS.md. This workflow does not
expand authorization: an audit-only or no-commit request stays read-only.
Preserve behavior; report bugs separately from refactors. Do not grade the project
with a subjective score.

## Workflow

1. **Git and scope.** Run `git status --short --branch`, `git diff`,
   `git diff --staged`, and `git log -10 --oneline --decorate`. Identify existing
   user work. Use the checked-out development branch. Never discard work, push
   to main, force-push, or include `.local/` or generated output.
2. **Baseline.** Read project/analyzer settings and relevant tests. Run
   `dotnet build .\FlowLyrics.csproj -c Release` and
   `dotnet test .\FlowLyrics.Tests\FlowLyrics.Tests.csproj -c Release`.
   Record SDK, warning IDs/counts, failures and skipped tests. If a running app
   locks output, use a documented ignored `--artifacts-path`; do not kill it.
   Distinguish incremental build results from compiler/analyzer results.
3. **Audit before editing.** Map responsibility boundaries and state ownership,
   then inspect callers and failure paths. Review the checklist below. File and
   method sizes identify where to look; they are not defects by themselves.
4. **Classify evidence.** P0/Critical = confirmed urgent bug, data loss or unsafe
   behavior; P1/High = fragile structure with a concrete failure/change path;
   P2/Medium = maintainability debt without an urgent failure; P3/Low = preference.
   Separate confirmed defects, structural risks and unproven hypotheses. For each
   finding cite a file/member, trigger, consequence, test coverage and disposition.
5. **Plan a small refactor.** Choose only changes in scope with a clear benefit
   and low/moderate execution risk. State invariants, ownership before/after,
   affected callers and tests. Protect risky paths with characterization tests
   before moving code. Do not combine a behavior fix with an extraction.
6. **Change incrementally.** Complete one responsibility at a time. Preserve
   API/serialization/migration contracts. No wholesale MVVM, namespaces, UI or
   formatting rewrite, speculative interfaces, or new DI framework.
7. **Focused tests.** Exercise affected normal/error/cancellation paths and the
   public boundary. A bug fix should have a regression that fails before the fix.
   Avoid tests that only assert source spelling or reproduce the implementation.
8. **Full validation.** Run the full test project and Release build for each code
   checkpoint. Compare with the baseline: no new warnings or failures. Repeat
   only when a new edit/failure warrants it. UI behavior requires runtime WPF
   checks; automated/fake-player results are not real-player acceptance.
9. **Diff review.** Inspect the entire intended diff and `git diff --check`.
   Check for changed matching/ordering/timing/exception behavior, hidden side
   effects, lost cancellation and lifetime ownership, and accidental fixtures.
10. **Report.** Update `docs/CODE_HEALTH.md` (date and audited revision; prior
    findings are leads, not current truth). Explain each meaningful change as
    problem / why it matters / what changed / benefit. Include largest files,
    new/extracted responsibilities, duplication, dead-code evidence, new debt,
    analyzer baseline, build/tests, deferred risks and next bounded refactor.
    Keep machine-specific logs under `.local/` and secrets out of reports.
11. **Checkpoint.** When authorized by the request/repository rules, stage only
    intended files, use a focused docs/refactor/fix commit, push to the current
    development branch, then verify status and local HEAD against remote SHA.
    Do not publish unfinished or unvalidated changes as completed work.

## Audit checklist

- **Boundaries:** large classes/methods, multiple reasons to change, dependency
  direction/cycles, tight coupling, unnecessary abstractions, provider-specific
  rules, naming that obscures responsibility. Prioritize MainWindow,
  SettingsWindow, PersonalSyncWindow and LyricsService when affected.
- **State:** identify the owner of current track/session, lyrics, pending metadata,
  sync profile/preview, repeat, stop reservation and settings preview/visibility.
  A snapshot or edit draft is not automatically harmful duplication: verify how
  it is refreshed, validated and committed.
- **Duplication:** business rules, UI styles, timing/config values and cache or
  persistence policy. Merge only equivalent semantics. HTTP response caches,
  validated lyrics caches and durable user choices have different purposes.
- **Lifetime:** async void versus true event handlers; fire-and-forget tasks and
  exception observation; cancellation; late results after track/window changes;
  timers, events, locks, thread safety, Dispose and shutdown.
- **UI thread:** synchronous network/disk, parsing, UIA and large tree scans;
  control updates must remain on their Dispatcher. Verify completed-task guards
  before calling a GetResult site a blocking bug.
- **Errors/persistence:** swallowed exceptions versus intentional best-effort
  fallback; memory/disk agreement after failed writes; atomic replacement;
  cache invalidation and migration; raw provider metadata preservation.
- **Legacy/dead code:** check runtime wiring, embedded BAML/XAML connectors,
  reflection tests, serialization and old settings before deletion. The editable
  XAML is not currently compiled: source search alone cannot prove a handler dead.
- **Presentation:** untranslated actions versus deliberately fixed English
  headings, reusable style responsibilities and user-selected lyric fonts.
  Do not change layout or visuals as an incidental refactor.

Use the project shape as a guide: Models = data; Core = domain calculations;
Services = external systems/persistence/orchestration; Controls = reusable WPF;
Windows = composition/wiring. Do not move files merely to satisfy this diagram.
