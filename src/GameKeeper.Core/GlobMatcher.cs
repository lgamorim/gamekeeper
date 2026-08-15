using System.Text;
using System.Text.RegularExpressions;

namespace GameKeeper.Core;

/// <summary>
/// Matches relative paths against a set of glob patterns. A pattern may use <c>*</c> (any run of
/// characters, including directory separators) and <c>?</c> (exactly one character); everything
/// else is matched literally. Matching is case-insensitive and treats <c>/</c> and <c>\</c> as
/// the same separator, so a pattern written in either style matches Windows-style relative
/// paths. Used to skip files the user does not want synced, such as logs, caches, and crash
/// dumps.
/// </summary>
public sealed class GlobMatcher
{
    private readonly Regex[] _patterns;

    /// <summary>Initializes a new matcher from the given glob patterns.</summary>
    /// <param name="patterns">The glob patterns; blank entries are ignored.</param>
    public GlobMatcher(IEnumerable<string> patterns)
    {
        ArgumentNullException.ThrowIfNull(patterns);
        _patterns = [.. patterns
            .Where(pattern => !string.IsNullOrWhiteSpace(pattern))
            .Select(Compile)];
    }

    /// <summary>
    /// Whether any usable pattern was supplied. Callers need this to tell "no patterns, so the
    /// question does not apply" from "patterns were given and this path matched none of them".
    /// </summary>
    public bool HasPatterns => _patterns.Length > 0;

    /// <summary>Whether the given relative path matches any of the patterns.</summary>
    /// <param name="relativePath">The relative path to test.</param>
    /// <returns>Whether any pattern matches.</returns>
    public bool IsMatch(string relativePath)
    {
        ArgumentNullException.ThrowIfNull(relativePath);
        if (_patterns.Length == 0)
        {
            return false;
        }

        string normalized = Normalize(relativePath);
        return _patterns.Any(pattern => pattern.IsMatch(normalized));
    }

    private static Regex Compile(string pattern)
    {
        // Fully anchored, so a pattern describes the whole relative path; '*' deliberately
        // crosses separators, which is what makes '*.log' catch nested logs.
        var builder = new StringBuilder("^");
        foreach (char c in Normalize(pattern.Trim()))
        {
            builder.Append(c switch
            {
                '*' => ".*",
                '?' => ".",
                _ => Regex.Escape(c.ToString()),
            });
        }

        builder.Append('$');
        return new Regex(builder.ToString(), RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    }

    // Collapse both separator styles onto '/' so patterns and paths compare on equal footing.
    private static string Normalize(string path) => path.Replace('\\', '/');
}
