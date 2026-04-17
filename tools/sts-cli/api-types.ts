export interface ContextResponse {
  stateType: string;
  roomType?: string;
  overlayType?: string;
  recommendedQueries?: string[];
}

export interface ActionParameter {
  name: string;
  value: string;
}

export interface ActionEntry {
  actionType: string;
  index?: number;
  label: string;
  description?: string;
  isAvailable?: boolean;
  parameters?: ActionParameter[];
  targetOptions?: string[];
  tags?: string[];
}

export interface ActionSurfaceResponse {
  stateType: string;
  goal?: string;
  actions: ActionEntry[];
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

export interface RewardItem {
  index: number;
  type: string;
  label: string;
  description?: string;
}

export interface RewardsResponse {
  canProceed: boolean;
  items: RewardItem[];
}

export interface CardRewardCard {
  index: number;
  id: string;
  title: string;
  type: string;
  rarity: string;
  cost: string;
  description: string;
}

export interface CardRewardResponse {
  canSkip: boolean;
  cards: CardRewardCard[];
}
