using System;
using System.Collections.Concurrent;
using System.IO;
using System.IO.Pipes;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using R3P.Hivemind.Core.Interop;

namespace R3P.Hivemind.Desktop.Interop;

/// <summary>
/// Handles IPC communication with the AutoCAD plug-in. The client is transport-aware for
/// named pipes but operates on the shared Core abstractions so the plug-in can reuse the
/// same contracts.
/// </summary>
public sealed class IpcClient : IAsyncDisposable
{
    private readonly HivemindIpcClientOptions _options;
    private readonly JsonSerializerOptions _serializerOptions;
    private readonly SemaphoreSlim _writeLock = new(1, 1);
    private readonly SemaphoreSlim _connectLock = new(1, 1);
    private readonly ConcurrentDictionary<string, TaskCompletionSource<IpcResponse>> _pendingRequests = new();

    private NamedPipeClientStream? _pipe;
    private CancellationTokenSource? _listenerCts;
    private Task? _listenerTask;
    private TaskCompletionSource<IpcHandshake>? _handshakeCompletion;
    private IpcConnectionState _state = IpcConnectionState.Disconnected;

    public IpcClient(HivemindIpcClientOptions? options = null)
    {
        _options = options ?? new HivemindIpcClientOptions();
        _serializerOptions = new JsonSerializerOptions(JsonSerializerDefaults.Web)
        {
            WriteIndented = false
        };
    }

    public event EventHandler<IpcNotification>? DiagnosticsReceived;

    public event EventHandler<IpcConnectionState>? ConnectionStateChanged;

    public bool IsConnected => _pipe?.IsConnected == true;

    public IpcHandshake? Handshake { get; private set; }

    public IpcConnectionState State
    {
        get => _state;
        private set
        {
            if (_state == value)
            {
                return;
            }

            _state = value;
            ConnectionStateChanged?.Invoke(this, value);
        }
    }

    public async Task<IpcHandshake> ConnectAsync(CancellationToken cancellationToken = default)
    {
        if (IsConnected && Handshake is not null)
        {
            return Handshake;
        }

        await _connectLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (IsConnected && Handshake is not null)
            {
                return Handshake;
            }

            State = IpcConnectionState.Connecting;

            _pipe = new NamedPipeClientStream(".", _options.PipeName, PipeDirection.InOut, PipeOptions.Asynchronous);
            using var connectCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            connectCts.CancelAfter(_options.ConnectTimeout);
            await _pipe.ConnectAsync(connectCts.Token).ConfigureAwait(false);

            _listenerCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            _listenerTask = Task.Run(() => ListenAsync(_listenerCts.Token));

            _handshakeCompletion = new TaskCompletionSource<IpcHandshake>(TaskCreationOptions.RunContinuationsAsynchronously);
            var handshakeRequest = new IpcHandshakeRequest(_options.ClientName, _options.ClientVersion, _options.Capabilities);
            await SendWireMessageAsync(HivemindIpcWireMessage.ForHandshake(handshakeRequest), cancellationToken).ConfigureAwait(false);

            using var handshakeTimeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            handshakeTimeout.CancelAfter(_options.ResponseTimeout);
            using var handshakeRegistration = handshakeTimeout.Token.Register(static state =>
            {
                var completion = (TaskCompletionSource<IpcHandshake>)state!;
                completion.TrySetCanceled();
            }, _handshakeCompletion, useSynchronizationContext: false);

            Handshake = await _handshakeCompletion.Task.ConfigureAwait(false);

            State = IpcConnectionState.Connected;
            return Handshake;
        }
        catch (OperationCanceledException)
        {
            State = IpcConnectionState.Faulted;
            throw;
        }
        catch
        {
            State = IpcConnectionState.Faulted;
            throw;
        }
        finally
        {
            _connectLock.Release();
        }
    }

    public async Task<IpcResponse> SendAsync(IpcRequest request, CancellationToken cancellationToken = default)
    {
        await EnsureConnectedAsync(cancellationToken).ConfigureAwait(false);

        var correlationId = request.CorrelationId ?? Guid.NewGuid().ToString("N");
        var correlatedRequest = request with { CorrelationId = correlationId };
        var message = HivemindIpcWireMessage.ForRequest(correlatedRequest);

        var completionSource = new TaskCompletionSource<IpcResponse>(TaskCreationOptions.RunContinuationsAsynchronously);
        _pendingRequests[correlationId] = completionSource;

        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(_options.ResponseTimeout);

        using var timeoutRegistration = timeoutCts.Token.Register(static state =>
        {
            var (dictionary, key) = ((ConcurrentDictionary<string, TaskCompletionSource<IpcResponse>>, string))state!;
            if (dictionary.TryRemove(key, out var tcs))
            {
                tcs.TrySetCanceled();
            }
        }, (_pendingRequests, correlationId), useSynchronizationContext: false);

        await SendWireMessageAsync(message, cancellationToken).ConfigureAwait(false);
        return await completionSource.Task.ConfigureAwait(false);
    }

    public async Task SendDiagnosticsSubscriptionAsync(bool subscribe, CancellationToken cancellationToken = default)
    {
        var command = subscribe ? HivemindIpcCommands.DiagnosticsSubscribe : HivemindIpcCommands.DiagnosticsUnsubscribe;
        var payload = new JsonObject
        {
            ["client"] = _options.ClientName
        };
        await SendAsync(new IpcRequest(command, payload), cancellationToken).ConfigureAwait(false);
    }

    public async ValueTask DisposeAsync()
    {
        await _connectLock.WaitAsync().ConfigureAwait(false);
        try
        {
            State = IpcConnectionState.Disconnected;

            if (_listenerCts is not null)
            {
                try
                {
                    _listenerCts.Cancel();
                    if (_listenerTask is not null)
                    {
                        await Task.WhenAny(_listenerTask, Task.Delay(500)).ConfigureAwait(false);
                    }
                }
                finally
                {
                    _listenerCts.Dispose();
                    _listenerCts = null;
                }
            }

            if (_pipe is not null)
            {
                await _pipe.DisposeAsync().ConfigureAwait(false);
                _pipe = null;
            }

            foreach (var pending in _pendingRequests.Values)
            {
                pending.TrySetCanceled();
            }
            _pendingRequests.Clear();

            _handshakeCompletion?.TrySetCanceled();
            _handshakeCompletion = null;
            Handshake = null;
        }
        finally
        {
            _connectLock.Release();
        }
    }

    private async Task EnsureConnectedAsync(CancellationToken cancellationToken)
    {
        if (!IsConnected)
        {
            await ConnectAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    private async Task SendWireMessageAsync(HivemindIpcWireMessage message, CancellationToken cancellationToken)
    {
        if (_pipe is null)
        {
            throw new InvalidOperationException("The IPC pipe is not connected.");
        }

        var json = JsonSerializer.Serialize(message, _serializerOptions);
        var bytes = Encoding.UTF8.GetBytes(json + Environment.NewLine);

        await _writeLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await _pipe.WriteAsync(bytes, 0, bytes.Length, cancellationToken).ConfigureAwait(false);
            await _pipe.FlushAsync(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _writeLock.Release();
        }
    }

    private async Task ListenAsync(CancellationToken cancellationToken)
    {
        if (_pipe is null)
        {
            return;
        }

        try
        {
            using var reader = new StreamReader(_pipe, Encoding.UTF8, leaveOpen: true);
            while (!cancellationToken.IsCancellationRequested)
            {
                var line = await reader.ReadLineAsync(cancellationToken).ConfigureAwait(false);
                if (line is null)
                {
                    break;
                }

                var message = JsonSerializer.Deserialize<HivemindIpcWireMessage>(line, _serializerOptions);
                if (message is null)
                {
                    continue;
                }

                ProcessIncomingMessage(message);
            }
        }
        catch (OperationCanceledException)
        {
            // Expected during shutdown.
        }
        catch
        {
            State = IpcConnectionState.Faulted;
        }
        finally
        {
            State = IpcConnectionState.Disconnected;
            _handshakeCompletion?.TrySetCanceled();
            foreach (var key in _pendingRequests.Keys)
            {
                if (_pendingRequests.TryRemove(key, out var pending))
                {
                    pending.TrySetCanceled();
                }
            }
        }
    }

    private void ProcessIncomingMessage(HivemindIpcWireMessage message)
    {
        switch (message.Kind)
        {
            case HivemindIpcMessageKinds.HandshakeAcknowledgement:
                _handshakeCompletion?.TrySetResult(message.ToHandshake());
                break;
            case HivemindIpcMessageKinds.Response:
                if (message.CorrelationId is not null && _pendingRequests.TryRemove(message.CorrelationId, out var pending))
                {
                    pending.TrySetResult(message.ToResponse());
                }
                break;
            case HivemindIpcMessageKinds.Diagnostics:
                DiagnosticsReceived?.Invoke(this, message.ToNotification());
                break;
        }
    }
}
