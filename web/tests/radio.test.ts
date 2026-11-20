import { describe, expect, it } from 'vitest';
import { accessPoints, defaults } from '../src/simulation/config';
import { multilaterate } from '../src/simulation/estimator';
import { classify, SignalHistory } from '../src/simulation/history';
import { creatureLoss, geometry, nearestPoint } from '../src/simulation/interference';
import { expectedSignal, observe, recoverDistance } from '../src/simulation/radioModel';
import { distance, type Band } from '../src/simulation/types';
import { roundedDbm, signalFraction } from '../src/ui/diagnostics';

const v = (x: number, y: number) => ({ x, y });
const readings = (x: number, y: number) => accessPoints.map(({ position }) => ({
  position, distance: recoverDistance(-20, expectedSignal(-20, 5, distance(position, v(x, y))), 5),
}));
describe('RSSI and multilateration', () => {
  it('recovers three meters from −35 dBm', () => {
    expect(expectedSignal(-20, 5, 3)).toBe(-35);
    expect(recoverDistance(-20, -35, 5)).toBe(3);
    expect(recoverDistance(-20, -18, 5)).toBe(0);
  });
  it.each([0, -1, NaN, Infinity])('rejects faulty loss %s', loss => {
    expect(recoverDistance(-20, -55, loss)).toBeNull();
  });
  it('handles non-finite model inputs and overflow', () => {
    expect(expectedSignal(Infinity, 5, 1)).toBeNaN();
    expect(expectedSignal(-20, 5, NaN)).toBeNaN();
    expect(recoverDistance(-20, NaN, 5)).toBeNull();
    expect(recoverDistance(1e308, -1e308, 2)).toBeNull();
  });
  it('solves the Unity validation target with only AP coordinates and inverted readings', () => {
    const solution = multilaterate(readings(1.25, -2.1));
    expect(solution!.x).toBeCloseTo(1.25, 10);
    expect(solution!.y).toBeCloseTo(-2.1, 10);
  });
  it('requires point zero, skips later invalid distances, and needs independent rows', () => {
    const r = readings(1.25, -2.1);
    expect(multilaterate([null, ...r.slice(1)])).toBeNull();
    expect(multilaterate([{ ...r[0]!, distance: NaN }, ...r.slice(1)])).toBeNull();
    for (const invalid of [-1, NaN, Infinity, null]) {
      expect(multilaterate([r[0], { ...r[1]!, distance: invalid }, ...r.slice(2)])!.x).toBeCloseTo(1.25);
    }
    expect(multilaterate(r.slice(0, 2))).toBeNull();
    expect(multilaterate([r[0], null, null, r[3]])).toBeNull();
    expect(multilaterate([0, 1, 2].map(x => ({ position: v(x, 0), distance: 2 })))).toBeNull();
    expect(multilaterate([{ position: v(NaN, 2), distance: 2 }, ...r])).toBeNull();
    expect(multilaterate(r.map(o => ({ ...o, distance: 1e300 })))).toBeNull();
  });
  it('does not clamp a perturbed estimate', () => {
    const r = readings(0, 0);
    r[0]!.distance = 30;
    expect(multilaterate(r)!.x).toBeGreaterThan(5);
  });
  it('subtracts static loss, adds independent noise and negative creature attenuation before inversion', () => {
    const ap = { ...accessPoints[0]!, stableAttenuationDb: 14 };
    const c = { ...defaults };
    for (const random of [0, 0.25, 0.9, 1]) {
      const obs = observe(ap, v(0, 0), ap.position, 7, c, new SignalHistory(), () => random);
      expect(obs.noise).toBe(random * 6 - 3);
      expect(obs.measured).toBeCloseTo(obs.expected - 14 + obs.noise + obs.attenuation, 12);
      expect(obs.distance).toBeCloseTo((-20 - obs.measured) / 5, 12);
      expect(obs.attenuation).toBeLessThan(0);
    }
  });
  it('rounds for display and clamps signal bars', () => {
    expect([-52.5, -51.5, -52.6].map(roundedDbm)).toEqual([-52, -52, -53]);
    expect([-120, -90, -55, -20, 0, NaN].map(signalFraction)).toEqual([0, 0, .5, 1, 1, 0]);
  });
});
describe('finite AP paths and coherent attenuation', () => {
  it('projects before, inside, after and onto a point segment', () => {
    const a = v(0, 0), b = v(2, 0);
    expect(nearestPoint(v(-1, 1), a, b)).toEqual(a);
    expect(nearestPoint(v(1, 1), a, b)).toEqual(v(1, 0));
    expect(nearestPoint(v(3, 1), a, b)).toEqual(b);
    expect(nearestPoint(v(3, 1), b, b)).toEqual(b);
  });
  it.each([[0.25, 'strong', 12], [0.25001, 'medium', 6], [0.65, 'medium', 6],
    [0.65001, 'weak', 3], [1.4, 'weak', 3], [1.40001, 'none', 0]] as const)(
    'classifies %s m as %s', (y, level, amplitude) => {
      expect(geometry(v(0, y), v(-2, 0), v(2, 0), defaults)).toMatchObject({ level, amplitude, pathDistance: y });
    });
  it('promotes contamination at the AP radius, preserving strong hits', () => {
    const at = (y: number) => geometry(v(0, y), v(0, 0), v(3, 0), defaults);
    expect(at(0.9)).toMatchObject({ contaminated: true, level: 'medium', amplitude: 6 });
    expect(at(0.90001)).toMatchObject({ contaminated: false, level: 'weak' });
    expect(at(0.1)).toMatchObject({ contaminated: true, level: 'strong', amplitude: 12 });
  });
  it('varies smoothly over time and independently by AP, within band limits', () => {
    const loss = (id: string, time: number) => creatureLoss(12, id, time, defaults);
    expect(new Set(accessPoints.map(p => loss(p.id, 4))).size).toBe(4);
    expect(loss('NW', 4)).toBe(loss('NW', 4));
    expect(Math.abs(loss('NW', 4) - loss('NW', 4.00001))).toBeLessThan(0.001);
    const samples = Array.from({ length: 100 }, (_, t) => loss('NW', t / 3));
    expect(new Set(samples).size).toBeGreaterThan(80);
    expect(Math.max(...samples)).toBeLessThanOrEqual(-8);
    expect(Math.min(...samples)).toBeGreaterThanOrEqual(-12);
  });
});
describe('bounded histories and precedence', () => {
  it('overwrites the oldest entry and expires a creature hit after 12 clear samples', () => {
    const h = new SignalHistory();
    h.record(NaN, 'none');
    expect(h.count).toBe(0);
    h.record(-90, 'strong');
    for (let i = 0; i < 11; i++) h.record(-40, 'none');
    expect([h.count, h.range, h.recent]).toEqual([12, 50, 1]);
    h.record(-40, 'none');
    expect([h.count, h.range, h.recent]).toEqual([12, 0, 0]);
  });
  it('keeps movement and static-only attenuation stable', () => {
    const h = new SignalHistory();
    const c = { ...defaults, noiseAmplitude: 0 };
    observe(accessPoints[0]!, v(0, 0), v(50, 50), 0, c, h, Math.random);
    const o = observe({ ...accessPoints[0]!, stableAttenuationDb: 18 }, v(2, -3), v(50, 50), 1, c, h, Math.random);
    expect(o.range).toBeGreaterThan(9);
    expect(o.status).toBe('STABLE');
  });
  it('applies the status priority and inclusive thresholds', () => {
    const status = (band: Band, range: number, recent = true, contaminated = false) =>
      classify(contaminated, band, range, recent, defaults);
    expect(status('strong', 12, true, true)).toBe('AP CONTAMINATED');
    expect(status('strong', 0)).toBe('SEVERE INTERFERENCE');
    expect(status('medium', 9)).toBe('SEVERE INTERFERENCE');
    expect(status('medium', 0)).toBe('UNSTABLE');
    expect(status('weak', 5)).toBe('UNSTABLE');
    expect(status('none', 8)).toBe('UNSTABLE');
    expect(status('weak', 4)).toBe('WEAK INTERFERENCE');
    expect(status('none', 20, false)).toBe('STABLE');
  });
});
