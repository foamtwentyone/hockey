// Симуляция хоккейного матча: физика, шайба, вратари, боты.
// Работает одинаково в одиночной игре, на одном компьютере и на хосте онлайн-матча.

export const RINK = {
  L: 60,          // длина площадки (ось X)
  W: 30,          // ширина (ось Z)
  R: 8,           // радиус закруглений
  goalX: 26,      // линия ворот
  goalW: 3.2,     // ширина ворот
  goalH: 1.3,     // высота ворот
  goalD: 1.2,     // глубина сетки
  boardH: 1.1,
  blueX: 8,
};

export const TEAM_DIR = [1, -1]; // направление атаки команд вдоль X
export const TEAM_NAMES = ['Красные', 'Синие'];

const BOT_NAMES = ['Медведь', 'Шайба', 'Молния', 'Танк', 'Ёжик', 'Кузя', 'Буран', 'Вихрь',
  'Сокол', 'Барс', 'Пельмень', 'Ракета', 'Гром', 'Лось', 'Бублик', 'Тайфун', 'Филин', 'Колобок'];

const DIFF = [
  { speed: 0.84, react: 0.55, shootErr: 0.16, save: 0.45, steal: 1.2, check: 0.8 },
  { speed: 0.93, react: 0.8, shootErr: 0.09, save: 0.56, steal: 1.8, check: 1.3 },
  { speed: 1.0, react: 1.0, shootErr: 0.05, save: 0.66, steal: 2.4, check: 1.8 },
];

const STEP_EMPTY = { throttle: 0, turn: 0, shoot: false, pass: false, check: false, sprint: false };
const PLAYER_R = 0.45;
const PUCK_R = 0.12;
const G = 9.8;

const clamp = (v, a, b) => (v < a ? a : v > b ? b : v);
const hyp = Math.hypot;
export function wrapAngle(a) {
  while (a > Math.PI) a -= Math.PI * 2;
  while (a < -Math.PI) a += Math.PI * 2;
  return a;
}

// Выталкивание точки внутрь закруглённого прямоугольника площадки.
export function rinkClamp(x, z, r) {
  const hx = RINK.L / 2, hz = RINK.W / 2, R = RINK.R;
  const cx = hx - R, cz = hz - R;
  const ax = Math.abs(x), az = Math.abs(z);
  if (ax > cx && az > cz) {
    const dx = ax - cx, dz = az - cz;
    const d = hyp(dx, dz);
    if (d > R - r) {
      const nx = dx / d, nz = dz / d;
      return { x: Math.sign(x) * (cx + nx * (R - r)), z: Math.sign(z) * (cz + nz * (R - r)), nx: -Math.sign(x) * nx, nz: -Math.sign(z) * nz, hit: true };
    }
    return null;
  }
  let hit = false, nx = 0, nz = 0, ox = x, oz = z;
  if (ax > hx - r) { ox = Math.sign(x) * (hx - r); nx = -Math.sign(x); hit = true; }
  if (az > hz - r) { oz = Math.sign(z) * (hz - r); nz = -Math.sign(z); hit = true; }
  if (!hit) return null;
  const l = hyp(nx, nz);
  return { x: ox, z: oz, nx: nx / l, nz: nz / l, hit };
}

function rnd(a, b) { return a + Math.random() * (b - a); }

export class Sim {
  /**
   * cfg: { skaters, duration (сек), difficulty (0..2), humans: [{key, name, team}] }
   */
  constructor(cfg) {
    this.cfg = cfg;
    this.diff = DIFF[cfg.difficulty ?? 1];
    this.players = [];
    this.score = [0, 0];
    this.time = cfg.duration;
    this.simTime = 0;
    this.overtime = false;
    this.phase = 'faceoff';
    this.phaseT = 2.5;
    this.lastGoal = null; // {team, scorer, assist}
    this.events = [];
    this.shotSeq = 0;
    this.puck = { x: 0, y: 0, z: 0, vx: 0, vy: 0, vz: 0, owner: -1, holdT: 0, lastTouch: [-1, -1], passTarget: -1, passT: 0, saveCheck: {}, inNet: false };
    this.botNames = [...BOT_NAMES].sort(() => Math.random() - 0.5);
    this.buildTeams();
    this.faceoff();
  }

  buildTeams() {
    const { skaters, humans } = this.cfg;
    const usedNums = [new Set(), new Set()];
    const num = (t) => { let n; do { n = 2 + Math.floor(Math.random() * 97); } while (usedNums[t].has(n)); usedNums[t].add(n); return n; };
    for (let t = 0; t < 2; t++) {
      const hs = humans.filter((h) => h.team === t);
      const count = Math.max(skaters, hs.length);
      for (let i = 0; i < count; i++) {
        const h = hs[i];
        this.addPlayer({ team: t, human: h ? h.key : null, name: h ? h.name : this.botNames.pop() || 'Бот', number: num(t), goalie: false });
      }
      this.addPlayer({ team: t, human: null, name: t === 0 ? 'Вратарь' : 'Вратарь', number: t === 0 ? 1 : 30, goalie: true });
    }
  }

  addPlayer(o) {
    const p = {
      idx: this.players.length, team: o.team, human: o.human, name: o.name, number: o.number, goalie: o.goalie,
      x: 0, z: 0, vx: 0, vz: 0, h: 0,
      charge: 0, stun: 0, checking: 0, checkCd: 0, noPick: 0, swing: 0, save: 0, stamina: 1,
      prev: { ...STEP_EMPTY }, bot: { shootT: -1, chargeFor: 0, think: 0, passWish: 0 }, goals: 0, assists: 0, lane: 0,
      skin: Math.floor(Math.random() * 5), hair: Math.floor(Math.random() * 6),
    };
    this.players.push(p);
    return p;
  }

  roster() {
    return this.players.map((p) => ({ team: p.team, human: p.human, name: p.name, number: p.number, goalie: p.goalie, skin: p.skin, hair: p.hair }));
  }

  // Подключение игрока во время матча: заменяет бота.
  replaceBotWithHuman(key, name, team) {
    let cand = this.players.filter((p) => !p.goalie && !p.human && p.team === team);
    if (!cand.length) cand = this.players.filter((p) => !p.goalie && !p.human);
    if (!cand.length) return null;
    const p = cand[0];
    p.human = key; p.name = name;
    return p;
  }

  humanLeft(key) {
    for (const p of this.players) if (p.human === key) { p.human = null; p.name = this.botNames.pop() || 'Бот'; }
  }

  ev(type, data = {}) { this.events.push({ type, ...data }); }

  faceoff() {
    const spots = [[-1.6, 0], [-6, 7], [-6, -7], [-12, 0], [-11, 10], [-11, -10], [-16, 4], [-16, -4]];
    for (const t of [0, 1]) {
      const team = this.players.filter((p) => p.team === t);
      let i = 0;
      for (const p of team) {
        p.vx = p.vz = 0; p.charge = 0; p.stun = 0; p.checking = 0; p.swing = 0; p.save = 0;
        p.h = TEAM_DIR[t] > 0 ? Math.PI / 2 : -Math.PI / 2;
        if (p.goalie) { p.x = -TEAM_DIR[t] * (RINK.goalX - 0.9); p.z = 0; continue; }
        const s = spots[i % spots.length];
        p.lane = i;
        p.x = s[0] * TEAM_DIR[t]; p.z = s[1] * (t === 0 ? 1 : -1);
        i++;
      }
    }
    const pk = this.puck;
    Object.assign(pk, { x: 0, y: 0, z: 0, vx: 0, vy: 0, vz: 0, owner: -1, holdT: 0, passTarget: -1, saveCheck: {}, inNet: false });
    this.phase = 'faceoff';
    this.phaseT = 2.2;
  }

  // ---------- основной шаг ----------
  step(dt, inputs) {
    this.simTime += dt;
    if (this.phase === 'faceoff') {
      this.phaseT -= dt;
      if (this.phaseT <= 0) {
        this.phase = 'play';
        const pk = this.puck;
        pk.vx = rnd(-1, 1); pk.vz = rnd(-1.5, 1.5); pk.y = 0.6; pk.vy = 0;
        this.ev('whistle');
      }
      return;
    }
    if (this.phase === 'play') {
      if (!this.overtime) {
        this.time -= dt;
        if (this.time <= 0) {
          this.time = 0;
          if (this.score[0] === this.score[1]) { this.overtime = true; this.ev('overtime'); this.ev('horn'); }
          else { this.phase = 'end'; this.ev('horn'); this.ev('end'); }
        }
      }
    } else if (this.phase === 'goal') {
      this.phaseT -= dt;
      if (this.phaseT <= 0) {
        if (this.overtime) { this.phase = 'end'; this.ev('end'); }
        else this.faceoff();
        return;
      }
    }

    const live = this.phase === 'play';
    this.updateRoles();
    for (const p of this.players) {
      let inp;
      if (p.goalie) inp = STEP_EMPTY;
      else if (p.human) inp = (inputs && inputs.get(p.human)) || STEP_EMPTY;
      else inp = live ? this.botInput(p, dt) : this.idleBotInput(p);
      if (this.phase === 'end') inp = STEP_EMPTY;
      if (p.goalie) this.updateGoalie(p, dt);
      else this.updateSkater(p, inp, dt, live);
      p.prev = { ...inp };
    }
    this.collidePlayers();
    this.updatePuck(dt);
  }

  // ---------- полевые игроки ----------
  forward(p) { return [Math.sin(p.h), Math.cos(p.h)]; }
  blade(p) {
    const fx = Math.sin(p.h), fz = Math.cos(p.h);
    const rx = -fz, rz = fx;
    return [p.x + fx * 0.95 + rx * 0.28, p.z + fz * 0.95 + rz * 0.28];
  }

  updateSkater(p, inp, dt, live) {
    p.checkCd = Math.max(0, p.checkCd - dt);
    p.noPick = Math.max(0, p.noPick - dt);
    p.swing = Math.max(0, p.swing - dt);
    p.checking = Math.max(0, p.checking - dt);
    if (p.stun > 0) {
      p.stun -= dt;
      inp = STEP_EMPTY;
      p.charge = 0;
    }
    const fx = Math.sin(p.h), fz = Math.cos(p.h);
    let spd = hyp(p.vx, p.vz);

    // поворот
    const turnRate = clamp(3.7 - spd * 0.17, 2.1, 3.7);
    p.h = wrapAngle(p.h + clamp(inp.turn, -1, 1) * turnRate * dt);

    // сцепление коньков: боковая скорость частично переходит в продольную
    const nfx = Math.sin(p.h), nfz = Math.cos(p.h);
    let fwd = p.vx * nfx + p.vz * nfz;
    let lx = p.vx - fwd * nfx, lz = p.vz - fwd * nfz;
    const lat = hyp(lx, lz);
    const grip = 1 - Math.exp(-(p.stun > 0 ? 0.5 : 5.5) * dt);
    fwd += Math.sign(fwd || 1) * lat * grip * 0.75;
    lx *= 1 - grip; lz *= 1 - grip;

    const hasPuck = this.puck.owner === p.idx;
    const botMul = p.human ? 1 : this.diff.speed;
    const sprinting = inp.sprint && p.stamina > 0.05 && inp.throttle > 0;
    if (sprinting) p.stamina = Math.max(0, p.stamina - dt * 0.33);
    else p.stamina = Math.min(1, p.stamina + dt * 0.18);
    let maxS = (sprinting ? 10.8 : 8.2) * botMul * (hasPuck ? 0.93 : 1);
    const accel = sprinting ? 13 : 10.5;

    if (inp.throttle > 0) {
      if (fwd < maxS) fwd = Math.min(maxS, fwd + accel * inp.throttle * dt);
    } else if (inp.throttle < 0) {
      if (fwd > 0.4) fwd -= Math.min(fwd, 16 * dt);
      else if (fwd > -3.6) fwd -= 7 * dt;
    }
    p.vx = fwd * nfx + lx;
    p.vz = fwd * nfz + lz;
    const fr = inp.throttle === 0 ? 0.45 : 0.2;
    p.vx *= 1 - fr * dt; p.vz *= 1 - fr * dt;
    spd = hyp(p.vx, p.vz);
    const cap = maxS * 1.35;
    if (spd > cap) { p.vx *= cap / spd; p.vz *= cap / spd; }

    if (live) {
      // силовой приём
      if (inp.check && !p.prev.check && p.checkCd <= 0 && p.stun <= 0) {
        p.checking = 0.35; p.checkCd = 1.3;
        p.vx += nfx * 5; p.vz += nfz * 5;
        this.ev('whoosh');
      }
      // бросок: удержание — замах, отпускание — бросок
      if (inp.shoot) p.charge = Math.min(1, p.charge + dt / 0.9);
      else if (p.prev.shoot && p.stun <= 0) { this.shoot(p, p.charge); p.charge = 0; }
      else p.charge = 0;
      if (inp.pass && !p.prev.pass && p.stun <= 0) this.pass(p);
    } else {
      p.charge = 0;
    }

    p.x += p.vx * dt; p.z += p.vz * dt;
    const c = rinkClamp(p.x, p.z, PLAYER_R);
    if (c) {
      p.x = c.x; p.z = c.z;
      const vn = p.vx * c.nx + p.vz * c.nz;
      if (vn < 0) { p.vx -= vn * c.nx * 1.4; p.vz -= vn * c.nz * 1.4; if (vn < -6) this.ev('board', { v: -vn }); }
    }
    this.collideGoalFrames(p, PLAYER_R);
  }

  collideGoalFrames(o, r) {
    for (const s of [1, -1]) {
      const x0 = RINK.goalX - 0.08, x1 = RINK.goalX + RINK.goalD + 0.1, hz = RINK.goalW / 2 + 0.12;
      const ax = o.x * s;
      const cx = clamp(ax, x0, x1), cz = clamp(o.z, -hz, hz);
      const dx = ax - cx, dz = o.z - cz;
      const d = hyp(dx, dz);
      if (d < r) {
        if (d > 1e-4) {
          const nx = dx / d, nz = dz / d;
          o.x = (cx + nx * r) * s; o.z = cz + nz * r;
          const vn = o.vx * s * nx + o.vz * nz;
          if (vn < 0) { o.vx -= vn * nx * s; o.vz -= vn * nz; }
        } else {
          o.x = (x0 - r) * s;
        }
      }
    }
  }

  collidePlayers() {
    const ps = this.players;
    for (let i = 0; i < ps.length; i++) {
      for (let j = i + 1; j < ps.length; j++) {
        const a = ps[i], b = ps[j];
        const dx = b.x - a.x, dz = b.z - a.z;
        const d = hyp(dx, dz);
        const min = PLAYER_R * 2;
        if (d >= min || d < 1e-5) continue;
        const nx = dx / d, nz = dz / d;
        const ma = a.goalie ? 6 : 1, mb = b.goalie ? 6 : 1;
        const pen = min - d;
        a.x -= nx * pen * mb / (ma + mb); a.z -= nz * pen * mb / (ma + mb);
        b.x += nx * pen * ma / (ma + mb); b.z += nz * pen * ma / (ma + mb);
        const rv = (b.vx - a.vx) * nx + (b.vz - a.vz) * nz;
        if (rv < 0) {
          const imp = (-(1.3) * rv) / (1 / ma + 1 / mb);
          a.vx -= (imp / ma) * nx; a.vz -= (imp / ma) * nz;
          b.vx += (imp / mb) * nx; b.vz += (imp / mb) * nz;
        }
        if (a.team !== b.team && this.phase === 'play') {
          if (a.checking > 0 && !b.goalie && b.stun <= 0) this.knock(a, b, nx, nz, -rv);
          else if (b.checking > 0 && !a.goalie && a.stun <= 0) this.knock(b, a, -nx, -nz, -rv);
        }
      }
    }
  }

  knock(hitter, victim, nx, nz, closing) {
    if (closing < 1.5) return;
    victim.stun = 1.4;
    victim.vx = nx * (4 + closing * 0.5) + hitter.vx * 0.3;
    victim.vz = nz * (4 + closing * 0.5) + hitter.vz * 0.3;
    victim.charge = 0;
    hitter.checking = 0;
    this.ev('hit', { v: closing });
    if (this.puck.owner === victim.idx) {
      const pk = this.puck;
      pk.owner = -1;
      pk.vx = victim.vx * 0.8 + rnd(-2, 2); pk.vz = victim.vz * 0.8 + rnd(-2, 2);
      victim.noPick = 0.8;
    }
  }

  // ---------- бросок и пас ----------
  shoot(p, charge) {
    const pk = this.puck;
    p.swing = 0.32;
    const [bx, bz] = this.blade(p);
    const own = pk.owner === p.idx;
    const oneTimer = !own && pk.owner < 0 && p.noPick <= 0 && pk.y < 0.8 && hyp(pk.x - bx, pk.z - bz) < 1.35;
    if (!own && !oneTimer) { this.ev('swish'); return; }

    const power = charge < 0.12 ? 0.3 : charge;
    const speed = 15 + 22 * power + (oneTimer ? 4 : 0);
    let dx = Math.sin(p.h), dz = Math.cos(p.h);
    let vy = 0.4 + 3.2 * power * rnd(0.6, 1);

    // автоприцел: если игрок смотрит примерно в ворота
    const gx = TEAM_DIR[p.team] * RINK.goalX;
    const keeper = this.players.find((q) => q.goalie && q.team !== p.team);
    let tz = rnd(0.55, 1.25) * (keeper && keeper.z > 0 ? -1 : keeper && keeper.z < 0 ? 1 : Math.random() < 0.5 ? -1 : 1);
    const tx = gx - pk.x, tzz = tz - pk.z;
    const dist = hyp(tx, tzz);
    const ang = Math.abs(wrapAngle(Math.atan2(tx, tzz) - p.h));
    const facingGoal = TEAM_DIR[p.team] * (gx - pk.x) > 0.5;
    if (facingGoal && ang < 0.65 && dist < 30) {
      const k = 0.9 - ang * 0.6;
      dx = dx * (1 - k) + (tx / dist) * k; dz = dz * (1 - k) + (tzz / dist) * k;
      const l = hyp(dx, dz); dx /= l; dz /= l;
      const th = rnd(0.15, 1.05) * (0.6 + power * 0.5);
      const t = dist / speed;
      vy = clamp((th - 0.1 + 0.5 * G * t * t) / t, 0, 7.5);
    }
    const err = (p.human ? 0.035 : this.diff.shootErr) * (0.6 + power) * (oneTimer ? 1.6 : 1);
    const a = Math.atan2(dx, dz) + rnd(-err, err);
    dx = Math.sin(a); dz = Math.cos(a);

    pk.owner = -1;
    pk.x = bx; pk.z = bz; pk.y = 0.08;
    pk.vx = dx * speed + p.vx * 0.25; pk.vz = dz * speed + p.vz * 0.25; pk.vy = vy;
    pk.saveCheck = {};
    pk.passTarget = -1;
    this.touch(p);
    p.noPick = 0.35;
    this.ev('shot', { power, x: bx, z: bz });
  }

  pass(p) {
    const pk = this.puck;
    if (pk.owner !== p.idx) { this.ev('swish'); p.swing = 0.2; return; }
    const [fx, fz] = this.forward(p);
    let best = null, bestScore = Infinity;
    for (const q of this.players) {
      if (q === p || q.team !== p.team || q.goalie) continue;
      const dx = q.x - p.x, dz = q.z - p.z;
      const d = hyp(dx, dz);
      if (d < 2) continue;
      const ang = Math.acos(clamp((dx * fx + dz * fz) / d, -1, 1));
      if (ang > 1.55) continue;
      // перехват: соперник на линии паса
      let blocked = 0;
      for (const o of this.players) {
        if (o.team === p.team) continue;
        const t = clamp(((o.x - p.x) * dx + (o.z - p.z) * dz) / (d * d), 0, 1);
        const cd = hyp(p.x + dx * t - o.x, p.z + dz * t - o.z);
        if (cd < 1.2) blocked += 1;
      }
      const s = ang * 7 + d * 0.12 + blocked * 4 - (q.human ? 1.5 : 0);
      if (s < bestScore) { bestScore = s; best = q; }
    }
    let tx, tz, speed;
    if (best) {
      const d = hyp(best.x - p.x, best.z - p.z);
      const lead = clamp(d / 18, 0.15, 0.6);
      tx = best.x + best.vx * lead; tz = best.z + best.vz * lead;
      speed = clamp(9 + d * 0.75, 11, 24);
      pk.passTarget = best.idx; pk.passT = 1.2;
    } else {
      tx = p.x + fx * 10; tz = p.z + fz * 10; speed = 15;
      pk.passTarget = -1;
    }
    const dx = tx - pk.x, dz = tz - pk.z;
    const l = hyp(dx, dz) || 1;
    pk.owner = -1;
    pk.vx = (dx / l) * speed; pk.vz = (dz / l) * speed; pk.vy = 0;
    pk.y = 0.02;
    pk.saveCheck = {};
    this.touch(p);
    p.noPick = 0.3;
    p.swing = 0.22;
    this.ev('pass');
  }

  touch(p) {
    const lt = this.puck.lastTouch;
    if (lt[0] !== p.idx) { lt[1] = lt[0]; lt[0] = p.idx; }
  }

  // ---------- вратарь ----------
  updateGoalie(g, dt) {
    g.swing = Math.max(0, g.swing - dt);
    g.save = Math.max(0, g.save - dt);
    const s = -TEAM_DIR[g.team]; // сторона своих ворот
    const out = -s;               // направление к центру
    const gx = s * RINK.goalX, gz = 0;
    const pk = this.puck;
    let tx, tz;
    const px = pk.x, pz = pk.z;
    if (s * px > RINK.goalX - 0.2) {
      // шайба за воротами — прижаться к штанге
      tx = gx + out * 0.55; tz = Math.sign(pz || 1) * (RINK.goalW / 2 - 0.25);
    } else {
      const dx = px - gx, dz = pz - gz;
      const d = hyp(dx, dz) || 1;
      const r = clamp(0.55 + d * 0.05, 0.6, 1.5);
      tx = gx + (dx / d) * r; tz = gz + (dz / d) * r;
      if (out * (tx - gx) < 0.45) tx = gx + out * 0.45;
      tz = clamp(tz, -RINK.goalW / 2 - 0.3, RINK.goalW / 2 + 0.3);
    }
    const mx = tx - g.x, mz = tz - g.z;
    const md = hyp(mx, mz);
    const maxV = 4.6;
    const want = Math.min(maxV, md * 7);
    const vx = md > 1e-3 ? (mx / md) * want : 0, vz = md > 1e-3 ? (mz / md) * want : 0;
    g.vx += (vx - g.vx) * Math.min(1, dt * 12);
    g.vz += (vz - g.vz) * Math.min(1, dt * 12);
    g.x += g.vx * dt; g.z += g.vz * dt;
    const desired = Math.atan2(px - g.x, pz - g.z);
    g.h = wrapAngle(g.h + clamp(wrapAngle(desired - g.h), -6 * dt, 6 * dt));
    if (out * (g.x - gx) < 0.3) g.x = gx + out * 0.3;

    if (pk.owner === g.idx) {
      pk.holdT -= dt;
      if (pk.holdT <= 0 && this.phase === 'play') this.goaliePass(g);
    }
  }

  goaliePass(g) {
    const pk = this.puck;
    const mates = this.players.filter((q) => q.team === g.team && !q.goalie);
    let best = null, bd = Infinity;
    for (const q of mates) {
      let danger = 0;
      for (const o of this.players) if (o.team !== g.team && hyp(o.x - q.x, o.z - q.z) < 3) danger += 6;
      const d = hyp(q.x - g.x, q.z - g.z) + danger;
      if (d < bd) { bd = d; best = q; }
    }
    const out = TEAM_DIR[g.team];
    let tx = g.x + out * 10, tz = g.z + 6;
    if (best) { tx = best.x + best.vx * 0.3; tz = best.z + best.vz * 0.3; pk.passTarget = best.idx; pk.passT = 1.5; }
    const dx = tx - g.x, dz = tz - g.z, l = hyp(dx, dz) || 1;
    pk.owner = -1;
    pk.x = g.x + out * 0.7; pk.z = g.z; pk.y = 0.02;
    const sp = clamp(9 + l * 0.6, 11, 20);
    pk.vx = (dx / l) * sp; pk.vz = (dz / l) * sp; pk.vy = 0;
    pk.saveCheck = {};
    g.noPick = 0.6;
    g.swing = 0.25;
    this.touch(g);
    this.ev('pass');
  }

  // ---------- шайба ----------
  updatePuck(dt) {
    const pk = this.puck;
    pk.passT = Math.max(0, pk.passT - dt);
    if (pk.passT <= 0) pk.passTarget = -1;

    if (pk.inNet) {
      pk.vx *= 1 - 4 * dt; pk.vz *= 1 - 4 * dt;
      pk.vy -= G * dt;
      pk.x += pk.vx * dt; pk.z += pk.vz * dt; pk.y += pk.vy * dt;
      if (pk.y < 0) { pk.y = 0; pk.vy = 0; }
      const s = Math.sign(pk.x);
      const ax = clamp(Math.abs(pk.x), RINK.goalX + 0.1, RINK.goalX + RINK.goalD - 0.15);
      pk.x = s * ax; pk.z = clamp(pk.z, -RINK.goalW / 2 + 0.15, RINK.goalW / 2 - 0.15);
      return;
    }

    if (pk.owner >= 0) {
      const o = this.players[pk.owner];
      if (o.goalie) {
        const out = TEAM_DIR[o.team];
        pk.x = o.x + out * 0.35; pk.z = o.z + 0.25; pk.y = 0.9; pk.vx = pk.vz = pk.vy = 0;
        return;
      }
      const [bx, bz] = this.blade(o);
      const wig = Math.sin(this.simTime * 9 + o.idx) * 0.12;
      const fx = Math.sin(o.h), fz = Math.cos(o.h);
      let nx = bx + -fz * wig, nz = bz + fx * wig;
      const c = rinkClamp(nx, nz, PUCK_R);
      if (c) { nx = c.x; nz = c.z; }
      pk.x = nx; pk.z = nz; pk.y = 0; pk.vx = o.vx; pk.vz = o.vz; pk.vy = 0;
      // отбор шайбы клюшкой
      if (this.phase === 'play') {
        for (const q of this.players) {
          if (q.team === o.team || q.goalie || q.stun > 0 || q.noPick > 0) continue;
          const [qx, qz] = this.blade(q);
          if (hyp(qx - pk.x, qz - pk.z) < 0.75 || hyp(q.x - pk.x, q.z - pk.z) < 0.55) {
            const rate = q.human ? 2.6 : this.diff.steal;
            const resist = o.human ? 1 : 1.15;
            if (Math.random() < (rate / resist) * this.dtStep(dt)) {
              pk.owner = q.idx; o.noPick = 0.7; this.touch(q); this.ev('steal');
              break;
            }
          }
        }
      }
      return;
    }

    // свободная шайба: 3 подшага против «туннелирования»
    const N = 3, h = dt / N;
    for (let k = 0; k < N; k++) {
      const ox = pk.x, oz = pk.z;
      pk.vy -= G * h;
      pk.x += pk.vx * h; pk.z += pk.vz * h; pk.y += pk.vy * h;
      if (pk.y <= 0) {
        pk.y = 0;
        pk.vy = pk.vy < -2 ? -pk.vy * 0.3 : 0;
      }
      if (pk.y === 0) {
        const f = 1 - 0.12 * h;
        pk.vx *= f; pk.vz *= f;
      } else {
        pk.vx *= 1 - 0.03 * h; pk.vz *= 1 - 0.03 * h;
      }
      // борта
      const c = rinkClamp(pk.x, pk.z, PUCK_R);
      if (c) {
        pk.x = c.x; pk.z = c.z;
        const vn = pk.vx * c.nx + pk.vz * c.nz;
        if (vn < 0) {
          pk.vx -= vn * c.nx * 1.65; pk.vz -= vn * c.nz * 1.65;
          pk.vx *= 0.9; pk.vz *= 0.9;
          if (vn < -3) this.ev('board', { v: -vn, puck: true });
        }
      }
      if (this.phase === 'play' && this.checkGoal(ox, oz)) return;
      this.puckGoalFrame();
      if (this.phase !== 'play') continue;
      if (this.goalieSaves()) return;
      if (this.pickup()) return;
      this.puckBodies();
    }
  }

  dtStep(dt) { return dt; }

  checkGoal(ox, oz) {
    const pk = this.puck;
    for (const s of [1, -1]) {
      const a0 = ox * s, a1 = pk.x * s;
      if (a0 < RINK.goalX && a1 >= RINK.goalX) {
        const t = (RINK.goalX - a0) / (a1 - a0 || 1);
        const zc = oz + (pk.z - oz) * t;
        if (Math.abs(zc) < RINK.goalW / 2 - 0.1 && pk.y < RINK.goalH - 0.08) {
          const team = s > 0 ? 0 : 1; // забила команда, атакующая в сторону s
          this.score[team]++;
          pk.inNet = true;
          pk.vx *= 0.35; pk.vz *= 0.35;
          const lt = pk.lastTouch;
          let scorer = this.players[lt[0]];
          let assist = this.players[lt[1]];
          let own = false;
          if (!scorer || scorer.team !== team) { own = true; }
          else { scorer.goals++; if (assist && assist.team === team && assist !== scorer && !assist.goalie) assist.assists++; else assist = null; }
          this.lastGoal = { team, scorer: own ? null : scorer.idx, assist: own || !assist ? null : assist.idx, own };
          this.phase = 'goal';
          this.phaseT = 3.6;
          this.ev('goal', { team, scorer: this.lastGoal.scorer, own });
          return true;
        }
      }
    }
    return false;
  }

  puckGoalFrame() {
    const pk = this.puck;
    if (pk.y > RINK.goalH + 0.1) return;
    for (const s of [1, -1]) {
      const ax = pk.x * s;
      const x0 = RINK.goalX - 0.06, x1 = RINK.goalX + RINK.goalD, hz = RINK.goalW / 2 + 0.06;
      // открытый створ: шайбу пропускаем к линии ворот (гол считает checkGoal)
      if (ax < RINK.goalX && Math.abs(pk.z) < RINK.goalW / 2 - 0.1 && pk.y < RINK.goalH - 0.08) continue;
      if (ax > x0 - PUCK_R && ax < x1 + PUCK_R && Math.abs(pk.z) < hz + PUCK_R) {
        // ищем грань с наименьшим проникновением
        const pens = [
          { p: ax - (x0 - PUCK_R), nx: -1, nz: 0 },
          { p: x1 + PUCK_R - ax, nx: 1, nz: 0 },
          { p: hz + PUCK_R - pk.z, nx: 0, nz: 1 },
          { p: pk.z + hz + PUCK_R, nx: 0, nz: -1 },
        ];
        pens.sort((a, b) => a.p - b.p);
        const f = pens[0];
        pk.x += f.nx * f.p * s; pk.z += f.nz * f.p;
        const vn = pk.vx * f.nx * s + pk.vz * f.nz;
        if (vn < 0) {
          pk.vx -= vn * f.nx * s * 1.6; pk.vz -= vn * f.nz * 1.6;
          if (vn < -4) this.ev(f.nx === -1 ? 'post' : 'board', { v: -vn, puck: true });
        }
      }
    }
  }

  goalieSaves() {
    const pk = this.puck;
    const spd = hyp(pk.vx, pk.vz);
    for (const g of this.players) {
      if (!g.goalie) continue;
      const s = -TEAM_DIR[g.team];
      const dx = pk.x - g.x, dz = pk.z - g.z;
      const d = hyp(dx, dz);
      // медленная шайба рядом — накрыть
      if (d < 1.0 && spd < 7 && pk.y < 0.6 && g.noPick <= 0 && !(g.idx in pk.saveCheck && !pk.saveCheck[g.idx])) {
        this.goalieHold(g, 1.1);
        return true;
      }
      const towards = pk.vx * s > 2;
      if (!towards || d > 1.15 || pk.y > 1.55) continue;
      if (g.idx in pk.saveCheck) continue;
      const lateral = Math.abs(dz);
      let prob = this.diff.save + 0.1 - (spd - 14) / 70 - lateral * 0.22 - Math.max(0, pk.y - 0.9) * 0.3;
      const shooter = this.players[pk.lastTouch[0]];
      if (shooter && shooter.team === g.team) prob = 1;
      prob = clamp(prob, 0.18, 0.95);
      const ok = Math.random() < prob;
      pk.saveCheck[g.idx] = ok;
      if (!ok) continue;
      g.save = 0.6;
      this.ev('save');
      if (spd < 24 && Math.random() < 0.55) {
        this.goalieHold(g, 1.3);
      } else {
        const out = -s;
        const a = rnd(-1.1, 1.1);
        const sp = spd * rnd(0.3, 0.5);
        pk.vx = out * Math.cos(a) * sp; pk.vz = Math.sin(a) * sp + (dz > 0 ? 2 : -2); pk.vy = rnd(0.5, 3);
        pk.x = g.x + out * 0.6; pk.z = g.z + dz;
        this.touch(g);
      }
      return true;
    }
    return false;
  }

  goalieHold(g, t) {
    const pk = this.puck;
    pk.owner = g.idx; pk.holdT = t;
    pk.vx = pk.vz = pk.vy = 0;
    this.touch(g);
    g.save = Math.max(g.save, 0.4);
  }

  pickup() {
    const pk = this.puck;
    if (pk.y > 0.65) return false;
    const spd = hyp(pk.vx, pk.vz);
    let best = null, bd = Infinity;
    for (const p of this.players) {
      if (p.goalie || p.stun > 0 || p.noPick > 0) continue;
      const [bx, bz] = this.blade(p);
      const d = Math.min(hyp(bx - pk.x, bz - pk.z), hyp(p.x - pk.x, p.z - pk.z) + 0.25);
      const r = pk.passTarget === p.idx ? 1.25 : 0.8;
      if (d < r && d < bd) { bd = d; best = p; }
    }
    if (!best) return false;
    const relSpd = hyp(pk.vx - best.vx, pk.vz - best.vz);
    if (relSpd > 27 && pk.passTarget !== best.idx) {
      // слишком сильный бросок — рикошет от клюшки
      pk.vx = pk.vx * 0.35 + rnd(-3, 3); pk.vz = pk.vz * 0.35 + rnd(-3, 3); pk.vy = rnd(0, 2);
      best.noPick = 0.25;
      this.touch(best);
      this.ev('deflect');
      return true;
    }
    pk.owner = best.idx;
    pk.passTarget = -1;
    this.touch(best);
    if (spd > 6) this.ev('receive');
    return true;
  }

  puckBodies() {
    const pk = this.puck;
    if (pk.y > 1.5) return;
    for (const p of this.players) {
      const dx = pk.x - p.x, dz = pk.z - p.z;
      const d = hyp(dx, dz);
      const r = p.goalie ? 0.5 : 0.4;
      if (d < r && d > 1e-4) {
        const nx = dx / d, nz = dz / d;
        const vn = (pk.vx - p.vx) * nx + (pk.vz - p.vz) * nz;
        pk.x = p.x + nx * r; pk.z = p.z + nz * r;
        if (vn < 0) { pk.vx -= vn * nx * 1.4; pk.vz -= vn * nz * 1.4; if (vn < -6) this.ev('body'); }
      }
    }
  }

  // ---------- боты ----------
  updateRoles() {
    const pk = this.puck;
    this.carrier = pk.owner >= 0 ? this.players[pk.owner] : null;
    this.chaser = [-1, -1];
    for (const t of [0, 1]) {
      let best = -1, bd = Infinity;
      const tx = pk.x + pk.vx * 0.3, tz = pk.z + pk.vz * 0.3;
      for (const p of this.players) {
        if (p.team !== t || p.goalie || p.human || p.stun > 0) continue;
        if (this.carrier && this.carrier.team === t) continue;
        const d = hyp(p.x - tx, p.z - tz);
        if (d < bd) { bd = d; best = p.idx; }
      }
      this.chaser[t] = best;
      // нумерация «линий» для ботов
      let lane = 0;
      for (const p of this.players) if (p.team === t && !p.goalie && !p.human && p.idx !== best) p.lane = lane++;
    }
  }

  idleBotInput(p) {
    if (this.phase === 'goal' && this.lastGoal && this.lastGoal.team === p.team) {
      return { ...STEP_EMPTY, throttle: 0.5, turn: 0.6 };
    }
    return STEP_EMPTY;
  }

  steer(p, tx, tz, arrive = 1.5, speedMul = 1) {
    const dx = tx - p.x, dz = tz - p.z;
    const d = hyp(dx, dz);
    const desired = Math.atan2(dx, dz);
    const diff = wrapAngle(desired - p.h);
    const inp = { ...STEP_EMPTY };
    inp.turn = clamp(diff * 3.2, -1, 1);
    const spd = hyp(p.vx, p.vz);
    if (d < arrive) {
      inp.throttle = spd > 1.5 ? -1 : 0;
    } else {
      inp.throttle = Math.abs(diff) < 1.3 ? speedMul : 0.15;
      if (Math.abs(diff) > 2.2 && spd > 5) inp.throttle = -1;
    }
    return { inp, d, diff };
  }

  botInput(p, dt) {
    const pk = this.puck;
    const dir = TEAM_DIR[p.team];
    const oppGoalX = dir * RINK.goalX, ownGoalX = -dir * RINK.goalX;
    const b = p.bot;
    const D = this.diff;

    // продолжение замаха
    if (b.shootT >= 0) {
      b.shootT += dt;
      const inp = this.steer(p, oppGoalX, 0, 0).inp;
      if (pk.owner !== p.idx && b.shootT > 0.1) { b.shootT = -1; inp.shoot = false; return inp; }
      inp.shoot = b.shootT < b.chargeFor;
      if (!inp.shoot) b.shootT = -1;
      inp.throttle = 0.3;
      return inp;
    }

    // у бота шайба
    if (pk.owner === p.idx) {
      const gdx = oppGoalX - p.x, gdz = -p.z;
      const gd = hyp(gdx, gdz);
      const gang = Math.abs(wrapAngle(Math.atan2(gdx, gdz) - p.h));
      let pressure = 0, nearOpp = null, nd = Infinity;
      for (const o of this.players) {
        if (o.team === p.team || o.goalie) continue;
        const d = hyp(o.x - p.x, o.z - p.z);
        if (d < nd) { nd = d; nearOpp = o; }
        if (d < 3.2) pressure++;
      }
      const inZone = dir * p.x > RINK.goalX - 18 && dir * p.x < RINK.goalX - 1.5;
      if (inZone && gd < 17 && gang < 0.45 && Math.random() < (2.2 + (17 - gd) * 0.25) * dt * D.react) {
        b.shootT = 0; b.chargeFor = rnd(0.15, 0.85);
        const inp = this.steer(p, oppGoalX, 0, 0).inp; inp.shoot = true; return inp;
      }
      if (pressure > 0 && Math.random() < 1.4 * dt * D.react) {
        const inp = this.steer(p, oppGoalX, 0).inp; inp.pass = true; return inp;
      }
      // ведение: к «пятаку» с обводкой ближайшего соперника
      let tx = oppGoalX - dir * 7, tz = clamp(p.z * 0.5, -4, 4);
      if (dir * p.x > RINK.goalX - 10) { tx = oppGoalX - dir * 3; tz = 0; }
      if (nearOpp && nd < 5) {
        const ahead = (nearOpp.x - p.x) * dir > -0.5;
        if (ahead) tz += (p.z > nearOpp.z ? 1 : -1) * 4.5;
      }
      if (Math.abs(p.z) > 11) tz = 0;
      const r = this.steer(p, tx, tz, 0);
      r.inp.sprint = nd > 4 && p.stamina > 0.4 && dir * p.x < RINK.goalX - 15;
      return r.inp;
    }

    const carrier = this.carrier;
    // у своих шайба — открываемся
    if (carrier && carrier.team === p.team) {
      const lanes = [-7, 7, 0, -11, 11, -3, 3];
      const lz = lanes[p.lane % lanes.length];
      let tx = clamp(carrier.x + dir * rnd(5, 7), -RINK.goalX + 5, RINK.goalX - 5);
      if (carrier.goalie) tx = carrier.x + dir * 9;
      const r = this.steer(p, tx, lz, 2, 0.95);
      r.inp.sprint = r.d > 8 && p.stamina > 0.6;
      return r.inp;
    }

    // ближайший бот гонится за шайбой / владельцем
    if (this.chaser[p.team] === p.idx) {
      if (carrier && carrier.goalie) {
        const r = this.steer(p, carrier.x - TEAM_DIR[carrier.team] * -6, carrier.z, 2, 0.7);
        return r.inp;
      }
      const lead = carrier ? 0.35 : clamp(hyp(pk.x - p.x, pk.z - p.z) / 14, 0.1, 0.6);
      const tx = pk.x + pk.vx * lead, tz = pk.z + pk.vz * lead;
      const r = this.steer(p, tx, tz, 0);
      if (carrier && r.d < 2.6 && Math.abs(r.diff) < 0.4 && p.checkCd <= 0 && Math.random() < D.check * dt * 2) r.inp.check = true;
      r.inp.sprint = r.d > 5 && p.stamina > 0.3;
      if (!carrier && pk.owner < 0 && r.d < 1.6 && Math.random() < 0.05 && dir * (oppGoalX - pk.x) > 0 && Math.abs(wrapAngle(Math.atan2(oppGoalX - p.x, -p.z) - p.h)) < 0.4 && hyp(oppGoalX - p.x, p.z) < 14) {
        // бросок «в одно касание»
        b.shootT = 0; b.chargeFor = 0.05; r.inp.shoot = true;
      }
      return r.inp;
    }

    // остальные — оборона / позиция
    const lanes = [-6, 6, 0, -10, 10];
    const lz = lanes[p.lane % lanes.length];
    let tx, tz;
    if (carrier) {
      tx = ownGoalX + (carrier.x - ownGoalX) * 0.55;
      tz = carrier.z * 0.45 + lz * 0.6;
    } else {
      tx = clamp(pk.x - dir * 6, -RINK.goalX + 4, RINK.goalX - 4);
      tz = lz;
    }
    return this.steer(p, tx, tz, 1.8, 0.9).inp;
  }

  // ---------- состояние для рендера / сети ----------
  viewState() {
    const pk = this.puck;
    return {
      players: this.players.map((p) => ({
        x: p.x, z: p.z, h: p.h, spd: hyp(p.vx, p.vz), charge: p.charge, stun: p.stun > 0 ? 1 : 0,
        swing: p.swing, save: p.save, stamina: p.stamina, back: (p.vx * Math.sin(p.h) + p.vz * Math.cos(p.h)) < -0.5 ? 1 : 0,
      })),
      puck: { x: pk.x, y: pk.y, z: pk.z, owner: pk.owner },
      score: [...this.score], time: this.time, phase: this.phase, phaseT: this.phaseT, overtime: this.overtime,
      lastGoal: this.lastGoal, st: this.simTime,
    };
  }

  stats() {
    return this.players.filter((p) => !p.goalie).map((p) => ({ name: p.name, team: p.team, goals: p.goals, assists: p.assists, human: !!p.human }));
  }
}

// Компактная упаковка состояния для сети
const r2 = (v) => Math.round(v * 100) / 100;
export function packState(vs) {
  return {
    p: vs.players.map((q) => [r2(q.x), r2(q.z), r2(q.h), r2(q.spd), r2(q.charge), q.stun, r2(q.swing), r2(q.save), r2(q.stamina), q.back]),
    k: [r2(vs.puck.x), r2(vs.puck.y), r2(vs.puck.z), vs.puck.owner],
    s: vs.score, tm: r2(vs.time), ph: vs.phase, pt: r2(vs.phaseT), ot: vs.overtime ? 1 : 0, lg: vs.lastGoal, st: vs.st,
  };
}
export function unpackState(m) {
  return {
    players: m.p.map((a) => ({ x: a[0], z: a[1], h: a[2], spd: a[3], charge: a[4], stun: a[5], swing: a[6], save: a[7], stamina: a[8], back: a[9] })),
    puck: { x: m.k[0], y: m.k[1], z: m.k[2], owner: m.k[3] },
    score: m.s, time: m.tm, phase: m.ph, phaseT: m.pt, overtime: !!m.ot, lastGoal: m.lg, st: m.st,
  };
}
