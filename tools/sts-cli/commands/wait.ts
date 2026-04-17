import { POLL_INTERVAL_MS } from "../config.ts";
import { CliError, HttpError } from "../core/errors.ts";
import type { RequestClient } from "../core/client.ts";
import type { ActionSurfaceResponse, CombatSummaryResponse, ContextResponse } from "../api-types.ts";

export interface WaitResult {
  condition: string;
  matched: true;
  context: ContextResponse;
  combat?: CombatSummaryResponse;
}

interface WaitObservation {
  context: ContextResponse;
  combat: CombatSummaryResponse | null;
  actions: ActionSurfaceResponse | null;
}

const READY_STATE_PRIMARY_ACTIONS: Readonly<Record<string, readonly string[]>> = {
  map: ["choose_map_node"],
  event: ["choose_event_option", "advance_dialogue"],
  fake_merchant: ["shop_purchase", "proceed"],
  shop: ["shop_purchase", "proceed"],
  rest_site: ["choose_rest_option", "proceed"],
  rewards: ["claim_reward", "proceed"],
  card_reward: ["select_card_reward", "skip_card_reward"],
  card_select: ["select_card", "confirm_selection"],
  bundle_select: ["select_bundle", "confirm_bundle_selection", "cancel_bundle_selection"],
  relic_select: ["select_relic", "skip_relic_selection"],
  crystal_sphere: ["crystal_sphere_set_tool", "crystal_sphere_click_cell", "crystal_sphere_proceed"],
  treasure: ["claim_treasure_relic", "proceed"]
};

export function normalizeWaitCondition(condition: string): string {
  return condition.trim().replaceAll("-", "_").toLowerCase();
}

function contextStateTypeMatches(context: ContextResponse, expected: string): boolean {
  return context.stateType === expected;
}

function isContextStable(context: ContextResponse): boolean {
  return context.isStable !== false && context.isTransitioning !== true;
}

function isCombatState(stateType: string | undefined): boolean {
  return ["monster", "elite", "boss"].includes(String(stateType ?? ""));
}

function isInteractiveReadyState(stateType: string | undefined): stateType is keyof typeof READY_STATE_PRIMARY_ACTIONS {
  return Object.hasOwn(READY_STATE_PRIMARY_ACTIONS, String(stateType ?? ""));
}

function isCombatPlayerReady(combat: CombatSummaryResponse | null): combat is CombatSummaryResponse {
  return combat !== null && combat.side === "player" && combat.actionCount > 0;
}

function isCombatEnemyStable(combat: CombatSummaryResponse | null): combat is CombatSummaryResponse {
  return combat !== null && combat.side === "enemy" && combat.actionCount === 0;
}

function hasPrimaryAction(actions: ActionSurfaceResponse | null, stateType: string): boolean {
  if (actions === null || actions.stateType !== stateType) {
    return false;
  }

  const primaryActions = READY_STATE_PRIMARY_ACTIONS[stateType];
  if (!primaryActions) {
    return false;
  }

  return actions.actions.some((action) => primaryActions.includes(action.actionType));
}

async function maybeReadCombatSummary(client: RequestClient, context: ContextResponse): Promise<CombatSummaryResponse | null> {
  if (!isCombatState(context.stateType)) {
    return null;
  }

  try {
    return await client.request<CombatSummaryResponse>("/api/v1/combat/summary");
  } catch (error) {
    if (error instanceof HttpError && error.status === 409) {
      return null;
    }

    throw error;
  }
}

async function maybeReadActionSurface(client: RequestClient, context: ContextResponse, condition: string): Promise<ActionSurfaceResponse | null> {
  const shouldReadActions = condition === "player_ready" || condition === "run_active" || isInteractiveReadyState(context.stateType) || context.stateType === condition;
  if (!shouldReadActions || isCombatState(context.stateType)) {
    return null;
  }

  try {
    return await client.request<ActionSurfaceResponse>("/api/v1/actions");
  } catch (error) {
    if (error instanceof HttpError && error.status === 409) {
      return null;
    }

    throw error;
  }
}

async function readWaitObservation(client: RequestClient, condition: string): Promise<WaitObservation> {
  const context = await client.request<ContextResponse>("/api/v1/context");
  const combat = condition === "player_turn" || condition === "enemy_turn" || condition === "player_ready" || condition === "run_active" || isCombatState(condition)
    ? await maybeReadCombatSummary(client, context)
    : null;
  const actions = await maybeReadActionSurface(client, context, condition);

  return { context, combat, actions };
}

function observationMatchesCondition(observation: WaitObservation, condition: string): boolean {
  if (!isContextStable(observation.context)) {
    return false;
  }

  if (condition === "player_turn") {
    return isCombatState(observation.context.stateType) && isCombatPlayerReady(observation.combat);
  }

  if (condition === "enemy_turn") {
    return isCombatState(observation.context.stateType) && isCombatEnemyStable(observation.combat);
  }

  if (condition === "player_ready") {
    if (isCombatState(observation.context.stateType)) {
      return isCombatPlayerReady(observation.combat);
    }

    return isInteractiveReadyState(observation.context.stateType) &&
      hasPrimaryAction(observation.actions, observation.context.stateType);
  }

  if (condition === "run_active") {
    if (observation.context.stateType === "menu" || observation.context.stateType === "unknown") {
      return false;
    }

    if (isCombatState(observation.context.stateType)) {
      return observation.combat !== null;
    }

    if (isInteractiveReadyState(observation.context.stateType)) {
      return hasPrimaryAction(observation.actions, observation.context.stateType);
    }

    return true;
  }

  if (!contextStateTypeMatches(observation.context, condition)) {
    return false;
  }

  if (condition === "player_ready") {
    return false;
  }

  if (condition === "player_turn" || condition === "enemy_turn") {
    return false;
  }

  if (isCombatState(observation.context.stateType)) {
    return observation.combat !== null;
  }

  if (isInteractiveReadyState(observation.context.stateType)) {
    return hasPrimaryAction(observation.actions, observation.context.stateType);
  }

  return true;
}

function buildObservationStabilityKey(observation: WaitObservation): string {
  return JSON.stringify({
    context: observation.context,
    combat: observation.combat,
    actions: observation.actions
  });
}

export async function waitForCondition(client: RequestClient, rawCondition: string, timeoutSeconds: number): Promise<WaitResult> {
  const condition = normalizeWaitCondition(rawCondition);
  const deadline = Date.now() + timeoutSeconds * 1000;
  let previousMatchedKey: string | null = null;

  while (Date.now() < deadline) {
    const observation = await readWaitObservation(client, condition);
    const matched = observationMatchesCondition(observation, condition);

    if (matched) {
      const stabilityKey = buildObservationStabilityKey(observation);
      if (stabilityKey === previousMatchedKey) {
        return observation.combat === null
          ? { condition, matched: true, context: observation.context }
          : { condition, matched: true, context: observation.context, combat: observation.combat };
      }

      previousMatchedKey = stabilityKey;
    }
    else
    {
      previousMatchedKey = null;
    }

    await new Promise((resolve) => setTimeout(resolve, POLL_INTERVAL_MS));
  }

  const latestContext = await client.request<ContextResponse>("/api/v1/context");
  throw new CliError(`Timed out waiting for condition: ${condition}\n${JSON.stringify(latestContext, null, 2)}`);
}
