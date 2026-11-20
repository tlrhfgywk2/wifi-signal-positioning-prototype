import type { DistanceReading, Vec2 } from './types';
type Reading = DistanceReading | null | undefined;
function valid(r: Reading): r is DistanceReading & { distance: number } {
  return !!r && r.distance !== null && r.distance >= 0
    && [r.distance, r.position.x, r.position.y].every(Number.isFinite);
}
export function multilaterate(readings: readonly Reading[]): Vec2 | null {
  const ref = readings[0];
  if (readings.length < 3 || !valid(ref)) return null;
  const p0 = ref.position;
  let a00 = 0, a01 = 0, a11 = 0, b0 = 0, b1 = 0, count = 0;
  for (const r of readings.slice(1)) {
    if (!valid(r)) continue;
    const p = r.position;
    const x = 2 * (p.x - p0.x), y = 2 * (p.y - p0.y);
    const rhs = ref.distance ** 2 - r.distance ** 2 + p.x ** 2 + p.y ** 2 - p0.x ** 2 - p0.y ** 2;
    a00 += x * x;
    a01 += x * y;
    a11 += y * y;
    b0 += x * rhs;
    b1 += y * rhs;
    count++;
  }
  const det = a00 * a11 - a01 ** 2;
  if (count < 2 || !Number.isFinite(det) || Math.abs(det) <= 1e-6) return null;
  const x = (b0 * a11 - b1 * a01) / det, y = (a00 * b1 - a01 * b0) / det;
  return Number.isFinite(x) && Number.isFinite(y) ? { x, y } : null;
}
