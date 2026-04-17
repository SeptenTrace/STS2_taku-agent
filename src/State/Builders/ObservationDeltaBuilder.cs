using TakuAgentMod.State.Snapshots;

namespace TakuAgentMod.State.Builders;

internal static class ObservationDeltaBuilder
{
    public static ObservationDeltaSnapshot Build(GameSnapshot? previous, GameSnapshot current, int version, bool changed)
    {
        if (previous is null)
        {
            return new ObservationDeltaSnapshot(
                Version: version,
                StateType: current.Context.StateType,
                Changed: true,
                ChangedSections: ["context"],
                Facts: [$"Initial observation for state '{current.Context.StateType}'."],
                SuggestedQueries: current.Context.RecommendedQueries);
        }

        var changedSections = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var facts = new List<string>();

        if (!string.Equals(previous.Context.StateType, current.Context.StateType, StringComparison.OrdinalIgnoreCase))
        {
            changedSections.Add("context");
            facts.Add($"State changed from '{previous.Context.StateType}' to '{current.Context.StateType}'.");
        }

        CompareRun(previous.Run, current.Run, changedSections, facts);
        ComparePlayer(previous.Player, current.Player, changedSections, facts);
        CompareCombat(previous.Combat, current.Combat, changedSections, facts);
        CompareMap(previous.Map, current.Map, changedSections, facts);
        CompareEvent(previous.Event, current.Event, changedSections, facts);
        CompareFakeMerchant(previous.FakeMerchant, current.FakeMerchant, changedSections, facts);
        CompareShop(previous.Shop, current.Shop, changedSections, facts);
        CompareRewards(previous.Rewards, current.Rewards, changedSections, facts);
        CompareCardReward(previous.CardReward, current.CardReward, changedSections, facts);
        CompareRestSite(previous.RestSite, current.RestSite, changedSections, facts);
        CompareTreasure(previous.Treasure, current.Treasure, changedSections, facts);
        CompareCardSelection(previous.CardSelection, current.CardSelection, changedSections, facts);
        CompareBundleSelection(previous.BundleSelection, current.BundleSelection, changedSections, facts);
        CompareRelicSelection(previous.RelicSelection, current.RelicSelection, changedSections, facts);
        CompareCrystalSphere(previous.CrystalSphere, current.CrystalSphere, changedSections, facts);
        CompareOverlay(previous.Overlay, current.Overlay, changedSections, facts);

        if (!changed && facts.Count == 0)
        {
            facts.Add("No state changes since the previous distinct observation.");
        }

        return new ObservationDeltaSnapshot(
            Version: version,
            StateType: current.Context.StateType,
            Changed: changed,
            ChangedSections: changedSections.OrderBy(section => section, StringComparer.OrdinalIgnoreCase).ToArray(),
            Facts: facts,
            SuggestedQueries: current.Context.RecommendedQueries);
    }

    private static void CompareRun(RunSnapshot? previous, RunSnapshot? current, ISet<string> changedSections, ICollection<string> facts)
    {
        if (previous is null || current is null)
        {
            return;
        }

        if (previous.Floor != current.Floor || previous.Act != current.Act)
        {
            changedSections.Add("run");
            facts.Add($"Run progress moved to act {current.Act}, floor {current.Floor}.");
        }
    }

    private static void ComparePlayer(PlayerStateSnapshot? previous, PlayerStateSnapshot? current, ISet<string> changedSections, ICollection<string> facts)
    {
        if (previous is null || current is null)
        {
            return;
        }

        if (previous.CurrentHp != current.CurrentHp)
        {
            changedSections.Add("player");
            facts.Add($"Player HP changed {previous.CurrentHp} -> {current.CurrentHp}.");
        }

        if (previous.Block != current.Block)
        {
            changedSections.Add("player");
            facts.Add($"Player block changed {previous.Block} -> {current.Block}.");
        }

        if (previous.Gold != current.Gold)
        {
            changedSections.Add("player");
            facts.Add($"Player gold changed {previous.Gold} -> {current.Gold}.");
        }

        if (previous.Energy != current.Energy || previous.Stars != current.Stars)
        {
            changedSections.Add("player");
            facts.Add($"Player resources changed to energy {current.Energy ?? 0}/{current.MaxEnergy ?? 0}, stars {current.Stars ?? 0}.");
        }

        if (previous.DeckCount != current.DeckCount)
        {
            changedSections.Add("player_deck");
            facts.Add($"Player deck count changed {previous.DeckCount} -> {current.DeckCount}.");
        }

        string previousRelics = string.Join("|", previous.RelicIdsOrEmpty());
        string currentRelics = string.Join("|", current.RelicIdsOrEmpty());
        if (!string.Equals(previousRelics, currentRelics, StringComparison.Ordinal))
        {
            changedSections.Add("player_relics");
            facts.Add("Player relic list changed.");
        }

        string previousPotions = string.Join("|", previous.PotionIdsOrEmpty());
        string currentPotions = string.Join("|", current.PotionIdsOrEmpty());
        if (!string.Equals(previousPotions, currentPotions, StringComparison.Ordinal))
        {
            changedSections.Add("player_potions");
            facts.Add("Player potion slots changed.");
        }

        string previousStatus = BuildStatusSignature(previous.Status);
        string currentStatus = BuildStatusSignature(current.Status);
        if (!string.Equals(previousStatus, currentStatus, StringComparison.Ordinal))
        {
            changedSections.Add("player_status");
            facts.Add("Player status stack changed.");
        }
    }

    private static void CompareCombat(CombatStateSnapshot? previous, CombatStateSnapshot? current, ISet<string> changedSections, ICollection<string> facts)
    {
        if (previous is null || current is null)
        {
            return;
        }

        if (previous.Round != current.Round || !string.Equals(previous.Side, current.Side, StringComparison.OrdinalIgnoreCase))
        {
            changedSections.Add("combat");
            facts.Add($"Combat moved to round {current.Round}, side {current.Side}.");
        }

        if (previous.Piles.Draw != current.Piles.Draw || previous.Piles.Discard != current.Piles.Discard || previous.Piles.Exhaust != current.Piles.Exhaust)
        {
            changedSections.Add("combat_piles");
            facts.Add($"Pile counts changed to draw {current.Piles.Draw}, discard {current.Piles.Discard}, exhaust {current.Piles.Exhaust}.");
        }

        string previousHand = string.Join("|", previous.Hand.Select(card => card.Id));
        string currentHand = string.Join("|", current.Hand.Select(card => card.Id));
        if (!string.Equals(previousHand, currentHand, StringComparison.Ordinal))
        {
            changedSections.Add("combat_hand");
            facts.Add($"Hand changed to {current.Hand.Count} visible cards.");
        }

        string previousEnemies = BuildEnemySignature(previous.Enemies);
        string currentEnemies = BuildEnemySignature(current.Enemies);
        if (!string.Equals(previousEnemies, currentEnemies, StringComparison.Ordinal))
        {
            changedSections.Add("combat_enemies");
            facts.Add("Enemy HP, intents, or statuses changed.");
        }

        string previousSelection = BuildCombatSelectionSignature(previous.Selection);
        string currentSelection = BuildCombatSelectionSignature(current.Selection);
        if (!string.Equals(previousSelection, currentSelection, StringComparison.Ordinal))
        {
            changedSections.Add("combat_selection");
            facts.Add(current.Selection is null
                ? "Combat selection mode ended."
                : $"Combat selection mode changed to '{current.Selection.Mode}'.");
        }

        string previousActions = BuildActionSignature(previous.AvailableActions);
        string currentActions = BuildActionSignature(current.AvailableActions);
        if (!string.Equals(previousActions, currentActions, StringComparison.Ordinal))
        {
            changedSections.Add("combat_actions");
            facts.Add($"Available combat actions changed to {current.AvailableActions.Count}.");
        }
    }

    private static void CompareMap(MapStateSnapshot? previous, MapStateSnapshot? current, ISet<string> changedSections, ICollection<string> facts)
    {
        if (previous is null || current is null)
        {
            return;
        }

        string previousPos = previous.CurrentPosition is null ? "none" : $"{previous.CurrentPosition.Col}:{previous.CurrentPosition.Row}";
        string currentPos = current.CurrentPosition is null ? "none" : $"{current.CurrentPosition.Col}:{current.CurrentPosition.Row}";
        if (!string.Equals(previousPos, currentPos, StringComparison.Ordinal) || previous.NextOptions.Count != current.NextOptions.Count)
        {
            changedSections.Add("map");
            facts.Add($"Map options changed to {current.NextOptions.Count} travelable nodes.");
        }
    }

    private static void CompareEvent(EventStateSnapshot? previous, EventStateSnapshot? current, ISet<string> changedSections, ICollection<string> facts)
    {
        if (previous is null || current is null)
        {
            return;
        }

        if (previous.Options.Count != current.Options.Count || !string.Equals(previous.Body, current.Body, StringComparison.Ordinal))
        {
            changedSections.Add("event");
            facts.Add($"Event content changed. Visible options: {current.Options.Count}.");
        }
    }

    private static void CompareShop(ShopStateSnapshot? previous, ShopStateSnapshot? current, ISet<string> changedSections, ICollection<string> facts)
    {
        if (previous is null || current is null)
        {
            return;
        }

        int previousAffordable = previous.Items.Count(item => item.CanAfford);
        int currentAffordable = current.Items.Count(item => item.CanAfford);
        if (previous.Items.Count != current.Items.Count || previousAffordable != currentAffordable)
        {
            changedSections.Add("shop");
            facts.Add($"Shop inventory changed. Affordable items: {currentAffordable}/{current.Items.Count}.");
        }
    }

    private static void CompareFakeMerchant(FakeMerchantStateSnapshot? previous, FakeMerchantStateSnapshot? current, ISet<string> changedSections, ICollection<string> facts)
    {
        if (previous is null || current is null)
        {
            return;
        }

        int previousAffordable = previous.Items.Count(item => item.CanAfford);
        int currentAffordable = current.Items.Count(item => item.CanAfford);
        if (previous.StartedFight != current.StartedFight ||
            previous.Items.Count != current.Items.Count ||
            previousAffordable != currentAffordable ||
            previous.CanProceed != current.CanProceed)
        {
            changedSections.Add("fake_merchant");
            facts.Add($"Fake merchant changed. Items: {current.Items.Count}, affordable: {currentAffordable}, started fight: {current.StartedFight}.");
        }
    }

    private static void CompareRewards(RewardsStateSnapshot? previous, RewardsStateSnapshot? current, ISet<string> changedSections, ICollection<string> facts)
    {
        if (previous is null || current is null)
        {
            return;
        }

        if (previous.Items.Count != current.Items.Count || previous.CanProceed != current.CanProceed)
        {
            changedSections.Add("rewards");
            facts.Add($"Rewards screen changed. Visible rewards: {current.Items.Count}.");
        }
    }

    private static void CompareCardReward(CardRewardStateSnapshot? previous, CardRewardStateSnapshot? current, ISet<string> changedSections, ICollection<string> facts)
    {
        if (previous is null || current is null)
        {
            return;
        }

        string previousCards = string.Join("|", previous.Cards.Select(card => card.Id));
        string currentCards = string.Join("|", current.Cards.Select(card => card.Id));
        if (!string.Equals(previousCards, currentCards, StringComparison.Ordinal) || previous.CanSkip != current.CanSkip)
        {
            changedSections.Add("card_reward");
            facts.Add($"Card reward changed. Visible cards: {current.Cards.Count}.");
        }
    }

    private static void CompareRestSite(RestSiteStateSnapshot? previous, RestSiteStateSnapshot? current, ISet<string> changedSections, ICollection<string> facts)
    {
        if (previous is null || current is null)
        {
            return;
        }

        string previousEnabled = string.Join("|", previous.Options.Where(option => option.IsEnabled).Select(option => option.Id));
        string currentEnabled = string.Join("|", current.Options.Where(option => option.IsEnabled).Select(option => option.Id));
        if (!string.Equals(previousEnabled, currentEnabled, StringComparison.Ordinal) || previous.CanProceed != current.CanProceed)
        {
            changedSections.Add("rest_site");
            facts.Add("Rest-site options changed.");
        }
    }

    private static void CompareTreasure(TreasureStateSnapshot? previous, TreasureStateSnapshot? current, ISet<string> changedSections, ICollection<string> facts)
    {
        if (previous is null || current is null)
        {
            return;
        }

        string previousRelics = string.Join("|", previous.Relics.Select(relic => relic.Id));
        string currentRelics = string.Join("|", current.Relics.Select(relic => relic.Id));
        if (!string.Equals(previousRelics, currentRelics, StringComparison.Ordinal) || previous.CanProceed != current.CanProceed)
        {
            changedSections.Add("treasure");
            facts.Add($"Treasure options changed. Visible relics: {current.Relics.Count}.");
        }
    }

    private static void CompareCardSelection(CardSelectionStateSnapshot? previous, CardSelectionStateSnapshot? current, ISet<string> changedSections, ICollection<string> facts)
    {
        if (previous is null || current is null)
        {
            return;
        }

        string previousCards = string.Join("|", previous.Cards.Select(card => card.Id));
        string currentCards = string.Join("|", current.Cards.Select(card => card.Id));
        if (!string.Equals(previousCards, currentCards, StringComparison.Ordinal) ||
            previous.CanConfirm != current.CanConfirm ||
            previous.CanCancel != current.CanCancel ||
            previous.CanSkip != current.CanSkip)
        {
            changedSections.Add("card_selection");
            facts.Add($"Card selection changed. Visible cards: {current.Cards.Count}.");
        }
    }

    private static void CompareBundleSelection(BundleSelectionStateSnapshot? previous, BundleSelectionStateSnapshot? current, ISet<string> changedSections, ICollection<string> facts)
    {
        if (previous is null || current is null)
        {
            return;
        }

        string previousBundles = string.Join("|", previous.Bundles.Select(bundle => $"{bundle.Index}:{string.Join(",", bundle.Cards.Select(card => card.Id))}"));
        string currentBundles = string.Join("|", current.Bundles.Select(bundle => $"{bundle.Index}:{string.Join(",", bundle.Cards.Select(card => card.Id))}"));
        if (!string.Equals(previousBundles, currentBundles, StringComparison.Ordinal) ||
            previous.PreviewShowing != current.PreviewShowing ||
            previous.CanConfirm != current.CanConfirm ||
            previous.CanCancel != current.CanCancel)
        {
            changedSections.Add("bundle_selection");
            facts.Add($"Bundle selection changed. Visible bundles: {current.Bundles.Count}, preview showing: {current.PreviewShowing}.");
        }
    }

    private static void CompareRelicSelection(RelicSelectionStateSnapshot? previous, RelicSelectionStateSnapshot? current, ISet<string> changedSections, ICollection<string> facts)
    {
        if (previous is null || current is null)
        {
            return;
        }

        string previousRelics = string.Join("|", previous.Relics.Select(relic => relic.Id));
        string currentRelics = string.Join("|", current.Relics.Select(relic => relic.Id));
        if (!string.Equals(previousRelics, currentRelics, StringComparison.Ordinal) || previous.CanSkip != current.CanSkip)
        {
            changedSections.Add("relic_selection");
            facts.Add($"Relic selection changed. Visible relics: {current.Relics.Count}.");
        }
    }

    private static void CompareOverlay(OverlayStateSnapshot? previous, OverlayStateSnapshot? current, ISet<string> changedSections, ICollection<string> facts)
    {
        if (previous is null || current is null)
        {
            return;
        }

        if (!string.Equals(previous.ScreenType, current.ScreenType, StringComparison.Ordinal) ||
            !string.Equals(previous.Message, current.Message, StringComparison.Ordinal) ||
            previous.ManualInterventionRequired != current.ManualInterventionRequired)
        {
            changedSections.Add("overlay");
            facts.Add($"Overlay changed to '{current.ScreenType}'.");
        }
    }

    private static void CompareCrystalSphere(CrystalSphereStateSnapshot? previous, CrystalSphereStateSnapshot? current, ISet<string> changedSections, ICollection<string> facts)
    {
        if (previous is null || current is null)
        {
            return;
        }

        string previousClickable = string.Join("|", previous.ClickableCells.Select(cell => $"{cell.X}:{cell.Y}"));
        string currentClickable = string.Join("|", current.ClickableCells.Select(cell => $"{cell.X}:{cell.Y}"));
        if (!string.Equals(previousClickable, currentClickable, StringComparison.Ordinal) ||
            !string.Equals(previous.Tool, current.Tool, StringComparison.Ordinal) ||
            previous.CanProceed != current.CanProceed)
        {
            changedSections.Add("crystal_sphere");
            facts.Add($"Crystal Sphere changed. Clickable cells: {current.ClickableCells.Count}, tool: {current.Tool}, can proceed: {current.CanProceed}.");
        }
    }

    private static string BuildStatusSignature(IReadOnlyList<StatusEntrySnapshot> statuses)
    {
        return string.Join("|", statuses.Select(status => $"{status.Id}:{status.Amount}:{status.Category}"));
    }

    private static string BuildEnemySignature(IReadOnlyList<EnemyStateEntrySnapshot> enemies)
    {
        return string.Join("|", enemies.Select(enemy => $"{enemy.EntityId}:{enemy.CurrentHp}:{enemy.Block}:{BuildStatusSignature(enemy.Status)}:{string.Join(",", enemy.Intents.Select(intent => $"{intent.Type}:{intent.ExpectedValue}:{intent.Label}"))}"));
    }

    private static string BuildActionSignature(IReadOnlyList<CombatActionSnapshot> actions)
    {
        return string.Join("|", actions.Select(action => $"{action.ActionType}:{action.CardIndex}:{action.PotionSlot}:{action.SourceId}:{string.Join(",", action.TargetOptions)}"));
    }

    private static string BuildCombatSelectionSignature(CombatSelectionSnapshot? selection)
    {
        if (selection is null)
        {
            return "none";
        }

        return $"{selection.Mode}:{selection.CanConfirm}:{selection.Prompt}:{string.Join(",", selection.SelectedCards)}";
    }

    private static IReadOnlyList<string> RelicIdsOrEmpty(this PlayerStateSnapshot player)
    {
        return player.Relics.Select(relic => relic.Id).ToArray();
    }

    private static IReadOnlyList<string> PotionIdsOrEmpty(this PlayerStateSnapshot player)
    {
        return player.Potions.Select(potion => potion.Id).ToArray();
    }
}
