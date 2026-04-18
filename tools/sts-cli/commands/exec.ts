import { DEFAULT_WAIT_TIMEOUT_SECONDS } from "../config.ts";
import { CliError } from "../core/errors.ts";
import { parseScalar, type JsonObject } from "../core/json.ts";

export interface ExecInvocation {
  payload: JsonObject;
  waitFor?: string;
  timeoutSeconds: number;
  waitVerbose: boolean;
}

const DEFAULT_WAIT_CONDITIONS: Readonly<Record<string, string>> = {
  continue_game: "run_active",
  end_turn: "player_turn",
  choose_map_node: "room_ready",
  proceed: "room_ready",
  skip_card_reward: "room_ready",
  crystal_sphere_proceed: "room_ready"
};

export function buildExecPayload(action: string, args: string[]): JsonObject {
  if (!action) {
    throw new CliError("Usage: sts exec ACTION [key=value ...]");
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

export function buildExecInvocation(action: string, args: string[]): ExecInvocation {
  const actionArgs: string[] = [];
  let waitFor: string | undefined;
  let timeoutSeconds = DEFAULT_WAIT_TIMEOUT_SECONDS;
  let waitVerbose = false;

  for (let index = 0; index < args.length; index++) {
    const arg = args[index]!;
    if (arg === "--wait-for") {
      waitFor = args[index + 1];
      if (!waitFor) {
        throw new CliError("Usage: sts exec ACTION ... [--wait-for CONDITION] [--timeout SECONDS]");
      }

      index++;
      continue;
    }

    if (arg.startsWith("--wait-for=")) {
      waitFor = arg.slice("--wait-for=".length);
      if (!waitFor) {
        throw new CliError("Usage: sts exec ACTION ... [--wait-for CONDITION] [--timeout SECONDS]");
      }

      continue;
    }

    if (arg === "--timeout") {
      const timeoutRaw = args[index + 1];
      if (timeoutRaw === undefined) {
        throw new CliError("Usage: sts exec ACTION ... [--wait-for CONDITION] [--timeout SECONDS]");
      }

      timeoutSeconds = parseExecTimeout(timeoutRaw);
      index++;
      continue;
    }

    if (arg.startsWith("--timeout=")) {
      timeoutSeconds = parseExecTimeout(arg.slice("--timeout=".length));
      continue;
    }

    if (arg === "--wait-for-ready") {
      waitFor = "player_ready";
      continue;
    }

    if (arg === "--wait-for-room") {
      waitFor = "room_ready";
      continue;
    }

    if (arg === "--wait-for-run") {
      waitFor = "run_active";
      continue;
    }

    if (arg === "--wait-verbose") {
      waitVerbose = true;
      continue;
    }

    actionArgs.push(arg);
  }

  if (!waitFor) {
    waitFor = DEFAULT_WAIT_CONDITIONS[action];
  }

  if (waitVerbose && !waitFor) {
    throw new CliError("`--wait-verbose` requires a wait target such as `--wait-for`, `--wait-for-ready`, `--wait-for-room`, `--wait-for-run`, or an action with a built-in default wait.");
  }

  return {
    payload: buildExecPayload(action, actionArgs),
    waitFor,
    timeoutSeconds,
    waitVerbose
  };
}

function parseExecTimeout(rawValue: string): number {
  const timeout = Number(rawValue);
  if (!Number.isInteger(timeout) || timeout < 0) {
    throw new CliError(`TIMEOUT_SECONDS must be an integer: ${rawValue}`);
  }

  return timeout;
}
