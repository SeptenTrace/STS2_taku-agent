import test from "node:test";
import assert from "node:assert/strict";

import { dispatch } from "./dispatch.ts";
import { CliError } from "../core/errors.ts";
import { MockClient } from "../test-helpers/mock-client.ts";
import { MockOutput } from "../test-helpers/mock-output.ts";

test("dispatch help writes usage text", async () => {
  const client = new MockClient({});
  const output = new MockOutput();

  await dispatch(client, "help", [], output);

  assert.equal(output.jsonValues.length, 0);
  assert.equal(output.textValues.length, 1);
  assert.match(output.textValues[0]!, /^Usage:/);
});

test("dispatch next combines context and observation into one JSON payload", async () => {
  const client = new MockClient({
    "/api/v1/context": {
      stateType: "monster",
      roomType: "Monster"
    },
    "/api/v1/observation/compact": {
      stateType: "monster",
      goal: "Decide the next combat action."
    }
  });
  const output = new MockOutput();

  await dispatch(client, "next", [], output);

  assert.equal(output.textValues.length, 0);
  assert.deepEqual(output.jsonValues[0], {
    context: {
      stateType: "monster",
      roomType: "Monster"
    },
    observation: {
      stateType: "monster",
      goal: "Decide the next combat action."
    }
  });
});

test("dispatch actions uses the typed actions endpoint", async () => {
  const client = new MockClient({
    "/api/v1/actions": {
      stateType: "rewards",
      actions: []
    }
  });
  const output = new MockOutput();

  await dispatch(client, "actions", [], output);

  assert.deepEqual(client.requests.map((entry) => entry.path), ["/api/v1/actions"]);
  assert.deepEqual(output.jsonValues[0], {
    stateType: "rewards",
    actions: []
  });
});

test("dispatch rejects unknown subcommands with CliError", async () => {
  const client = new MockClient({});
  const output = new MockOutput();

  await assert.rejects(
    () => dispatch(client, "player", ["bad-section"], output),
    (error) => error instanceof CliError && error.message.includes("Unknown player subcommand")
  );
});
