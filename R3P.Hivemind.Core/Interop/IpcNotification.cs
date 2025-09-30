using System.Text.Json.Nodes;

namespace R3P.Hivemind.Core.Interop;

/// <summary>
/// Represents a diagnostic event or informational message flowing across the IPC channel.
/// </summary>
/// <param name="Category">Logical category or severity (e.g. Info, Warning, Error).</param>
/// <param name="Message">Human readable message.</param>
/// <param name="Payload">Optional structured payload that accompanies the message.</param>
public sealed record IpcNotification(
    string Category,
    string Message,
    JsonObject? Payload)
{
    /// <summary>
    /// Timestamp the notification was created on the originating process.
    /// </summary>
    public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// Creates a notification without a payload.
    /// </summary>
    public IpcNotification(string category, string message)
        : this(category, message, null)
    {
    }
}
