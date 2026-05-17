import type {
  ActionExecutionResponse,
  ActionSurfaceResponse,
  BundleSelectionResponse,
  CardRewardResponse,
  CardSelectionResponse,
  CombatActionResponse,
  CombatHandCardResponse,
  CombatSummaryResponse,
  ContextResponse,
  CrystalSphereResponse,
  EnemyStateResponse,
  EventResponse,
  FakeMerchantResponse,
  MapSummaryResponse,
  MenuResponse,
  ObservationCompactResponse,
  OverlayResponse,
  PlayerSummaryResponse,
  RunResponse,
  RelicSelectionResponse,
  RestSiteResponse,
  RewardsResponse,
  ShopResponse,
  TreasureResponse
} from "../api-types.ts";
import type { RequestClient } from "../core/client.ts";

type RoomStateData =
  | { kind: "menu"; path: "/api/v1/menu"; data: MenuResponse }
  | { kind: "combat"; path: "/api/v1/combat/summary"; data: CombatSummaryResponse }
  | { kind: "map"; path: "/api/v1/map/summary"; data: MapSummaryResponse }
  | { kind: "event"; path: "/api/v1/event"; data: EventResponse }
  | { kind: "fake_merchant"; path: "/api/v1/fake-merchant"; data: FakeMerchantResponse }
  | { kind: "shop"; path: "/api/v1/shop"; data: ShopResponse }
  | { kind: "rewards"; path: "/api/v1/rewards"; data: RewardsResponse }
  | { kind: "card_reward"; path: "/api/v1/card-reward"; data: CardRewardResponse }
  | { kind: "card_select"; path: "/api/v1/card-selection"; data: CardSelectionResponse }
  | { kind: "bundle_select"; path: "/api/v1/bundle-selection"; data: BundleSelectionResponse }
  | { kind: "relic_select"; path: "/api/v1/relic-selection"; data: RelicSelectionResponse }
  | { kind: "crystal_sphere"; path: "/api/v1/crystal-sphere"; data: CrystalSphereResponse }
  | { kind: "rest_site"; path: "/api/v1/rest-site"; data: RestSiteResponse }
  | { kind: "treasure"; path: "/api/v1/treasure"; data: TreasureResponse }
  | { kind: "overlay"; path: "/api/v1/overlay"; data: OverlayResponse };

export interface ClaimAllSafeResult {
  context: ContextResponse;
  claimed: Array<{
    index: number;
    type: string;
    label: string;
    message: string;
  }>;
  stoppedReason: "no_safe_rewards_left" | "card_reward_pending" | "left_rewards";
  rewards?: RewardsResponse;
  actions?: ActionSurfaceResponse;
}

export interface RoomSummaryResult {
  context: ContextResponse;
  playerSummary?: PlayerSummaryResponse;
  actions: ActionSurfaceResponse;
  stateData?: RoomStateData;
}

export interface CombatSnapshotResult {
  context: ContextResponse;
  playerSummary: PlayerSummaryResponse;
  actions: ActionSurfaceResponse;
  combatSummary: CombatSummaryResponse;
  combatActions: CombatActionResponse[];
  hand: CombatHandCardResponse[];
  enemies: EnemyStateResponse[];
}

export type RoomSnapshotDetail = "standard" | "full";

export interface RoomSnapshotResult extends RoomSummaryResult {
  compactObservation?: ObservationCompactResponse;
  run?: RunResponse;
  combat?: CombatSnapshotResult;
}

export interface RunSnapshotResult {
  context: ContextResponse;
  run?: RunResponse;
  compactObservation: ObservationCompactResponse;
  room: RoomSummaryResult;
  combat?: CombatSnapshotResult;
}

function isSafeReward(item: RewardsResponse["items"][number]): boolean {
  return item.type !== "card";
}

export async function claimAllSafeRewards(client: RequestClient): Promise<ClaimAllSafeResult> {
  const claimed: ClaimAllSafeResult["claimed"] = [];

  while (true) {
    const context = await client.request<ContextResponse>("/api/v1/context");
    if (context.stateType !== "rewards") {
      return {
        context,
        claimed,
        stoppedReason: "left_rewards"
      };
    }

    const rewards = await client.request<RewardsResponse>("/api/v1/rewards");
    const actions = await client.request<ActionSurfaceResponse>("/api/v1/actions");
    const safeReward = rewards.items.find(isSafeReward);
    if (!safeReward) {
      const stoppedReason = rewards.items.some((item) => item.type === "card")
        ? "card_reward_pending"
        : "no_safe_rewards_left";

      return {
        context,
        claimed,
        stoppedReason,
        rewards,
        actions
      };
    }

    const result = await client.request<ActionExecutionResponse>("/api/v1/actions/execute", {
      method: "POST",
      body: {
        actionType: "claim_reward",
        parameters: {
          index: safeReward.index
        }
      }
    });

    claimed.push({
      index: safeReward.index,
      type: safeReward.type,
      label: safeReward.label,
      message: result.message
    });
  }
}

async function readStateData(client: RequestClient, context: ContextResponse): Promise<RoomStateData | undefined> {
  switch (context.stateType) {
    case "menu":
      return {
        kind: "menu",
        path: "/api/v1/menu",
        data: await client.request<MenuResponse>("/api/v1/menu")
      };
    case "monster":
    case "elite":
    case "boss":
      return {
        kind: "combat",
        path: "/api/v1/combat/summary",
        data: await client.request<CombatSummaryResponse>("/api/v1/combat/summary")
      };
    case "map":
      return {
        kind: "map",
        path: "/api/v1/map/summary",
        data: await client.request<MapSummaryResponse>("/api/v1/map/summary")
      };
    case "event":
      return {
        kind: "event",
        path: "/api/v1/event",
        data: await client.request<EventResponse>("/api/v1/event")
      };
    case "fake_merchant":
      return {
        kind: "fake_merchant",
        path: "/api/v1/fake-merchant",
        data: await client.request<FakeMerchantResponse>("/api/v1/fake-merchant")
      };
    case "shop":
      return {
        kind: "shop",
        path: "/api/v1/shop",
        data: await client.request<ShopResponse>("/api/v1/shop")
      };
    case "rewards":
      return {
        kind: "rewards",
        path: "/api/v1/rewards",
        data: await client.request<RewardsResponse>("/api/v1/rewards")
      };
    case "card_reward":
      return {
        kind: "card_reward",
        path: "/api/v1/card-reward",
        data: await client.request<CardRewardResponse>("/api/v1/card-reward")
      };
    case "card_select":
      return {
        kind: "card_select",
        path: "/api/v1/card-selection",
        data: await client.request<CardSelectionResponse>("/api/v1/card-selection")
      };
    case "bundle_select":
      return {
        kind: "bundle_select",
        path: "/api/v1/bundle-selection",
        data: await client.request<BundleSelectionResponse>("/api/v1/bundle-selection")
      };
    case "relic_select":
      return {
        kind: "relic_select",
        path: "/api/v1/relic-selection",
        data: await client.request<RelicSelectionResponse>("/api/v1/relic-selection")
      };
    case "crystal_sphere":
      return {
        kind: "crystal_sphere",
        path: "/api/v1/crystal-sphere",
        data: await client.request<CrystalSphereResponse>("/api/v1/crystal-sphere")
      };
    case "rest_site":
      return {
        kind: "rest_site",
        path: "/api/v1/rest-site",
        data: await client.request<RestSiteResponse>("/api/v1/rest-site")
      };
    case "treasure":
      return {
        kind: "treasure",
        path: "/api/v1/treasure",
        data: await client.request<TreasureResponse>("/api/v1/treasure")
      };
    case "overlay":
      return {
        kind: "overlay",
        path: "/api/v1/overlay",
        data: await client.request<OverlayResponse>("/api/v1/overlay")
      };
    default:
      return undefined;
  }
}

export async function buildRoomSummary(client: RequestClient): Promise<RoomSummaryResult> {
  const [context, actions] = await Promise.all([
    client.request<ContextResponse>("/api/v1/context"),
    client.request<ActionSurfaceResponse>("/api/v1/actions")
  ]);
  const playerSummary = context.stateType === "menu"
    ? undefined
    : await client.request<PlayerSummaryResponse>("/api/v1/player/summary");

  return {
    context,
    playerSummary,
    actions,
    stateData: await readStateData(client, context)
  };
}

export async function buildCombatSnapshot(client: RequestClient): Promise<CombatSnapshotResult> {
  const [context, playerSummary, actions, combatSummary, combatActions, hand, enemies] = await Promise.all([
    client.request<ContextResponse>("/api/v1/context"),
    client.request<PlayerSummaryResponse>("/api/v1/player/summary"),
    client.request<ActionSurfaceResponse>("/api/v1/actions"),
    client.request<CombatSummaryResponse>("/api/v1/combat/summary"),
    client.request<CombatActionResponse[]>("/api/v1/combat/actions"),
    client.request<CombatHandCardResponse[]>("/api/v1/combat/hand"),
    client.request<EnemyStateResponse[]>("/api/v1/combat/enemies")
  ]);

  return {
    context,
    playerSummary,
    actions,
    combatSummary,
    combatActions,
    hand,
    enemies
  };
}

export async function buildRoomSnapshot(
  client: RequestClient,
  detail: RoomSnapshotDetail = "standard"
): Promise<RoomSnapshotResult> {
  const roomSummary = await buildRoomSummary(client);

  const result: RoomSnapshotResult = {
    ...roomSummary
  };

  if (detail === "full") {
    const [compactObservation, run] = await Promise.all([
      client.request<ObservationCompactResponse>("/api/v1/observation/compact"),
      roomSummary.context.stateType === "menu"
        ? Promise.resolve(undefined)
        : client.request<RunResponse>("/api/v1/run")
    ]);
    result.compactObservation = compactObservation;
    result.run = run;
  }

  if (["monster", "elite", "boss"].includes(roomSummary.context.stateType)) {
    result.combat = await buildCombatSnapshot(client);
  }

  return result;
}

export async function buildRunSnapshot(client: RequestClient): Promise<RunSnapshotResult> {
  const room = await buildRoomSummary(client);
  const compactObservation = await client.request<ObservationCompactResponse>("/api/v1/observation/compact");

  const result: RunSnapshotResult = {
    context: room.context,
    compactObservation,
    room
  };

  if (room.context.stateType !== "menu") {
    result.run = await client.request<RunResponse>("/api/v1/run");
  }

  if (["monster", "elite", "boss"].includes(room.context.stateType)) {
    result.combat = await buildCombatSnapshot(client);
  }

  return result;
}
