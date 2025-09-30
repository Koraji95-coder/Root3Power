using System.Threading;
using System.Threading.Tasks;

namespace R3P.Hivemind.Core.Interop;

/// <summary>
/// Defines the shared contract that the desktop coordinator and AutoCAD plug-in use
/// to communicate over an IPC channel. The abstraction is intentionally transport
/// agnostic so it can back a named pipe, WCF channel or any other duplex transport.
/// </summary>
public interface IHivemindIpcService
{
    /// <summary>
    /// Gets the logical name of the IPC channel. This is typically the name of the
    /// named pipe or WCF endpoint used to exchange messages.
    /// </summary>
    string ChannelName { get; }

    /// <summary>
    /// Performs the initial handshake so both sides can advertise versions and
    /// capabilities before subsequent conduit commands are issued.
    /// </summary>
    /// <param name="request">The metadata describing the connecting client.</param>
    /// <param name="cancellationToken">Token that propagates cancellation.</param>
    /// <returns>Information about the service endpoint that accepted the connection.</returns>
    Task<IpcHandshake> HandshakeAsync(IpcHandshakeRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Sends a conduit request to the plug-in and awaits a response. The <see cref="IpcRequest"/>
    /// and <see cref="IpcResponse"/> payloads are lightweight JSON documents so both managed
    /// and unmanaged components can participate without a heavy serialization dependency.
    /// </summary>
    /// <param name="request">The logical request to execute.</param>
    /// <param name="cancellationToken">Token that propagates cancellation.</param>
    /// <returns>The response returned by the conduit.</returns>
    Task<IpcResponse> ExecuteAsync(IpcRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Publishes diagnostics produced by the plug-in. Desktop components can subscribe to
    /// these notifications to surface a live console or persist them to disk.
    /// </summary>
    /// <param name="notification">The diagnostic notification to broadcast.</param>
    /// <param name="cancellationToken">Token that propagates cancellation.</param>
    Task PublishDiagnosticsAsync(IpcNotification notification, CancellationToken cancellationToken = default);
}
