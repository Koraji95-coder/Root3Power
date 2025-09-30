namespace R3P.Hivemind.Core.Interop;

/// <summary>
/// Defines defaults that both the AutoCAD plug-in and desktop coordinator rely on when
/// establishing IPC communication.
/// </summary>
public static class HivemindIpcDefaults
{
    /// <summary>
    /// Default named pipe used by the Root3Power conduit.
    /// </summary>
    public const string DefaultPipeName = "r3p.hivemind";

    /// <summary>
    /// Logical service name used during the handshake if the plug-in does not override it.
    /// </summary>
    public const string DefaultServiceName = "Root3Power.Hivemind";
}
