using System.Collections;
using System.Reflection;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;
using TakuAgentMod.State.Snapshots;

namespace TakuAgentMod.State.Builders;

internal sealed class BattleSnapshotBuilder
{
    public BattleSnapshot Build(string trigger, CombatState state, Player? activePlayer = null)
    {
        Player? player = activePlayer ?? state.Players.FirstOrDefault();

        return new BattleSnapshot(
            Timestamp: DateTimeOffset.Now,
            Trigger: trigger,
            RoundNumber: state.RoundNumber,
            CurrentSide: state.CurrentSide.ToString(),
            EncounterType: state.Encounter?.GetType().FullName,
            Player: player is null ? null : BuildPlayerSnapshot(player),
            Enemies: state.Enemies.Select(BuildEnemySnapshot).ToArray());
    }

    private static PlayerSnapshot BuildPlayerSnapshot(Player player)
    {
        PlayerCombatState? combatState = player.PlayerCombatState;
        Creature? creature = player.Creature;

        if (combatState is null || creature is null)
        {
            return new PlayerSnapshot(
                CharacterType: player.Character?.GetType().FullName,
                Name: creature?.Name ?? "unknown-player",
                CurrentHp: creature?.CurrentHp ?? 0,
                MaxHp: creature?.MaxHp ?? 0,
                Block: creature?.Block ?? 0,
                Energy: 0,
                MaxEnergy: 0,
                Stars: 0,
                Hand: EmptyPileSnapshot("Hand"),
                DrawPile: EmptyPileSnapshot("DrawPile"),
                DiscardPile: EmptyPileSnapshot("DiscardPile"),
                ExhaustPile: EmptyPileSnapshot("ExhaustPile"),
                Potions: player.Potions.Select(BuildPotionSnapshot).ToArray(),
                Relics: player.Relics.Select(BuildRelicSnapshot).ToArray(),
                Powers: Array.Empty<StatusEffectSnapshot>());
        }

        return new PlayerSnapshot(
            CharacterType: player.Character?.GetType().FullName,
            Name: creature.Name,
            CurrentHp: creature.CurrentHp,
            MaxHp: creature.MaxHp,
            Block: creature.Block,
            Energy: combatState.Energy,
            MaxEnergy: combatState.MaxEnergy,
            Stars: combatState.Stars,
            Hand: BuildPileSnapshot(combatState.Hand),
            DrawPile: BuildPileSnapshot(combatState.DrawPile),
            DiscardPile: BuildPileSnapshot(combatState.DiscardPile),
            ExhaustPile: BuildPileSnapshot(combatState.ExhaustPile),
            Potions: player.Potions.Select(BuildPotionSnapshot).ToArray(),
            Relics: player.Relics.Select(BuildRelicSnapshot).ToArray(),
            Powers: creature.Powers.Select(BuildStatusEffectSnapshot).ToArray());
    }

    private static EnemySnapshot BuildEnemySnapshot(Creature creature)
    {
        return new EnemySnapshot(
            Name: creature.Name,
            ModelType: creature.ModelId.ToString(),
            CurrentHp: creature.CurrentHp,
            MaxHp: creature.MaxHp,
            Block: creature.Block,
            IsAlive: creature.IsAlive,
            IsHittable: creature.IsHittable,
            SlotName: creature.SlotName ?? string.Empty,
            Intent: BuildIntentSnapshot(creature),
            Powers: creature.Powers.Select(BuildStatusEffectSnapshot).ToArray());
    }

    private static IntentSnapshot BuildIntentSnapshot(Creature creature)
    {
        object? nextMove = creature.Monster?.NextMove;
        if (nextMove is null)
        {
            return new IntentSnapshot(null, null, creature.Monster?.IntendsToAttack, Array.Empty<IntentDetailSnapshot>());
        }

        IReadOnlyList<Creature> targets = creature.CombatState?.GetOpponentsOf(creature) ?? Array.Empty<Creature>();
        IEnumerable intentObjects = (IEnumerable?)TryGetPropertyValue(nextMove, "Intents") ?? Array.Empty<object>();
        IntentDetailSnapshot[] intents = intentObjects.Cast<object>()
            .Select(intent => BuildIntentDetailSnapshot(intent, creature, targets))
            .ToArray();

        return new IntentSnapshot(
            MoveStateType: nextMove.GetType().FullName,
            StateId: TryReadStringRepresentation(TryGetPropertyValue(nextMove, "StateId"))
                ?? TryReadStringRepresentation(TryGetPropertyValue(nextMove, "Id")),
            IntendsToAttack: creature.Monster?.IntendsToAttack,
            Intents: intents);
    }

    private static IntentDetailSnapshot BuildIntentDetailSnapshot(
        object intent,
        Creature owner,
        IEnumerable<Creature> targets)
    {
        string? label = null;

        try
        {
            MethodInfo? getIntentLabel = intent.GetType().GetMethod(
                "GetIntentLabel",
                BindingFlags.Public | BindingFlags.Instance);

            label = ToReadableText(getIntentLabel?.Invoke(intent, new object[] { targets, owner }));
        }
        catch
        {
            label = null;
        }

        return new IntentDetailSnapshot(
            IntentClass: intent.GetType().FullName ?? intent.GetType().Name,
            IntentType: TryReadStringRepresentation(TryGetPropertyValue(intent, "IntentType")) ?? intent.GetType().Name,
            Label: label);
    }

    private static PileSnapshot BuildPileSnapshot(MegaCrit.Sts2.Core.Entities.Cards.CardPile pile)
    {
        return new PileSnapshot(
            Type: pile.Type.ToString(),
            Count: pile.Cards.Count,
            Cards: pile.Cards.Select(BuildCardSnapshot).ToArray());
    }

    private static PileSnapshot EmptyPileSnapshot(string type)
    {
        return new PileSnapshot(type, 0, Array.Empty<CardSnapshot>());
    }

    private static CardSnapshot BuildCardSnapshot(CardModel card)
    {
        string? description = null;

        try
        {
            description = card.GetDescriptionForPile(card.Pile?.Type ?? MegaCrit.Sts2.Core.Entities.Cards.PileType.Hand, card.CurrentTarget);
        }
        catch
        {
            description = ToReadableText(card.Description);
        }

        return new CardSnapshot(
            Title: card.Title,
            Description: description ?? string.Empty,
            Type: card.Type.ToString(),
            Rarity: card.Rarity.ToString(),
            TargetType: card.TargetType.ToString(),
            EnergyCost: card.EnergyCost.GetResolved(),
            EnergyCostIsX: card.EnergyCost.CostsX,
            StarCost: card.CurrentStarCost,
            IsUpgraded: card.IsUpgraded,
            Keywords: card.Keywords.Select(keyword => keyword.ToString()).ToArray());
    }

    private static RelicSnapshot BuildRelicSnapshot(RelicModel relic)
    {
        string title = ToReadableText(relic.Title) ?? relic.GetType().Name;
        string description = ToReadableText(relic.DynamicDescription)
            ?? string.Empty;

        return new RelicSnapshot(
            Type: relic.GetType().FullName ?? relic.GetType().Name,
            Title: title,
            Description: description,
            Amount: relic.ShowCounter ? relic.DisplayAmount : null,
            Rarity: relic.Rarity.ToString());
    }

    private static PotionSnapshot BuildPotionSnapshot(PotionModel potion)
    {
        string title = ToReadableText(potion.Title) ?? potion.GetType().Name;
        string description = ToReadableText(potion.DynamicDescription)
            ?? string.Empty;

        return new PotionSnapshot(
            Type: potion.GetType().FullName ?? potion.GetType().Name,
            Title: title,
            Description: description,
            Usage: potion.Usage.ToString(),
            TargetType: potion.TargetType.ToString(),
            Rarity: potion.Rarity.ToString());
    }

    private static StatusEffectSnapshot BuildStatusEffectSnapshot(PowerModel power)
    {
        string title = ToReadableText(power.Title) ?? power.GetType().Name;
        string description = ToReadableText(power.SmartDescription)
            ?? ToReadableText(power.Description)
            ?? string.Empty;

        return new StatusEffectSnapshot(
            Type: power.GetType().FullName ?? power.GetType().Name,
            Title: title,
            Description: description,
            Amount: power.DisplayAmount,
            Category: power.Type.ToString());
    }

    private static object? TryGetPropertyValue(object? instance, string propertyName)
    {
        if (instance is null)
        {
            return null;
        }

        PropertyInfo? property = instance.GetType().GetProperty(
            propertyName,
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static);

        return property?.GetValue(instance);
    }

    private static string? TryReadStringRepresentation(object? value)
    {
        if (value is null)
        {
            return null;
        }

        return value switch
        {
            string text => text,
            _ => value.ToString()
        };
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

        return TryReadStringRepresentation(value);
    }
}
