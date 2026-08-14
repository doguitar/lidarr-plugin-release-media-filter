using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using NzbDrone.Core.Music;

namespace NzbDrone.Core.Plugins;

public static class MediaTypeMatcher
{
    private static readonly TimeSpan MatchTimeout = TimeSpan.FromMilliseconds(250);

    public static IReadOnlyList<string> GetFormats(AlbumRelease release)
    {
        if (release?.Media == null)
        {
            return Array.Empty<string>();
        }

        return release.Media
            .Select(medium => medium?.Format)
            .Where(format => !string.IsNullOrWhiteSpace(format))
            .Cast<string>()
            .ToList();
    }

    public static bool IsReleaseFiltered(AlbumRelease release, FilterOptions options)
    {
        return IsFiltered(GetFormats(release), options);
    }

    public static bool IsFiltered(IEnumerable<string?> formats, FilterOptions options)
    {
        if (options == null)
        {
            throw new ArgumentNullException(nameof(options));
        }

        if (options.MediaTypes.Count == 0)
        {
            return false;
        }

        var knownFormats = (formats ?? Array.Empty<string?>())
            .Where(format => !string.IsNullOrWhiteSpace(format))
            .Select(format => format!.Trim())
            .ToList();

        if (knownFormats.Count == 0)
        {
            return false;
        }

        if (options.Mode == FilterMode.Blacklist)
        {
            return knownFormats.Any(format => MatchesAnyMediaType(format, options.MediaTypes));
        }

        return knownFormats.Any(format => !AllFormatTokensAllowed(format, options.MediaTypes));
    }

    public static bool MatchesAnyMediaType(string format, IEnumerable<string> mediaTypes)
    {
        if (string.IsNullOrWhiteSpace(format))
        {
            return false;
        }

        foreach (var mediaType in mediaTypes)
        {
            if (FormatMatches(format, mediaType))
            {
                return true;
            }
        }

        return false;
    }

    internal static bool FormatMatches(string format, string mediaType)
    {
        if (string.IsNullOrWhiteSpace(format) || string.IsNullOrWhiteSpace(mediaType))
        {
            return false;
        }

        var formatTokens = Tokenize(format);
        var typeTokens = Tokenize(mediaType);
        if (formatTokens.Count == 0 || typeTokens.Count == 0 || typeTokens.Count > formatTokens.Count)
        {
            return false;
        }

        for (var index = 0; index <= formatTokens.Count - typeTokens.Count; index++)
        {
            var windowMatches = true;
            for (var offset = 0; offset < typeTokens.Count; offset++)
            {
                if (!string.Equals(formatTokens[index + offset], typeTokens[offset], StringComparison.Ordinal))
                {
                    windowMatches = false;
                    break;
                }
            }

            if (windowMatches)
            {
                return true;
            }
        }

        return false;
    }

    internal static List<string> Tokenize(string value)
    {
        var parts = Regex.Split(value.Trim().ToLowerInvariant(), @"[^a-z0-9-]+", RegexOptions.None, MatchTimeout);
        var tokens = new List<string>();

        foreach (var part in parts)
        {
            if (string.IsNullOrWhiteSpace(part))
            {
                continue;
            }

            var token = Regex.Replace(part, @"^\d+x", string.Empty, RegexOptions.None, MatchTimeout);
            if (token.Length > 0)
            {
                tokens.Add(token);
            }
        }

        return tokens;
    }

    private static bool AllFormatTokensAllowed(string format, IEnumerable<string> mediaTypes)
    {
        var formatTokens = Tokenize(format);
        if (formatTokens.Count == 0)
        {
            return false;
        }

        var typeSequences = mediaTypes
            .Select(Tokenize)
            .Where(tokens => tokens.Count > 0)
            .OrderByDescending(tokens => tokens.Count)
            .ToList();

        var index = 0;
        while (index < formatTokens.Count)
        {
            var matched = false;
            foreach (var sequence in typeSequences)
            {
                if (index + sequence.Count > formatTokens.Count)
                {
                    continue;
                }

                var windowMatches = true;
                for (var offset = 0; offset < sequence.Count; offset++)
                {
                    if (!string.Equals(formatTokens[index + offset], sequence[offset], StringComparison.Ordinal))
                    {
                        windowMatches = false;
                        break;
                    }
                }

                if (windowMatches)
                {
                    index += sequence.Count;
                    matched = true;
                    break;
                }
            }

            if (!matched)
            {
                return false;
            }
        }

        return true;
    }
}
