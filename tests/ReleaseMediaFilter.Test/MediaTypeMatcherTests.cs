using NzbDrone.Core.Plugins;
using Xunit;

namespace ReleaseMediaFilter.Test;

public class MediaTypeMatcherTests
{
    private static FilterOptions Blacklist(params string[] types) =>
        new(FilterMode.Blacklist, types, NoAllowedReleaseAction.DeleteFiltered, skipReleasesWithFiles: true);

    private static FilterOptions Whitelist(params string[] types) =>
        new(FilterMode.Whitelist, types, NoAllowedReleaseAction.DeleteFiltered, skipReleasesWithFiles: true);

    [Theory]
    [InlineData("Vinyl", "Vinyl")]
    [InlineData("2xVinyl", "Vinyl")]
    [InlineData("12\" Vinyl", "Vinyl")]
    [InlineData("Vinyl + CD", "Vinyl")]
    [InlineData("Cassette", "Cassette")]
    [InlineData("CD", "CD")]
    [InlineData("2xCD", "CD")]
    [InlineData("Digital Media", "Digital Media")]
    public void FormatMatches_positive_cases(string format, string mediaType)
    {
        Assert.True(MediaTypeMatcher.FormatMatches(format, mediaType));
    }

    [Theory]
    [InlineData("HDCD", "CD")]
    [InlineData("CD-R", "CD")]
    [InlineData("CD-R", "DAT")]
    [InlineData("Digital Media", "Vinyl")]
    [InlineData("VinylDisc", "Vinyl")]
    [InlineData("Vinyl", "in")]
    [InlineData("", "Vinyl")]
    public void FormatMatches_negative_cases(string format, string mediaType)
    {
        Assert.False(MediaTypeMatcher.FormatMatches(format, mediaType));
    }

    [Fact]
    public void Blacklist_filters_vinyl_or_cassette()
    {
        var options = Blacklist("Vinyl", "Cassette");

        Assert.True(MediaTypeMatcher.IsFiltered(new[] { "Vinyl" }, options));
        Assert.True(MediaTypeMatcher.IsFiltered(new[] { "Cassette" }, options));
        Assert.True(MediaTypeMatcher.IsFiltered(new[] { "2xVinyl" }, options));
        Assert.True(MediaTypeMatcher.IsFiltered(new[] { "CD", "Vinyl" }, options));
        Assert.False(MediaTypeMatcher.IsFiltered(new[] { "CD" }, options));
        Assert.False(MediaTypeMatcher.IsFiltered(new[] { "Digital Media" }, options));
    }

    [Fact]
    public void Whitelist_requires_every_medium_to_match()
    {
        var options = Whitelist("CD", "Digital Media");

        Assert.False(MediaTypeMatcher.IsFiltered(new[] { "CD" }, options));
        Assert.False(MediaTypeMatcher.IsFiltered(new[] { "Digital Media" }, options));
        Assert.False(MediaTypeMatcher.IsFiltered(new[] { "CD", "Digital Media" }, options));
        Assert.True(MediaTypeMatcher.IsFiltered(new[] { "Vinyl" }, options));
        Assert.True(MediaTypeMatcher.IsFiltered(new[] { "CD", "Vinyl" }, options));
        Assert.True(MediaTypeMatcher.IsFiltered(new[] { "Vinyl + CD" }, options));
    }

    [Fact]
    public void Empty_or_unknown_format_is_kept()
    {
        var options = Blacklist("Vinyl", "Cassette");

        Assert.False(MediaTypeMatcher.IsFiltered(Array.Empty<string>(), options));
        Assert.False(MediaTypeMatcher.IsFiltered(new string?[] { null, "  ", "" }, options));
    }

    [Fact]
    public void Empty_media_type_list_filters_nothing()
    {
        var options = new FilterOptions(FilterMode.Blacklist, Array.Empty<string>(), NoAllowedReleaseAction.DeleteFiltered, true);

        Assert.False(MediaTypeMatcher.IsFiltered(new[] { "Vinyl" }, options));
    }

    [Fact]
    public void Tokenize_keeps_hyphenated_formats_and_strips_copy_prefix()
    {
        Assert.Equal(new[] { "cd-r" }, MediaTypeMatcher.Tokenize("CD-R"));
        Assert.Equal(new[] { "vinyl", "cd" }, MediaTypeMatcher.Tokenize("2xVinyl + CD"));
    }
}
