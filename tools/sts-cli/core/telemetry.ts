import { appendFileSync, mkdirSync } from "node:fs";

import type { RequestClient, RequestOptions } from "./client.ts";
import { CliError, HttpError } from "./errors.ts";
import { getLogDirectory, resolveLogFilePath } from "./log-files.ts";

interface RequestTraceEntry {
  method: string;
  path: string;
  durationMs: number;
  ok: boolean;
  status?: number;
  correlationId?: string;
  errorName?: string;
  errorMessage?: string;
}

export interface CliCommandTelemetryRecord {
  Timestamp: string;
  Command: string;
  Args: string[];
  WorkingDirectory: string;
  BaseUrl: string;
  DurationMs: number;
  Outcome: "ok" | "cli_error" | "http_error" | "error";
  ExitCode: number;
  HttpRequestCount: number;
  HttpRequests: RequestTraceEntry[];
  CorrelationIds: string[];
  ErrorName?: string;
  ErrorMessage?: string;
  ErrorStatus?: number;
  ErrorPath?: string;
}

export interface TelemetrySession {
  wrapClient(client: RequestClient): RequestClient;
  writeSuccess(): void;
  writeError(error: unknown): void;
}

function nowMs(): number {
  return Date.now();
}

function shouldWriteTelemetry(): boolean {
  return process.env.STS_CLI_TEST !== "1" && process.env.STS_CLI_DISABLE_TELEMETRY !== "1";
}

function writeTelemetryRecord(record: CliCommandTelemetryRecord): void {
  if (!shouldWriteTelemetry()) {
    return;
  }

  mkdirSync(getLogDirectory(), { recursive: true });
  appendFileSync(resolveLogFilePath("cli-command"), `${JSON.stringify(record)}\n`, "utf8");
}

class TelemetryClient implements RequestClient {
  private readonly inner: RequestClient;
  private readonly trace: RequestTraceEntry[];

  constructor(inner: RequestClient, trace: RequestTraceEntry[]) {
    this.inner = inner;
    this.trace = trace;
  }

  async request<T = unknown>(path: string, options: RequestOptions = {}): Promise<T> {
    const startedAt = nowMs();
    const method = options.method ?? "GET";
    const correlationId = options.headers?.["X-Sts-Correlation-Id"];

    try {
      const result = await this.inner.request<T>(path, options);
      this.trace.push({
        method,
        path,
        durationMs: nowMs() - startedAt,
        ok: true,
        status: 200,
        correlationId
      });
      return result;
    } catch (error: unknown) {
      const entry: RequestTraceEntry = {
        method,
        path,
        durationMs: nowMs() - startedAt,
        ok: false,
        correlationId
      };

      if (error instanceof HttpError) {
        entry.status = error.status;
        entry.errorName = "HttpError";
        entry.errorMessage = error.message;
      } else if (error instanceof Error) {
        entry.errorName = error.name;
        entry.errorMessage = error.message;
      } else {
        entry.errorName = "UnknownError";
        entry.errorMessage = String(error);
      }

      this.trace.push(entry);
      throw error;
    }
  }
}

export function createTelemetrySession(command: string, args: string[], baseUrl: string): TelemetrySession {
  const startedAt = nowMs();
  const trace: RequestTraceEntry[] = [];

  function buildBaseRecord(durationMs: number): Omit<CliCommandTelemetryRecord, "Outcome" | "ExitCode"> {
    const correlationIds = Array.from(
      new Set(
        trace
          .map((entry) => entry.correlationId)
          .filter((value): value is string => typeof value === "string" && value.length > 0)
      )
    );

    return {
      Timestamp: new Date().toISOString(),
      Command: command,
      Args: args,
      WorkingDirectory: process.cwd(),
      BaseUrl: baseUrl,
      DurationMs: durationMs,
      HttpRequestCount: trace.length,
      HttpRequests: [...trace],
      CorrelationIds: correlationIds
    };
  }

  return {
    wrapClient(client: RequestClient): RequestClient {
      return new TelemetryClient(client, trace);
    },
    writeSuccess(): void {
      writeTelemetryRecord({
        ...buildBaseRecord(nowMs() - startedAt),
        Outcome: "ok",
        ExitCode: 0
      });
    },
    writeError(error: unknown): void {
      const base = buildBaseRecord(nowMs() - startedAt);

      if (error instanceof HttpError) {
        writeTelemetryRecord({
          ...base,
          Outcome: "http_error",
          ExitCode: 22,
          ErrorName: "HttpError",
          ErrorMessage: error.message,
          ErrorStatus: error.status,
          ErrorPath: error.path
        });
        return;
      }

      if (error instanceof CliError) {
        writeTelemetryRecord({
          ...base,
          Outcome: "cli_error",
          ExitCode: error.exitCode,
          ErrorName: "CliError",
          ErrorMessage: error.message
        });
        return;
      }

      if (error instanceof Error) {
        writeTelemetryRecord({
          ...base,
          Outcome: "error",
          ExitCode: 1,
          ErrorName: error.name,
          ErrorMessage: error.message
        });
        return;
      }

      writeTelemetryRecord({
        ...base,
        Outcome: "error",
        ExitCode: 1,
        ErrorName: "UnknownError",
        ErrorMessage: String(error)
      });
    }
  };
}
