export interface PingResponse {
  name: string;
  status: string;
  port: number;
  endpoints: string[];
}

export interface CapabilitiesResponse {
  stateFirst: string;
  observation: string;
  endpoints: string[];
}

export interface ContextResponse {
  stateType: string;
  roomType?: string;
  overlayType?: string;
  recommendedQueries?: string[];
}

export interface PlayerStatusEntry {
  id: string;
  title: string;
  description: string;
  amount?: number;
  category?: string;
}

export interface PlayerSummaryResponse {
  characterId: string;
  character: string;
  currentHp: number;
  maxHp: number;
  block: number;
  gold: number;
  deckCount: number;
  uniqueCards: number;
  upgradedCards: number;
  relicIds: string[];
  potionIds: string[];
  status: PlayerStatusEntry[];
  energy?: number;
  maxEnergy?: number;
  stars?: number;
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

export interface MapCoordResponse {
  col: number;
  row: number;
  type?: string;
}

export interface MapOptionResponse {
  index: number;
  col: number;
  row: number;
  type: string;
  leadsTo: MapCoordResponse[];
}

export interface MapSummaryResponse {
  currentPosition?: MapCoordResponse;
  nextOptions: MapOptionResponse[];
  boss?: MapCoordResponse;
  visitedCount: number;
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

export interface EventOptionResponse {
  index: number;
  title: string;
  description: string;
  isLocked?: boolean;
  isProceed?: boolean;
}

export interface EventResponse {
  eventId: string;
  title: string;
  body: string;
  inDialogue: boolean;
  options: EventOptionResponse[];
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

export interface ShopItemResponse {
  index: number;
  category: string;
  price: number;
  canAfford: boolean;
  title: string;
  description?: string;
}

export interface ShopResponse {
  canProceed: boolean;
  items: ShopItemResponse[];
}
