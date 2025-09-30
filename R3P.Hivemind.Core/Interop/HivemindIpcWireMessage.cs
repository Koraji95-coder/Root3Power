using System.Linq;
using System.Text.Json.Nodes;

namespace R3P.Hivemind.Core.Interop;

/// <summary>
/// Represents the serialized message that flows across the IPC transport. The wire message
/// wraps higher-level request/response types with metadata required for routing.
/// </summary>
public sealed class HivemindIpcWireMessage
{
    public string Kind { get; set; } = string.Empty;

    public string? Command { get; set; }

    public string? CorrelationId { get; set; }

    public JsonObject? Payload { get; set; }

    public bool? Success { get; set; }

    public string? Error { get; set; }

    public string? Category { get; set; }

    public string? Message { get; set; }

    public DateTimeOffset Timestamp { get; set; } = DateTimeOffset.UtcNow;

    public static HivemindIpcWireMessage ForHandshake(IpcHandshakeRequest request)
        => new()
        {
            Kind = HivemindIpcMessageKinds.HandshakeRequest,
            Payload = new JsonObject
            {
                ["clientName"] = request.ClientName,
                ["clientVersion"] = request.ClientVersion,
                ["capabilities"] = new JsonArray(request.Capabilities.Select(c => (JsonNode)c).ToArray())
            }
        };

    public static HivemindIpcWireMessage ForHandshake(IpcHandshake handshake)
        => new()
        {
            Kind = HivemindIpcMessageKinds.HandshakeAcknowledgement,
            Payload = new JsonObject
            {
                ["serverName"] = handshake.ServerName,
                ["serverVersion"] = handshake.ServerVersion,
                ["processId"] = handshake.ProcessId,
                ["capabilities"] = new JsonArray(handshake.Capabilities.Select(c => (JsonNode)c).ToArray())
            }
        };

    public static HivemindIpcWireMessage ForRequest(IpcRequest request)
        => new()
        {
            Kind = HivemindIpcMessageKinds.Request,
            Command = request.Command,
            CorrelationId = request.CorrelationId,
            Payload = request.Payload
        };

    public static HivemindIpcWireMessage ForDiagnostics(IpcNotification notification)
        => new()
        {
            Kind = HivemindIpcMessageKinds.Diagnostics,
            Category = notification.Category,
            Message = notification.Message,
            Payload = notification.Payload,
            Timestamp = notification.Timestamp
        };

    public static HivemindIpcWireMessage ForResponse(IpcResponse response)
        => new()
        {
            Kind = HivemindIpcMessageKinds.Response,
            CorrelationId = response.CorrelationId,
            Success = response.Success,
            Payload = response.Payload,
            Error = response.Error
        };

    public IpcHandshake ToHandshake()
    {
        if (Payload is null)
        {
            throw new InvalidOperationException("The wire message does not contain handshake payload data.");
        }

        var serverName = Payload.TryGetPropertyValue("serverName", out var serverNameNode) && serverNameNode is not null
            ? serverNameNode.GetValue<string>()
            : HivemindIpcDefaults.DefaultServiceName;
        var serverVersion = Payload.TryGetPropertyValue("serverVersion", out var serverVersionNode) && serverVersionNode is not null
            ? serverVersionNode.GetValue<string>()
            : "unknown";
        int? processId = null;
        if (Payload.TryGetPropertyValue("processId", out var processIdNode) && processIdNode is not null)
        {
            try
            {
                processId = processIdNode.GetValue<int>();
            }
            catch
            {
                processId = null;
            }
        }

        var capabilities = Array.Empty<string>();
        if (Payload.TryGetPropertyValue("capabilities", out var capabilitiesNode) && capabilitiesNode is JsonArray array)
        {
            capabilities = array
                .Where(node => node is not null)
                .Select(node => node!.GetValue<string>())
                .ToArray();
        }

        return new IpcHandshake(serverName, serverVersion, processId, capabilities);
    }

    public IpcNotification ToNotification()
    {
        var category = string.IsNullOrWhiteSpace(Category) ? "Info" : Category;
        var message = !string.IsNullOrWhiteSpace(Message) ? Message : Error ?? string.Empty;
        return new IpcNotification(category!, message, Payload) { Timestamp = Timestamp };
    }

    public IpcResponse ToResponse()
    {
        return new IpcResponse(Success ?? false, Payload, Error, CorrelationId);
    }
}
