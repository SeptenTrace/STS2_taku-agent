using TakuAgentMod.State.Snapshots;

namespace TakuAgentMod.State.Builders;

internal static class ActionSurfaceBuilder
{
    public static ActionSurfaceSnapshot Build(GameSnapshot snapshot)
    {
        if (!snapshot.Context.IsStable || snapshot.Context.IsTransitioning)
        {
            return new ActionSurfaceSnapshot(
                snapshot.Context.StateType,
                "Wait for the current screen transition to settle before acting.",
                Array.Empty<SceneActionSnapshot>());
        }

        return snapshot.Context.StateType switch
        {
            "menu" => BuildMenuActions(snapshot),
            "monster" or "elite" or "boss" => BuildCombatActions(snapshot),
            "map" => BuildMapActions(snapshot),
            "event" => BuildEventActions(snapshot),
            "fake_merchant" => BuildFakeMerchantActions(snapshot),
            "shop" => BuildShopActions(snapshot),
            "rest_site" => BuildRestSiteActions(snapshot),
            "rewards" => BuildRewardsActions(snapshot),
            "card_reward" => BuildCardRewardActions(snapshot),
            "card_select" => BuildCardSelectionActions(snapshot),
            "bundle_select" => BuildBundleSelectionActions(snapshot),
            "relic_select" => BuildRelicSelectionActions(snapshot),
            "crystal_sphere" => BuildCrystalSphereActions(snapshot),
            "treasure" => BuildTreasureActions(snapshot),
            _ => new ActionSurfaceSnapshot(snapshot.Context.StateType, snapshot.CompactObservation.Goal, Array.Empty<SceneActionSnapshot>())
        };
    }

    private static ActionSurfaceSnapshot BuildMenuActions(GameSnapshot snapshot)
    {
        if (snapshot.Menu?.CanContinue == true)
        {
            return new ActionSurfaceSnapshot(
                snapshot.Context.StateType,
                "Resume the saved run before querying deeper run state.",
                [
                    new SceneActionSnapshot(
                        ActionType: "continue_game",
                        Index: null,
                        Label: snapshot.Menu.ContinueLabel ?? "Continue game",
                        Description: "Resume the most recent saved run from the main menu.",
                        IsAvailable: true,
                        Parameters: Array.Empty<ActionArgumentSnapshot>(),
                        TargetOptions: Array.Empty<string>(),
                        Tags: ["menu", "resume"])
                ]);
        }

        return new ActionSurfaceSnapshot(
            snapshot.Context.StateType,
            "No resumable run is currently available from the main menu.",
            Array.Empty<SceneActionSnapshot>());
    }

    private static ActionSurfaceSnapshot BuildCombatActions(GameSnapshot snapshot)
    {
        string goal = snapshot.Combat?.Selection is null
            ? "Use legal combat actions before expanding card text."
            : "Resolve the active in-combat card selection before returning to normal play.";
        SceneActionSnapshot[] actions = snapshot.Combat?.AvailableActions.Select(BuildCombatAction).ToArray() ?? Array.Empty<SceneActionSnapshot>();
        return new ActionSurfaceSnapshot(snapshot.Context.StateType, goal, actions);
    }

    private static SceneActionSnapshot BuildCombatAction(CombatActionSnapshot action)
    {
        var parameters = new List<ActionArgumentSnapshot>();

        int? index = action.CardIndex ?? action.PotionSlot;
        if (index.HasValue)
        {
            parameters.Add(new ActionArgumentSnapshot("index", index.Value.ToString()));
        }

        if (action.RequiresTarget)
        {
            parameters.Add(new ActionArgumentSnapshot("target", "required"));
        }

        return new SceneActionSnapshot(
            ActionType: action.ActionType,
            Index: index,
            Label: action.SourceTitle ?? action.ActionType,
            Description: action.Semantic?.Summary ?? action.SourceDescription,
            IsAvailable: true,
            Parameters: parameters,
            TargetOptions: action.TargetOptions,
            Tags: action.Tags);
    }

    private static ActionSurfaceSnapshot BuildMapActions(GameSnapshot snapshot)
    {
        var actions = snapshot.Map?.NextOptions
            .Select(option => new SceneActionSnapshot(
                ActionType: "choose_map_node",
                Index: option.Index,
                Label: $"Node {option.Index}: {option.Type}",
                Description: $"Move to ({option.Col}, {option.Row}) and open {option.LeadsTo.Count} follow-up branches.",
                IsAvailable: true,
                Parameters: [new ActionArgumentSnapshot("index", option.Index.ToString())],
                TargetOptions: Array.Empty<string>(),
                Tags: [option.Type.ToLowerInvariant()]))
            .ToList() ?? new List<SceneActionSnapshot>();

        AppendOutOfCombatPotionActions(actions, snapshot.Player);

        return new ActionSurfaceSnapshot(snapshot.Context.StateType, "Choose the next map node.", actions);
    }

    private static ActionSurfaceSnapshot BuildEventActions(GameSnapshot snapshot)
    {
        var actions = new List<SceneActionSnapshot>();
        if (snapshot.Event is not null)
        {
            foreach (EventOptionEntrySnapshot option in snapshot.Event.Options)
            {
                actions.Add(new SceneActionSnapshot(
                    ActionType: "choose_event_option",
                    Index: option.Index,
                    Label: option.Title,
                    Description: option.Description,
                    IsAvailable: !option.IsLocked,
                    Parameters: [new ActionArgumentSnapshot("index", option.Index.ToString())],
                    TargetOptions: Array.Empty<string>(),
                    Tags: BuildTags(option.IsProceed ? "proceed" : "event_option", option.IsLocked ? "locked" : "enabled")));
            }

            if (snapshot.Event.InDialogue && snapshot.Event.Options.Count == 0)
            {
                actions.Add(new SceneActionSnapshot(
                    ActionType: "advance_dialogue",
                    Index: null,
                    Label: "Advance dialogue",
                    Description: "Continue the current event dialogue.",
                    IsAvailable: true,
                    Parameters: Array.Empty<ActionArgumentSnapshot>(),
                    TargetOptions: Array.Empty<string>(),
                    Tags: ["dialogue"]));
            }
        }

        AppendOutOfCombatPotionActions(actions, snapshot.Player);

        return new ActionSurfaceSnapshot(snapshot.Context.StateType, "Choose one visible event option.", actions);
    }

    private static ActionSurfaceSnapshot BuildFakeMerchantActions(GameSnapshot snapshot)
    {
        var actions = new List<SceneActionSnapshot>();

        if (snapshot.FakeMerchant is not null)
        {
            foreach (ShopItemEntrySnapshot item in snapshot.FakeMerchant.Items)
            {
                actions.Add(new SceneActionSnapshot(
                    ActionType: "shop_purchase",
                    Index: item.Index,
                    Label: item.Title,
                    Description: $"{item.Category} for {item.Price} gold. {item.Description}".Trim(),
                    IsAvailable: item.CanAfford,
                    Parameters: [new ActionArgumentSnapshot("index", item.Index.ToString())],
                    TargetOptions: Array.Empty<string>(),
                    Tags: BuildTags("fake_merchant", item.Category, item.CanAfford ? "affordable" : "too_expensive")));
            }

            if (snapshot.FakeMerchant.CanProceed)
            {
                actions.Add(new SceneActionSnapshot(
                    ActionType: "proceed",
                    Index: null,
                    Label: "Leave fake merchant",
                    Description: "Proceed from the fake merchant event.",
                    IsAvailable: true,
                    Parameters: Array.Empty<ActionArgumentSnapshot>(),
                    TargetOptions: Array.Empty<string>(),
                    Tags: ["proceed", "fake_merchant"]));
            }
        }

        AppendOutOfCombatPotionActions(actions, snapshot.Player);

        return new ActionSurfaceSnapshot(snapshot.Context.StateType, "Buy a relic, throw a potion, or proceed.", actions);
    }

    private static ActionSurfaceSnapshot BuildShopActions(GameSnapshot snapshot)
    {
        var actions = new List<SceneActionSnapshot>();

        if (snapshot.Shop is not null)
        {
            foreach (ShopItemEntrySnapshot item in snapshot.Shop.Items)
            {
                actions.Add(new SceneActionSnapshot(
                    ActionType: "shop_purchase",
                    Index: item.Index,
                    Label: item.Title,
                    Description: $"{item.Category} for {item.Price} gold. {item.Description}".Trim(),
                    IsAvailable: item.CanAfford,
                    Parameters: [new ActionArgumentSnapshot("index", item.Index.ToString())],
                    TargetOptions: Array.Empty<string>(),
                    Tags: BuildTags(item.Category, item.CanAfford ? "affordable" : "too_expensive")));
            }

            if (snapshot.Shop.CanProceed)
            {
                actions.Add(new SceneActionSnapshot(
                    ActionType: "proceed",
                    Index: null,
                    Label: "Leave shop",
                    Description: "Proceed without buying more items.",
                    IsAvailable: true,
                    Parameters: Array.Empty<ActionArgumentSnapshot>(),
                    TargetOptions: Array.Empty<string>(),
                    Tags: ["proceed"]));
            }
        }

        AppendOutOfCombatPotionActions(actions, snapshot.Player);

        return new ActionSurfaceSnapshot(snapshot.Context.StateType, "Buy an item or leave the shop.", actions);
    }

    private static ActionSurfaceSnapshot BuildRestSiteActions(GameSnapshot snapshot)
    {
        var actions = new List<SceneActionSnapshot>();

        if (snapshot.RestSite is not null)
        {
            foreach (RestOptionEntrySnapshot option in snapshot.RestSite.Options)
            {
                actions.Add(new SceneActionSnapshot(
                    ActionType: "choose_rest_option",
                    Index: option.Index,
                    Label: option.Title,
                    Description: option.Description,
                    IsAvailable: option.IsEnabled,
                    Parameters: [new ActionArgumentSnapshot("index", option.Index.ToString())],
                    TargetOptions: Array.Empty<string>(),
                    Tags: BuildTags("rest_site", option.IsEnabled ? "enabled" : "disabled")));
            }

            if (snapshot.RestSite.CanProceed)
            {
                actions.Add(new SceneActionSnapshot(
                    ActionType: "proceed",
                    Index: null,
                    Label: "Leave rest site",
                    Description: "Proceed to the next screen.",
                    IsAvailable: true,
                    Parameters: Array.Empty<ActionArgumentSnapshot>(),
                    TargetOptions: Array.Empty<string>(),
                    Tags: ["proceed"]));
            }
        }

        AppendOutOfCombatPotionActions(actions, snapshot.Player);

        return new ActionSurfaceSnapshot(snapshot.Context.StateType, "Choose an enabled rest-site option.", actions);
    }

    private static ActionSurfaceSnapshot BuildRewardsActions(GameSnapshot snapshot)
    {
        var actions = new List<SceneActionSnapshot>();

        if (snapshot.Rewards is not null)
        {
            foreach (RewardEntrySnapshot item in snapshot.Rewards.Items)
            {
                actions.Add(new SceneActionSnapshot(
                    ActionType: "claim_reward",
                    Index: item.Index,
                    Label: item.Label,
                    Description: item.Description,
                    IsAvailable: true,
                    Parameters: [new ActionArgumentSnapshot("index", item.Index.ToString())],
                    TargetOptions: Array.Empty<string>(),
                    Tags: [item.Type]));
            }

            if (snapshot.Rewards.CanProceed)
            {
                actions.Add(new SceneActionSnapshot(
                    ActionType: "proceed",
                    Index: null,
                    Label: "Proceed",
                    Description: "Leave the rewards screen.",
                    IsAvailable: true,
                    Parameters: Array.Empty<ActionArgumentSnapshot>(),
                    TargetOptions: Array.Empty<string>(),
                    Tags: ["proceed"]));
            }
        }

        AppendOutOfCombatPotionActions(actions, snapshot.Player);

        return new ActionSurfaceSnapshot(snapshot.Context.StateType, "Claim a reward or proceed.", actions);
    }

    private static ActionSurfaceSnapshot BuildCardRewardActions(GameSnapshot snapshot)
    {
        var actions = new List<SceneActionSnapshot>();

        if (snapshot.CardReward is not null)
        {
            foreach (CardRewardEntrySnapshot card in snapshot.CardReward.Cards)
            {
                actions.Add(new SceneActionSnapshot(
                    ActionType: "select_card_reward",
                    Index: card.Index,
                    Label: card.Title,
                    Description: card.Description,
                    IsAvailable: true,
                    Parameters: [new ActionArgumentSnapshot("index", card.Index.ToString())],
                    TargetOptions: Array.Empty<string>(),
                    Tags: BuildTags("card_reward", card.Rarity.ToLowerInvariant(), card.Type.ToLowerInvariant())));
            }

            if (snapshot.CardReward.CanSkip)
            {
                actions.Add(new SceneActionSnapshot(
                    ActionType: "skip_card_reward",
                    Index: null,
                    Label: "Skip card reward",
                    Description: "Do not add any of the visible cards.",
                    IsAvailable: true,
                    Parameters: Array.Empty<ActionArgumentSnapshot>(),
                    TargetOptions: Array.Empty<string>(),
                    Tags: ["skip"]));
            }
        }

        AppendOutOfCombatPotionActions(actions, snapshot.Player);

        return new ActionSurfaceSnapshot(snapshot.Context.StateType, "Choose a card reward or skip.", actions);
    }

    private static ActionSurfaceSnapshot BuildCardSelectionActions(GameSnapshot snapshot)
    {
        var actions = new List<SceneActionSnapshot>();

        if (snapshot.CardSelection is not null)
        {
            foreach (CardRewardEntrySnapshot card in snapshot.CardSelection.Cards)
            {
                actions.Add(new SceneActionSnapshot(
                    ActionType: "select_card",
                    Index: card.Index,
                    Label: card.Title,
                    Description: card.Description,
                    IsAvailable: true,
                    Parameters: [new ActionArgumentSnapshot("index", card.Index.ToString())],
                    TargetOptions: Array.Empty<string>(),
                    Tags: BuildTags("card_selection", card.Rarity.ToLowerInvariant(), card.Type.ToLowerInvariant())));
            }

            if (snapshot.CardSelection.CanConfirm)
            {
                actions.Add(new SceneActionSnapshot(
                    ActionType: "confirm_selection",
                    Index: null,
                    Label: "Confirm selection",
                    Description: "Confirm the currently selected cards.",
                    IsAvailable: true,
                    Parameters: Array.Empty<ActionArgumentSnapshot>(),
                    TargetOptions: Array.Empty<string>(),
                    Tags: ["confirm"]));
            }

            if (snapshot.CardSelection.CanCancel)
            {
                actions.Add(new SceneActionSnapshot(
                    ActionType: snapshot.CardSelection.CanSkip ? "skip_selection" : "cancel_selection",
                    Index: null,
                    Label: snapshot.CardSelection.CanSkip ? "Skip selection" : "Cancel selection",
                    Description: snapshot.CardSelection.CanSkip ? "Skip the current selection screen." : "Back out of the current selection screen.",
                    IsAvailable: true,
                    Parameters: Array.Empty<ActionArgumentSnapshot>(),
                    TargetOptions: Array.Empty<string>(),
                    Tags: [snapshot.CardSelection.CanSkip ? "skip" : "cancel"]));
            }
        }

        AppendOutOfCombatPotionActions(actions, snapshot.Player);

        return new ActionSurfaceSnapshot(snapshot.Context.StateType, "Resolve the current card selection.", actions);
    }

    private static ActionSurfaceSnapshot BuildTreasureActions(GameSnapshot snapshot)
    {
        var actions = new List<SceneActionSnapshot>();

        if (snapshot.Treasure is not null)
        {
            foreach (RelicChoiceEntrySnapshot relic in snapshot.Treasure.Relics)
            {
                actions.Add(new SceneActionSnapshot(
                    ActionType: "claim_treasure_relic",
                    Index: relic.Index,
                    Label: relic.Title,
                    Description: relic.Description,
                    IsAvailable: true,
                    Parameters: [new ActionArgumentSnapshot("index", relic.Index.ToString())],
                    TargetOptions: Array.Empty<string>(),
                    Tags: BuildTags("relic", relic.Rarity.ToLowerInvariant())));
            }

            if (snapshot.Treasure.CanProceed)
            {
                actions.Add(new SceneActionSnapshot(
                    ActionType: "proceed",
                    Index: null,
                    Label: "Proceed",
                    Description: "Leave the treasure screen.",
                    IsAvailable: true,
                    Parameters: Array.Empty<ActionArgumentSnapshot>(),
                    TargetOptions: Array.Empty<string>(),
                    Tags: ["proceed"]));
            }
        }

        AppendOutOfCombatPotionActions(actions, snapshot.Player);

        return new ActionSurfaceSnapshot(snapshot.Context.StateType, "Claim a treasure relic or proceed.", actions);
    }

    private static ActionSurfaceSnapshot BuildBundleSelectionActions(GameSnapshot snapshot)
    {
        var actions = new List<SceneActionSnapshot>();

        if (snapshot.BundleSelection is not null)
        {
            foreach (CardBundleEntrySnapshot bundle in snapshot.BundleSelection.Bundles)
            {
                actions.Add(new SceneActionSnapshot(
                    ActionType: "select_bundle",
                    Index: bundle.Index,
                    Label: $"Bundle {bundle.Index}",
                    Description: $"Preview a bundle with {bundle.CardCount} cards.",
                    IsAvailable: !snapshot.BundleSelection.PreviewShowing,
                    Parameters: [new ActionArgumentSnapshot("index", bundle.Index.ToString())],
                    TargetOptions: Array.Empty<string>(),
                    Tags: ["bundle"]));
            }

            if (snapshot.BundleSelection.CanConfirm)
            {
                actions.Add(new SceneActionSnapshot(
                    ActionType: "confirm_bundle_selection",
                    Index: null,
                    Label: "Confirm bundle",
                    Description: "Confirm the currently previewed bundle.",
                    IsAvailable: true,
                    Parameters: Array.Empty<ActionArgumentSnapshot>(),
                    TargetOptions: Array.Empty<string>(),
                    Tags: ["confirm", "bundle"]));
            }

            if (snapshot.BundleSelection.CanCancel)
            {
                actions.Add(new SceneActionSnapshot(
                    ActionType: "cancel_bundle_selection",
                    Index: null,
                    Label: "Cancel bundle preview",
                    Description: "Cancel the current bundle preview.",
                    IsAvailable: true,
                    Parameters: Array.Empty<ActionArgumentSnapshot>(),
                    TargetOptions: Array.Empty<string>(),
                    Tags: ["cancel", "bundle"]));
            }
        }

        AppendOutOfCombatPotionActions(actions, snapshot.Player);

        return new ActionSurfaceSnapshot(snapshot.Context.StateType, "Resolve the current bundle selection.", actions);
    }

    private static ActionSurfaceSnapshot BuildRelicSelectionActions(GameSnapshot snapshot)
    {
        var actions = new List<SceneActionSnapshot>();

        if (snapshot.RelicSelection is not null)
        {
            foreach (RelicChoiceEntrySnapshot relic in snapshot.RelicSelection.Relics)
            {
                actions.Add(new SceneActionSnapshot(
                    ActionType: "select_relic",
                    Index: relic.Index,
                    Label: relic.Title,
                    Description: relic.Description,
                    IsAvailable: true,
                    Parameters: [new ActionArgumentSnapshot("index", relic.Index.ToString())],
                    TargetOptions: Array.Empty<string>(),
                    Tags: BuildTags("relic", relic.Rarity.ToLowerInvariant())));
            }

            if (snapshot.RelicSelection.CanSkip)
            {
                actions.Add(new SceneActionSnapshot(
                    ActionType: "skip_relic_selection",
                    Index: null,
                    Label: "Skip relic selection",
                    Description: "Do not choose any of the visible relics.",
                    IsAvailable: true,
                    Parameters: Array.Empty<ActionArgumentSnapshot>(),
                    TargetOptions: Array.Empty<string>(),
                    Tags: ["skip", "relic"]));
            }
        }

        AppendOutOfCombatPotionActions(actions, snapshot.Player);

        return new ActionSurfaceSnapshot(snapshot.Context.StateType, "Resolve the current relic choice.", actions);
    }

    private static ActionSurfaceSnapshot BuildCrystalSphereActions(GameSnapshot snapshot)
    {
        var actions = new List<SceneActionSnapshot>();

        if (snapshot.CrystalSphere is not null)
        {
            if (snapshot.CrystalSphere.CanUseBigTool)
            {
                actions.Add(new SceneActionSnapshot(
                    ActionType: "crystal_sphere_set_tool",
                    Index: null,
                    Label: "Use big tool",
                    Description: "Switch Crystal Sphere to the big divination tool.",
                    IsAvailable: true,
                    Parameters: [new ActionArgumentSnapshot("tool", "big")],
                    TargetOptions: Array.Empty<string>(),
                    Tags: ["crystal_sphere", "tool"]));
            }

            if (snapshot.CrystalSphere.CanUseSmallTool)
            {
                actions.Add(new SceneActionSnapshot(
                    ActionType: "crystal_sphere_set_tool",
                    Index: null,
                    Label: "Use small tool",
                    Description: "Switch Crystal Sphere to the small divination tool.",
                    IsAvailable: true,
                    Parameters: [new ActionArgumentSnapshot("tool", "small")],
                    TargetOptions: Array.Empty<string>(),
                    Tags: ["crystal_sphere", "tool"]));
            }

            foreach (CrystalSphereCellCoordSnapshot cell in snapshot.CrystalSphere.ClickableCells)
            {
                actions.Add(new SceneActionSnapshot(
                    ActionType: "crystal_sphere_click_cell",
                    Index: null,
                    Label: $"Cell ({cell.X}, {cell.Y})",
                    Description: "Reveal this Crystal Sphere cell.",
                    IsAvailable: true,
                    Parameters: [
                        new ActionArgumentSnapshot("x", cell.X.ToString()),
                        new ActionArgumentSnapshot("y", cell.Y.ToString())
                    ],
                    TargetOptions: Array.Empty<string>(),
                    Tags: ["crystal_sphere", "cell"]));
            }

            if (snapshot.CrystalSphere.CanProceed)
            {
                actions.Add(new SceneActionSnapshot(
                    ActionType: "crystal_sphere_proceed",
                    Index: null,
                    Label: "Proceed",
                    Description: "Finish the Crystal Sphere minigame.",
                    IsAvailable: true,
                    Parameters: Array.Empty<ActionArgumentSnapshot>(),
                    TargetOptions: Array.Empty<string>(),
                    Tags: ["crystal_sphere", "proceed"]));
            }
        }

        return new ActionSurfaceSnapshot(snapshot.Context.StateType, "Resolve the Crystal Sphere minigame.", actions);
    }

    private static void AppendOutOfCombatPotionActions(ICollection<SceneActionSnapshot> actions, PlayerStateSnapshot? player)
    {
        if (player is null)
        {
            return;
        }

        foreach (PotionEntrySnapshot potion in player.Potions)
        {
            if (potion.CanUse && !string.Equals(potion.Usage, "CombatOnly", StringComparison.OrdinalIgnoreCase))
            {
                bool requiresTarget = potion.TargetType.Equals("AnyEnemy", StringComparison.OrdinalIgnoreCase);
                var parameters = new List<ActionArgumentSnapshot> { new("index", potion.Slot.ToString()) };
                var targetOptions = Array.Empty<string>();

                if (requiresTarget)
                {
                    parameters.Add(new ActionArgumentSnapshot("target", "required"));
                }
                else if (potion.TargetType is "Self" or "AnyAlly" or "AnyPlayer")
                {
                    targetOptions = ["self"];
                }

                actions.Add(new SceneActionSnapshot(
                    ActionType: "use_potion",
                    Index: potion.Slot,
                    Label: potion.Title,
                    Description: potion.Description,
                    IsAvailable: !requiresTarget,
                    Parameters: parameters,
                    TargetOptions: targetOptions,
                    Tags: BuildTags("potion", "use", potion.Usage.ToLowerInvariant())));
            }

            if (potion.CanDiscard)
            {
                actions.Add(new SceneActionSnapshot(
                    ActionType: "discard_potion",
                    Index: potion.Slot,
                    Label: $"Discard {potion.Title}",
                    Description: "Discard this potion to free the slot.",
                    IsAvailable: true,
                    Parameters: [new ActionArgumentSnapshot("index", potion.Slot.ToString())],
                    TargetOptions: Array.Empty<string>(),
                    Tags: ["potion", "discard"]));
            }
        }
    }

    private static string[] BuildTags(params string[] values)
    {
        return values.Where(value => !string.IsNullOrWhiteSpace(value)).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
    }
}
