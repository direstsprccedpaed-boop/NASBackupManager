using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WinForms = System.Windows.Forms;

namespace NasBackupManager;

public partial class MainViewModel : ObservableObject
{
    private readonly ScanService _scanService = new();
    private readonly ResumableCopyService _copyService = new();

    private CancellationTokenSource? _cancellationTokenSource;

    [ObservableProperty]
    private string _nas1Path = string.Empty;

    [ObservableProperty]
    private string _nas2Path = string.Empty;

    [ObservableProperty]
    private string _backupPath = string.Empty;

    [ObservableProperty]
    private string _status =
        "Prêt. Configurez les chemins de vos sources.";

    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    private bool _dryRun = true;

    [ObservableProperty]
    private int _filesFound;

    [ObservableProperty]
    private int _duplicateCount;

    [ObservableProperty]
    private int _newFilesCount;

    public ObservableCollection<MediaFile> Files { get; } = new();

    public ObservableCollection<CopyOperation> CopyQueue { get; } = new();

    public ObservableCollection<DiagnosticEntry> Diagnostics =>
        DiagnosticStore.Instance.Entries;

    [RelayCommand]
    private void BrowseNas1()
    {
        Nas1Path = SelectFolder(Nas1Path);
    }

    [RelayCommand]
    private void BrowseNas2()
    {
        Nas2Path = SelectFolder(Nas2Path);
    }

    [RelayCommand]
    private void BrowseBackup()
    {
        BackupPath = SelectFolder(BackupPath);
    }

    [RelayCommand]
    private async Task ScanAsync()
    {
        CancelCurrentOperation();

        _cancellationTokenSource = new CancellationTokenSource();

        IsBusy = true;

        Files.Clear();
        CopyQueue.Clear();

        try
        {
            var progress = new Progress<string>(
                message => Status = message);

            var roots = new Dictionary<SourceKind, string>
            {
                [SourceKind.Nas1] = Nas1Path,
                [SourceKind.Nas2] = Nas2Path,
                [SourceKind.Backup] = BackupPath
            };

            var result = await _scanService.ScanAsync(
                roots,
                progress,
                _cancellationTokenSource.Token);

            foreach (var item in result.Items)
            {
                Files.Add(item);
            }

            foreach (var item in result.NewFiles)
            {
                if (string.IsNullOrWhiteSpace(BackupPath))
                {
                    continue;
                }

                var relativeDirectory =
                    item.RelativeDirectory == "."
                        ? string.Empty
                        : item.RelativeDirectory;

                var destination = Path.Combine(
                    BackupPath,
                    relativeDirectory,
                    item.FileName);

                CopyQueue.Add(new CopyOperation
                {
                    Item = item,
                    Destination = destination
                });
            }

            FilesFound = result.Items.Count;
            DuplicateCount = result.DuplicateGroups.Count;
            NewFilesCount = result.NewFiles.Count;

            Status =
                $"Analyse terminée : {FilesFound} fichiers, " +
                $"{DuplicateCount} groupes de doublons, " +
                $"{NewFilesCount} fichiers à sauvegarder.";

            if (result.WarningCount > 0)
            {
                Status +=
                    $" {result.WarningCount} avertissement(s), " +
                    "voir Diagnostic.";
            }
        }
        catch (OperationCanceledException)
        {
            Status = "Analyse annulée.";
        }
        catch (Exception exception)
        {
            Status = "Erreur pendant le scan : " + exception.Message;

            DiagnosticStore.Instance.Error(
                "Scan",
                exception.Message,
                exception);
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task CopyAsync()
    {
        if (CopyQueue.Count == 0)
        {
            Status = "Aucun fichier nouveau à copier.";
            return;
        }

        if (string.IsNullOrWhiteSpace(BackupPath))
        {
            Status =
                "Choisissez un dossier de sauvegarde avant la copie.";
            return;
        }

        _cancellationTokenSource ??= new CancellationTokenSource();

        IsBusy = true;

        try
        {
            var progress = new Progress<string>(
                message => Status = message);

            await _copyService.RunAsync(
                CopyQueue,
                DryRun,
                progress,
                _cancellationTokenSource.Token);

            Status = DryRun
                ? "Simulation terminée. Aucun fichier n'a été écrit."
                : "Copie terminée. Les échecs peuvent être relancés.";
        }
        catch (OperationCanceledException)
        {
            Status = "Copie annulée.";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task RetryFailedAsync()
    {
        foreach (var operation in CopyQueue.Where(
                     operation =>
                         operation.State == OperationState.Failed))
        {
            operation.State = OperationState.Pending;
            operation.Error = null;
        }

        await CopyAsync();
    }

    [RelayCommand]
    private void Cancel()
    {
        CancelCurrentOperation();
        Status = "Annulation demandée...";
    }

    private void CancelCurrentOperation()
    {
        if (_cancellationTokenSource is null)
        {
            return;
        }

        if (!_cancellationTokenSource.IsCancellationRequested)
        {
            _cancellationTokenSource.Cancel();
        }

        _cancellationTokenSource.Dispose();
        _cancellationTokenSource = null;
    }

    private static string SelectFolder(string currentPath)
    {
        using var dialog = new WinForms.FolderBrowserDialog
        {
            Description = "Choisir un dossier",
            SelectedPath = currentPath,
            UseDescriptionForTitle = true
        };

        return dialog.ShowDialog() == WinForms.DialogResult.OK
            ? dialog.SelectedPath
            : currentPath;
    }
}