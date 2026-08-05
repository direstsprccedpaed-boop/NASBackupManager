using System.Collections.Concurrent;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace NasBackupManager;

public sealed class ScanService
{
    private static readonly HashSet<string> VideoExtensions =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ".mkv",
            ".mp4",
            ".avi",
            ".mov",
            ".m4v",
            ".wmv",
            ".ts"
        };

    public async Task<ScanSummary> ScanAsync(
        IReadOnlyDictionary<SourceKind, string> roots,
        IProgress<string> progress,
        CancellationToken cancellationToken)
    {
        var allFiles = new ConcurrentBag<MediaFile>();
        var warnings = 0;

        var tasks = roots.Select(pair =>
            Task.Run(() =>
            {
                ScanRoot(
                    pair.Key,
                    pair.Value,
                    allFiles,
                    () => Interlocked.Increment(ref warnings),
                    cancellationToken);
            }, cancellationToken));

        await Task.WhenAll(tasks);

        var items = allFiles
            .OrderBy(item => item.Parsed.Title)
            .ThenBy(item => item.Parsed.Year)
            .ToList();

        var duplicateGroups = items
            .GroupBy(FileBotStyleParser.BuildDuplicateKey)
            .Where(group =>
                group.Count() > 1 &&
                group.Key.Length > 6)
            .Select(group =>
                (IReadOnlyList<MediaFile>)group
                    .OrderBy(item => item.Source)
                    .ThenByDescending(item => item.SizeBytes)
                    .ToList())
            .ToList();

        var backupKeys = items
            .Where(item => item.Source == SourceKind.Backup)
            .Select(FileBotStyleParser.BuildDuplicateKey)
            .ToHashSet();

        var newFiles = items
            .Where(item =>
                item.Source != SourceKind.Backup &&
                !backupKeys.Contains(
                    FileBotStyleParser.BuildDuplicateKey(item)))
            .ToList();

        progress.Report(
            $"{items.Count} fichiers analysés · " +
            $"{duplicateGroups.Count} groupes de doublons · " +
            $"{newFiles.Count} éléments à sauvegarder");

        return new ScanSummary(
            items,
            duplicateGroups,
            newFiles,
            warnings);
    }

    private static void ScanRoot(
        SourceKind source,
        string root,
        ConcurrentBag<MediaFile> results,
        Action incrementWarnings,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(root))
        {
            incrementWarnings();

            DiagnosticStore.Instance.Warning(
                "Scan",
                $"Chemin manquant pour {source}.");

            return;
        }

        if (!Directory.Exists(root))
        {
            incrementWarnings();

            DiagnosticStore.Instance.Warning(
                "Scan",
                $"Dossier inaccessible pour {source}.",
                root);

            return;
        }

        foreach (var path in EnumerateFilesSafe(
                     root,
                     cancellationToken))
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!VideoExtensions.Contains(Path.GetExtension(path)))
            {
                continue;
            }

            try
            {
                var info = new FileInfo(path);

                var directory = info.DirectoryName ?? root;

                var relativeDirectory = Path.GetRelativePath(
                    root,
                    directory);

                results.Add(new MediaFile
                {
                    FullPath = path,
                    FileName = info.Name,
                    Source = source,
                    SizeBytes = info.Length,
                    LastWriteUtc = info.LastWriteTimeUtc,
                    RelativeDirectory = relativeDirectory,
                    Parsed = FileBotStyleParser.Parse(info.Name)
                });
            }
            catch (Exception exception)
            {
                incrementWarnings();

                DiagnosticStore.Instance.Warning(
                    "Scan",
                    exception.Message,
                    path);
            }
        }
    }

    private static IEnumerable<string> EnumerateFilesSafe(
        string root,
        CancellationToken cancellationToken)
    {
        var directories = new Queue<string>();
        directories.Enqueue(root);

        while (directories.Count > 0)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var currentDirectory = directories.Dequeue();

            IEnumerable<string> files = Array.Empty<string>();
            IEnumerable<string> subDirectories = Array.Empty<string>();

            try
            {
                files = Directory.EnumerateFiles(currentDirectory);
                subDirectories =
                    Directory.EnumerateDirectories(currentDirectory);
            }
            catch (Exception exception)
            {
                DiagnosticStore.Instance.Warning(
                    "Scan",
                    exception.Message,
                    currentDirectory);
            }

            foreach (var file in files)
            {
                yield return file;
            }

            foreach (var directory in subDirectories)
            {
                directories.Enqueue(directory);
            }
        }
    }
}

public sealed class ResumableCopyService
{
    public async Task RunAsync(
        IEnumerable<CopyOperation> operations,
        bool dryRun,
        IProgress<string> progress,
        CancellationToken cancellationToken)
    {
        foreach (var operation in operations.Where(
                     operation =>
                         operation.State is OperationState.Pending or
                         OperationState.Failed))
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                if (dryRun)
                {
                    operation.State = OperationState.Simulated;

                    progress.Report(
                        $"Simulation : {operation.Item.FileName}");

                    continue;
                }

                var destinationDirectory =
                    Path.GetDirectoryName(operation.Destination);

                if (string.IsNullOrWhiteSpace(destinationDirectory))
                {
                    throw new IOException(
                        "Destination de copie invalide.");
                }

                Directory.CreateDirectory(destinationDirectory);

                var partialPath = operation.Destination + ".partial";

                await using var source = File.OpenRead(
                    operation.Item.FullPath);

                await using var destination = File.Create(partialPath);

                await source.CopyToAsync(
                    destination,
                    cancellationToken);

                await destination.FlushAsync(cancellationToken);

                var copiedSize = new FileInfo(partialPath).Length;

                if (copiedSize != operation.Item.SizeBytes)
                {
                    throw new IOException(
                        "La taille du fichier copié ne correspond pas " +
                        "à la taille du fichier source.");
                }

                File.Move(
                    partialPath,
                    operation.Destination,
                    true);

                operation.State = OperationState.Succeeded;

                progress.Report(
                    $"Copié : {operation.Item.FileName}");
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception exception)
            {
                operation.State = OperationState.Failed;
                operation.Error = exception.Message;

                DiagnosticStore.Instance.Error(
                    "Copie",
                    exception.Message,
                    exception,
                    operation.Item.FullPath);

                progress.Report(
                    $"Échec : {operation.Item.FileName}");
            }
        }
    }
}
