using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using R3P.Hivemind.Core.Interop;
using R3P.Hivemind.Desktop.Interop;

namespace R3P.Hivemind.Desktop;

public partial class MainWindow : Window, INotifyPropertyChanged
{
    private readonly AutoCadConnector _autoCadConnector;
    private readonly IpcClient _ipcClient;
    private readonly ObservableCollection<IpcNotification> _diagnostics = new();
    private readonly SemaphoreSlim _initializationGate = new(1, 1);

    private string _autoCadStatus = "AutoCAD automation not initialised.";
    private string _ipcStatus = "IPC channel disconnected.";
    private string _handshakeSummary = "Handshake not established.";
    private CancellationTokenSource? _initializationCts;

    public MainWindow(AutoCadConnector autoCadConnector, IpcClient ipcClient)
    {
        _autoCadConnector = autoCadConnector;
        _ipcClient = ipcClient;

        InitializeComponent();
        DataContext = this;

        _autoCadConnector.ConnectionChanged += OnAutoCadConnectionChanged;
        _ipcClient.DiagnosticsReceived += OnDiagnosticsReceived;
        _ipcClient.ConnectionStateChanged += OnIpcConnectionStateChanged;

        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public ObservableCollection<IpcNotification> Diagnostics => _diagnostics;

    public string AutoCadStatus
    {
        get => _autoCadStatus;
        private set => SetProperty(ref _autoCadStatus, value);
    }

    public string IpcStatus
    {
        get => _ipcStatus;
        private set => SetProperty(ref _ipcStatus, value);
    }

    public string HandshakeSummary
    {
        get => _handshakeSummary;
        private set => SetProperty(ref _handshakeSummary, value);
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        _initializationCts = new CancellationTokenSource();
        try
        {
            await InitializeAsync(_initializationCts.Token);
        }
        catch (OperationCanceledException)
        {
            // Ignore cancellation when the window is closing.
        }
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        _initializationCts?.Cancel();
        _initializationCts?.Dispose();
        _initializationCts = null;

        _autoCadConnector.ConnectionChanged -= OnAutoCadConnectionChanged;
        _ipcClient.DiagnosticsReceived -= OnDiagnosticsReceived;
        _ipcClient.ConnectionStateChanged -= OnIpcConnectionStateChanged;
    }

    private async Task InitializeAsync(CancellationToken cancellationToken)
    {
        await _initializationGate.WaitAsync(cancellationToken);
        try
        {
            AutoCadStatus = "Probing AutoCAD…";
            var autoCadResult = await _autoCadConnector.ConnectAsync(true, cancellationToken);
            AutoCadStatus = autoCadResult.Status;

            IpcStatus = "Connecting to conduit…";
            HandshakeSummary = "Negotiating handshake…";
            try
            {
                var handshake = await _ipcClient.ConnectAsync(cancellationToken);
                IpcStatus = "IPC connected.";
                HandshakeSummary = $"Connected to {handshake.ServerName} v{handshake.ServerVersion} (PID: {handshake.ProcessId?.ToString() ?? "n/a"}).";
                await _ipcClient.SendDiagnosticsSubscriptionAsync(true, cancellationToken);
            }
            catch (Exception ex)
            {
                IpcStatus = $"IPC connection failed: {ex.Message}";
                HandshakeSummary = "IPC handshake failed.";
                AppendDiagnostic(new IpcNotification("Error", ex.Message));
            }
        }
        finally
        {
            _initializationGate.Release();
        }
    }

    private void OnAutoCadConnectionChanged(object? sender, AutoCadConnectionResult e)
    {
        Dispatcher.Invoke(() => AutoCadStatus = e.Status);
    }

    private void OnIpcConnectionStateChanged(object? sender, IpcConnectionState state)
    {
        Dispatcher.Invoke(() =>
        {
            IpcStatus = state switch
            {
                IpcConnectionState.Connected => "IPC connected.",
                IpcConnectionState.Connecting => "IPC connecting…",
                IpcConnectionState.Faulted => "IPC faulted - check conduit host.",
                _ => "IPC disconnected."
            };
        });
    }

    private void OnDiagnosticsReceived(object? sender, IpcNotification notification)
    {
        AppendDiagnostic(notification);
    }

    private void AppendDiagnostic(IpcNotification notification)
    {
        Dispatcher.Invoke(() =>
        {
            _diagnostics.Insert(0, notification);
            const int maxEntries = 200;
            while (_diagnostics.Count > maxEntries)
            {
                _diagnostics.RemoveAt(_diagnostics.Count - 1);
            }
        });
    }

    private async void ReconnectIpc_OnClick(object sender, RoutedEventArgs e)
    {
        try
        {
            await _ipcClient.DisposeAsync();
            IpcStatus = "Reconnecting…";
            HandshakeSummary = "Negotiating handshake…";
            await _ipcClient.ConnectAsync();
            IpcStatus = "IPC connected.";
            var handshake = _ipcClient.Handshake;
            if (handshake is not null)
            {
                HandshakeSummary = $"Connected to {handshake.ServerName} v{handshake.ServerVersion} (PID: {handshake.ProcessId?.ToString() ?? "n/a"}).";
            }
            await _ipcClient.SendDiagnosticsSubscriptionAsync(true);
        }
        catch (Exception ex)
        {
            AppendDiagnostic(new IpcNotification("Error", ex.Message));
            IpcStatus = $"IPC reconnection failed: {ex.Message}";
            HandshakeSummary = "IPC handshake failed.";
        }
    }

    private async void AttachAutoCad_OnClick(object sender, RoutedEventArgs e)
    {
        try
        {
            AutoCadStatus = "Attempting to attach to AutoCAD…";
            var result = await _autoCadConnector.ConnectAsync();
            AutoCadStatus = result.Status;
        }
        catch (Exception ex)
        {
            AutoCadStatus = $"AutoCAD connection failed: {ex.Message}";
        }
    }

    private async void RunConduitRefresh_OnClick(object sender, RoutedEventArgs e)
    {
        try
        {
            IpcStatus = "Dispatching conduit refresh…";
            var payload = new JsonObject
            {
                ["requestedBy"] = Environment.UserName,
                ["timestamp"] = DateTimeOffset.UtcNow
            };
            var response = await _ipcClient.SendAsync(new IpcRequest(HivemindIpcCommands.ConduitRefresh, payload));
            if (!response.Success)
            {
                AppendDiagnostic(new IpcNotification("Error", response.Error ?? "Conduit refresh failed", response.Payload));
            }
            else
            {
                AppendDiagnostic(new IpcNotification("Info", "Conduit refresh command dispatched successfully.", response.Payload));
            }
        }
        catch (Exception ex)
        {
            AppendDiagnostic(new IpcNotification("Error", ex.Message));
            IpcStatus = $"Conduit command failed: {ex.Message}";
        }
        finally
        {
            IpcStatus = _ipcClient.State == IpcConnectionState.Connected ? "IPC connected." : IpcStatus;
        }
    }

    private void SetProperty<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return;
        }

        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
