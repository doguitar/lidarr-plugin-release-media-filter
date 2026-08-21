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
        SearchAfterFileCleanup = false;
        SortField1 = ReleaseSortField.None;
        SortDirection1 = ReleaseSortDirection.Descending;
        SortPattern1 = string.Empty;
        SortField2 = ReleaseSortField.None;
        SortDirection2 = ReleaseSortDirection.Ascending;
        SortPattern2 = string.Empty;
        SortField3 = ReleaseSortField.None;
        SortDirection3 = ReleaseSortDirection.Descending;
        SortPattern3 = string.Empty;
        ScanIntervalMinutes = DefaultScanIntervalMinutes;
    }

    [FieldDefinition(0, Label = "Filter mode", Type = FieldType.Select, SelectOptions = typeof(FilterMode), HelpText = "Blacklist removes matching media types. Whitelist keeps only matching media types.")]
    public FilterMode FilterMode { get; set; }

    [FieldDefinition(1, Label = "Media types", Type = FieldType.Textbox, HelpText = "Comma-separated MusicBrainz medium formats. Defaults to Vinyl, Cassette.")]
    public string MediaTypes { get; set; }

    [FieldDefinition(2, Label = "When no allowed release remains", Type = FieldType.Select, SelectOptions = typeof(NoAllowedReleaseAction), HelpText = "Keep the last remaining filtered release (default), or delete filtered releases anyway.")]
    public NoAllowedReleaseAction NoAllowedReleaseAction { get; set; }

    [FieldDefinition(3, Label = "Skip releases that already have files", Type = FieldType.Checkbox, HelpText = "When on, never delete a release that already has imported files. When off, send those files to Lidarr's recycle bin and then delete the release.")]
    public bool SkipReleasesWithFiles { get; set; }

    [FieldDefinition(4, Label = "Search after file cleanup", Type = FieldType.Checkbox, HelpText = "After deleting imported files from a filtered release, search indexers for the newly monitored remaining release.")]
    public bool SearchAfterFileCleanup { get; set; }

    [FieldDefinition(5, Label = "Sort 1", Type = FieldType.Select, SelectOptions = typeof(ReleaseSortField), HelpText = "First key used to choose the monitored remaining release. Example: Medium regex with ^CD$ descending, then track count ascending.")]
    public ReleaseSortField SortField1 { get; set; }

    [FieldDefinition(6, Label = "Sort 1 direction", Type = FieldType.Select, SelectOptions = typeof(ReleaseSortDirection), HelpText = "Ascending or descending for sort 1. For a regex field, descending puts matches first.")]
    public ReleaseSortDirection SortDirection1 { get; set; }

    [FieldDefinition(7, Label = "Sort 1 regex", Type = FieldType.Textbox, HelpText = "Used when sort 1 is country regex or medium regex. Case-insensitive. Example: ^CD$ or US|GB|UK.")]
    public string SortPattern1 { get; set; }

    [FieldDefinition(8, Label = "Sort 2", Type = FieldType.Select, SelectOptions = typeof(ReleaseSortField), HelpText = "Second key used when sort 1 ties.")]
    public ReleaseSortField SortField2 { get; set; }

    [FieldDefinition(9, Label = "Sort 2 direction", Type = FieldType.Select, SelectOptions = typeof(ReleaseSortDirection), HelpText = "Ascending or descending for sort 2.")]
    public ReleaseSortDirection SortDirection2 { get; set; }

    [FieldDefinition(10, Label = "Sort 2 regex", Type = FieldType.Textbox, HelpText = "Used when sort 2 is country regex or medium regex.")]
    public string SortPattern2 { get; set; }

    [FieldDefinition(11, Label = "Sort 3", Type = FieldType.Select, SelectOptions = typeof(ReleaseSortField), HelpText = "Third key used when sort 1 and 2 tie.")]
    public ReleaseSortField SortField3 { get; set; }

    [FieldDefinition(12, Label = "Sort 3 direction", Type = FieldType.Select, SelectOptions = typeof(ReleaseSortDirection), HelpText = "Ascending or descending for sort 3.")]
    public ReleaseSortDirection SortDirection3 { get; set; }

    [FieldDefinition(13, Label = "Sort 3 regex", Type = FieldType.Textbox, HelpText = "Used when sort 3 is country regex or medium regex.")]
    public string SortPattern3 { get; set; }

    [FieldDefinition(14, Label = "Scan interval (minutes)", Type = FieldType.Number, HelpText = "How often to run the library backfill scan. Minimum 60 minutes. Defaults to 1440 (24 hours).")]
    public int ScanIntervalMinutes { get; set; }

    public FilterOptions ToFilterOptions()
    {
        return new FilterOptions(
            FilterMode,
            ParseMediaTypes(MediaTypes),
            NoAllowedReleaseAction,
            SkipReleasesWithFiles,
            SearchAfterFileCleanup,
            ParseSortRules());
    }

    public IReadOnlyList<ReleaseSortRule> ParseSortRules()
    {
        return new[]
        {
            new ReleaseSortRule(SortField1, SortDirection1, SortPattern1),
            new ReleaseSortRule(SortField2, SortDirection2, SortPattern2),
            new ReleaseSortRule(SortField3, SortDirection3, SortPattern3)
        };
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
        RuleFor(c => c.SortPattern1)
            .Must((settings, pattern) => ReleaseSortRule.TryValidatePattern(settings.SortField1, pattern, out _))
            .WithMessage("Sort 1 regex is required and must be a valid regular expression.");
        RuleFor(c => c.SortPattern2)
            .Must((settings, pattern) => ReleaseSortRule.TryValidatePattern(settings.SortField2, pattern, out _))
            .WithMessage("Sort 2 regex is required and must be a valid regular expression.");
        RuleFor(c => c.SortPattern3)
            .Must((settings, pattern) => ReleaseSortRule.TryValidatePattern(settings.SortField3, pattern, out _))
            .WithMessage("Sort 3 regex is required and must be a valid regular expression.");
    }
}
