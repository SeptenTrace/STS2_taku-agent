import test from "node:test";
import assert from "node:assert/strict";
import { mkdtempSync, readFileSync, rmSync } from "node:fs";
import { tmpdir } from "node:os";
import { join } from "node:path";

import { createTelemetrySession } from "./telemetry.ts";
import { MockClient } from "../test-helpers/mock-client.ts";

test("telemetry writes successful CLI command records with traced requests", async () => {
  const logDir = mkdtempSync(join(tmpdir(), "sts-cli-telemetry-"));
  const previousLogDir = process.env.STS_LOG_DIR;
  const previousCliTest = process.env.STS_CLI_TEST;
  delete process.env.STS_CLI_TEST;
  process.env.STS_LOG_DIR = logDir;

  try {
    const telemetry = createTelemetrySession("context", [], "http://localhost:15527");
    const client = telemetry.wrapClient(new MockClient({
      "/api/v1/context": {
        stateType: "map"
      }
    }));

    await client.request("/api/v1/context");
    telemetry.writeSuccess();

    const entries = readFileSync(join(logDir, "cli-command.jsonl"), "utf8")
      .trim()
      .split(/\r?\n/)
      .map((line) => JSON.parse(line));

    assert.equal(entries.length, 1);
    assert.equal(entries[0].Command, "context");
    assert.equal(entries[0].Outcome, "ok");
    assert.equal(entries[0].HttpRequestCount, 1);
    assert.equal(entries[0].HttpRequests[0].path, "/api/v1/context");
  } finally {
    if (previousLogDir === undefined) {
      delete process.env.STS_LOG_DIR;
    } else {
      process.env.STS_LOG_DIR = previousLogDir;
    }

    if (previousCliTest === undefined) {
      delete process.env.STS_CLI_TEST;
    } else {
      process.env.STS_CLI_TEST = previousCliTest;
    }

    rmSync(logDir, { recursive: true, force: true });
  }
});
