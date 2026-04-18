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
    string CorrelationId,
    string ActionType,
    IReadOnlyDictionary<string, string?> Parameters,
    bool Success,
    string Message,
    string ReasonCode,
    bool Retryable,
    RunContextLogSnapshot? RunContext,
    PlayerResourceLogSnapshot? PlayerBefore,
    PlayerResourceLogSnapshot? PlayerAfter,
    ActionSurfaceLogSummary? ActionSurfaceBefore,
    ActionSurfaceLogSummary? ActionSurfaceAfter,
    string StateTypeBefore,
    string StateTypeAfter,
    bool IsStableBefore,
    bool IsStableAfter,
    int ObservationVersion,
    bool ObservationChanged,
    IReadOnlyList<string> ChangedSections,
    IReadOnlyList<string> Facts,
    string? DebugSnapshotPath);

internal sealed record RunContextLogSnapshot(
    int? Act,
    int? Floor,
    string? RoomType,
    int? MapCol,
    int? MapRow);

internal sealed record PlayerResourceLogSnapshot(
    int CurrentHp,
    int MaxHp,
    int Block,
    int Gold,
    int? Energy,
    int? MaxEnergy,
    int? Stars,
    int PotionCount,
    int RelicCount);

internal sealed record ActionSurfaceLogSummary(
    string StateType,
    int ActionCount,
    IReadOnlyList<string> ActionTypes);
