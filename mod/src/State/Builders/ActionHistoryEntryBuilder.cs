using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;
using TakuAgentMod.State.History;

namespace TakuAgentMod.State.Builders;

internal sealed class ActionHistoryEntryBuilder
{
    public ActionHistoryEntry BuildCardPlayedEntry(CombatState state, CardPlay cardPlay, string? snapshotPath)
    {
        string playerName = state.Players.FirstOrDefault()?.Creature?.Name ?? "unknown-player";

        return new ActionHistoryEntry(
            Timestamp: DateTimeOffset.Now,
            ActionType: "card_played",
            RoundNumber: state.RoundNumber,
            CurrentSide: state.CurrentSide.ToString(),
            PlayerName: playerName,
            Card: BuildCardActionSnapshot(cardPlay.Card),
            Target: BuildTargetSnapshot(cardPlay.Target),
            Resources: new ResourceSpendSnapshot(
                EnergySpent: cardPlay.Resources.EnergySpent,
                EnergyValue: cardPlay.Resources.EnergyValue,
                StarsSpent: cardPlay.Resources.StarsSpent,
                StarValue: cardPlay.Resources.StarValue),
            ResultPile: cardPlay.ResultPile.ToString(),
            IsAutoPlay: cardPlay.IsAutoPlay,
            PlayIndex: cardPlay.PlayIndex,
            PlayCount: cardPlay.PlayCount,
            SnapshotPath: snapshotPath);
    }

    private static CardActionSnapshot BuildCardActionSnapshot(CardModel card)
    {
        string? description = null;

        try
        {
            description = card.GetDescriptionForPile(card.Pile?.Type ?? PileType.Hand, card.CurrentTarget);
        }
        catch
        {
            description = ToReadableText(card.Description);
        }

        return new CardActionSnapshot(
            Title: card.Title,
            Description: description ?? string.Empty,
            Type: card.Type.ToString(),
            Rarity: card.Rarity.ToString(),
            TargetType: card.TargetType.ToString(),
            EnergyCost: card.EnergyCost.GetResolved(),
            EnergyCostIsX: card.EnergyCost.CostsX,
            StarCost: card.CurrentStarCost,
            IsUpgraded: card.IsUpgraded);
    }

    private static ActionTargetSnapshot? BuildTargetSnapshot(Creature? target)
    {
        if (target is null)
        {
            return null;
        }

        return new ActionTargetSnapshot(
            Name: target.Name,
            ModelType: target.ModelId.ToString(),
            CurrentHp: target.CurrentHp,
            MaxHp: target.MaxHp,
            Block: target.Block,
            IsAlive: target.IsAlive);
    }

    private static string? ToReadableText(object? value)
    {
        if (value is null)
        {
            return null;
        }

        if (value is LocString locString)
        {
            try
            {
                string formatted = locString.GetFormattedText();
                if (!string.IsNullOrWhiteSpace(formatted))
                {
                    return formatted;
                }
            }
            catch
            {
            }

            try
            {
                string raw = locString.GetRawText();
                if (!string.IsNullOrWhiteSpace(raw))
                {
                    return raw;
                }
            }
            catch
            {
            }

            return $"{locString.LocTable}:{locString.LocEntryKey}";
        }

        return value.ToString();
    }
}
