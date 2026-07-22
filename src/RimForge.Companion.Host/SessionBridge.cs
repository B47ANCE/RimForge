using RimForge.Protocol.Contracts;
using RimForge.Protocol.Serialization;

namespace RimForge.Companion.Host;

public sealed class SessionBridge : IDisposable
{
    private readonly object _gate = new();
    private readonly StreamWriter _writer;

    public SessionBridge(string stateRoot, string forgeSessionId)
    {
        var sessionRoot = Path.Combine(Path.GetFullPath(stateRoot), "Sessions", forgeSessionId);
        Directory.CreateDirectory(sessionRoot);
        EvidencePath = Path.Combine(sessionRoot, "runtime-envelopes.jsonl");
        _writer = new StreamWriter(new FileStream(EvidencePath, FileMode.Append, FileAccess.Write, FileShare.Read))
        {
            AutoFlush = true
        };
    }

    public string EvidencePath { get; }
    public event EventHandler<RimForgeEnvelope>? Forwarded;

    public void Accept(RimForgeEnvelope envelope)
    {
        var serialized = ProtocolSerializer.Serialize(envelope);
        lock (_gate) _writer.WriteLine(serialized);
        Forwarded?.Invoke(this, envelope);
    }

    public void Dispose()
    {
        lock (_gate) _writer.Dispose();
    }
}
