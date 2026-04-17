type JsonPrimitive = string | number | boolean | null;
type JsonValue = JsonPrimitive | JsonObject | JsonValue[];

interface JsonObject {
  [key: string]: JsonValue;
}

interface RequestOptions {
  method?: "GET" | "POST";
  body?: JsonObject;
}

interface WaitResult {
  condition: string;
  matched: true;
  context: JsonValue;
  combat?: JsonValue;
}

const BASE_URL = process.env.STS_OBSERVER_URL ?? "http://127.0.0.1:15527";
const POLL_INTERVAL_MS = 200;
const DEFAULT_WAIT_TIMEOUT_SECONDS = 15;

const usage = `Usage:
  ./sts help
  ./sts ping
  ./sts capabilities
  ./sts context
  ./sts next
  ./sts compact
  ./sts delta
  ./sts actions
  ./sts run
  ./sts knowledge [current|cards|relics|potions|status]
  ./sts player [summary|deck|relics|potions|status]
  ./sts combat [summary|actions|hand|enemies|piles]
  ./sts map
  ./sts event
  ./sts shop
  ./sts rest-site
  ./sts rewards
  ./sts card-reward
  ./sts card-selection
  ./sts treasure
  ./sts wait CONDITION [TIMEOUT_SECONDS]
  ./sts exec ACTION [INDEX] [TARGET]
  ./sts exec ACTION [key=value ...]
  ./sts full
  ./sts get /api/v1/...

Environment:
  STS_OBSERVER_URL   Override observer server base URL.

Examples:
  ./sts ping
  ./sts next
  ./sts actions
  ./sts combat actions
  ./sts player summary
  ./sts knowledge cards
  ./sts wait player_turn
  ./sts wait rewards 10
  ./sts exec play_card 0 jaw_worm_0
  ./sts exec select_card 1
  ./sts exec end_turn
  ./sts get /api/v1/state/full`;

class CliError extends Error {
  exitCode: number;

  constructor(message: string, exitCode = 1) {
    super(message);
    this.exitCode = exitCode;
  }
}

class HttpError extends Error {
  status: number;
  method: string;
  path: string;
  body: unknown;

  constructor(status: number, method: string, path: string, body: unknown) {
    super(`HTTP ${status} for ${method} ${path}`);
    this.status = status;
    this.method = method;
    this.path = path;
    this.body = body;
  }
}

function printJson(value: unknown, stream: NodeJS.WriteStream = process.stdout): void {
  stream.write(`${JSON.stringify(value, null, 2)}\n`);
}

function printUsage(stream: NodeJS.WriteStream = process.stdout): void {
  stream.write(`${usage}\n`);
}

function normalizeWaitCondition(condition: string): string {
  return condition.trim().replaceAll("-", "_").toLowerCase();
}

function isJsonObject(value: unknown): value is JsonObject {
  return typeof value === "object" && value !== null && !Array.isArray(value);
}

function parseJsonOrText(text: string): unknown {
  if (!text.trim()) {
    return {};
  }

  try {
    return JSON.parse(text) as unknown;
  } catch {
    return text;
  }
}

async function request(path: string, options: RequestOptions = {}): Promise<unknown> {
  const method = options.method ?? "GET";
  const response = await fetch(`${BASE_URL}${path}`, {
    method,
    headers: options.body ? { "Content-Type": "application/json" } : undefined,
    body: options.body ? JSON.stringify(options.body) : undefined
  });

  const text = await response.text();
  const parsed = parseJsonOrText(text);

  if (!response.ok) {
    throw new HttpError(response.status, method, path, parsed);
  }

  return parsed;
}

function requireJsonObject(value: unknown, errorMessage: string): JsonObject {
  if (!isJsonObject(value)) {
    throw new CliError(errorMessage);
  }

  return value;
}

function parseScalar(value: string): JsonValue {
  if (/^-?\d+$/.test(value)) {
    return Number(value);
  }

  if (value === "true") {
    return true;
  }

  if (value === "false") {
    return false;
  }

  if (value === "null") {
    return null;
  }

  return value;
}

function buildExecPayload(action: string, args: string[]): JsonObject {
  if (!action) {
    throw new CliError("Usage: ./sts exec ACTION [key=value ...]");
  }

  const parameters: JsonObject = {};

  if (args.length > 0 && !args[0].includes("=")) {
    if (args.length > 2) {
      throw new CliError("Positional exec form supports at most ACTION [INDEX] [TARGET]");
    }

    parameters.index = parseScalar(args[0]);
    if (args[1] !== undefined) {
      parameters.target = parseScalar(args[1]);
    }

    return {
      actionType: action,
      parameters
    };
  }

  for (const pair of args) {
    const separator = pair.indexOf("=");
    if (separator <= 0) {
      throw new CliError(`Execution arguments must use key=value format: ${pair}`);
    }

    const key = pair.slice(0, separator);
    const value = pair.slice(separator + 1);
    parameters[key] = parseScalar(value);
  }

  return {
    actionType: action,
    parameters
  };
}

function contextStateTypeMatches(context: JsonObject, expected: string): boolean {
  return context.stateType === expected;
}

function combatSideMatches(combat: JsonObject | null, expected: string): boolean {
  return combat !== null && combat.side === expected;
}

async function maybeReadCombatSummary(context: JsonObject): Promise<JsonObject | null> {
  if (!["monster", "elite", "boss"].includes(String(context.stateType ?? ""))) {
    return null;
  }

  try {
    const combat = await request("/api/v1/combat/summary");
    return requireJsonObject(combat, "Combat summary response must be an object.");
  } catch (error) {
    if (error instanceof HttpError && error.status === 409) {
      return null;
    }

    throw error;
  }
}

async function waitForCondition(rawCondition: string, timeoutSeconds: number): Promise<WaitResult> {
  const condition = normalizeWaitCondition(rawCondition);
  const deadline = Date.now() + timeoutSeconds * 1000;

  while (Date.now() < deadline) {
    const context = requireJsonObject(await request("/api/v1/context"), "Context response must be an object.");
    const combat = condition === "player_turn" || condition === "enemy_turn"
      ? await maybeReadCombatSummary(context)
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

  const latestContext = await request("/api/v1/context");
  throw new CliError(`Timed out waiting for condition: ${condition}\n${JSON.stringify(latestContext, null, 2)}`);
}

async function commandKnowledge(section = "current"): Promise<void> {
  const pathBySection: Record<string, string> = {
    current: "/api/v1/knowledge/current",
    cards: "/api/v1/knowledge/cards",
    relics: "/api/v1/knowledge/relics",
    potions: "/api/v1/knowledge/potions",
    status: "/api/v1/knowledge/status"
  };

  const path = pathBySection[section];
  if (!path) {
    throw new CliError(`Unknown knowledge subcommand: ${section}`);
  }

  printJson(await request(path));
}

async function commandPlayer(section = "summary"): Promise<void> {
  const pathBySection: Record<string, string> = {
    summary: "/api/v1/player/summary",
    deck: "/api/v1/player/deck",
    relics: "/api/v1/player/relics",
    potions: "/api/v1/player/potions",
    status: "/api/v1/player/status"
  };

  const path = pathBySection[section];
  if (!path) {
    throw new CliError(`Unknown player subcommand: ${section}`);
  }

  printJson(await request(path));
}

async function commandCombat(section = "summary"): Promise<void> {
  const pathBySection: Record<string, string> = {
    summary: "/api/v1/combat/summary",
    actions: "/api/v1/combat/actions",
    hand: "/api/v1/combat/hand",
    enemies: "/api/v1/combat/enemies",
    piles: "/api/v1/combat/piles"
  };

  const path = pathBySection[section];
  if (!path) {
    throw new CliError(`Unknown combat subcommand: ${section}`);
  }

  printJson(await request(path));
}

async function commandNext(): Promise<void> {
  const [context, observation] = await Promise.all([
    request("/api/v1/context"),
    request("/api/v1/observation/compact")
  ]);

  printJson({ context, observation });
}

async function dispatch(command: string, args: string[]): Promise<void> {
  switch (command) {
    case "help":
    case "-h":
    case "--help":
      printUsage();
      return;
    case "ping":
      printJson(await request("/"));
      return;
    case "capabilities":
      printJson(await request("/api/v1/capabilities"));
      return;
    case "context":
      printJson(await request("/api/v1/context"));
      return;
    case "next":
      await commandNext();
      return;
    case "compact":
    case "observe":
    case "observation":
      printJson(await request("/api/v1/observation/compact"));
      return;
    case "delta":
      printJson(await request("/api/v1/observation/delta"));
      return;
    case "actions":
      printJson(await request("/api/v1/actions"));
      return;
    case "run":
      printJson(await request("/api/v1/run"));
      return;
    case "knowledge":
      await commandKnowledge(args[0]);
      return;
    case "player":
      await commandPlayer(args[0]);
      return;
    case "combat":
      await commandCombat(args[0]);
      return;
    case "map":
      printJson(await request("/api/v1/map/summary"));
      return;
    case "event":
      printJson(await request("/api/v1/event"));
      return;
    case "shop":
      printJson(await request("/api/v1/shop"));
      return;
    case "rest":
    case "rest-site":
      printJson(await request("/api/v1/rest-site"));
      return;
    case "rewards":
      printJson(await request("/api/v1/rewards"));
      return;
    case "card-reward":
      printJson(await request("/api/v1/card-reward"));
      return;
    case "card-select":
    case "card-selection":
      printJson(await request("/api/v1/card-selection"));
      return;
    case "treasure":
      printJson(await request("/api/v1/treasure"));
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

      printJson(await waitForCondition(condition, timeout));
      return;
    }
    case "exec":
    case "do":
    case "act": {
      const action = args[0] ?? "";
      const payload = buildExecPayload(action, args.slice(1));
      printJson(await request("/api/v1/actions/execute", { method: "POST", body: payload }));
      return;
    }
    case "full":
      printJson(await request("/api/v1/state/full"));
      return;
    case "get": {
      const path = args[0];
      if (!path) {
        throw new CliError("Usage: ./sts get /api/v1/...");
      }

      printJson(await request(path));
      return;
    }
    default:
      throw new CliError(`Unknown command: ${command}\n\n${usage}`);
  }
}

async function main(): Promise<void> {
  const [, , rawCommand, ...args] = process.argv;
  await dispatch(rawCommand ?? "help", args);
}

main().catch((error: unknown) => {
  if (error instanceof HttpError) {
    process.stderr.write(`${error.message}\n`);
    printJson(error.body, process.stderr);
    process.exit(22);
  }

  if (error instanceof CliError) {
    process.stderr.write(`${error.message}\n`);
    process.exit(error.exitCode);
  }

  process.stderr.write(`${error instanceof Error ? error.stack ?? error.message : String(error)}\n`);
  process.exit(1);
});
