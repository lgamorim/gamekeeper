# GameKeeper

Single-deployable application, developed solo.

## Build & test
- `dotnet build`
- `dotnet test`

## Shared conventions
@.claude/rules/profiles/application-solo.md

## Project-specific notes
- Layout: `GameKeeper.slnx` at the root; app code in `src/GameKeeper.App/`,
  tests in `test/GameKeeper.App.UnitTests/`.
- Update `README.md` at the end of every milestone so it always documents the
  feature surface that actually exists.
