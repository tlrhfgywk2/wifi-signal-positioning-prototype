import { accessPoints } from '../simulation/config';
import type { Sample } from '../simulation/types';

// Mathf.RoundToInt uses nearest-even at exact half-integers.
export function roundedDbm(value: number) {
  const floor = Math.floor(value);
  return value - floor === 0.5 ? floor + Math.abs(floor % 2) : Math.round(value);
}
export function signalFraction(value: number) {
  return Number.isFinite(value) ? Math.min(1, Math.max(0, (value + 90) / 70)) : 0;
}
export function bindDiagnostics() {
  const rows = accessPoints.map(ap => {
    const row = document.createElement('div');
    row.className = 'ap-row';
    row.innerHTML = '<div class="ap-name"><i class="ap-icon"></i><b></b></div>'
      + '<div class="reading"><strong></strong> dBm</div><div class="bar"><i></i></div>'
      + '<span class="delta"></span><span class="status"></span><small class="path" hidden></small>';
    row.querySelector('b')!.textContent = ap.id;
    document.querySelector('#rows')!.append(row);
    return {
      dbm: row.querySelector('strong')!, bar: row.querySelector<HTMLElement>('.bar i')!,
      delta: row.querySelector<HTMLElement>('.delta')!, status: row.querySelector<HTMLElement>('.status')!,
      path: row.querySelector<HTMLElement>('.path')!,
    };
  });
  const field = (id: string) => document.getElementById(id)!;
  const x = field('x'), y = field('y'), error = field('error'), rate = field('rate');
  const invalid = field('invalid'), coordinates = field('coordinates'), errorLine = field('error-line');
  return (sample: Sample, interval: number, debug: boolean) => {
    rate.textContent = Number((1 / interval).toFixed(2)) + ' Hz';
    sample.observations.forEach((o, i) => {
      const row = rows[i]!;
      row.dbm.textContent = Number.isFinite(o.measured) ? String(roundedDbm(o.measured)) : '—';
      row.bar.style.width = (100 * signalFraction(o.measured)) + '%';
      const delta = Math.sign(o.delta) * Math.round(Math.abs(o.delta));
      row.delta.textContent = 'Δ ' + (Number.isFinite(delta) ? (delta > 0 ? '+' : '') + delta : '—') + ' dB';
      row.status.textContent = o.status;
      row.status.classList.toggle('affected', o.status !== 'STABLE');
      row.path.hidden = !debug;
      if (debug) row.path.textContent = 'Path distance ' + o.geometry.pathDistance.toFixed(3) + ' m';
    });
    const e = sample.estimate;
    invalid.hidden = !!e;
    coordinates.hidden = errorLine.hidden = !e;
    if (e) {
      x.textContent = e.position.x.toFixed(2) + ' m';
      y.textContent = e.position.y.toFixed(2) + ' m';
      error.textContent = e.error.toFixed(3) + ' m';
    }
  };
}
