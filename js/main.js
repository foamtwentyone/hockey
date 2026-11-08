import { Sim, RINK, TEAM_NAMES, packState, unpackState, wrapAngle } from './sim.js';
import { Renderer, SLOT_COLORS } from './render.js';
import { readInput, attachMouse, packInput, unpackInput } from './input.js';
import { initAudio, playEvent, setMuted } from './audio.js';
import { Host, Client } from './net.js';
import * as THREE from 'three';

const $ = (id) => document.getElementById(id);
const STEP = 1 / 60;

const renderer = new Renderer($('game'));
attachMouse($('game'));

// ---------- состояние приложения ----------
let demo = null;
const app = {
  mode: null,          // 'local' | 'host' | 'client'
  sim: null,
  host: null,
  client: null,
  sources: [],         // локальные игроки: {key, scheme, pad, useMouse}
  myKeys: [],          // за кем следят камеры
  roster: null,
  lobby: [],           // [{key, name, team}]
  myKey: null,         // для гостя
  remoteInputs: new Map(),
  netEvents: [],
  snaps: [],           // буфер снимков у гостя
  offset: null,
  view: null,          // последнее состояние для HUD
  paused: false,
  acc: 0,
  steps: 0,
  cfg: null,
  muted: false,
  inMatch: false,
};

// ---------- меню ----------
const savedName = localStorage.getItem('hockeyName') || '';
$('name').value = savedName;
const params = new URLSearchParams(location.search);
if (params.get('join')) $('joinCode').value = params.get('join').toUpperCase();

function playerName() {
  const n = $('name').value.trim() || 'Игрок';
  localStorage.setItem('hockeyName', n);
  return n.slice(0, 14);
}
function readCfg() {
  return { skaters: +$('skaters').value, duration: +$('duration').value, difficulty: +$('difficulty').value };
}

const CONTROLS = `
<table>
<tr><td><b>Один игрок / онлайн</b></td><td></td></tr>
<tr><td><kbd>W</kbd><kbd>S</kbd> / <kbd>↑</kbd><kbd>↓</kbd></td><td>разгон / торможение, задний ход</td></tr>
<tr><td><kbd>A</kbd><kbd>D</kbd> / <kbd>←</kbd><kbd>→</kbd></td><td>поворот</td></tr>
<tr><td><kbd>Пробел</kbd> или ЛКМ</td><td>бросок (держи — сильнее)</td></tr>
<tr><td><kbd>Q</kbd> или ПКМ</td><td>пас партнёру</td></tr>
<tr><td><kbd>E</kbd></td><td>силовой приём</td></tr>
<tr><td><kbd>Shift</kbd></td><td>ускорение</td></tr>
<tr><td><b>Игрок 2 (сплит-скрин)</b></td><td></td></tr>
<tr><td><kbd>←</kbd><kbd>↑</kbd><kbd>↓</kbd><kbd>→</kbd></td><td>движение</td></tr>
<tr><td><kbd>Enter</kbd> / <kbd>Num0</kbd></td><td>бросок</td></tr>
<tr><td><kbd>Правый Shift</kbd> / <kbd>Num1</kbd></td><td>пас</td></tr>
<tr><td><kbd>Правый Ctrl</kbd> / <kbd>Num2</kbd></td><td>силовой</td></tr>
<tr><td><kbd>/</kbd> / <kbd>Num3</kbd></td><td>ускорение</td></tr>
<tr><td><b>Геймпад</b></td><td>стик/курок — движение, A — бросок, B/Y — пас, X — силовой, LB/RB — ускорение</td></tr>
</table>`;
$('controlsText').innerHTML = CONTROLS;
$('help').innerHTML = CONTROLS;

function show(id) {
  for (const s of ['menu', 'lobby', 'pause', 'end']) $(s).classList.toggle('hidden', s !== id);
}
function menuMsg(t) { $('menuMsg').textContent = t || ''; }

$('btnSolo').onclick = () => {
  initAudio();
  const name = playerName();
  app.cfg = readCfg();
  startLocal([{ key: 'local0', name, team: 0 }], [{ key: 'local0', scheme: 'solo', pad: 0, useMouse: true }]);
};
$('btnSplit').onclick = () => {
  initAudio();
  const name = playerName();
  app.cfg = readCfg();
  const coop = $('splitMode').value === 'coop';
  startLocal(
    [{ key: 'local0', name, team: 0 }, { key: 'local1', name: 'Игрок 2', team: coop ? 0 : 1 }],
    [{ key: 'local0', scheme: 'p1', pad: 0 }, { key: 'local1', scheme: 'p2', pad: 1 }],
  );
};
$('btnHost').onclick = () => { initAudio(); startHosting(); };
$('btnJoin').onclick = () => { initAudio(); joinGame($('joinCode').value); };
$('joinCode').addEventListener('keydown', (e) => { if (e.key === 'Enter') $('btnJoin').click(); });

// ---------- локальная игра ----------
function startLocal(humans, sources) {
  cleanupNet();
  app.mode = 'local';
  app.sources = sources;
  app.lobby = humans;
  app.myKeys = sources.map((s) => s.key);
  newMatch();
}

function newMatch() {
  app.sim = new Sim({ ...app.cfg, humans: app.lobby.map((h) => ({ ...h })) });
  app.acc = 0; app.steps = 0; simLast = performance.now();
  setupMatch(app.sim.roster());
  if (app.mode === 'host') app.host.broadcast({ t: 'start', roster: app.roster, cfg: app.cfg });
}

function setupMatch(roster) {
  demo = null;
  app.roster = roster;
  const slots = new Map();
  roster.forEach((r) => { if (r.human && !slots.has(r.human)) slots.set(r.human, slots.size); });
  app.slots = slots;
  renderer.setRoster(roster, slots);
  const follow = app.myKeys.map((k) => roster.findIndex((r) => r.human === k)).filter((i) => i >= 0);
  renderer.setViews(follow.map((f) => ({ follow: f })));
  buildViewOverlays(follow);
  app.inMatch = true;
  app.paused = false;
  app.endShown = false;
  show(null);
  $('hud').classList.remove('hidden');
  $('minimap').classList.toggle('center', follow.length > 1);
}

function updateRosterViews(roster) {
  app.roster = roster;
  const slots = new Map();
  roster.forEach((r) => { if (r.human && !slots.has(r.human)) slots.set(r.human, slots.size); });
  app.slots = slots;
  renderer.setRoster(roster, slots);
  const follow = app.myKeys.map((k) => roster.findIndex((r) => r.human === k)).filter((i) => i >= 0);
  renderer.views.forEach((v, i) => { if (follow[i] !== undefined) v.follow = follow[i]; });
  buildViewOverlays(follow);
}

function buildViewOverlays(follow) {
  const box = $('viewOverlays');
  box.innerHTML = '';
  const n = follow.length;
  app.overlays = follow.map((f, i) => {
    const d = document.createElement('div');
    d.className = 'viewOverlay';
    d.style.left = n > 1 ? `${i * 50}%` : '0';
    d.style.width = n > 1 ? '50%' : '100%';
    const r = app.roster[f];
    const color = SLOT_COLORS[(app.slots.get(r.human) ?? 0) % SLOT_COLORS.length];
    d.innerHTML = `${n > 1 ? `<div class="who" style="color:${color}">${escapeHtml(r.name)}</div>` : ''}
      <div class="bars"><div class="bar charge"><div></div></div><div class="bar stamina"><div></div></div></div>`;
    box.appendChild(d);
    return { follow: f, charge: d.querySelector('.charge > div'), chargeBar: d.querySelector('.charge'), stamina: d.querySelector('.stamina > div') };
  });
  if (n > 1) { const l = document.createElement('div'); l.className = 'splitline'; box.appendChild(l); }
}

function escapeHtml(s) { return String(s).replace(/[&<>"']/g, (c) => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' }[c])); }

// ---------- онлайн: хост ----------
function startHosting() {
  cleanupNet();
  const name = playerName();
  app.cfg = readCfg();
  app.mode = 'host';
  app.lobby = [{ key: 'local0', name, team: 0 }];
  app.sources = [{ key: 'local0', scheme: 'solo', pad: 0, useMouse: true }];
  app.myKeys = ['local0'];
  app.myKey = 'local0';
  show('lobby');
  $('lobbyTitle').textContent = 'Онлайн-матч';
  $('codeBox').classList.remove('hidden');
  $('lobbyCode').textContent = '·····';
  $('lobbyStatus').textContent = 'Создаём матч…';
  $('btnStart').classList.remove('hidden');
  renderLobby();
  try {
    app.host = new Host({
      onReady: (code) => { app.code = code; $('lobbyCode').textContent = code; $('lobbyStatus').textContent = 'Ждём друзей… (можно начать и одному)'; },
      onError: (err) => { $('lobbyStatus').textContent = 'Ошибка сети: ' + (err.type || err.message || err); },
      onJoin: (key, pname) => {
        const t0 = app.lobby.filter((p) => p.team === 0).length, t1 = app.lobby.filter((p) => p.team === 1).length;
        const team = t0 <= t1 ? 0 : 1;
        app.lobby.push({ key, name: pname, team });
        app.host.send(key, { t: 'welcome', key, cfg: app.cfg });
        if (app.inMatch && app.sim) {
          const p = app.sim.replaceBotWithHuman(key, pname, team);
          if (p) { const lp = app.lobby.find((l) => l.key === key); lp.team = p.team; }
          updateRosterViews(app.sim.roster());
          app.host.send(key, { t: 'start', roster: app.roster, cfg: app.cfg });
          app.host.broadcast({ t: 'roster', roster: app.roster });
        }
        broadcastLobby();
      },
      onLeave: (key) => {
        app.lobby = app.lobby.filter((p) => p.key !== key);
        app.remoteInputs.delete(key);
        if (app.inMatch && app.sim) {
          app.sim.humanLeft(key);
          updateRosterViews(app.sim.roster());
          app.host.broadcast({ t: 'roster', roster: app.roster });
        }
        broadcastLobby();
      },
      onMessage: (key, msg) => {
        if (!msg) return;
        if (msg.t === 'in') app.remoteInputs.set(key, unpackInput(msg.i));
        else if (msg.t === 'team' && !app.inMatch) { const p = app.lobby.find((l) => l.key === key); if (p) p.team = 1 - p.team; broadcastLobby(); }
        else if (msg.t === 'ping') app.host.send(key, { t: 'pong', c: msg.c });
      },
    });
  } catch (e) {
    $('lobbyStatus').textContent = e.message;
  }
}

function broadcastLobby() {
  renderLobby();
  if (app.host) app.host.broadcast({ t: 'lobby', players: app.lobby, cfg: app.cfg });
}

function renderLobby() {
  const cfg = app.cfg || readCfg();
  for (const t of [0, 1]) {
    const ul = $('team' + t);
    ul.innerHTML = '';
    const ps = app.lobby.filter((p) => p.team === t);
    for (const p of ps) {
      const li = document.createElement('li');
      li.textContent = p.name + (p.key === 'local0' && app.mode === 'client' ? '' : '') + (p.key === 'local0' ? ' 👑' : '');
      if (p.key === app.myKey) li.classList.add('me');
      li.onclick = () => {
        if (app.mode === 'host') { p.team = 1 - p.team; broadcastLobby(); }
        else if (p.key === app.myKey && app.client) app.client.send({ t: 'team' });
      };
      ul.appendChild(li);
    }
    for (let i = ps.length; i < cfg.skaters; i++) {
      const li = document.createElement('li');
      li.className = 'bot';
      li.textContent = 'бот';
      ul.appendChild(li);
    }
  }
}

$('btnSwitch').onclick = () => {
  if (app.mode === 'host') { const me = app.lobby.find((p) => p.key === 'local0'); me.team = 1 - me.team; broadcastLobby(); }
  else if (app.client) app.client.send({ t: 'team' });
};
$('btnStart').onclick = () => { if (app.mode === 'host') newMatch(); };
$('btnCopy').onclick = async () => {
  const url = `${location.origin}${location.pathname}?join=${app.code}`;
  try { await navigator.clipboard.writeText(url); $('btnCopy').textContent = 'Скопировано!'; } catch { prompt('Ссылка для друзей:', url); }
  setTimeout(() => ($('btnCopy').textContent = 'Скопировать ссылку'), 1500);
};
$('btnLeave').onclick = () => toMenu();

// ---------- онлайн: гость ----------
function joinGame(code) {
  code = (code || '').toUpperCase().trim();
  if (code.length < 4) { menuMsg('Введи код матча от друга'); return; }
  cleanupNet();
  const name = playerName();
  app.mode = 'client';
  app.sources = [{ key: 'me', scheme: 'solo', pad: 0, useMouse: true }];
  app.lobby = [];
  menuMsg('Подключаемся…');
  try {
    app.client = new Client(code, name, {
      onOpen: () => {
        menuMsg('');
        show('lobby');
        $('lobbyTitle').textContent = 'Матч ' + code;
        $('codeBox').classList.add('hidden');
        $('btnStart').classList.add('hidden');
        $('lobbyStatus').textContent = 'Ждём, пока хост начнёт матч…';
      },
      onMessage: onClientMessage,
      onClose: () => { if (app.mode === 'client') { toMenu(); menuMsg('Соединение с хостом потеряно'); } },
      onError: (e) => { if (app.mode === 'client') { toMenu(); menuMsg(e.message || String(e.type || e)); } },
    });
  } catch (e) { menuMsg(e.message); }
}

function onClientMessage(msg) {
  if (!msg) return;
  switch (msg.t) {
    case 'welcome': app.myKey = msg.key; app.myKeys = [msg.key]; app.cfg = msg.cfg; break;
    case 'lobby':
      app.lobby = msg.players; app.cfg = msg.cfg;
      if (!app.inMatch) renderLobby();
      break;
    case 'start':
      app.cfg = msg.cfg;
      app.snaps = []; app.offset = null;
      setupMatch(msg.roster);
      break;
    case 'roster': updateRosterViews(msg.roster); break;
    case 's': {
      const st = unpackState(msg);
      const now = performance.now() / 1000;
      const off = now - st.st;
      if (app.offset === null || off < app.offset) app.offset = off; else app.offset += (off - app.offset) * 0.02;
      app.snaps.push(st);
      if (app.snaps.length > 30) app.snaps.shift();
      if (msg.e) for (const e of msg.e) handleEvent(e);
      if (msg.stats) app.stats = msg.stats;
      break;
    }
    case 'pong': app.ping = Math.round(performance.now() - msg.c); break;
    default: break;
  }
}

function clientState() {
  const sn = app.snaps;
  if (!sn.length) return null;
  const now = performance.now() / 1000;
  const t = now - app.offset - 0.1;
  let a = sn[0], b = sn[sn.length - 1];
  if (t >= b.st) return b;
  if (t <= a.st) return a;
  for (let i = 0; i < sn.length - 1; i++) if (sn[i].st <= t && sn[i + 1].st >= t) { a = sn[i]; b = sn[i + 1]; break; }
  const k = (t - a.st) / Math.max(1e-4, b.st - a.st);
  const L = (x, y) => x + (y - x) * k;
  const tele = (pa, pb) => Math.hypot(pa.x - pb.x, pa.z - pb.z) > 4;
  return {
    ...b,
    players: b.players.map((pb, i) => {
      const pa = a.players[i];
      if (!pa || tele(pa, pb)) return pb;
      return { ...pb, x: L(pa.x, pb.x), z: L(pa.z, pb.z), h: pa.h + wrapAngle(pb.h - pa.h) * k, spd: L(pa.spd, pb.spd), charge: L(pa.charge, pb.charge) };
    }),
    puck: tele(a.puck, b.puck) ? b.puck : { ...b.puck, x: L(a.puck.x, b.puck.x), y: L(a.puck.y, b.puck.y), z: L(a.puck.z, b.puck.z) },
  };
}

// ---------- общие ----------
function cleanupNet() {
  if (app.host) app.host.close();
  if (app.client) app.client.close();
  app.host = null; app.client = null;
  app.remoteInputs.clear();
  app.snaps = [];
  app.stats = null;
}

function toMenu() {
  app.mode = null;
  demo = null;
  cleanupNet();
  app.sim = null; app.inMatch = false; app.paused = false;
  $('hud').classList.add('hidden');
  show('menu');
  renderer.setViews([]);
}

$('btnResume').onclick = () => { app.paused = false; show(null); };
$('btnQuit').onclick = () => toMenu();
$('btnMenu').onclick = () => toMenu();
$('btnAgain').onclick = () => {
  if (app.mode === 'local' || app.mode === 'host') newMatch();
};

window.addEventListener('keydown', (e) => {
  if (!app.inMatch) return;
  if (e.code === 'Escape') {
    if (!$('end').classList.contains('hidden')) return;
    app.paused = !app.paused;
    show(app.paused ? 'pause' : null);
  } else if (e.code === 'KeyH') {
    $('help').classList.toggle('hidden');
  } else if (e.code === 'KeyM') {
    app.muted = !app.muted; setMuted(app.muted);
  }
});

function handleEvent(e) {
  playEvent(e);
  if (e.type === 'hit') renderer.shake = 0.25;
  if (e.type === 'goal') renderer.shake = 0.4;
}

// ---------- HUD ----------
const fmtTime = (t) => { t = Math.max(0, Math.ceil(t)); return `${Math.floor(t / 60)}:${String(t % 60).padStart(2, '0')}`; };
let lastBanner = '';
function updateHud(st) {
  $('s0').textContent = st.score[0];
  $('s1').textContent = st.score[1];
  $('clock').textContent = st.overtime ? '∞' : fmtTime(st.time);
  $('ot').classList.toggle('hidden', !st.overtime);

  let big = '', small = '';
  if (st.phase === 'faceoff') { big = st.phaseT > 0.6 ? String(Math.ceil(st.phaseT - 0.4)) : 'ШАЙБА!'; small = st.overtime ? 'Овертайм — до первой шайбы' : 'Вбрасывание'; }
  else if (st.phase === 'goal' && st.lastGoal) {
    big = 'ГОЛ!';
    const sc = st.lastGoal.scorer != null ? app.roster[st.lastGoal.scorer] : null;
    const as = st.lastGoal.assist != null ? app.roster[st.lastGoal.assist] : null;
    small = `${TEAM_NAMES[st.lastGoal.team]} · ` + (sc ? `забил ${sc.name}${as ? `, пас ${as.name}` : ''}` : 'автогол');
  }
  const key = big + '|' + small;
  if (key !== lastBanner) {
    lastBanner = key;
    $('banner').classList.toggle('hidden', !big);
    $('bannerBig').textContent = big;
    $('bannerSmall').textContent = small;
    $('bannerBig').style.textShadow = st.phase === 'goal' && st.lastGoal ? `0 5px 0 ${st.lastGoal.team === 0 ? '#e2483d' : '#3f86e8'}, 0 10px 0 rgba(0,0,0,.35)` : '';
    if (big) { const b = $('banner'); b.style.animation = 'none'; void b.offsetWidth; b.style.animation = ''; }
  }

  for (const o of app.overlays || []) {
    const p = st.players[o.follow];
    if (!p) continue;
    o.charge.style.width = `${Math.round(p.charge * 100)}%`;
    o.chargeBar.style.opacity = p.charge > 0 ? 1 : 0.25;
    o.stamina.style.width = `${Math.round(p.stamina * 100)}%`;
  }
  drawMinimap(st);

  if (app.mode === 'host') {
    const n = app.lobby.length - 1;
    $('netinfo').classList.remove('hidden');
    $('netinfo').textContent = `Код: ${app.code || '…'} · друзей: ${n}`;
  } else if (app.mode === 'client') {
    $('netinfo').classList.remove('hidden');
    $('netinfo').textContent = `Пинг: ${app.ping ?? '…'} мс`;
  } else $('netinfo').classList.add('hidden');

  if (st.phase === 'end' && !app.endShown) {
    app.endShown = true;
    setTimeout(() => showEnd(st), 1200);
  }
}

function drawMinimap(st) {
  const c = $('minimap'), g = c.getContext('2d');
  const W = c.width, H = c.height, pad = 8;
  const sx = (W - pad * 2) / RINK.L, sz = (H - pad * 2) / RINK.W;
  const X = (x) => pad + (x + RINK.L / 2) * sx, Z = (z) => pad + (z + RINK.W / 2) * sz;
  g.clearRect(0, 0, W, H);
  g.fillStyle = 'rgba(235,245,255,0.92)';
  g.beginPath(); g.roundRect(pad, pad, W - pad * 2, H - pad * 2, RINK.R * sx); g.fill();
  g.fillStyle = '#d9363e'; g.fillRect(X(0) - 1, pad, 2, H - pad * 2);
  g.fillStyle = '#2f6fd6'; g.fillRect(X(RINK.blueX) - 1, pad, 2, H - pad * 2); g.fillRect(X(-RINK.blueX) - 1, pad, 2, H - pad * 2);
  g.fillStyle = '#d9363e';
  g.fillRect(X(RINK.goalX), Z(-RINK.goalW / 2), 3, RINK.goalW * sz); g.fillRect(X(-RINK.goalX) - 3, Z(-RINK.goalW / 2), 3, RINK.goalW * sz);
  const myIdx = new Set((app.overlays || []).map((o) => o.follow));
  st.players.forEach((p, i) => {
    const r = app.roster[i];
    if (!r) return;
    const human = !!r.human;
    g.fillStyle = r.team === 0 ? '#e2483d' : '#3f86e8';
    g.beginPath();
    if (myIdx.has(i)) {
      const a = p.h;
      const x = X(p.x), y = Z(p.z), s = 7;
      g.moveTo(x + Math.sin(a) * s * 1.4, y + Math.cos(a) * s * 1.4);
      g.lineTo(x + Math.sin(a + 2.4) * s, y + Math.cos(a + 2.4) * s);
      g.lineTo(x + Math.sin(a - 2.4) * s, y + Math.cos(a - 2.4) * s);
      g.closePath(); g.fill();
      g.strokeStyle = SLOT_COLORS[(app.slots.get(r.human) ?? 0) % SLOT_COLORS.length]; g.lineWidth = 2; g.stroke();
    } else {
      g.arc(X(p.x), Z(p.z), human ? 4.5 : 3.5, 0, Math.PI * 2); g.fill();
      if (human) { g.strokeStyle = SLOT_COLORS[(app.slots.get(r.human) ?? 0) % SLOT_COLORS.length]; g.lineWidth = 2; g.stroke(); }
    }
  });
  g.fillStyle = '#111'; g.strokeStyle = '#fff'; g.lineWidth = 1.5;
  g.beginPath(); g.arc(X(st.puck.x), Z(st.puck.z), 3, 0, Math.PI * 2); g.fill(); g.stroke();
}

function showEnd(st) {
  show('end');
  const s = st.score;
  const myTeams = new Set(app.myKeys.map((k) => app.roster.find((r) => r.human === k)?.team));
  let title = 'Ничья';
  if (s[0] !== s[1]) {
    const win = s[0] > s[1] ? 0 : 1;
    title = `Победа: ${TEAM_NAMES[win]}!`;
    if (myTeams.size === 1) title = myTeams.has(win) ? '🏆 ПОБЕДА!' : 'Поражение…';
  }
  $('endTitle').textContent = title;
  $('endScore').innerHTML = `<span class="r">${s[0]}</span> : <span class="b">${s[1]}</span>`;
  const stats = app.sim ? app.sim.stats() : app.stats || [];
  stats.sort((a, b) => b.goals * 2 + b.assists - (a.goals * 2 + a.assists));
  $('stats').innerHTML = '<tr><th>Игрок</th><th>Голы</th><th>Пасы</th></tr>' +
    stats.map((p) => `<tr class="t${p.team} ${p.human ? 'human' : ''}"><td>${escapeHtml(p.name)}</td><td>${p.goals}</td><td>${p.assists}</td></tr>`).join('');
  const canRestart = app.mode !== 'client';
  $('btnAgain').classList.toggle('hidden', !canRestart);
  $('endWait').textContent = canRestart ? '' : 'Хост может начать новый матч';
}

// ---------- главный цикл ----------
let last = performance.now();
let simLast = performance.now();
let inputTimer = 0, pingTimer = 0;

// Шаги симуляции идут по реальному времени. Вызывается и из rAF, и из таймера в воркере —
// чтобы матч у хоста не замирал, если он свернул вкладку.
function simTick() {
  const now = performance.now();
  const dt = Math.min(0.25, (now - simLast) / 1000);
  simLast = now;
  if (!((app.mode === 'local' || app.mode === 'host') && app.sim && app.inMatch)) return;
  if (app.paused && app.mode === 'local') return;
  app.acc += dt;
  while (app.acc >= STEP) {
    const inputs = new Map(app.remoteInputs);
    for (const s of app.sources) inputs.set(s.key, app.paused || document.hidden ? readInput({}) : readInput(s));
    app.sim.step(STEP, inputs);
    app.acc -= STEP;
    app.steps++;
    const evs = app.sim.events.splice(0);
    if (!document.hidden) for (const e of evs) handleEvent(e);
    if (app.mode === 'host') {
      app.netEvents.push(...evs);
      if (app.steps % 3 === 0) {
        const vs = app.sim.viewState();
        const msg = { t: 's', ...packState(vs) };
        if (app.netEvents.length) msg.e = app.netEvents.splice(0);
        if (vs.phase === 'end') msg.stats = app.sim.stats();
        app.host.broadcast(msg);
      }
    }
  }
}
try {
  const src = 'setInterval(() => postMessage(0), 16);';
  const w = new Worker(URL.createObjectURL(new Blob([src], { type: 'text/javascript' })));
  w.onmessage = () => { if (app.mode === 'host') simTick(); };
} catch { /* без воркера — только rAF */ }

function frame(now) {
  requestAnimationFrame(frame);
  const dt = Math.min(0.05, (now - last) / 1000);
  last = now;
  let st = null;

  if ((app.mode === 'local' || app.mode === 'host') && app.sim && app.inMatch) {
    simTick();
    st = app.sim.viewState();
  } else if (app.mode === 'client' && app.inMatch) {
    inputTimer += dt; pingTimer += dt;
    if (inputTimer > 1 / 30) {
      inputTimer = 0;
      const inp = app.paused ? readInput({}) : readInput(app.sources[0]);
      app.client.send({ t: 'in', i: packInput(inp) });
    }
    if (pingTimer > 2) { pingTimer = 0; app.client.send({ t: 'ping', c: performance.now() }); }
    st = clientState();
  }

  if (st && app.inMatch) {
    renderer.update(st, dt, app.roster);
    updateHud(st);
    app.view = st;
    renderer.render();
  } else {
    // фон меню: медленный облёт арены
    menuCamera(dt);
  }
}

// облёт камеры на фоне меню (демо-матч ботов)
const menuCam = new THREE.PerspectiveCamera(50, 1, 0.1, 300);
let menuT = 0;
function menuCamera(dt) {
  menuT += dt * 0.08;
  if (!demo || demo.phase === 'end') {
    demo = new Sim({ skaters: 3, duration: 180, difficulty: 2, humans: [] });
    renderer.setRoster(demo.roster(), new Map());
  }
  demo.step(dt, null);
  demo.events.length = 0;
  const vs = demo.viewState();
  renderer.views = [];
  renderer.update(vs, dt, null);
  menuCam.aspect = renderer.w / renderer.h; menuCam.updateProjectionMatrix();
  menuCam.position.set(Math.sin(menuT) * 34, 15, Math.cos(menuT) * 26);
  menuCam.lookAt(vs.puck.x * 0.4, 0, vs.puck.z * 0.4);
  renderer.gl.setScissorTest(false);
  renderer.gl.setViewport(0, 0, renderer.w, renderer.h);
  renderer.gl.render(renderer.scene, menuCam);
}

show('menu');
requestAnimationFrame(frame);

// Для отладки из консоли
window.__hockey = { app, renderer };
