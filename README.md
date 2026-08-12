# GameKeeper

GameKeeper is a single-shot Windows CLI that keeps a local game-save folder and
a cloud-synced folder in sync: two-way by default with the newer copy winning,
additive unless deletions are explicitly enabled, and with anything destructive
backed up first.

> **Status:** under development — see the [roadmap](ROADMAP.md). The surface
> below is what exists today: a working sync engine (newer-wins copying with a
> recorded baseline) in two-way and one-way modes. Deletion propagation,
> backups, filters, dry-run, and profiles arrive in upcoming milestones.

## Build & test

```
dotnet build
dotnet test
```

## Usage

```
GameKeeper <gameFolder> <cloudFolder> [--mode <both|up|down>]
GameKeeper --game <gameFolder> --cloud <cloudFolder> [--mode <both|up|down>]
```

The folders may be given positionally or by option (`--game value` or
`--game=value` syntax), in any order.

| Option | Description |
| --- | --- |
| `-g, --game <path>` | The local game folder. |
| `-c, --cloud <path>` | The shared cloud folder. |
| `-m, --mode <both\|up\|down>` | Sync direction: `both` (default, two-way), `up` (game → cloud), `down` (cloud → game). |
| `--version` | Show the version, then exit. |
| `-h, --help` | Show this help. |

## How it syncs

- Files are compared by last write time and size against a baseline recorded
  on the previous run; content is never read. Timestamps within two seconds of
  each other count as the same moment, absorbing file-system rounding.
- The newer copy wins. On an exact timestamp tie with different sizes, the
  game folder's copy wins.
- Nothing is ever deleted in this version: a file missing on one side is
  copied back from the other, and an edit always outlives a delete.
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
