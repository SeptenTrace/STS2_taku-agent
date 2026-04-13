using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Players;
using TakuAgentMod.State.Builders;
using TakuAgentMod.State.Exporters;
using TakuAgentMod.State.Snapshots;
using TakuAgentMod.State.Validation;

namespace TakuAgentMod.Diagnostics;

internal static class BattleStateCaptureService
{
    private static readonly BattleSnapshotBuilder SnapshotBuilder = new();
    private static readonly SnapshotFileExporter Exporter = new();
    private static readonly BattleSnapshotValidator Validator = new();

    public static void Log(string message)
    {
        Exporter.Log(message);
    }

    public static void CaptureSnapshot(string trigger, CombatState? state, Player? activePlayer = null)
    {
        if (state is null)
        {
            Log($"Skip snapshot for trigger '{trigger}': combat state is null.");
            return;
        }

        try
        {
            BattleSnapshot snapshot = SnapshotBuilder.Build(trigger, state, activePlayer);
            SnapshotValidationResult validation = Validator.Validate(snapshot);
            foreach (string warning in validation.Warnings)
            {
                Log($"Snapshot validation warning: {warning}");
            }

            string filePath = Exporter.WriteSnapshot(snapshot);
            Log(
                $"Snapshot written: trigger={trigger}, round={snapshot.RoundNumber}, side={snapshot.CurrentSide}, hand={snapshot.Player?.Hand.Count ?? 0}, enemies={snapshot.Enemies.Count}, file={filePath}");
        }
        catch (Exception ex)
        {
            Log($"Snapshot capture failed for trigger '{trigger}': {ex}");
        }
    }
}
