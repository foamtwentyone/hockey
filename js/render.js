// 3D-сцена: арена, зрители, игроки, шайба, камеры «из-за спины».
import * as THREE from 'three';
import { RoundedBoxGeometry } from 'three/addons/geometries/RoundedBoxGeometry.js';
import { buildPlayer, animatePlayer, mat, TEAM_COLORS } from './character.js';
import { RINK, wrapAngle } from './sim.js';

export const SLOT_COLORS = ['#ffd23f', '#5cf28a', '#ff7ad9', '#4fe3ff', '#ffa94d', '#b48cff'];

function rrPath(path, hx, hz, r) {
  path.moveTo(-hx + r, -hz);
  path.lineTo(hx - r, -hz);
  path.absarc(hx - r, -hz + r, r, -Math.PI / 2, 0, false);
  path.lineTo(hx, hz - r);
  path.absarc(hx - r, hz - r, r, 0, Math.PI / 2, false);
  path.lineTo(-hx + r, hz);
  path.absarc(-hx + r, hz - r, r, Math.PI / 2, Math.PI, false);
  path.lineTo(-hx, -hz + r);
  path.absarc(-hx + r, -hz + r, r, Math.PI, Math.PI * 1.5, false);
  return path;
}
function ringGeo(innerOff, outerOff, height) {
  const hx = RINK.L / 2, hz = RINK.W / 2, R = RINK.R;
  const shape = rrPath(new THREE.Shape(), hx + outerOff, hz + outerOff, R + outerOff);
  shape.holes.push(rrPath(new THREE.Path(), hx + innerOff, hz + innerOff, R + innerOff));
  const g = new THREE.ExtrudeGeometry(shape, { depth: height, bevelEnabled: false, curveSegments: 20 });
  g.rotateX(-Math.PI / 2);
  return g;
}
// точки вдоль периметра скруглённого прямоугольника
function perimeterPoints(off, spacing) {
  const hx = RINK.L / 2 + off, hz = RINK.W / 2 + off, r = RINK.R + off;
  const pts = [];
  const segs = [];
  const sx = hx - r, sz = hz - r;
  segs.push({ len: 2 * sx, f: (t) => [-sx + t, hz, 0, 1] });
  segs.push({ len: (Math.PI / 2) * r, f: (t) => { const a = t / r; return [sx + Math.sin(a) * r, sz + Math.cos(a) * r, Math.sin(a), Math.cos(a)]; } });
  segs.push({ len: 2 * sz, f: (t) => [hx, sz - t, 1, 0] });
  segs.push({ len: (Math.PI / 2) * r, f: (t) => { const a = t / r; return [sx + Math.cos(a) * r, -sz - Math.sin(a) * r, Math.cos(a), -Math.sin(a)]; } });
  segs.push({ len: 2 * sx, f: (t) => [sx - t, -hz, 0, -1] });
  segs.push({ len: (Math.PI / 2) * r, f: (t) => { const a = t / r; return [-sx - Math.sin(a) * r, -sz - Math.cos(a) * r, -Math.sin(a), -Math.cos(a)]; } });
  segs.push({ len: 2 * sz, f: (t) => [-hx, -sz + t, -1, 0] });
  segs.push({ len: (Math.PI / 2) * r, f: (t) => { const a = t / r; return [-sx - Math.cos(a) * r, sz + Math.sin(a) * r, -Math.cos(a), Math.sin(a)]; } });
  for (const s of segs) {
    const n = Math.max(1, Math.floor(s.len / spacing));
    for (let i = 0; i < n; i++) pts.push(s.f((i + 0.5) * (s.len / n)));
  }
  return pts;
}

function iceTexture() {
  const S = 32;
  const L = RINK.L, W = RINK.W;
  const c = document.createElement('canvas');
  c.width = L * S; c.height = W * S;
  const g = c.getContext('2d');
  const X = (x) => (x + L / 2) * S, Z = (z) => (z + W / 2) * S;
  const grd = g.createRadialGradient(c.width / 2, c.height / 2, 50, c.width / 2, c.height / 2, c.width / 1.6);
  grd.addColorStop(0, '#f7fbff'); grd.addColorStop(1, '#e3eef8');
  g.fillStyle = grd; g.fillRect(0, 0, c.width, c.height);
  // царапины
  g.strokeStyle = 'rgba(150,180,210,0.10)';
  g.lineWidth = 1.5;
  for (let i = 0; i < 260; i++) {
    const x = Math.random() * c.width, y = Math.random() * c.height, r = 30 + Math.random() * 200, a = Math.random() * 6;
    g.beginPath(); g.arc(x, y, r, a, a + 0.3 + Math.random() * 0.8); g.stroke();
  }
  const red = '#d9363e', blue = '#2f6fd6';
  const line = (x, w, col) => { g.fillStyle = col; g.fillRect(X(x) - (w * S) / 2, 0, w * S, c.height); };
  line(0, 0.3, red);
  g.fillStyle = 'rgba(255,255,255,0.6)';
  for (let z = -W / 2; z < W / 2; z += 1.2) g.fillRect(X(0) - 1, Z(z), 2, 0.6 * S);
  line(RINK.blueX, 0.3, blue); line(-RINK.blueX, 0.3, blue);
  line(RINK.goalX, 0.06, red); line(-RINK.goalX, 0.06, red);
  // площадь ворот
  for (const s of [1, -1]) {
    g.fillStyle = 'rgba(80,150,240,0.45)';
    g.beginPath();
    g.moveTo(X(s * RINK.goalX), Z(-1.8));
    g.arc(X(s * RINK.goalX), Z(0), 1.8 * S, s > 0 ? Math.PI / 2 : -Math.PI / 2, s > 0 ? Math.PI * 1.5 : Math.PI / 2, false);
    g.closePath(); g.fill();
    g.strokeStyle = red; g.lineWidth = 3; g.stroke();
  }
  const circle = (x, z, r, col, w = 3) => { g.strokeStyle = col; g.lineWidth = w; g.beginPath(); g.arc(X(x), Z(z), r * S, 0, Math.PI * 2); g.stroke(); };
  const dot = (x, z, r, col) => { g.fillStyle = col; g.beginPath(); g.arc(X(x), Z(z), r * S, 0, Math.PI * 2); g.fill(); };
  circle(0, 0, 4.5, blue, 4); dot(0, 0, 0.3, blue);
  for (const sx of [1, -1]) for (const sz of [1, -1]) {
    circle(sx * 20, sz * 7, 4.5, red, 4); dot(sx * 20, sz * 7, 0.35, red);
    dot(sx * 5, sz * 7, 0.3, red);
  }
  // логотип в центре
  g.save();
  g.translate(X(0), Z(0));
  g.globalAlpha = 0.18;
  g.fillStyle = '#1e3b70';
  g.font = `bold ${2.2 * S}px "Trebuchet MS", Arial, sans-serif`;
  g.textAlign = 'center'; g.textBaseline = 'middle';
  g.rotate(Math.PI / 2);
  g.fillText('ХОККЕЙ', 0, 0);
  g.restore();
  // рекламные надписи в зонах
  g.save();
  g.globalAlpha = 0.12; g.fillStyle = '#c0392b';
  g.font = `bold ${1.4 * S}px "Trebuchet MS", Arial`; g.textAlign = 'center';
  for (const s of [1, -1]) { g.save(); g.translate(X(s * 14), Z(0)); g.rotate(s * Math.PI / 2); g.fillText('ДВОР-ЛИГА', 0, 0); g.restore(); }
  g.restore();
  const t = new THREE.CanvasTexture(c);
  t.colorSpace = THREE.SRGBColorSpace;
  t.anisotropy = 8;
  return t;
}

function netTexture() {
  const c = document.createElement('canvas');
  c.width = c.height = 64;
  const g = c.getContext('2d');
  g.strokeStyle = 'rgba(255,255,255,0.95)';
  g.lineWidth = 3;
  for (let i = 0; i <= 64; i += 16) { g.beginPath(); g.moveTo(i, 0); g.lineTo(i, 64); g.stroke(); g.beginPath(); g.moveTo(0, i); g.lineTo(64, i); g.stroke(); }
  const t = new THREE.CanvasTexture(c);
  t.wrapS = t.wrapT = THREE.RepeatWrapping;
  t.repeat.set(6, 4);
  return t;
}

function labelSprite(text, color) {
  const c = document.createElement('canvas');
  c.width = 256; c.height = 64;
  const g = c.getContext('2d');
  g.font = 'bold 34px "Trebuchet MS", Arial, sans-serif';
  const w = Math.min(250, g.measureText(text).width + 30);
  g.fillStyle = 'rgba(20,24,36,0.72)';
  const x0 = 128 - w / 2;
  g.beginPath(); g.roundRect(x0, 8, w, 48, 22); g.fill();
  g.strokeStyle = color; g.lineWidth = 4; g.stroke();
  g.fillStyle = '#fff'; g.textAlign = 'center'; g.textBaseline = 'middle';
  g.fillText(text, 128, 33);
  const t = new THREE.CanvasTexture(c);
  t.colorSpace = THREE.SRGBColorSpace;
  const s = new THREE.Sprite(new THREE.SpriteMaterial({ map: t, depthTest: false, transparent: true, sizeAttenuation: false }));
  s.scale.set(0.16, 0.04, 1);
  s.renderOrder = 10;
  return s;
}

export class Renderer {
  constructor(container) {
    const r = new THREE.WebGLRenderer({ antialias: true, powerPreference: 'high-performance' });
    r.setPixelRatio(Math.min(window.devicePixelRatio, 2));
    r.shadowMap.enabled = true;
    r.shadowMap.type = THREE.PCFSoftShadowMap;
    r.toneMapping = THREE.ACESFilmicToneMapping;
    r.toneMappingExposure = 1.05;
    r.outputColorSpace = THREE.SRGBColorSpace;
    container.appendChild(r.domElement);
    this.gl = r;
    this.container = container;
    this.scene = new THREE.Scene();
    this.scene.background = new THREE.Color('#1a2033');
    this.scene.fog = new THREE.Fog('#1a2033', 55, 120);
    this.time = 0;
    this.chars = [];
    this.views = [];
    this.excite = 0;
    this.buildLights();
    this.buildArena();
    this.buildCrowd();
    this.buildPuck();
    this.resize();
    window.addEventListener('resize', () => this.resize());
  }

  resize() {
    const w = this.container.clientWidth, h = this.container.clientHeight;
    this.gl.setSize(w, h);
    this.w = w; this.h = h;
    this.layoutViews();
  }

  buildLights() {
    const s = this.scene;
    s.add(new THREE.HemisphereLight('#e8f3ff', '#7d8aa3', 1.25));
    const d = new THREE.DirectionalLight('#fff6e8', 2.1);
    d.position.set(-12, 32, 14);
    d.castShadow = true;
    d.shadow.mapSize.set(2048, 2048);
    const sc = d.shadow.camera;
    sc.left = -36; sc.right = 36; sc.top = 22; sc.bottom = -22; sc.near = 5; sc.far = 80;
    d.shadow.bias = -0.0005;
    d.shadow.normalBias = 0.02;
    d.shadow.radius = 4;
    s.add(d);
    const fill = new THREE.DirectionalLight('#bcd4ff', 0.5);
    fill.position.set(15, 20, -20);
    s.add(fill);
  }

  buildArena() {
    const s = this.scene;
    const hx = RINK.L / 2, hz = RINK.W / 2;
    // лёд
    const shape = rrPath(new THREE.Shape(), hx, hz, RINK.R);
    const iceGeo = new THREE.ShapeGeometry(shape, 24);
    const pos = iceGeo.attributes.position, uv = iceGeo.attributes.uv;
    for (let i = 0; i < pos.count; i++) {
      uv.setXY(i, (pos.getX(i) + hx) / RINK.L, (pos.getY(i) + hz) / RINK.W);
    }
    iceGeo.rotateX(-Math.PI / 2);
    const ice = new THREE.Mesh(iceGeo, new THREE.MeshStandardMaterial({ map: iceTexture(), roughness: 0.18, metalness: 0.0 }));
    ice.receiveShadow = true;
    s.add(ice);

    // борта
    const boards = new THREE.Mesh(ringGeo(0, 0.25, RINK.boardH), mat('#f5f5f0'));
    boards.receiveShadow = true; boards.castShadow = true;
    s.add(boards);
    const kick = new THREE.Mesh(ringGeo(-0.02, 0.27, 0.22), mat('#f2c230'));
    s.add(kick);
    const cap = new THREE.Mesh(ringGeo(-0.04, 0.32, 0.09), mat('#2a4c8f'));
    cap.position.y = RINK.boardH;
    s.add(cap);
    // рекламные полосы на бортах
    const adColors = ['#e2483d', '#3f86e8', '#2fbf71', '#f2a33a', '#9b59d0'];
    const adPts = perimeterPoints(-0.01, 4.2);
    adPts.forEach((p, i) => {
      if (i % 2) return;
      const m = new THREE.Mesh(new THREE.PlaneGeometry(3.2, 0.42), mat(adColors[(i / 2) % adColors.length]));
      m.position.set(p[0], 0.62, p[1]);
      m.lookAt(p[0] - p[2], 0.62, p[1] - p[3]);
      s.add(m);
    });
    // стекло
    const glass = new THREE.Mesh(ringGeo(0.08, 0.12, 2.1), new THREE.MeshStandardMaterial({ color: '#cfe8ff', transparent: true, opacity: 0.1, roughness: 0.05, depthWrite: false }));
    glass.position.y = RINK.boardH + 0.09;
    s.add(glass);
    const glassTop = new THREE.Mesh(ringGeo(0.06, 0.14, 0.05), mat('#9fb3c8'));
    glassTop.position.y = RINK.boardH + 2.19;
    s.add(glassTop);

    // трибуны
    const seatCols = ['#2b3554', '#34406a', '#2b3554', '#3c4a78', '#2b3554', '#34406a', '#2b3554'];
    this.rows = [];
    for (let i = 0; i < 7; i++) {
      const off = 2.8 + i * 1.1;
      const hgt = 0.6 + i * 0.75;
      const step = new THREE.Mesh(ringGeo(off, off + 1.1, hgt), mat(seatCols[i]));
      step.receiveShadow = true;
      s.add(step);
      this.rows.push({ off: off + 0.55, y: hgt });
    }
    // внешняя стена
    const wall = new THREE.Mesh(ringGeo(10.5, 11.2, 12), mat('#20283f'));
    s.add(wall);
    // пол вокруг площадки
    const floor = new THREE.Mesh(ringGeo(0.25, 3, 0.02), mat('#3a4258'));
    s.add(floor);

    // ворота
    this.buildGoal(1);
    this.buildGoal(-1);

    // табло-куб
    const cube = new THREE.Group();
    cube.position.set(0, 13, 0);
    const box = new THREE.Mesh(new RoundedBoxGeometry(6, 3, 6, 3, 0.25), mat('#232a3d'));
    cube.add(box);
    this.scoreCanvas = document.createElement('canvas');
    this.scoreCanvas.width = 512; this.scoreCanvas.height = 256;
    this.scoreTex = new THREE.CanvasTexture(this.scoreCanvas);
    this.scoreTex.colorSpace = THREE.SRGBColorSpace;
    const screenMat = new THREE.MeshStandardMaterial({ map: this.scoreTex, emissive: '#ffffff', emissiveMap: this.scoreTex, emissiveIntensity: 0.9 });
    for (let i = 0; i < 4; i++) {
      const sc = new THREE.Mesh(new THREE.PlaneGeometry(5.4, 2.6), screenMat);
      const a = (i * Math.PI) / 2;
      sc.position.set(Math.sin(a) * 3.02, 0, Math.cos(a) * 3.02);
      sc.rotation.y = a;
      cube.add(sc);
    }
    s.add(cube);
    this.drawScoreboard([0, 0], 0);

    // прожекторы под потолком
    const lampMat = new THREE.MeshStandardMaterial({ color: '#ffffff', emissive: '#fff4d6', emissiveIntensity: 2 });
    for (let i = -2; i <= 2; i++) for (const zz of [-12, 12]) {
      const l = new THREE.Mesh(new THREE.BoxGeometry(3, 0.3, 1.2), lampMat);
      l.position.set(i * 12, 17, zz);
      s.add(l);
    }
  }

  buildGoal(sgn) {
    const g = new THREE.Group();
    const red = mat('#e0322e', { roughness: 0.4 });
    const white = mat('#f2f2f2');
    const W = RINK.goalW, H = RINK.goalH, D = RINK.goalD;
    const post = new THREE.CylinderGeometry(0.06, 0.06, H, 12);
    for (const z of [-W / 2, W / 2]) {
      const p = new THREE.Mesh(post, red); p.position.set(0, H / 2, z); p.castShadow = true; g.add(p);
    }
    const bar = new THREE.Mesh(new THREE.CylinderGeometry(0.06, 0.06, W + 0.12, 12), red);
    bar.rotation.x = Math.PI / 2; bar.position.set(0, H, 0); bar.castShadow = true; g.add(bar);
    const base = new THREE.Mesh(new THREE.BoxGeometry(D, 0.06, 0.06), white);
    for (const z of [-W / 2, W / 2]) { const b = base.clone(); b.position.set(D / 2, 0.03, z); g.add(b); }
    const back = new THREE.Mesh(new THREE.BoxGeometry(0.06, 0.06, W), white); back.position.set(D, 0.03, 0); g.add(back);
    const netMat = new THREE.MeshStandardMaterial({ map: netTexture(), transparent: true, side: THREE.DoubleSide, depthWrite: false, roughness: 0.9, alphaTest: 0.05 });
    const top = new THREE.Mesh(new THREE.PlaneGeometry(D * 0.6, W), netMat);
    top.rotation.set(-Math.PI / 2, 0, 0);
    top.position.set(D * 0.3, H, 0);
    g.add(top);
    // наклонная задняя сетка
    const slant = Math.hypot(D * 0.4, H);
    const bk = new THREE.Mesh(new THREE.PlaneGeometry(slant, W), netMat);
    bk.rotation.set(-Math.PI / 2, Math.atan2(H, D * 0.4), 0);
    bk.position.set(D * 0.8, H / 2, 0);
    g.add(bk);
    const sideShape = new THREE.Shape();
    sideShape.moveTo(0, 0); sideShape.lineTo(D, 0); sideShape.lineTo(D * 0.6, H); sideShape.lineTo(0, H); sideShape.closePath();
    const sideGeo = new THREE.ShapeGeometry(sideShape);
    const sp = sideGeo.attributes.position, suv = sideGeo.attributes.uv;
    for (let i = 0; i < sp.count; i++) suv.setXY(i, sp.getX(i) / D * 0.3, sp.getY(i) / H * 0.5);
    for (const z of [-W / 2, W / 2]) { const sd = new THREE.Mesh(sideGeo, netMat); sd.position.z = z; g.add(sd); }
    g.position.x = sgn * RINK.goalX;
    if (sgn < 0) g.rotation.y = Math.PI;
    this.scene.add(g);
  }

  drawScoreboard(score, time) {
    const g = this.scoreCanvas.getContext('2d');
    g.fillStyle = '#0b0f1a'; g.fillRect(0, 0, 512, 256);
    g.fillStyle = TEAM_COLORS[0].main; g.fillRect(20, 20, 200, 40);
    g.fillStyle = TEAM_COLORS[1].main; g.fillRect(292, 20, 200, 40);
    g.fillStyle = '#fff'; g.font = 'bold 30px Arial'; g.textAlign = 'center'; g.textBaseline = 'middle';
    g.fillText('КРАСНЫЕ', 120, 42); g.fillText('СИНИЕ', 392, 42);
    g.font = 'bold 120px Arial'; g.fillStyle = '#ffd23f';
    g.fillText(String(score[0]), 120, 160); g.fillText(String(score[1]), 392, 160);
    g.font = 'bold 44px Arial'; g.fillStyle = '#7fe0ff';
    const m = Math.floor(time / 60), s = Math.floor(time % 60);
    g.fillText(`${m}:${String(s).padStart(2, '0')}`, 256, 160);
    this.scoreTex.needsUpdate = true;
  }

  buildCrowd() {
    const body = new RoundedBoxGeometry(0.5, 0.55, 0.36, 2, 0.12);
    const head = new RoundedBoxGeometry(0.38, 0.36, 0.36, 2, 0.12);
    const places = [];
    for (const row of this.rows) {
      for (const p of perimeterPoints(row.off, 0.75)) {
        if (Math.random() < 0.2) continue;
        places.push({ x: p[0], z: p[1], nx: p[2], nz: p[3], y: row.y, ph: Math.random() * 10, sp: 2 + Math.random() * 3 });
      }
    }
    const n = places.length;
    const bm = new THREE.InstancedMesh(body, new THREE.MeshStandardMaterial({ roughness: 0.8 }), n);
    const hm = new THREE.InstancedMesh(head, new THREE.MeshStandardMaterial({ roughness: 0.8 }), n);
    const shirt = ['#e2483d', '#3f86e8', '#ffd23f', '#2fbf71', '#f28c38', '#ffffff', '#9b59d0', '#e2483d', '#3f86e8', '#222831'];
    const skins = ['#f3c9a3', '#e9b48c', '#cf956b', '#94603f', '#f7d9bd'];
    const col = new THREE.Color();
    places.forEach((p, i) => {
      bm.setColorAt(i, col.set(shirt[Math.floor(Math.random() * shirt.length)]));
      hm.setColorAt(i, col.set(skins[Math.floor(Math.random() * skins.length)]));
    });
    this.scene.add(bm, hm);
    this.crowd = { bm, hm, places, m: new THREE.Matrix4(), q: new THREE.Quaternion(), v: new THREE.Vector3(), one: new THREE.Vector3(1, 1, 1), e: new THREE.Euler() };
    this.updateCrowd(0);
  }

  updateCrowd(dt) {
    const c = this.crowd;
    this.excite = Math.max(0, this.excite - dt * 0.25);
    const ex = this.excite;
    const t = this.time;
    c.places.forEach((p, i) => {
      const bob = Math.max(0, Math.sin(t * p.sp + p.ph)) * (0.03 + ex * 0.35);
      const yaw = Math.atan2(-p.nx, -p.nz);
      c.e.set(0, yaw, 0);
      c.q.setFromEuler(c.e);
      c.v.set(p.x, p.y + 0.28 + bob, p.z);
      c.m.compose(c.v, c.q, c.one);
      c.bm.setMatrixAt(i, c.m);
      c.v.set(p.x, p.y + 0.74 + bob, p.z);
      c.m.compose(c.v, c.q, c.one);
      c.hm.setMatrixAt(i, c.m);
    });
    c.bm.instanceMatrix.needsUpdate = true;
    c.hm.instanceMatrix.needsUpdate = true;
  }

  buildPuck() {
    const g = new THREE.Group();
    const m = new THREE.Mesh(new THREE.CylinderGeometry(0.13, 0.13, 0.07, 20), mat('#141418', { roughness: 0.4 }));
    m.position.y = 0.035;
    m.castShadow = true;
    g.add(m);
    this.puck = g;
    this.scene.add(g);
    const ring = new THREE.Mesh(new THREE.RingGeometry(0.28, 0.36, 32), new THREE.MeshBasicMaterial({ color: '#5fe1ff', transparent: true, opacity: 0.55, depthWrite: false }));
    ring.rotation.x = -Math.PI / 2;
    ring.position.y = 0.012;
    this.puckRing = ring;
    this.scene.add(ring);
    // след шайбы
    this.trailPts = [];
    const tg = new THREE.BufferGeometry();
    tg.setAttribute('position', new THREE.BufferAttribute(new Float32Array(16 * 3), 3));
    this.trail = new THREE.Line(tg, new THREE.LineBasicMaterial({ color: '#bff3ff', transparent: true, opacity: 0.6 }));
    this.trail.frustumCulled = false;
    this.scene.add(this.trail);
  }

  /** roster: [{team, human, name, number, goalie, skin, hair}], humanSlots: Map(humanKey -> slot index) */
  setRoster(roster, humanSlots) {
    for (const c of this.chars) { this.scene.remove(c.root); if (c.ring) this.scene.remove(c.ring); }
    this.chars = roster.map((r) => {
      const c = buildPlayer({ team: r.team, skin: r.skin, hair: r.hair, number: r.number, goalie: r.goalie });
      this.scene.add(c.root);
      if (r.human) {
        const slot = humanSlots.get(r.human) ?? 0;
        const color = SLOT_COLORS[slot % SLOT_COLORS.length];
        const lab = labelSprite(r.name, color);
        lab.position.y = 2.35;
        c.root.add(lab);
        const ring = new THREE.Mesh(new THREE.RingGeometry(0.52, 0.66, 32), new THREE.MeshBasicMaterial({ color, transparent: true, opacity: 0.8, depthWrite: false }));
        ring.rotation.x = -Math.PI / 2;
        ring.position.y = 0.015;
        c.ring = ring;
        this.scene.add(ring);
        const arrow = new THREE.Mesh(new THREE.ConeGeometry(0.16, 0.3, 4), new THREE.MeshBasicMaterial({ color }));
        arrow.rotation.x = Math.PI;
        arrow.position.y = 2.75;
        c.root.add(arrow);
        c.arrow = arrow;
      }
      c.team = r.team;
      return c;
    });
  }

  /** views: [{follow: playerIndex}] */
  setViews(views) {
    this.views = views.map((v) => ({ follow: v.follow, cam: new THREE.PerspectiveCamera(62, 1, 0.1, 300), yaw: null, rect: [0, 0, 1, 1], pos: new THREE.Vector3(), look: new THREE.Vector3() }));
    this.layoutViews();
  }

  layoutViews() {
    const n = this.views.length;
    this.views.forEach((v, i) => {
      v.rect = n === 1 ? [0, 0, this.w, this.h] : [(i * this.w) / 2, 0, this.w / 2, this.h];
      v.cam.aspect = v.rect[2] / v.rect[3];
      v.cam.updateProjectionMatrix();
    });
  }

  update(state, dt, roster) {
    this.time += dt;
    const ps = state.players;
    const celebrateTeam = state.phase === 'goal' && state.lastGoal ? state.lastGoal.team : -1;
    for (let i = 0; i < this.chars.length && i < ps.length; i++) {
      const c = this.chars[i], s = ps[i];
      c.root.position.set(s.x, 0, s.z);
      c.root.rotation.y = s.h;
      animatePlayer(c, { ...s, hasPuck: state.puck.owner === i }, dt, c.team === celebrateTeam && !c.goalie, this.time);
      if (c.ring) { c.ring.position.x = s.x; c.ring.position.z = s.z; }
      if (c.arrow) c.arrow.position.y = 2.75 + Math.sin(this.time * 4) * 0.08;
    }
    const pk = state.puck;
    this.puck.position.set(pk.x, pk.y, pk.z);
    this.puckRing.position.x = pk.x; this.puckRing.position.z = pk.z;
    this.puckRing.visible = pk.owner < 0;
    // след
    const tp = this.trailPts;
    tp.push(pk.x, pk.y + 0.04, pk.z);
    if (tp.length > 16 * 3) tp.splice(0, 3);
    const arr = this.trail.geometry.attributes.position.array;
    for (let i = 0; i < 16; i++) {
      const j = Math.min(i, tp.length / 3 - 1) * 3;
      arr[i * 3] = tp[j]; arr[i * 3 + 1] = tp[j + 1]; arr[i * 3 + 2] = tp[j + 2];
    }
    this.trail.geometry.attributes.position.needsUpdate = true;
    const n = tp.length / 3;
    const fast = n > 3 && Math.hypot(tp[tp.length - 3] - tp[tp.length - 12], tp[tp.length - 1] - tp[tp.length - 10]) > 1.2;
    this.trail.visible = fast && pk.owner < 0;

    if (state.phase === 'goal') this.excite = 1;
    this.updateCrowd(dt);
    const key = state.score.join(':') + '|' + Math.floor(state.time);
    if (key !== this.sbKey) { this.sbKey = key; this.drawScoreboard(state.score, state.time); }

    // камеры
    for (const v of this.views) {
      const s = ps[v.follow];
      if (!s) continue;
      if (v.yaw === null) { v.yaw = s.h; }
      let targetYaw = s.h;
      if (s.back && s.spd > 1) targetYaw = s.h; // остаётся за спиной
      v.yaw += wrapAngle(targetYaw - v.yaw) * (1 - Math.exp(-dt * 3.2));
      const dist = 6.4 + Math.min(s.spd, 10) * 0.12;
      const fx = Math.sin(v.yaw), fz = Math.cos(v.yaw);
      let cx = s.x - fx * dist, cz = s.z - fz * dist;
      const lim = 3.2;
      cx = Math.max(-RINK.L / 2 - lim, Math.min(RINK.L / 2 + lim, cx));
      cz = Math.max(-RINK.W / 2 - lim, Math.min(RINK.W / 2 + lim, cz));
      const target = new THREE.Vector3(cx, 3.4, cz);
      const look = new THREE.Vector3(s.x + fx * 3.5, 0.9, s.z + fz * 3.5);
      if (!v.init) { v.pos.copy(target); v.look.copy(look); v.init = true; }
      v.pos.lerp(target, 1 - Math.exp(-dt * 8));
      v.look.lerp(look, 1 - Math.exp(-dt * 10));
      v.cam.position.copy(v.pos);
      v.cam.lookAt(v.look);
      if (this.shake > 0) {
        v.cam.position.x += (Math.random() - 0.5) * this.shake;
        v.cam.position.y += (Math.random() - 0.5) * this.shake;
      }
    }
    this.shake = Math.max(0, (this.shake || 0) - dt * 1.5);
  }

  render() {
    const gl = this.gl;
    if (this.views.length <= 1) {
      gl.setScissorTest(false);
      gl.setViewport(0, 0, this.w, this.h);
      if (this.views[0]) gl.render(this.scene, this.views[0].cam);
      return;
    }
    gl.setScissorTest(true);
    for (const v of this.views) {
      const [x, y, w, h] = v.rect;
      gl.setViewport(x, y, w, h);
      gl.setScissor(x, y, w, h);
      gl.render(this.scene, v.cam);
    }
  }
}
