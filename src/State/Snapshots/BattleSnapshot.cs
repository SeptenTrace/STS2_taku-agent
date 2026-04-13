namespace TakuAgentMod.State.Snapshots;

internal sealed record BattleSnapshot(
    DateTimeOffset Timestamp,
    string Trigger,
    int RoundNumber,
    string CurrentSide,
    string? EncounterType,
    PlayerSnapshot? Player,
    IReadOnlyList<EnemySnapshot> Enemies);

internal sealed record PlayerSnapshot(
    string? CharacterType,
    string Name,
    int CurrentHp,
    int MaxHp,
    int Block,
    int Energy,
    int MaxEnergy,
    int Stars,
    PileSnapshot Hand,
    PileSnapshot DrawPile,
    PileSnapshot DiscardPile,
    PileSnapshot ExhaustPile,
    IReadOnlyList<PotionSnapshot> Potions,
    IReadOnlyList<RelicSnapshot> Relics,
    IReadOnlyList<StatusEffectSnapshot> Powers);

internal sealed record EnemySnapshot(
    string Name,
    string ModelType,
    int CurrentHp,
    int MaxHp,
    int Block,
    bool IsAlive,
    bool IsHittable,
    string SlotName,
    IntentSnapshot Intent,
    IReadOnlyList<StatusEffectSnapshot> Powers);

internal sealed record IntentSnapshot(
    string? MoveStateType,
    string? StateId,
    bool? IntendsToAttack,
    IReadOnlyList<IntentDetailSnapshot> Intents);

internal sealed record IntentDetailSnapshot(
    string IntentClass,
    string IntentType,
    string? Label);

internal sealed record PileSnapshot(
    string Type,
    int Count,
    IReadOnlyList<CardSnapshot> Cards);

internal sealed record CardSnapshot(
    string Title,
    string Description,
    string Type,
    string Rarity,
    string TargetType,
    int EnergyCost,
    bool EnergyCostIsX,
    int StarCost,
    bool IsUpgraded,
    IReadOnlyList<string> Keywords);

internal sealed record RelicSnapshot(
    string Type,
    string Title,
    string Description,
    int? Amount,
    string? Rarity);

internal sealed record PotionSnapshot(
    string Type,
    string Title,
    string Description,
    string? Usage,
    string? TargetType,
    string? Rarity);

internal sealed record StatusEffectSnapshot(
    string Type,
    string Title,
    string Description,
    int? Amount,
    string? Category);
