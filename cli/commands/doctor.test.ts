import test from "node:test";
import assert from "node:assert/strict";

import { runDoctor, type LocalInspector } from "./doctor.ts";
import { MockClient } from "../test-helpers/mock-client.ts";

class MockInspector implements LocalInspector {
  private readonly gameProcessRunning: boolean;
  private readonly observerPortListening: boolean;

  constructor(gameProcessRunning: boolean, observerPortListening: boolean) {
    this.gameProcessRunning = gameProcessRunning;
    this.observerPortListening = observerPortListening;
  }

  async isGameProcessRunning(): Promise<boolean> {
    return this.gameProcessRunning;
  }

  async isObserverPortListening(): Promise<boolean> {
    return this.observerPortListening;
  }
}

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

  const result = await runDoctor(client, new MockInspector(true, true));
  assert.equal(result.ok, true);
  assert.equal(result.checks.find((check) => check.name === "game_process")?.ok, true);
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

  const result = await runDoctor(client, new MockInspector(true, true));
  assert.equal(result.ok, true);
  assert.equal(result.checks.find((check) => check.name === "room_summary")?.ok, true);
});
