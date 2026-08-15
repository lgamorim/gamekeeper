# GameKeeper

GameKeeper is a single-shot Windows CLI that keeps a local game-save folder and
a cloud-synced folder in sync: two-way by default with the newer copy winning,
additive unless deletions are explicitly enabled, and with anything destructive
backed up first.

> **Status:** beta — see the [roadmap](ROADMAP.md). The surface below is what
> exists today: a working sync engine (newer-wins copying with a recorded
> baseline) in two-way and one-way modes, opt-in deletion propagation,
> conflict reporting, automatic backups with pruning, a mass-deletion guard,
> include/exclude filters, and a full dry-run preview. Profiles and a config
> file arrive in the next milestone.

## Build & test

```
dotnet build
dotnet test
```

## Usage

```
GameKeeper <gameFolder> <cloudFolder> [--mode <both|up|down>] [--delete] [--dry-run]
GameKeeper --game <gameFolder> --cloud <cloudFolder> [--mode <both|up|down>] [--delete] [--dry-run]
```

The folders may be given positionally or by option (`--game value` or
`--game=value` syntax), in any order.

| Option | Description |
| --- | --- |
| `-g, --game <path>` | The local game folder. |
| `-c, --cloud <path>` | The shared cloud folder. |
| `-m, --mode <both\|up\|down>` | Sync direction: `both` (default, two-way), `up` (game → cloud), `down` (cloud → game). |
| `--delete` | Propagate deletions (off by default; files are only added or updated unless this is set). |
| `--no-backup` | Do not back up overwritten or deleted files. |
| `--keep-backups <n>` | Backups to keep per file (default 10; `0` keeps all). |
| `--force` | Allow a run that would delete most tracked files. |
| `-i, --include <glob>` | Sync only paths matching the glob (repeatable). Applies to files; folders follow `--exclude`. |
| `-x, --exclude <glob>` | Skip paths matching the glob (repeatable), e.g. `--exclude *.log`. |
| `-n, --dry-run` | Preview the actions without changing any files. |
| `--version` | Show the version, then exit. |
| `-h, --help` | Show this help. |

## How it syncs

- Files are compared by last write time and size against a baseline recorded
  on the previous run; content is never read. Timestamps within two seconds of
  each other count as the same moment, absorbing file-system rounding.
- The newer copy wins. When both sides changed since the last sync, that
  overwrite is reported as a conflict, naming the file and the side that
  survived. On an exact timestamp tie with different sizes, the game folder's
  copy wins.
- By default nothing is deleted: a file missing on one side is copied back
  from the other. With `--delete`, a file removed on one side (since the last
  recorded sync) is deleted from the other side too — but an edit always
  outlives a delete, and one-way modes only ever delete from the destination.
- Every deleted file is named in the run's output, not just counted.
- Empty folders present on one side are recreated on the other, so the two
  structures match exactly.

## Filters and dry-run

- Globs use `*` (any run of characters, **including** folder separators — so
  `*.log` also catches `saves\x.log`) and `?` (exactly one character), matched
  case-insensitively against the whole relative path; `/` and `\` are
  interchangeable. To skip a folder and everything in it, use the `cache*`
  form — `cache\*` matches its contents but not the folder itself.
- `--include` is an allow-list: when set, only matching files sync, in both
  directions. `--exclude` subtracts from whatever is in scope and always wins.
  Folders answer to excludes only (an include list names files).
- A file that falls out of scope — newly excluded, or no longer matching the
  include list — is left alone on both sides, never treated as deleted, even
  with `--delete` on.
- `--dry-run` previews the full run: the same counts a real run would report,
  plus a name-by-name listing of every copy, deletion, conflict, and one-way
  skip — while touching nothing: no copies, no deletions, no backups, no
  folder creation, and the sync record is left unchanged. A `--delete
  --dry-run` preview that would be refused for real warns instead of blocking.

## Backups and the safety net

- Before a conflict overwrite or a propagated deletion, the losing copy is
  saved to a `.gamekeeper-backups` folder at its own root, named
  `<name>.<timestamp>.bak`. The timestamp is the file's own last-modified
  time in UTC (`yyyyMMddHHmmss`), not the moment of the backup, so a backup
  written today can still be found and restored years later. This folder and
  naming are part of the on-disk contract.
- After each backup, only the newest 10 backups per file are kept (tune with
  `--keep-backups`; `0` keeps all). Only files matching GameKeeper's own
  backup naming are ever pruned — the folder is otherwise yours.
- The backups folder is never synced.
- A `--delete` run is previewed first (so it scans twice) and refused before
  anything changes when it would delete at least 3 and more than half of the
  tracked files — the signature of a reinstalled game, a changed drive
  letter, or a half-downloaded cloud folder. `--force` proceeds anyway.
- Copies are staged next to the destination and swapped in with the source's
  timestamp already applied, so an interrupted run leaves either the old file
  or the new one — never a truncated hybrid — and rerunning is always safe.
- One-way modes never write the source side; files they refuse to update are
  reported as skipped.
- The game folder must exist; a missing cloud folder is created on first use.
- Each folder pair's baseline is stored under `%LOCALAPPDATA%\GameKeeper\state`.

## Exit codes

| Code | Meaning |
| --- | --- |
| `0` | Success. |
| `1` | The sync could not be attempted or completed. |
| `2` | Unusable command line (bad arguments or `--help`). |
