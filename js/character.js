// Мультяшные «чиби»-хоккеисты из скруглённых кубиков: большая голова, короткое тело.
import * as THREE from 'three';
import { RoundedBoxGeometry } from 'three/addons/geometries/RoundedBoxGeometry.js';

export const SKINS = ['#f3c9a3', '#e9b48c', '#cf956b', '#94603f', '#f7d9bd'];
export const HAIRS = ['#6b4226', '#2e1f16', '#c98a3c', '#e8c35c', '#8e4a26', '#1d1d22'];
export const TEAM_COLORS = [
  { main: '#e2483d', dark: '#9e2b25', accent: '#ffffff', pants: '#2b2f45', glove: '#7a2420' },
  { main: '#3f86e8', dark: '#22549c', accent: '#ffffff', pants: '#22283d', glove: '#1f3f78' },
];

const geoCache = new Map();
function rbox(w, h, d, r = 0.06) {
  const key = `${w}|${h}|${d}|${r}`;
  let g = geoCache.get(key);
  if (!g) {
    g = new RoundedBoxGeometry(w, h, d, 3, Math.min(r, Math.min(w, h, d) / 2 - 0.002));
    geoCache.set(key, g);
  }
  return g;
}
const matCache = new Map();
export function mat(color, extra = {}) {
  const key = color + JSON.stringify(extra);
  let m = matCache.get(key);
  if (!m) {
    m = new THREE.MeshStandardMaterial({ color, roughness: 0.72, metalness: 0, ...extra });
    matCache.set(key, m);
  }
  return m;
}
function part(geo, material, x, y, z, parent, shadow = true) {
  const m = new THREE.Mesh(geo, material);
  m.position.set(x, y, z);
  m.castShadow = shadow;
  m.receiveShadow = false;
  parent.add(m);
  return m;
}

function numberTexture(num, color) {
  const c = document.createElement('canvas');
  c.width = 128; c.height = 128;
  const g = c.getContext('2d');
  g.fillStyle = color;
  g.font = 'bold 84px "Trebuchet MS", Arial, sans-serif';
  g.textAlign = 'center'; g.textBaseline = 'middle';
  g.lineWidth = 8; g.strokeStyle = 'rgba(0,0,0,0.25)';
  g.strokeText(String(num), 64, 70);
  g.fillText(String(num), 64, 70);
  const t = new THREE.CanvasTexture(c);
  t.colorSpace = THREE.SRGBColorSpace;
  return t;
}

/**
 * Создаёт персонажа. Лицом смотрит в +Z, «левая» сторона — +X.
 */
export function buildPlayer({ team, skin = 0, hair = 0, number = 7, goalie = false }) {
  const tc = TEAM_COLORS[team];
  const M = {
    main: mat(tc.main), dark: mat(tc.dark), white: mat('#f4f4f2'), pants: mat(tc.pants), glove: mat(tc.glove),
    skin: mat(SKINS[skin % SKINS.length]), hair: mat(HAIRS[hair % HAIRS.length]), black: mat('#1b1b20', { roughness: 0.5 }),
    blade: mat('#cfd8e3', { metalness: 0.6, roughness: 0.3 }), boot: mat('#26262c'), wood: mat('#d9a066'), tape: mat('#202024'),
    cheek: mat('#ff8f8f', { transparent: true, opacity: 0.45 }), eyeHi: mat('#ffffff', { emissive: '#ffffff', emissiveIntensity: 0.4 }),
    cage: mat('#3a3a40', { metalness: 0.5, roughness: 0.4 }), brown: mat('#8a5a32'),
  };

  const root = new THREE.Group();
  const tilt = new THREE.Group();     // для падения
  root.add(tilt);
  const hips = new THREE.Group();
  hips.position.y = goalie ? 0.5 : 0.55;
  tilt.add(hips);

  // ноги
  const legs = [];
  for (const side of [1, -1]) {
    const leg = new THREE.Group();
    leg.position.set(side * 0.13, 0, 0);
    hips.add(leg);
    if (goalie) {
      part(rbox(0.3, 0.52, 0.32, 0.08), M.white, 0, -0.24, 0.06, leg);
      part(rbox(0.31, 0.08, 0.33, 0.03), M.main, 0, -0.12, 0.06, leg);
      part(rbox(0.31, 0.08, 0.33, 0.03), M.main, 0, -0.32, 0.06, leg);
    } else {
      part(rbox(0.18, 0.3, 0.19, 0.05), M.main, 0, -0.2, 0, leg);
      part(rbox(0.185, 0.06, 0.195, 0.02), M.white, 0, -0.2, 0, leg);
    }
    part(rbox(0.2, 0.15, 0.34, 0.05), M.boot, 0, -0.43, 0.04, leg);
    part(new THREE.BoxGeometry(0.035, 0.05, 0.38), M.blade, 0, -0.52, 0.04, leg);
    legs.push(leg);
  }

  // шорты
  part(rbox(goalie ? 0.58 : 0.5, 0.26, 0.38, 0.08), M.pants, 0, 0, 0, hips);
  part(rbox(goalie ? 0.585 : 0.505, 0.05, 0.385, 0.02), M.main, 0, -0.08, 0, hips);

  // торс
  const torso = new THREE.Group();
  torso.position.y = 0.1;
  hips.add(torso);
  const bw = goalie ? 0.72 : 0.6, bd = goalie ? 0.48 : 0.4;
  part(rbox(bw, 0.46, bd, 0.1), M.main, 0, 0.22, 0, torso);
  part(rbox(bw + 0.01, 0.07, bd + 0.01, 0.03), M.white, 0, 0.1, 0, torso);
  part(rbox(bw + 0.01, 0.04, bd + 0.01, 0.02), M.dark, 0, 0.05, 0, torso);
  // номер на спине
  const numMat = new THREE.MeshStandardMaterial({ map: numberTexture(number, '#ffffff'), transparent: true, roughness: 0.8 });
  const numPlane = new THREE.Mesh(new THREE.PlaneGeometry(0.34, 0.34), numMat);
  numPlane.position.set(0, 0.27, -bd / 2 - 0.006);
  numPlane.rotation.y = Math.PI;
  torso.add(numPlane);
  // маленький номер на груди
  const numPlane2 = new THREE.Mesh(new THREE.PlaneGeometry(0.16, 0.16), numMat);
  numPlane2.position.set(0.13, 0.3, bd / 2 + 0.006);
  torso.add(numPlane2);

  // голова
  const head = new THREE.Group();
  head.position.y = 0.44;
  torso.add(head);
  part(rbox(0.66, 0.6, 0.6, 0.16), M.skin, 0, 0.3, 0, head);
  const face = new THREE.Group();
  head.add(face);
  for (const s of [1, -1]) {
    part(rbox(0.075, 0.12, 0.04, 0.03), M.black, s * 0.14, 0.26, 0.3, face, false);
    part(new THREE.BoxGeometry(0.025, 0.03, 0.01), M.eyeHi, s * 0.14 + 0.015, 0.29, 0.322, face, false);
    part(rbox(0.13, 0.035, 0.03, 0.012), M.hair, s * 0.15, 0.36, 0.3, face, false);
    const ch = new THREE.Mesh(new THREE.CircleGeometry(0.05, 16), M.cheek);
    ch.position.set(s * 0.22, 0.17, 0.302);
    face.add(ch);
  }
  part(rbox(0.07, 0.06, 0.05, 0.02), M.skin, 0, 0.2, 0.31, face, false);
  part(rbox(0.1, 0.025, 0.02, 0.01), mat('#7a3b2e'), 0, 0.12, 0.3, face, false);
  // волосы, торчащие из-под шлема
  part(rbox(0.6, 0.2, 0.12, 0.05), M.hair, 0, 0.32, -0.27, head);
  for (const s of [1, -1]) part(rbox(0.08, 0.16, 0.14, 0.03), M.hair, s * 0.32, 0.36, 0.05, head);
  part(rbox(0.46, 0.07, 0.06, 0.025), M.hair, -0.04, 0.415, 0.29, head);

  if (goalie) {
    // маска вратаря с решёткой
    part(rbox(0.72, 0.66, 0.66, 0.2), M.white, 0, 0.32, -0.02, head);
    part(rbox(0.2, 0.67, 0.67, 0.06), M.main, 0, 0.33, -0.02, head);
    const bars = new THREE.Group();
    bars.position.set(0, 0.25, 0.335);
    head.add(bars);
    for (let i = -2; i <= 2; i++) part(new THREE.BoxGeometry(0.018, 0.36, 0.018), M.cage, i * 0.07, 0, 0, bars, false);
    for (let i = -1; i <= 1; i++) part(new THREE.BoxGeometry(0.34, 0.018, 0.018), M.cage, 0, i * 0.1, 0, bars, false);
    face.position.z = 0.03;
  } else {
    // шлем
    part(rbox(0.72, 0.26, 0.68, 0.12), M.main, 0, 0.56, -0.03, head);
    part(rbox(0.74, 0.04, 0.7, 0.02), M.dark, 0, 0.45, -0.03, head);
    for (const s of [1, -1]) part(rbox(0.08, 0.2, 0.22, 0.04), M.main, s * 0.35, 0.32, 0.0, head);
  }

  // руки
  const arms = [];
  for (const side of [1, -1]) {
    const arm = new THREE.Group();
    arm.position.set(side * (bw / 2 + 0.06), 0.38, 0);
    torso.add(arm);
    part(rbox(0.17, 0.32, 0.17, 0.06), M.main, 0, -0.14, 0, arm);
    part(rbox(0.175, 0.05, 0.175, 0.02), M.white, 0, -0.2, 0, arm);
    if (goalie && side === 1) part(rbox(0.3, 0.3, 0.14, 0.08), M.brown, 0, -0.34, 0.02, arm);
    else if (goalie) part(rbox(0.14, 0.34, 0.28, 0.05), M.white, 0, -0.34, 0.03, arm);
    else part(rbox(0.2, 0.18, 0.2, 0.06), M.glove, 0, -0.34, 0, arm);
    arms.push(arm);
  }

  // клюшка: шарнир у рук, ориентирована так, чтобы крюк лежал на льду справа-спереди
  const stickPivot = new THREE.Group();
  const handle = new THREE.Vector3(0.12, 0.2, 0.28);
  stickPivot.position.copy(handle);
  torso.add(stickPivot);
  const torsoWorldY = hips.position.y + torso.position.y;
  const bladeTarget = new THREE.Vector3(-0.3, 0.03 - torsoWorldY, 0.95);
  const dirv = bladeTarget.clone().sub(handle);
  const len = dirv.length();
  dirv.normalize();
  const stickOrient = new THREE.Group();
  stickOrient.quaternion.setFromUnitVectors(new THREE.Vector3(0, -1, 0), dirv);
  stickPivot.add(stickOrient);
  part(new THREE.BoxGeometry(0.045, len + 0.25, 0.045), M.wood, 0, -len / 2 + 0.12, 0, stickOrient);
  part(new THREE.BoxGeometry(0.05, 0.14, 0.05), M.tape, 0, 0.18, 0, stickOrient);
  const bladeHolder = new THREE.Group();
  bladeHolder.position.y = -len;
  stickOrient.add(bladeHolder);
  bladeHolder.quaternion.copy(stickOrient.quaternion).invert();
  const bl = goalie ? 0.5 : 0.36;
  part(new THREE.BoxGeometry(bl, goalie ? 0.12 : 0.08, 0.035), M.tape, -bl / 2 + 0.05, 0.02, 0.02, bladeHolder);

  const p = { root, tilt, hips, torso, head, legs, arms, stickPivot, goalie, phase: Math.random() * 6, fall: 0, celebrate: 0, baseHipY: hips.position.y };
  root.userData.char = p;
  return p;
}

const lerp = (a, b, t) => a + (b - a) * t;

/**
 * Процедурная анимация. s: {spd, charge, stun, swing, save, back, hasPuck}, celebrate: bool
 */
export function animatePlayer(c, s, dt, celebrate, time) {
  const k = 1 - Math.exp(-dt * 12);
  const spd = s.spd;
  c.phase += dt * (2.2 + spd * 0.85);
  c.fall = lerp(c.fall, s.stun ? 1 : 0, 1 - Math.exp(-dt * (s.stun ? 9 : 4)));
  c.celebrate = lerp(c.celebrate, celebrate ? 1 : 0, k);
  const amp = Math.min(1, spd / 7);
  const sinp = Math.sin(c.phase);

  if (c.goalie) {
    const sv = Math.min(1, s.save * 3);
    c.hips.position.y = lerp(c.hips.position.y, c.baseHipY - 0.08 - sv * 0.2, k);
    c.legs[0].rotation.z = lerp(c.legs[0].rotation.z, 0.25 + sv * 1.1, k);
    c.legs[1].rotation.z = lerp(c.legs[1].rotation.z, -0.25 - sv * 1.1, k);
    c.legs[0].rotation.x = lerp(c.legs[0].rotation.x, -0.25, k);
    c.legs[1].rotation.x = lerp(c.legs[1].rotation.x, -0.25, k);
    c.torso.rotation.x = lerp(c.torso.rotation.x, 0.35, k);
    c.arms[0].rotation.x = lerp(c.arms[0].rotation.x, -0.9 - sv * 0.8, k);
    c.arms[0].rotation.z = lerp(c.arms[0].rotation.z, 0.5 + sv * 0.6, k);
    c.arms[1].rotation.x = lerp(c.arms[1].rotation.x, -0.9, k);
    c.arms[1].rotation.z = lerp(c.arms[1].rotation.z, -0.2, k);
    c.stickPivot.rotation.x = lerp(c.stickPivot.rotation.x, -0.1 + sv * 0.2, k);
    c.stickPivot.rotation.y = lerp(c.stickPivot.rotation.y, 0.5, k);
    c.tilt.position.y = 0;
    return;
  }

  // ноги: боковой толчок как на коньках
  const back = s.back ? -1 : 1;
  const stride = amp * 0.55;
  c.legs[0].rotation.x = lerp(c.legs[0].rotation.x, sinp * stride * 0.6 * back - 0.15, k);
  c.legs[1].rotation.x = lerp(c.legs[1].rotation.x, -sinp * stride * 0.6 * back - 0.15, k);
  c.legs[0].rotation.z = lerp(c.legs[0].rotation.z, Math.max(0, sinp) * stride * 0.7 + 0.04, k);
  c.legs[1].rotation.z = lerp(c.legs[1].rotation.z, -Math.max(0, -sinp) * stride * 0.7 - 0.04, k);
  c.hips.position.y = lerp(c.hips.position.y, c.baseHipY - 0.03 - amp * 0.04 + Math.abs(sinp) * 0.025 * amp + c.celebrate * Math.max(0, Math.sin(time * 9)) * 0.25, k);
  c.hips.rotation.y = lerp(c.hips.rotation.y, sinp * 0.12 * amp, k);

  const lean = 0.18 + amp * 0.22 + s.charge * 0.15;
  c.torso.rotation.x = lerp(c.torso.rotation.x, lean - c.celebrate * 0.3, k);
  c.torso.rotation.y = lerp(c.torso.rotation.y, -s.charge * 0.5 + (s.swing > 0 ? 0.4 : 0) - sinp * 0.08 * amp, k);

  // руки держат клюшку
  const armUp = c.celebrate;
  c.arms[0].rotation.x = lerp(c.arms[0].rotation.x, lerp(-1.05, -2.9, armUp), k);
  c.arms[0].rotation.z = lerp(c.arms[0].rotation.z, lerp(-0.45, 0.2, armUp), k);
  c.arms[1].rotation.x = lerp(c.arms[1].rotation.x, lerp(-0.85, -2.9, armUp), k);
  c.arms[1].rotation.z = lerp(c.arms[1].rotation.z, lerp(0.1, -0.2, armUp), k);

  // клюшка: замах, бросок, дриблинг
  let sx = 0, sy = 0, sz = 0;
  if (s.charge > 0) { sx = s.charge * 1.3; sy = -s.charge * 0.6; sz = -s.charge * 0.3; }
  if (s.swing > 0) { sx = -0.55; sy = 0.5; }
  if (s.hasPuck && s.charge <= 0 && s.swing <= 0) sy = Math.sin(time * 9) * 0.15;
  if (armUp > 0.5) { sx = -1.6; }
  const kk = s.swing > 0 ? 1 - Math.exp(-dt * 40) : k;
  c.sx = lerp(c.sx || 0, sx, kk);
  c.stickPivot.rotation.x = c.sx - c.torso.rotation.x * 0.9;
  c.stickPivot.rotation.y = lerp(c.stickPivot.rotation.y, sy, kk);
  c.stickPivot.rotation.z = lerp(c.stickPivot.rotation.z, sz, kk);

  // падение после силового приёма
  c.tilt.rotation.x = -c.fall * 1.45;
  c.tilt.position.y = c.fall * 0.25;
  c.tilt.position.z = -c.fall * 0.35;
}
