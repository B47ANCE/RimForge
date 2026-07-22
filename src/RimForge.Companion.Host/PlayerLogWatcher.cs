namespace RimForge.Companion.Host;

public sealed class PlayerLogWatcher(string path)
{
    public string Path { get; } = System.IO.Path.GetFullPath(path);
    public event EventHandler<string>? LineReceived;

    public async Task RunAsync(CancellationToken cancellationToken)
    {
        long position = 0;
        while (!cancellationToken.IsCancellationRequested)
        {
            if (!File.Exists(Path))
            {
                await Task.Delay(500, cancellationToken).ConfigureAwait(false);
                continue;
            }

            await using var stream = new FileStream(Path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            if (stream.Length < position) position = 0;
            stream.Position = position;
            using var reader = new StreamReader(stream, leaveOpen: true);
            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var line = await reader.ReadLineAsync(cancellationToken).ConfigureAwait(false);
                if (line is null) break;
                LineReceived?.Invoke(this, line);
            }
            position = stream.Position;
            await Task.Delay(250, cancellationToken).ConfigureAwait(false);
        }
    }
}
