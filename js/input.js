// Клавиатура, мышь и геймпады.
const keys = new Set();
let mouse = { left: false, right: false };
window.addEventListener('keydown', (e) => {
  keys.add(e.code);
  if (['Space', 'ArrowUp', 'ArrowDown', 'ArrowLeft', 'ArrowRight', 'Tab'].includes(e.code) && document.activeElement?.tagName !== 'INPUT') e.preventDefault();
});
window.addEventListener('keyup', (e) => keys.delete(e.code));
window.addEventListener('blur', () => { keys.clear(); mouse.left = mouse.right = false; });

export function attachMouse(el) {
  el.addEventListener('mousedown', (e) => { if (e.button === 0) mouse.left = true; if (e.button === 2) mouse.right = true; });
  window.addEventListener('mouseup', (e) => { if (e.button === 0) mouse.left = false; if (e.button === 2) mouse.right = false; });
  el.addEventListener('contextmenu', (e) => e.preventDefault());
}

export const SCHEMES = {
  p1: { up: ['KeyW'], down: ['KeyS'], left: ['KeyA'], right: ['KeyD'], shoot: ['Space'], pass: ['KeyQ'], check: ['KeyE'], sprint: ['ShiftLeft'] },
  p2: { up: ['ArrowUp'], down: ['ArrowDown'], left: ['ArrowLeft'], right: ['ArrowRight'], shoot: ['Enter', 'Numpad0'], pass: ['ShiftRight', 'Numpad1'], check: ['ControlRight', 'Numpad2'], sprint: ['Slash', 'Numpad3'] },
  solo: { up: ['KeyW', 'ArrowUp'], down: ['KeyS', 'ArrowDown'], left: ['KeyA', 'ArrowLeft'], right: ['KeyD', 'ArrowRight'], shoot: ['Space', 'KeyK'], pass: ['KeyQ', 'KeyJ'], check: ['KeyE', 'KeyL'], sprint: ['ShiftLeft', 'ShiftRight'] },
};

const any = (codes) => codes.some((c) => keys.has(c));

/**
 * Источник ввода: клавиатурная схема + номер геймпада (или null) + мышь.
 */
export function readInput({ scheme, pad = null, useMouse = false }) {
  const s = SCHEMES[scheme];
  const inp = { throttle: 0, turn: 0, shoot: false, pass: false, check: false, sprint: false };
  if (s) {
    inp.throttle = (any(s.up) ? 1 : 0) - (any(s.down) ? 1 : 0);
    inp.turn = (any(s.left) ? 1 : 0) - (any(s.right) ? 1 : 0);
    inp.shoot = any(s.shoot);
    inp.pass = any(s.pass);
    inp.check = any(s.check);
    inp.sprint = any(s.sprint);
  }
  if (useMouse) {
    inp.shoot ||= mouse.left;
    inp.pass ||= mouse.right;
  }
  if (pad !== null) {
    const gp = navigator.getGamepads ? navigator.getGamepads()[pad] : null;
    if (gp && gp.connected) {
      const dz = (v) => (Math.abs(v) < 0.18 ? 0 : v);
      const ax = dz(gp.axes[0] || 0), ay = dz(gp.axes[1] || 0);
      const b = (i) => !!(gp.buttons[i] && gp.buttons[i].pressed);
      const rt = gp.buttons[7] ? gp.buttons[7].value : 0;
      const lt = gp.buttons[6] ? gp.buttons[6].value : 0;
      let thr = -ay;
      if (rt > 0.1) thr = Math.max(thr, rt);
      if (lt > 0.1) thr = Math.min(thr, -lt);
      if (b(12)) thr = 1; if (b(13)) thr = -1;
      if (Math.abs(thr) > Math.abs(inp.throttle)) inp.throttle = thr;
      let tr = -ax;
      if (b(14)) tr = 1; if (b(15)) tr = -1;
      if (Math.abs(tr) > Math.abs(inp.turn)) inp.turn = tr;
      inp.shoot ||= b(0);
      inp.pass ||= b(1) || b(3);
      inp.check ||= b(2);
      inp.sprint ||= b(4) || b(5);
    }
  }
  return inp;
}

export function packInput(i) {
  return [Math.round(i.throttle * 100) / 100, Math.round(i.turn * 100) / 100, (i.shoot ? 1 : 0) | (i.pass ? 2 : 0) | (i.check ? 4 : 0) | (i.sprint ? 8 : 0)];
}
export function unpackInput(a) {
  return { throttle: +a[0] || 0, turn: +a[1] || 0, shoot: !!(a[2] & 1), pass: !!(a[2] & 2), check: !!(a[2] & 4), sprint: !!(a[2] & 8) };
}
export function isDown(code) { return keys.has(code); }
