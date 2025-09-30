namespace R3P.Hivemind.Core.Interop;

/// <summary>
/// Response returned from the AutoCAD plug-in after a successful handshake. The desktop
/// process can use this information to display connection details and diagnose mismatched
/// versions when troubleshooting the conduit.
/// </summary>
/// <param name="ServerName">Logical name of the service accepting IPC requests.</param>
/// <param name="ServerVersion">Version string reported by the plug-in.</param>
/// <param name="ProcessId">Process identifier associated with the AutoCAD host, when available.</param>
/// <param name="Capabilities">Optional features the service is willing to honor.</param>
public sealed record IpcHandshake(
    string ServerName,
    string ServerVersion,
    int? ProcessId,
    IReadOnlyCollection<string> Capabilities)
{
    /// <summary>
    /// Creates a response with an empty capability list.
    /// </summary>
    public IpcHandshake(string serverName, string serverVersion, int? processId = null)
        : this(serverName, serverVersion, processId, Array.Empty<string>())
    {
    }
}
