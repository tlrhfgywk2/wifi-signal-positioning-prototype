import { distance, type Vec2, type InterferenceGeometry, type SimulationConfig } from './types';

export function nearestPoint(point: Vec2, start: Vec2, end: Vec2): Vec2 {
  const dx = end.x - start.x;
  const dy = end.y - start.y;
  const lengthSquared = dx * dx + dy * dy;
  if (lengthSquared === 0) return { ...start };
  const projection = ((point.x - start.x) * dx + (point.y - start.y) * dy) / lengthSquared;
  const t = Math.min(1, Math.max(0, projection));
  return { x: start.x + t * dx, y: start.y + t * dy };
}

export function radii(c: SimulationConfig) {
  const strong = Math.max(0, c.strongRadius);
  const medium = Math.max(strong, c.mediumRadius);
  return { strong, medium, weak: Math.max(medium, c.weakRadius) };
}

export function geometry(creature: Vec2, ap: Vec2, player: Vec2, c: SimulationConfig): InterferenceGeometry {
  const closest = nearestPoint(creature, ap, player);
  const pathDistance = distance(creature, closest);
  const radius = radii(c);
  const strong = Math.max(0, c.strongAmplitude);
  const medium = Math.max(0, Math.min(strong, c.mediumAmplitude));
  const weak = Math.max(0, Math.min(medium, c.weakAmplitude));
  const contaminated = distance(creature, ap) <= Math.max(0, c.contaminationRadius);
  const result: InterferenceGeometry = { closest, pathDistance, contaminated, level: 'none', amplitude: 0 };
  if (pathDistance <= radius.strong) Object.assign(result, { level: 'strong', amplitude: strong });
  else if (pathDistance <= radius.medium) Object.assign(result, { level: 'medium', amplitude: medium });
  else if (pathDistance <= radius.weak) Object.assign(result, { level: 'weak', amplitude: weak });
  if (contaminated && result.amplitude < medium) {
    result.amplitude = medium;
    result.level = 'medium';
  }
  return result;
}

function channel(id: string) {
  let hash = 17;
  for (let i = 0; i < id.length; i++) hash = (Math.imul(hash, 31) + id.charCodeAt(i)) | 0;
  return hash & 0x7fffffff;
}

// 2D gradient (Perlin) noise. The lattice hash is local to this implementation.
const gradients = Array.from({ length: 8 }, (_, i) => ({
  x: Math.cos(i * Math.PI / 4), y: Math.sin(i * Math.PI / 4),
}));
function gradientDot(ix: number, iy: number, x: number, y: number) {
  const hash = Math.imul(ix, 374761393) + Math.imul(iy, 668265263);
  const mixed = Math.imul(hash ^ (hash >>> 13), 1274126177);
  const g = gradients[(mixed ^ (mixed >>> 16)) & 7]!;
  return g.x * (x - ix) + g.y * (y - iy);
}
const fade = (t: number) => t * t * t * (t * (t * 6 - 15) + 10);
const lerp = (a: number, b: number, t: number) => a + (b - a) * t;
function perlin(x: number, y: number) {
  const ix = Math.floor(x), iy = Math.floor(y);
  const u = fade(x - ix), v = fade(y - iy);
  const lower = lerp(gradientDot(ix, iy, x, y), gradientDot(ix + 1, iy, x, y), u);
  const upper = lerp(gradientDot(ix, iy + 1, x, y), gradientDot(ix + 1, iy + 1, x, y), u);
  return Math.max(0, Math.min(1, 0.5 + 0.5 * lerp(lower, upper, v)));
}
export function creatureLoss(amplitude: number, id: string, time: number, c: SimulationConfig) {
  if (amplitude <= 0) return 0;
  const x = (channel(id) % 997) * 0.173 + c.noiseSeed;
  return -lerp(amplitude * 2 / 3, amplitude, perlin(x, time * Math.max(0.01, c.frequency)));
}
