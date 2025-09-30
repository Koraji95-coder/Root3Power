using System.Text.Json.Nodes;

namespace R3P.Hivemind.Core.Interop;

/// <summary>
/// Describes the result returned by the conduit for a specific request.
/// </summary>
/// <param name="Success">Indicates whether the request succeeded.</param>
/// <param name="Payload">Optional JSON payload returned by the conduit.</param>
/// <param name="Error">Error description when <see cref="Success"/> is false.</param>
/// <param name="CorrelationId">Correlation identifier that ties the response to its request.</param>
public sealed record IpcResponse(
    bool Success,
    JsonObject? Payload,
    string? Error,
    string? CorrelationId)
{
    /// <summary>
    /// Creates a successful response that only contains a payload.
    /// </summary>
    public static IpcResponse Ok(JsonObject? payload = null, string? correlationId = null)
        => new(true, payload, null, correlationId);

    /// <summary>
    /// Creates a failed response with an error message.
    /// </summary>
    public static IpcResponse Failed(string error, string? correlationId = null, JsonObject? payload = null)
        => new(false, payload, error, correlationId);
}
