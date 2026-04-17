using Godot;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Merchant;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Potions;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Map;
using MegaCrit.Sts2.Core.Nodes.Cards.Holders;
using MegaCrit.Sts2.Core.Nodes.CommonUi;
using MegaCrit.Sts2.Core.Nodes.Events;
using MegaCrit.Sts2.Core.Nodes.GodotExtensions;
using MegaCrit.Sts2.Core.Nodes.Relics;
using MegaCrit.Sts2.Core.Nodes.Rewards;
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
using TakuAgentMod.State.Snapshots;
using TakuAgentMod.State.Support;

namespace TakuAgentMod.State.Builders;

internal sealed class GameSnapshotBuilder
{
    public GameSnapshot Build()
    {
        DateTimeOffset timestamp = DateTimeOffset.Now;

        if (!RunManager.Instance.IsInProgress)
        {
            ContextSnapshot menuContext = BuildContext("menu", null, null);
            return new GameSnapshot(
                Timestamp: timestamp,
                Context: menuContext,
                Run: null,
                Player: null,
                CompactObservation: BuildCompactObservation(menuContext, null, null, null, null, null, null, null, null, null, null, null),
                Combat: null,
                Map: null,
                Rewards: null,
                CardReward: null,
                Event: null,
                Shop: null,
                RestSite: null,
                Treasure: null,
                CardSelection: null,
                Overlay: null);
        }

        RunState? runState = RunManager.Instance.DebugOnlyGetState();
        if (runState is null)
        {
            ContextSnapshot unknownContext = BuildContext("unknown", null, null);
            return new GameSnapshot(
                Timestamp: timestamp,
                Context: unknownContext,
                Run: null,
                Player: null,
                CompactObservation: BuildCompactObservation(unknownContext, null, null, null, null, null, null, null, null, null, null, null),
                Combat: null,
                Map: null,
                Rewards: null,
                CardReward: null,
                Event: null,
                Shop: null,
                RestSite: null,
                Treasure: null,
                CardSelection: null,
                Overlay: null);
        }

        Player? player = LocalContext.GetMe(runState) ?? runState.Players.FirstOrDefault();
        RunSnapshot run = BuildRunSnapshot(runState);
        PlayerStateSnapshot? playerState = player is null ? null : BuildPlayerState(player);

        object? topOverlay = NOverlayStack.Instance?.Peek();
        AbstractRoom? currentRoom = runState.CurrentRoom;
        bool mapIsOpen = NMapScreen.Instance is { IsOpen: true };

        string stateType = "unknown";
        CardSelectionStateSnapshot? cardSelection = null;
        CardRewardStateSnapshot? cardReward = null;
        RewardsStateSnapshot? rewards = null;
        CombatStateSnapshot? combat = null;
        MapStateSnapshot? map = null;
        EventStateSnapshot? eventState = null;
        ShopStateSnapshot? shop = null;
        RestSiteStateSnapshot? restSite = null;
        TreasureStateSnapshot? treasure = null;
        OverlayStateSnapshot? overlay = null;

        if (topOverlay is NCardGridSelectionScreen gridSelection)
        {
            stateType = "card_select";
            cardSelection = BuildCardGridSelectionState(gridSelection);
        }
        else if (topOverlay is NChooseACardSelectionScreen chooseCardSelection)
        {
            stateType = "card_select";
            cardSelection = BuildChooseCardSelectionState(chooseCardSelection);
        }
        else if (!mapIsOpen && topOverlay is NCardRewardSelectionScreen cardRewardSelection)
        {
            stateType = "card_reward";
            cardReward = BuildCardRewardState(cardRewardSelection);
        }
        else if (!mapIsOpen && topOverlay is NRewardsScreen rewardsScreen)
        {
            stateType = "rewards";
            rewards = BuildRewardsState(rewardsScreen);
        }
        else if (currentRoom is CombatRoom combatRoom && CombatManager.Instance.IsInProgress)
        {
            stateType = combatRoom.RoomType.ToString().ToLowerInvariant();
            combat = BuildCombatState(runState, combatRoom);
        }
        else if (mapIsOpen || currentRoom is MapRoom)
        {
            stateType = "map";
            map = BuildMapState(runState);
        }
        else if (currentRoom is EventRoom eventRoom)
        {
            stateType = "event";
            eventState = BuildEventState(eventRoom);
        }
        else if (currentRoom is MerchantRoom merchantRoom)
        {
            stateType = "shop";
            shop = BuildShopState(merchantRoom);
        }
        else if (currentRoom is RestSiteRoom restSiteRoom)
        {
            stateType = "rest_site";
            restSite = BuildRestSiteState(restSiteRoom);
        }
        else if (currentRoom is TreasureRoom treasureRoom)
        {
            stateType = "treasure";
            treasure = BuildTreasureState(treasureRoom);
        }
        else if (topOverlay is IOverlayScreen overlayScreen)
        {
            stateType = "overlay";
            overlay = new OverlayStateSnapshot(
                ScreenType: overlayScreen.GetType().Name,
                Message: $"Unhandled overlay: {overlayScreen.GetType().Name}");
        }

        ContextSnapshot context = BuildContext(stateType, currentRoom?.RoomType.ToString(), topOverlay?.GetType().Name);
        CompactObservationSnapshot compactObservation = BuildCompactObservation(
            context,
            playerState,
            combat,
            map,
            rewards,
            cardReward,
            eventState,
            shop,
            restSite,
            treasure,
            cardSelection,
            overlay);

        return new GameSnapshot(
            Timestamp: timestamp,
            Context: context,
            Run: run,
            Player: playerState,
            CompactObservation: compactObservation,
            Combat: combat,
            Map: map,
            Rewards: rewards,
            CardReward: cardReward,
            Event: eventState,
            Shop: shop,
            RestSite: restSite,
            Treasure: treasure,
            CardSelection: cardSelection,
            Overlay: overlay);
    }

    private static ContextSnapshot BuildContext(string stateType, string? roomType, string? overlayType)
    {
        return new ContextSnapshot(
            StateType: stateType,
            RoomType: roomType,
            OverlayType: overlayType,
            RecommendedQueries: GetRecommendedQueries(stateType));
    }

    private static RunSnapshot BuildRunSnapshot(RunState runState)
    {
        MapCoordSnapshot? currentMapCoord = null;
        if (runState.CurrentMapCoord is { } coord)
        {
            currentMapCoord = new MapCoordSnapshot(
                Col: coord.col,
                Row: coord.row,
                Type: runState.CurrentMapPoint?.PointType.ToString());
        }

        return new RunSnapshot(
            Act: runState.CurrentActIndex + 1,
            Floor: runState.TotalFloor,
            Ascension: runState.AscensionLevel,
            RoomType: runState.CurrentRoom?.RoomType.ToString(),
            CurrentMapCoord: currentMapCoord);
    }

    private static PlayerStateSnapshot BuildPlayerState(Player player)
    {
        Creature creature = player.Creature;
        PlayerCombatState? combatState = player.PlayerCombatState;

        int? energy = null;
        int? maxEnergy = null;
        int? stars = null;
        if (combatState is not null && CombatManager.Instance.IsInProgress)
        {
            energy = combatState.Energy;
            maxEnergy = combatState.MaxEnergy;
            stars = combatState.Stars;
        }

        return new PlayerStateSnapshot(
            CharacterId: player.Character.Id.Entry,
            Character: ObservationText.SafeGetText(() => player.Character.Title) ?? player.Character.Id.Entry,
            CurrentHp: creature.CurrentHp,
            MaxHp: creature.MaxHp,
            Block: creature.Block,
            Gold: player.Gold,
            Energy: energy,
            MaxEnergy: maxEnergy,
            Stars: stars,
            DeckCount: player.Deck.Cards.Count,
            Status: BuildStatusEntries(creature),
            Relics: BuildRelicEntries(player),
            Potions: BuildPotionEntries(player),
            Deck: BuildDeckSummary(player.Deck.Cards));
    }

    private static DeckSummarySnapshot BuildDeckSummary(IEnumerable<CardModel> cards)
    {
        DeckCardEntrySnapshot[] entries = cards
            .GroupBy(card => card.Id.Entry)
            .OrderBy(group => group.Key)
            .Select(group =>
            {
                CardModel representative = group.First();
                return new DeckCardEntrySnapshot(
                    Id: representative.Id.Entry,
                    Title: ObservationText.SafeGetText(() => representative.Title) ?? representative.Id.Entry,
                    Copies: group.Count(),
                    UpgradedCopies: group.Count(card => card.IsUpgraded),
                    Type: representative.Type.ToString(),
                    Rarity: representative.Rarity.ToString(),
                    Cost: GetCostDisplay(representative),
                    StarCost: GetStarCostDisplay(representative),
                    Description: ObservationText.SafeGetCardDescription(representative, PileType.Draw) ?? string.Empty);
            })
            .ToArray();

        return new DeckSummarySnapshot(entries.Sum(entry => entry.Copies), entries);
    }

    private static IReadOnlyList<RelicEntrySnapshot> BuildRelicEntries(Player player)
    {
        return player.Relics
            .Select(relic => new RelicEntrySnapshot(
                Id: relic.Id.Entry,
                    Title: ObservationText.SafeGetText(() => relic.Title) ?? relic.Id.Entry,
                    Description: ObservationText.SafeGetText(() => relic.DynamicDescription)
                        ?? string.Empty,
                Counter: relic.ShowCounter ? relic.DisplayAmount : null,
                Rarity: relic.Rarity.ToString()))
            .ToArray();
    }

    private static IReadOnlyList<PotionEntrySnapshot> BuildPotionEntries(Player player)
    {
        var potions = new List<PotionEntrySnapshot>();
        int slot = 0;
        foreach (PotionModel? potion in player.PotionSlots)
        {
            if (potion is not null)
            {
                potions.Add(new PotionEntrySnapshot(
                    Id: potion.Id.Entry,
                    Title: ObservationText.SafeGetText(() => potion.Title) ?? potion.Id.Entry,
                    Description: ObservationText.SafeGetText(() => potion.DynamicDescription)
                        ?? string.Empty,
                    Slot: slot,
                    TargetType: potion.TargetType.ToString(),
                    Usage: potion.Usage.ToString()));
            }

            slot++;
        }

        return potions;
    }

    private static IReadOnlyList<StatusEntrySnapshot> BuildStatusEntries(Creature creature)
    {
        var statuses = new List<StatusEntrySnapshot>();
        foreach (PowerModel power in creature.Powers)
        {
            if (!power.IsVisible)
            {
                continue;
            }

            statuses.Add(new StatusEntrySnapshot(
                Id: power.Id.Entry,
                Title: ObservationText.SafeGetText(() => power.Title) ?? power.Id.Entry,
                Description: ObservationText.SafeGetText(() => power.SmartDescription)
                    ?? ObservationText.SafeGetText(() => power.Description)
                    ?? string.Empty,
                Amount: power.DisplayAmount,
                Category: power.Type.ToString()));
        }

        return statuses;
    }

    private static CombatStateSnapshot? BuildCombatState(RunState runState, CombatRoom combatRoom)
    {
        CombatState? combatState = CombatManager.Instance.DebugOnlyGetState();
        if (combatState is null)
        {
            return null;
        }

        Player? player = LocalContext.GetMe(runState) ?? combatState.Players.FirstOrDefault();
        PlayerCombatState? playerCombatState = player?.PlayerCombatState;
        if (playerCombatState is null)
        {
            EnemyStateEntrySnapshot[] enemies = BuildEnemies(combatState).ToArray();
            return new CombatStateSnapshot(
                RoomType: combatRoom.RoomType.ToString().ToLowerInvariant(),
                Round: combatState.RoundNumber,
                Side: combatState.CurrentSide.ToString().ToLowerInvariant(),
                Piles: new PileCountsSnapshot(0, 0, 0),
                PileDetails: new PileDetailsSnapshot(Array.Empty<PileCardEntrySnapshot>(), Array.Empty<PileCardEntrySnapshot>(), Array.Empty<PileCardEntrySnapshot>()),
                Hand: Array.Empty<HandCardEntrySnapshot>(),
                Enemies: enemies,
                AvailableActions: [new CombatActionSnapshot("end_turn", null, null, null, false, Array.Empty<string>())]);
        }

        EnemyStateEntrySnapshot[] builtEnemies = BuildEnemies(combatState).ToArray();
        return new CombatStateSnapshot(
            RoomType: combatRoom.RoomType.ToString().ToLowerInvariant(),
            Round: combatState.RoundNumber,
            Side: combatState.CurrentSide.ToString().ToLowerInvariant(),
            Piles: new PileCountsSnapshot(
                Draw: playerCombatState.DrawPile.Cards.Count,
                Discard: playerCombatState.DiscardPile.Cards.Count,
                Exhaust: playerCombatState.ExhaustPile.Cards.Count),
            PileDetails: new PileDetailsSnapshot(
                DrawPile: BuildPileCards(playerCombatState.DrawPile.Cards, PileType.Draw),
                DiscardPile: BuildPileCards(playerCombatState.DiscardPile.Cards, PileType.Discard),
                ExhaustPile: BuildPileCards(playerCombatState.ExhaustPile.Cards, PileType.Exhaust)),
            Hand: BuildHand(playerCombatState.Hand.Cards, builtEnemies.Select(enemy => enemy.EntityId).ToArray()),
            Enemies: builtEnemies,
            AvailableActions: BuildAvailableActions(playerCombatState.Hand.Cards, builtEnemies.Select(enemy => enemy.EntityId).ToArray()));
    }

    private static IReadOnlyList<HandCardEntrySnapshot> BuildHand(IReadOnlyList<CardModel> cards, string[] enemyIds)
    {
        var hand = new List<HandCardEntrySnapshot>();
        for (int index = 0; index < cards.Count; index++)
        {
            CardModel card = cards[index];
            card.CanPlay(out var unplayableReason, out _);
            string[] legalTargets = BuildLegalTargets(card, enemyIds);

            hand.Add(new HandCardEntrySnapshot(
                Index: index,
                Id: card.Id.Entry,
                Title: ObservationText.SafeGetText(() => card.Title) ?? card.Id.Entry,
                Description: ObservationText.SafeGetCardDescription(card, PileType.Hand) ?? string.Empty,
                Type: card.Type.ToString(),
                Rarity: card.Rarity.ToString(),
                TargetType: card.TargetType.ToString(),
                Cost: GetCostDisplay(card),
                StarCost: GetStarCostDisplay(card),
                CanPlay: unplayableReason == UnplayableReason.None,
                IsUpgraded: card.IsUpgraded,
                LegalTargets: legalTargets));
        }

        return hand;
    }

    private static PileCardEntrySnapshot[] BuildPileCards(IEnumerable<CardModel> cards, PileType pileType)
    {
        return cards
            .Select(card => new PileCardEntrySnapshot(
                Id: card.Id.Entry,
                Title: ObservationText.SafeGetText(() => card.Title) ?? card.Id.Entry,
                Description: ObservationText.SafeGetCardDescription(card, pileType) ?? string.Empty,
                Type: card.Type.ToString(),
                Rarity: card.Rarity.ToString(),
                Cost: GetCostDisplay(card),
                StarCost: GetStarCostDisplay(card),
                IsUpgraded: card.IsUpgraded))
            .ToArray();
    }

    private static IReadOnlyList<EnemyStateEntrySnapshot> BuildEnemies(CombatState combatState)
    {
        var entityCounts = new Dictionary<string, int>();
        var enemies = new List<EnemyStateEntrySnapshot>();

        foreach (Creature creature in combatState.Enemies)
        {
            if (!creature.IsAlive)
            {
                continue;
            }

            string baseId = creature.Monster?.Id.Entry ?? creature.ModelId.ToString();
            entityCounts.TryGetValue(baseId, out int count);
            entityCounts[baseId] = count + 1;
            string entityId = $"{baseId}_{count}";

            enemies.Add(new EnemyStateEntrySnapshot(
                EntityId: entityId,
                Title: ObservationText.SafeGetText(() => creature.Monster?.Title) ?? creature.Name,
                CurrentHp: creature.CurrentHp,
                MaxHp: creature.MaxHp,
                Block: creature.Block,
                IsAlive: creature.IsAlive,
                Status: BuildStatusEntries(creature),
                Intents: BuildIntents(creature, out int? incomingDamage),
                IncomingDamage: incomingDamage));
        }

        return enemies;
    }

    private static IReadOnlyList<IntentEntrySnapshot> BuildIntents(Creature creature, out int? incomingDamage)
    {
        var intents = new List<IntentEntrySnapshot>();
        int damageTotal = 0;
        if (creature.Monster?.NextMove is not MegaCrit.Sts2.Core.MonsterMoves.MonsterMoveStateMachine.MoveState moveState)
        {
            incomingDamage = null;
            return intents;
        }

        IEnumerable<Creature> targets = creature.CombatState?.PlayerCreatures ?? Array.Empty<Creature>();
        foreach (object intent in moveState.Intents)
        {
            string? label = null;
            string? description = null;

            try
            {
                dynamic dynamicIntent = intent;
                label = ObservationText.ToReadableText(dynamicIntent.GetIntentLabel(targets, creature));
                dynamic hoverTip = dynamicIntent.GetHoverTip(targets, creature);
                description = ObservationText.ToReadableText(hoverTip.Description);
            }
            catch
            {
            }

            string intentType = (string?)intent.GetType().GetProperty("IntentType")?.GetValue(intent)?.ToString() ?? intent.GetType().Name;
            bool isAttack = string.Equals(intentType, "Attack", StringComparison.OrdinalIgnoreCase);
            int? expectedValue = TryParseNumericLabel(label);
            if (isAttack && expectedValue.HasValue)
            {
                damageTotal += expectedValue.Value;
            }

            intents.Add(new IntentEntrySnapshot(
                Type: intentType,
                Label: label,
                Description: description,
                IsAttack: isAttack,
                ExpectedValue: expectedValue));
        }

        incomingDamage = damageTotal > 0 ? damageTotal : null;
        return intents;
    }

    private static IReadOnlyList<CombatActionSnapshot> BuildAvailableActions(IReadOnlyList<CardModel> cards, string[] enemyIds)
    {
        var actions = new List<CombatActionSnapshot>();

        for (int index = 0; index < cards.Count; index++)
        {
            CardModel card = cards[index];
            card.CanPlay(out var unplayableReason, out _);
            if (unplayableReason != UnplayableReason.None)
            {
                continue;
            }

            string[] targetOptions = BuildLegalTargets(card, enemyIds);
            actions.Add(new CombatActionSnapshot(
                ActionType: "play_card",
                CardIndex: index,
                CardId: card.Id.Entry,
                CardTitle: ObservationText.SafeGetText(() => card.Title) ?? card.Id.Entry,
                RequiresTarget: card.TargetType == TargetType.AnyEnemy,
                TargetOptions: targetOptions));
        }

        actions.Add(new CombatActionSnapshot(
            ActionType: "end_turn",
            CardIndex: null,
            CardId: null,
            CardTitle: null,
            RequiresTarget: false,
            TargetOptions: Array.Empty<string>()));

        return actions;
    }

    private static string[] BuildLegalTargets(CardModel card, IReadOnlyList<string> enemyIds)
    {
        return card.TargetType switch
        {
            TargetType.AnyEnemy => enemyIds.ToArray(),
            TargetType.Self or TargetType.AnyAlly or TargetType.AnyPlayer => ["self"],
            _ => Array.Empty<string>()
        };
    }

    private static int? TryParseNumericLabel(string? label)
    {
        if (string.IsNullOrWhiteSpace(label))
        {
            return null;
        }

        return int.TryParse(label, out int value) ? value : null;
    }

    private static MapStateSnapshot BuildMapState(RunState runState)
    {
        MapCoordSnapshot? currentPosition = null;
        if (runState.CurrentMapCoord is { } currentCoord)
        {
            currentPosition = new MapCoordSnapshot(
                Col: currentCoord.col,
                Row: currentCoord.row,
                Type: runState.CurrentMapPoint?.PointType.ToString());
        }

        var nextOptions = new List<MapOptionSnapshot>();
        NMapScreen? mapScreen = NMapScreen.Instance;
        if (mapScreen is not null)
        {
            List<NMapPoint> travelable = GodotNodeSearch.FindAll<NMapPoint>(mapScreen)
                .Where(point => point.State == MapPointState.Travelable && point.Point is not null)
                .OrderBy(point => point.Point!.coord.col)
                .ToList();

            int index = 0;
            foreach (NMapPoint mapPoint in travelable)
            {
                MapPoint point = mapPoint.Point!;
                nextOptions.Add(new MapOptionSnapshot(
                    Index: index,
                    Col: point.coord.col,
                    Row: point.coord.row,
                    Type: point.PointType.ToString(),
                    LeadsTo: point.Children
                        .OrderBy(child => child.coord.col)
                        .Select(child => new MapCoordSnapshot(child.coord.col, child.coord.row, child.PointType.ToString()))
                        .ToArray()));
                index++;
            }
        }

        MapCoordSnapshot? boss = null;
        if (runState.Map?.BossMapPoint is { } bossPoint)
        {
            boss = new MapCoordSnapshot(
                Col: bossPoint.coord.col,
                Row: bossPoint.coord.row,
                Type: bossPoint.PointType.ToString());
        }

        return new MapStateSnapshot(
            CurrentPosition: currentPosition,
            NextOptions: nextOptions,
            Boss: boss,
            VisitedCount: runState.VisitedMapCoords.Count);
    }

    private static RewardsStateSnapshot BuildRewardsState(NRewardsScreen rewardsScreen)
    {
        var items = new List<RewardEntrySnapshot>();
        int index = 0;
        foreach (NRewardButton rewardButton in GodotNodeSearch.FindAll<NRewardButton>(rewardsScreen))
        {
            if (rewardButton.Reward is null || !rewardButton.IsEnabled)
            {
                continue;
            }

            Reward reward = rewardButton.Reward;
            items.Add(new RewardEntrySnapshot(
                Index: index,
                Type: GetRewardTypeName(reward),
                Label: BuildRewardLabel(reward),
                Description: ObservationText.SafeGetText(() => reward.Description)));
            index++;
        }

        bool canProceed = GodotNodeSearch.FindFirst<NProceedButton>(rewardsScreen)?.IsEnabled ?? false;
        return new RewardsStateSnapshot(canProceed, items);
    }

    private static CardRewardStateSnapshot BuildCardRewardState(NCardRewardSelectionScreen cardScreen)
    {
        var cards = new List<CardRewardEntrySnapshot>();
        int index = 0;
        foreach (NCardHolder holder in GodotNodeSearch.FindAllSortedByPosition<NCardHolder>(cardScreen))
        {
            CardModel? card = holder.CardModel;
            if (card is null)
            {
                continue;
            }

            cards.Add(BuildCardRewardEntry(index, card));
            index++;
        }

        bool canSkip = GodotNodeSearch.FindAll<NCardRewardAlternativeButton>(cardScreen).Count > 0;
        return new CardRewardStateSnapshot(canSkip, cards);
    }

    private static CardSelectionStateSnapshot BuildCardGridSelectionState(NCardGridSelectionScreen screen)
    {
        var cards = new List<CardRewardEntrySnapshot>();
        int index = 0;
        foreach (NGridCardHolder holder in GodotNodeSearch.FindAllSortedByPosition<NGridCardHolder>(screen))
        {
            CardModel? card = holder.CardModel;
            if (card is null)
            {
                continue;
            }

            cards.Add(BuildCardRewardEntry(index, card));
            index++;
        }

        string? prompt = ReadControlText(screen.GetNodeOrNull<Control>("%BottomLabel"));
        bool canCancel = screen.GetNodeOrNull<NBackButton>("%Close")?.IsEnabled ?? false;
        bool canConfirm = GodotNodeSearch.FindAll<NConfirmButton>(screen).Any(button => button.IsEnabled && button.IsVisibleInTree());

        return new CardSelectionStateSnapshot(
            ScreenType: screen.GetType().Name,
            Prompt: prompt,
            CanConfirm: canConfirm,
            CanCancel: canCancel,
            CanSkip: false,
            Cards: cards);
    }

    private static CardSelectionStateSnapshot BuildChooseCardSelectionState(NChooseACardSelectionScreen screen)
    {
        var cards = new List<CardRewardEntrySnapshot>();
        int index = 0;
        foreach (NGridCardHolder holder in GodotNodeSearch.FindAllSortedByPosition<NGridCardHolder>(screen))
        {
            CardModel? card = holder.CardModel;
            if (card is null)
            {
                continue;
            }

            cards.Add(BuildCardRewardEntry(index, card));
            index++;
        }

        bool canSkip = screen.GetNodeOrNull<NClickableControl>("SkipButton") is { IsEnabled: true, Visible: true };
        return new CardSelectionStateSnapshot(
            ScreenType: "choose_card",
            Prompt: "Choose a card.",
            CanConfirm: false,
            CanCancel: canSkip,
            CanSkip: canSkip,
            Cards: cards);
    }

    private static EventStateSnapshot BuildEventState(EventRoom eventRoom)
    {
        var eventModel = eventRoom.CanonicalEvent;
        var options = new List<EventOptionEntrySnapshot>();
        Node? eventUi = NEventRoom.Instance;
        if (eventUi is not null)
        {
            int index = 0;
            foreach (NEventOptionButton button in GodotNodeSearch.FindAll<NEventOptionButton>(eventUi))
            {
                dynamic option = button.Option;
                options.Add(new EventOptionEntrySnapshot(
                    Index: index,
                    Title: ObservationText.ToReadableText(option.Title) ?? $"option_{index}",
                    Description: ObservationText.ToReadableText(option.Description) ?? string.Empty,
                    IsLocked: option.IsLocked,
                    IsProceed: option.IsProceed));
                index++;
            }
        }

        return new EventStateSnapshot(
            EventId: eventModel.Id.Entry,
            Title: ObservationText.SafeGetText(() => eventModel.Title) ?? eventModel.Id.Entry,
            Body: ObservationText.SafeGetText(() => eventModel.Description) ?? string.Empty,
            InDialogue: false,
            Options: options);
    }

    private static ShopStateSnapshot BuildShopState(MerchantRoom merchantRoom)
    {
        var items = new List<ShopItemEntrySnapshot>();
        MerchantInventory? inventory = merchantRoom.Inventory;
        if (inventory is not null)
        {
            int index = 0;
            foreach (dynamic entry in inventory.CardEntries)
            {
                if (entry.CreationResult?.Card is CardModel card)
                {
                    items.Add(new ShopItemEntrySnapshot(
                        Index: index,
                        Category: "card",
                        Price: entry.Cost,
                        CanAfford: entry.EnoughGold,
                        Title: ObservationText.SafeGetText(() => card.Title) ?? card.Id.Entry,
                        Description: ObservationText.SafeGetCardDescription(card, PileType.Draw)));
                    index++;
                }
            }

            foreach (dynamic entry in inventory.RelicEntries)
            {
                if (entry.Model is RelicModel relic)
                {
                    items.Add(new ShopItemEntrySnapshot(
                        Index: index,
                        Category: "relic",
                        Price: entry.Cost,
                        CanAfford: entry.EnoughGold,
                        Title: ObservationText.SafeGetText(() => relic.Title) ?? relic.Id.Entry,
                        Description: ObservationText.SafeGetText(() => relic.DynamicDescription)));
                    index++;
                }
            }

            foreach (dynamic entry in inventory.PotionEntries)
            {
                if (entry.Model is PotionModel potion)
                {
                    items.Add(new ShopItemEntrySnapshot(
                        Index: index,
                        Category: "potion",
                        Price: entry.Cost,
                        CanAfford: entry.EnoughGold,
                        Title: ObservationText.SafeGetText(() => potion.Title) ?? potion.Id.Entry,
                        Description: ObservationText.SafeGetText(() => potion.DynamicDescription)));
                    index++;
                }
            }

            if (inventory.CardRemovalEntry is { } removal)
            {
                items.Add(new ShopItemEntrySnapshot(
                    Index: index,
                    Category: "card_removal",
                    Price: removal.Cost,
                    CanAfford: removal.EnoughGold,
                    Title: "Remove a card",
                    Description: null));
            }
        }

        bool canProceed = NMerchantRoom.Instance?.ProceedButton?.IsEnabled ?? false;
        return new ShopStateSnapshot(canProceed, items);
    }

    private static RestSiteStateSnapshot BuildRestSiteState(RestSiteRoom restSiteRoom)
    {
        RestOptionEntrySnapshot[] options = restSiteRoom.Options
            .Select((option, index) => new RestOptionEntrySnapshot(
                Index: index,
                Id: option.OptionId,
                Title: ObservationText.SafeGetText(() => option.Title) ?? option.OptionId,
                Description: ObservationText.SafeGetText(() => option.Description) ?? string.Empty,
                IsEnabled: option.IsEnabled))
            .ToArray();

        bool canProceed = NRestSiteRoom.Instance?.ProceedButton?.IsEnabled ?? false;
        return new RestSiteStateSnapshot(canProceed, options);
    }

    private static TreasureStateSnapshot BuildTreasureState(TreasureRoom treasureRoom)
    {
        NTreasureRoom? treasureUi = GodotNodeSearch.FindFirst<NTreasureRoom>(((SceneTree)Engine.GetMainLoop()).Root);
        if (treasureUi is null)
        {
            return new TreasureStateSnapshot("Treasure room loading...", false, Array.Empty<RelicChoiceEntrySnapshot>());
        }

        var relics = new List<RelicChoiceEntrySnapshot>();
        NTreasureRoomRelicCollection? relicCollection = treasureUi.GetNodeOrNull<NTreasureRoomRelicCollection>("%RelicCollection");
        if (relicCollection?.Visible == true)
        {
            int index = 0;
            foreach (NTreasureRoomRelicHolder holder in GodotNodeSearch.FindAll<NTreasureRoomRelicHolder>(relicCollection).Where(holder => holder.IsEnabled && holder.Visible))
            {
                RelicModel? relic = holder.Relic?.Model;
                if (relic is null)
                {
                    continue;
                }

                relics.Add(new RelicChoiceEntrySnapshot(
                    Index: index,
                    Id: relic.Id.Entry,
                    Title: ObservationText.SafeGetText(() => relic.Title) ?? relic.Id.Entry,
                    Description: ObservationText.SafeGetText(() => relic.DynamicDescription) ?? string.Empty,
                    Rarity: relic.Rarity.ToString()));
                index++;
            }
        }

        return new TreasureStateSnapshot(
            Message: null,
            CanProceed: treasureUi.ProceedButton?.IsEnabled ?? false,
            Relics: relics);
    }

    private static CardRewardEntrySnapshot BuildCardRewardEntry(int index, CardModel card)
    {
        return new CardRewardEntrySnapshot(
            Index: index,
            Id: card.Id.Entry,
            Title: ObservationText.SafeGetText(() => card.Title) ?? card.Id.Entry,
            Type: card.Type.ToString(),
            Rarity: card.Rarity.ToString(),
            Cost: GetCostDisplay(card),
            StarCost: GetStarCostDisplay(card),
            Description: ObservationText.SafeGetCardDescription(card, PileType.Draw) ?? string.Empty);
    }

    private static string BuildRewardLabel(Reward reward)
    {
        return reward switch
        {
            GoldReward goldReward => $"{goldReward.Amount} gold",
            PotionReward potionReward when potionReward.Potion is not null =>
                ObservationText.SafeGetText(() => potionReward.Potion.Title) ?? "Potion reward",
            RelicReward => "Relic reward",
            CardReward => "Card reward",
            SpecialCardReward => "Special card reward",
            CardRemovalReward => "Card removal reward",
            _ => reward.GetType().Name
        };
    }

    private static string GetRewardTypeName(Reward reward)
    {
        return reward switch
        {
            GoldReward => "gold",
            PotionReward => "potion",
            RelicReward => "relic",
            CardReward => "card",
            SpecialCardReward => "special_card",
            CardRemovalReward => "card_removal",
            _ => reward.GetType().Name.ToLowerInvariant()
        };
    }

    private static string GetCostDisplay(CardModel card)
    {
        return card.EnergyCost.CostsX ? "X" : card.EnergyCost.GetAmountToSpend().ToString();
    }

    private static string? GetStarCostDisplay(CardModel card)
    {
        if (card.HasStarCostX)
        {
            return "X";
        }

        return card.CurrentStarCost >= 0 ? card.GetStarCostWithModifiers().ToString() : null;
    }

    private static string? ReadControlText(Control? control)
    {
        if (control is null)
        {
            return null;
        }

        Variant textVariant = control.Get("text");
        return textVariant.VariantType == Variant.Type.Nil
            ? null
            : ObservationText.StripRichTextTags(textVariant.AsString());
    }

    private static IReadOnlyList<string> GetRecommendedQueries(string stateType)
    {
        return stateType switch
        {
            "menu" => ["/api/v1/context"],
            "map" => ["/api/v1/context", "/api/v1/map/summary", "/api/v1/player/summary"],
            "event" => ["/api/v1/context", "/api/v1/event", "/api/v1/player/summary"],
            "shop" => ["/api/v1/context", "/api/v1/shop", "/api/v1/player/summary"],
            "rest_site" => ["/api/v1/context", "/api/v1/rest-site", "/api/v1/player/summary"],
            "treasure" => ["/api/v1/context", "/api/v1/treasure", "/api/v1/player/summary"],
            "rewards" => ["/api/v1/context", "/api/v1/rewards", "/api/v1/player/summary"],
            "card_reward" => ["/api/v1/context", "/api/v1/card-reward", "/api/v1/player/deck"],
            "card_select" => ["/api/v1/context", "/api/v1/card-selection", "/api/v1/player/deck"],
            "monster" or "elite" or "boss" =>
            [
                "/api/v1/context",
                "/api/v1/combat/summary",
                "/api/v1/combat/actions",
                "/api/v1/combat/hand",
                "/api/v1/combat/enemies",
                "/api/v1/player/status"
            ],
            _ => ["/api/v1/context", "/api/v1/observation/compact"]
        };
    }

    private static CompactObservationSnapshot BuildCompactObservation(
        ContextSnapshot context,
        PlayerStateSnapshot? player,
        CombatStateSnapshot? combat,
        MapStateSnapshot? map,
        RewardsStateSnapshot? rewards,
        CardRewardStateSnapshot? cardReward,
        EventStateSnapshot? eventState,
        ShopStateSnapshot? shop,
        RestSiteStateSnapshot? restSite,
        TreasureStateSnapshot? treasure,
        CardSelectionStateSnapshot? cardSelection,
        OverlayStateSnapshot? overlay)
    {
        var facts = new List<string>();
        string goal;

        switch (context.StateType)
        {
            case "menu":
                goal = "No run is active. Start or load a run before querying deeper state.";
                break;
            case "monster":
            case "elite":
            case "boss":
                goal = "Decide the next combat action with the smallest possible query set.";
                if (player is not null)
                {
                    facts.Add($"Player HP {player.CurrentHp}/{player.MaxHp}, block {player.Block}, energy {player.Energy ?? 0}/{player.MaxEnergy ?? 0}.");
                }
                if (combat is not null)
                {
                    facts.Add($"Round {combat.Round}, side {combat.Side}, hand {combat.Hand.Count}, draw {combat.Piles.Draw}, discard {combat.Piles.Discard}.");
                    facts.Add($"Alive enemies {combat.Enemies.Count}.");
                    facts.Add($"Playable actions {combat.AvailableActions.Count(action => action.ActionType == "play_card")}, incoming damage {combat.Enemies.Sum(enemy => enemy.IncomingDamage ?? 0)}.");
                }
                break;
            case "map":
                goal = "Plan the next route while keeping path context compact.";
                if (map?.CurrentPosition is not null)
                {
                    facts.Add($"Current map position ({map.CurrentPosition.Col}, {map.CurrentPosition.Row}).");
                }
                facts.Add($"Travelable next nodes: {map?.NextOptions.Count ?? 0}.");
                break;
            case "event":
                goal = "Choose the event option with only event text and current player summary.";
                if (eventState is not null)
                {
                    facts.Add($"Event '{eventState.Title}' with {eventState.Options.Count} options.");
                }
                break;
            case "shop":
                goal = "Evaluate affordable purchases without re-reading the whole deck.";
                if (player is not null)
                {
                    facts.Add($"Gold {player.Gold}.");
                }
                if (shop is not null)
                {
                    facts.Add($"Shop items {shop.Items.Count}, affordable {shop.Items.Count(item => item.CanAfford)}.");
                }
                break;
            case "rest_site":
                goal = "Choose one enabled rest-site option.";
                facts.Add($"Enabled rest options: {restSite?.Options.Count(option => option.IsEnabled) ?? 0}.");
                break;
            case "rewards":
                goal = "Inspect reward options before claiming anything.";
                facts.Add($"Visible rewards: {rewards?.Items.Count ?? 0}.");
                break;
            case "card_reward":
                goal = "Judge card reward candidates against the current deck summary.";
                facts.Add($"Card reward choices: {cardReward?.Cards.Count ?? 0}.");
                break;
            case "card_select":
                goal = "Resolve the current card selection with only the visible choices.";
                if (cardSelection is not null)
                {
                    facts.Add($"Selection screen {cardSelection.ScreenType}, cards {cardSelection.Cards.Count}.");
                }
                break;
            case "treasure":
                goal = "Pick a relic from the treasure screen.";
                facts.Add($"Visible relic choices: {treasure?.Relics.Count ?? 0}.");
                break;
            case "overlay":
                goal = "An unhandled overlay is active. Query context first and fall back to full state only if blocked.";
                if (overlay is not null)
                {
                    facts.Add(overlay.Message);
                }
                break;
            default:
                goal = "Query only the endpoint that matches the active screen.";
                break;
        }

        return new CompactObservationSnapshot(
            StateType: context.StateType,
            Goal: goal,
            Facts: facts,
            SuggestedQueries: context.RecommendedQueries);
    }
}
