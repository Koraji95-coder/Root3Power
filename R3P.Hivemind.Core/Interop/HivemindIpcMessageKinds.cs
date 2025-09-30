namespace R3P.Hivemind.Core.Interop;

/// <summary>
/// Defines the well known message kinds that traverse the Root3Power IPC channel.
/// </summary>
public static class HivemindIpcMessageKinds
{
    public const string HandshakeRequest = "handshake";
    public const string HandshakeAcknowledgement = "handshake-ack";
    public const string Request = "request";
    public const string Response = "response";
    public const string Diagnostics = "diagnostics";
    public const string Heartbeat = "heartbeat";
}
