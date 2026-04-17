import test from "node:test";
import assert from "node:assert/strict";

import { buildRoomSummary, claimAllSafeRewards, waitForPlayerReady } from "./combo.ts";
import { MockClient } from "../test-helpers/mock-client.ts";

test("waitForPlayerReady resolves on rewards screens", async () => {
  const client = new MockClient({
    "/api/v1/context": {
      stateType: "rewards",
      roomType: "Monster"
    }
  });

  const result = await waitForPlayerReady(client);
  assert.equal(result.condition, "player_ready");
  assert.equal(result.context.stateType, "rewards");
});

test("claimAllSafeRewards claims non-card rewards and stops at card rewards", async () => {
  const client = new MockClient({
    "/api/v1/context": [
      { stateType: "rewards", roomType: "Monster" },
      { stateType: "rewards", roomType: "Monster" }
    ],
    "/api/v1/rewards": [
      {
        canProceed: true,
        items: [
          { index: 0, type: "gold", label: "12 gold" },
          { index: 1, type: "card", label: "Card reward" }
        ]
      },
      {
        canProceed: true,
        items: [
          { index: 0, type: "card", label: "Card reward" }
        ]
      }
    ],
    "/api/v1/actions": [
      {
        stateType: "rewards",
        actions: [
          { actionType: "claim_reward", index: 0, label: "12 gold" },
          { actionType: "claim_reward", index: 1, label: "Card reward" }
        ]
      },
      {
        stateType: "rewards",
        actions: [
          { actionType: "claim_reward", index: 0, label: "Card reward" }
        ]
      }
    ],
    "/api/v1/actions/execute": {
      status: "ok",
      actionType: "claim_reward",
      message: "Claimed reward '12 gold'."
    }
  });

  const result = await claimAllSafeRewards(client);
  assert.equal(result.claimed.length, 1);
  assert.equal(result.claimed[0]?.type, "gold");
  assert.equal(result.stoppedReason, "card_reward_pending");
  assert.deepEqual(
    client.requests.map((entry) => entry.path),
    [
      "/api/v1/context",
      "/api/v1/rewards",
      "/api/v1/actions",
      "/api/v1/actions/execute",
      "/api/v1/context",
      "/api/v1/rewards",
      "/api/v1/actions"
    ]
  );
});

test("buildRoomSummary combines context, player, actions, and state data", async () => {
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
      items: [
        { index: 0, type: "potion", label: "火焰药水" }
      ]
    }
  });

  const result = await buildRoomSummary(client);
  assert.equal(result.context.stateType, "rewards");
  assert.equal(result.playerSummary.characterId, "IRONCLAD");
  assert.equal(result.stateData?.kind, "rewards");
});
