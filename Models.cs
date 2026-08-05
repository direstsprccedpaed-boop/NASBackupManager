using CommunityToolkit.Mvvm.ComponentModel;

namespace NasBackupManager;

public enum SourceKind
{
    Nas1,
    Nas2,
    Backup
}

public enum DiagnosticLevel
{
    Info,
    Warning,
    Error
}

public enum OperationState
{
    Pending,
    Simulated,
    Succeeded,
    Failed,
    Skipped
}

public sealed record ParsedRelease(
    string Title,
    int? Year,
    string? Resolution,
    string? Codec,
    string? Group,
    double Confidence,
    IReadOnlyList<string> Warnings);

public sealed class MediaFile : ObservableObject
{
    public required string FullPath { get; init; }

    public required string FileName { get; init; }

    public required SourceKind Source { get; init; }

    public required long SizeBytes { get; init; }

    public required DateTime LastWriteUtc { get; init; }

    public required string RelativeDirectory { get; init; }

    public required ParsedRelease Parsed { get; init; }

    public string SizeLabel => $"{SizeBytes / 1024d / 1024d:N1} Mo";

    public string SourceLabel => Source switch
    {
        SourceKind.Nas1 => "NAS 1",
        SourceKind.Nas2 => "NAS 2",
        SourceKind.Backup => "Backup",
        _ => "Inconnu"
    };

    public string ResolutionLabel => Parsed.Resolution ?? "—";

    public string YearLabel => Parsed.Year?.ToString() ?? "—";

    public string ConfidenceLabel =>
        $"{Parsed.Confidence:P0}";
}

public sealed record DiagnosticEntry(
    DateTimeOffset Timestamp,
    DiagnosticLevel Level,
    string Area,
    string Message,
    string? Path = null,
    Exception? Exception = null);

public sealed class CopyOperation : ObservableObject
{
    public required MediaFile Item { get; init; }

    public required string Destination { get; init; }

    [ObservableProperty]
    private OperationState _state = OperationState.Pending;

    [ObservableProperty]
    private string? _error;

    public string FileName => Item.FileName;

    public string Source => Item.SourceLabel;

    public string Size => Item.SizeLabel;

    public string StatusLabel => State switch
    {
        OperationState.Pending => "En attente",
        OperationState.Simulated => "Simulé",
        OperationState.Succeeded => "Copié",
        OperationState.Failed => "Échec",
        OperationState.Skipped => "Ignoré",
        _ => "Inconnu"
    };
}

public sealed record ScanSummary(
    IReadOnlyList<MediaFile> Items,
    IReadOnlyList<IReadOnlyList<MediaFile>> DuplicateGroups,
    IReadOnlyList<MediaFile> NewFiles,
    int WarningCount);