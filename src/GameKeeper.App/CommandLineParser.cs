using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using GameKeeper.Core;

namespace GameKeeper.App;

/// <summary>
/// Parses the command line into a <see cref="CommandLineParseResult"/>. Supports the game and
/// cloud folders as positional arguments, as named options (<c>--game</c>/<c>-g</c> and
/// <c>--cloud</c>/<c>-c</c>, with either <c>--game value</c> or <c>--game=value</c> syntax),
/// or a mix of the two, plus <c>--mode</c>/<c>-m</c> for the sync direction.
/// </summary>
public static class CommandLineParser
{
    private static readonly string[] HelpFlags = ["--help", "-h", "/?"];
    private static readonly string[] GameNames = ["--game", "-g"];
    private static readonly string[] CloudNames = ["--cloud", "-c"];
    private static readonly string[] ModeNames = ["--mode", "-m"];
    private static readonly string[] KeepBackupsNames = ["--keep-backups"];
    private const string VersionFlag = "--version";
    private const string DeleteFlag = "--delete";
    private const string NoBackupFlag = "--no-backup";
    private const string ForceFlag = "--force";

    /// <summary>Parses the supplied command-line arguments.</summary>
    /// <param name="args">The raw command-line arguments.</param>
    /// <returns>The parse outcome.</returns>
    public static CommandLineParseResult Parse(string[] args)
    {
        ArgumentNullException.ThrowIfNull(args);

        // A help flag anywhere on the command line wins, even alongside otherwise invalid input.
        if (args.Any(arg => HelpFlags.Contains(arg, StringComparer.OrdinalIgnoreCase)))
        {
            return CommandLineParseResult.Help();
        }

        // --version short-circuits the same way, but help explains more, so help wins outright.
        if (args.Any(arg => arg.Equals(VersionFlag, StringComparison.OrdinalIgnoreCase)))
        {
            return CommandLineParseResult.Version();
        }

        string? game = null;
        string? cloud = null;
        SyncMode? mode = null;
        bool propagateDeletions = false;
        bool createBackups = true;
        int? keepBackups = null;
        bool force = false;
        var positionals = new List<string>();

        for (int i = 0; i < args.Length; i++)
        {
            string token = args[i];

            if (TryMatchOption(token, GameNames, out string? inlineGame))
            {
                if (!TryResolveValue("--game", inlineGame, args, ref i, out string? value, out string? error))
                {
                    return CommandLineParseResult.Failure(error);
                }

                if (game is not null)
                {
                    return CommandLineParseResult.Failure("The game folder was specified more than once.");
                }

                game = value;
            }
            else if (TryMatchOption(token, CloudNames, out string? inlineCloud))
            {
                if (!TryResolveValue("--cloud", inlineCloud, args, ref i, out string? value, out string? error))
                {
                    return CommandLineParseResult.Failure(error);
                }

                if (cloud is not null)
                {
                    return CommandLineParseResult.Failure("The cloud folder was specified more than once.");
                }

                cloud = value;
            }
            else if (TryMatchOption(token, ModeNames, out string? inlineMode))
            {
                if (!TryResolveValue("--mode", inlineMode, args, ref i, out string? value, out string? error))
                {
                    return CommandLineParseResult.Failure(error);
                }

                if (mode is not null)
                {
                    return CommandLineParseResult.Failure("The sync mode was specified more than once.");
                }

                if (!TryParseMode(value, out SyncMode parsedMode))
                {
                    return CommandLineParseResult.Failure(
                        $"Unknown sync mode: {value}. Expected 'both', 'up', or 'down'.");
                }

                mode = parsedMode;
            }
            else if (token.Equals(DeleteFlag, StringComparison.OrdinalIgnoreCase))
            {
                // A bare flag: repeating it is harmless, so no duplicate check.
                propagateDeletions = true;
            }
            else if (token.Equals(NoBackupFlag, StringComparison.OrdinalIgnoreCase))
            {
                createBackups = false;
            }
            else if (token.Equals(ForceFlag, StringComparison.OrdinalIgnoreCase))
            {
                force = true;
            }
            else if (TryMatchOption(token, KeepBackupsNames, out string? inlineKeep))
            {
                if (!TryResolveValue("--keep-backups", inlineKeep, args, ref i, out string? value, out string? error))
                {
                    return CommandLineParseResult.Failure(error);
                }

                if (keepBackups is not null)
                {
                    return CommandLineParseResult.Failure("The backup count was specified more than once.");
                }

                if (!int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out int parsedKeep))
                {
                    return CommandLineParseResult.Failure(
                        $"Invalid value for --keep-backups: {value}. Expected a whole number, 0 to keep every backup.");
                }

                keepBackups = parsedKeep;
            }
            else if (token.StartsWith('-'))
            {
                return CommandLineParseResult.Failure($"Unknown option: {token}");
            }
            else
            {
                positionals.Add(token);
            }
        }

        // These options govern GameKeeper's own housekeeping rather than the folder pairing,
        // so they are attached once here rather than threaded through the factory method.
        return Resolve(game, cloud, mode, propagateDeletions, positionals) with
        {
            CreateBackups = createBackups,
            KeepBackups = keepBackups,
            Force = force,
        };
    }

    private static CommandLineParseResult Resolve(
        string? game,
        string? cloud,
        SyncMode? mode,
        bool propagateDeletions,
        List<string> positionals)
    {
        // Positionals fill whichever folder a named option did not already set, game first.
        foreach (string positional in positionals)
        {
            if (game is null)
            {
                game = positional;
            }
            else if (cloud is null)
            {
                cloud = positional;
            }
            else
            {
                return CommandLineParseResult.Failure("Too many arguments were supplied.");
            }
        }

        if (game is null || cloud is null)
        {
            return CommandLineParseResult.Failure("Both a game folder and a cloud folder are required.");
        }

        return CommandLineParseResult.Success(game, cloud, mode ?? SyncMode.Bidirectional, propagateDeletions);
    }

    private static bool TryParseMode(string value, out SyncMode mode)
    {
        switch (value.ToLowerInvariant())
        {
            case "both":
                mode = SyncMode.Bidirectional;
                return true;
            case "up":
                mode = SyncMode.FirstToSecond;
                return true;
            case "down":
                mode = SyncMode.SecondToFirst;
                return true;
            default:
                mode = SyncMode.Bidirectional;
                return false;
        }
    }

    private static bool TryMatchOption(string token, string[] names, out string? inlineValue)
    {
        inlineValue = null;

        string namePart = token;
        int equals = token.IndexOf('=');
        if (equals >= 0)
        {
            namePart = token[..equals];
            inlineValue = token[(equals + 1)..];
        }

        if (names.Contains(namePart, StringComparer.OrdinalIgnoreCase))
        {
            return true;
        }

        inlineValue = null;
        return false;
    }

    private static bool TryResolveValue(
        string displayName,
        string? inlineValue,
        string[] args,
        ref int index,
        [NotNullWhen(true)] out string? value,
        [NotNullWhen(false)] out string? error)
    {
        error = null;

        // Value attached with '=' (for example, --game=C:\saves).
        if (inlineValue is not null)
        {
            if (inlineValue.Length == 0)
            {
                value = null;
                error = $"Missing value for {displayName}.";
                return false;
            }

            value = inlineValue;
            return true;
        }

        // Value supplied as the following token. A token that looks like another option is
        // treated as a missing value rather than silently consumed.
        if (index + 1 >= args.Length || args[index + 1].StartsWith('-'))
        {
            value = null;
            error = $"Missing value for {displayName}.";
            return false;
        }

        index++;
        value = args[index];
        return true;
    }
}
