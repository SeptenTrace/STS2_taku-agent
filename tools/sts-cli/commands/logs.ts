import { existsSync, readFileSync } from "node:fs";
import { homedir } from "node:os";
import { join } from "node:path";

import { CliError } from "../core/errors.ts";

type LogFileKind = "action-execution" | "action-history";

export interface LogReadResult {
  kind: LogFileKind;
  path: string;
  entryCount: number;
  entries: unknown[];
}

interface LogReadOptions {
  last: number;
}

const DEFAULT_TAIL_COUNT = 20;
const LOG_FILE_PATHS: Readonly<Record<LogFileKind, string>> = {
  "action-execution": "action-execution.jsonl",
  "action-history": "action-history.jsonl"
};

function getLogDirectory(): string {
  return process.env.STS_LOG_DIR
    ?? join(homedir(), "Library", "Application Support", "STS2TakuAgent", "phase1-feasibility");
}

function resolveLogFilePath(kind: LogFileKind): string {
  return join(getLogDirectory(), LOG_FILE_PATHS[kind]);
}

function parsePositiveInt(raw: string, flagName: string): number {
  const value = Number(raw);
  if (!Number.isInteger(value) || value <= 0) {
    throw new CliError(`${flagName} must be a positive integer: ${raw}`);
  }

  return value;
}

function readJsonLines(path: string): unknown[] {
  if (!existsSync(path)) {
    return [];
  }

  const text = readFileSync(path, "utf8");
  return text
    .split(/\r?\n/)
    .map((line) => line.trim())
    .filter((line) => line.length > 0)
    .map((line) => JSON.parse(line));
}

function tailEntries(entries: unknown[], last: number): unknown[] {
  return entries.slice(Math.max(entries.length - last, 0));
}

export function readLogTail(args: string[]): LogReadResult {
  let kind: LogFileKind = "action-execution";
  let last = DEFAULT_TAIL_COUNT;

  for (let index = 0; index < args.length; index++) {
    const arg = args[index]!;
    if (arg === "--file") {
      const next = args[index + 1];
      if (next !== "action-execution" && next !== "action-history") {
        throw new CliError("Usage: sts logs tail [--file action-execution|action-history] [--last N]");
      }

      kind = next;
      index++;
      continue;
    }

    if (arg.startsWith("--file=")) {
      const value = arg.slice("--file=".length);
      if (value !== "action-execution" && value !== "action-history") {
        throw new CliError("Usage: sts logs tail [--file action-execution|action-history] [--last N]");
      }

      kind = value;
      continue;
    }

    if (arg === "--last") {
      const next = args[index + 1];
      if (!next) {
        throw new CliError("Usage: sts logs tail [--file action-execution|action-history] [--last N]");
      }

      last = parsePositiveInt(next, "--last");
      index++;
      continue;
    }

    if (arg.startsWith("--last=")) {
      last = parsePositiveInt(arg.slice("--last=".length), "--last");
      continue;
    }

    throw new CliError(`Unknown logs tail argument: ${arg}`);
  }

  const path = resolveLogFilePath(kind);
  const entries = tailEntries(readJsonLines(path), last);
  return {
    kind,
    path,
    entryCount: entries.length,
    entries
  };
}

export function readLogsByCorrelation(correlationId: string, args: string[]): LogReadResult & { correlationId: string } {
  if (!correlationId) {
    throw new CliError("Usage: sts logs correlation CORRELATION_ID [--last N]");
  }

  let last = DEFAULT_TAIL_COUNT;
  for (let index = 0; index < args.length; index++) {
    const arg = args[index]!;
    if (arg === "--last") {
      const next = args[index + 1];
      if (!next) {
        throw new CliError("Usage: sts logs correlation CORRELATION_ID [--last N]");
      }

      last = parsePositiveInt(next, "--last");
      index++;
      continue;
    }

    if (arg.startsWith("--last=")) {
      last = parsePositiveInt(arg.slice("--last=".length), "--last");
      continue;
    }

    throw new CliError(`Unknown logs correlation argument: ${arg}`);
  }

  const path = resolveLogFilePath("action-execution");
  const entries = readJsonLines(path)
    .filter((entry) => {
      if (typeof entry !== "object" || entry === null) {
        return false;
      }

      const typedEntry = entry as { correlationId?: unknown; CorrelationId?: unknown };
      const entryCorrelationId = typeof typedEntry.correlationId === "string"
        ? typedEntry.correlationId
        : typeof typedEntry.CorrelationId === "string"
          ? typedEntry.CorrelationId
          : undefined;

      return entryCorrelationId === correlationId;
    });

  return {
    correlationId,
    kind: "action-execution",
    path,
    entryCount: Math.min(entries.length, last),
    entries: tailEntries(entries, last)
  };
}
