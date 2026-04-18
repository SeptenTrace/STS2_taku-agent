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

test("dispatch player summary uses the typed player summary endpoint", async () => {
  const client = new MockClient({
    "/api/v1/player/summary": {
      characterId: "IRONCLAD",
      character: "铁甲战士",
      currentHp: 80,
      maxHp: 80,
      block: 0,
      gold: 99,
      deckCount: 12,
      uniqueCards: 5,
      upgradedCards: 0,
      relicIds: ["BURNING_BLOOD"],
      potionIds: [],
      status: []
    }
  });
  const output = new MockOutput();

  await dispatch(client, "player", ["summary"], output);

  assert.deepEqual(client.requests.map((entry) => entry.path), ["/api/v1/player/summary"]);
  assert.equal((output.jsonValues[0] as { characterId: string }).characterId, "IRONCLAD");
});

test("dispatch map uses the typed map endpoint", async () => {
  const client = new MockClient({
    "/api/v1/map/summary": {
      currentPosition: {
        col: 3,
        row: 2,
        type: "Unknown"
      },
      nextOptions: [],
      boss: {
        col: 3,
        row: 16,
        type: "Boss"
      },
      visitedCount: 3
    }
  });
  const output = new MockOutput();

  await dispatch(client, "map", [], output);

  assert.deepEqual(client.requests.map((entry) => entry.path), ["/api/v1/map/summary"]);
  assert.equal((output.jsonValues[0] as { visitedCount: number }).visitedCount, 3);
});

test("dispatch menu uses the typed menu endpoint", async () => {
  const client = new MockClient({
    "/api/v1/menu": {
      isVisible: true,
      hasContinueRun: true,
      canContinue: true,
      continueLabel: "继续游戏"
    }
  });
  const output = new MockOutput();

  await dispatch(client, "menu", [], output);

  assert.deepEqual(client.requests.map((entry) => entry.path), ["/api/v1/menu"]);
  assert.equal((output.jsonValues[0] as { canContinue: boolean }).canContinue, true);
});

test("dispatch doctor runs the built-in diagnostics", async () => {
  const client = new MockClient({
    "/": {
      name: "taku-agent-observer",
      status: "ok",
      port: 15527,
      endpoints: []
    },
    "/api/v1/context": {
      stateType: "menu",
      isStable: true,
      isTransitioning: false
    },
    "/api/v1/actions": {
      stateType: "menu",
      actions: [
        { actionType: "continue_game", label: "Continue game" }
      ]
    },
    "/api/v1/menu": {
      isVisible: true,
      hasContinueRun: true,
      canContinue: true,
      continueLabel: "继续游戏"
    }
  });
  const output = new MockOutput();

  await dispatch(client, "doctor", [], output);

  assert.equal((output.jsonValues[0] as { ok: boolean }).ok, true);
});

test("dispatch room summary writes combined room data", async () => {
  const client = new MockClient({
    "/api/v1/context": {
      stateType: "rewards",
      roomType: "Monster"
    },
    "/api/v1/player/summary": {
      characterId: "IRONCLAD",
      character: "铁甲战士",
      currentHp: 85,
      maxHp: 87,
      block: 0,
      gold: 104,
      deckCount: 13,
      uniqueCards: 6,
      upgradedCards: 0,
      relicIds: ["BURNING_BLOOD"],
      potionIds: [],
      status: []
    },
    "/api/v1/actions": {
      stateType: "rewards",
      actions: []
    },
    "/api/v1/rewards": {
      canProceed: true,
      items: []
    }
  });
  const output = new MockOutput();

  await dispatch(client, "room", ["summary"], output);

  assert.equal((output.jsonValues[0] as { context: { stateType: string } }).context.stateType, "rewards");
});

test("dispatch room summary handles menu without querying player summary", async () => {
  const client = new MockClient({
    "/api/v1/context": {
      stateType: "menu",
      isStable: true,
      isTransitioning: false
    },
    "/api/v1/actions": {
      stateType: "menu",
      actions: [
        { actionType: "continue_game", label: "Continue game" }
      ]
    },
    "/api/v1/menu": {
      isVisible: true,
      hasContinueRun: true,
      canContinue: true,
      continueLabel: "继续游戏"
    }
  });
  const output = new MockOutput();

  await dispatch(client, "room", ["summary"], output);

  assert.deepEqual(client.requests.map((entry) => entry.path), [
    "/api/v1/context",
    "/api/v1/actions",
    "/api/v1/menu"
  ]);
  assert.equal((output.jsonValues[0] as { playerSummary?: unknown }).playerSummary, undefined);
});

test("dispatch bundle-selection uses the typed bundle endpoint", async () => {
  const client = new MockClient({
    "/api/v1/bundle-selection": {
      screenType: "bundle",
      prompt: "Choose a bundle.",
      previewShowing: false,
      canConfirm: false,
      canCancel: false,
      bundles: [],
      previewCards: []
    }
  });
  const output = new MockOutput();

  await dispatch(client, "bundle-selection", [], output);

  assert.deepEqual(client.requests.map((entry) => entry.path), ["/api/v1/bundle-selection"]);
  assert.equal((output.jsonValues[0] as { screenType: string }).screenType, "bundle");
});

test("dispatch fake-merchant uses the typed fake merchant endpoint", async () => {
  const client = new MockClient({
    "/api/v1/fake-merchant": {
      eventId: "FAKE_MERCHANT",
      title: "Fake Merchant",
      startedFight: false,
      canProceed: true,
      items: []
    }
  });
  const output = new MockOutput();

  await dispatch(client, "fake-merchant", [], output);

  assert.deepEqual(client.requests.map((entry) => entry.path), ["/api/v1/fake-merchant"]);
  assert.equal((output.jsonValues[0] as { eventId: string }).eventId, "FAKE_MERCHANT");
});

test("dispatch rejects unknown subcommands with CliError", async () => {
  const client = new MockClient({});
  const output = new MockOutput();

  await assert.rejects(
    () => dispatch(client, "player", ["bad-section"], output),
    (error) => error instanceof CliError && error.message.includes("Unknown player subcommand")
  );
});

test("dispatch exec can wait for a stable follow-up condition", async () => {
  const client = new MockClient({
    "/api/v1/actions/execute": {
      status: "ok",
      actionType: "proceed",
      message: "Proceeded from rewards."
    },
    "/api/v1/context": [
      {
        stateType: "map",
        roomType: "Monster",
        isStable: true,
        isTransitioning: false
      },
      {
        stateType: "map",
        roomType: "Monster",
        isStable: true,
        isTransitioning: false
      }
    ],
    "/api/v1/actions": [
      {
        stateType: "map",
        actions: [
          { actionType: "choose_map_node", label: "Node 0: Unknown" }
        ]
      },
      {
        stateType: "map",
        actions: [
          { actionType: "choose_map_node", label: "Node 0: Unknown" }
        ]
      }
    ]
  });
  const output = new MockOutput();

  await dispatch(client, "exec", ["proceed", "--wait-for", "map"], output);

  assert.deepEqual(client.requests.map((entry) => entry.path), [
    "/api/v1/actions/execute",
    "/api/v1/context",
    "/api/v1/actions",
    "/api/v1/context",
    "/api/v1/actions"
  ]);
  assert.deepEqual(output.jsonValues[0], {
    execution: {
      status: "ok",
      actionType: "proceed",
      message: "Proceeded from rewards."
    },
    wait: {
      condition: "map",
      matched: true,
      context: {
        stateType: "map",
        roomType: "Monster",
        isStable: true,
        isTransitioning: false
      }
    }
  });
});
