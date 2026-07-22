using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Xml.Linq;
using RimForge.Core.Models;
using RimForge.Core.Services;

namespace RimForge.Infrastructure.Services;

public sealed class ProfilePackageInspectionService : IProfilePackageInspectionService
{
    private const long MaximumManifestBytes = 1024 * 1024;
    private const long MaximumConfigBytes = 4 * 1024 * 1024;
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };
    private static readonly StringComparer PackageComparer = StringComparer.OrdinalIgnoreCase;

    public async Task<ProfilePackageInspection> InspectAsync(
        string packagePath,
        IReadOnlyList<ModRecord> installedMods,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(packagePath);
        ArgumentNullException.ThrowIfNull(installedMods);
        var issues = new List<string>();
        if (!File.Exists(packagePath))
            return Invalid(packagePath, "Profile package was not found.");

        try
        {
            using var archive = ZipFile.OpenRead(packagePath);
            var manifests = archive.Entries.Where(entry => entry.FullName.Equals("Profile.json", StringComparison.OrdinalIgnoreCase)).ToArray();
            if (manifests.Length != 1) return Invalid(packagePath, "Profile package must contain exactly one Profile.json manifest.");
            if (manifests[0].Length > MaximumManifestBytes) return Invalid(packagePath, "Profile manifest exceeds the safety limit.");

            ProfileBackupManifest manifest;
            await using (var stream = manifests[0].Open())
                manifest = await JsonSerializer.DeserializeAsync<ProfileBackupManifest>(stream, JsonOptions, cancellationToken)
                    ?? throw new InvalidDataException("Profile manifest is invalid.");
            if (string.IsNullOrWhiteSpace(manifest.ProfileName)) issues.Add("Profile name is missing.");
            if (string.IsNullOrWhiteSpace(manifest.Version)) issues.Add("Target RimWorld version is missing.");
            if (manifest.ActiveMods.Count == 0) issues.Add("Profile contains no active mods.");
            if (!IsSafeEntryName(manifest.ModsConfigFile)) issues.Add("Manifest contains an unsafe ModsConfig file name.");
            if (issues.Count > 0) return new(false, packagePath, manifest, [], [], issues);

            var configs = archive.Entries.Where(entry => entry.FullName.Equals(manifest.ModsConfigFile, StringComparison.OrdinalIgnoreCase)).ToArray();
            if (configs.Length != 1) return Invalid(packagePath, "Profile package must contain exactly one declared ModsConfig file.", manifest);
            if (configs[0].Length > MaximumConfigBytes) return Invalid(packagePath, "ModsConfig exceeds the safety limit.", manifest);
            byte[] configBytes;
            await using (var stream = configs[0].Open())
            await using (var buffer = new MemoryStream())
            {
                await stream.CopyToAsync(buffer, cancellationToken);
                configBytes = buffer.ToArray();
            }
            var actualHash = Convert.ToHexString(SHA256.HashData(configBytes));
            if (string.IsNullOrWhiteSpace(manifest.ModsConfigSha256) ||
                !actualHash.Equals(manifest.ModsConfigSha256, StringComparison.OrdinalIgnoreCase))
                return Invalid(packagePath, "ModsConfig checksum is missing or invalid.", manifest);

            var document = XDocument.Parse(Encoding.UTF8.GetString(configBytes));
            var configMods = Normalize(document.Root?.Element("activeMods")?.Elements("li").Select(item => item.Value) ?? []);
            var manifestMods = Normalize(manifest.ActiveMods);
            if (!configMods.SequenceEqual(manifestMods, PackageComparer))
                return Invalid(packagePath, "Manifest and ModsConfig active-mod orders do not match.", manifest);

            var installed = installedMods.Where(mod => !string.IsNullOrWhiteSpace(mod.PackageId))
                .GroupBy(mod => mod.PackageId!, PackageComparer)
                .ToDictionary(group => group.Key, group => group.First(), PackageComparer);
            var missing = manifestMods.Where(packageId => !installed.ContainsKey(packageId)).ToArray();
            var incompatible = manifestMods
                .Where(installed.ContainsKey)
                .Where(packageId => installed[packageId].SupportedVersions.Count > 0 &&
                    !installed[packageId].SupportedVersions.Contains(manifest.Version, PackageComparer))
                .ToArray();
            return new(true, packagePath, manifest, missing, incompatible, Array.Empty<string>());
        }
        catch (Exception ex) when (ex is InvalidDataException or JsonException or System.Xml.XmlException)
        {
            return Invalid(packagePath, ex.Message);
        }
    }

    private static bool IsSafeEntryName(string value) =>
        !string.IsNullOrWhiteSpace(value) && Path.GetFileName(value).Equals(value, StringComparison.Ordinal) &&
        !value.Contains("..", StringComparison.Ordinal);

    private static string[] Normalize(IEnumerable<string> values) => values.Select(value => value.Trim().ToLowerInvariant())
        .Where(value => value.Length > 0).Distinct(PackageComparer).ToArray();

    private static ProfilePackageInspection Invalid(string path, string issue, ProfileBackupManifest? manifest = null) =>
        new(false, path, manifest, Array.Empty<string>(), Array.Empty<string>(), [issue]);
}
