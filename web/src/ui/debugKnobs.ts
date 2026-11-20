import type { SimulationConfig } from '../simulation/types';

type NumericKey = Exclude<keyof SimulationConfig, 'patrol'>;
type Knob = [NumericKey, string, number?];
const groups: [string, Knob[]][] = [
  ['Movement & sampling', [
    ['speed', 'Movement speed (m/s)', 0.1], ['interval', 'Sample interval (s)', 0.1],
    ['reference', 'Reference strength (dBm)'], ['lossPerMeter', 'Loss per meter (dB/m)', 0.001],
    ['noiseAmplitude', 'Measurement noise (±dB)', 0],
  ]],
  ['Interference history', [
    ['unstableThreshold', 'Unstable range (dB)', 0], ['severeThreshold', 'Severe range (dB)', 0],
  ]],
  ['Distance bands & loss', [
    ['strongRadius', 'Strong radius (m)', 0], ['strongAmplitude', 'Strong amplitude (dB)', 0],
    ['mediumRadius', 'Medium radius (m)', 0], ['mediumAmplitude', 'Medium amplitude (dB)', 0],
    ['weakRadius', 'Weak radius (m)', 0], ['weakAmplitude', 'Weak amplitude (dB)', 0],
    ['contaminationRadius', 'AP proximity (m)', 0],
  ]],
  ['Creature & patrol', [
    ['creatureX', 'Creature origin X (m)'], ['creatureY', 'Creature origin Y (m)'],
    ['patrolDistance', 'Patrol distance (m)', 0], ['patrolSpeed', 'Patrol speed (rad/s)', 0],
    ['patrolX', 'Patrol direction X'], ['patrolY', 'Patrol direction Y'],
  ]],
];
export function bindKnobs(config: SimulationConfig, reset: () => void) {
  const panel = document.querySelector<HTMLElement>('#settings')!;
  const note = document.createElement('p');
  note.textContent = 'Development only. Changes apply to this session.';
  panel.append(note);
  const fields = new Map<NumericKey, HTMLInputElement>();
  for (const [title, knobs] of groups) {
    const group = document.createElement('fieldset');
    const legend = document.createElement('legend');
    legend.textContent = title;
    group.append(legend);
    for (const [key, text, min] of knobs) {
      const label = document.createElement('label');
      label.textContent = text;
      const input = document.createElement('input');
      input.type = 'number';
      input.step = 'any';
      if (min !== undefined) input.min = String(min);
      input.value = String(config[key]);
      input.addEventListener('change', () => {
        if (Number.isFinite(input.valueAsNumber) && input.validity.valid) config[key] = input.valueAsNumber;
        input.value = String(config[key]);
      });
      fields.set(key, input);
      label.append(input);
      group.append(label);
    }
    panel.append(group);
  }
  function checkbox(text: string) {
    const label = document.createElement('label');
    const input = document.createElement('input');
    input.type = 'checkbox';
    label.append(input, text);
    panel.append(label);
    return input;
  }
  const patrol = checkbox('Enable sinusoidal patrol');
  patrol.addEventListener('change', () => { config.patrol = patrol.checked; });
  const debug = checkbox('Show interference geometry');
  const button = document.createElement('button');
  button.textContent = 'Reset to Unity defaults';
  button.addEventListener('click', () => {
    reset();
    for (const [key, field] of fields) field.value = String(config[key]);
    patrol.checked = config.patrol;
    debug.checked = false;
  });
  panel.append(button);
  return () => debug.checked;
}
