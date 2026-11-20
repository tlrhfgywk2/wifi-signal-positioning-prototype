import { expect, it, vi } from 'vitest';
import { measurementRandom } from '../src/simulation/random';
import { axes, type Direction } from '../src/input/controls';
import { accessPoints, defaults } from '../src/simulation/config';
import { move, Simulation } from '../src/simulation/simulation';
const zero = { x: 0, y: 0 };
it('uses serialized scene coordinates and the configured player inset', () => {
  expect(accessPoints.map(p => [p.id, p.position.x, p.position.y])).toEqual([
    ['NW', -4.55, 4.55], ['NE', 4.55, 4.55], ['SW', -4.55, -4.55], ['SE', 4.55, -4.55],
  ]);
  expect(new Simulation().state.player).toEqual(zero);
  expect(move(zero, { x: 1, y: -1 }, 3, 20)).toEqual({ x: 4.86, y: -4.86 });
});
it.each([
  ['KeyW', 'ArrowUp', 'up', 0, 1], ['KeyA', 'ArrowLeft', 'left', -1, 0],
  ['KeyS', 'ArrowDown', 'down', 0, -1], ['KeyD', 'ArrowRight', 'right', 1, 0],
] as const)('maps %s, arrows and pad holds', (key, arrow, dir: Direction, x, y) => {
  expect(axes([key, arrow], [])).toEqual({ x, y });
  expect(axes([], [dir, dir])).toEqual({ x, y });
  expect(move(zero, axes([key], []), 3, 0.5)).toEqual({ x: 1.5 * x, y: 1.5 * y });
});
it('adds keyboard and UI axes before normalizing', () => {
  const input = axes(['KeyW', 'KeyD'], ['right']);
  expect(input).toEqual({ x: 2, y: 1 });
  const result = move(zero, input, 3, 1);
  expect(Math.hypot(result.x, result.y)).toBeCloseTo(3, 12);
  expect(result.x / result.y).toBe(2);
  expect(axes(['ArrowLeft'], ['right'])).toEqual(zero);
});
it('draws one noise per AP, primes history, and holds estimates/error between samples', () => {
  let draws = 0;
  const random = vi.fn(() => (++draws % 17) / 17);
  const sim = new Simulation({ ...defaults }, random);
  const first = sim.state.sample;
  expect(random).toHaveBeenCalledTimes(8);
  expect(new Set(first.observations.map(o => o.noise)).size).toBe(4);
  for (let i = 0; i < 59; i++) sim.step(1 / 60, { x: 1, y: 0 });
  expect(sim.state.sample).toBe(first);
  expect(sim.state.player.x).toBeCloseTo(2.95);
  sim.step(1 / 60, { x: 1, y: 0 });
  expect(random).toHaveBeenCalledTimes(12);
  expect(sim.state.sample).not.toBe(first);
  const e = sim.state.sample.estimate!;
  expect(e.error).toBeCloseTo(Math.hypot(e.position.x - 3, e.position.y), 10);
});
it('samples at identical times and positions across frame rates', () => {
  for (const fps of [1, 2, 7, 30, 60, 144]) {
    const sim = new Simulation({ ...defaults, noiseAmplitude: 0, creatureX: 50 });
    for (let i = 0; i < fps * 2; i++) sim.step(1 / fps, { x: 0.5, y: 0 });
    expect(sim.state.sample.time).toBe(2);
    expect(sim.state.sample.estimate!.position.x).toBeCloseTo(3, 10);
  }
});
it('limits long stalls and handles interval changes without historical replay', () => {
  const random = vi.fn(() => .5);
  const sim = new Simulation({ ...defaults }, random);
  sim.step(500, { x: 1, y: 0 });
  expect(sim.state.time).toBe(0.25);
  expect(sim.state.player.x).toBe(0.75);
  expect(random).toHaveBeenCalledTimes(8);
  sim.config.interval = 0.1;
  sim.step(0.1, zero);
  expect(sim.state.sample.time).toBeCloseTo(0.35);
  expect(random).toHaveBeenCalledTimes(16);
});
it('uses absolute simulation time for normalized patrol and holds position when disabled', () => {
  const sim = new Simulation();
  sim.step(.2, zero);
  expect(sim.state.creature).toEqual({ x: -1.2, y: 1.1 });
  Object.assign(sim.config, { patrol: true, patrolX: 3, patrolY: 4 });
  sim.step(.2, zero);
  const offset = Math.sin(.4 * .5) * 1.5;
  expect(sim.state.creature.x).toBeCloseTo(-1.2 + .6 * offset);
  expect(sim.state.creature.y).toBeCloseTo(1.1 + .8 * offset);
  const stopped = sim.state.creature;
  sim.config.patrol = false;
  sim.step(.2, zero);
  expect(sim.state.creature).toEqual(stopped);
});
it('invalidates the sampled estimate on model failure and recovers on the next valid reading', () => {
  const sim = new Simulation({ ...defaults, lossPerMeter: 0, interval: 0.1 });
  expect(sim.state.sample.estimate).toBeNull();
  sim.config.lossPerMeter = 5;
  sim.step(.1, zero);
  expect(sim.state.sample.estimate).not.toBeNull();
});

it('opts into a repeatable measurement stream only with a uint32 seed', () => {
  const draws = (seed: string) => {
    const random = measurementRandom(seed);
    return Array.from({ length: 8 }, () => random());
  };
  expect(draws('17')).toEqual(draws('17'));
  expect(draws('0')).not.toEqual(draws('17'));
  expect(measurementRandom(null)).toBe(Math.random);
  expect(measurementRandom('4294967296')).toBe(Math.random);
});
