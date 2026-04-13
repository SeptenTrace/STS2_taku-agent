using System.Text.Json;
using TakuAgentMod.State.Snapshots;

namespace TakuAgentMod.State.Exporters;

internal sealed class SnapshotFileExporter
{
    private const string OutputFolderName = "STS2TakuAgent/phase1-feasibility";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    private readonly object _sync = new();
    private int _snapshotSequence;

    public string OutputDirectory => GetOutputDirectory();

    public void Log(string message)
    {
        lock (_sync)
        {
            string line = $"[{DateTimeOffset.Now:O}] {message}{Environment.NewLine}";
            Console.Write(line);
            File.AppendAllText(Path.Combine(GetOutputDirectory(), "phase1.log"), line);
        }
    }

    public string WriteSnapshot(BattleSnapshot snapshot)
    {
        string filePath = Path.Combine(
            GetOutputDirectory(),
            $"{DateTime.Now:yyyyMMdd-HHmmss-fff}_{Interlocked.Increment(ref _snapshotSequence):D4}_{Sanitize(snapshot.Trigger)}.json");

        string json = JsonSerializer.Serialize(snapshot, JsonOptions);
        lock (_sync)
        {
            File.WriteAllText(filePath, json);
        }

        return filePath;
    }

    private static string GetOutputDirectory()
    {
        string baseDirectory = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        if (string.IsNullOrWhiteSpace(baseDirectory))
        {
            baseDirectory = Path.GetTempPath();
        }

        string outputDirectory = Path.Combine(baseDirectory, OutputFolderName);
        Directory.CreateDirectory(outputDirectory);
        return outputDirectory;
    }

    private static string Sanitize(string value)
    {
        char[] invalid = Path.GetInvalidFileNameChars();
        return new string(value.Select(ch => invalid.Contains(ch) ? '_' : ch).ToArray());
    }
}
