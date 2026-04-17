namespace TakuAgentMod.Observation;

internal sealed record ObservationEndpointDescriptor(
    string Path,
    string Cost,
    string Description,
    string UseWhen,
    IReadOnlyList<string> StateTypes);

internal static class ObservationApiCatalog
{
    public static IReadOnlyList<ObservationEndpointDescriptor> Endpoints { get; } =
    [
        new("/api/v1/context", "low", "Current state type and recommended follow-up queries.", "Always call first.", ["*"]),
        new("/api/v1/observation/compact", "low", "Compact LLM-oriented observation for the active screen.", "Use when you want the minimum decision context.", ["*"]),
        new("/api/v1/observation/delta", "low", "Incremental changes since the previous distinct observation.", "Use after actions or screen transitions to avoid rereading full sections.", ["*"]),
        new("/api/v1/actions/execute", "write", "Execute one action using the existing low-token action contract and return a linked delta.", "Use after /api/v1/actions once you have chosen a legal action.", ["*"]),
        new("/api/v1/run", "low", "Run progress summary: act, floor, ascension, current room.", "Use for coarse long-horizon planning.", ["*"]),
        new("/api/v1/actions", "low", "Current-screen action surface with action names, parameters, and lightweight semantics.", "Use before deeper reads on any actionable screen.", ["*"]),
        new("/api/v1/player/summary", "low", "Lightweight player summary with HP, gold, energy, status, and build counts.", "Use first when evaluating any choice that depends on build state.", ["*"]),
        new("/api/v1/player/deck", "medium", "Grouped deck summary with counts and card text.", "Use when choosing rewards, shops, or route priorities.", ["*"]),
        new("/api/v1/player/relics", "low", "Relic list with counters and descriptions.", "Use only when a relic interaction matters.", ["*"]),
        new("/api/v1/player/potions", "low", "Potion slots with target and usage info.", "Use when potion decisions matter.", ["*"]),
        new("/api/v1/player/status", "low", "Visible player buffs and debuffs.", "Use in combat or when a status-based event matters.", ["monster", "elite", "boss", "event"]),
        new("/api/v1/knowledge/current", "medium", "Current-context static knowledge cache for cards, relics, potions, and status text.", "Use when ids are known but you want to avoid rereading descriptions from dynamic endpoints.", ["*"]),
        new("/api/v1/knowledge/cards", "medium", "Current-context card knowledge dictionary.", "Use after hand, deck, reward, or selection reads when card text matters.", ["*"]),
        new("/api/v1/knowledge/relics", "low", "Current-context relic knowledge dictionary.", "Use when relic ids are already known.", ["*"]),
        new("/api/v1/knowledge/potions", "low", "Current-context potion knowledge dictionary.", "Use when potion ids are already known.", ["*"]),
        new("/api/v1/knowledge/status", "low", "Current-context status effect knowledge dictionary.", "Use when status ids are already known.", ["*"]),
        new("/api/v1/combat/summary", "low", "Combat round, side, pile counts, and enemy count.", "Use first during combat.", ["monster", "elite", "boss"]),
        new("/api/v1/combat/actions", "low", "Available combat actions with legal target sets, potion actions, and semantic summaries.", "Use when combat-specific action detail is needed.", ["monster", "elite", "boss"]),
        new("/api/v1/combat/hand", "medium", "Current hand with playability and card text.", "Use when deciding the next play.", ["monster", "elite", "boss"]),
        new("/api/v1/combat/enemies", "medium", "Enemy HP, block, status, and intent summaries.", "Use when targeting or damage planning matters.", ["monster", "elite", "boss"]),
        new("/api/v1/combat/piles", "medium", "Draw, discard, and exhaust pile contents.", "Use only when pile order or recycle effects matter.", ["monster", "elite", "boss"]),
        new("/api/v1/map/summary", "low", "Current map position and travelable next nodes.", "Use on map screens.", ["map"]),
        new("/api/v1/event", "low", "Current event text and options.", "Use on event screens.", ["event"]),
        new("/api/v1/fake-merchant", "medium", "Fake merchant relic inventory and proceed state.", "Use on Fake Merchant events.", ["fake_merchant"]),
        new("/api/v1/shop", "medium", "Shop inventory with prices and affordability.", "Use on merchant screens.", ["shop"]),
        new("/api/v1/rest-site", "low", "Rest site options and enabled state.", "Use on campfire screens.", ["rest_site"]),
        new("/api/v1/rewards", "low", "Visible non-card rewards and proceed state.", "Use after combat when rewards are open.", ["rewards"]),
        new("/api/v1/card-reward", "low", "Visible card reward choices.", "Use when card rewards are open.", ["card_reward"]),
        new("/api/v1/card-selection", "medium", "Visible cards on selection screens plus confirm/cancel state.", "Use for upgrades, transforms, removals, or choose-a-card screens.", ["card_select"]),
        new("/api/v1/bundle-selection", "medium", "Visible bundle choices plus preview and confirm state.", "Use when a bundle choice overlay is open.", ["bundle_select"]),
        new("/api/v1/relic-selection", "low", "Visible relic choices and skip state.", "Use when a relic choice overlay is open.", ["relic_select"]),
        new("/api/v1/crystal-sphere", "medium", "Crystal Sphere grid state, tools, and clickable cells.", "Use on Crystal Sphere overlays.", ["crystal_sphere"]),
        new("/api/v1/treasure", "low", "Visible treasure relic choices and proceed state.", "Use on treasure screens.", ["treasure"]),
        new("/api/v1/overlay", "low", "Unhandled overlay metadata and manual-intervention guidance.", "Use when stateType is overlay.", ["overlay"]),
        new("/api/v1/state/full", "high", "Full combined snapshot for debugging.", "Use only when targeted endpoints are insufficient.", ["*"])
    ];
}
