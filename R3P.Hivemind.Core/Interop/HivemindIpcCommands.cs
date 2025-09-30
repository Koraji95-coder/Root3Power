namespace R3P.Hivemind.Core.Interop;

/// <summary>
/// Well known conduit commands exchanged between the desktop host and the AutoCAD plug-in.
/// </summary>
public static class HivemindIpcCommands
{
    public const string Ping = "system.ping";
    public const string DiagnosticsSubscribe = "diagnostics.subscribe";
    public const string DiagnosticsUnsubscribe = "diagnostics.unsubscribe";
    public const string ConduitRefresh = "conduit.refresh";
    public const string ConduitRebuild = "conduit.rebuild";
}
