// Diagnostic measurement stream. Creature gradient noise uses its own Unity seed.
export function measurementRandom(seed: string | null): () => number {
  if (seed === null || !/^\d{1,10}$/.test(seed) || Number(seed) > 0xffffffff) return Math.random;
  let state = Number(seed);
  return () => {
    state = (Math.imul(state, 1664525) + 1013904223) >>> 0;
    return state / 0x100000000;
  };
}
