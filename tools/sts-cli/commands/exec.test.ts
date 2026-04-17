import test from "node:test";
import assert from "node:assert/strict";

import { buildExecPayload } from "./exec.ts";
import { CliError } from "../core/errors.ts";

test("buildExecPayload supports positional action arguments", () => {
  const payload = buildExecPayload("play_card", ["0", "jaw_worm_0"]);
  assert.deepEqual(payload, {
    actionType: "play_card",
    parameters: {
      index: 0,
      target: "jaw_worm_0"
    }
  });
});

test("buildExecPayload parses key=value arguments with scalar coercion", () => {
  const payload = buildExecPayload("choose_map_node", ["index=1", "preview=true", "note=null"]);
  assert.deepEqual(payload, {
    actionType: "choose_map_node",
    parameters: {
      index: 1,
      preview: true,
      note: null
    }
  });
});

test("buildExecPayload rejects malformed key=value arguments", () => {
  assert.throws(
    () => buildExecPayload("play_card", ["index=1", "bad-arg"]),
    (error) => error instanceof CliError && error.message.includes("key=value")
  );
});
