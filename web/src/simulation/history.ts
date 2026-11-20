import { HISTORY_CAPACITY } from './config';
import type { Band, SimulationConfig } from './types';
export class SignalHistory {
  private readonly values = new Float64Array(HISTORY_CAPACITY);
  private next = 0;
  count = 0;
  recent = 0;

  record(measured: number, level: Band) {
    this.recent = level === 'none' ? Math.max(0, this.recent - 1) : this.values.length;
    if (!Number.isFinite(measured)) return;
    this.values[this.next] = measured;
    this.next = (this.next + 1) % this.values.length;
    this.count = Math.min(this.values.length, this.count + 1);
  }

  get range() {
    if (this.count < 2) return 0;
    const active = this.values.subarray(0, this.count);
    return Math.max(...active) - Math.min(...active);
  }
}

export function classify(contaminated: boolean, level: Band, range: number, recent: boolean,
  config: Pick<SimulationConfig, 'severeThreshold' | 'unstableThreshold'>) {
  if (contaminated) return 'AP CONTAMINATED';
  if (level === 'strong' || (recent && range >= config.severeThreshold)) return 'SEVERE INTERFERENCE';
  if (level === 'medium' || (recent && range >= config.unstableThreshold)) return 'UNSTABLE';
  if (level === 'weak') return 'WEAK INTERFERENCE';
  return 'STABLE';
}
