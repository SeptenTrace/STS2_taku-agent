import { POLL_INTERVAL_MS } from "../config.ts";
import { CliError, HttpError } from "../core/errors.ts";
import type { RequestClient } from "../core/client.ts";
import type { CombatSummaryResponse, ContextResponse } from "../api-types.ts";

export interface WaitResult {
  condition: string;
  matched: true;
  context: ContextResponse;
  combat?: CombatSummaryResponse;
}

export function normalizeWaitCondition(condition: string): string {
  return condition.trim().replaceAll("-", "_").toLowerCase();
}

function contextStateTypeMatches(context: ContextResponse, expected: string): boolean {
  return context.stateType === expected;
}

function combatSideMatches(combat: CombatSummaryResponse | null, expected: string): boolean {
  return combat !== null && combat.side === expected;
}

async function maybeReadCombatSummary(client: RequestClient, context: ContextResponse): Promise<CombatSummaryResponse | null> {
  if (!["monster", "elite", "boss"].includes(String(context.stateType ?? ""))) {
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

export async function waitForCondition(client: RequestClient, rawCondition: string, timeoutSeconds: number): Promise<WaitResult> {
  const condition = normalizeWaitCondition(rawCondition);
  const deadline = Date.now() + timeoutSeconds * 1000;

  while (Date.now() < deadline) {
    const context = await client.request<ContextResponse>("/api/v1/context");

    const combat = condition === "player_turn" || condition === "enemy_turn"
      ? await maybeReadCombatSummary(client, context)
      : null;

    const matched = (() => {
      if (condition === "player_turn") {
        return ["monster", "elite", "boss"].includes(String(context.stateType ?? "")) &&
          combatSideMatches(combat, "player");
      }

      if (condition === "enemy_turn") {
        return ["monster", "elite", "boss"].includes(String(context.stateType ?? "")) &&
          combatSideMatches(combat, "enemy");
      }

      return contextStateTypeMatches(context, condition);
    })();

    if (matched) {
      return combat === null
        ? { condition, matched: true, context }
        : { condition, matched: true, context, combat };
    }

    await new Promise((resolve) => setTimeout(resolve, POLL_INTERVAL_MS));
  }

  const latestContext = await client.request<ContextResponse>("/api/v1/context");
  throw new CliError(`Timed out waiting for condition: ${condition}\n${JSON.stringify(latestContext, null, 2)}`);
}
