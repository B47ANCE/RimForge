using System;
using RimForge.Protocol.Contracts;

namespace RimForge.Protocol.Serialization;

public static class EnvelopeValidator
{
    public static void Validate(RimForgeEnvelope envelope)
    {
        if (envelope is null) throw new ArgumentNullException(nameof(envelope));
        if (envelope.ProtocolVersion != ProtocolConstants.CurrentVersion)
            throw new InvalidOperationException($"Unsupported protocol version {envelope.ProtocolVersion}.");
        if (string.IsNullOrWhiteSpace(envelope.MessageType))
            throw new InvalidOperationException("Message type is required.");
        if (!Guid.TryParse(envelope.MessageId, out _))
            throw new InvalidOperationException("Message ID must be a GUID.");
        if (string.IsNullOrWhiteSpace(envelope.SessionId))
            throw new InvalidOperationException("Session ID is required.");
        if (envelope.TimestampUtc == default)
            throw new InvalidOperationException("Timestamp is required.");
        if (envelope.Payload is null)
            throw new InvalidOperationException("Payload is required.");
    }
}
