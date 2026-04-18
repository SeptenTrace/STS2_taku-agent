import { POLL_INTERVAL_MS } from "../config.ts";
import { CliError, HttpError } from "../core/errors.ts";
import type { RequestClient } from "../core/client.ts";
import type { ActionSurfaceResponse, CombatSummaryResponse, ContextResponse, OverlayResponse } from "../api-types.ts";

export type WaitStatus = "pending" | "matched" | "terminal";

export interface WaitTraceEntry {
  poll: number;
  status: WaitStatus;
  matched: boolean;
  reason: string;
  context: ContextResponse;
  combat?: CombatSummaryResponse;
  actions?: ActionSurfaceResponse;
  overlay?: OverlayResponse;
  terminalReason?: string;
}

export interface WaitMatchedResult {
  condition: string;
  status: "matched";
  matched: true;
  terminal: false;
  context: ContextResponse;
  combat?: CombatSummaryResponse;
  trace?: WaitTraceEntry[];
}

export interface WaitTerminalResult {
  condition: string;
  status: "terminal";
  matched: false;
  terminal: true;
  terminalReason: string;
  context: ContextResponse;
  overlay?: OverlayResponse;
  trace?: WaitTraceEntry[];
}

export type WaitResult = WaitMatchedResult | WaitTerminalResult;

export interface WaitOptions {
  verbose?: boolean;
}

export interface WaitInvocation {
  condition: string;
  timeoutSeconds: number;
  verbose: boolean;
}

interface WaitObservation {
  context: ContextResponse;
  combat: CombatSummaryResponse | null;
  actions: ActionSurfaceResponse | null;
  overlay: OverlayResponse | null;
}

interface WaitEvaluation {
  status: WaitStatus;
  reason: string;
  terminalReason?: string;
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
  treasure: ["open_treasure", "claim_treasure_relic", "proceed"]
};

export function normalizeWaitCondition(condition: string): string {
  return condition.trim().replaceAll("-", "_").toLowerCase();
}

export function buildWaitInvocation(args: string[], defaultTimeoutSeconds: number): WaitInvocation {
  const condition = args[0];
  if (!condition) {
    throw new CliError("Usage: sts wait CONDITION [TIMEOUT_SECONDS] [--verbose]");
  }

  let timeoutSeconds = defaultTimeoutSeconds;
  let verbose = false;

  for (let index = 1; index < args.length; index++) {
    const arg = args[index]!;
    if (arg === "--verbose") {
      verbose = true;
      continue;
    }

    if (arg === "--timeout") {
      const timeoutRaw = args[index + 1];
      if (timeoutRaw === undefined) {
        throw new CliError("Usage: sts wait CONDITION [TIMEOUT_SECONDS] [--verbose]");
      }

      timeoutSeconds = parseWaitTimeout(timeoutRaw);
      index++;
      continue;
    }

    if (arg.startsWith("--timeout=")) {
      timeoutSeconds = parseWaitTimeout(arg.slice("--timeout=".length));
      continue;
    }

    if (!arg.startsWith("--") && index === 1) {
      timeoutSeconds = parseWaitTimeout(arg);
      continue;
    }

    throw new CliError(`Unknown wait argument: ${arg}`);
  }

  return { condition, timeoutSeconds, verbose };
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

async function maybeReadOverlay(client: RequestClient, context: ContextResponse): Promise<OverlayResponse | null> {
  if (context.stateType !== "overlay") {
    return null;
  }

  try {
    return await client.request<OverlayResponse>("/api/v1/overlay");
  } catch (error) {
    if (error instanceof HttpError && error.status === 409) {
      return null;
    }

    throw error;
  }
}

async function readWaitObservation(client: RequestClient, condition: string): Promise<WaitObservation> {
  const context = await client.request<ContextResponse>("/api/v1/context");
  const combat = condition === "player_turn" || condition === "enemy_turn" || condition === "player_ready" || condition === "room_ready" || condition === "run_active" || isCombatState(condition)
    ? await maybeReadCombatSummary(client, context)
    : null;
  const actions = await maybeReadActionSurface(client, context, condition);
  const overlay = await maybeReadOverlay(client, context);

  return { context, combat, actions, overlay };
}

function buildWaitTraceEntry(poll: number, evaluation: WaitEvaluation, observation: WaitObservation): WaitTraceEntry {
  return {
    poll,
    status: evaluation.status,
    matched: evaluation.status === "matched",
    reason: evaluation.reason,
    context: observation.context,
    combat: observation.combat ?? undefined,
    actions: observation.actions ?? undefined,
    overlay: observation.overlay ?? undefined,
    terminalReason: evaluation.terminalReason
  };
}

function evaluateObservation(observation: WaitObservation, condition: string): WaitEvaluation {
  if (observation.overlay?.isTerminal) {
    return {
      status: "terminal",
      reason: "terminal_overlay",
      terminalReason: observation.overlay.terminalReason ?? "overlay"
    };
  }

  if (!isContextStable(observation.context)) {
    return { status: "pending", reason: "context_not_stable" };
  }

  if (condition === "player_turn") {
    if (!isCombatState(observation.context.stateType)) {
      return { status: "pending", reason: "not_in_combat" };
    }

    return isCombatPlayerReady(observation.combat)
      ? { status: "matched", reason: "player_turn_ready" }
      : { status: "pending", reason: "combat_not_player_ready" };
  }

  if (condition === "enemy_turn") {
    if (!isCombatState(observation.context.stateType)) {
      return { status: "pending", reason: "not_in_combat" };
    }

    return isCombatEnemyStable(observation.combat)
      ? { status: "matched", reason: "enemy_turn_stable" }
      : { status: "pending", reason: "combat_not_enemy_stable" };
  }

  if (condition === "player_ready") {
    if (isCombatState(observation.context.stateType)) {
      return isCombatPlayerReady(observation.combat)
        ? { status: "matched", reason: "combat_player_ready" }
        : { status: "pending", reason: "combat_not_player_ready" };
    }

    if (!isInteractiveReadyState(observation.context.stateType)) {
      return { status: "pending", reason: "state_not_player_ready_compatible" };
    }

    return hasPrimaryAction(observation.actions, observation.context.stateType)
      ? { status: "matched", reason: "interactive_player_ready" }
      : { status: "pending", reason: "missing_primary_action" };
  }

  if (condition === "room_ready") {
    if (observation.context.stateType === "menu" || observation.context.stateType === "unknown" || observation.context.stateType === "overlay") {
      return { status: "pending", reason: "not_in_actionable_room" };
    }

    if (isCombatState(observation.context.stateType)) {
      return observation.combat !== null
        ? { status: "matched", reason: "combat_room_ready" }
        : { status: "pending", reason: "combat_summary_unavailable" };
    }

    if (isInteractiveReadyState(observation.context.stateType)) {
      return hasPrimaryAction(observation.actions, observation.context.stateType)
        ? { status: "matched", reason: "interactive_room_ready" }
        : { status: "pending", reason: "missing_primary_action" };
    }

    return { status: "pending", reason: "state_not_room_ready_compatible" };
  }

  if (condition === "run_active") {
    if (observation.context.stateType === "menu" || observation.context.stateType === "unknown" || observation.context.stateType === "overlay") {
      return { status: "pending", reason: "run_not_active" };
    }

    if (isCombatState(observation.context.stateType)) {
      return observation.combat !== null
        ? { status: "matched", reason: "combat_run_active" }
        : { status: "pending", reason: "combat_summary_unavailable" };
    }

    if (isInteractiveReadyState(observation.context.stateType)) {
      return hasPrimaryAction(observation.actions, observation.context.stateType)
        ? { status: "matched", reason: "interactive_run_active" }
        : { status: "pending", reason: "missing_primary_action" };
    }

    return { status: "matched", reason: "stable_run_active" };
  }

  if (!contextStateTypeMatches(observation.context, condition)) {
    return { status: "pending", reason: `state_mismatch:${observation.context.stateType}` };
  }

  if (condition === "player_ready") {
    return { status: "pending", reason: "unreachable_player_ready_branch" };
  }

  if (condition === "player_turn" || condition === "enemy_turn") {
    return { status: "pending", reason: "unreachable_turn_branch" };
  }

  if (isCombatState(observation.context.stateType)) {
    return observation.combat !== null
      ? { status: "matched", reason: "combat_state_matched" }
      : { status: "pending", reason: "combat_summary_unavailable" };
  }

  if (isInteractiveReadyState(observation.context.stateType)) {
    return hasPrimaryAction(observation.actions, observation.context.stateType)
      ? { status: "matched", reason: "interactive_state_matched" }
      : { status: "pending", reason: "missing_primary_action" };
  }

  return { status: "matched", reason: "stable_state_matched" };
}

function buildObservationStabilityKey(observation: WaitObservation): string {
  return JSON.stringify({
    context: observation.context,
    combat: observation.combat,
    actions: observation.actions,
    overlay: observation.overlay
  });
}

function parseWaitTimeout(rawValue: string): number {
  const timeout = Number(rawValue);
  if (!Number.isInteger(timeout) || timeout < 0) {
    throw new CliError(`TIMEOUT_SECONDS must be an integer: ${rawValue}`);
  }

  return timeout;
}

export async function waitForCondition(client: RequestClient, rawCondition: string, timeoutSeconds: number, options: WaitOptions = {}): Promise<WaitResult> {
  const condition = normalizeWaitCondition(rawCondition);
  const deadline = Date.now() + timeoutSeconds * 1000;
  let previousMatchedKey: string | null = null;
  const trace: WaitTraceEntry[] = [];
  let poll = 0;

  while (Date.now() < deadline) {
    const observation = await readWaitObservation(client, condition);
    const evaluation = evaluateObservation(observation, condition);
    poll++;

    if (options.verbose) {
      trace.push(buildWaitTraceEntry(poll, evaluation, observation));
    }

    if (evaluation.status === "terminal") {
      const result: WaitTerminalResult = {
        condition,
        status: "terminal",
        matched: false,
        terminal: true,
        terminalReason: evaluation.terminalReason ?? "overlay",
        context: observation.context,
        overlay: observation.overlay ?? undefined
      };
      if (options.verbose) {
        result.trace = trace;
      }

      return result;
    }

    if (evaluation.status === "matched") {
      const stabilityKey = buildObservationStabilityKey(observation);
      if (stabilityKey === previousMatchedKey) {
        const result: WaitMatchedResult = observation.combat === null
          ? { condition, status: "matched", matched: true, terminal: false, context: observation.context }
          : { condition, status: "matched", matched: true, terminal: false, context: observation.context, combat: observation.combat };
        if (options.verbose) {
          result.trace = trace;
        }

        return result;
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
  const timeoutPayload = {
    condition,
    latestContext,
    trace: options.verbose ? trace : undefined
  };
  throw new CliError(`Timed out waiting for condition: ${condition}\n${JSON.stringify(timeoutPayload, null, 2)}`);
}
