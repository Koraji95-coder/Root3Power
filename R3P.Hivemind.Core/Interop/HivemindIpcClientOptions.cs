namespace R3P.Hivemind.Core.Interop;

/// <summary>
/// Configures how the desktop coordinator connects to the plug-in via IPC.
/// </summary>
public sealed class HivemindIpcClientOptions
{
    /// <summary>
    /// Gets or sets the pipe or endpoint name used for communication.
    /// </summary>
    public string PipeName { get; set; } = HivemindIpcDefaults.DefaultPipeName;

    /// <summary>
    /// Gets or sets the logical client name sent during the handshake.
    /// </summary>
    public string ClientName { get; set; } = "R3P.Hivemind.Desktop";

    /// <summary>
    /// Gets or sets the version reported to the plug-in during the handshake.
    /// </summary>
    public string ClientVersion { get; set; } = typeof(HivemindIpcClientOptions).Assembly.GetName().Version?.ToString() ?? "0.0.0";

    /// <summary>
    /// Gets or sets the capability set advertised to the plug-in.
    /// </summary>
    public IReadOnlyCollection<string> Capabilities { get; set; } = Array.Empty<string>();

    /// <summary>
    /// Gets or sets the maximum amount of time to wait for the pipe to become available.
    /// </summary>
    public TimeSpan ConnectTimeout { get; set; } = TimeSpan.FromSeconds(15);

    /// <summary>
    /// Gets or sets the timeout applied to requests waiting for responses from the plug-in.
    /// </summary>
    public TimeSpan ResponseTimeout { get; set; } = TimeSpan.FromSeconds(30);
}
