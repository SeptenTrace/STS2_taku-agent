import { BASE_URL } from "./config.ts";
import { dispatch } from "./commands/dispatch.ts";
import { CliError, HttpError } from "./core/errors.ts";
import { HttpClient } from "./core/http.ts";
import { printJson, StreamOutput } from "./core/output.ts";

async function main(): Promise<void> {
  const [, , rawCommand, ...args] = process.argv;
  const client = new HttpClient(BASE_URL);
  await dispatch(client, rawCommand ?? "help", args, new StreamOutput());
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
