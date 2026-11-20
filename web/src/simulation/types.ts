export interface Vec2 { x: number; y: number }
export type AccessPointId = 'NW' | 'NE' | 'SW' | 'SE';
export interface AccessPoint {
  id: AccessPointId;
  position: Vec2;
  stableAttenuationDb: number;
}
export interface SimulationConfig {
  speed: number;
  interval: number;
  reference: number;
  lossPerMeter: number;
  noiseAmplitude: number;
  unstableThreshold: number;
  severeThreshold: number;
  strongRadius: number;
  mediumRadius: number;
  weakRadius: number;
  strongAmplitude: number;
  mediumAmplitude: number;
  weakAmplitude: number;
  contaminationRadius: number;
  frequency: number;
  noiseSeed: number;
  creatureX: number;
  creatureY: number;
  patrol: boolean;
  patrolDistance: number;
  patrolSpeed: number;
  patrolX: number;
  patrolY: number;
}
export type Band = 'none' | 'weak' | 'medium' | 'strong';
export interface InterferenceGeometry {
  closest: Vec2;
  pathDistance: number;
  level: Band;
  amplitude: number;
  contaminated: boolean;
}
export interface DistanceReading { position: Vec2; distance: number | null }
export interface AccessPointObservation extends DistanceReading {
  id: AccessPointId;
  expected: number;
  measured: number;
  delta: number;
  noise: number;
  attenuation: number;
  geometry: InterferenceGeometry;
  range: number;
  recentCreatureSamples: number;
  status: string;
}
export interface PositionEstimate {
  position: Vec2;
  error: number;
}
export interface Sample {
  time: number;
  observations: AccessPointObservation[];
  estimate: PositionEstimate | null;
}
export interface SimulationState {
  time: number;
  player: Vec2;
  creature: Vec2;
  sample: Sample;
}
export function distance(a: Vec2, b: Vec2) {
  return Math.hypot(a.x - b.x, a.y - b.y);
}
