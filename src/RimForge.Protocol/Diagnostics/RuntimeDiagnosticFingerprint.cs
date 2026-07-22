using System;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace RimForge.Protocol.Diagnostics;

public static class RuntimeDiagnosticFingerprint
{
    public const int CurrentVersion = 2;

    private static readonly Regex ReferenceSuffixRegex = new Regex(@"\s*\[Ref\s+[0-9A-Fa-f]+\](?:\s+Duplicate stacktrace, see ref for original)?\s*$", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex MissingTypeRegex = new Regex(@"Could not find type named\s+(?<type>[A-Za-z_][\w`]*(?:\.[A-Za-z_][\w`]*)+)", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex XmlLoadPrefixRegex = new Regex(@"^Exception loading def from file\s+[^:]+:\s*", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex HexAddressRegex = new Regex(@"\b0x[0-9A-Fa-f]+\b", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex GuidRegex = new Regex(@"\b[0-9A-Fa-f]{8}-[0-9A-Fa-f]{4}-[0-9A-Fa-f]{4}-[0-9A-Fa-f]{4}-[0-9A-Fa-f]{12}\b", RegexOptions.Compiled);
    private static readonly Regex RimWorldObjectIdRegex = new Regex(@"\b(?:Pawn|Thing|Map|WorldObject|Job|Lord|Hediff|Quest|Site|Faction|Settlement|Caravan|Projectile|Plant|Filth)_[A-Za-z0-9_-]+\b", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex LongNumberRegex = new Regex(@"(?<![A-Za-z])\d{4,}(?![A-Za-z])", RegexOptions.Compiled);
    private static readonly Regex StackOffsetRegex = new Regex(@"\s*\[0x[0-9A-Fa-f]+\]\s*in\s*<[^>]+>:0\s*$", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    public static RuntimeDiagnosticFingerprintResult Create(string severity, string category, string message, string exceptionType, string stackTrace)
    {
        var normalizedMessage = NormalizeMessage(message);
        var canonicalMessage = CanonicalizeRootCause(normalizedMessage);
        var evidence = SelectPrimaryEvidence(message, stackTrace);
        var identity = string.Join("\n", new[]
        {
            NormalizeToken(severity), NormalizeToken(category), NormalizeToken(exceptionType),
            canonicalMessage, NormalizeFrame(evidence.Value)
        });

        using (var sha256 = SHA256.Create())
        {
            var hash = sha256.ComputeHash(Encoding.UTF8.GetBytes(identity));
            var builder = new StringBuilder(24);
            for (var index = 0; index < 12; index++) builder.Append(hash[index].ToString("x2"));
            return new RuntimeDiagnosticFingerprintResult(builder.ToString(), CurrentVersion, normalizedMessage,
                canonicalMessage, evidence.Value, evidence.Kind);
        }
    }

    public static string NormalizeMessage(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return string.Empty;
        var normalized = ReferenceSuffixRegex.Replace(value.Trim(), string.Empty);
        normalized = GuidRegex.Replace(normalized, "{guid}");
        normalized = HexAddressRegex.Replace(normalized, "{address}");
        normalized = RimWorldObjectIdRegex.Replace(normalized, "{object-id}");
        normalized = LongNumberRegex.Replace(normalized, "{number}");
        return CollapseWhitespace(normalized).ToLowerInvariant();
    }

    public static string CanonicalizeRootCause(string normalizedMessage)
    {
        if (string.IsNullOrWhiteSpace(normalizedMessage)) return string.Empty;
        var missingType = MissingTypeRegex.Match(normalizedMessage);
        if (missingType.Success)
            return "could not find type named " + missingType.Groups["type"].Value.ToLowerInvariant();

        return CollapseWhitespace(XmlLoadPrefixRegex.Replace(normalizedMessage, string.Empty));
    }

    public static RuntimeDiagnosticEvidence SelectPrimaryEvidence(string message, string stackTrace)
    {
        var modFrame = SelectPrimaryFrame(stackTrace, false);
        if (modFrame.Length > 0) return new RuntimeDiagnosticEvidence(modFrame, "mod-frame");

        var missingType = MissingTypeRegex.Match(message ?? string.Empty);
        if (missingType.Success) return new RuntimeDiagnosticEvidence(missingType.Groups["type"].Value, "referenced-type");

        var fallback = SelectPrimaryFrame(stackTrace, true);
        return new RuntimeDiagnosticEvidence(fallback, fallback.Length > 0 ? "framework-frame" : string.Empty);
    }

    public static string SelectPrimaryFrame(string stackTrace) => SelectPrimaryFrame(stackTrace, true);

    private static string SelectPrimaryFrame(string stackTrace, bool allowFrameworkFallback)
    {
        if (string.IsNullOrWhiteSpace(stackTrace)) return string.Empty;
        var fallback = string.Empty;
        foreach (var rawLine in stackTrace.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
        {
            var line = rawLine.Trim();
            if (line.Length == 0 || IsLoggingInfrastructure(line)) continue;
            if (fallback.Length == 0) fallback = line;
            if (!IsFrameworkFrame(line)) return line;
        }
        return allowFrameworkFallback ? fallback : string.Empty;
    }

    private static string NormalizeFrame(string value) => string.IsNullOrWhiteSpace(value) ? string.Empty : CollapseWhitespace(StackOffsetRegex.Replace(value.Trim(), string.Empty)).ToLowerInvariant();
    private static string NormalizeToken(string value) => string.IsNullOrWhiteSpace(value) ? string.Empty : CollapseWhitespace(value.Trim()).ToLowerInvariant();

    private static string CollapseWhitespace(string value)
    {
        var builder = new StringBuilder(value.Length); var whitespace = false;
        foreach (var character in value)
        {
            if (char.IsWhiteSpace(character)) { if (!whitespace) builder.Append(' '); whitespace = true; }
            else { builder.Append(character); whitespace = false; }
        }
        return builder.ToString().Trim();
    }

    private static bool IsLoggingInfrastructure(string line) =>
        line.IndexOf("StackTraceUtility:ExtractStackTrace", StringComparison.OrdinalIgnoreCase) >= 0 ||
        line.IndexOf("Verse.Log:", StringComparison.OrdinalIgnoreCase) >= 0 ||
        line.IndexOf("UnityEngine.Debug", StringComparison.OrdinalIgnoreCase) >= 0;

    private static bool IsFrameworkFrame(string line)
    {
        var value = line.TrimStart();
        if (value.StartsWith("at ", StringComparison.Ordinal)) value = value.Substring(3);
        if (value.StartsWith("(wrapper ", StringComparison.Ordinal)) return true;
        return value.StartsWith("System.", StringComparison.Ordinal) || value.StartsWith("Microsoft.", StringComparison.Ordinal) ||
               value.StartsWith("UnityEngine.", StringComparison.Ordinal) || value.StartsWith("Verse.", StringComparison.Ordinal) ||
               value.StartsWith("RimWorld.", StringComparison.Ordinal) || value.StartsWith("HarmonyLib.", StringComparison.Ordinal) ||
               value.StartsWith("MonoMod.", StringComparison.Ordinal);
    }
}

public sealed class RuntimeDiagnosticFingerprintResult
{
    public RuntimeDiagnosticFingerprintResult(string fingerprint, int version, string normalizedMessage, string canonicalMessage, string primaryEvidence, string primaryEvidenceKind)
    { Fingerprint = fingerprint; Version = version; NormalizedMessage = normalizedMessage; CanonicalMessage = canonicalMessage; PrimaryEvidence = primaryEvidence; PrimaryEvidenceKind = primaryEvidenceKind; }
    public string Fingerprint { get; }
    public int Version { get; }
    public string NormalizedMessage { get; }
    public string CanonicalMessage { get; }
    public string PrimaryEvidence { get; }
    public string PrimaryEvidenceKind { get; }
    public string PrimaryFrame => PrimaryEvidenceKind.EndsWith("frame", StringComparison.Ordinal) ? PrimaryEvidence : string.Empty;
}

public readonly struct RuntimeDiagnosticEvidence
{
    public RuntimeDiagnosticEvidence(string value, string kind) { Value = value ?? string.Empty; Kind = kind ?? string.Empty; }
    public string Value { get; }
    public string Kind { get; }
}
