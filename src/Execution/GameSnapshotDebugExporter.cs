using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using TakuAgentMod.State.Exporters;
using TakuAgentMod.State.Snapshots;

namespace TakuAgentMod.Execution;

internal sealed class GameSnapshotDebugExporter
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    private readonly object _sync = new();

    public string Write(string prefix, GameSnapshot snapshot)
    {
        string fileName = $"{DateTime.Now:yyyyMMdd-HHmmss-fff}_{Sanitize(prefix)}.json";
        string path = Path.Combine(SnapshotFileExporter.GetOutputDirectoryPath(), fileName);
        string json = JsonSerializer.Serialize(snapshot, JsonOptions);

        lock (_sync)
        {
            File.WriteAllText(path, json);
        }

        return path;
    }

    private static string Sanitize(string value)
    {
        char[] invalid = Path.GetInvalidFileNameChars();
        return new string(value.Select(ch => invalid.Contains(ch) ? '_' : ch).ToArray());
    }
}
