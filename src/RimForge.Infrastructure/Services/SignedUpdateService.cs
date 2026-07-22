using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using RimForge.Core.Models;
using RimForge.Core.Services;

namespace RimForge.Infrastructure.Services;

public sealed class SignedUpdateService : ISignedUpdateService
{
    private sealed record UpdateTransaction(
        RimForgeUpdateManifest Manifest,
        string StagedPackagePath,
        DateTimeOffset StagedAtUtc,
        IReadOnlyList<string> ProtectedRoots);

    private readonly string _updatesRoot;
    private readonly IStatePreservationService _preservation;
    private readonly IReadOnlyDictionary<string, string> _trustedChannelKeys;

    public SignedUpdateService(
        string updatesRoot,
        IStatePreservationService preservation,
        IReadOnlyDictionary<string, string>? trustedChannelKeys = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(updatesRoot);
        _updatesRoot = Path.GetFullPath(updatesRoot);
        _preservation = preservation;
        _trustedChannelKeys = new Dictionary<string, string>(
            trustedChannelKeys ?? new Dictionary<string, string>(),
            StringComparer.OrdinalIgnoreCase);
    }

    public bool VerifyManifest(string manifestJson, string signatureBase64, string publicKeyPem)
    {
        if (string.IsNullOrWhiteSpace(manifestJson) || string.IsNullOrWhiteSpace(signatureBase64) || string.IsNullOrWhiteSpace(publicKeyPem)) return false;
        try
        {
            using var rsa = RSA.Create();
            rsa.ImportFromPem(publicKeyPem);
            return rsa.VerifyData(Encoding.UTF8.GetBytes(manifestJson), Convert.FromBase64String(signatureBase64),
                HashAlgorithmName.SHA256, RSASignaturePadding.Pss);
        }
        catch (CryptographicException) { return false; }
        catch (FormatException) { return false; }
    }

    public async Task<UpdateStagingResult> StageAsync(
        string manifestJson,
        string signatureBase64,
        string packagePath,
        CancellationToken cancellationToken = default)
    {
        RimForgeUpdateManifest? manifest;
        try { manifest = JsonSerializer.Deserialize<RimForgeUpdateManifest>(manifestJson, JsonOptions); }
        catch (JsonException ex) { return new(false, $"The update manifest is invalid: {ex.Message}"); }
        if (manifest is null || manifest.SchemaVersion != 1 || string.IsNullOrWhiteSpace(manifest.Version))
            return new(false, "The update manifest schema is unsupported.");
        if (!_trustedChannelKeys.TryGetValue(manifest.Channel, out var trustedKey))
            return new(false, $"Update channel '{manifest.Channel}' is not trusted.");
        if (!VerifyManifest(manifestJson, signatureBase64, trustedKey))
            return new(false, "The update manifest signature is invalid.");
        if (!File.Exists(packagePath)) return new(false, "The update package does not exist.");

        await using var package = File.OpenRead(packagePath);
        var actualHash = Convert.ToHexString(await SHA256.HashDataAsync(package, cancellationToken).ConfigureAwait(false));
        if (!actualHash.Equals(manifest.PackageSha256, StringComparison.OrdinalIgnoreCase))
            return new(false, "The update package failed SHA-256 verification.");

        ValidateRelativeFiles(manifest.InstallFiles);
        var stageRoot = Path.Combine(_updatesRoot, "Staging", Sanitize(manifest.Version));
        Directory.CreateDirectory(stageRoot);
        var stagedPackage = Path.Combine(stageRoot, Path.GetFileName(packagePath));
        File.Copy(packagePath, stagedPackage, true);
        var preserved = await _preservation.CaptureAsync(manifest.Version, cancellationToken).ConfigureAwait(false);
        var transactionPath = Path.Combine(stageRoot, "transaction.json");
        await using (var output = File.Create(transactionPath))
            await JsonSerializer.SerializeAsync(output,
                new UpdateTransaction(manifest, stagedPackage, DateTimeOffset.UtcNow, preserved.ProtectedRoots),
                JsonOptions, cancellationToken).ConfigureAwait(false);
        return new(true, "The signed update is staged and ready for an external installer.", stagedPackage, transactionPath);
    }

    public async Task<UpdateRollbackResult> CaptureRollbackAsync(
        RimForgeUpdateManifest manifest,
        string installRoot,
        CancellationToken cancellationToken = default)
    {
        _preservation.ValidateInstallBoundary(installRoot);
        ValidateRelativeFiles(manifest.InstallFiles);
        var sourceRoot = Path.GetFullPath(installRoot);
        var rollbackRoot = Path.Combine(_updatesRoot, "Rollback", Sanitize(manifest.Version), DateTimeOffset.UtcNow.ToString("yyyyMMddHHmmssfff"));
        foreach (var relative in manifest.InstallFiles)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var source = ResolveWithin(sourceRoot, relative);
            if (!File.Exists(source)) continue;
            var destination = ResolveWithin(rollbackRoot, relative);
            Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
            await using var input = File.OpenRead(source);
            await using var output = File.Create(destination);
            await input.CopyToAsync(output, cancellationToken).ConfigureAwait(false);
        }
        return new(true, "Rollback files were captured without modifying application state.", rollbackRoot);
    }

    public async Task<UpdateRollbackResult> RestoreRollbackAsync(
        string rollbackRoot,
        string installRoot,
        CancellationToken cancellationToken = default)
    {
        _preservation.ValidateInstallBoundary(installRoot);
        var backupRoot = Path.GetFullPath(rollbackRoot);
        if (!Directory.Exists(backupRoot)) return new(false, "The rollback directory does not exist.");
        var targetRoot = Path.GetFullPath(installRoot);
        foreach (var source in Directory.EnumerateFiles(backupRoot, "*", SearchOption.AllDirectories))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var relative = Path.GetRelativePath(backupRoot, source);
            var destination = ResolveWithin(targetRoot, relative);
            Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
            var temporary = destination + ".rimforge-rollback.tmp";
            await using (var input = File.OpenRead(source))
            await using (var output = File.Create(temporary))
                await input.CopyToAsync(output, cancellationToken).ConfigureAwait(false);
            File.Move(temporary, destination, true);
        }
        return new(true, "The previous installation files were restored; protected state was not modified.", backupRoot);
    }

    private static void ValidateRelativeFiles(IEnumerable<string> files)
    {
        foreach (var file in files)
            if (string.IsNullOrWhiteSpace(file) || Path.IsPathRooted(file) || file.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar).Contains(".."))
                throw new InvalidDataException($"Unsafe update path: {file}");
    }

    private static string ResolveWithin(string root, string relative)
    {
        var fullRoot = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        var full = Path.GetFullPath(Path.Combine(fullRoot, relative));
        if (!full.StartsWith(fullRoot, StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException($"Path escapes update root: {relative}");
        return full;
    }

    private static string Sanitize(string value) => string.Concat(value.Select(character => Path.GetInvalidFileNameChars().Contains(character) ? '_' : character));
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web) { WriteIndented = true };
}
