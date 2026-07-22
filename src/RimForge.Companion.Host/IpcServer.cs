using System.IO.Pipes;
using System.Text;
using RimForge.Protocol.Contracts;
using RimForge.Protocol.Serialization;

namespace RimForge.Companion.Host;

public sealed class IpcServer(string pipeName)
{
    private long _received;
    private long _rejected;

    public bool IsListening { get; private set; }
    public bool IsConnected { get; private set; }
    public long EnvelopesReceived => Interlocked.Read(ref _received);
    public long RejectedEnvelopes => Interlocked.Read(ref _rejected);
    public event EventHandler<RimForgeEnvelope>? EnvelopeReceived;
    public event EventHandler<bool>? ConnectionChanged;

    public async Task RunAsync(CancellationToken cancellationToken)
    {
        IsListening = true;
        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                await using var server = new NamedPipeServerStream(
                    pipeName,
                    PipeDirection.In,
                    1,
                    PipeTransmissionMode.Byte,
                    PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
                try
                {
                    await server.WaitForConnectionAsync(cancellationToken).ConfigureAwait(false);
                    IsConnected = true;
                    ConnectionChanged?.Invoke(this, true);
                    using var reader = new StreamReader(server, new UTF8Encoding(false), false, 4096, true);
                    while (server.IsConnected && !cancellationToken.IsCancellationRequested)
                    {
                        var line = await reader.ReadLineAsync(cancellationToken).ConfigureAwait(false);
                        if (line is null) break;
                        try
                        {
                            var envelope = ProtocolSerializer.Deserialize(line);
                            Interlocked.Increment(ref _received);
                            EnvelopeReceived?.Invoke(this, envelope);
                        }
                        catch
                        {
                            Interlocked.Increment(ref _rejected);
                        }
                    }
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    break;
                }
                finally
                {
                    if (IsConnected)
                    {
                        IsConnected = false;
                        ConnectionChanged?.Invoke(this, false);
                    }
                }
            }
        }
        finally
        {
            IsListening = false;
        }
    }
}
