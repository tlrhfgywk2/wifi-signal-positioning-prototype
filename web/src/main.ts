import './styles/main.css';
import { defaults } from './simulation/config';
import { measurementRandom } from './simulation/random';
import { Simulation } from './simulation/simulation';
import { bindControls } from './input/controls';
import { createWorld } from './rendering/scene';
import { bindDiagnostics } from './ui/diagnostics';
import { bindKnobs } from './ui/debugKnobs';

function start() {
  const viewport = document.querySelector<HTMLElement>('#viewport')!;
  let render: ReturnType<typeof createWorld>;
  try {
    render = createWorld(viewport);
  } catch {
    viewport.setAttribute('role', 'alert');
    viewport.removeAttribute('aria-label');
    viewport.textContent = 'The room needs WebGL 2. Enable hardware acceleration or open this page in a WebGL 2 compatible browser, then reload.';
    return;
  }
  const config = { ...defaults };
  const seed = new URLSearchParams(location.search).get('seed');
  const createSimulation = () => new Simulation(config, measurementRandom(seed));
  let simulation = createSimulation();
  const input = bindControls(document.querySelector('#pad')!);
  const diagnostics = bindDiagnostics();
  const debugEnabled = bindKnobs(config, () => {
    Object.assign(config, defaults);
    simulation = createSimulation();
  });
  let previous: number | undefined;
  let displayed: Simulation['state']['sample'] | undefined;
  let previousDebug = false;
  document.addEventListener('visibilitychange', () => { previous = undefined; });
  function frame(now: number) {
    if (!document.hidden) {
      if (previous !== undefined) simulation.step((now - previous) / 1000, input());
      previous = now;
      const state = simulation.state, debug = debugEnabled();
      render(state, config, debug);
      if (displayed !== state.sample || previousDebug !== debug) {
        diagnostics(state.sample, config.interval, debug);
        displayed = state.sample;
        previousDebug = debug;
      }
    }
    requestAnimationFrame(frame);
  }
  requestAnimationFrame(frame);
}
start();
