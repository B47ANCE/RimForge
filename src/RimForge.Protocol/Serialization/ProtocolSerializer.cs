using System;
using System.Text;
using Newtonsoft.Json;
using RimForge.Protocol.Contracts;

namespace RimForge.Protocol.Serialization;

public static class ProtocolSerializer
{
    private static readonly JsonSerializerSettings Settings = new JsonSerializerSettings
    {
        Formatting = Formatting.None,
        DateFormatHandling = DateFormatHandling.IsoDateFormat,
        DateTimeZoneHandling = DateTimeZoneHandling.Utc,
        MissingMemberHandling = MissingMemberHandling.Ignore,
        NullValueHandling = NullValueHandling.Include
    };

    public static string Serialize(RimForgeEnvelope envelope)
    {
        EnvelopeValidator.Validate(envelope);
        var json = JsonConvert.SerializeObject(envelope, Settings);
        if (Encoding.UTF8.GetByteCount(json) > ProtocolConstants.MaximumEnvelopeBytes)
            throw new InvalidOperationException("Envelope exceeds the maximum allowed size.");
        return json;
    }

    public static RimForgeEnvelope Deserialize(string json)
    {
        if (string.IsNullOrWhiteSpace(json)) throw new ArgumentException("Envelope JSON is required.", nameof(json));
        if (Encoding.UTF8.GetByteCount(json) > ProtocolConstants.MaximumEnvelopeBytes)
            throw new InvalidOperationException("Envelope exceeds the maximum allowed size.");

        var envelope = JsonConvert.DeserializeObject<RimForgeEnvelope>(json, Settings)
            ?? throw new JsonSerializationException("Envelope deserialized to null.");
        EnvelopeValidator.Validate(envelope);
        return envelope;
    }
}
