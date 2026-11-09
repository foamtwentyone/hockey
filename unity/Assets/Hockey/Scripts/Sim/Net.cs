// Сетевая игра по UDP: хост считает физику, гости отправляют ввод и получают состояние матча.
// Есть автопоиск матчей в локальной сети. Чистый .NET — без зависимостей от Unity.
using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace Hockey
{
    public static class NetConst
    {
        public const int GamePort = 7777;
        public const int DiscoveryPort = 7778;
        public const int Protocol = 3;
        public static readonly byte[] Magic = { (byte)'D', (byte)'V', (byte)'H', (byte)'K' };
    }

    public enum Msg : byte
    {
        Hello = 1, Input = 2, SetTeam = 3, Bye = 4, Ping = 5,
        Welcome = 17, Session = 18, Snapshot = 19, Pong = 20, Reject = 21,
        Discovery = 32,
    }

    public class LobbyEntry
    {
        public string Key, Name;
        public int Team, Number = -1, Skin = -1, Hair = -1;
    }

    /// Состояние сессии: лобби, настройки и (во время матча) состав команд.
    public class SessionInfo
    {
        public int Seq, MatchId;
        public bool InMatch;
        public MatchConfig Cfg = new MatchConfig();
        public List<LobbyEntry> Lobby = new List<LobbyEntry>();
        public List<RosterEntry> Roster = new List<RosterEntry>();
    }

    public static class Proto
    {
        static void Str(BinaryWriter w, string s) => w.Write(s ?? "");
        public static void WriteHeader(BinaryWriter w, Msg m) { w.Write(NetConst.Magic); w.Write((byte)m); }

        public static bool ReadHeader(BinaryReader r, out Msg m)
        {
            m = 0;
            if (r.BaseStream.Length < 5) return false;
            var mg = r.ReadBytes(4);
            for (int i = 0; i < 4; i++) if (mg[i] != NetConst.Magic[i]) return false;
            m = (Msg)r.ReadByte();
            return true;
        }

        public static void WriteSession(BinaryWriter w, SessionInfo s)
        {
            w.Write(s.Seq); w.Write(s.MatchId); w.Write(s.InMatch);
            w.Write((byte)s.Cfg.Skaters); w.Write((short)s.Cfg.Duration); w.Write((byte)s.Cfg.Difficulty); w.Write(s.Cfg.Arcade);
            w.Write((byte)s.Lobby.Count);
            foreach (var l in s.Lobby) { Str(w, l.Key); Str(w, l.Name); w.Write((byte)l.Team); }
            w.Write((byte)(s.InMatch ? s.Roster.Count : 0));
            if (s.InMatch)
                foreach (var e in s.Roster)
                {
                    w.Write((byte)e.Team); Str(w, e.Human); Str(w, e.Name); w.Write((byte)e.Number);
                    w.Write(e.Goalie); w.Write((byte)e.Skin); w.Write((byte)e.Hair);
                }
        }

        public static SessionInfo ReadSession(BinaryReader r)
        {
            var s = new SessionInfo { Seq = r.ReadInt32(), MatchId = r.ReadInt32(), InMatch = r.ReadBoolean() };
            s.Cfg.Skaters = r.ReadByte(); s.Cfg.Duration = r.ReadInt16(); s.Cfg.Difficulty = r.ReadByte(); s.Cfg.Arcade = r.ReadBoolean();
            int n = r.ReadByte();
            for (int i = 0; i < n; i++) s.Lobby.Add(new LobbyEntry { Key = r.ReadString(), Name = r.ReadString(), Team = r.ReadByte() });
            int m = r.ReadByte();
            for (int i = 0; i < m; i++)
            {
                var e = new RosterEntry { Team = r.ReadByte() };
                string h = r.ReadString(); e.Human = h.Length == 0 ? null : h;
                e.Name = r.ReadString(); e.Number = r.ReadByte(); e.Goalie = r.ReadBoolean(); e.Skin = r.ReadByte(); e.Hair = r.ReadByte();
                s.Roster.Add(e);
            }
            return s;
        }

        static short Q(float v, float scale) => (short)Math.Clamp(MathF.Round(v * scale), short.MinValue, short.MaxValue);

        public static void WriteSnapshot(BinaryWriter w, int matchId, ViewState v, List<GameEvent> evs)
        {
            w.Write(matchId);
            w.Write(v.SimTime); w.Write(v.Time); w.Write(v.PhaseT);
            w.Write((byte)v.Phase); w.Write(v.Overtime);
            w.Write((byte)v.Score0); w.Write((byte)v.Score1);
            w.Write((sbyte)v.LastGoal.Team); w.Write((sbyte)v.LastGoal.Scorer); w.Write((sbyte)v.LastGoal.Assist); w.Write(v.LastGoal.Own);
            w.Write(Q(v.PuckX, 100)); w.Write(Q(v.PuckY, 100)); w.Write(Q(v.PuckZ, 100)); w.Write((sbyte)v.PuckOwner);
            w.Write((byte)v.Players.Length);
            foreach (var p in v.Players)
            {
                w.Write(Q(p.X, 100)); w.Write(Q(p.Z, 100)); w.Write(Q(p.H, 1000)); w.Write(Q(p.Spd, 100));
                w.Write((byte)(p.Charge * 255)); w.Write((byte)(Math.Min(1, p.Swing) * 255)); w.Write((byte)(Math.Min(1, p.Save) * 255));
                w.Write((byte)(p.Stamina * 255));
                w.Write((byte)((p.Stun ? 1 : 0) | (p.Back ? 2 : 0) | (p.Dive ? 4 : 0) | (p.Punch ? 8 : 0) | (p.Brawl ? 16 : 0) | (p.Stagger ? 32 : 0) | (p.Helmet ? 64 : 0)));
                w.Write((byte)Math.Min(255, p.BrawlPower));
                w.Write((byte)p.Goals); w.Write((byte)p.Assists);
                w.Write(Q(p.Y, 100)); w.Write(p.Buffs);
            }
            w.Write(v.PuckMega);
            w.Write((byte)Math.Min(v.Items.Count, 8));
            for (int i = 0; i < v.Items.Count && i < 8; i++) { w.Write(Q(v.Items[i].X, 100)); w.Write(Q(v.Items[i].Z, 100)); w.Write((byte)v.Items[i].Type); }
            int ne = Math.Min(evs.Count, 40);
            w.Write((byte)ne);
            for (int i = 0; i < ne; i++) { w.Write((byte)evs[i].Type); w.Write(Q(evs[i].Value, 100)); w.Write((sbyte)evs[i].Who); }
        }

        public static int ReadSnapshot(BinaryReader r, ViewState v, List<GameEvent> evs)
        {
            int matchId = r.ReadInt32();
            v.SimTime = r.ReadSingle(); v.Time = r.ReadSingle(); v.PhaseT = r.ReadSingle();
            v.Phase = (Phase)r.ReadByte(); v.Overtime = r.ReadBoolean();
            v.Score0 = r.ReadByte(); v.Score1 = r.ReadByte();
            v.LastGoal = new GoalInfo { Team = r.ReadSByte(), Scorer = r.ReadSByte(), Assist = r.ReadSByte(), Own = r.ReadBoolean() };
            v.PuckX = r.ReadInt16() / 100f; v.PuckY = r.ReadInt16() / 100f; v.PuckZ = r.ReadInt16() / 100f; v.PuckOwner = r.ReadSByte();
            int n = r.ReadByte();
            v.Resize(n);
            for (int i = 0; i < n; i++)
            {
                var p = v.Players[i];
                p.X = r.ReadInt16() / 100f; p.Z = r.ReadInt16() / 100f; p.H = r.ReadInt16() / 1000f; p.Spd = r.ReadInt16() / 100f;
                p.Charge = r.ReadByte() / 255f; p.Swing = r.ReadByte() / 255f; p.Save = r.ReadByte() / 255f; p.Stamina = r.ReadByte() / 255f;
                int f = r.ReadByte(); p.Stun = (f & 1) != 0; p.Back = (f & 2) != 0; p.Dive = (f & 4) != 0;
                p.Punch = (f & 8) != 0; p.Brawl = (f & 16) != 0; p.Stagger = (f & 32) != 0; p.Helmet = (f & 64) != 0;
                p.BrawlPower = r.ReadByte();
                p.Goals = r.ReadByte(); p.Assists = r.ReadByte();
                p.Y = r.ReadInt16() / 100f; p.Buffs = r.ReadByte();
            }
            v.PuckMega = r.ReadBoolean();
            int ni = r.ReadByte();
            v.Items.Clear();
            for (int i = 0; i < ni; i++) v.Items.Add(new ItemView { X = r.ReadInt16() / 100f, Z = r.ReadInt16() / 100f, Type = r.ReadByte() });
            int ne = r.ReadByte();
            for (int i = 0; i < ne; i++) evs.Add(new GameEvent((EvType)r.ReadByte(), r.ReadInt16() / 100f, r.ReadSByte()));
            return matchId;
        }
    }

    /// Неблокирующий UDP-сокет с обработкой пакетов в главном потоке.
    public class UdpPeer : IDisposable
    {
        public readonly Socket Sock;
        readonly byte[] buf = new byte[4096];
        readonly MemoryStream ms = new MemoryStream(2048);
        readonly BinaryWriter bw;

        public UdpPeer(int port, bool broadcast = false)
        {
            Sock = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
            Sock.Blocking = false;
            if (broadcast) Sock.EnableBroadcast = true;
            try { Sock.IOControl(-1744830452, new byte[] { 0 }, null); } catch { /* только Windows: игнорировать ICMP reset */ }
            if (port >= 0)
            {
                Sock.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
                Sock.Bind(new IPEndPoint(IPAddress.Any, port));
            }
            else Sock.Bind(new IPEndPoint(IPAddress.Any, 0));
            bw = new BinaryWriter(ms, Encoding.UTF8);
        }

        public BinaryWriter Begin(Msg m)
        {
            ms.SetLength(0);
            Proto.WriteHeader(bw, m);
            return bw;
        }

        public void SendTo(EndPoint ep)
        {
            bw.Flush();
            try { Sock.SendTo(ms.GetBuffer(), 0, (int)ms.Length, SocketFlags.None, ep); } catch (SocketException) { }
        }

        /// Читает все пришедшие пакеты.
        public void Poll(Action<EndPoint, BinaryReader> onPacket)
        {
            for (int guard = 0; guard < 512; guard++)
            {
                EndPoint from = new IPEndPoint(IPAddress.Any, 0);
                int n;
                try
                {
                    if (Sock.Available <= 0) return;
                    n = Sock.ReceiveFrom(buf, ref from);
                }
                catch (SocketException) { continue; }
                catch (ObjectDisposedException) { return; }
                using var rs = new MemoryStream(buf, 0, n, false);
                using var br = new BinaryReader(rs, Encoding.UTF8);
                try { onPacket(from, br); } catch (EndOfStreamException) { } catch (IOException) { }
            }
        }

        public void Dispose() { try { Sock.Close(); } catch { } }
    }

    // ---------------------------------------------------------------- хост
    public class HostNet : IDisposable
    {
        class Client { public string Key; public EndPoint Ep; public double LastSeen; public PlayerInput Input; }

        readonly UdpPeer peer;
        readonly UdpPeer disco;
        readonly Dictionary<string, Client> byEp = new Dictionary<string, Client>();
        int seq;
        double now, lastSession, lastDisco;
        public string HostName;
        public int PlayerCount;
        public bool InMatch;

        public event Action<string, string, int, int, int> OnJoin;   // key, name, номер, кожа, цвет обмотки
        public event Action<string> OnLeave;          // key
        public event Action<string, int> OnSetTeam;   // key, team
        public Func<SessionInfo> GetSession;

        public HostNet(string hostName, int port = NetConst.GamePort)
        {
            HostName = hostName;
            peer = new UdpPeer(port);
            try { disco = new UdpPeer(-1, true); } catch { disco = null; }
        }

        public bool TryGetInput(string key, out PlayerInput inp)
        {
            foreach (var c in byEp.Values) if (c.Key == key) { inp = c.Input; return true; }
            inp = default; return false;
        }

        public IEnumerable<string> ClientKeys { get { foreach (var c in byEp.Values) yield return c.Key; } }

        public void Update(double time)
        {
            now = time;
            peer.Poll(Handle);
            // отвалившиеся игроки
            List<string> dead = null;
            foreach (var kv in byEp) if (now - kv.Value.LastSeen > 6) (dead ??= new List<string>()).Add(kv.Key);
            if (dead != null) foreach (var k in dead) Drop(k);
            if (now - lastSession > 0.5) { lastSession = now; BroadcastSession(); }
            if (disco != null && now - lastDisco > 1)
            {
                lastDisco = now;
                var w = disco.Begin(Msg.Discovery);
                w.Write(NetConst.Protocol); w.Write(HostName ?? "Хоккей"); w.Write((short)NetConst.GamePort);
                w.Write((byte)PlayerCount); w.Write(InMatch);
                disco.SendTo(new IPEndPoint(IPAddress.Broadcast, NetConst.DiscoveryPort));
            }
        }

        void Drop(string epKey)
        {
            if (!byEp.TryGetValue(epKey, out var c)) return;
            byEp.Remove(epKey);
            OnLeave?.Invoke(c.Key);
        }

        void Handle(EndPoint from, BinaryReader r)
        {
            if (!Proto.ReadHeader(r, out var m)) return;
            string ek = from.ToString();
            byEp.TryGetValue(ek, out var c);
            switch (m)
            {
                case Msg.Hello:
                    {
                        int proto = r.ReadInt32();
                        string name = r.ReadString();
                        int num = -1, skin = -1, hair = -1;
                        if (r.BaseStream.Position + 3 <= r.BaseStream.Length) { num = r.ReadByte(); skin = r.ReadByte(); hair = r.ReadByte(); }
                        if (name.Length > 14) name = name.Substring(0, 14);
                        if (proto != NetConst.Protocol)
                        {
                            var rw = peer.Begin(Msg.Reject); rw.Write("Другая версия игры"); peer.SendTo(from); return;
                        }
                        if (c == null)
                        {
                            c = new Client { Key = "r" + (++seq), Ep = from, LastSeen = now };
                            byEp[ek] = c;
                            OnJoin?.Invoke(c.Key, string.IsNullOrWhiteSpace(name) ? "Игрок" : name, num, skin, hair);
                        }
                        c.LastSeen = now;
                        var w = peer.Begin(Msg.Welcome); w.Write(c.Key); peer.SendTo(from);
                        SendSession(from);
                        break;
                    }
                case Msg.Input:
                    if (c == null) return;
                    c.LastSeen = now;
                    c.Input = PlayerInput.FromBits(r.ReadSingle(), r.ReadSingle(), r.ReadByte());
                    break;
                case Msg.SetTeam:
                    if (c == null) return;
                    c.LastSeen = now;
                    OnSetTeam?.Invoke(c.Key, r.ReadByte());
                    break;
                case Msg.Ping:
                    {
                        if (c != null) c.LastSeen = now;
                        double t = r.ReadDouble();
                        var w = peer.Begin(Msg.Pong); w.Write(t); peer.SendTo(from);
                        break;
                    }
                case Msg.Bye:
                    if (c != null) Drop(ek);
                    break;
            }
        }

        void SendSession(EndPoint ep)
        {
            var s = GetSession?.Invoke();
            if (s == null) return;
            var w = peer.Begin(Msg.Session);
            Proto.WriteSession(w, s);
            peer.SendTo(ep);
        }

        public void BroadcastSession()
        {
            var s = GetSession?.Invoke();
            if (s == null) return;
            var w = peer.Begin(Msg.Session);
            Proto.WriteSession(w, s);
            foreach (var c in byEp.Values) peer.SendTo(c.Ep);
        }

        public void BroadcastSnapshot(int matchId, ViewState v, List<GameEvent> evs)
        {
            if (byEp.Count == 0) return;
            var w = peer.Begin(Msg.Snapshot);
            Proto.WriteSnapshot(w, matchId, v, evs);
            foreach (var c in byEp.Values) peer.SendTo(c.Ep);
        }

        public void Dispose() { peer.Dispose(); disco?.Dispose(); }
    }

    // ---------------------------------------------------------------- гость
    public class ClientNet : IDisposable
    {
        readonly UdpPeer peer;
        readonly EndPoint host;
        readonly string name;
        double now, lastHello = -10, lastInput, lastPing, lastHeard;
        public string MyKey;
        public bool Connected => MyKey != null;
        public SessionInfo Session;
        public int PingMs = -1;
        public string Error;
        public readonly double StartedAt;

        public event Action<SessionInfo> OnSession;
        public event Action<ViewState, int, List<GameEvent>> OnSnapshot;

        readonly byte pNum, pSkin, pHair;

        public ClientNet(string address, string playerName, double time, int number = 0, int skin = 0, int hair = 0)
        {
            name = playerName;
            pNum = (byte)Math.Clamp(number, 0, 99); pSkin = (byte)Math.Clamp(skin, 0, 255); pHair = (byte)Math.Clamp(hair, 0, 255);
            StartedAt = time; lastHeard = time;
            host = Resolve(address);
            peer = new UdpPeer(-1);
        }

        static EndPoint Resolve(string address)
        {
            address = (address ?? "").Trim();
            int port = NetConst.GamePort;
            int colon = address.LastIndexOf(':');
            if (colon > 0 && int.TryParse(address.Substring(colon + 1), out int p)) { port = p; address = address.Substring(0, colon); }
            if (!IPAddress.TryParse(address, out var ip))
            {
                var list = Dns.GetHostAddresses(address);
                ip = null;
                foreach (var a in list) if (a.AddressFamily == AddressFamily.InterNetwork) { ip = a; break; }
                if (ip == null) throw new Exception("Не удалось найти адрес " + address);
            }
            return new IPEndPoint(ip, port);
        }

        public void Update(double time, PlayerInput inp)
        {
            now = time;
            peer.Poll(Handle);
            if (!Connected && now - lastHello > 0.5)
            {
                lastHello = now;
                var w = peer.Begin(Msg.Hello); w.Write(NetConst.Protocol); w.Write(name); w.Write(pNum); w.Write(pSkin); w.Write(pHair); peer.SendTo(host);
            }
            if (Connected && now - lastInput > 1.0 / 30)
            {
                lastInput = now;
                var w = peer.Begin(Msg.Input); w.Write(inp.MoveX); w.Write(inp.MoveZ); w.Write(inp.Bits); peer.SendTo(host);
            }
            if (Connected && now - lastPing > 1.5)
            {
                lastPing = now;
                var w = peer.Begin(Msg.Ping); w.Write(now); peer.SendTo(host);
            }
            if (!Connected && now - StartedAt > 10 && Error == null) Error = "Хост не отвечает. Проверь адрес, порт 7777 (UDP) и брандмауэр.";
            if (Connected && now - lastHeard > 6 && Error == null) Error = "Соединение с хостом потеряно.";
        }

        public void SetTeam(int team)
        {
            var w = peer.Begin(Msg.SetTeam); w.Write((byte)team); peer.SendTo(host);
        }

        readonly List<GameEvent> evBuf = new List<GameEvent>();

        void Handle(EndPoint from, BinaryReader r)
        {
            if (!Proto.ReadHeader(r, out var m)) return;
            lastHeard = now;
            switch (m)
            {
                case Msg.Welcome: MyKey = r.ReadString(); break;
                case Msg.Reject: Error = r.ReadString(); break;
                case Msg.Session:
                    {
                        var s = Proto.ReadSession(r);
                        if (Session != null && s.Seq < Session.Seq) return;
                        Session = s;
                        OnSession?.Invoke(s);
                        break;
                    }
                case Msg.Snapshot:
                    {
                        var v = new ViewState();
                        evBuf.Clear();
                        int mid = Proto.ReadSnapshot(r, v, evBuf);
                        OnSnapshot?.Invoke(v, mid, evBuf);
                        break;
                    }
                case Msg.Pong: PingMs = (int)((now - r.ReadDouble()) * 1000); break;
            }
        }

        public void Dispose()
        {
            try { peer.Begin(Msg.Bye); peer.SendTo(host); } catch { }
            peer.Dispose();
        }
    }

    // ---------------------------------------------------------------- поиск матчей в локальной сети
    public class LanFinder : IDisposable
    {
        public class Found { public string Name, Address; public int Players; public bool InMatch; public double Seen; }
        readonly UdpPeer peer;
        public readonly List<Found> Games = new List<Found>();
        public string Error;

        public LanFinder()
        {
            try { peer = new UdpPeer(NetConst.DiscoveryPort); }
            catch (Exception e) { Error = e.Message; }
        }

        public void Update(double now)
        {
            peer?.Poll((from, r) =>
            {
                if (!Proto.ReadHeader(r, out var m) || m != Msg.Discovery) return;
                if (r.ReadInt32() != NetConst.Protocol) return;
                string name = r.ReadString(); int port = r.ReadInt16(); int players = r.ReadByte(); bool inMatch = r.ReadBoolean();
                string addr = ((IPEndPoint)from).Address + ":" + port;
                var f = Games.Find(g => g.Address == addr);
                if (f == null) { f = new Found { Address = addr }; Games.Add(f); }
                f.Name = name; f.Players = players; f.InMatch = inMatch; f.Seen = now;
            });
            Games.RemoveAll(g => now - g.Seen > 4);
        }

        public void Dispose() => peer?.Dispose();
    }

    public static class NetUtil
    {
        /// Локальные IPv4-адреса компьютера (чтобы показать хосту, что сообщить друзьям).
        public static List<string> LocalAddresses()
        {
            var res = new List<string>();
            try
            {
                foreach (var a in Dns.GetHostAddresses(Dns.GetHostName()))
                    if (a.AddressFamily == AddressFamily.InterNetwork && !IPAddress.IsLoopback(a)) res.Add(a.ToString());
            }
            catch { }
            if (res.Count == 0)
            {
                try
                {
                    using var s = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
                    s.Connect("8.8.8.8", 65530);
                    if (s.LocalEndPoint is IPEndPoint ep) res.Add(ep.Address.ToString());
                }
                catch { }
            }
            return res;
        }
    }
}
