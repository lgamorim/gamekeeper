# Roadmap

GameKeeper is a single-shot Windows CLI that keeps a local game-save folder and
a cloud-synced folder in sync: two-way by default with the newer copy winning,
additive unless deletions are explicitly enabled, and with anything destructive
backed up first.

Each milestone below is one `feature/` branch, squash-merged into `master` when
complete, per this repo's workflow rules.

## Milestones

1. **Scaffold + CLI surface** — solution layout (`src/`, `test/`, `.slnx`,
   `Directory.Build.props`, `.editorconfig`), argument parsing (positional and
   named forms, `--help`, `--version`), the exit-code contract (`0` success,
   `1` sync failed, `2` unusable command line), and CI (windows-latest
   build + test).
2. **Sync engine core** — file comparison with newer-wins copying, first-run
   baseline, machine-local sync state, and `--mode both|up|down`.
3. **Deletions + conflicts** — deletion detection from recorded state,
   `--delete` opt-in (additive by default), conflict detection and resolution.
4. **Safety net** — backups of overwritten and deleted files
   (`.gamekeeper-backups/`, `<name>.<timestamp>.bak`, UTC timestamps),
   `--no-backup`, `--keep-backups` pruning, and the mass-deletion guard
   (refuse a run deleting more than half of tracked files, `--force` to
   override).
5. **Filters + dry-run** — `--include`/`--exclude` glob patterns, empty-folder
   mirroring, and a full `--dry-run` preview. Tag **`1.0.0-beta.1`**.
6. **Profiles + config** — `config.json` with a `profiles` array, `%NAME%`
   environment-token expansion in paths, `--profile`/`--all`,
   `--config`/`--state-dir` overrides, and portable mode (`GameKeeper-data`
   next to the exe) with `%APPDATA%\GameKeeper` /
   `%LOCALAPPDATA%\GameKeeper` fallback.
7. **Release engineering** — version and commit stamping, tag-driven release
   job, user documentation. Tag **`1.0.0-rc.1`**.

## To 1.0.0

A soak period using the RC for real game saves on two machines gates
**`1.0.0`**. From `1.0.0`, the CLI options, exit codes, config schema, and
on-disk backup layout become a stable contract.

## Out of scope

Docker, cross-platform support, a watcher/daemon mode, and cloud-provider SDK
integrations.
