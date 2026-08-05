using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace NasBackupManager;

public static partial class FileBotStyleParser
{
    private static readonly string[] StopWords =
    [
        "2160p",
        "1080p",
        "720p",
        "480p",
        "bluray",
        "blu-ray",
        "web-dl",
        "webrip",
        "hdtv",
        "dvdrip",
        "bdrip",
        "remux",
        "x264",
        "x265",
        "h.264",
        "h.265",
        "hevc",
        "av1",
        "aac",
        "dts",
        "ac3",
        "vostfr",
        "multi",
        "proper",
        "repack"
    ];

    [GeneratedRegex(
        @"(?<!\d)(19\d{2}|20\d{2})(?!\d)",
        RegexOptions.CultureInvariant)]
    private static partial Regex YearRegex();

    public static ParsedRelease Parse(string fileName)
    {
        var raw = Path.GetFileNameWithoutExtension(fileName);

        var normalized = raw.Normalize(NormalizationForm.FormKD);

        normalized = new string(
            normalized
                .Where(character =>
                    CharUnicodeInfo.GetUnicodeCategory(character) !=
                    UnicodeCategory.NonSpacingMark)
                .ToArray());

        normalized = Regex.Replace(normalized, @"[._]+", " ");

        var lower = normalized.ToLowerInvariant();

        var cutIndex = normalized.Length;

        foreach (var stopWord in StopWords)
        {
            var index = lower.IndexOf(
                stopWord,
                StringComparison.Ordinal);

            if (index >= 0)
            {
                cutIndex = Math.Min(cutIndex, index);
            }
        }

        var yearMatch = YearRegex().Match(normalized);

        if (yearMatch.Success)
        {
            cutIndex = Math.Min(cutIndex, yearMatch.Index);
        }

        var title = normalized[..cutIndex];

        title = Regex.Replace(title, @"[\[\]{}()]+", " ");
        title = Regex.Replace(title, @"\s+", " ");
        title = title.Trim(' ', '-');

        int? year = null;

        if (yearMatch.Success &&
            int.TryParse(yearMatch.Value, out var parsedYear))
        {
            year = parsedYear;
        }

        var resolution =
            lower.Contains("2160") || lower.Contains("4k")
                ? "4K"
                : lower.Contains("1080")
                    ? "1080p"
                    : lower.Contains("720")
                        ? "720p"
                        : lower.Contains("480")
                            ? "480p"
                            : null;

        var codec =
            lower.Contains("x265") ||
            lower.Contains("hevc") ||
            lower.Contains("h.265")
                ? "HEVC"
                : lower.Contains("x264") ||
                  lower.Contains("h.264")
                    ? "H.264"
                    : null;

        var groupMatch = Regex.Match(
            normalized,
            @"-(?<group>[A-Za-z0-9]{2,16})$");

        var group = groupMatch.Success
            ? groupMatch.Groups["group"].Value
            : null;

        var warnings = new List<string>();

        if (title.Length < 2)
        {
            warnings.Add("Titre insuffisant");
        }

        if (!year.HasValue)
        {
            warnings.Add("Année absente");
        }

        var confidence = 0.50;

        if (year.HasValue)
        {
            confidence += 0.20;
        }

        if (!string.IsNullOrWhiteSpace(resolution))
        {
            confidence += 0.15;
        }

        if (title.Length > 4)
        {
            confidence += 0.15;
        }

        confidence = Math.Clamp(confidence, 0d, 1d);

        return new ParsedRelease(
            title,
            year,
            resolution,
            codec,
            group,
            confidence,
            warnings);
    }

    public static string BuildDuplicateKey(MediaFile item)
    {
        var title = NormalizeForKey(item.Parsed.Title);
        var year = item.Parsed.Year?.ToString() ?? "0000";

        return $"{title}|{year}";
    }

    private static string NormalizeForKey(string value)
    {
        value = value.Normalize(NormalizationForm.FormKD);

        value = new string(
            value
                .Where(character =>
                    CharUnicodeInfo.GetUnicodeCategory(character) !=
                    UnicodeCategory.NonSpacingMark)
                .ToArray());

        return Regex.Replace(
            value.ToLowerInvariant(),
            @"[^a-z0-9]",
            string.Empty);
    }
}