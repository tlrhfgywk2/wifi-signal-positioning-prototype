import type { Vec2 } from '../simulation/types';
export type Direction = 'up' | 'left' | 'down' | 'right';
const keys: Record<string, Direction> = {
  KeyW: 'up', ArrowUp: 'up', KeyA: 'left', ArrowLeft: 'left',
  KeyS: 'down', ArrowDown: 'down', KeyD: 'right', ArrowRight: 'right',
};
export function axes(keyCodes: Iterable<string>, pointers: Iterable<Direction>): Vec2 {
  const keyboard = new Set(Array.from(keyCodes, code => keys[code]));
  const pad = new Set(pointers);
  const axis = (positive: Direction, negative: Direction) => Number(keyboard.has(positive))
    - Number(keyboard.has(negative)) + Number(pad.has(positive)) - Number(pad.has(negative));
  return { x: axis('right', 'left'), y: axis('up', 'down') };
}
export function bindControls(pad: HTMLElement) {
  const heldKeys = new Set<string>();
  const pointers = new Map<number, Direction>();
  const buttons = Array.from(pad.querySelectorAll<HTMLButtonElement>('button[data-direction]'));
  function paint() {
    for (const button of buttons) {
      const dir = button.dataset.direction;
      const pressed = [...pointers.values()].includes(dir as Direction)
        || [...heldKeys].some(code => keys[code] === dir);
      button.classList.toggle('held', pressed);
    }
  }
  function clear() { heldKeys.clear(); pointers.clear(); paint(); }
  window.addEventListener('keydown', event => {
    if (!keys[event.code] || event.ctrlKey || event.metaKey || event.altKey
      || (event.target instanceof HTMLElement && event.target.closest('input, textarea, select, [contenteditable]'))) return;
    event.preventDefault();
    heldKeys.add(event.code);
    paint();
  });
  window.addEventListener('keyup', event => { if (heldKeys.delete(event.code)) paint(); });
  function release(event: PointerEvent) { if (pointers.delete(event.pointerId)) paint(); }
  for (const button of buttons) {
    button.addEventListener('pointerdown', event => {
      if (event.button !== 0) return;
      // Touch implicit capture would otherwise suppress exit when a finger slides away.
      if (button.hasPointerCapture(event.pointerId)) button.releasePointerCapture(event.pointerId);
      pointers.set(event.pointerId, button.dataset.direction as Direction);
      paint();
    });
    button.addEventListener('pointerleave', release);
    button.addEventListener('contextmenu', event => event.preventDefault());
  }
  window.addEventListener('pointerup', release);
  window.addEventListener('pointercancel', release);
  window.addEventListener('blur', clear);
  document.addEventListener('visibilitychange', clear);
  return () => axes(heldKeys, pointers.values());
}
