import test from "node:test";
import assert from "node:assert/strict";

import { runDoctor } from "./doctor.ts";
import { MockClient } from "../test-helpers/mock-client.ts";

test("runDoctor validates menu continue exposure", async () => {
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

  const result = await runDoctor(client);
  assert.equal(result.ok, true);
  assert.equal(result.checks.find((check) => check.name === "menu_continue_action")?.ok, true);
});

test("runDoctor validates in-run room summary", async () => {
  const client = new MockClient({
    "/": {
      name: "taku-agent-observer",
      status: "ok",
      port: 15527,
      endpoints: []
    },
    "/api/v1/context": [
      {
        stateType: "rewards",
        roomType: "Monster",
        isStable: true,
        isTransitioning: false
      },
      {
        stateType: "rewards",
        roomType: "Monster",
        isStable: true,
        isTransitioning: false
      }
    ],
    "/api/v1/actions": [
      {
        stateType: "rewards",
        actions: [
          { actionType: "claim_reward", label: "12 gold" }
        ]
      },
      {
        stateType: "rewards",
        actions: [
          { actionType: "claim_reward", label: "12 gold" }
        ]
      }
    ],
    "/api/v1/player/summary": {
      characterId: "IRONCLAD",
      character: "铁甲战士",
      currentHp: 70,
      maxHp: 80,
      block: 0,
      gold: 99,
      deckCount: 12,
      uniqueCards: 7,
      upgradedCards: 1,
      relicIds: [],
      potionIds: [],
      status: []
    },
    "/api/v1/rewards": {
      canProceed: true,
      items: []
    }
  });

  const result = await runDoctor(client);
  assert.equal(result.ok, true);
  assert.equal(result.checks.find((check) => check.name === "room_summary")?.ok, true);
});
