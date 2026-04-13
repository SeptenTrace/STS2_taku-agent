using System.Text.Json;
using TakuAgentMod.State.History;

namespace TakuAgentMod.State.Exporters;

internal sealed class ActionHistoryExporter
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = false
    };

    private readonly object _sync = new();

    public void Append(ActionHistoryEntry entry)
    {
        string line = JsonSerializer.Serialize(entry, JsonOptions) + Environment.NewLine;

        lock (_sync)
        {
            File.AppendAllText(
                Path.Combine(SnapshotFileExporter.GetOutputDirectoryPath(), "action-history.jsonl"),
                line);
        }
    }
}
