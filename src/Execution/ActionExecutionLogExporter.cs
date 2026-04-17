using System.Text.Json;

namespace TakuAgentMod.Execution;

internal sealed class ActionExecutionLogExporter
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = false
    };

    private readonly object _sync = new();

    public void Append(ActionExecutionLogEntry entry)
    {
        string line = JsonSerializer.Serialize(entry, JsonOptions) + Environment.NewLine;
        string path = Path.Combine(State.Exporters.SnapshotFileExporter.GetOutputDirectoryPath(), "action-execution.jsonl");

        lock (_sync)
        {
            File.AppendAllText(path, line);
        }
    }
}

internal sealed record ActionExecutionLogEntry(
    DateTimeOffset Timestamp,
    string ActionType,
    IReadOnlyDictionary<string, string?> Parameters,
    bool Success,
    string Message,
    string ReasonCode,
    bool Retryable,
    string StateTypeBefore,
    string StateTypeAfter,
    int ObservationVersion,
    bool ObservationChanged,
    IReadOnlyList<string> ChangedSections,
    IReadOnlyList<string> Facts,
    string? DebugSnapshotPath);
