// Онлайн через WebRTC (PeerJS). Хост считает физику, гости шлют ввод и получают состояние.
const PREFIX = 'dvor-hockey-3d-';
const ALPHABET = 'ABCDEFGHJKLMNPQRSTUVWXYZ23456789';

export function makeCode() {
  let s = '';
  for (let i = 0; i < 5; i++) s += ALPHABET[Math.floor(Math.random() * ALPHABET.length)];
  return s;
}

function PeerCtor() {
  if (!window.Peer) throw new Error('PeerJS не загрузился');
  return window.Peer;
}

const PEER_OPTS = {
  debug: 1,
  config: {
    iceServers: [
      { urls: 'stun:stun.l.google.com:19302' },
      { urls: 'stun:stun1.l.google.com:19302' },
      { urls: 'stun:global.stun.twilio.com:3478' },
    ],
  },
};

export class Host {
  constructor({ onJoin, onLeave, onMessage, onReady, onError }) {
    this.conns = new Map(); // key -> conn
    this.seq = 0;
    this.cb = { onJoin, onLeave, onMessage, onReady, onError };
    this.open(makeCode(), 0);
  }
  open(code, attempt) {
    const Peer = PeerCtor();
    this.code = code;
    this.peer = new Peer(PREFIX + code, PEER_OPTS);
    this.peer.on('open', () => this.cb.onReady(code));
    this.peer.on('error', (err) => {
      if (err.type === 'unavailable-id' && attempt < 5) { this.peer.destroy(); this.open(makeCode(), attempt + 1); return; }
      this.cb.onError(err);
    });
    this.peer.on('connection', (conn) => {
      const key = 'r' + ++this.seq;
      conn.on('open', () => { this.conns.set(key, conn); });
      conn.on('data', (msg) => {
        if (msg && msg.t === 'hello') this.cb.onJoin(key, String(msg.name || 'Игрок').slice(0, 16));
        else this.cb.onMessage(key, msg);
      });
      const gone = () => { if (this.conns.delete(key)) this.cb.onLeave(key); };
      conn.on('close', gone);
      conn.on('error', gone);
    });
  }
  send(key, msg) { const c = this.conns.get(key); if (c && c.open) c.send(msg); }
  broadcast(msg) { for (const c of this.conns.values()) if (c.open) c.send(msg); }
  kick(key) { const c = this.conns.get(key); if (c) c.close(); }
  close() { try { this.peer.destroy(); } catch {} }
}

export class Client {
  constructor(code, name, { onOpen, onMessage, onClose, onError }) {
    const Peer = PeerCtor();
    this.peer = new Peer(PEER_OPTS);
    let opened = false;
    const timer = setTimeout(() => { if (!opened) onError(new Error('Не удалось подключиться. Проверь код или попробуй ещё раз.')); }, 15000);
    this.peer.on('open', () => {
      this.conn = this.peer.connect(PREFIX + code.toUpperCase().trim(), { reliable: true, serialization: 'json' });
      this.conn.on('open', () => { opened = true; clearTimeout(timer); this.conn.send({ t: 'hello', name }); onOpen(); });
      this.conn.on('data', onMessage);
      this.conn.on('close', onClose);
      this.conn.on('error', (e) => onError(e));
    });
    this.peer.on('error', (err) => {
      clearTimeout(timer);
      if (err.type === 'peer-unavailable') onError(new Error('Матч с таким кодом не найден.'));
      else onError(err);
    });
  }
  send(msg) { if (this.conn && this.conn.open) this.conn.send(msg); }
  close() { try { this.peer.destroy(); } catch {} }
}
