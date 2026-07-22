using System;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace RimForge.Protocol.Contracts;

public sealed class RimForgeEnvelope
{
    [JsonProperty("protocolVersion", Required = Required.Always)]
    public int ProtocolVersion { get; set; } = ProtocolConstants.CurrentVersion;

    [JsonProperty("messageType", Required = Required.Always)]
    public string MessageType { get; set; } = string.Empty;

    [JsonProperty("messageId", Required = Required.Always)]
    public string MessageId { get; set; } = Guid.NewGuid().ToString("D");

    [JsonProperty("sessionId", Required = Required.Always)]
    public string SessionId { get; set; } = string.Empty;

    [JsonProperty("timestampUtc", Required = Required.Always)]
    public DateTime TimestampUtc { get; set; } = DateTime.UtcNow;

    [JsonProperty("payload", Required = Required.Always)]
    public JObject Payload { get; set; } = new JObject();

    public static RimForgeEnvelope Create<T>(string messageType, string sessionId, T payload)
    {
        if (string.IsNullOrWhiteSpace(messageType)) throw new ArgumentException("Message type is required.", nameof(messageType));
        if (string.IsNullOrWhiteSpace(sessionId)) throw new ArgumentException("Session ID is required.", nameof(sessionId));

        return new RimForgeEnvelope
        {
            MessageType = messageType,
            SessionId = sessionId,
            Payload = payload is null ? new JObject() : JObject.FromObject(payload)
        };
    }
}
