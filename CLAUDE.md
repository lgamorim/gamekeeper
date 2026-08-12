# GameKeeper

GameKeeper is a single-shot Windows CLI that two-way syncs a local game-save
folder with a cloud-synced folder. Single deployable, developed solo. Milestone
plan and scope: see `ROADMAP.md`.

## Build & test
- `dotnet build`
- `dotnet test`

## Shared conventions
@.claude/rules/profiles/application-solo.md

## Project-specific notes
- Layout: `GameKeeper.slnx` at the root; the sync engine (App-agnostic) in
  `src/GameKeeper.Core/`, the CLI and composition root in
  `src/GameKeeper.App/`; tests in `test/GameKeeper.Core.UnitTests/` and
  `test/GameKeeper.App.UnitTests/`.
- Cross-milestone contracts: exit codes `0` success / `1` sync failed /
  `2` unusable command line; reserved on-disk names `.gamekeeper-backups`
  (backup folder), `.gamekeeper-tmp` (staging suffix), and sync state under
  `%LOCALAPPDATA%\GameKeeper\state`.
- At the end of every milestone: update `README.md` so it always documents the
  feature surface that actually exists, and bump `<Version>` in
  `Directory.Build.props`.
