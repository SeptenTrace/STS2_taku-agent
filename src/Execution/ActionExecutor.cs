using System.Text.Json;
using Godot;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Merchant;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Potions;
using MegaCrit.Sts2.Core.GameActions;
using MegaCrit.Sts2.Core.Map;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Nodes.Cards;
using MegaCrit.Sts2.Core.Nodes.Cards.Holders;
using MegaCrit.Sts2.Core.Nodes.CommonUi;
using MegaCrit.Sts2.Core.Nodes.Events;
using MegaCrit.Sts2.Core.Nodes.GodotExtensions;
using MegaCrit.Sts2.Core.Nodes.Rewards;
using MegaCrit.Sts2.Core.Nodes.RestSite;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using MegaCrit.Sts2.Core.Nodes.Screens;
using MegaCrit.Sts2.Core.Nodes.Screens.CardSelection;
using MegaCrit.Sts2.Core.Nodes.Screens.Map;
using MegaCrit.Sts2.Core.Nodes.Screens.Overlays;
using MegaCrit.Sts2.Core.Nodes.Screens.Shops;
using MegaCrit.Sts2.Core.Nodes.Screens.TreasureRoomRelic;
using MegaCrit.Sts2.Core.Rewards;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;
using TakuAgentMod.State.Support;

namespace TakuAgentMod.Execution;

internal static class ActionExecutor
{
    public static ActionExecutionOutcome Execute(string actionType, Dictionary<string, JsonElement> parameters)
    {
        if (!RunManager.Instance.IsInProgress)
        {
            return ActionExecutionOutcome.Fail("No run is active.");
        }

        RunState? runState = RunManager.Instance.DebugOnlyGetState();
        if (runState is null)
        {
            return ActionExecutionOutcome.Fail("Run state is unavailable.");
        }

        Player? player = LocalContext.GetMe(runState) ?? runState.Players.FirstOrDefault();
        if (player is null)
        {
            return ActionExecutionOutcome.Fail("Could not resolve the local player.");
        }

        try
        {
            return actionType switch
            {
                "play_card" => ExecutePlayCard(player, parameters),
                "use_potion" => ExecuteUsePotion(player, parameters),
                "discard_potion" => ExecuteDiscardPotion(player, parameters),
                "end_turn" => ExecuteEndTurn(player),
                "choose_map_node" => ExecuteChooseMapNode(parameters),
                "choose_event_option" => ExecuteChooseEventOption(parameters),
                "advance_dialogue" => ExecuteAdvanceDialogue(),
                "choose_rest_option" => ExecuteChooseRestOption(parameters),
                "shop_purchase" => ExecuteShopPurchase(player, parameters),
                "claim_reward" => ExecuteClaimReward(parameters),
                "select_card_reward" => ExecuteSelectCardReward(parameters),
                "skip_card_reward" => ExecuteSkipCardReward(),
                "proceed" => ExecuteProceed(),
                "select_card" => ExecuteSelectCard(parameters),
                "confirm_selection" => ExecuteConfirmSelection(),
                "cancel_selection" or "skip_selection" => ExecuteCancelSelection(),
                "claim_treasure_relic" => ExecuteClaimTreasureRelic(parameters),
                _ => ActionExecutionOutcome.Fail($"Unknown action type '{actionType}'.")
            };
        }
        catch (Exception ex)
        {
            return ActionExecutionOutcome.Fail($"Action '{actionType}' failed: {ex.Message}");
        }
    }

    private static ActionExecutionOutcome ExecutePlayCard(Player player, IReadOnlyDictionary<string, JsonElement> parameters)
    {
        if (!CombatManager.Instance.IsInProgress)
        {
            return ActionExecutionOutcome.Fail("Combat is not active.");
        }

        if (!CombatManager.Instance.IsPlayPhase)
        {
            return ActionExecutionOutcome.Fail("Not in player play phase.");
        }

        if (CombatManager.Instance.PlayerActionsDisabled)
        {
            return ActionExecutionOutcome.Fail("Player actions are currently disabled.");
        }

        if (!player.Creature.IsAlive)
        {
            return ActionExecutionOutcome.Fail("Player creature is dead.");
        }

        int cardIndex = GetRequiredInt(parameters, "index", "card_index");
        if (cardIndex < 0)
        {
            return ActionExecutionOutcome.Fail("Missing 'index'.");
        }

        var hand = player.PlayerCombatState?.Hand;
        if (hand is null)
        {
            return ActionExecutionOutcome.Fail("Player hand is unavailable.");
        }

        if (cardIndex >= hand.Cards.Count)
        {
            return ActionExecutionOutcome.Fail($"card_index {cardIndex} is out of range ({hand.Cards.Count} cards in hand).");
        }

        CardModel card = hand.Cards[cardIndex];
        card.CanPlay(out var reason, out _);
        if (reason != UnplayableReason.None)
        {
            return ActionExecutionOutcome.Fail($"Card '{ObservationText.SafeGetText(() => card.Title) ?? card.Id.Entry}' cannot be played: {reason}.");
        }

        Creature? target = null;
        if (card.TargetType == TargetType.AnyEnemy)
        {
            string? targetId = GetOptionalString(parameters, "target", "entity_id");
            if (string.IsNullOrWhiteSpace(targetId))
            {
                return ActionExecutionOutcome.Fail("Card requires a target. Provide 'target'.");
            }

            CombatState? combatState = player.Creature.CombatState;
            if (combatState is null)
            {
                return ActionExecutionOutcome.Fail("Combat state is unavailable for target resolution.");
            }

            target = ResolveTarget(combatState, targetId);
            if (target is null)
            {
                return ActionExecutionOutcome.Fail($"Target '{targetId}' is not a valid alive enemy.");
            }
        }

        RunManager.Instance.ActionQueueSynchronizer.RequestEnqueue(new PlayCardAction(card, target));

        string cardTitle = ObservationText.SafeGetText(() => card.Title) ?? card.Id.Entry;
        string message = target is null
            ? $"Queued play_card for '{cardTitle}'."
            : $"Queued play_card for '{cardTitle}' targeting '{ObservationText.SafeGetText(() => target.Monster?.Title) ?? targetIdOrFallback(target)}'.";

        return ActionExecutionOutcome.Ok(message);
    }

    private static ActionExecutionOutcome ExecuteUsePotion(Player player, IReadOnlyDictionary<string, JsonElement> parameters)
    {
        int slot = GetRequiredInt(parameters, "index", "slot");
        if (slot < 0)
        {
            return ActionExecutionOutcome.Fail("Missing 'index'.");
        }

        if (slot >= player.PotionSlots.Count)
        {
            return ActionExecutionOutcome.Fail($"slot {slot} is out of range ({player.PotionSlots.Count} potion slots).");
        }

        PotionModel? potion = player.GetPotionAtSlotIndex(slot);
        if (potion is null)
        {
            return ActionExecutionOutcome.Fail($"Potion slot {slot} is empty.");
        }

        if (potion.IsQueued)
        {
            return ActionExecutionOutcome.Fail($"Potion '{ObservationText.SafeGetText(() => potion.Title) ?? potion.Id.Entry}' is already queued.");
        }

        if (potion.Owner.Creature.IsDead)
        {
            return ActionExecutionOutcome.Fail("Player creature is dead.");
        }

        if (!potion.PassesCustomUsabilityCheck)
        {
            return ActionExecutionOutcome.Fail($"Potion '{ObservationText.SafeGetText(() => potion.Title) ?? potion.Id.Entry}' cannot be used right now.");
        }

        bool inCombat = CombatManager.Instance.IsInProgress;
        if (potion.Usage == PotionUsage.CombatOnly)
        {
            if (!inCombat)
            {
                return ActionExecutionOutcome.Fail($"Potion '{ObservationText.SafeGetText(() => potion.Title) ?? potion.Id.Entry}' can only be used in combat.");
            }

            if (!CombatManager.Instance.IsPlayPhase)
            {
                return ActionExecutionOutcome.Fail("Cannot use potions outside the player play phase.");
            }
        }
        else if (potion.Usage == PotionUsage.Automatic)
        {
            return ActionExecutionOutcome.Fail($"Potion '{ObservationText.SafeGetText(() => potion.Title) ?? potion.Id.Entry}' is automatic and cannot be manually used.");
        }

        if (inCombat && CombatManager.Instance.PlayerActionsDisabled)
        {
            return ActionExecutionOutcome.Fail("Player actions are currently disabled.");
        }

        Creature? target = potion.TargetType switch
        {
            TargetType.AnyEnemy => ResolvePotionTarget(player, parameters),
            TargetType.Self or TargetType.AnyAlly or TargetType.AnyPlayer => player.Creature,
            _ => null
        };

        if (potion.TargetType == TargetType.AnyEnemy && target is null)
        {
            return ActionExecutionOutcome.Fail("Potion requires a valid alive enemy target.");
        }

        potion.EnqueueManualUse(target);

        string potionTitle = ObservationText.SafeGetText(() => potion.Title) ?? potion.Id.Entry;
        string targetSuffix = potion.TargetType switch
        {
            TargetType.AnyEnemy => $" targeting '{ObservationText.SafeGetText(() => target?.Monster?.Title) ?? targetIdOrFallback(target)}'",
            TargetType.Self or TargetType.AnyAlly or TargetType.AnyPlayer => " on self",
            _ => string.Empty
        };

        return ActionExecutionOutcome.Ok($"Queued use_potion for '{potionTitle}' from slot {slot}{targetSuffix}.");
    }

    private static ActionExecutionOutcome ExecuteDiscardPotion(Player player, IReadOnlyDictionary<string, JsonElement> parameters)
    {
        int slot = GetRequiredInt(parameters, "index", "slot");
        if (slot < 0)
        {
            return ActionExecutionOutcome.Fail("Missing 'index'.");
        }

        if (slot >= player.PotionSlots.Count)
        {
            return ActionExecutionOutcome.Fail($"slot {slot} is out of range ({player.PotionSlots.Count} potion slots).");
        }

        PotionModel? potion = player.GetPotionAtSlotIndex(slot);
        if (potion is null)
        {
            return ActionExecutionOutcome.Fail($"Potion slot {slot} is empty.");
        }

        string potionTitle = ObservationText.SafeGetText(() => potion.Title) ?? potion.Id.Entry;
        _ = PotionCmd.Discard(potion);
        return ActionExecutionOutcome.Ok($"Queued discard_potion for '{potionTitle}' from slot {slot}.");
    }

    private static ActionExecutionOutcome ExecuteEndTurn(Player player)
    {
        if (!CombatManager.Instance.IsInProgress)
        {
            return ActionExecutionOutcome.Fail("Combat is not active.");
        }

        if (!CombatManager.Instance.IsPlayPhase)
        {
            return ActionExecutionOutcome.Fail("Not in player play phase.");
        }

        if (CombatManager.Instance.PlayerActionsDisabled)
        {
            return ActionExecutionOutcome.Fail("Player actions are currently disabled.");
        }

        NPlayerHand? hand = NCombatRoom.Instance?.Ui?.Hand;
        if (hand is not null && (hand.InCardPlay || hand.CurrentMode != NPlayerHand.Mode.Play))
        {
            return ActionExecutionOutcome.Fail("Cannot end turn while the hand is resolving another interaction.");
        }

        PlayerCmd.EndTurn(player, canBackOut: false);
        return ActionExecutionOutcome.Ok("Queued end_turn.");
    }

    private static ActionExecutionOutcome ExecuteChooseMapNode(IReadOnlyDictionary<string, JsonElement> parameters)
    {
        NMapScreen? mapScreen = NMapScreen.Instance;
        if (mapScreen is null || !mapScreen.IsOpen)
        {
            return ActionExecutionOutcome.Fail("Map screen is not open.");
        }

        int nodeIndex = GetRequiredInt(parameters, "node_index", "index");
        if (nodeIndex < 0)
        {
            return ActionExecutionOutcome.Fail("Missing 'node_index'.");
        }

        List<NMapPoint> travelable = GodotNodeSearch.FindAll<NMapPoint>(mapScreen)
            .Where(point => point.State == MapPointState.Travelable && point.Point is not null)
            .OrderBy(point => point.Point!.coord.col)
            .ToList();

        if (nodeIndex >= travelable.Count)
        {
            return ActionExecutionOutcome.Fail($"node_index {nodeIndex} is out of range ({travelable.Count} travelable nodes).");
        }

        NMapPoint selected = travelable[nodeIndex];
        mapScreen.OnMapPointSelectedLocally(selected);

        MapPoint point = selected.Point!;
        return ActionExecutionOutcome.Ok($"Selected map node {point.PointType} at ({point.coord.col},{point.coord.row}).");
    }

    private static ActionExecutionOutcome ExecuteChooseEventOption(IReadOnlyDictionary<string, JsonElement> parameters)
    {
        Node? eventRoom = NEventRoom.Instance;
        if (eventRoom is null)
        {
            return ActionExecutionOutcome.Fail("Event room is not open.");
        }

        int optionIndex = GetRequiredInt(parameters, "option_index", "index");
        if (optionIndex < 0)
        {
            return ActionExecutionOutcome.Fail("Missing 'option_index'.");
        }

        List<NEventOptionButton> buttons = GodotNodeSearch.FindAll<NEventOptionButton>(eventRoom);
        if (optionIndex >= buttons.Count)
        {
            return ActionExecutionOutcome.Fail($"option_index {optionIndex} is out of range ({buttons.Count} event options).");
        }

        NEventOptionButton button = buttons[optionIndex];
        if (button.Option.IsLocked)
        {
            return ActionExecutionOutcome.Fail($"Event option {optionIndex} is locked.");
        }

        string title = ObservationText.SafeGetText(() => button.Option.Title) ?? $"option_{optionIndex}";
        button.ForceClick();
        return ActionExecutionOutcome.Ok($"Selected event option '{title}'.");
    }

    private static ActionExecutionOutcome ExecuteAdvanceDialogue()
    {
        Node? eventRoom = NEventRoom.Instance;
        if (eventRoom is null)
        {
            return ActionExecutionOutcome.Fail("Event room is not open.");
        }

        NAncientEventLayout? layout = GodotNodeSearch.FindFirst<NAncientEventLayout>(eventRoom);
        NClickableControl? hitbox = layout?.GetNodeOrNull<NClickableControl>("%DialogueHitbox");
        if (hitbox is not { Visible: true, IsEnabled: true })
        {
            return ActionExecutionOutcome.Fail("Dialogue advance hitbox is not available.");
        }

        hitbox.ForceClick();
        return ActionExecutionOutcome.Ok("Advanced event dialogue.");
    }

    private static ActionExecutionOutcome ExecuteChooseRestOption(IReadOnlyDictionary<string, JsonElement> parameters)
    {
        NRestSiteRoom? restSiteRoom = NRestSiteRoom.Instance;
        if (restSiteRoom is null)
        {
            return ActionExecutionOutcome.Fail("Rest site is not open.");
        }

        int optionIndex = GetRequiredInt(parameters, "option_index", "index");
        if (optionIndex < 0)
        {
            return ActionExecutionOutcome.Fail("Missing 'option_index'.");
        }

        List<NRestSiteButton> buttons = GodotNodeSearch.FindAll<NRestSiteButton>(restSiteRoom);
        if (optionIndex >= buttons.Count)
        {
            return ActionExecutionOutcome.Fail($"option_index {optionIndex} is out of range ({buttons.Count} rest options).");
        }

        NRestSiteButton button = buttons[optionIndex];
        if (!button.Option.IsEnabled)
        {
            return ActionExecutionOutcome.Fail($"Rest option '{button.Option.OptionId}' is disabled.");
        }

        string title = ObservationText.SafeGetText(() => button.Option.Title) ?? button.Option.OptionId;
        button.ForceClick();
        return ActionExecutionOutcome.Ok($"Selected rest-site option '{title}'.");
    }

    private static ActionExecutionOutcome ExecuteShopPurchase(Player player, IReadOnlyDictionary<string, JsonElement> parameters)
    {
        if (player.RunState.CurrentRoom is not MerchantRoom merchantRoom)
        {
            return ActionExecutionOutcome.Fail("Shop is not active.");
        }

        NMerchantRoom? merchantUi = NMerchantRoom.Instance;
        if (merchantUi?.Inventory is not null && !merchantUi.Inventory.IsOpen)
        {
            merchantUi.OpenInventory();
        }

        MerchantInventory? inventory = merchantRoom.Inventory;
        if (inventory is null)
        {
            return ActionExecutionOutcome.Fail("Shop inventory is not ready.");
        }

        int itemIndex = GetRequiredInt(parameters, "item_index", "index");
        if (itemIndex < 0)
        {
            return ActionExecutionOutcome.Fail("Missing 'item_index'.");
        }

        List<dynamic> entries = BuildShopEntries(inventory);
        if (itemIndex >= entries.Count)
        {
            return ActionExecutionOutcome.Fail($"item_index {itemIndex} is out of range ({entries.Count} shop entries).");
        }

        dynamic entry = entries[itemIndex];
        if (!(bool)entry.IsStocked)
        {
            return ActionExecutionOutcome.Fail("Shop item is already sold out.");
        }

        if (!(bool)entry.EnoughGold)
        {
            return ActionExecutionOutcome.Fail($"Not enough gold for item_index {itemIndex}.");
        }

        _ = entry.OnTryPurchaseWrapper(inventory);
        return ActionExecutionOutcome.Ok($"Queued shop_purchase for item_index {itemIndex} at cost {entry.Cost}.");
    }

    private static ActionExecutionOutcome ExecuteClaimReward(IReadOnlyDictionary<string, JsonElement> parameters)
    {
        if (NOverlayStack.Instance?.Peek() is not NRewardsScreen rewardsScreen)
        {
            return ActionExecutionOutcome.Fail("Rewards screen is not open.");
        }

        int rewardIndex = GetRequiredInt(parameters, "reward_index", "index");
        if (rewardIndex < 0)
        {
            return ActionExecutionOutcome.Fail("Missing 'reward_index'.");
        }

        List<NRewardButton> buttons = GodotNodeSearch.FindAll<NRewardButton>(rewardsScreen)
            .Where(button => button.IsEnabled && button.Reward is not null)
            .ToList();

        if (rewardIndex >= buttons.Count)
        {
            return ActionExecutionOutcome.Fail($"reward_index {rewardIndex} is out of range ({buttons.Count} visible rewards).");
        }

        Reward reward = buttons[rewardIndex].Reward!;
        buttons[rewardIndex].ForceClick();
        return ActionExecutionOutcome.Ok($"Claimed reward '{DescribeReward(reward)}'.");
    }

    private static ActionExecutionOutcome ExecuteSelectCardReward(IReadOnlyDictionary<string, JsonElement> parameters)
    {
        if (NOverlayStack.Instance?.Peek() is not NCardRewardSelectionScreen cardRewardScreen)
        {
            return ActionExecutionOutcome.Fail("Card reward screen is not open.");
        }

        int cardIndex = GetRequiredInt(parameters, "card_index", "index");
        if (cardIndex < 0)
        {
            return ActionExecutionOutcome.Fail("Missing 'card_index'.");
        }

        List<NCardHolder> holders = GodotNodeSearch.FindAllSortedByPosition<NCardHolder>(cardRewardScreen);
        if (cardIndex >= holders.Count)
        {
            return ActionExecutionOutcome.Fail($"card_index {cardIndex} is out of range ({holders.Count} card rewards).");
        }

        NCardHolder holder = holders[cardIndex];
        string title = ObservationText.SafeGetText(() => holder.CardModel?.Title) ?? "unknown";
        holder.EmitSignal(NCardHolder.SignalName.Pressed, holder);
        return ActionExecutionOutcome.Ok($"Selected card reward '{title}'.");
    }

    private static ActionExecutionOutcome ExecuteSkipCardReward()
    {
        if (NOverlayStack.Instance?.Peek() is not NCardRewardSelectionScreen cardRewardScreen)
        {
            return ActionExecutionOutcome.Fail("Card reward screen is not open.");
        }

        List<NCardRewardAlternativeButton> buttons = GodotNodeSearch.FindAll<NCardRewardAlternativeButton>(cardRewardScreen);
        if (buttons.Count == 0)
        {
            return ActionExecutionOutcome.Fail("Card reward screen does not expose a skip action.");
        }

        buttons[0].ForceClick();
        return ActionExecutionOutcome.Ok("Skipped card reward.");
    }

    private static ActionExecutionOutcome ExecuteProceed()
    {
        if (NOverlayStack.Instance?.Peek() is NRewardsScreen rewardsScreen)
        {
            NProceedButton? button = GodotNodeSearch.FindFirst<NProceedButton>(rewardsScreen);
            if (button is { IsEnabled: true })
            {
                button.ForceClick();
                return ActionExecutionOutcome.Ok("Proceeded from rewards.");
            }
        }

        if (NRestSiteRoom.Instance is { } restSiteRoom && restSiteRoom.ProceedButton.IsEnabled)
        {
            restSiteRoom.ProceedButton.ForceClick();
            return ActionExecutionOutcome.Ok("Proceeded from rest site.");
        }

        if (NMerchantRoom.Instance is { } merchantRoom)
        {
            if (merchantRoom.Inventory?.IsOpen == true)
            {
                NBackButton? backButton = GodotNodeSearch.FindFirst<NBackButton>(merchantRoom);
                if (backButton is { IsEnabled: true })
                {
                    backButton.ForceClick();
                }
            }

            if (merchantRoom.ProceedButton.IsEnabled)
            {
                merchantRoom.ProceedButton.ForceClick();
                return ActionExecutionOutcome.Ok("Proceeded from shop.");
            }
        }

        NTreasureRoom? treasureRoom = GodotNodeSearch.FindFirst<NTreasureRoom>(((SceneTree)Engine.GetMainLoop()).Root);
        if (treasureRoom is not null && treasureRoom.ProceedButton.IsEnabled)
        {
            treasureRoom.ProceedButton.ForceClick();
            return ActionExecutionOutcome.Ok("Proceeded from treasure room.");
        }

        return ActionExecutionOutcome.Fail("No enabled proceed button is available.");
    }

    private static ActionExecutionOutcome ExecuteSelectCard(IReadOnlyDictionary<string, JsonElement> parameters)
    {
        int cardIndex = GetRequiredInt(parameters, "index", "card_index");
        if (cardIndex < 0)
        {
            return ActionExecutionOutcome.Fail("Missing 'index'.");
        }

        NPlayerHand? combatHand = NPlayerHand.Instance;
        if (combatHand is not null && combatHand.IsInCardSelection)
        {
            List<NHandCardHolder> holders = combatHand.ActiveHolders.ToList();
            if (cardIndex >= holders.Count)
            {
                return ActionExecutionOutcome.Fail($"index {cardIndex} is out of range ({holders.Count} selectable hand cards).");
            }

            NHandCardHolder holder = holders[cardIndex];
            string title = ObservationText.SafeGetText(() => holder.CardModel?.Title) ?? "unknown";
            holder.EmitSignal(NCardHolder.SignalName.Pressed, holder);
            return ActionExecutionOutcome.Ok($"Toggled in-combat card selection for '{title}'.");
        }

        object? overlay = NOverlayStack.Instance?.Peek();
        if (overlay is NCardGridSelectionScreen gridSelectionScreen)
        {
            NCardGrid? grid = GodotNodeSearch.FindFirst<NCardGrid>(gridSelectionScreen);
            if (grid is null)
            {
                return ActionExecutionOutcome.Fail("Card grid is unavailable.");
            }

            List<NGridCardHolder> holders = GodotNodeSearch.FindAllSortedByPosition<NGridCardHolder>(gridSelectionScreen);
            if (cardIndex >= holders.Count)
            {
                return ActionExecutionOutcome.Fail($"card_index {cardIndex} is out of range ({holders.Count} selectable cards).");
            }

            NGridCardHolder holder = holders[cardIndex];
            string title = ObservationText.SafeGetText(() => holder.CardModel?.Title) ?? "unknown";
            grid.EmitSignal(NCardGrid.SignalName.HolderPressed, holder);
            return ActionExecutionOutcome.Ok($"Toggled card selection for '{title}'.");
        }

        if (overlay is NChooseACardSelectionScreen chooseCardScreen)
        {
            List<NGridCardHolder> holders = GodotNodeSearch.FindAllSortedByPosition<NGridCardHolder>(chooseCardScreen);
            if (cardIndex >= holders.Count)
            {
                return ActionExecutionOutcome.Fail($"card_index {cardIndex} is out of range ({holders.Count} choices).");
            }

            NGridCardHolder holder = holders[cardIndex];
            string title = ObservationText.SafeGetText(() => holder.CardModel?.Title) ?? "unknown";
            holder.EmitSignal(NCardHolder.SignalName.Pressed, holder);
            return ActionExecutionOutcome.Ok($"Selected card '{title}'.");
        }

        return ActionExecutionOutcome.Fail("No card selection screen is open.");
    }

    private static ActionExecutionOutcome ExecuteConfirmSelection()
    {
        object? overlay = NOverlayStack.Instance?.Peek();
        NPlayerHand? combatHand = NPlayerHand.Instance;
        if (combatHand is not null && combatHand.IsInCardSelection)
        {
            NConfirmButton? confirmButton = combatHand.GetNodeOrNull<NConfirmButton>("%SelectModeConfirmButton");
            if (confirmButton is not { IsEnabled: true })
            {
                return ActionExecutionOutcome.Fail("In-combat selection cannot be confirmed yet.");
            }

            confirmButton.ForceClick();
            return ActionExecutionOutcome.Ok("Confirmed in-combat card selection.");
        }

        if (overlay is NChooseACardSelectionScreen)
        {
            return ActionExecutionOutcome.Fail("Current choose-a-card screen does not require confirmation.");
        }

        if (overlay is not NCardGridSelectionScreen screen)
        {
            return ActionExecutionOutcome.Fail("No card selection screen is open.");
        }

        foreach (string containerName in new[] { "%UpgradeSinglePreviewContainer", "%UpgradeMultiPreviewContainer", "%PreviewContainer" })
        {
            Control? container = screen.GetNodeOrNull<Control>(containerName);
            if (container?.Visible != true)
            {
                continue;
            }

            NConfirmButton? confirm = container.GetNodeOrNull<NConfirmButton>("Confirm")
                ?? container.GetNodeOrNull<NConfirmButton>("%PreviewConfirm");
            if (confirm is { IsEnabled: true })
            {
                confirm.ForceClick();
                return ActionExecutionOutcome.Ok("Confirmed card selection preview.");
            }
        }

        NConfirmButton? mainConfirm = screen.GetNodeOrNull<NConfirmButton>("Confirm")
            ?? screen.GetNodeOrNull<NConfirmButton>("%Confirm");
        if (mainConfirm is { IsEnabled: true })
        {
            mainConfirm.ForceClick();
            return ActionExecutionOutcome.Ok("Confirmed card selection.");
        }

        foreach (NConfirmButton confirm in GodotNodeSearch.FindAll<NConfirmButton>(screen))
        {
            if (!confirm.IsEnabled || !confirm.IsVisibleInTree())
            {
                continue;
            }

            confirm.ForceClick();
            return ActionExecutionOutcome.Ok("Confirmed card selection.");
        }

        return ActionExecutionOutcome.Fail("No enabled confirm button is available.");
    }

    private static ActionExecutionOutcome ExecuteCancelSelection()
    {
        object? overlay = NOverlayStack.Instance?.Peek();
        if (NPlayerHand.Instance is { IsInCardSelection: true })
        {
            return ActionExecutionOutcome.Fail("In-combat card selection cannot be cancelled from the current UI.");
        }

        if (overlay is NChooseACardSelectionScreen chooseCardScreen)
        {
            NClickableControl? skipButton = chooseCardScreen.GetNodeOrNull<NClickableControl>("SkipButton");
            if (skipButton is not { IsEnabled: true })
            {
                return ActionExecutionOutcome.Fail("No skip option is available on this choose-a-card screen.");
            }

            skipButton.ForceClick();
            return ActionExecutionOutcome.Ok("Skipped current card selection.");
        }

        if (overlay is not NCardGridSelectionScreen screen)
        {
            return ActionExecutionOutcome.Fail("No card selection screen is open.");
        }

        foreach (string containerName in new[] { "%UpgradeSinglePreviewContainer", "%UpgradeMultiPreviewContainer", "%PreviewContainer" })
        {
            Control? container = screen.GetNodeOrNull<Control>(containerName);
            if (container?.Visible != true)
            {
                continue;
            }

            NBackButton? cancel = container.GetNodeOrNull<NBackButton>("Cancel")
                ?? container.GetNodeOrNull<NBackButton>("%PreviewCancel");
            if (cancel is { IsEnabled: true })
            {
                cancel.ForceClick();
                return ActionExecutionOutcome.Ok("Cancelled card selection preview.");
            }
        }

        NBackButton? closeButton = screen.GetNodeOrNull<NBackButton>("%Close");
        if (closeButton is { IsEnabled: true })
        {
            closeButton.ForceClick();
            return ActionExecutionOutcome.Ok("Closed card selection screen.");
        }

        return ActionExecutionOutcome.Fail("No enabled cancel or close control is available.");
    }

    private static ActionExecutionOutcome ExecuteClaimTreasureRelic(IReadOnlyDictionary<string, JsonElement> parameters)
    {
        NTreasureRoom? treasureRoom = GodotNodeSearch.FindFirst<NTreasureRoom>(((SceneTree)Engine.GetMainLoop()).Root);
        if (treasureRoom is null)
        {
            return ActionExecutionOutcome.Fail("Treasure room is not open.");
        }

        NTreasureRoomRelicCollection? relicCollection = treasureRoom.GetNodeOrNull<NTreasureRoomRelicCollection>("%RelicCollection");
        if (relicCollection?.Visible != true)
        {
            return ActionExecutionOutcome.Fail("Treasure relic collection is not visible.");
        }

        int relicIndex = GetRequiredInt(parameters, "relic_index", "index");
        if (relicIndex < 0)
        {
            return ActionExecutionOutcome.Fail("Missing 'relic_index'.");
        }

        List<NTreasureRoomRelicHolder> holders = GodotNodeSearch.FindAll<NTreasureRoomRelicHolder>(relicCollection)
            .Where(holder => holder.IsEnabled && holder.Visible)
            .ToList();

        if (relicIndex >= holders.Count)
        {
            return ActionExecutionOutcome.Fail($"relic_index {relicIndex} is out of range ({holders.Count} relics).");
        }

        NTreasureRoomRelicHolder holder = holders[relicIndex];
        string title = ObservationText.SafeGetText(() => holder.Relic?.Model?.Title) ?? "unknown";
        holder.ForceClick();
        return ActionExecutionOutcome.Ok($"Claimed treasure relic '{title}'.");
    }

    private static Creature? ResolvePotionTarget(Player player, IReadOnlyDictionary<string, JsonElement> parameters)
    {
        string? targetId = GetOptionalString(parameters, "target", "entity_id");
        if (string.IsNullOrWhiteSpace(targetId))
        {
            return null;
        }

        CombatState? combatState = player.Creature.CombatState;
        return combatState is null ? null : ResolveTarget(combatState, targetId);
    }

    private static Creature? ResolveTarget(CombatState combatState, string entityId)
    {
        var entityCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (Creature creature in combatState.Enemies)
        {
            if (!creature.IsAlive)
            {
                continue;
            }

            string baseId = creature.Monster?.Id.Entry ?? "unknown";
            entityCounts.TryGetValue(baseId, out int seenCount);
            entityCounts[baseId] = seenCount + 1;

            string generatedId = $"{baseId}_{seenCount}";
            if (string.Equals(generatedId, entityId, StringComparison.OrdinalIgnoreCase))
            {
                return creature;
            }
        }

        return null;
    }

    private static List<dynamic> BuildShopEntries(MerchantInventory inventory)
    {
        var entries = new List<dynamic>();

        foreach (dynamic entry in inventory.CardEntries)
        {
            entries.Add(entry);
        }

        foreach (dynamic entry in inventory.RelicEntries)
        {
            entries.Add(entry);
        }

        foreach (dynamic entry in inventory.PotionEntries)
        {
            entries.Add(entry);
        }

        if (inventory.CardRemovalEntry is { } removalEntry)
        {
            entries.Add(removalEntry);
        }

        return entries;
    }

    private static string DescribeReward(Reward reward)
    {
        return reward switch
        {
            GoldReward goldReward => $"{goldReward.Amount} gold",
            PotionReward potionReward => ObservationText.SafeGetText(() => potionReward.Potion?.Title) ?? "Potion reward",
            CardReward => "Card reward",
            RelicReward => "Relic reward",
            _ => reward.GetType().Name
        };
    }

    private static int GetRequiredInt(IReadOnlyDictionary<string, JsonElement> parameters, params string[] names)
    {
        foreach (string name in names)
        {
            if (!parameters.TryGetValue(name, out JsonElement value))
            {
                continue;
            }

            if (value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out int number))
            {
                return number;
            }

            if (value.ValueKind == JsonValueKind.String && int.TryParse(value.GetString(), out number))
            {
                return number;
            }
        }

        return -1;
    }

    private static string? GetOptionalString(IReadOnlyDictionary<string, JsonElement> parameters, params string[] names)
    {
        foreach (string name in names)
        {
            if (!parameters.TryGetValue(name, out JsonElement value))
            {
                continue;
            }

            return value.ValueKind == JsonValueKind.String ? value.GetString() : value.ToString();
        }

        return null;
    }

    private static string targetIdOrFallback(Creature? creature)
    {
        return creature?.Monster?.Id.Entry ?? creature?.Name ?? "target";
    }
}

internal sealed record ActionExecutionOutcome(
    bool Success,
    string Message)
{
    public static ActionExecutionOutcome Ok(string message) => new(true, message);

    public static ActionExecutionOutcome Fail(string message) => new(false, message);
}
