using System;
using System.Collections.Generic;
using System.Linq;
using FluentValidation;
using NzbDrone.Core.Annotations;
using NzbDrone.Core.ThingiProvider;
using NzbDrone.Core.Validation;

namespace NzbDrone.Core.Plugins;

public class ReleaseMediaFilterSettings : IProviderConfig
{
    public const string DefaultMediaTypes = "Vinyl, Cassette";
    private const int DefaultScanIntervalMinutes = 1440;

    private static readonly ReleaseMediaFilterSettingsValidator Validator = new();

    public ReleaseMediaFilterSettings()
    {
        FilterMode = FilterMode.Blacklist;
        MediaTypes = DefaultMediaTypes;
        NoAllowedReleaseAction = NoAllowedReleaseAction.KeepLastResort;
        SkipReleasesWithFiles = true;
        ScanIntervalMinutes = DefaultScanIntervalMinutes;
    }

    [FieldDefinition(0, Label = "Filter mode", Type = FieldType.Select, SelectOptions = typeof(FilterMode), HelpText = "Blacklist removes matching media types. Whitelist keeps only matching media types.")]
    public FilterMode FilterMode { get; set; }

    [FieldDefinition(1, Label = "Media types", Type = FieldType.Textbox, HelpText = "Comma-separated MusicBrainz medium formats. Defaults to Vinyl, Cassette.")]
    public string MediaTypes { get; set; }

    [FieldDefinition(2, Label = "When no allowed release remains", Type = FieldType.Select, SelectOptions = typeof(NoAllowedReleaseAction), HelpText = "Keep the last remaining filtered release (default), or delete filtered releases anyway.")]
    public NoAllowedReleaseAction NoAllowedReleaseAction { get; set; }

    [FieldDefinition(3, Label = "Skip releases that already have files", Type = FieldType.Checkbox, HelpText = "Do not delete an album release that already has imported track files. File cleanup is a later mode.")]
    public bool SkipReleasesWithFiles { get; set; }

    [FieldDefinition(4, Label = "Scan interval (minutes)", Type = FieldType.Number, HelpText = "How often to run the library backfill scan. Minimum 60 minutes. Defaults to 1440 (24 hours).")]
    public int ScanIntervalMinutes { get; set; }

    public FilterOptions ToFilterOptions()
    {
        return new FilterOptions(FilterMode, ParseMediaTypes(MediaTypes), NoAllowedReleaseAction, SkipReleasesWithFiles);
    }

    public static IReadOnlyList<string> ParseMediaTypes(string? mediaTypes)
    {
        if (string.IsNullOrWhiteSpace(mediaTypes))
        {
            return Array.Empty<string>();
        }

        return mediaTypes.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(type => !string.IsNullOrWhiteSpace(type))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public NzbDroneValidationResult Validate()
    {
        var result = Validator.Validate(this);
        if (FilterMode == FilterMode.Whitelist &&
            ParseMediaTypes(MediaTypes).Any(type =>
                string.Equals(type, "Vinyl", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(type, "Cassette", StringComparison.OrdinalIgnoreCase)))
        {
            var warning = new NzbDroneValidationFailure(
                nameof(FilterMode),
                "Whitelist with Vinyl or Cassette selected will delete CD and digital releases.")
            {
                IsWarning = true
            };
            return new NzbDroneValidationResult(result.Errors.Concat(new[] { warning }));
        }

        return new NzbDroneValidationResult(result);
    }
}

public class ReleaseMediaFilterSettingsValidator : AbstractValidator<ReleaseMediaFilterSettings>
{
    public ReleaseMediaFilterSettingsValidator()
    {
        RuleFor(c => c.MediaTypes)
            .Must(value => ReleaseMediaFilterSettings.ParseMediaTypes(value).Count > 0)
            .WithMessage("Enter at least one media type");
        RuleFor(c => c.ScanIntervalMinutes).GreaterThanOrEqualTo(60)
            .WithMessage("Scan interval must be at least 60 minutes");
    }
}
