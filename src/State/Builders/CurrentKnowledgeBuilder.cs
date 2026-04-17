using TakuAgentMod.State.Snapshots;
using TakuAgentMod.State.Support;

namespace TakuAgentMod.State.Builders;

internal static class CurrentKnowledgeBuilder
{
    public static CurrentKnowledgeSnapshot Build(GameSnapshot snapshot)
    {
        Dictionary<string, CardKnowledgeSnapshot> cards = BuildCards(snapshot);
        Dictionary<string, RelicKnowledgeSnapshot> relics = BuildRelics(snapshot);
        Dictionary<string, PotionKnowledgeSnapshot> potions = BuildPotions(snapshot);
        Dictionary<string, StatusKnowledgeSnapshot> statuses = BuildStatuses(snapshot);

        return new CurrentKnowledgeSnapshot(
            Cards: cards.Values.OrderBy(card => card.Id, StringComparer.OrdinalIgnoreCase).ToArray(),
            Relics: relics.Values.OrderBy(relic => relic.Id, StringComparer.OrdinalIgnoreCase).ToArray(),
            Potions: potions.Values.OrderBy(potion => potion.Id, StringComparer.OrdinalIgnoreCase).ToArray(),
            Statuses: statuses.Values.OrderBy(status => status.Id, StringComparer.OrdinalIgnoreCase).ToArray());
    }

    private static Dictionary<string, CardKnowledgeSnapshot> BuildCards(GameSnapshot snapshot)
    {
        var cards = new Dictionary<string, CardKnowledgeSnapshot>(StringComparer.OrdinalIgnoreCase);

        if (snapshot.Player is not null)
        {
            foreach (DeckCardEntrySnapshot card in snapshot.Player.Deck.Cards)
            {
                CardKnowledgeSnapshot knowledge = new(
                    Id: card.Id,
                    Title: card.Title,
                    Type: card.Type,
                    Rarity: card.Rarity,
                    TargetType: null,
                    Cost: card.Cost,
                    StarCost: card.StarCost,
                    Description: card.Description,
                    SemanticSummary: SemanticDescriptionParser.BuildActionSemantic("Unknown", ParseCost(card.Cost), ParseCost(card.StarCost), string.Equals(card.Cost, "X", StringComparison.OrdinalIgnoreCase), card.Description).Summary);
                Upsert(cards, knowledge.Id, knowledge);
            }
        }

        if (snapshot.Combat is not null)
        {
            foreach (HandCardEntrySnapshot card in snapshot.Combat.Hand)
            {
                CardKnowledgeSnapshot knowledge = BuildCardKnowledge(card.Id, card.Title, card.Type, card.Rarity, card.TargetType, card.Cost, card.StarCost, card.Description);
                Upsert(cards, knowledge.Id, knowledge);
            }

            foreach (PileCardEntrySnapshot card in snapshot.Combat.PileDetails.DrawPile.Concat(snapshot.Combat.PileDetails.DiscardPile).Concat(snapshot.Combat.PileDetails.ExhaustPile))
            {
                CardKnowledgeSnapshot knowledge = BuildCardKnowledge(card.Id, card.Title, card.Type, card.Rarity, null, card.Cost, card.StarCost, card.Description);
                Upsert(cards, knowledge.Id, knowledge);
            }
        }

        if (snapshot.CardReward is not null)
        {
            foreach (CardRewardEntrySnapshot card in snapshot.CardReward.Cards)
            {
                CardKnowledgeSnapshot knowledge = BuildCardKnowledge(card.Id, card.Title, card.Type, card.Rarity, null, card.Cost, card.StarCost, card.Description);
                Upsert(cards, knowledge.Id, knowledge);
            }
        }

        if (snapshot.CardSelection is not null)
        {
            foreach (CardRewardEntrySnapshot card in snapshot.CardSelection.Cards)
            {
                CardKnowledgeSnapshot knowledge = BuildCardKnowledge(card.Id, card.Title, card.Type, card.Rarity, null, card.Cost, card.StarCost, card.Description);
                Upsert(cards, knowledge.Id, knowledge);
            }
        }

        return cards;
    }

    private static Dictionary<string, RelicKnowledgeSnapshot> BuildRelics(GameSnapshot snapshot)
    {
        var relics = new Dictionary<string, RelicKnowledgeSnapshot>(StringComparer.OrdinalIgnoreCase);

        if (snapshot.Player is not null)
        {
            foreach (RelicEntrySnapshot relic in snapshot.Player.Relics)
            {
                RelicKnowledgeSnapshot knowledge = new(relic.Id, relic.Title, relic.Description, relic.Rarity);
                Upsert(relics, knowledge.Id, knowledge);
            }
        }

        if (snapshot.Treasure is not null)
        {
            foreach (RelicChoiceEntrySnapshot relic in snapshot.Treasure.Relics)
            {
                RelicKnowledgeSnapshot knowledge = new(relic.Id, relic.Title, relic.Description, relic.Rarity);
                Upsert(relics, knowledge.Id, knowledge);
            }
        }

        return relics;
    }

    private static Dictionary<string, PotionKnowledgeSnapshot> BuildPotions(GameSnapshot snapshot)
    {
        var potions = new Dictionary<string, PotionKnowledgeSnapshot>(StringComparer.OrdinalIgnoreCase);

        if (snapshot.Player is not null)
        {
            foreach (PotionEntrySnapshot potion in snapshot.Player.Potions)
            {
                PotionKnowledgeSnapshot knowledge = new(potion.Id, potion.Title, potion.Description, potion.TargetType, potion.Usage);
                Upsert(potions, knowledge.Id, knowledge);
            }
        }

        return potions;
    }

    private static Dictionary<string, StatusKnowledgeSnapshot> BuildStatuses(GameSnapshot snapshot)
    {
        var statuses = new Dictionary<string, StatusKnowledgeSnapshot>(StringComparer.OrdinalIgnoreCase);

        if (snapshot.Player is not null)
        {
            foreach (StatusEntrySnapshot status in snapshot.Player.Status)
            {
                StatusKnowledgeSnapshot knowledge = new(status.Id, status.Title, status.Description, status.Category);
                Upsert(statuses, knowledge.Id, knowledge);
            }
        }

        if (snapshot.Combat is not null)
        {
            foreach (StatusEntrySnapshot status in snapshot.Combat.Enemies.SelectMany(enemy => enemy.Status))
            {
                StatusKnowledgeSnapshot knowledge = new(status.Id, status.Title, status.Description, status.Category);
                Upsert(statuses, knowledge.Id, knowledge);
            }
        }

        return statuses;
    }

    private static CardKnowledgeSnapshot BuildCardKnowledge(
        string id,
        string title,
        string type,
        string rarity,
        string? targetType,
        string cost,
        string? starCost,
        string description)
    {
        CombatActionSemanticSnapshot semantic = SemanticDescriptionParser.BuildActionSemantic(
            targetType ?? "Unknown",
            ParseCost(cost),
            ParseCost(starCost),
            string.Equals(cost, "X", StringComparison.OrdinalIgnoreCase),
            description);

        return new CardKnowledgeSnapshot(
            Id: id,
            Title: title,
            Type: type,
            Rarity: rarity,
            TargetType: targetType,
            Cost: cost,
            StarCost: starCost,
            Description: description,
            SemanticSummary: semantic.Summary);
    }

    private static int? ParseCost(string? value)
    {
        return int.TryParse(value, out int amount) ? amount : null;
    }

    private static void Upsert<T>(IDictionary<string, T> map, string id, T item) where T : class
    {
        if (string.IsNullOrWhiteSpace(id) || map.ContainsKey(id))
        {
            return;
        }

        map[id] = item;
    }
}
