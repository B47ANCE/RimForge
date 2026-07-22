using System;

namespace RimForge.Protocol.Diagnostics;

public static class RuntimeDiagnosticSeverityMapper
{
    public static string FromUnityLogType(string? unityLogType)
    {
        if (string.Equals(unityLogType, "Warning", StringComparison.OrdinalIgnoreCase)) return "warning";
        if (string.Equals(unityLogType, "Error", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(unityLogType, "Assert", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(unityLogType, "Exception", StringComparison.OrdinalIgnoreCase)) return "error";
        return "message";
    }
}
