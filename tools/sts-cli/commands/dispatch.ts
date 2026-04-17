import type {
  ActionSurfaceResponse,
  CardRewardResponse,
  ContextResponse,
  ObservationCompactResponse,
  RewardsResponse
} from "../api-types.ts";
import type { RequestClient } from "../core/client.ts";
import { DEFAULT_WAIT_TIMEOUT_SECONDS } from "../config.ts";
import { CliError } from "../core/errors.ts";
import { StreamOutput, type Output } from "../core/output.ts";
import { buildExecPayload } from "./exec.ts";
import { waitForCondition } from "./wait.ts";
import { usage } from "../usage.ts";

const knowledgePaths: Record<string, string> = {
  current: "/api/v1/knowledge/current",
  cards: "/api/v1/knowledge/cards",
  relics: "/api/v1/knowledge/relics",
  potions: "/api/v1/knowledge/potions",
  status: "/api/v1/knowledge/status"
};

const playerPaths: Record<string, string> = {
  summary: "/api/v1/player/summary",
  deck: "/api/v1/player/deck",
  relics: "/api/v1/player/relics",
  potions: "/api/v1/player/potions",
  status: "/api/v1/player/status"
};

const combatPaths: Record<string, string> = {
  summary: "/api/v1/combat/summary",
  actions: "/api/v1/combat/actions",
  hand: "/api/v1/combat/hand",
  enemies: "/api/v1/combat/enemies",
  piles: "/api/v1/combat/piles"
};

async function printRequest<T>(client: RequestClient, output: Output, path: string): Promise<void> {
  output.printJson(await client.request<T>(path));
}

async function commandNext(client: RequestClient, output: Output): Promise<void> {
  const [context, observation] = await Promise.all([
    client.request<ContextResponse>("/api/v1/context"),
    client.request<ObservationCompactResponse>("/api/v1/observation/compact")
  ]);

  output.printJson({ context, observation });
}

async function commandFromMap(client: RequestClient, output: Output, section: string | undefined, pathMap: Record<string, string>, label: string): Promise<void> {
  const resolvedSection = section ?? Object.keys(pathMap)[0];
  const path = pathMap[resolvedSection];
  if (!path) {
    throw new CliError(`Unknown ${label} subcommand: ${resolvedSection}`);
  }

  await printRequest(client, output, path);
}

export async function dispatch(
  client: RequestClient,
  command: string,
  args: string[],
  output: Output = new StreamOutput()
): Promise<void> {
  switch (command) {
    case "help":
    case "-h":
    case "--help":
      output.printText(usage);
      return;
    case "ping":
      await printRequest(client, output, "/");
      return;
    case "capabilities":
      await printRequest(client, output, "/api/v1/capabilities");
      return;
    case "context":
      await printRequest<ContextResponse>(client, output, "/api/v1/context");
      return;
    case "next":
      await commandNext(client, output);
      return;
    case "compact":
    case "observe":
    case "observation":
      await printRequest<ObservationCompactResponse>(client, output, "/api/v1/observation/compact");
      return;
    case "delta":
      await printRequest(client, output, "/api/v1/observation/delta");
      return;
    case "actions":
      await printRequest<ActionSurfaceResponse>(client, output, "/api/v1/actions");
      return;
    case "run":
      await printRequest(client, output, "/api/v1/run");
      return;
    case "knowledge":
      await commandFromMap(client, output, args[0], knowledgePaths, "knowledge");
      return;
    case "player":
      await commandFromMap(client, output, args[0], playerPaths, "player");
      return;
    case "combat":
      await commandFromMap(client, output, args[0], combatPaths, "combat");
      return;
    case "map":
      await printRequest(client, output, "/api/v1/map/summary");
      return;
    case "event":
      await printRequest(client, output, "/api/v1/event");
      return;
    case "shop":
      await printRequest(client, output, "/api/v1/shop");
      return;
    case "rest":
    case "rest-site":
      await printRequest(client, output, "/api/v1/rest-site");
      return;
    case "rewards":
      await printRequest<RewardsResponse>(client, output, "/api/v1/rewards");
      return;
    case "card-reward":
      await printRequest<CardRewardResponse>(client, output, "/api/v1/card-reward");
      return;
    case "card-select":
    case "card-selection":
      await printRequest(client, output, "/api/v1/card-selection");
      return;
    case "treasure":
      await printRequest(client, output, "/api/v1/treasure");
      return;
    case "wait": {
      const condition = args[0];
      if (!condition) {
        throw new CliError("Usage: ./sts wait CONDITION [TIMEOUT_SECONDS]");
      }

      const timeoutRaw = args[1];
      const timeout = timeoutRaw === undefined ? DEFAULT_WAIT_TIMEOUT_SECONDS : Number(timeoutRaw);
      if (!Number.isInteger(timeout) || timeout < 0) {
        throw new CliError(`TIMEOUT_SECONDS must be an integer: ${timeoutRaw}`);
      }

      output.printJson(await waitForCondition(client, condition, timeout));
      return;
    }
    case "exec":
    case "do":
    case "act": {
      const action = args[0] ?? "";
      const payload = buildExecPayload(action, args.slice(1));
      output.printJson(await client.request("/api/v1/actions/execute", { method: "POST", body: payload }));
      return;
    }
    case "full":
      await printRequest(client, output, "/api/v1/state/full");
      return;
    case "get": {
      const path = args[0];
      if (!path) {
        throw new CliError("Usage: ./sts get /api/v1/...");
      }

      await printRequest(client, output, path);
      return;
    }
    default:
      throw new CliError(`Unknown command: ${command}\n\n${usage}`);
  }
}
