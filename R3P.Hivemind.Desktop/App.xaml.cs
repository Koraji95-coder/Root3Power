using System.Threading.Tasks;
using System.Windows;
using R3P.Hivemind.Desktop.Interop;

namespace R3P.Hivemind.Desktop;

public partial class App : Application
{
    private readonly AutoCadConnector _autoCadConnector = new();
    private readonly IpcClient _ipcClient = new();

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        var mainWindow = new MainWindow(_autoCadConnector, _ipcClient);
        MainWindow = mainWindow;
        mainWindow.Show();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _autoCadConnector.Disconnect();
        _ipcClient.DisposeAsync().AsTask().GetAwaiter().GetResult();

        base.OnExit(e);
    }
}
