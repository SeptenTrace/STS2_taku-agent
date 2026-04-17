namespace TakuAgentMod.State.Snapshots;

internal sealed record GameSnapshot(
    DateTimeOffset Timestamp,
    ContextSnapshot Context,
    RunSnapshot? Run,
    PlayerStateSnapshot? Player,
    CompactObservationSnapshot CompactObservation,
    CombatStateSnapshot? Combat,
    MapStateSnapshot? Map,
    RewardsStateSnapshot? Rewards,
    CardRewardStateSnapshot? CardReward,
    EventStateSnapshot? Event,
    ShopStateSnapshot? Shop,
    RestSiteStateSnapshot? RestSite,
    TreasureStateSnapshot? Treasure,
    CardSelectionStateSnapshot? CardSelection,
    OverlayStateSnapshot? Overlay);

internal sealed record ContextSnapshot(
    string StateType,
    string? RoomType,
    string? OverlayType,
    IReadOnlyList<string> RecommendedQueries);

internal sealed record RunSnapshot(
    int Act,
    int Floor,
    int Ascension,
    string? RoomType,
    MapCoordSnapshot? CurrentMapCoord);

internal sealed record MapCoordSnapshot(
    int Col,
    int Row,
    string? Type);

internal sealed record PlayerStateSnapshot(
    string? CharacterId,
    string Character,
    int CurrentHp,
    int MaxHp,
    int Block,
    int Gold,
    int? Energy,
    int? MaxEnergy,
    int? Stars,
    int DeckCount,
    IReadOnlyList<StatusEntrySnapshot> Status,
    IReadOnlyList<RelicEntrySnapshot> Relics,
    IReadOnlyList<PotionEntrySnapshot> Potions,
    DeckSummarySnapshot Deck);

internal sealed record PlayerSummarySnapshot(
    string? CharacterId,
    string Character,
    int CurrentHp,
    int MaxHp,
    int Block,
    int Gold,
    int? Energy,
    int? MaxEnergy,
    int? Stars,
    int DeckCount,
    int UniqueCards,
    int UpgradedCards,
    IReadOnlyList<string> RelicIds,
    IReadOnlyList<string> PotionIds,
    IReadOnlyList<StatusEntrySnapshot> Status);

internal sealed record DeckSummarySnapshot(
    int TotalCards,
    IReadOnlyList<DeckCardEntrySnapshot> Cards);

internal sealed record DeckCardEntrySnapshot(
    string Id,
    string Title,
    int Copies,
    int UpgradedCopies,
    string Type,
    string Rarity,
    string Cost,
    string? StarCost,
    string Description);

internal sealed record StatusEntrySnapshot(
    string Id,
    string Title,
    string Description,
    int? Amount,
    string Category);

internal sealed record RelicEntrySnapshot(
    string Id,
    string Title,
    string Description,
    int? Counter,
    string? Rarity);

internal sealed record PotionEntrySnapshot(
    string Id,
    string Title,
    string Description,
    int Slot,
    string TargetType,
    string Usage);

internal sealed record CombatStateSnapshot(
    string RoomType,
    int Round,
    string Side,
    PileCountsSnapshot Piles,
    PileDetailsSnapshot PileDetails,
    IReadOnlyList<HandCardEntrySnapshot> Hand,
    IReadOnlyList<EnemyStateEntrySnapshot> Enemies,
    IReadOnlyList<CombatActionSnapshot> AvailableActions);

internal sealed record PileCountsSnapshot(
    int Draw,
    int Discard,
    int Exhaust);

internal sealed record PileDetailsSnapshot(
    IReadOnlyList<PileCardEntrySnapshot> DrawPile,
    IReadOnlyList<PileCardEntrySnapshot> DiscardPile,
    IReadOnlyList<PileCardEntrySnapshot> ExhaustPile);

internal sealed record HandCardEntrySnapshot(
    int Index,
    string Id,
    string Title,
    string Description,
    string Type,
    string Rarity,
    string TargetType,
    string Cost,
    string? StarCost,
    bool CanPlay,
    bool IsUpgraded,
    IReadOnlyList<string> LegalTargets);

internal sealed record PileCardEntrySnapshot(
    string Id,
    string Title,
    string Description,
    string Type,
    string Rarity,
    string Cost,
    string? StarCost,
    bool IsUpgraded);

internal sealed record EnemyStateEntrySnapshot(
    string EntityId,
    string Title,
    int CurrentHp,
    int MaxHp,
    int Block,
    bool IsAlive,
    IReadOnlyList<StatusEntrySnapshot> Status,
    IReadOnlyList<IntentEntrySnapshot> Intents,
    int? IncomingDamage);

internal sealed record IntentEntrySnapshot(
    string Type,
    string? Label,
    string? Description,
    bool IsAttack,
    int? ExpectedValue);

internal sealed record CombatActionSnapshot(
    string ActionType,
    int? CardIndex,
    string? CardId,
    string? CardTitle,
    bool RequiresTarget,
    IReadOnlyList<string> TargetOptions);

internal sealed record MapStateSnapshot(
    MapCoordSnapshot? CurrentPosition,
    IReadOnlyList<MapOptionSnapshot> NextOptions,
    MapCoordSnapshot? Boss,
    int VisitedCount);

internal sealed record MapOptionSnapshot(
    int Index,
    int Col,
    int Row,
    string Type,
    IReadOnlyList<MapCoordSnapshot> LeadsTo);

internal sealed record RewardsStateSnapshot(
    bool CanProceed,
    IReadOnlyList<RewardEntrySnapshot> Items);

internal sealed record RewardEntrySnapshot(
    int Index,
    string Type,
    string Label,
    string? Description);

internal sealed record CardRewardStateSnapshot(
    bool CanSkip,
    IReadOnlyList<CardRewardEntrySnapshot> Cards);

internal sealed record CardRewardEntrySnapshot(
    int Index,
    string Id,
    string Title,
    string Type,
    string Rarity,
    string Cost,
    string? StarCost,
    string Description);

internal sealed record EventStateSnapshot(
    string EventId,
    string Title,
    string Body,
    bool InDialogue,
    IReadOnlyList<EventOptionEntrySnapshot> Options);

internal sealed record EventOptionEntrySnapshot(
    int Index,
    string Title,
    string Description,
    bool IsLocked,
    bool IsProceed);

internal sealed record ShopStateSnapshot(
    bool CanProceed,
    IReadOnlyList<ShopItemEntrySnapshot> Items);

internal sealed record ShopItemEntrySnapshot(
    int Index,
    string Category,
    int Price,
    bool CanAfford,
    string Title,
    string? Description);

internal sealed record RestSiteStateSnapshot(
    bool CanProceed,
    IReadOnlyList<RestOptionEntrySnapshot> Options);

internal sealed record RestOptionEntrySnapshot(
    int Index,
    string Id,
    string Title,
    string Description,
    bool IsEnabled);

internal sealed record TreasureStateSnapshot(
    string? Message,
    bool CanProceed,
    IReadOnlyList<RelicChoiceEntrySnapshot> Relics);

internal sealed record RelicChoiceEntrySnapshot(
    int Index,
    string Id,
    string Title,
    string Description,
    string Rarity);

internal sealed record CardSelectionStateSnapshot(
    string ScreenType,
    string? Prompt,
    bool CanConfirm,
    bool CanCancel,
    bool CanSkip,
    IReadOnlyList<CardRewardEntrySnapshot> Cards);

internal sealed record OverlayStateSnapshot(
    string ScreenType,
    string Message);

internal sealed record CompactObservationSnapshot(
    string StateType,
    string Goal,
    IReadOnlyList<string> Facts,
    IReadOnlyList<string> SuggestedQueries);
