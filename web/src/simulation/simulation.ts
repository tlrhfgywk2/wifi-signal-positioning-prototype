import { accessPoints, defaults, PLAYER_RADIUS, ROOM_SIZE } from './config';
import { multilaterate } from './estimator';
import { SignalHistory } from './history';
import { observe } from './radioModel';
import { distance, type Sample, type SimulationConfig, type SimulationState, type Vec2 } from './types';

export function move(player: Vec2, input: Vec2, speed: number, dt: number): Vec2 {
  const scale = speed * Math.max(0, dt) / Math.max(1, Math.hypot(input.x, input.y));
  const limit = ROOM_SIZE / 2 - PLAYER_RADIUS;
  const bound = (n: number) => Math.max(-limit, Math.min(n, limit));
  return { x: bound(player.x + input.x * scale), y: bound(player.y + input.y * scale) };
}

export class Simulation {
  readonly state: SimulationState;
  private histories = accessPoints.map(() => new SignalHistory());
  private origin: Vec2;
  constructor(readonly config: SimulationConfig = { ...defaults }, private random = Math.random) {
    const player = { x: 0, y: 0 };
    this.origin = { x: config.creatureX, y: config.creatureY };
    const creature = { ...this.origin };
    // Unity primes AP histories in BuildPrototype before its first LateUpdate solve.
    this.measure(player, creature, 0);
    this.state = { time: 0, player, creature, sample: this.measure(player, creature, 0) };
  }
  private measure(player: Vec2, creature: Vec2, time: number): Sample {
    const observations = accessPoints.map((ap, i) =>
      observe(ap, player, creature, time, this.config, this.histories[i]!, this.random));
    const position = multilaterate(observations);
    return { time, observations, estimate: position && { position, error: distance(position, player) } };
  }

  private advanceTo(time: number, input: Vec2) {
    const s = this.state, c = this.config;
    s.player = move(s.player, input, c.speed, time - s.time);
    s.time = time;
    if (this.origin.x !== c.creatureX || this.origin.y !== c.creatureY) {
      this.origin = { x: c.creatureX, y: c.creatureY };
      s.creature = { ...this.origin };
    }
    const magnitude = Math.hypot(c.patrolX, c.patrolY);
    if (c.patrol && c.patrolDistance > 0 && magnitude > 0) {
      const offset = Math.sin(time * c.patrolSpeed) * c.patrolDistance / magnitude;
      s.creature = { x: this.origin.x + offset * c.patrolX, y: this.origin.y + offset * c.patrolY };
    }
  }

  step(deltaSeconds: number, input: Vec2) {
    if (!Number.isFinite(deltaSeconds) || deltaSeconds <= 0) return;
    // Bound stalls and advance movement exactly to each measurement boundary.
    const end = this.state.time + (deltaSeconds > 1 ? 0.25 : deltaSeconds);
    const interval = this.config.interval > 0 ? Math.max(0.1, this.config.interval) : 1;
    // A shortened interval takes effect now, without replaying historical samples.
    let due = Math.max(this.state.time, this.state.sample.time + interval);
    while (due <= end + 1e-10) {
      this.advanceTo(due, input);
      this.state.sample = this.measure(this.state.player, this.state.creature, due);
      due += interval;
    }
    if (end > this.state.time) this.advanceTo(end, input);
  }
}
