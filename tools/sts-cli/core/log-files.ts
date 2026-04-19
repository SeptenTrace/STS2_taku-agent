import { homedir } from "node:os";
import { join } from "node:path";

export type LogFileKind = "action-execution" | "action-history" | "cli-command";

export const LOG_FILE_PATHS: Readonly<Record<LogFileKind, string>> = {
  "action-execution": "action-execution.jsonl",
  "action-history": "action-history.jsonl",
  "cli-command": "cli-command.jsonl"
};

export function getLogDirectory(): string {
  return process.env.STS_LOG_DIR
    ?? join(homedir(), "Library", "Application Support", "STS2TakuAgent", "phase1-feasibility");
}

export function resolveLogFilePath(kind: LogFileKind): string {
  return join(getLogDirectory(), LOG_FILE_PATHS[kind]);
}
