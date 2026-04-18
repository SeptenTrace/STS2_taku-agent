import test from "node:test";
import assert from "node:assert/strict";

import { buildExecInvocation, buildExecPayload } from "./exec.ts";
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

test("buildExecInvocation extracts wait flags from positional exec arguments", () => {
  const invocation = buildExecInvocation("play_card", ["0", "jaw_worm_0", "--wait-for", "player_turn", "--timeout", "9"]);
  assert.deepEqual(invocation, {
    payload: {
      actionType: "play_card",
      parameters: {
        index: 0,
        target: "jaw_worm_0"
      }
    },
    waitFor: "player_turn",
    timeoutSeconds: 9,
    waitVerbose: false
  });
});

test("buildExecInvocation extracts inline wait flags from key=value exec arguments", () => {
  const invocation = buildExecInvocation("choose_map_node", ["index=1", "--wait-for=monster"]);
  assert.deepEqual(invocation, {
    payload: {
      actionType: "choose_map_node",
      parameters: {
        index: 1
      }
    },
    waitFor: "monster",
    timeoutSeconds: 15,
    waitVerbose: false
  });
});

test("buildExecInvocation supports high-level wait aliases", () => {
  const invocation = buildExecInvocation("continue_game", ["--wait-for-run", "--wait-verbose", "--timeout", "30"]);
  assert.deepEqual(invocation, {
    payload: {
      actionType: "continue_game",
      parameters: {}
    },
    waitFor: "run_active",
    timeoutSeconds: 30,
    waitVerbose: true
  });
});

test("buildExecInvocation infers a default wait target for continue_game", () => {
  const invocation = buildExecInvocation("continue_game", []);
  assert.deepEqual(invocation, {
    payload: {
      actionType: "continue_game",
      parameters: {}
    },
    waitFor: "run_active",
    timeoutSeconds: 15,
    waitVerbose: false
  });
});

test("buildExecInvocation infers a default wait target for proceed", () => {
  const invocation = buildExecInvocation("proceed", []);
  assert.deepEqual(invocation, {
    payload: {
      actionType: "proceed",
      parameters: {}
    },
    waitFor: "room_ready",
    timeoutSeconds: 15,
    waitVerbose: false
  });
});

test("buildExecInvocation infers a default wait target for open_treasure", () => {
  const invocation = buildExecInvocation("open_treasure", []);
  assert.deepEqual(invocation, {
    payload: {
      actionType: "open_treasure",
      parameters: {}
    },
    waitFor: "treasure",
    timeoutSeconds: 15,
    waitVerbose: false
  });
});

test("buildExecInvocation rejects wait verbose without a wait target", () => {
  assert.throws(
    () => buildExecInvocation("shop_purchase", ["index=1", "--wait-verbose"]),
    (error) => error instanceof CliError && error.message.includes("--wait-verbose")
  );
});

test("buildExecInvocation rejects malformed wait timeout", () => {
  assert.throws(
    () => buildExecInvocation("end_turn", ["--wait-for", "player_turn", "--timeout", "bad"]),
    (error) => error instanceof CliError && error.message.includes("TIMEOUT_SECONDS")
  );
});
