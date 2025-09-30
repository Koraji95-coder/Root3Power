namespace R3P.Hivemind.Core.Interop;

/// <summary>
/// Represents the lifecycle state of the IPC client connection.
/// </summary>
public enum IpcConnectionState
{
    Disconnected,
    Connecting,
    Connected,
    Faulted
}
