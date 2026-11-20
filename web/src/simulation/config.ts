import type { AccessPoint, SimulationConfig } from './types';
export const ROOM_SIZE = 10;
export const PLAYER_RADIUS = 0.14;
export const HISTORY_CAPACITY = 12;
const corner = ROOM_SIZE / 2 - 0.45;
export const accessPoints: readonly AccessPoint[] = [
  { id: 'NW', position: { x: -corner, y: corner }, stableAttenuationDb: 0 },
  { id: 'NE', position: { x: corner, y: corner }, stableAttenuationDb: 0 },
  { id: 'SW', position: { x: -corner, y: -corner }, stableAttenuationDb: 0 },
  { id: 'SE', position: { x: corner, y: -corner }, stableAttenuationDb: 0 },
];
export const defaults: Readonly<SimulationConfig> = Object.freeze({
  speed: 3, interval: 1, reference: -20, lossPerMeter: 5, noiseAmplitude: 3,
  unstableThreshold: 5, severeThreshold: 9,
  strongRadius: 0.25, mediumRadius: 0.65, weakRadius: 1.4,
  strongAmplitude: 12, mediumAmplitude: 6, weakAmplitude: 3,
  contaminationRadius: 0.9, frequency: 0.7, noiseSeed: 17.31,
  creatureX: -1.2, creatureY: 1.1, patrol: false, patrolDistance: 1.5,
  patrolSpeed: 0.5, patrolX: 1, patrolY: 0,
});
