using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using TakuAgentMod.State.Builders;
using TakuAgentMod.State.Exporters;
using TakuAgentMod.State.History;
using TakuAgentMod.State.Snapshots;
using TakuAgentMod.State.Validation;

namespace TakuAgentMod.Diagnostics;

internal static class BattleStateCaptureService
{
    private static readonly BattleSnapshotBuilder SnapshotBuilder = new();
    private static readonly SnapshotFileExporter Exporter = new();
    private static readonly BattleSnapshotValidator Validator = new();
    private static readonly ActionHistoryEntryBuilder ActionHistoryBuilder = new();
    private static readonly ActionHistoryExporter ActionHistoryExporter = new();

    public static void Log(string message)
    {
        Exporter.Log(message);
    }

    public static string? CaptureSnapshot(string trigger, CombatState? state, Player? activePlayer = null)
    {
        if (state is null)
        {
            Log($"Skip snapshot for trigger '{trigger}': combat state is null.");
            return null;
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
            return filePath;
        }
        catch (Exception ex)
        {
            Log($"Snapshot capture failed for trigger '{trigger}': {ex}");
            return null;
        }
    }

    public static void CaptureCardPlayed(CombatState? state, CardPlay? cardPlay)
    {
        if (state is null)
        {
            Log("Skip action capture for trigger 'after_card_played': combat state is null.");
            return;
        }

        if (cardPlay is null)
        {
            Log("Skip action capture for trigger 'after_card_played': card play is null.");
            return;
        }

        string? snapshotPath = CaptureSnapshot("after_card_played", state);

        try
        {
            ActionHistoryEntry entry = ActionHistoryBuilder.BuildCardPlayedEntry(state, cardPlay, snapshotPath);
            ActionHistoryExporter.Append(entry);
            Log(
                $"Action recorded: type={entry.ActionType}, card={entry.Card.Title}, target={entry.Target?.Name ?? "none"}, energySpent={entry.Resources.EnergySpent}, starsSpent={entry.Resources.StarsSpent}, resultPile={entry.ResultPile}");
        }
        catch (Exception ex)
        {
            Log($"Action history capture failed for trigger 'after_card_played': {ex}");
        }
    }
}
