using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

namespace R3P.Hivemind.Desktop.Interop;

/// <summary>
/// Handles launching or attaching to AutoCAD through COM automation. The connector keeps
/// the automation object alive and raises events when the connection state changes so the
/// desktop application can orchestrate conduit workflows.
/// </summary>
public sealed class AutoCadConnector
{
    private readonly string _progId;
    private readonly TimeSpan _readyTimeout;
    private object? _automationObject;

    public AutoCadConnector(string progId = "AutoCAD.Application", TimeSpan? readyTimeout = null)
    {
        _progId = progId;
        _readyTimeout = readyTimeout ?? TimeSpan.FromSeconds(30);
    }

    /// <summary>
    /// Raised whenever the connector transitions to a new state.
    /// </summary>
    public event EventHandler<AutoCadConnectionResult>? ConnectionChanged;

    /// <summary>
    /// Gets a value indicating whether a COM automation object has been acquired.
    /// </summary>
    public bool IsConnected => _automationObject is not null;

    /// <summary>
    /// Attempts to either attach to an existing AutoCAD session or launch a new instance
    /// if none are currently running.
    /// </summary>
    public async Task<AutoCadConnectionResult> ConnectAsync(bool preferExisting = true, CancellationToken cancellationToken = default)
    {
        try
        {
            if (IsConnected)
            {
                var alreadyConnected = AutoCadConnectionResult.Attached("An AutoCAD automation session is already active.");
                OnConnectionChanged(alreadyConnected);
                return alreadyConnected;
            }

            var autoCadType = Type.GetTypeFromProgID(_progId, throwOnError: false);
            if (autoCadType is null)
            {
                var missing = AutoCadConnectionResult.Failed($"AutoCAD COM ProgID '{_progId}' was not found. Ensure AutoCAD is installed on this workstation.");
                OnConnectionChanged(missing);
                return missing;
            }

            if (preferExisting)
            {
                var existing = TryGetRunningInstance();
                if (existing is not null)
                {
                    _automationObject = existing;
                    var attached = AutoCadConnectionResult.Attached("Attached to an existing AutoCAD session.");
                    OnConnectionChanged(attached);
                    return attached;
                }
            }

            cancellationToken.ThrowIfCancellationRequested();

            var instance = Activator.CreateInstance(autoCadType);
            if (instance is null)
            {
                var failedCreate = AutoCadConnectionResult.Failed("Unable to create the AutoCAD COM automation object.");
                OnConnectionChanged(failedCreate);
                return failedCreate;
            }

            _automationObject = instance;
            await WaitForApplicationReadyAsync(instance, cancellationToken).ConfigureAwait(false);

            var launched = AutoCadConnectionResult.Launched("Started a new AutoCAD session via COM automation.");
            OnConnectionChanged(launched);
            return launched;
        }
        catch (OperationCanceledException)
        {
            var cancelled = AutoCadConnectionResult.Failed("AutoCAD launch was cancelled by the caller.");
            OnConnectionChanged(cancelled);
            return cancelled;
        }
        catch (Exception ex)
        {
            _automationObject = null;
            var error = AutoCadConnectionResult.Failed($"AutoCAD automation error: {ex.Message}");
            OnConnectionChanged(error);
            return error;
        }
    }

    /// <summary>
    /// Releases the COM automation object and resets the connector state.
    /// </summary>
    public void Disconnect()
    {
        if (_automationObject is null)
        {
            return;
        }

        try
        {
            Marshal.FinalReleaseComObject(_automationObject);
        }
        catch
        {
            // Ignore cleanup exceptions - AutoCAD will release the automation object when ready.
        }
        finally
        {
            _automationObject = null;
            OnConnectionChanged(AutoCadConnectionResult.Disconnected("AutoCAD automation object released."));
        }
    }

    private object? TryGetRunningInstance()
    {
        try
        {
            return Marshal.GetActiveObject(_progId);
        }
        catch (COMException)
        {
            return null;
        }
    }

    private async Task WaitForApplicationReadyAsync(object automationObject, CancellationToken cancellationToken)
    {
        var start = DateTime.UtcNow;
        while (DateTime.UtcNow - start < _readyTimeout)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                var documents = automationObject.GetType().InvokeMember(
                    "Documents",
                    System.Reflection.BindingFlags.GetProperty,
                    binder: null,
                    target: automationObject,
                    args: null);

                if (documents is not null)
                {
                    return;
                }
            }
            catch (COMException)
            {
                // AutoCAD is still starting up - keep polling.
            }

            await Task.Delay(TimeSpan.FromMilliseconds(250), cancellationToken).ConfigureAwait(false);
        }
    }

    private void OnConnectionChanged(AutoCadConnectionResult result)
    {
        ConnectionChanged?.Invoke(this, result);
    }
}

/// <summary>
/// Represents the outcome of an AutoCAD connection attempt.
/// </summary>
/// <param name="Status">Human-readable status message describing the connection state.</param>
/// <param name="State">The resulting state after the operation completes.</param>
public sealed record AutoCadConnectionResult(string Status, AutoCadConnectionState State)
{
    public static AutoCadConnectionResult Launched(string status)
        => new(status, AutoCadConnectionState.Launched);

    public static AutoCadConnectionResult Attached(string status)
        => new(status, AutoCadConnectionState.Attached);

    public static AutoCadConnectionResult Failed(string status)
        => new(status, AutoCadConnectionState.Failed);

    public static AutoCadConnectionResult Disconnected(string status)
        => new(status, AutoCadConnectionState.Disconnected);
}

/// <summary>
/// Describes the AutoCAD automation state.
/// </summary>
public enum AutoCadConnectionState
{
    Disconnected,
    Attached,
    Launched,
    Failed
}
