using System.Text.Json;
using System.Text.Json.Serialization;

namespace Avalonia.Diagnostics.AutomationBridge.Tests.Protocol;

/// <summary>
/// Shared <see cref="JsonSerializerOptions"/> used across all protocol serialization tests.
/// Matches the options the bridge runtime uses: compact, null-omitting.
/// </summary>
internal static class ProtocolTestOptions
{
    internal static readonly JsonSerializerOptions Default = new()
    {
        WriteIndented = false,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };
}
