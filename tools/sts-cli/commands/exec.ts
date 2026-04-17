import { CliError } from "../core/errors.ts";
import { parseScalar, type JsonObject } from "../core/json.ts";

export function buildExecPayload(action: string, args: string[]): JsonObject {
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
