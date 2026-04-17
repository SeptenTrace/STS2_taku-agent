import type {
  ActionExecutionResponse,
  ActionSurfaceResponse,
  CardRewardResponse,
  CardSelectionResponse,
  CombatSummaryResponse,
  ContextResponse,
  EventResponse,
  MapSummaryResponse,
  PlayerSummaryResponse,
  RestSiteResponse,
  RewardsResponse,
  ShopResponse,
  TreasureResponse
} from "../api-types.ts";
import type { RequestClient } from "../core/client.ts";
import { CliError } from "../core/errors.ts";

const READY_STATES = new Set([
  "rewards",
  "map",
  "event",
  "shop",
  "rest_site",
  "treasure",
  "card_reward",
  "card_select"
]);

type RoomStateData =
  | { kind: "combat"; path: "/api/v1/combat/summary"; data: CombatSummaryResponse }
  | { kind: "map"; path: "/api/v1/map/summary"; data: MapSummaryResponse }
  | { kind: "event"; path: "/api/v1/event"; data: EventResponse }
  | { kind: "shop"; path: "/api/v1/shop"; data: ShopResponse }
  | { kind: "rewards"; path: "/api/v1/rewards"; data: RewardsResponse }
  | { kind: "card_reward"; path: "/api/v1/card-reward"; data: CardRewardResponse }
  | { kind: "card_select"; path: "/api/v1/card-selection"; data: CardSelectionResponse }
  | { kind: "rest_site"; path: "/api/v1/rest-site"; data: RestSiteResponse }
  | { kind: "treasure"; path: "/api/v1/treasure"; data: TreasureResponse };

export interface PlayerReadyResult {
  condition: "player_ready";
  matched: true;
  context: ContextResponse;
  combat?: CombatSummaryResponse;
}

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
  playerSummary: PlayerSummaryResponse;
  actions: ActionSurfaceResponse;
  stateData?: RoomStateData;
}

function isCombatState(stateType: string | undefined): boolean {
  return stateType === "monster" || stateType === "elite" || stateType === "boss";
}

export async function waitForPlayerReady(client: RequestClient): Promise<PlayerReadyResult> {
  const context = await client.request<ContextResponse>("/api/v1/context");
  if (READY_STATES.has(context.stateType)) {
    return {
      condition: "player_ready",
      matched: true,
      context
    };
  }

  if (isCombatState(context.stateType)) {
    const combat = await client.request<CombatSummaryResponse>("/api/v1/combat/summary");
    if (combat.side !== "player") {
      throw new CliError(`State is not player-ready yet: combat side is '${combat.side}'.`);
    }

    return {
      condition: "player_ready",
      matched: true,
      context,
      combat
    };
  }

  throw new CliError(`State is not player-ready: ${context.stateType}`);
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
    default:
      return undefined;
  }
}

export async function buildRoomSummary(client: RequestClient): Promise<RoomSummaryResult> {
  const [context, playerSummary, actions] = await Promise.all([
    client.request<ContextResponse>("/api/v1/context"),
    client.request<PlayerSummaryResponse>("/api/v1/player/summary"),
    client.request<ActionSurfaceResponse>("/api/v1/actions")
  ]);

  return {
    context,
    playerSummary,
    actions,
    stateData: await readStateData(client, context)
  };
}
