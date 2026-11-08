// Синтезированные звуки — без внешних файлов.
let ctx = null, master = null, crowdGain = null, noiseBuf = null;

export function initAudio() {
  if (ctx) { if (ctx.state === 'suspended') ctx.resume(); return; }
  try {
    ctx = new (window.AudioContext || window.webkitAudioContext)();
  } catch { return; }
  master = ctx.createGain();
  master.gain.value = 0.6;
  master.connect(ctx.destination);
  noiseBuf = ctx.createBuffer(1, ctx.sampleRate * 2, ctx.sampleRate);
  const d = noiseBuf.getChannelData(0);
  for (let i = 0; i < d.length; i++) d[i] = Math.random() * 2 - 1;
  // гул трибун
  const src = ctx.createBufferSource();
  src.buffer = noiseBuf; src.loop = true;
  const f = ctx.createBiquadFilter(); f.type = 'bandpass'; f.frequency.value = 600; f.Q.value = 0.5;
  crowdGain = ctx.createGain(); crowdGain.gain.value = 0.05;
  src.connect(f).connect(crowdGain).connect(master);
  src.start();
}

export function setMuted(m) { if (master) master.gain.value = m ? 0 : 0.6; }

function noise(dur, freq, q, vol, type = 'bandpass') {
  if (!ctx) return;
  const t = ctx.currentTime;
  const s = ctx.createBufferSource();
  s.buffer = noiseBuf;
  const f = ctx.createBiquadFilter(); f.type = type; f.frequency.value = freq; f.Q.value = q;
  const g = ctx.createGain();
  g.gain.setValueAtTime(vol, t);
  g.gain.exponentialRampToValueAtTime(0.001, t + dur);
  s.connect(f).connect(g).connect(master);
  s.start(t, Math.random()); s.stop(t + dur + 0.05);
}
function tone(freq, dur, vol, type = 'sine', slide = 0, delay = 0) {
  if (!ctx) return;
  const t = ctx.currentTime + delay;
  const o = ctx.createOscillator(); o.type = type;
  o.frequency.setValueAtTime(freq, t);
  if (slide) o.frequency.exponentialRampToValueAtTime(Math.max(30, freq + slide), t + dur);
  const g = ctx.createGain();
  g.gain.setValueAtTime(0.0001, t);
  g.gain.exponentialRampToValueAtTime(vol, t + 0.01);
  g.gain.exponentialRampToValueAtTime(0.0001, t + dur);
  o.connect(g).connect(master);
  o.start(t); o.stop(t + dur + 0.05);
}

function cheer(level, dur) {
  if (!crowdGain) return;
  const t = ctx.currentTime;
  crowdGain.gain.cancelScheduledValues(t);
  crowdGain.gain.setValueAtTime(crowdGain.gain.value, t);
  crowdGain.gain.linearRampToValueAtTime(level, t + 0.2);
  crowdGain.gain.linearRampToValueAtTime(0.05, t + dur);
}

export function playEvent(e) {
  if (!ctx) return;
  switch (e.type) {
    case 'shot': noise(0.12, 2500, 1.5, 0.5 + e.power * 0.4); tone(180, 0.08, 0.3, 'triangle', -80); break;
    case 'pass': noise(0.07, 3000, 2, 0.3); break;
    case 'receive': noise(0.05, 1800, 2, 0.15); break;
    case 'swish': noise(0.15, 900, 0.7, 0.12, 'highpass'); break;
    case 'whoosh': noise(0.2, 700, 0.8, 0.15); break;
    case 'board': noise(0.18, e.puck ? 400 : 160, 1, Math.min(0.6, (e.v || 5) / 18)); break;
    case 'post': tone(1650, 0.5, 0.35, 'sine'); tone(2470, 0.4, 0.15, 'sine'); break;
    case 'hit': noise(0.25, 140, 0.8, 0.8); tone(90, 0.2, 0.5, 'sine', -40); cheer(0.18, 1.2); break;
    case 'body': noise(0.1, 300, 1, 0.3); break;
    case 'save': noise(0.1, 900, 1, 0.4); cheer(0.14, 1); break;
    case 'deflect': noise(0.08, 2000, 1.5, 0.3); break;
    case 'steal': noise(0.06, 2200, 2, 0.25); break;
    case 'whistle': tone(2900, 0.35, 0.18, 'sine', 120); tone(2950, 0.35, 0.1, 'square', 100); break;
    case 'goal':
      tone(220, 2.2, 0.28, 'sawtooth'); tone(277, 2.2, 0.2, 'sawtooth'); tone(330, 2.2, 0.2, 'sawtooth');
      cheer(0.45, 4);
      break;
    case 'horn': tone(196, 1.6, 0.3, 'sawtooth'); tone(247, 1.6, 0.2, 'sawtooth'); break;
    default: break;
  }
}
