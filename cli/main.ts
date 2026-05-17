import { BASE_URL } from "./config.ts";
import { dispatch } from "./commands/dispatch.ts";
import { CliError, HttpError } from "./core/errors.ts";
import { HttpClient } from "./core/http.ts";
import { printJson, StreamOutput } from "./core/output.ts";
import { createTelemetrySession } from "./core/telemetry.ts";

async function main(): Promise<void> {
  const [, , rawCommand, ...args] = process.argv;
  const telemetry = createTelemetrySession(rawCommand ?? "help", args, BASE_URL);
  const client = telemetry.wrapClient(new HttpClient(BASE_URL));

  try {
    await dispatch(client, rawCommand ?? "help", args, new StreamOutput());
    telemetry.writeSuccess();
  } catch (error: unknown) {
    telemetry.writeError(error);
    throw error;
  }
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
