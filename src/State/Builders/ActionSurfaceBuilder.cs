using TakuAgentMod.State.Snapshots;

namespace TakuAgentMod.State.Builders;

internal static class ActionSurfaceBuilder
{
    public static ActionSurfaceSnapshot Build(GameSnapshot snapshot)
    {
        return snapshot.Context.StateType switch
        {
            "monster" or "elite" or "boss" => BuildCombatActions(snapshot),
            "map" => BuildMapActions(snapshot),
            "event" => BuildEventActions(snapshot),
            "shop" => BuildShopActions(snapshot),
            "rest_site" => BuildRestSiteActions(snapshot),
            "rewards" => BuildRewardsActions(snapshot),
            "card_reward" => BuildCardRewardActions(snapshot),
            "card_select" => BuildCardSelectionActions(snapshot),
            "treasure" => BuildTreasureActions(snapshot),
            _ => new ActionSurfaceSnapshot(snapshot.Context.StateType, snapshot.CompactObservation.Goal, Array.Empty<SceneActionSnapshot>())
        };
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
        SceneActionSnapshot[] actions = snapshot.Map?.NextOptions
            .Select(option => new SceneActionSnapshot(
                ActionType: "choose_map_node",
                Index: option.Index,
                Label: $"Node {option.Index}: {option.Type}",
                Description: $"Move to ({option.Col}, {option.Row}) and open {option.LeadsTo.Count} follow-up branches.",
                IsAvailable: true,
                Parameters: [new ActionArgumentSnapshot("index", option.Index.ToString())],
                TargetOptions: Array.Empty<string>(),
                Tags: [option.Type.ToLowerInvariant()]))
            .ToArray() ?? Array.Empty<SceneActionSnapshot>();

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

        return new ActionSurfaceSnapshot(snapshot.Context.StateType, "Choose one visible event option.", actions);
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

        return new ActionSurfaceSnapshot(snapshot.Context.StateType, "Claim a treasure relic or proceed.", actions);
    }

    private static string[] BuildTags(params string[] values)
    {
        return values.Where(value => !string.IsNullOrWhiteSpace(value)).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
    }
}
