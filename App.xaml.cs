using System.Windows;
using System.Windows.Threading;

namespace NasBackupManager;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        DispatcherUnhandledException += OnDispatcherUnhandledException;

        var window = new MainWindow
        {
            DataContext = new MainViewModel()
        };

        window.Show();
    }

    private static void OnDispatcherUnhandledException(
        object sender,
        DispatcherUnhandledExceptionEventArgs e)
    {
        DiagnosticStore.Instance.Error(
            "Interface",
            e.Exception.Message,
            e.Exception);

        MessageBox.Show(
            "Une erreur inattendue est survenue.\n\n" +
            e.Exception.Message +
            "\n\nLe détail est disponible dans l'onglet Diagnostic.",
            "NAS Backup Manager",
            MessageBoxButton.OK,
            MessageBoxImage.Error);

        e.Handled = true;
    }
}