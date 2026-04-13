namespace TakuAgentMod.State.History;

internal sealed record ActionHistoryEntry(
    DateTimeOffset Timestamp,
    string ActionType,
    int RoundNumber,
    string CurrentSide,
    string PlayerName,
    CardActionSnapshot Card,
    ActionTargetSnapshot? Target,
    ResourceSpendSnapshot Resources,
    string ResultPile,
    bool IsAutoPlay,
    int PlayIndex,
    int PlayCount,
    string? SnapshotPath);

internal sealed record CardActionSnapshot(
    string Title,
    string Description,
    string Type,
    string Rarity,
    string TargetType,
    int EnergyCost,
    bool EnergyCostIsX,
    int StarCost,
    bool IsUpgraded);

internal sealed record ActionTargetSnapshot(
    string Name,
    string ModelType,
    int CurrentHp,
    int MaxHp,
    int Block,
    bool IsAlive);

internal sealed record ResourceSpendSnapshot(
    int EnergySpent,
    int EnergyValue,
    int StarsSpent,
    int StarValue);
