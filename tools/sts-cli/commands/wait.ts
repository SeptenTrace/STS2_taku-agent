import { POLL_INTERVAL_MS } from "../config.ts";
import { CliError, HttpError } from "../core/errors.ts";
import { HttpClient } from "../core/http.ts";
import { requireJsonObject, type JsonObject, type JsonValue } from "../core/json.ts";

export interface WaitResult {
  condition: string;
  matched: true;
  context: JsonValue;
  combat?: JsonValue;
}

export function normalizeWaitCondition(condition: string): string {
  return condition.trim().replaceAll("-", "_").toLowerCase();
}

function contextStateTypeMatches(context: JsonObject, expected: string): boolean {
  return context.stateType === expected;
}

function combatSideMatches(combat: JsonObject | null, expected: string): boolean {
  return combat !== null && combat.side === expected;
}

async function maybeReadCombatSummary(client: HttpClient, context: JsonObject): Promise<JsonObject | null> {
  if (!["monster", "elite", "boss"].includes(String(context.stateType ?? ""))) {
    return null;
  }

  try {
    const combat = await client.request("/api/v1/combat/summary");
    return requireJsonObject(combat, "Combat summary response must be an object.");
  } catch (error) {
    if (error instanceof HttpError && error.status === 409) {
      return null;
    }

    throw error;
  }
}

export async function waitForCondition(client: HttpClient, rawCondition: string, timeoutSeconds: number): Promise<WaitResult> {
  const condition = normalizeWaitCondition(rawCondition);
  const deadline = Date.now() + timeoutSeconds * 1000;

  while (Date.now() < deadline) {
    const context = requireJsonObject(
      await client.request("/api/v1/context"),
      "Context response must be an object."
    );

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

  const latestContext = await client.request("/api/v1/context");
  throw new CliError(`Timed out waiting for condition: ${condition}\n${JSON.stringify(latestContext, null, 2)}`);
}
