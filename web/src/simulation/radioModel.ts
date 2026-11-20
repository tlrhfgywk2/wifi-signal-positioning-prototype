import { classify, SignalHistory } from './history';
import { creatureLoss, geometry } from './interference';
import { distance, type AccessPoint, type AccessPointObservation, type SimulationConfig, type Vec2 } from './types';

export function expectedSignal(reference: number, loss: number, meters: number) {
  if (![reference, loss, meters].every(Number.isFinite)) return NaN;
  return reference - loss * Math.max(0, meters);
}
export function recoverDistance(reference: number, measured: number, loss: number) {
  if (![reference, measured, loss].every(Number.isFinite) || loss <= 0) return null;
  const meters = (reference - measured) / loss;
  return Number.isFinite(meters) ? Math.max(0, meters) : null;
}
export function observe(ap: AccessPoint, player: Vec2, creature: Vec2, time: number,
  config: SimulationConfig, history: SignalHistory, random: () => number): AccessPointObservation {
  const expected = expectedSignal(config.reference, config.lossPerMeter, distance(ap.position, player));
  const path = geometry(creature, ap.position, player, config);
  const attenuation = creatureLoss(path.amplitude, ap.id, time, config);
  const noise = config.noiseAmplitude > 0 ? (2 * random() - 1) * config.noiseAmplitude : 0;
  const measured = expected - Math.max(0, ap.stableAttenuationDb) + noise + attenuation;
  history.record(measured, path.level);
  const range = history.range;
  return {
    id: ap.id, position: ap.position, distance: recoverDistance(config.reference, measured, config.lossPerMeter),
    expected, measured, delta: measured - expected, noise, attenuation, geometry: path,
    range, recentCreatureSamples: history.recent,
    status: classify(path.contaminated, path.level, range, history.recent > 0, config),
  };
}
