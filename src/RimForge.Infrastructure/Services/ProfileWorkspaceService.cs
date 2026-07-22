using System.Diagnostics;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;
using System.Xml.Linq;
using RimForge.Core.Models;
using RimForge.Core.Services;

namespace RimForge.Infrastructure.Services;

public sealed class ProfileWorkspaceService : IProfileWorkspaceService
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };
    private readonly IPlatformDiscoveryService _platformDiscoveryService;

    public ProfileWorkspaceService(IPlatformDiscoveryService platformDiscoveryService) =>
        _platformDiscoveryService = platformDiscoveryService ?? throw new ArgumentNullException(nameof(platformDiscoveryService));

    public async Task<IReadOnlyList<RimForgeProfile>> LoadProfilesAsync(
        string repositoryRoot,
        IReadOnlyList<ModRecord> installedMods,
        CancellationToken cancellationToken = default)
    {
        var outputRoot = RimForgePathLayout.Create(repositoryRoot).ProfilesRoot;
        Directory.CreateDirectory(outputRoot);
        CleanupStaleTransactions(outputRoot);

        var profiles = new List<RimForgeProfile>();
        var vanilla = await EnsureVanillaProfileAsync(outputRoot, installedMods, cancellationToken);
        profiles.Add(vanilla);

        MigrateLegacyProfileSources(repositoryRoot, outputRoot);
        await EnsureEditableStarterProfileAsync(outputRoot, installedMods, cancellationToken);

        foreach (var sourcePath in Directory.EnumerateFiles(outputRoot, "*.xml").OrderBy(Path.GetFileName))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var name = Path.GetFileNameWithoutExtension(sourcePath);
            if (name.Equals("Vanilla", StringComparison.OrdinalIgnoreCase)) continue;
            var parsed = await ParseSourceProfileAsync(sourcePath, cancellationToken);
            profiles.Add(await WriteWorkspaceAsync(outputRoot, name, parsed.ActiveMods, parsed.KnownExpansions, parsed.Version, false, false, cancellationToken));
        }

        return profiles;
    }

    private static void MigrateLegacyProfileSources(string repositoryRoot, string outputRoot)
    {
        var legacyRoot = Path.Combine(repositoryRoot, "Profiles");
        if (!Directory.Exists(legacyRoot)) return;

        foreach (var legacyPath in Directory.EnumerateFiles(legacyRoot, "*.xml"))
        {
            var destination = Path.Combine(outputRoot, Path.GetFileName(legacyPath));
            if (!File.Exists(destination))
            {
                File.Move(legacyPath, destination);
                continue;
            }

            // Prefer the consolidated copy. Preserve a differing legacy file rather than deleting user data.
            if (!File.ReadAllBytes(legacyPath).SequenceEqual(File.ReadAllBytes(destination)))
            {
                var conflicts = Path.Combine(outputRoot, "LegacyConflicts");
                Directory.CreateDirectory(conflicts);
                var conflictName = $"{Path.GetFileNameWithoutExtension(legacyPath)}.legacy-{DateTimeOffset.Now:yyyyMMdd-HHmmss}.xml";
                File.Move(legacyPath, Path.Combine(conflicts, conflictName));
            }
            else
            {
                File.Delete(legacyPath);
            }
        }

        if (!Directory.EnumerateFileSystemEntries(legacyRoot).Any())
        {
            Directory.Delete(legacyRoot);
        }
    }

    public async Task<ProfileActivationResult> ActivateAsync(RimForgeProfile profile, CancellationToken cancellationToken = default)
    {
        if (Process.GetProcessesByName("RimWorldWin64").Length > 0 || Process.GetProcessesByName("RimWorld").Length > 0)
            return new(false, "Close RimWorld before activating another profile.");

        if (!File.Exists(profile.ModsConfigPath))
            return new(false, $"Profile ModsConfig.xml was not found: {profile.ModsConfigPath}");

        var targetPath = GetRimWorldModsConfigPath();
        var targetDirectory = Path.GetDirectoryName(targetPath)!;
        Directory.CreateDirectory(targetDirectory);

        var recoveryPath = Path.Combine(targetDirectory, "ModsConfig.RimForgeRecovery.xml");
        var stagedPath = Path.Combine(targetDirectory, "ModsConfig.RimForgeStaged.xml");

        try
        {
            if (File.Exists(targetPath)) File.Copy(targetPath, recoveryPath, true);
            File.Copy(profile.ModsConfigPath, stagedPath, true);
            _ = XDocument.Load(stagedPath);
            File.Move(stagedPath, targetPath, true);

            var activation = new
            {
                profile.Name,
                ActivatedUtc = DateTimeOffset.UtcNow,
                Source = profile.ModsConfigPath,
                Target = targetPath
            };
            await File.WriteAllTextAsync(
                Path.Combine(profile.WorkspacePath, "Activation.json"),
                JsonSerializer.Serialize(activation, JsonOptions),
                cancellationToken);

            return new(true, $"Activated profile '{profile.Name}'.", targetPath, File.Exists(recoveryPath) ? recoveryPath : null);
        }
        catch (Exception ex)
        {
            try
            {
                if (File.Exists(recoveryPath)) File.Copy(recoveryPath, targetPath, true);
                if (File.Exists(stagedPath)) File.Delete(stagedPath);
            }
            catch { }
            return new(false, $"Profile activation failed: {ex.Message}", targetPath, recoveryPath);
        }
    }

    public async Task<ProfileActivationResult> LaunchAsync(RimForgeProfile profile, CancellationToken cancellationToken = default)
    {
        var activation = await ActivateAsync(profile, cancellationToken);
        if (!activation.Success) return activation;

        try
        {
            Process.Start(new ProcessStartInfo("steam://run/294100") { UseShellExecute = true });
            return activation with { Message = $"Activated '{profile.Name}' and launched RimWorld through Steam." };
        }
        catch (Exception ex)
        {
            return new(false, $"The profile was activated, but Steam could not be launched: {ex.Message}", activation.ActiveModsConfigPath, activation.RecoveryPath);
        }
    }

    public async Task<ProfileActivationResult> RestoreActivationRecoveryAsync(
        string recoveryPath,
        CancellationToken cancellationToken = default)
    {
        if (Process.GetProcessesByName("RimWorldWin64").Length > 0 || Process.GetProcessesByName("RimWorld").Length > 0)
            return new(false, "Close RimWorld before restoring the previous active mod configuration.");

        if (string.IsNullOrWhiteSpace(recoveryPath) || !File.Exists(recoveryPath))
            return new(false, "The activation recovery file could not be found.", RecoveryPath: recoveryPath);

        var targetPath = GetRimWorldModsConfigPath();
        var stagedPath = targetPath + ".RimForgeRestore.tmp";
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            Directory.CreateDirectory(Path.GetDirectoryName(targetPath)!);
            File.Copy(recoveryPath, stagedPath, true);
            _ = XDocument.Load(stagedPath);
            File.Move(stagedPath, targetPath, true);
            await Task.CompletedTask;
            return new(true, "Restored the previous active RimWorld mod configuration.", targetPath, recoveryPath);
        }
        catch (Exception ex)
        {
            TryDelete(stagedPath);
            return new(false, $"Activation recovery failed: {ex.Message}", targetPath, recoveryPath);
        }
    }

    public async Task<LoadOrderSaveResult> SaveLoadOrderAsync(
        RimForgeProfile profile,
        IReadOnlyList<string> activeMods,
        CancellationToken cancellationToken = default)
    {
        if (activeMods.Count == 0)
            return new(false, "A profile load order cannot be empty.");

        var core = activeMods.FirstOrDefault(id => id.Equals("ludeon.rimworld", StringComparison.OrdinalIgnoreCase));
        if (core is null)
            return new(false, "Core must remain enabled in every profile.");

        if (profile.IsBuiltIn)
        {
            var invalid = activeMods.Where(id => !id.StartsWith("ludeon.rimworld", StringComparison.OrdinalIgnoreCase)).ToArray();
            if (invalid.Length > 0)
                return new(false, "The Vanilla profile may only contain Core and official DLC.");
        }

        var normalized = LoadOrderRules.Normalize(activeMods).ToList();
        var known = normalized.Where(IsOfficialExpansion).ToArray();
        var outputRoot = Directory.GetParent(profile.WorkspacePath)?.FullName;
        if (string.IsNullOrWhiteSpace(outputRoot))
            return new(false, $"The profile workspace path is invalid: {profile.WorkspacePath}");

        // The root-level source profile is authoritative during startup. The workspace copy is
        // a generated representation used for activation and inspection. Save both atomically
        // so a successful save survives application restart.
        var sourcePath = GetSourceProfilePath(outputRoot, profile.Name);
        var sourceBackup = sourcePath + $".{DateTimeOffset.Now:yyyyMMdd-HHmmss}.bak";
        var workspaceBackup = profile.ModsConfigPath + $".{DateTimeOffset.Now:yyyyMMdd-HHmmss}.bak";

        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (File.Exists(sourcePath)) File.Copy(sourcePath, sourceBackup, true);
            if (File.Exists(profile.ModsConfigPath)) File.Copy(profile.ModsConfigPath, workspaceBackup, true);

            await WriteSourceProfileAtomicAsync(
                sourcePath,
                normalized,
                known,
                profile.Version,
                cancellationToken);

            var updated = await WriteWorkspaceAsync(
                outputRoot,
                profile.Name,
                normalized,
                known,
                profile.Version,
                profile.IsBuiltIn,
                profile.IsLocked,
                cancellationToken);

            return new(
                true,
                $"Saved {normalized.Count} active mods to '{profile.Name}'.",
                updated,
                File.Exists(sourceBackup) ? sourceBackup : workspaceBackup);
        }
        catch (Exception ex)
        {
            try
            {
                if (File.Exists(sourceBackup)) File.Copy(sourceBackup, sourcePath, true);
                if (File.Exists(workspaceBackup)) File.Copy(workspaceBackup, profile.ModsConfigPath, true);
            }
            catch { }

            return new(false, $"Load order save failed: {ex.Message}", BackupPath: File.Exists(sourceBackup) ? sourceBackup : workspaceBackup);
        }
    }

    public async Task<ProfileOperationResult> CreateAsync(
        string repositoryRoot,
        string name,
        IReadOnlyList<string>? activeMods = null,
        string version = "1.6",
        CancellationToken cancellationToken = default)
    {
        var outputRoot = RimForgePathLayout.Create(repositoryRoot).ProfilesRoot;
        Directory.CreateDirectory(outputRoot);
        string safeName;
        try
        {
            safeName = ValidateProfileName(name);
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException)
        {
            return new(false, ProfileOperationKind.Create, ex.Message);
        }

        var sourcePath = GetSourceProfilePath(outputRoot, safeName);
        if (File.Exists(sourcePath) || Directory.Exists(Path.Combine(outputRoot, safeName)))
            return new(false, ProfileOperationKind.Create, $"A profile named '{safeName}' already exists.");

        var normalized = LoadOrderRules.Normalize(activeMods is { Count: > 0 }
            ? activeMods
            : new[] { "ludeon.rimworld" });
        if (!normalized.Contains("ludeon.rimworld", StringComparer.OrdinalIgnoreCase))
            return new(false, ProfileOperationKind.Create, "Core must remain enabled in every profile.");

        try
        {
            await WriteSourceProfileAtomicAsync(sourcePath, normalized, normalized.Where(IsOfficialExpansion).ToArray(), version, cancellationToken);
            var profile = await WriteWorkspaceAsync(outputRoot, safeName, normalized, normalized.Where(IsOfficialExpansion).ToArray(), version, false, false, cancellationToken);
            return new(true, ProfileOperationKind.Create, $"Created profile '{safeName}'.", profile);
        }
        catch (Exception ex)
        {
            TryDelete(sourcePath);
            TryDeleteDirectory(Path.Combine(outputRoot, safeName));
            return new(false, ProfileOperationKind.Create, $"Profile creation failed: {ex.Message}");
        }
    }

    public async Task<ProfileOperationResult> DuplicateAsync(
        string repositoryRoot,
        RimForgeProfile source,
        string newName,
        CancellationToken cancellationToken = default)
    {
        var result = await CreateAsync(repositoryRoot, newName, source.ActiveMods, source.Version, cancellationToken);
        return result with
        {
            Operation = ProfileOperationKind.Duplicate,
            Message = result.Success
                ? $"Duplicated '{source.Name}' as '{result.Profile?.Name}'."
                : result.Message
        };
    }

    public async Task<ProfileOperationResult> RenameAsync(
        string repositoryRoot,
        RimForgeProfile profile,
        string newName,
        CancellationToken cancellationToken = default)
    {
        if (profile.IsBuiltIn || profile.IsLocked)
            return new(false, ProfileOperationKind.Rename, $"Profile '{profile.Name}' is locked and cannot be renamed.");

        var outputRoot = RimForgePathLayout.Create(repositoryRoot).ProfilesRoot;
        string safeName;
        try
        {
            safeName = ValidateProfileName(newName);
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException)
        {
            return new(false, ProfileOperationKind.Rename, ex.Message);
        }
        if (safeName.Equals(profile.Name, StringComparison.OrdinalIgnoreCase))
            return new(true, ProfileOperationKind.Rename, "The profile name is unchanged.", profile);

        var newSource = GetSourceProfilePath(outputRoot, safeName);
        var newWorkspace = Path.Combine(outputRoot, safeName);
        if (File.Exists(newSource) || Directory.Exists(newWorkspace))
            return new(false, ProfileOperationKind.Rename, $"A profile named '{safeName}' already exists.");

        var backup = await CreateBackupAsync(outputRoot, profile, cancellationToken);
        var originalSource = GetSourceProfilePath(outputRoot, profile.Name);
        var transactionRoot = CreateTransactionRoot(outputRoot, "rename");
        var stagedSource = Path.Combine(transactionRoot, Path.GetFileName(originalSource));
        var stagedWorkspace = Path.Combine(transactionRoot, "Workspace");
        try
        {
            await WriteSourceProfileAtomicAsync(newSource, profile.ActiveMods, profile.KnownExpansions, profile.Version, cancellationToken);
            var renamed = await WriteWorkspaceAsync(outputRoot, safeName, profile.ActiveMods, profile.KnownExpansions, profile.Version, false, false, cancellationToken);

            MoveFileVerified(originalSource, stagedSource);
            MoveDirectoryVerified(profile.WorkspacePath, stagedWorkspace);
            DeleteDirectoryVerified(transactionRoot);
            return new(true, ProfileOperationKind.Rename, $"Renamed '{profile.Name}' to '{safeName}'.", renamed, backup, CanRestore: true);
        }
        catch (Exception ex)
        {
            TryDelete(newSource);
            TryDeleteDirectory(newWorkspace);
            TryRestoreMovedFile(stagedSource, originalSource);
            TryRestoreMovedDirectory(stagedWorkspace, profile.WorkspacePath);
            TryDeleteDirectory(transactionRoot);
            return new(false, ProfileOperationKind.Rename, $"Profile rename failed and the original profile was preserved: {ex.Message}", BackupPath: backup, CanRestore: true);
        }
    }

    public async Task<ProfileOperationResult> DeleteAsync(
        string repositoryRoot,
        RimForgeProfile profile,
        CancellationToken cancellationToken = default)
    {
        if (profile.IsBuiltIn || profile.IsLocked)
            return new(false, ProfileOperationKind.Delete, $"Profile '{profile.Name}' is locked and cannot be deleted.");

        var outputRoot = RimForgePathLayout.Create(repositoryRoot).ProfilesRoot;
        var backup = await CreateBackupAsync(outputRoot, profile, cancellationToken);
        var originalSource = GetSourceProfilePath(outputRoot, profile.Name);
        var transactionRoot = CreateTransactionRoot(outputRoot, "delete");
        var stagedSource = Path.Combine(transactionRoot, Path.GetFileName(originalSource));
        var stagedWorkspace = Path.Combine(transactionRoot, "Workspace");
        try
        {
            MoveFileVerified(originalSource, stagedSource);
            MoveDirectoryVerified(profile.WorkspacePath, stagedWorkspace);
            DeleteDirectoryVerified(transactionRoot);
            return new(true, ProfileOperationKind.Delete, $"Deleted profile '{profile.Name}'.", BackupPath: backup, CanRestore: true);
        }
        catch (Exception ex)
        {
            TryRestoreMovedFile(stagedSource, originalSource);
            TryRestoreMovedDirectory(stagedWorkspace, profile.WorkspacePath);
            TryDeleteDirectory(transactionRoot);
            return new(false, ProfileOperationKind.Delete, $"Profile deletion failed and the original profile was preserved: {ex.Message}", BackupPath: backup, CanRestore: true);
        }
    }

    public async Task<ProfileOperationResult> ImportAsync(
        string repositoryRoot,
        string sourcePath,
        string profileName,
        CancellationToken cancellationToken = default)
    {
        if (!File.Exists(sourcePath))
            return new(false, ProfileOperationKind.Import, $"Import source was not found: {sourcePath}");

        if (Path.GetExtension(sourcePath).Equals(".zip", StringComparison.OrdinalIgnoreCase))
        {
            var restored = await RestoreAsync(repositoryRoot, sourcePath, profileName, cancellationToken);
            return restored with
            {
                Operation = ProfileOperationKind.Import,
                Message = restored.Success ? $"Imported profile '{restored.Profile?.Name}' from a portable backup." : restored.Message
            };
        }

        try
        {
            var parsed = await ParseSourceProfileAsync(sourcePath, cancellationToken);
            var result = await CreateAsync(repositoryRoot, profileName, parsed.ActiveMods, parsed.Version, cancellationToken);
            return result with
            {
                Operation = ProfileOperationKind.Import,
                Message = result.Success ? $"Imported profile '{result.Profile?.Name}'." : result.Message
            };
        }
        catch (Exception ex)
        {
            return new(false, ProfileOperationKind.Import, $"Profile import failed: {ex.Message}");
        }
    }

    public async Task<ProfileOperationResult> ExportAsync(
        RimForgeProfile profile,
        string destinationPath,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var extension = Path.GetExtension(destinationPath);
            if (extension.Equals(".zip", StringComparison.OrdinalIgnoreCase))
            {
                var temp = destinationPath + ".tmp";
                TryDelete(temp);
                await using (var stream = new FileStream(temp, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None, 81920, true))
                using (var archive = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: false))
                {
                    var configBytes = await File.ReadAllBytesAsync(profile.ModsConfigPath, cancellationToken);
                    _ = XDocument.Parse(System.Text.Encoding.UTF8.GetString(configBytes));
                    var configHash = Convert.ToHexString(SHA256.HashData(configBytes));
                    var configEntry = archive.CreateEntry("ModsConfig.xml", CompressionLevel.Optimal);
                    await using (var entryStream = configEntry.Open())
                        await entryStream.WriteAsync(configBytes, cancellationToken);

                    var manifestEntry = archive.CreateEntry("Profile.json", CompressionLevel.Optimal);
                    await using var manifestStream = manifestEntry.Open();
                    var manifest = new ProfileBackupManifest(profile.Name, profile.Version, DateTimeOffset.UtcNow, profile.ActiveMods, profile.KnownExpansions, ModsConfigSha256: configHash);
                    await JsonSerializer.SerializeAsync(manifestStream, manifest, JsonOptions, cancellationToken);
                }
                File.Move(temp, destinationPath, true);
            }
            else
            {
                var temp = destinationPath + ".tmp";
                File.Copy(profile.ModsConfigPath, temp, true);
                _ = XDocument.Load(temp);
                File.Move(temp, destinationPath, true);
            }

            return new(true, ProfileOperationKind.Export, $"Exported profile '{profile.Name}'.", profile, ExportPath: destinationPath);
        }
        catch (Exception ex)
        {
            return new(false, ProfileOperationKind.Export, $"Profile export failed: {ex.Message}");
        }
    }

    public async Task<ProfileOperationResult> RestoreAsync(
        string repositoryRoot,
        string backupPath,
        string? profileName = null,
        CancellationToken cancellationToken = default)
    {
        if (!File.Exists(backupPath))
            return new(false, ProfileOperationKind.Restore, $"Profile backup was not found: {backupPath}");

        try
        {
            using var archive = ZipFile.OpenRead(backupPath);
            var manifestEntry = archive.GetEntry("Profile.json") ?? throw new InvalidDataException("Backup manifest is missing.");
            ProfileBackupManifest manifest;
            await using (var stream = manifestEntry.Open())
                manifest = await JsonSerializer.DeserializeAsync<ProfileBackupManifest>(stream, JsonOptions, cancellationToken)
                    ?? throw new InvalidDataException("Backup manifest is invalid.");

            var configEntry = archive.GetEntry(manifest.ModsConfigFile) ?? throw new InvalidDataException("Backup ModsConfig.xml is missing.");
            byte[] configBytes;
            await using (var stream = configEntry.Open())
            await using (var buffer = new MemoryStream())
            {
                await stream.CopyToAsync(buffer, cancellationToken);
                configBytes = buffer.ToArray();
            }
            if (!string.IsNullOrWhiteSpace(manifest.ModsConfigSha256))
            {
                var actualHash = Convert.ToHexString(SHA256.HashData(configBytes));
                if (!actualHash.Equals(manifest.ModsConfigSha256, StringComparison.OrdinalIgnoreCase))
                    throw new InvalidDataException("Backup ModsConfig.xml failed its integrity check.");
            }
            var raw = System.Text.Encoding.UTF8.GetString(configBytes);
            var document = XDocument.Parse(raw);
            var restoredVersion = document.Root?.Element("version")?.Value.Trim() ?? manifest.Version;
            var restoredMods = document.Root?.Element("activeMods")?.Elements("li")
                .Select(element => element.Value.Trim())
                .Where(value => value.Length > 0)
                .ToArray() ?? Array.Empty<string>();
            if (restoredMods.Length == 0)
                throw new InvalidDataException("Backup ModsConfig.xml contains no active mods.");

            var targetName = string.IsNullOrWhiteSpace(profileName) ? manifest.ProfileName : profileName;
            var result = await CreateAsync(repositoryRoot, targetName!, restoredMods, restoredVersion, cancellationToken);
            return result with
            {
                Operation = ProfileOperationKind.Restore,
                Message = result.Success ? $"Restored profile '{result.Profile?.Name}'." : result.Message
            };
        }
        catch (Exception ex)
        {
            return new(false, ProfileOperationKind.Restore, $"Profile restore failed: {ex.Message}");
        }
    }

    public ProfileComparisonResult Compare(RimForgeProfile left, RimForgeProfile right)
    {
        ArgumentNullException.ThrowIfNull(left);
        ArgumentNullException.ThrowIfNull(right);

        var comparer = StringComparer.OrdinalIgnoreCase;
        var leftOrder = DistinctPackageIds(left.ActiveMods);
        var rightOrder = DistinctPackageIds(right.ActiveMods);
        var leftSet = leftOrder.ToHashSet(comparer);
        var rightSet = rightOrder.ToHashSet(comparer);
        var added = rightOrder.Where(id => !leftSet.Contains(id)).ToArray();
        var removed = leftOrder.Where(id => !rightSet.Contains(id)).ToArray();
        var rightIndexes = rightOrder
            .Select((id, index) => (id, index))
            .ToDictionary(pair => pair.id, pair => pair.index, comparer);
        var orderChanges = leftOrder
            .Select((id, index) => (id, index))
            .Where(pair => rightIndexes.TryGetValue(pair.id, out var rightIndex) && rightIndex != pair.index)
            .Select(pair => new ProfileOrderChange(pair.id, pair.index, rightIndexes[pair.id]))
            .ToArray();
        return new(left.Name, right.Name, added, removed, orderChanges);
    }

    public string GetRimWorldModsConfigPath() => _platformDiscoveryService.Discover().UserPaths.ModsConfigPath;

    private async Task EnsureEditableStarterProfileAsync(
        string outputRoot,
        IReadOnlyList<ModRecord> installedMods,
        CancellationToken cancellationToken)
    {
        var hasEditableProfile = Directory
            .EnumerateFiles(outputRoot, "*.xml", SearchOption.TopDirectoryOnly)
            .Any(path => !Path.GetFileNameWithoutExtension(path)
                .Equals("Vanilla", StringComparison.OrdinalIgnoreCase));
        if (hasEditableProfile) return;

        var starterPath = Path.Combine(outputRoot, "My First Profile.xml");
        var nativeModsConfigPath = GetRimWorldModsConfigPath();

        IReadOnlyList<string> activeMods;
        IReadOnlyList<string> knownExpansions;
        string version;

        if (File.Exists(nativeModsConfigPath))
        {
            try
            {
                var imported = await ParseSourceProfileAsync(nativeModsConfigPath, cancellationToken);
                activeMods = NormalizeStarterLoadOrder(imported.ActiveMods);
                knownExpansions = imported.KnownExpansions
                    .Where(IsOfficialExpansion)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToArray();
                version = imported.Version;
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch
            {
                (activeMods, knownExpansions, version) = CreateVanillaStarterConfiguration(installedMods);
            }
        }
        else
        {
            (activeMods, knownExpansions, version) = CreateVanillaStarterConfiguration(installedMods);
        }

        await WriteSourceProfileAsync(
            starterPath,
            activeMods,
            knownExpansions,
            version,
            cancellationToken);
    }

    private static IReadOnlyList<string> NormalizeStarterLoadOrder(IReadOnlyList<string> activeMods)
    {
        return LoadOrderRules.Normalize(activeMods);
    }

    private static (IReadOnlyList<string> ActiveMods, IReadOnlyList<string> KnownExpansions, string Version)
        CreateVanillaStarterConfiguration(IReadOnlyList<ModRecord> installedMods)
    {
        var official = installedMods
            .Select(mod => mod.PackageId)
            .Where(static id => !string.IsNullOrWhiteSpace(id))
            .Select(static id => id!)
            .Where(id => id.StartsWith("ludeon.rimworld", StringComparison.OrdinalIgnoreCase))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(id => id.Equals("ludeon.rimworld", StringComparison.OrdinalIgnoreCase) ? 0 : 1)
            .ThenBy(id => id, StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (!official.Contains("ludeon.rimworld", StringComparer.OrdinalIgnoreCase))
            official.Insert(0, "ludeon.rimworld");

        return (official, official.Where(IsOfficialExpansion).ToArray(), "1.6");
    }

    private static async Task WriteSourceProfileAsync(
        string path,
        IReadOnlyList<string> activeMods,
        IReadOnlyList<string> knownExpansions,
        string version,
        CancellationToken cancellationToken)
    {
        var document = new XDocument(
            new XElement("ModsConfigData",
                new XElement("version", string.IsNullOrWhiteSpace(version) ? "1.6" : version),
                new XElement("activeMods", activeMods.Select(id => new XElement("li", id))),
                new XElement("knownExpansions", knownExpansions.Select(id => new XElement("li", id)))));
        await File.WriteAllTextAsync(path, document.ToString(), cancellationToken);
        _ = XDocument.Load(path);
    }

    private static async Task<RimForgeProfile> EnsureVanillaProfileAsync(
        string outputRoot,
        IReadOnlyList<ModRecord> installedMods,
        CancellationToken cancellationToken)
    {
        var installedOfficial = installedMods
            .Select(m => m.PackageId)
            .Where(static id => !string.IsNullOrWhiteSpace(id))
            .Select(static id => id!)
            .Where(id => id.StartsWith("ludeon.rimworld", StringComparison.OrdinalIgnoreCase))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        installedOfficial.Add("ludeon.rimworld");

        var sourcePath = GetSourceProfilePath(outputRoot, "Vanilla");
        IReadOnlyList<string> active;
        string version;

        if (File.Exists(sourcePath))
        {
            var parsed = await ParseSourceProfileAsync(sourcePath, cancellationToken);
            active = LoadOrderRules.Normalize(parsed.ActiveMods
                .Where(installedOfficial.Contains)
                .Append("ludeon.rimworld")
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray());
            version = parsed.Version;
        }
        else
        {
            active = LoadOrderRules.Normalize(installedOfficial);
            version = "1.6";
            await WriteSourceProfileAtomicAsync(
                sourcePath,
                active,
                active.Where(IsOfficialExpansion).ToArray(),
                version,
                cancellationToken);
        }

        return await WriteWorkspaceAsync(
            outputRoot,
            "Vanilla",
            active,
            active.Where(IsOfficialExpansion).ToArray(),
            version,
            true,
            true,
            cancellationToken);
    }

    private static async Task<RimForgeProfile> WriteWorkspaceAsync(
        string outputRoot,
        string name,
        IReadOnlyList<string> activeMods,
        IReadOnlyList<string> knownExpansions,
        string version,
        bool builtIn,
        bool locked,
        CancellationToken cancellationToken)
    {
        activeMods = LoadOrderRules.Normalize(activeMods);

        var safeName = string.Concat(name.Select(ch => Path.GetInvalidFileNameChars().Contains(ch) ? '_' : ch));
        var workspace = Path.Combine(outputRoot, safeName);
        Directory.CreateDirectory(workspace);
        var modsConfig = Path.Combine(workspace, "ModsConfig.xml");

        var document = new XDocument(
            new XElement("ModsConfigData",
                new XElement("version", string.IsNullOrWhiteSpace(version) ? "1.6" : version),
                new XElement("activeMods", activeMods.Select(id => new XElement("li", id))),
                new XElement("knownExpansions", knownExpansions.Select(id => new XElement("li", id)))));
        await File.WriteAllTextAsync(modsConfig, document.ToString(), cancellationToken);

        var metadataPath = Path.Combine(workspace, "Profile.json");
        var metadata = new
        {
            Name = name,
            Version = version,
            IsBuiltIn = builtIn,
            IsLocked = locked,
            ModCount = activeMods.Count,
            LastGeneratedUtc = DateTimeOffset.UtcNow,
            ModsConfig = "ModsConfig.xml"
        };
        await File.WriteAllTextAsync(metadataPath, JsonSerializer.Serialize(metadata, JsonOptions), cancellationToken);

        return new(name, workspace, modsConfig, activeMods, knownExpansions, version, builtIn, locked, DateTimeOffset.UtcNow);
    }

    private static async Task<(IReadOnlyList<string> ActiveMods, IReadOnlyList<string> KnownExpansions, string Version)> ParseSourceProfileAsync(
        string path,
        CancellationToken cancellationToken)
    {
        var raw = await File.ReadAllTextAsync(path, cancellationToken);
        if (raw.TrimStart().StartsWith("{", StringComparison.Ordinal))
        {
            using var doc = JsonDocument.Parse(raw);
            var root = doc.RootElement;
            var version = root.TryGetProperty("version", out var versionElement) ? versionElement.GetString() ?? "1.6" : "1.6";
            var active = root.TryGetProperty("activeMods", out var mods)
                ? mods.EnumerateArray().Select(x => x.GetString()).Where(x => !string.IsNullOrWhiteSpace(x)).Select(x => x!).ToList()
                : new List<string>();
            var expansions = root.TryGetProperty("knownExpansions", out var known)
                ? known.EnumerateArray().Select(x => x.GetString()).Where(x => !string.IsNullOrWhiteSpace(x)).Select(x => x!).ToList()
                : new List<string>();
            if (expansions.Count == 0) expansions = active.Where(IsOfficialExpansion).ToList();
            return (active, expansions, version);
        }

        var xml = XDocument.Parse(raw);
        var xmlVersion = xml.Root?.Element("version")?.Value.Trim() ?? "1.6";
        var activeXml = xml.Root?.Element("activeMods")?.Elements("li").Select(x => x.Value.Trim()).Where(x => x.Length > 0).ToList() ?? new List<string>();
        var knownXml = xml.Root?.Element("knownExpansions")?.Elements("li").Select(x => x.Value.Trim()).Where(x => x.Length > 0).ToList() ?? new List<string>();
        if (knownXml.Count == 0) knownXml = activeXml.Where(IsOfficialExpansion).ToList();
        return (activeXml, knownXml, xmlVersion);
    }

    private static string ValidateProfileName(string name)
    {
        var trimmed = name?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(trimmed))
            throw new ArgumentException("Profile name cannot be empty.", nameof(name));
        var safe = string.Concat(trimmed.Select(ch => Path.GetInvalidFileNameChars().Contains(ch) ? '_' : ch));
        if (safe is "." or "..")
            throw new ArgumentException("Profile name is invalid.", nameof(name));
        return safe;
    }

    private static string GetSourceProfilePath(string outputRoot, string name) =>
        Path.Combine(outputRoot, ValidateProfileName(name) + ".xml");

    private static async Task WriteSourceProfileAtomicAsync(
        string path,
        IReadOnlyList<string> activeMods,
        IReadOnlyList<string> knownExpansions,
        string version,
        CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var temporaryPath = path + ".tmp";
        var document = new XDocument(
            new XElement("ModsConfigData",
                new XElement("version", string.IsNullOrWhiteSpace(version) ? "1.6" : version),
                new XElement("activeMods", activeMods.Select(id => new XElement("li", id))),
                new XElement("knownExpansions", knownExpansions.Select(id => new XElement("li", id)))));
        try
        {
            await File.WriteAllTextAsync(temporaryPath, document.ToString(), cancellationToken);
            _ = XDocument.Load(temporaryPath);
            File.Move(temporaryPath, path, true);
        }
        finally
        {
            TryDelete(temporaryPath);
        }
    }

    private static async Task<string> CreateBackupAsync(
        string outputRoot,
        RimForgeProfile profile,
        CancellationToken cancellationToken)
    {
        var backupRoot = Path.Combine(outputRoot, "Backups");
        Directory.CreateDirectory(backupRoot);
        var backupPath = Path.Combine(backupRoot, $"{ValidateProfileName(profile.Name)}-{DateTimeOffset.UtcNow:yyyyMMdd-HHmmssfff}.rfprofile.zip");
        await using var stream = new FileStream(backupPath, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None, 81920, true);
        using var archive = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: false);
        var configBytes = await File.ReadAllBytesAsync(profile.ModsConfigPath, cancellationToken);
        _ = XDocument.Parse(System.Text.Encoding.UTF8.GetString(configBytes));
        var configHash = Convert.ToHexString(SHA256.HashData(configBytes));
        var configEntry = archive.CreateEntry("ModsConfig.xml", CompressionLevel.Optimal);
        await using (var entryStream = configEntry.Open())
            await entryStream.WriteAsync(configBytes, cancellationToken);
        var manifestEntry = archive.CreateEntry("Profile.json", CompressionLevel.Optimal);
        await using var manifestStream = manifestEntry.Open();
        var manifest = new ProfileBackupManifest(profile.Name, profile.Version, DateTimeOffset.UtcNow, profile.ActiveMods, profile.KnownExpansions, ModsConfigSha256: configHash);
        await JsonSerializer.SerializeAsync(manifestStream, manifest, JsonOptions, cancellationToken);
        return backupPath;
    }



    private static IReadOnlyList<string> DistinctPackageIds(IEnumerable<string> packageIds)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        return packageIds
            .Where(static id => !string.IsNullOrWhiteSpace(id))
            .Select(static id => id.Trim())
            .Where(seen.Add)
            .ToArray();
    }

    private static void CleanupStaleTransactions(string outputRoot)
    {
        var transactionsRoot = Path.Combine(outputRoot, ".transactions");
        if (!Directory.Exists(transactionsRoot)) return;

        var cutoffUtc = DateTime.UtcNow.AddDays(-7);
        foreach (var transactionPath in Directory.EnumerateDirectories(transactionsRoot))
        {
            try
            {
                if (Directory.GetLastWriteTimeUtc(transactionPath) < cutoffUtc)
                    Directory.Delete(transactionPath, true);
            }
            catch
            {
                // Stale transaction cleanup is best-effort and must never block profile loading.
            }
        }

        try
        {
            if (!Directory.EnumerateFileSystemEntries(transactionsRoot).Any())
                Directory.Delete(transactionsRoot);
        }
        catch
        {
            // Another profile operation may be using the transaction root.
        }
    }

    private static string CreateTransactionRoot(string outputRoot, string operation)
    {
        var transactionRoot = Path.Combine(outputRoot, ".transactions", $"{operation}-{Guid.NewGuid():N}");
        Directory.CreateDirectory(transactionRoot);
        return transactionRoot;
    }

    private static void MoveFileVerified(string source, string destination)
    {
        if (!File.Exists(source)) return;
        Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
        File.Move(source, destination, false);
        if (File.Exists(source) || !File.Exists(destination))
            throw new IOException($"Profile source could not be staged transactionally: {source}");
    }

    private static void MoveDirectoryVerified(string source, string destination)
    {
        if (!Directory.Exists(source)) return;
        Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
        Directory.Move(source, destination);
        if (Directory.Exists(source) || !Directory.Exists(destination))
            throw new IOException($"Profile workspace could not be staged transactionally: {source}");
    }

    private static void TryRestoreMovedFile(string stagedPath, string originalPath)
    {
        try
        {
            if (!File.Exists(stagedPath) || File.Exists(originalPath)) return;
            Directory.CreateDirectory(Path.GetDirectoryName(originalPath)!);
            File.Move(stagedPath, originalPath, false);
        }
        catch { }
    }

    private static void TryRestoreMovedDirectory(string stagedPath, string originalPath)
    {
        try
        {
            if (!Directory.Exists(stagedPath) || Directory.Exists(originalPath)) return;
            Directory.CreateDirectory(Path.GetDirectoryName(originalPath)!);
            Directory.Move(stagedPath, originalPath);
        }
        catch { }
    }

    private static void DeleteFileVerified(string path)
    {
        if (!File.Exists(path)) return;
        File.Delete(path);
        if (File.Exists(path))
            throw new IOException($"Profile source could not be removed: {path}");
    }

    private static void DeleteDirectoryVerified(string path)
    {
        if (!Directory.Exists(path)) return;
        Directory.Delete(path, true);
        if (Directory.Exists(path))
            throw new IOException($"Profile workspace could not be removed: {path}");
    }

    private static void TryDelete(string path)
    {
        try { if (File.Exists(path)) File.Delete(path); } catch { }
    }

    private static void TryDeleteDirectory(string path)
    {
        try { if (Directory.Exists(path)) Directory.Delete(path, true); } catch { }
    }

    private static bool IsOfficialExpansion(string packageId) =>
        packageId.StartsWith("ludeon.rimworld.", StringComparison.OrdinalIgnoreCase);
}
