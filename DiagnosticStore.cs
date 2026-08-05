using System.Collections.ObjectModel;
using System.Windows;

namespace NasBackupManager;

public sealed class DiagnosticStore
{
    public static DiagnosticStore Instance { get; } = new();

    public ObservableCollection<DiagnosticEntry> Entries { get; } = new();

    private DiagnosticStore()
    {
    }

    public void Info(
        string area,
        string message,
        string? path = null)
    {
        Add(DiagnosticLevel.Info, area, message, path, null);
    }

    public void Warning(
        string area,
        string message,
        string? path = null)
    {
        Add(DiagnosticLevel.Warning, area, message, path, null);
    }

    public void Error(
        string area,
        string message,
        Exception? exception = null,
        string? path = null)
    {
        Add(DiagnosticLevel.Error, area, message, path, exception);
    }

    private void Add(
        DiagnosticLevel level,
        string area,
        string message,
        string? path,
        Exception? exception)
    {
        var entry = new DiagnosticEntry(
            DateTimeOffset.Now,
            level,
            area,
            message,
            path,
            exception);

        var dispatcher = Application.Current?.Dispatcher;

        if (dispatcher is null || dispatcher.CheckAccess())
        {
            Entries.Insert(0, entry);
            return;
        }

        dispatcher.Invoke(() => Entries.Insert(0, entry));
    }
}