using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace RimForge.App.Features.TextureTools;

public sealed record TextureConversionOptions(
    string OutputFormat,
    int MaximumSize,
    bool PreserveAlpha,
    bool SkipUnchanged,
    string OutputRoot,
    string ManifestRoot);

public sealed record TextureConversionResult(bool Success, bool Skipped, string Message, string? OutputPath, long OutputBytes);

public sealed class TextureConversionEngine
{
    private readonly string _temporaryRoot;
    private static readonly HashSet<string> SupportedInputExtensions = new(StringComparer.OrdinalIgnoreCase)
    { ".png", ".jpg", ".jpeg", ".bmp", ".tif", ".tiff", ".dds" };

    public TextureConversionEngine(string temporaryRoot)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(temporaryRoot);
        _temporaryRoot = Path.GetFullPath(temporaryRoot);
    }

    public static bool IsSupportedInput(string path) => SupportedInputExtensions.Contains(Path.GetExtension(path));

    public async Task<TextureConversionResult> ConvertAsync(string sourcePath, TextureConversionOptions options, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        var extension = ResolveExtension(options.OutputFormat);
        var outputPath = Path.Combine(options.OutputRoot, Path.GetFileNameWithoutExtension(sourcePath) + extension);
        Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);

        if (options.SkipUnchanged && File.Exists(outputPath) && File.GetLastWriteTimeUtc(outputPath) >= File.GetLastWriteTimeUtc(sourcePath))
            return new(true, true, "Output is current", outputPath, new FileInfo(outputPath).Length);

        TextureConversionResult result;
        if (extension.Equals(".dds", StringComparison.OrdinalIgnoreCase))
            result = await ConvertWithTexconvAsync(sourcePath, outputPath, options, token).ConfigureAwait(false);
        else
        {
            if (Path.GetExtension(sourcePath).Equals(".dds", StringComparison.OrdinalIgnoreCase))
                return new(false, false, "DDS decoding requires texconv.exe in Tools\\DirectXTex", null, 0);
            ConvertWithWpf(sourcePath, outputPath, extension, options, token);
            result = new(true, false, "Converted", outputPath, new FileInfo(outputPath).Length);
        }

        if (result.Success && !result.Skipped && result.OutputPath is not null)
        {
            try
            {
                await TextureConversionManifestStore.RecordAsync(options.ManifestRoot, sourcePath, result.OutputPath, token).ConfigureAwait(false);
            }
            catch
            {
                try { File.Delete(result.OutputPath); } catch (IOException) { } catch (UnauthorizedAccessException) { }
                throw;
            }
        }
        return result;
    }

    public static int NearestMultipleOfFour(int value)
    {
        if (value <= 4) return 4;
        var lower = value - value % 4;
        var upper = lower + 4;
        return value - lower < upper - value ? lower : upper;
    }

    public static bool ValidateDds(string path, out string message)
    {
        try
        {
            using var stream = File.OpenRead(path);
            Span<byte> header = stackalloc byte[128];
            if (stream.Read(header) < 128) { message = "DDS header is truncated"; return false; }
            if (header[0] != (byte)'D' || header[1] != (byte)'D' || header[2] != (byte)'S' || header[3] != (byte)' ')
            { message = "Invalid DDS signature"; return false; }
            if (BitConverter.ToInt32(header[4..8]) != 124) { message = "Invalid DDS header size"; return false; }
            var height = BitConverter.ToInt32(header[12..16]);
            var width = BitConverter.ToInt32(header[16..20]);
            if (width <= 0 || height <= 0) { message = "Invalid DDS dimensions"; return false; }
            if (width % 4 != 0 || height % 4 != 0)
            {
                message = $"Invalid DDS dimensions ({width} × {height}); nearest valid size is {NearestMultipleOfFour(width)} × {NearestMultipleOfFour(height)}";
                return false;
            }
            message = $"Valid DDS ({width} × {height})";
            return true;
        }
        catch (Exception ex) { message = ex.Message; return false; }
    }

    private static void ConvertWithWpf(string sourcePath, string outputPath, string extension, TextureConversionOptions options, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        using var input = File.OpenRead(sourcePath);
        var decoder = BitmapDecoder.Create(input, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);
        var source = decoder.Frames[0];
        BitmapSource output = ScaleToMaximum(source, options.MaximumSize);

        BitmapEncoder encoder = extension.ToLowerInvariant() switch
        {
            ".jpg" or ".jpeg" => new JpegBitmapEncoder { QualityLevel = 92 },
            ".bmp" => new BmpBitmapEncoder(),
            ".tif" or ".tiff" => new TiffBitmapEncoder { Compression = TiffCompressOption.Zip },
            _ => new PngBitmapEncoder()
        };
        encoder.Frames.Add(BitmapFrame.Create(output));
        using var file = File.Create(outputPath);
        encoder.Save(file);
    }

    private async Task<TextureConversionResult> ConvertWithTexconvAsync(string sourcePath, string outputPath, TextureConversionOptions options, CancellationToken token)
    {
        if (Path.GetExtension(sourcePath).Equals(".dds", StringComparison.OrdinalIgnoreCase))
            return new(false, false, "Existing DDS files are analyzed but are not reconverted from a lossy source.", null, 0);

        var texconv = FindTexconv();
        if (texconv is null)
            return new(false, false, "DDS output requires texconv.exe in Tools\\DirectXTex or PATH", null, 0);

        var outputDir = Path.GetDirectoryName(outputPath)!;
        Directory.CreateDirectory(_temporaryRoot);
        var temporaryPng = Path.Combine(_temporaryRoot, $"rimforge-{Guid.NewGuid():N}.png");
        try
        {
            var geometry = CreateNormalizedPng(sourcePath, temporaryPng, options.MaximumSize, token);
            var explicitBc7 = options.OutputFormat.Contains("BC7", StringComparison.OrdinalIgnoreCase);
            var explicitBc3 = options.OutputFormat.Contains("BC3", StringComparison.OrdinalIgnoreCase) ||
                options.OutputFormat.Contains("DXT5", StringComparison.OrdinalIgnoreCase);
            var explicitBc1 = options.OutputFormat.Contains("BC1", StringComparison.OrdinalIgnoreCase) ||
                options.OutputFormat.Contains("DXT1", StringComparison.OrdinalIgnoreCase);
            var format = explicitBc7
                ? "BC7_UNORM"
                : explicitBc3
                    ? "BC3_UNORM"
                    : explicitBc1
                        ? "BC1_UNORM"
                        : "BC7_UNORM";
            var args = $"-y -f {format} -m 0 -o \"{outputDir}\" \"{temporaryPng}\"";
            using var process = new Process
            {
                StartInfo = new ProcessStartInfo(texconv, args)
                {
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                }
            };
            process.Start();
            var standardOutput = process.StandardOutput.ReadToEndAsync();
            var standardError = process.StandardError.ReadToEndAsync();
            try
            {
                await process.WaitForExitAsync(token).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                try
                {
                    if (!process.HasExited) process.Kill(entireProcessTree: true);
                }
                catch (InvalidOperationException)
                {
                    // The process exited between the state check and the kill request.
                }
                try
                {
                    await process.WaitForExitAsync(CancellationToken.None).ConfigureAwait(false);
                }
                catch (InvalidOperationException)
                {
                    // The process was never fully started; cancellation still propagates.
                }
                throw;
            }
            var outputText = (await standardOutput.ConfigureAwait(false)).Trim();
            var errorText = (await standardError.ConfigureAwait(false)).Trim();
            var generated = Path.Combine(outputDir, Path.GetFileNameWithoutExtension(temporaryPng) + ".DDS");
            if (process.ExitCode != 0 || !File.Exists(generated))
            {
                var diagnostic = errorText.Length > 0 ? errorText : outputText;
                return new(false, false, diagnostic.Length > 0 ? diagnostic : "texconv failed", null, 0);
            }
            File.Move(generated, outputPath, true);
            return new(true, false, $"Converted with DirectXTex {format} ({geometry.Width} × {geometry.Height}; nearest multiple of 4)", outputPath, new FileInfo(outputPath).Length);
        }
        finally
        {
            try { if (File.Exists(temporaryPng)) File.Delete(temporaryPng); } catch { }
        }
    }

    private static (int Width, int Height) CreateNormalizedPng(string sourcePath, string destinationPath, int maximumSize, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        using var input = File.OpenRead(sourcePath);
        var decoder = BitmapDecoder.Create(input, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);
        var source = decoder.Frames[0];
        var scaled = ScaleToMaximum(source, maximumSize);
        var canvasWidth = NearestMultipleOfFour(scaled.PixelWidth);
        var canvasHeight = NearestMultipleOfFour(scaled.PixelHeight);

        BitmapSource normalized = scaled;
        if (scaled.PixelWidth != canvasWidth || scaled.PixelHeight != canvasHeight)
        {
            var scale = Math.Min(canvasWidth / (double)scaled.PixelWidth, canvasHeight / (double)scaled.PixelHeight);
            var scaledWidth = Math.Max(1, Math.Min(canvasWidth, (int)Math.Round(scaled.PixelWidth * scale, MidpointRounding.AwayFromZero)));
            var scaledHeight = Math.Max(1, Math.Min(canvasHeight, (int)Math.Round(scaled.PixelHeight * scale, MidpointRounding.AwayFromZero)));
            var resized = new TransformedBitmap(scaled, new ScaleTransform(scaledWidth / (double)scaled.PixelWidth, scaledHeight / (double)scaled.PixelHeight));
            resized.Freeze();
            var visual = new DrawingVisual();
            using (var context = visual.RenderOpen())
            {
                context.DrawRectangle(Brushes.Transparent, null, new Rect(0, 0, canvasWidth, canvasHeight));
                context.DrawImage(resized, new Rect((canvasWidth - scaledWidth) / 2d, (canvasHeight - scaledHeight) / 2d, scaledWidth, scaledHeight));
            }
            var render = new RenderTargetBitmap(canvasWidth, canvasHeight, 96, 96, PixelFormats.Pbgra32);
            render.Render(visual);
            render.Freeze();
            normalized = render;
        }

        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(normalized));
        using var output = File.Create(destinationPath);
        encoder.Save(output);
        return (canvasWidth, canvasHeight);
    }

    private static BitmapSource ScaleToMaximum(BitmapSource source, int maximumSize)
    {
        var longest = Math.Max(source.PixelWidth, source.PixelHeight);
        if (maximumSize <= 0 || longest <= maximumSize) return source;
        var scale = maximumSize / (double)longest;
        var transformed = new TransformedBitmap(source, new ScaleTransform(scale, scale));
        transformed.Freeze();
        return transformed;
    }

    private static string ResolveExtension(string format) => format switch
    {
        var f when f.Contains("DDS", StringComparison.OrdinalIgnoreCase) => ".dds",
        var f when f.Contains("JPEG", StringComparison.OrdinalIgnoreCase) || f.Contains("JPG", StringComparison.OrdinalIgnoreCase) => ".jpg",
        var f when f.Contains("BMP", StringComparison.OrdinalIgnoreCase) => ".bmp",
        var f when f.Contains("TIFF", StringComparison.OrdinalIgnoreCase) => ".tiff",
        _ => ".png"
    };

    private static string? FindTexconv()
    {
        var candidates = new[]
        {
            Path.Combine(AppContext.BaseDirectory, "Tools", "DirectXTex", "texconv.exe"),
            Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "Tools", "DirectXTex", "texconv.exe"))
        };
        var local = candidates.FirstOrDefault(File.Exists);
        if (local is not null) return local;
        var pathValue = Environment.GetEnvironmentVariable("PATH") ?? string.Empty;
        return pathValue.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries)
            .Select(folder => Path.Combine(folder.Trim(), "texconv.exe"))
            .FirstOrDefault(File.Exists);
    }
}
