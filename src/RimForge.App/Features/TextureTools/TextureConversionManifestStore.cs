using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace RimForge.App.Features.TextureTools;

public sealed record TextureConversionManifestEntry(string SourcePath, string OutputPath, DateTimeOffset CreatedUtc, long OutputLength);
public sealed record TextureRevertResult(int Deleted, int Skipped);
public sealed record TextureRevertProgress(int Processed, int Total, string OutputPath, string Action);

public static class TextureConversionManifestStore
{
    private const string ManifestName = ".rimforge-texture-conversions.json";
    private static readonly SemaphoreSlim Gate = new(1, 1);

    public static async Task RecordAsync(string outputRoot, string sourcePath, string outputPath, CancellationToken token)
    {
        await Gate.WaitAsync(token).ConfigureAwait(false);
        try
        {
            var entries = await ReadAsync(outputRoot, token).ConfigureAwait(false);
            entries.RemoveAll(entry => entry.OutputPath.Equals(outputPath, StringComparison.OrdinalIgnoreCase));
            entries.Add(new TextureConversionManifestEntry(sourcePath, outputPath, DateTimeOffset.UtcNow, new FileInfo(outputPath).Length));
            await WriteAsync(outputRoot, entries, token).ConfigureAwait(false);
        }
        finally { Gate.Release(); }
    }

    public static async Task<TextureRevertResult> RevertAsync(string outputRoot, CancellationToken token)
        => await RevertAsync(outputRoot, progress: null, token).ConfigureAwait(false);

    public static async Task<TextureRevertResult> RevertAsync(
        string outputRoot,
        IProgress<TextureRevertProgress>? progress,
        CancellationToken token)
    {
        await Gate.WaitAsync(token).ConfigureAwait(false);
        try
        {
            var entries = await ReadAsync(outputRoot, token).ConfigureAwait(false);
            var deleted = 0;
            var skipped = 0;
            for (var index = 0; index < entries.Count; index++)
            {
                token.ThrowIfCancellationRequested();
                var entry = entries[index];
                var action = "Skipped";
                try
                {
                    if (!File.Exists(entry.OutputPath))
                    {
                        skipped++;
                        action = "Output was already absent";
                    }
                    else
                    {
                        var info = new FileInfo(entry.OutputPath);
                        if (info.Length != entry.OutputLength)
                        {
                            skipped++;
                            action = "Skipped user-modified output";
                        }
                        else
                        {
                            File.Delete(entry.OutputPath);
                            deleted++;
                            action = "Deleted verified RimForge output";
                        }
                    }
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                {
                    skipped++;
                    action = $"Skipped: {ex.Message}";
                }
                progress?.Report(new TextureRevertProgress(index + 1, entries.Count, entry.OutputPath, action));
            }
            await WriteAsync(outputRoot, new List<TextureConversionManifestEntry>(), token).ConfigureAwait(false);
            return new TextureRevertResult(deleted, skipped);
        }
        finally { Gate.Release(); }
    }

    private static async Task<List<TextureConversionManifestEntry>> ReadAsync(string outputRoot, CancellationToken token)
    {
        var path = Path.Combine(outputRoot, ManifestName);
        if (!File.Exists(path)) return new List<TextureConversionManifestEntry>();
        try
        {
            await using var stream = File.OpenRead(path);
            return await JsonSerializer.DeserializeAsync<List<TextureConversionManifestEntry>>(stream, cancellationToken: token).ConfigureAwait(false)
                ?? new List<TextureConversionManifestEntry>();
        }
        catch (JsonException)
        {
            var quarantine = path + $".corrupt-{DateTimeOffset.UtcNow:yyyyMMddHHmmssfff}";
            try { File.Move(path, quarantine, false); } catch (IOException) { } catch (UnauthorizedAccessException) { }
            return new List<TextureConversionManifestEntry>();
        }
    }

    private static async Task WriteAsync(string outputRoot, List<TextureConversionManifestEntry> entries, CancellationToken token)
    {
        Directory.CreateDirectory(outputRoot);
        var path = Path.Combine(outputRoot, ManifestName);
        var temporaryPath = path + $".tmp-{Guid.NewGuid():N}";
        try
        {
            await using (var stream = new FileStream(
                temporaryPath,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None,
                4096,
                FileOptions.Asynchronous | FileOptions.WriteThrough))
            {
                await JsonSerializer.SerializeAsync(
                    stream,
                    entries,
                    new JsonSerializerOptions { WriteIndented = true },
                    token).ConfigureAwait(false);
                await stream.FlushAsync(token).ConfigureAwait(false);
            }
            token.ThrowIfCancellationRequested();
            File.Move(temporaryPath, path, true);
        }
        finally
        {
            try { if (File.Exists(temporaryPath)) File.Delete(temporaryPath); } catch (IOException) { } catch (UnauthorizedAccessException) { }
        }
    }
}
