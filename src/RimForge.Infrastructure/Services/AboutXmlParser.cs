using System.Xml;
using System.Xml.Linq;
using RimForge.Core.Models;
using RimForge.Core.Services;

namespace RimForge.Infrastructure.Services;

public sealed class AboutXmlParser : IAboutXmlParser
{
    public async Task<ModRecord> ParseAsync(string modFolder, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(modFolder);

        var fullPath = Path.GetFullPath(modFolder);
        var folderName = Path.GetFileName(fullPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
        var aboutPath = Path.Combine(fullPath, "About", "About.xml");
        var errors = new List<string>();

        if (!File.Exists(aboutPath))
        {
            errors.Add("Missing About.xml");
            return CreateFallback(fullPath, folderName, aboutPath, errors);
        }

        XDocument document;
        try
        {
            var settings = new XmlReaderSettings
            {
                Async = true,
                DtdProcessing = DtdProcessing.Ignore,
                IgnoreComments = true,
                IgnoreWhitespace = true
            };

            await using var stream = new FileStream(
                aboutPath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.ReadWrite | FileShare.Delete,
                16 * 1024,
                FileOptions.Asynchronous | FileOptions.SequentialScan);
            using var reader = XmlReader.Create(stream, settings);
            document = await XDocument.LoadAsync(reader, LoadOptions.None, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is XmlException or IOException or UnauthorizedAccessException)
        {
            errors.Add($"Invalid About.xml: {ex.Message}");
            return CreateFallback(fullPath, folderName, aboutPath, errors);
        }

        var metadata = document.Root;
        if (metadata is null || !metadata.Name.LocalName.Equals("ModMetaData", StringComparison.OrdinalIgnoreCase))
        {
            errors.Add("About.xml does not contain a ModMetaData root node");
            return CreateFallback(fullPath, folderName, aboutPath, errors);
        }

        var supportedVersions = Values(metadata, "supportedVersions");
        if (supportedVersions.Count == 0)
        {
            var targetVersion = Text(metadata, "targetVersion");
            if (!string.IsNullOrWhiteSpace(targetVersion))
            {
                supportedVersions.Add(targetVersion);
            }
        }

        var dependencies = new List<ModDependency>();
        AddDependencies(Element(metadata, "modDependencies"), dependencies, "modDependencies", null);

        var byVersion = Element(metadata, "modDependenciesByVersion");
        if (byVersion is not null)
        {
            foreach (var versionNode in byVersion.Elements())
            {
                AddDependencies(versionNode, dependencies, "modDependenciesByVersion", versionNode.Name.LocalName);
            }
        }

        dependencies = dependencies
            .Where(d => !string.IsNullOrWhiteSpace(d.PackageId))
            .GroupBy(d => d.PackageId, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .OrderBy(d => d.PackageId, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var incompatibleWith = Values(metadata, "incompatibleWith");
        var incompatibleByVersion = Element(metadata, "incompatibleWithByVersion");
        if (incompatibleByVersion is not null)
        {
            foreach (var versionNode in incompatibleByVersion.Elements())
                incompatibleWith.AddRange(DirectValues(versionNode));
        }
        incompatibleWith = incompatibleWith
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(value => value, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var workshopId = IsWorkshopId(folderName) ? folderName : null;
        var packageId = Text(metadata, "packageId");
        var name = Text(metadata, "name");

        if (string.IsNullOrWhiteSpace(name)) errors.Add("Missing name");
        if (string.IsNullOrWhiteSpace(packageId)) errors.Add("Missing package ID");

        return new ModRecord
        {
            Id = packageId ?? workshopId ?? fullPath,
            RootPath = fullPath,
            FolderName = folderName,
            AboutPath = aboutPath,
            Name = name,
            PackageId = packageId,
            Author = Text(metadata, "author"),
            Description = Text(metadata, "description"),
            WorkshopId = workshopId,
            WorkshopUrl = workshopId is null ? null : $"https://steamcommunity.com/sharedfiles/filedetails/?id={workshopId}",
            PreviewImagePath = FindPreviewImage(fullPath),
            LastModified = Directory.GetLastWriteTimeUtc(fullPath),
            SupportedVersions = supportedVersions,
            Dependencies = dependencies,
            LoadBefore = Values(metadata, "loadBefore"),
            LoadAfter = Values(metadata, "loadAfter"),
            IncompatibleWith = incompatibleWith,
            Errors = errors
        };
    }

    private static void AddDependencies(
        XElement? container,
        ICollection<ModDependency> output,
        string source,
        string? version)
    {
        if (container is null) return;

        foreach (var item in container.Elements().Where(e => e.Name.LocalName.Equals("li", StringComparison.OrdinalIgnoreCase)))
        {
            var packageId = Text(item, "packageId");
            if (string.IsNullOrWhiteSpace(packageId)) continue;

            output.Add(new ModDependency(
                packageId,
                Text(item, "displayName"),
                Text(item, "steamWorkshopUrl"),
                Text(item, "downloadUrl"),
                source,
                version));
        }
    }

    private static string? Text(XElement parent, string localName)
    {
        var value = parent.Elements()
            .FirstOrDefault(e => e.Name.LocalName.Equals(localName, StringComparison.OrdinalIgnoreCase))?
            .Value
            .Trim();
        return string.IsNullOrWhiteSpace(value) ? null : value;
    }

    private static XElement? Element(XElement parent, string localName) =>
        parent.Elements().FirstOrDefault(element =>
            element.Name.LocalName.Equals(localName, StringComparison.OrdinalIgnoreCase));

    private static List<string> Values(XElement parent, string localName)
    {
        var container = Element(parent, localName);
        if (container is null) return new List<string>();

        return DirectValues(container);
    }

    private static List<string> DirectValues(XElement container) =>
        container.Elements()
            .Where(e => e.Name.LocalName.Equals("li", StringComparison.OrdinalIgnoreCase))
            .Select(e => e.Value.Trim())
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

    private static string? FindPreviewImage(string modRoot)
    {
        var candidates = new[]
        {
            Path.Combine(modRoot, "About", "Preview.png"),
            Path.Combine(modRoot, "About", "Preview.jpg"),
            Path.Combine(modRoot, "About", "Preview.jpeg"),
            Path.Combine(modRoot, "Preview.png"),
            Path.Combine(modRoot, "Preview.jpg"),
            Path.Combine(modRoot, "preview.png"),
            Path.Combine(modRoot, "preview.jpg")
        };

        return candidates.FirstOrDefault(File.Exists);
    }

    private static bool IsWorkshopId(string value) => value.Length > 4 && value.All(char.IsDigit);

    private static ModRecord CreateFallback(
        string fullPath,
        string folderName,
        string aboutPath,
        IReadOnlyList<string> errors)
    {
        var workshopId = IsWorkshopId(folderName) ? folderName : null;
        return new ModRecord
        {
            Id = workshopId ?? fullPath,
            RootPath = fullPath,
            FolderName = folderName,
            AboutPath = aboutPath,
            WorkshopId = workshopId,
            WorkshopUrl = workshopId is null ? null : $"https://steamcommunity.com/sharedfiles/filedetails/?id={workshopId}",
            PreviewImagePath = FindPreviewImage(fullPath),
            LastModified = Directory.GetLastWriteTimeUtc(fullPath),
            Errors = errors
        };
    }
}
