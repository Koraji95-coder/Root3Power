using System.Collections.ObjectModel;

namespace R3P.Hivemind.Core.Interop;

/// <summary>
/// Represents a handshake initiated by the desktop application when connecting to the
/// AutoCAD plug-in. The request communicates high level identity and capability flags
/// so both sides can negotiate optional behaviours.
/// </summary>
/// <param name="ClientName">Friendly identifier for the connecting client.</param>
/// <param name="ClientVersion">Semantic version of the client.</param>
/// <param name="Capabilities">Optional capability switches supported by the client.</param>
public sealed record IpcHandshakeRequest(
    string ClientName,
    string ClientVersion,
    IReadOnlyCollection<string> Capabilities)
{
    /// <summary>
    /// Creates a request with an empty capability list.
    /// </summary>
    public IpcHandshakeRequest(string clientName, string clientVersion)
        : this(clientName, clientVersion, Array.Empty<string>())
    {
    }

    /// <summary>
    /// Provides a mutable builder that makes it convenient to compose the capability list.
    /// </summary>
    /// <returns>A builder backed by an observable collection.</returns>
    public Collection<string> CreateCapabilityBuilder() => new();
}
