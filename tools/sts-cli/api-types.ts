export interface ContextResponse {
  stateType: string;
  roomType?: string;
  overlayType?: string;
  recommendedQueries?: string[];
}

export interface ObservationCompactResponse {
  stateType: string;
  goal?: string;
  facts?: string[];
  suggestedQueries?: string[];
}

export interface CombatPilesSummary {
  draw: number;
  discard: number;
  exhaust: number;
}

export interface CombatSummaryResponse {
  roomType: string;
  round: number;
  side: string;
  handCount: number;
  enemyCount: number;
  incomingDamage: number;
  playableCards: number;
  potionActions: number;
  actionCount: number;
  piles: CombatPilesSummary;
}
