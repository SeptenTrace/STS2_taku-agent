import type {
  ActionExecutionResponse,
  ActionSurfaceResponse,
  BundleSelectionResponse,
  CapabilitiesResponse,
  CardRewardResponse,
  ContextResponse,
  CrystalSphereResponse,
  EventResponse,
  FakeMerchantResponse,
  MapSummaryResponse,
  MenuResponse,
  ObservationCompactResponse,
  OverlayResponse,
  PingResponse,
  PlayerSummaryResponse,
  RelicSelectionResponse,
  RewardsResponse,
  ShopResponse
} from "../api-types.ts";
import type { RequestClient } from "../core/client.ts";
import { DEFAULT_WAIT_TIMEOUT_SECONDS } from "../config.ts";
import { CliError } from "../core/errors.ts";
import { StreamOutput, type Output } from "../core/output.ts";
import { buildCombatSnapshot, buildRoomSnapshot, buildRoomSummary, claimAllSafeRewards, type RoomSnapshotDetail } from "./combo.ts";
import { runDoctor } from "./doctor.ts";
import { buildExecInvocation } from "./exec.ts";
import { buildWaitInvocation, waitForCondition } from "./wait.ts";
import { usage } from "../usage.ts";
import { randomUUID } from "node:crypto";

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

async function executeAction(
  client: RequestClient,
  output: Output,
  action: string,
  args: string[]
): Promise<void> {
  const invocation = buildExecInvocation(action, args);
  const correlationId = randomUUID();
  const execution = await client.request<ActionExecutionResponse>("/api/v1/actions/execute", {
    method: "POST",
    body: invocation.payload,
    headers: {
      "X-Sts-Correlation-Id": correlationId
    }
  });

  if (invocation.waitFor && execution.status === "ok") {
    output.printJson({
      execution,
      wait: await waitForCondition(client, invocation.waitFor, invocation.timeoutSeconds ?? DEFAULT_WAIT_TIMEOUT_SECONDS, { verbose: invocation.waitVerbose })
    });
    return;
  }

  output.printJson(execution);
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

function parseRoomSnapshotDetail(args: string[]): RoomSnapshotDetail {
  for (let index = 0; index < args.length; index++) {
    const arg = args[index]!;
    if (arg === "--detail") {
      const detail = args[index + 1];
      if (detail === "full" || detail === "standard") {
        return detail;
      }

      throw new CliError("Usage: sts room snapshot [--detail standard|full]");
    }

    if (arg.startsWith("--detail=")) {
      const detail = arg.slice("--detail=".length);
      if (detail === "full" || detail === "standard") {
        return detail;
      }

      throw new CliError("Usage: sts room snapshot [--detail standard|full]");
    }
  }

  return "standard";
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
      await printRequest<PingResponse>(client, output, "/");
      return;
    case "capabilities":
      await printRequest<CapabilitiesResponse>(client, output, "/api/v1/capabilities");
      return;
    case "context":
      await printRequest<ContextResponse>(client, output, "/api/v1/context");
      return;
    case "menu":
      await printRequest<MenuResponse>(client, output, "/api/v1/menu");
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
      if ((args[0] ?? "summary") === "summary") {
        await printRequest<PlayerSummaryResponse>(client, output, "/api/v1/player/summary");
        return;
      }

      await commandFromMap(client, output, args[0], playerPaths, "player");
      return;
    case "combat":
      if ((args[0] ?? "").toLowerCase() === "snapshot") {
        output.printJson(await buildCombatSnapshot(client));
        return;
      }

      await commandFromMap(client, output, args[0], combatPaths, "combat");
      return;
    case "map":
      await printRequest<MapSummaryResponse>(client, output, "/api/v1/map/summary");
      return;
    case "event":
      await printRequest<EventResponse>(client, output, "/api/v1/event");
      return;
    case "fake-merchant":
      await printRequest<FakeMerchantResponse>(client, output, "/api/v1/fake-merchant");
      return;
    case "shop":
      await printRequest<ShopResponse>(client, output, "/api/v1/shop");
      return;
    case "rest":
    case "rest-site":
      await printRequest(client, output, "/api/v1/rest-site");
      return;
    case "rewards":
      if (args[0] === "claim-all-safe") {
        output.printJson(await claimAllSafeRewards(client));
        return;
      }

      await printRequest<RewardsResponse>(client, output, "/api/v1/rewards");
      return;
    case "card-reward":
      if ((args[0] ?? "").toLowerCase() === "skip") {
        await executeAction(client, output, "skip_card_reward", args.slice(1));
        return;
      }

      await printRequest<CardRewardResponse>(client, output, "/api/v1/card-reward");
      return;
    case "card-select":
    case "card-selection":
      await printRequest(client, output, "/api/v1/card-selection");
      return;
    case "bundle":
    case "bundle-selection":
      await printRequest<BundleSelectionResponse>(client, output, "/api/v1/bundle-selection");
      return;
    case "relic":
    case "relic-selection":
      await printRequest<RelicSelectionResponse>(client, output, "/api/v1/relic-selection");
      return;
    case "crystal-sphere":
      await printRequest<CrystalSphereResponse>(client, output, "/api/v1/crystal-sphere");
      return;
    case "treasure":
      await printRequest(client, output, "/api/v1/treasure");
      return;
    case "overlay":
      await printRequest<OverlayResponse>(client, output, "/api/v1/overlay");
      return;
    case "doctor":
      output.printJson(await runDoctor(client));
      return;
    case "wait": {
      const invocation = buildWaitInvocation(args, DEFAULT_WAIT_TIMEOUT_SECONDS);
      output.printJson(await waitForCondition(client, invocation.condition, invocation.timeoutSeconds, { verbose: invocation.verbose }));
      return;
    }
    case "room": {
      const subcommand = (args[0] ?? "summary").toLowerCase();
      if (subcommand === "summary") {
        output.printJson(await buildRoomSummary(client));
        return;
      }

      if (subcommand === "snapshot") {
        output.printJson(await buildRoomSnapshot(client, parseRoomSnapshotDetail(args.slice(1))));
        return;
      }

      if (subcommand !== "summary") {
        throw new CliError(`Unknown room subcommand: ${subcommand}`);
      }
    }
    case "exec":
    case "do":
    case "act": {
      const action = args[0] ?? "";
      await executeAction(client, output, action, args.slice(1));
      return;
    }
    case "full":
      await printRequest(client, output, "/api/v1/state/full");
      return;
    case "get": {
      const path = args[0];
      if (!path) {
        throw new CliError("Usage: sts get /api/v1/...");
      }

      await printRequest(client, output, path);
      return;
    }
    default:
      throw new CliError(`Unknown command: ${command}\n\n${usage}`);
  }
}
