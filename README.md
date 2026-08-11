# GameKeeper

GameKeeper is a single-shot Windows CLI that keeps a local game-save folder and
a cloud-synced folder in sync: two-way by default with the newer copy winning,
additive unless deletions are explicitly enabled, and with anything destructive
backed up first.

> **Status:** under development — see the [roadmap](ROADMAP.md). The CLI
> surface below is what exists today; the sync engine arrives in upcoming
> milestones, so a valid run currently validates the folders and changes no
> files.

## Build & test

```
dotnet build
dotnet test
```

## Usage

```
GameKeeper <gameFolder> <cloudFolder>
GameKeeper --game <gameFolder> --cloud <cloudFolder>
```

The folders may be given positionally or by option (`--game value` or
`--game=value` syntax), in any order.

| Option | Description |
| --- | --- |
| `-g, --game <path>` | The local game folder. |
| `-c, --cloud <path>` | The shared cloud folder. |
| `--version` | Show the version, then exit. |
| `-h, --help` | Show this help. |

## Exit codes

| Code | Meaning |
| --- | --- |
| `0` | Success. |
| `1` | The sync could not be attempted or completed. |
| `2` | Unusable command line (bad arguments or `--help`). |
