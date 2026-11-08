// Симуляция хоккейного матча: физика, шайба, вратари, ИИ ботов.
// Чистый C# без UnityEngine — одинаково работает в одиночной игре, на одном экране и на хосте онлайн-матча.
using System;
using System.Collections.Generic;

namespace Hockey
{
    public static class Rink
    {
        public const float L = 60f, W = 30f, R = 8f;
        public const float GoalX = 26f, GoalW = 3.2f, GoalH = 1.3f, GoalD = 1.2f;
        public const float BoardH = 1.1f, BlueX = 8f;
        public static readonly int[] TeamDir = { 1, -1 };
        public static readonly string[] TeamNames = { "Красные", "Синие" };

        public struct Hit { public float X, Z, Nx, Nz; }

        /// Выталкивание точки внутрь закруглённого прямоугольника площадки.
        public static bool Clamp(float x, float z, float r, out Hit hit)
        {
            float hx = L / 2, hz = W / 2;
            float cx = hx - R, cz = hz - R;
            float ax = Math.Abs(x), az = Math.Abs(z);
            hit = default;
            if (ax > cx && az > cz)
            {
                float dx = ax - cx, dz = az - cz;
                float d = MathF.Sqrt(dx * dx + dz * dz);
                if (d > R - r)
                {
                    float nx = dx / d, nz = dz / d;
                    hit.X = Math.Sign(x) * (cx + nx * (R - r));
                    hit.Z = Math.Sign(z) * (cz + nz * (R - r));
                    hit.Nx = -Math.Sign(x) * nx; hit.Nz = -Math.Sign(z) * nz;
                    return true;
                }
                return false;
            }
            bool h = false; float onx = 0, onz = 0, ox = x, oz = z;
            if (ax > hx - r) { ox = Math.Sign(x) * (hx - r); onx = -Math.Sign(x); h = true; }
            if (az > hz - r) { oz = Math.Sign(z) * (hz - r); onz = -Math.Sign(z); h = true; }
            if (!h) return false;
            float l = MathF.Sqrt(onx * onx + onz * onz);
            hit.X = ox; hit.Z = oz; hit.Nx = onx / l; hit.Nz = onz / l;
            return true;
        }
    }

    public struct PlayerInput
    {
        public float Throttle, Turn;
        public bool Shoot, Pass, Check, Sprint;
        public static readonly PlayerInput None = default;

        public byte Bits => (byte)((Shoot ? 1 : 0) | (Pass ? 2 : 0) | (Check ? 4 : 0) | (Sprint ? 8 : 0));
        public static PlayerInput FromBits(float throttle, float turn, byte bits) => new PlayerInput
        {
            Throttle = Math.Clamp(throttle, -1, 1), Turn = Math.Clamp(turn, -1, 1),
            Shoot = (bits & 1) != 0, Pass = (bits & 2) != 0, Check = (bits & 4) != 0, Sprint = (bits & 8) != 0,
        };
    }

    public enum EvType : byte { Shot, Pass, Receive, Swish, Whoosh, Board, BoardPuck, Post, Hit, Body, Save, Deflect, Steal, Whistle, Goal, Horn, Overtime, End }

    public struct GameEvent
    {
        public EvType Type;
        public float Value;
        public GameEvent(EvType t, float v = 0) { Type = t; Value = v; }
    }

    public enum Phase : byte { Faceoff, Play, Goal, End }

    public class HumanSlot
    {
        public string Key, Name;
        public int Team;
        public HumanSlot(string key, string name, int team) { Key = key; Name = name; Team = team; }
    }

    public class RosterEntry
    {
        public int Team, Number, Skin, Hair;
        public string Human; // null — бот
        public string Name;
        public bool Goalie;
    }

    public class MatchConfig
    {
        public int Skaters = 3, Duration = 180, Difficulty = 1;
    }

    public class GoalInfo
    {
        public int Team = -1, Scorer = -1, Assist = -1;
        public bool Own;
    }

    /// Всё, что нужно для отрисовки кадра. Одинаково у хоста и у гостей.
    public class PlayerView
    {
        public float X, Z, H, Spd, Charge, Swing, Save, Stamina;
        public bool Stun, Back;
        public int Goals, Assists;
    }

    public class ViewState
    {
        public PlayerView[] Players = Array.Empty<PlayerView>();
        public float PuckX, PuckY, PuckZ;
        public int PuckOwner = -1;
        public int Score0, Score1;
        public float Time, PhaseT, SimTime;
        public Phase Phase;
        public bool Overtime;
        public GoalInfo LastGoal = new GoalInfo();

        public void Resize(int n)
        {
            if (Players.Length == n) return;
            Players = new PlayerView[n];
            for (int i = 0; i < n; i++) Players[i] = new PlayerView();
        }
    }

    class BotState { public float ShootT = -1, ChargeFor; }

    public class Player
    {
        public int Idx, Team, Number, Skin, Hair, Lane, Goals, Assists;
        public string Human, Name;
        public bool Goalie;
        public float X, Z, Vx, Vz, H;
        public float Charge, Stun, Checking, CheckCd, NoPick, Swing, Save, Stamina = 1;
        public PlayerInput Prev;
        internal BotState Bot = new BotState();
    }

    public class Puck
    {
        public float X, Y, Z, Vx, Vy, Vz, HoldT, PassT;
        public int Owner = -1, PassTarget = -1;
        public int[] LastTouch = { -1, -1 };
        public Dictionary<int, bool> SaveCheck = new Dictionary<int, bool>();
        public bool InNet;
    }

    public class Sim
    {
        struct Diff { public float Speed, React, ShootErr, SaveP, Steal, CheckP; }
        static readonly Diff[] Diffs =
        {
            new Diff { Speed = 0.84f, React = 0.55f, ShootErr = 0.16f, SaveP = 0.45f, Steal = 1.2f, CheckP = 0.8f },
            new Diff { Speed = 0.93f, React = 0.8f, ShootErr = 0.09f, SaveP = 0.56f, Steal = 1.8f, CheckP = 1.3f },
            new Diff { Speed = 1.0f, React = 1.0f, ShootErr = 0.05f, SaveP = 0.66f, Steal = 2.4f, CheckP = 1.8f },
        };
        static readonly string[] BotNames = { "Медведь", "Шайба", "Молния", "Танк", "Ёжик", "Кузя", "Буран", "Вихрь",
            "Сокол", "Барс", "Пельмень", "Ракета", "Гром", "Лось", "Бублик", "Тайфун", "Филин", "Колобок" };

        const float PlayerR = 0.45f, PuckR = 0.12f, G = 9.8f;

        public readonly MatchConfig Cfg;
        public readonly List<Player> Players = new List<Player>();
        public readonly Puck Puck = new Puck();
        public readonly int[] Score = new int[2];
        public float Time, SimTime, PhaseT;
        public bool Overtime;
        public Phase Phase = Phase.Faceoff;
        public GoalInfo LastGoal = new GoalInfo();
        public readonly List<GameEvent> Events = new List<GameEvent>();

        readonly Diff diff;
        readonly Random rng;
        readonly Stack<string> names;
        Player carrier;
        readonly int[] chaser = { -1, -1 };

        public Sim(MatchConfig cfg, IList<HumanSlot> humans, int seed = 0)
        {
            Cfg = cfg;
            rng = seed == 0 ? new Random() : new Random(seed);
            diff = Diffs[Math.Clamp(cfg.Difficulty, 0, 2)];
            Time = cfg.Duration;
            var nl = new List<string>(BotNames);
            for (int i = nl.Count - 1; i > 0; i--) { int j = rng.Next(i + 1); (nl[i], nl[j]) = (nl[j], nl[i]); }
            names = new Stack<string>(nl);
            BuildTeams(humans ?? Array.Empty<HumanSlot>());
            Faceoff();
        }

        float Rnd(float a, float b) => a + (float)rng.NextDouble() * (b - a);
        float Rand() => (float)rng.NextDouble();
        static float Hyp(float a, float b) => MathF.Sqrt(a * a + b * b);
        static float Clamp(float v, float a, float b) => v < a ? a : v > b ? b : v;
        public static float Wrap(float a)
        {
            while (a > MathF.PI) a -= MathF.PI * 2;
            while (a < -MathF.PI) a += MathF.PI * 2;
            return a;
        }
        string NextBotName() => names.Count > 0 ? names.Pop() : "Бот";

        void BuildTeams(IList<HumanSlot> humans)
        {
            var used = new[] { new HashSet<int>(), new HashSet<int>() };
            int Num(int t) { int n; do { n = 2 + rng.Next(97); } while (used[t].Contains(n)); used[t].Add(n); return n; }
            for (int t = 0; t < 2; t++)
            {
                var hs = new List<HumanSlot>();
                foreach (var h in humans) if (h.Team == t) hs.Add(h);
                int count = Math.Max(Cfg.Skaters, hs.Count);
                for (int i = 0; i < count; i++)
                {
                    var h = i < hs.Count ? hs[i] : null;
                    Add(t, h?.Key, h != null ? h.Name : NextBotName(), Num(t), false);
                }
                Add(t, null, "Вратарь", t == 0 ? 1 : 30, true);
            }
        }

        void Add(int team, string human, string name, int number, bool goalie)
        {
            Players.Add(new Player
            {
                Idx = Players.Count, Team = team, Human = human, Name = name, Number = number, Goalie = goalie,
                Skin = rng.Next(5), Hair = rng.Next(6),
            });
        }

        public List<RosterEntry> Roster()
        {
            var r = new List<RosterEntry>();
            foreach (var p in Players)
                r.Add(new RosterEntry { Team = p.Team, Human = p.Human, Name = p.Name, Number = p.Number, Goalie = p.Goalie, Skin = p.Skin, Hair = p.Hair });
            return r;
        }

        /// Игрок подключился посреди матча — занимает место бота.
        public Player ReplaceBotWithHuman(string key, string name, int team)
        {
            Player pick = null;
            foreach (var p in Players) if (!p.Goalie && p.Human == null && p.Team == team) { pick = p; break; }
            if (pick == null) foreach (var p in Players) if (!p.Goalie && p.Human == null) { pick = p; break; }
            if (pick == null) return null;
            pick.Human = key; pick.Name = name;
            return pick;
        }

        public void HumanLeft(string key)
        {
            foreach (var p in Players) if (p.Human == key) { p.Human = null; p.Name = NextBotName(); }
        }

        void Ev(EvType t, float v = 0) => Events.Add(new GameEvent(t, v));

        static readonly float[,] Spots = { { -1.6f, 0 }, { -6, 7 }, { -6, -7 }, { -12, 0 }, { -11, 10 }, { -11, -10 }, { -16, 4 }, { -16, -4 } };

        public void Faceoff()
        {
            for (int t = 0; t < 2; t++)
            {
                int i = 0;
                foreach (var p in Players)
                {
                    if (p.Team != t) continue;
                    p.Vx = p.Vz = 0; p.Charge = p.Stun = p.Checking = p.Swing = p.Save = 0;
                    p.H = Rink.TeamDir[t] > 0 ? MathF.PI / 2 : -MathF.PI / 2;
                    if (p.Goalie) { p.X = -Rink.TeamDir[t] * (Rink.GoalX - 0.9f); p.Z = 0; continue; }
                    int s = i % Spots.GetLength(0);
                    p.Lane = i;
                    p.X = Spots[s, 0] * Rink.TeamDir[t];
                    p.Z = Spots[s, 1] * (t == 0 ? 1 : -1);
                    i++;
                }
            }
            var pk = Puck;
            pk.X = pk.Y = pk.Z = pk.Vx = pk.Vy = pk.Vz = pk.HoldT = 0;
            pk.Owner = -1; pk.PassTarget = -1; pk.SaveCheck.Clear(); pk.InNet = false;
            Phase = Phase.Faceoff;
            PhaseT = 2.2f;
        }

        // ---------------- основной шаг ----------------
        public void Step(float dt, IDictionary<string, PlayerInput> inputs)
        {
            SimTime += dt;
            if (Phase == Phase.Faceoff)
            {
                PhaseT -= dt;
                if (PhaseT <= 0)
                {
                    Phase = Phase.Play;
                    Puck.Vx = Rnd(-1, 1); Puck.Vz = Rnd(-1.5f, 1.5f); Puck.Y = 0.6f; Puck.Vy = 0;
                    Ev(EvType.Whistle);
                }
                return;
            }
            if (Phase == Phase.Play)
            {
                if (!Overtime)
                {
                    Time -= dt;
                    if (Time <= 0)
                    {
                        Time = 0;
                        if (Score[0] == Score[1]) { Overtime = true; Ev(EvType.Overtime); Ev(EvType.Horn); }
                        else { Phase = Phase.End; Ev(EvType.Horn); Ev(EvType.End); }
                    }
                }
            }
            else if (Phase == Phase.Goal)
            {
                PhaseT -= dt;
                if (PhaseT <= 0)
                {
                    if (Overtime) { Phase = Phase.End; Ev(EvType.End); }
                    else Faceoff();
                    return;
                }
            }

            bool live = Phase == Phase.Play;
            UpdateRoles();
            foreach (var p in Players)
            {
                PlayerInput inp;
                if (p.Goalie) inp = PlayerInput.None;
                else if (p.Human != null) inp = inputs != null && inputs.TryGetValue(p.Human, out var hi) ? hi : PlayerInput.None;
                else inp = live ? BotInput(p, dt) : IdleBotInput(p);
                if (Phase == Phase.End) inp = PlayerInput.None;
                if (p.Goalie) UpdateGoalie(p, dt);
                else UpdateSkater(p, inp, dt, live);
                p.Prev = inp;
            }
            CollidePlayers();
            UpdatePuck(dt);
        }

        // ---------------- полевые игроки ----------------
        public static void Blade(Player p, out float bx, out float bz)
        {
            float fx = MathF.Sin(p.H), fz = MathF.Cos(p.H);
            float rx = -fz, rz = fx;
            bx = p.X + fx * 0.95f + rx * 0.28f;
            bz = p.Z + fz * 0.95f + rz * 0.28f;
        }

        void UpdateSkater(Player p, PlayerInput inp, float dt, bool live)
        {
            p.CheckCd = MathF.Max(0, p.CheckCd - dt);
            p.NoPick = MathF.Max(0, p.NoPick - dt);
            p.Swing = MathF.Max(0, p.Swing - dt);
            p.Checking = MathF.Max(0, p.Checking - dt);
            if (p.Stun > 0) { p.Stun -= dt; inp = PlayerInput.None; p.Charge = 0; }
            float spd = Hyp(p.Vx, p.Vz);

            float turnRate = Clamp(3.7f - spd * 0.17f, 2.1f, 3.7f);
            p.H = Wrap(p.H + Clamp(inp.Turn, -1, 1) * turnRate * dt);

            // сцепление коньков: боковая скорость частично переходит в продольную
            float nfx = MathF.Sin(p.H), nfz = MathF.Cos(p.H);
            float fwd = p.Vx * nfx + p.Vz * nfz;
            float lx = p.Vx - fwd * nfx, lz = p.Vz - fwd * nfz;
            float lat = Hyp(lx, lz);
            float grip = 1 - MathF.Exp(-(p.Stun > 0 ? 0.5f : 5.5f) * dt);
            fwd += (fwd >= 0 ? 1 : -1) * lat * grip * 0.75f;
            lx *= 1 - grip; lz *= 1 - grip;

            bool hasPuck = Puck.Owner == p.Idx;
            float botMul = p.Human != null ? 1 : diff.Speed;
            bool sprinting = inp.Sprint && p.Stamina > 0.05f && inp.Throttle > 0;
            if (sprinting) p.Stamina = MathF.Max(0, p.Stamina - dt * 0.33f);
            else p.Stamina = MathF.Min(1, p.Stamina + dt * 0.18f);
            float maxS = (sprinting ? 10.8f : 8.2f) * botMul * (hasPuck ? 0.93f : 1);
            float accel = sprinting ? 13 : 10.5f;

            if (inp.Throttle > 0)
            {
                if (fwd < maxS) fwd = MathF.Min(maxS, fwd + accel * inp.Throttle * dt);
            }
            else if (inp.Throttle < 0)
            {
                if (fwd > 0.4f) fwd -= MathF.Min(fwd, 16 * dt);
                else if (fwd > -3.6f) fwd -= 7 * dt;
            }
            p.Vx = fwd * nfx + lx;
            p.Vz = fwd * nfz + lz;
            float fr = inp.Throttle == 0 ? 0.45f : 0.2f;
            p.Vx *= 1 - fr * dt; p.Vz *= 1 - fr * dt;
            spd = Hyp(p.Vx, p.Vz);
            float cap = maxS * 1.35f;
            if (spd > cap) { p.Vx *= cap / spd; p.Vz *= cap / spd; }

            if (live)
            {
                if (inp.Check && !p.Prev.Check && p.CheckCd <= 0 && p.Stun <= 0)
                {
                    p.Checking = 0.35f; p.CheckCd = 1.3f;
                    p.Vx += nfx * 5; p.Vz += nfz * 5;
                    Ev(EvType.Whoosh);
                }
                if (inp.Shoot) p.Charge = MathF.Min(1, p.Charge + dt / 0.9f);
                else if (p.Prev.Shoot && p.Stun <= 0) { Shoot(p, p.Charge); p.Charge = 0; }
                else p.Charge = 0;
                if (inp.Pass && !p.Prev.Pass && p.Stun <= 0) DoPass(p);
            }
            else p.Charge = 0;

            p.X += p.Vx * dt; p.Z += p.Vz * dt;
            if (Rink.Clamp(p.X, p.Z, PlayerR, out var c))
            {
                p.X = c.X; p.Z = c.Z;
                float vn = p.Vx * c.Nx + p.Vz * c.Nz;
                if (vn < 0)
                {
                    p.Vx -= vn * c.Nx * 1.4f; p.Vz -= vn * c.Nz * 1.4f;
                    if (vn < -6) Ev(EvType.Board, -vn);
                }
            }
            CollideGoalFrames(p);
        }

        void CollideGoalFrames(Player o)
        {
            float r = PlayerR;
            foreach (int s in new[] { 1, -1 })
            {
                float x0 = Rink.GoalX - 0.08f, x1 = Rink.GoalX + Rink.GoalD + 0.1f, hz = Rink.GoalW / 2 + 0.12f;
                float ax = o.X * s;
                float cx = Clamp(ax, x0, x1), cz = Clamp(o.Z, -hz, hz);
                float dx = ax - cx, dz = o.Z - cz;
                float d = Hyp(dx, dz);
                if (d < r)
                {
                    if (d > 1e-4f)
                    {
                        float nx = dx / d, nz = dz / d;
                        o.X = (cx + nx * r) * s; o.Z = cz + nz * r;
                        float vn = o.Vx * s * nx + o.Vz * nz;
                        if (vn < 0) { o.Vx -= vn * nx * s; o.Vz -= vn * nz; }
                    }
                    else o.X = (x0 - r) * s;
                }
            }
        }

        void CollidePlayers()
        {
            var ps = Players;
            for (int i = 0; i < ps.Count; i++)
                for (int j = i + 1; j < ps.Count; j++)
                {
                    var a = ps[i]; var b = ps[j];
                    float dx = b.X - a.X, dz = b.Z - a.Z;
                    float d = Hyp(dx, dz);
                    float min = PlayerR * 2;
                    if (d >= min || d < 1e-5f) continue;
                    float nx = dx / d, nz = dz / d;
                    float ma = a.Goalie ? 6 : 1, mb = b.Goalie ? 6 : 1;
                    float pen = min - d;
                    a.X -= nx * pen * mb / (ma + mb); a.Z -= nz * pen * mb / (ma + mb);
                    b.X += nx * pen * ma / (ma + mb); b.Z += nz * pen * ma / (ma + mb);
                    float rv = (b.Vx - a.Vx) * nx + (b.Vz - a.Vz) * nz;
                    if (rv < 0)
                    {
                        float imp = (-1.3f * rv) / (1 / ma + 1 / mb);
                        a.Vx -= imp / ma * nx; a.Vz -= imp / ma * nz;
                        b.Vx += imp / mb * nx; b.Vz += imp / mb * nz;
                    }
                    if (a.Team != b.Team && Phase == Phase.Play)
                    {
                        if (a.Checking > 0 && !b.Goalie && b.Stun <= 0) Knock(a, b, nx, nz, -rv);
                        else if (b.Checking > 0 && !a.Goalie && a.Stun <= 0) Knock(b, a, -nx, -nz, -rv);
                    }
                }
        }

        void Knock(Player hitter, Player victim, float nx, float nz, float closing)
        {
            if (closing < 1.5f) return;
            victim.Stun = 1.4f;
            victim.Vx = nx * (4 + closing * 0.5f) + hitter.Vx * 0.3f;
            victim.Vz = nz * (4 + closing * 0.5f) + hitter.Vz * 0.3f;
            victim.Charge = 0;
            hitter.Checking = 0;
            Ev(EvType.Hit, closing);
            if (Puck.Owner == victim.Idx)
            {
                Puck.Owner = -1;
                Puck.Vx = victim.Vx * 0.8f + Rnd(-2, 2); Puck.Vz = victim.Vz * 0.8f + Rnd(-2, 2);
                victim.NoPick = 0.8f;
            }
        }

        // ---------------- бросок и пас ----------------
        void Shoot(Player p, float charge)
        {
            var pk = Puck;
            p.Swing = 0.32f;
            Blade(p, out float bx, out float bz);
            bool own = pk.Owner == p.Idx;
            bool oneTimer = !own && pk.Owner < 0 && p.NoPick <= 0 && pk.Y < 0.8f && Hyp(pk.X - bx, pk.Z - bz) < 1.35f;
            if (!own && !oneTimer) { Ev(EvType.Swish); return; }

            float power = charge < 0.12f ? 0.3f : charge;
            float speed = 15 + 22 * power + (oneTimer ? 4 : 0);
            float dx = MathF.Sin(p.H), dz = MathF.Cos(p.H);
            float vy = 0.4f + 3.2f * power * Rnd(0.6f, 1);

            // автоприцел, если игрок смотрит примерно в ворота
            float gx = Rink.TeamDir[p.Team] * Rink.GoalX;
            Player keeper = null;
            foreach (var q in Players) if (q.Goalie && q.Team != p.Team) keeper = q;
            float side = keeper != null && keeper.Z > 0 ? -1 : keeper != null && keeper.Z < 0 ? 1 : (Rand() < 0.5f ? -1 : 1);
            float tz = Rnd(0.55f, 1.25f) * side;
            float tx = gx - pk.X, tzz = tz - pk.Z;
            float dist = Hyp(tx, tzz);
            float ang = MathF.Abs(Wrap(MathF.Atan2(tx, tzz) - p.H));
            bool facingGoal = Rink.TeamDir[p.Team] * (gx - pk.X) > 0.5f;
            if (facingGoal && ang < 0.65f && dist < 30)
            {
                float k = 0.9f - ang * 0.6f;
                dx = dx * (1 - k) + tx / dist * k; dz = dz * (1 - k) + tzz / dist * k;
                float l = Hyp(dx, dz); dx /= l; dz /= l;
                float th = Rnd(0.15f, 1.05f) * (0.6f + power * 0.5f);
                float t = dist / speed;
                vy = Clamp((th - 0.1f + 0.5f * G * t * t) / t, 0, 7.5f);
            }
            float err = (p.Human != null ? 0.035f : diff.ShootErr) * (0.6f + power) * (oneTimer ? 1.6f : 1);
            float a = MathF.Atan2(dx, dz) + Rnd(-err, err);
            dx = MathF.Sin(a); dz = MathF.Cos(a);

            pk.Owner = -1;
            pk.X = bx; pk.Z = bz; pk.Y = 0.08f;
            pk.Vx = dx * speed + p.Vx * 0.25f; pk.Vz = dz * speed + p.Vz * 0.25f; pk.Vy = vy;
            pk.SaveCheck.Clear();
            pk.PassTarget = -1;
            Touch(p);
            p.NoPick = 0.35f;
            Ev(EvType.Shot, power);
        }

        void DoPass(Player p)
        {
            var pk = Puck;
            if (pk.Owner != p.Idx) { Ev(EvType.Swish); p.Swing = 0.2f; return; }
            float fx = MathF.Sin(p.H), fz = MathF.Cos(p.H);
            Player best = null; float bestScore = float.MaxValue;
            foreach (var q in Players)
            {
                if (q == p || q.Team != p.Team || q.Goalie) continue;
                float dx = q.X - p.X, dz = q.Z - p.Z;
                float d = Hyp(dx, dz);
                if (d < 2) continue;
                float ang = MathF.Acos(Clamp((dx * fx + dz * fz) / d, -1, 1));
                if (ang > 1.55f) continue;
                int blocked = 0;
                foreach (var o in Players)
                {
                    if (o.Team == p.Team) continue;
                    float t = Clamp(((o.X - p.X) * dx + (o.Z - p.Z) * dz) / (d * d), 0, 1);
                    if (Hyp(p.X + dx * t - o.X, p.Z + dz * t - o.Z) < 1.2f) blocked++;
                }
                float s = ang * 7 + d * 0.12f + blocked * 4 - (q.Human != null ? 1.5f : 0);
                if (s < bestScore) { bestScore = s; best = q; }
            }
            float tx, tz, speed;
            if (best != null)
            {
                float d = Hyp(best.X - p.X, best.Z - p.Z);
                float lead = Clamp(d / 18, 0.15f, 0.6f);
                tx = best.X + best.Vx * lead; tz = best.Z + best.Vz * lead;
                speed = Clamp(9 + d * 0.75f, 11, 24);
                pk.PassTarget = best.Idx; pk.PassT = 1.2f;
            }
            else
            {
                tx = p.X + fx * 10; tz = p.Z + fz * 10; speed = 15;
                pk.PassTarget = -1;
            }
            float ddx = tx - pk.X, ddz = tz - pk.Z;
            float ll = Hyp(ddx, ddz); if (ll < 1e-4f) ll = 1;
            pk.Owner = -1;
            pk.Vx = ddx / ll * speed; pk.Vz = ddz / ll * speed; pk.Vy = 0;
            pk.Y = 0.02f;
            pk.SaveCheck.Clear();
            Touch(p);
            p.NoPick = 0.3f;
            p.Swing = 0.22f;
            Ev(EvType.Pass);
        }

        void Touch(Player p)
        {
            var lt = Puck.LastTouch;
            if (lt[0] != p.Idx) { lt[1] = lt[0]; lt[0] = p.Idx; }
        }

        // ---------------- вратарь ----------------
        void UpdateGoalie(Player g, float dt)
        {
            g.Swing = MathF.Max(0, g.Swing - dt);
            g.Save = MathF.Max(0, g.Save - dt);
            g.NoPick = MathF.Max(0, g.NoPick - dt);
            int s = -Rink.TeamDir[g.Team];
            int outDir = -s;
            float gx = s * Rink.GoalX, gz = 0;
            var pk = Puck;
            float tx, tz;
            if (s * pk.X > Rink.GoalX - 0.2f)
            {
                tx = gx + outDir * 0.55f;
                tz = (pk.Z >= 0 ? 1 : -1) * (Rink.GoalW / 2 - 0.25f);
            }
            else
            {
                float dx = pk.X - gx, dz = pk.Z - gz;
                float d = Hyp(dx, dz); if (d < 1e-4f) d = 1;
                float r = Clamp(0.55f + d * 0.05f, 0.6f, 1.5f);
                tx = gx + dx / d * r; tz = gz + dz / d * r;
                if (outDir * (tx - gx) < 0.45f) tx = gx + outDir * 0.45f;
                tz = Clamp(tz, -Rink.GoalW / 2 - 0.3f, Rink.GoalW / 2 + 0.3f);
            }
            float mx = tx - g.X, mz = tz - g.Z;
            float md = Hyp(mx, mz);
            float want = MathF.Min(4.6f, md * 7);
            float vx = md > 1e-3f ? mx / md * want : 0, vz = md > 1e-3f ? mz / md * want : 0;
            float k = MathF.Min(1, dt * 12);
            g.Vx += (vx - g.Vx) * k; g.Vz += (vz - g.Vz) * k;
            g.X += g.Vx * dt; g.Z += g.Vz * dt;
            float desired = MathF.Atan2(pk.X - g.X, pk.Z - g.Z);
            g.H = Wrap(g.H + Clamp(Wrap(desired - g.H), -6 * dt, 6 * dt));
            if (outDir * (g.X - gx) < 0.3f) g.X = gx + outDir * 0.3f;

            if (pk.Owner == g.Idx)
            {
                pk.HoldT -= dt;
                if (pk.HoldT <= 0 && Phase == Phase.Play) GoaliePass(g);
            }
        }

        void GoaliePass(Player g)
        {
            var pk = Puck;
            Player best = null; float bd = float.MaxValue;
            foreach (var q in Players)
            {
                if (q.Team != g.Team || q.Goalie) continue;
                float danger = 0;
                foreach (var o in Players) if (o.Team != g.Team && Hyp(o.X - q.X, o.Z - q.Z) < 3) danger += 6;
                float d = Hyp(q.X - g.X, q.Z - g.Z) + danger;
                if (d < bd) { bd = d; best = q; }
            }
            int outDir = Rink.TeamDir[g.Team];
            float tx = g.X + outDir * 10, tz = g.Z + 6;
            if (best != null) { tx = best.X + best.Vx * 0.3f; tz = best.Z + best.Vz * 0.3f; pk.PassTarget = best.Idx; pk.PassT = 1.5f; }
            float dx = tx - g.X, dz = tz - g.Z, l = Hyp(dx, dz); if (l < 1e-4f) l = 1;
            pk.Owner = -1;
            pk.X = g.X + outDir * 0.7f; pk.Z = g.Z; pk.Y = 0.02f;
            float sp = Clamp(9 + l * 0.6f, 11, 20);
            pk.Vx = dx / l * sp; pk.Vz = dz / l * sp; pk.Vy = 0;
            pk.SaveCheck.Clear();
            g.NoPick = 0.6f;
            g.Swing = 0.25f;
            Touch(g);
            Ev(EvType.Pass);
        }

        // ---------------- шайба ----------------
        void UpdatePuck(float dt)
        {
            var pk = Puck;
            pk.PassT = MathF.Max(0, pk.PassT - dt);
            if (pk.PassT <= 0) pk.PassTarget = -1;

            if (pk.InNet)
            {
                pk.Vx *= 1 - 4 * dt; pk.Vz *= 1 - 4 * dt;
                pk.Vy -= G * dt;
                pk.X += pk.Vx * dt; pk.Z += pk.Vz * dt; pk.Y += pk.Vy * dt;
                if (pk.Y < 0) { pk.Y = 0; pk.Vy = 0; }
                float sgn = pk.X >= 0 ? 1 : -1;
                float ax = Clamp(MathF.Abs(pk.X), Rink.GoalX + 0.1f, Rink.GoalX + Rink.GoalD - 0.15f);
                pk.X = sgn * ax; pk.Z = Clamp(pk.Z, -Rink.GoalW / 2 + 0.15f, Rink.GoalW / 2 - 0.15f);
                return;
            }

            if (pk.Owner >= 0)
            {
                var o = Players[pk.Owner];
                if (o.Goalie)
                {
                    int outDir = Rink.TeamDir[o.Team];
                    pk.X = o.X + outDir * 0.35f; pk.Z = o.Z + 0.25f; pk.Y = 0.9f; pk.Vx = pk.Vz = pk.Vy = 0;
                    return;
                }
                Blade(o, out float bx, out float bz);
                float wig = MathF.Sin(SimTime * 9 + o.Idx) * 0.12f;
                float fx = MathF.Sin(o.H), fz = MathF.Cos(o.H);
                float nx = bx - fz * wig, nz = bz + fx * wig;
                if (Rink.Clamp(nx, nz, PuckR, out var c)) { nx = c.X; nz = c.Z; }
                pk.X = nx; pk.Z = nz; pk.Y = 0; pk.Vx = o.Vx; pk.Vz = o.Vz; pk.Vy = 0;
                if (Phase == Phase.Play)
                {
                    foreach (var q in Players)
                    {
                        if (q.Team == o.Team || q.Goalie || q.Stun > 0 || q.NoPick > 0) continue;
                        Blade(q, out float qx, out float qz);
                        if (Hyp(qx - pk.X, qz - pk.Z) < 0.75f || Hyp(q.X - pk.X, q.Z - pk.Z) < 0.55f)
                        {
                            float rate = q.Human != null ? 2.6f : diff.Steal;
                            float resist = o.Human != null ? 1 : 1.15f;
                            if (Rand() < rate / resist * dt)
                            {
                                pk.Owner = q.Idx; o.NoPick = 0.7f; Touch(q); Ev(EvType.Steal);
                                break;
                            }
                        }
                    }
                }
                return;
            }

            // свободная шайба: 3 подшага против «туннелирования»
            const int N = 3;
            float h = dt / N;
            for (int k = 0; k < N; k++)
            {
                float ox = pk.X, oz = pk.Z;
                pk.Vy -= G * h;
                pk.X += pk.Vx * h; pk.Z += pk.Vz * h; pk.Y += pk.Vy * h;
                if (pk.Y <= 0) { pk.Y = 0; pk.Vy = pk.Vy < -2 ? -pk.Vy * 0.3f : 0; }
                if (pk.Y == 0) { float f = 1 - 0.12f * h; pk.Vx *= f; pk.Vz *= f; }
                else { pk.Vx *= 1 - 0.03f * h; pk.Vz *= 1 - 0.03f * h; }
                if (Rink.Clamp(pk.X, pk.Z, PuckR, out var c))
                {
                    pk.X = c.X; pk.Z = c.Z;
                    float vn = pk.Vx * c.Nx + pk.Vz * c.Nz;
                    if (vn < 0)
                    {
                        pk.Vx -= vn * c.Nx * 1.65f; pk.Vz -= vn * c.Nz * 1.65f;
                        pk.Vx *= 0.9f; pk.Vz *= 0.9f;
                        if (vn < -3) Ev(EvType.BoardPuck, -vn);
                    }
                }
                if (Phase == Phase.Play && CheckGoal(ox, oz)) return;
                PuckGoalFrame();
                if (Phase != Phase.Play) continue;
                if (GoalieSaves()) return;
                if (Pickup()) return;
                PuckBodies();
            }
        }

        bool CheckGoal(float ox, float oz)
        {
            var pk = Puck;
            foreach (int s in new[] { 1, -1 })
            {
                float a0 = ox * s, a1 = pk.X * s;
                if (a0 < Rink.GoalX && a1 >= Rink.GoalX)
                {
                    float den = a1 - a0; if (MathF.Abs(den) < 1e-6f) den = 1;
                    float t = (Rink.GoalX - a0) / den;
                    float zc = oz + (pk.Z - oz) * t;
                    if (MathF.Abs(zc) < Rink.GoalW / 2 - 0.1f && pk.Y < Rink.GoalH - 0.08f)
                    {
                        int team = s > 0 ? 0 : 1;
                        Score[team]++;
                        pk.InNet = true;
                        pk.Vx *= 0.35f; pk.Vz *= 0.35f;
                        var lt = pk.LastTouch;
                        Player scorer = lt[0] >= 0 ? Players[lt[0]] : null;
                        Player assist = lt[1] >= 0 ? Players[lt[1]] : null;
                        var gi = new GoalInfo { Team = team };
                        if (scorer == null || scorer.Team != team) gi.Own = true;
                        else
                        {
                            scorer.Goals++;
                            gi.Scorer = scorer.Idx;
                            if (assist != null && assist.Team == team && assist != scorer && !assist.Goalie) { assist.Assists++; gi.Assist = assist.Idx; }
                        }
                        LastGoal = gi;
                        Phase = Phase.Goal;
                        PhaseT = 3.6f;
                        Ev(EvType.Goal, team);
                        return true;
                    }
                }
            }
            return false;
        }

        void PuckGoalFrame()
        {
            var pk = Puck;
            if (pk.Y > Rink.GoalH + 0.1f) return;
            foreach (int s in new[] { 1, -1 })
            {
                float ax = pk.X * s;
                float x0 = Rink.GoalX - 0.06f, x1 = Rink.GoalX + Rink.GoalD, hz = Rink.GoalW / 2 + 0.06f;
                // открытый створ: шайба летит к линии ворот, гол считает CheckGoal
                if (ax < Rink.GoalX && MathF.Abs(pk.Z) < Rink.GoalW / 2 - 0.1f && pk.Y < Rink.GoalH - 0.08f) continue;
                if (ax > x0 - PuckR && ax < x1 + PuckR && MathF.Abs(pk.Z) < hz + PuckR)
                {
                    float best = ax - (x0 - PuckR); float fnx = -1, fnz = 0;
                    float p2 = x1 + PuckR - ax; if (p2 < best) { best = p2; fnx = 1; fnz = 0; }
                    float p3 = hz + PuckR - pk.Z; if (p3 < best) { best = p3; fnx = 0; fnz = 1; }
                    float p4 = pk.Z + hz + PuckR; if (p4 < best) { best = p4; fnx = 0; fnz = -1; }
                    pk.X += fnx * best * s; pk.Z += fnz * best;
                    float vn = pk.Vx * fnx * s + pk.Vz * fnz;
                    if (vn < 0)
                    {
                        pk.Vx -= vn * fnx * s * 1.6f; pk.Vz -= vn * fnz * 1.6f;
                        if (vn < -4) Ev(fnx == -1 ? EvType.Post : EvType.BoardPuck, -vn);
                    }
                }
            }
        }

        bool GoalieSaves()
        {
            var pk = Puck;
            float spd = Hyp(pk.Vx, pk.Vz);
            foreach (var g in Players)
            {
                if (!g.Goalie) continue;
                int s = -Rink.TeamDir[g.Team];
                float dx = pk.X - g.X, dz = pk.Z - g.Z;
                float d = Hyp(dx, dz);
                bool failedThisShot = pk.SaveCheck.TryGetValue(g.Idx, out bool prevOk) && !prevOk;
                if (d < 1.0f && spd < 7 && pk.Y < 0.6f && g.NoPick <= 0 && !failedThisShot)
                {
                    GoalieHold(g, 1.1f);
                    return true;
                }
                bool towards = pk.Vx * s > 2;
                if (!towards || d > 1.15f || pk.Y > 1.55f) continue;
                if (pk.SaveCheck.ContainsKey(g.Idx)) continue;
                float lateral = MathF.Abs(dz);
                float prob = diff.SaveP + 0.1f - (spd - 14) / 70 - lateral * 0.22f - MathF.Max(0, pk.Y - 0.9f) * 0.3f;
                int lt = pk.LastTouch[0];
                if (lt >= 0 && Players[lt].Team == g.Team) prob = 1;
                prob = Clamp(prob, 0.18f, 0.95f);
                bool ok = Rand() < prob;
                pk.SaveCheck[g.Idx] = ok;
                if (!ok) continue;
                g.Save = 0.6f;
                Ev(EvType.Save);
                if (spd < 24 && Rand() < 0.55f) GoalieHold(g, 1.3f);
                else
                {
                    int outDir = -s;
                    float a = Rnd(-1.1f, 1.1f);
                    float sp = spd * Rnd(0.3f, 0.5f);
                    pk.Vx = outDir * MathF.Cos(a) * sp; pk.Vz = MathF.Sin(a) * sp + (dz > 0 ? 2 : -2); pk.Vy = Rnd(0.5f, 3);
                    pk.X = g.X + outDir * 0.6f; pk.Z = g.Z + dz;
                    Touch(g);
                }
                return true;
            }
            return false;
        }

        void GoalieHold(Player g, float t)
        {
            var pk = Puck;
            pk.Owner = g.Idx; pk.HoldT = t;
            pk.Vx = pk.Vz = pk.Vy = 0;
            Touch(g);
            g.Save = MathF.Max(g.Save, 0.4f);
        }

        bool Pickup()
        {
            var pk = Puck;
            if (pk.Y > 0.65f) return false;
            float spd = Hyp(pk.Vx, pk.Vz);
            Player best = null; float bd = float.MaxValue;
            foreach (var p in Players)
            {
                if (p.Goalie || p.Stun > 0 || p.NoPick > 0) continue;
                Blade(p, out float bx, out float bz);
                float d = MathF.Min(Hyp(bx - pk.X, bz - pk.Z), Hyp(p.X - pk.X, p.Z - pk.Z) + 0.25f);
                float r = pk.PassTarget == p.Idx ? 1.25f : 0.8f;
                if (d < r && d < bd) { bd = d; best = p; }
            }
            if (best == null) return false;
            float rel = Hyp(pk.Vx - best.Vx, pk.Vz - best.Vz);
            if (rel > 27 && pk.PassTarget != best.Idx)
            {
                // слишком сильный бросок — рикошет от клюшки
                pk.Vx = pk.Vx * 0.35f + Rnd(-3, 3); pk.Vz = pk.Vz * 0.35f + Rnd(-3, 3); pk.Vy = Rnd(0, 2);
                best.NoPick = 0.25f;
                Touch(best);
                Ev(EvType.Deflect);
                return true;
            }
            pk.Owner = best.Idx;
            pk.PassTarget = -1;
            Touch(best);
            if (spd > 6) Ev(EvType.Receive);
            return true;
        }

        void PuckBodies()
        {
            var pk = Puck;
            if (pk.Y > 1.5f) return;
            foreach (var p in Players)
            {
                float dx = pk.X - p.X, dz = pk.Z - p.Z;
                float d = Hyp(dx, dz);
                float r = p.Goalie ? 0.5f : 0.4f;
                if (d < r && d > 1e-4f)
                {
                    float nx = dx / d, nz = dz / d;
                    float vn = (pk.Vx - p.Vx) * nx + (pk.Vz - p.Vz) * nz;
                    pk.X = p.X + nx * r; pk.Z = p.Z + nz * r;
                    if (vn < 0) { pk.Vx -= vn * nx * 1.4f; pk.Vz -= vn * nz * 1.4f; if (vn < -6) Ev(EvType.Body); }
                }
            }
        }

        // ---------------- боты ----------------
        void UpdateRoles()
        {
            var pk = Puck;
            carrier = pk.Owner >= 0 ? Players[pk.Owner] : null;
            for (int t = 0; t < 2; t++)
            {
                int best = -1; float bd = float.MaxValue;
                float tx = pk.X + pk.Vx * 0.3f, tz = pk.Z + pk.Vz * 0.3f;
                if (carrier == null || carrier.Team != t)
                    foreach (var p in Players)
                    {
                        if (p.Team != t || p.Goalie || p.Human != null || p.Stun > 0) continue;
                        float d = Hyp(p.X - tx, p.Z - tz);
                        if (d < bd) { bd = d; best = p.Idx; }
                    }
                chaser[t] = best;
                int lane = 0;
                foreach (var p in Players) if (p.Team == t && !p.Goalie && p.Human == null && p.Idx != best) p.Lane = lane++;
            }
        }

        PlayerInput IdleBotInput(Player p)
        {
            if (Phase == Phase.Goal && LastGoal.Team == p.Team) return new PlayerInput { Throttle = 0.5f, Turn = 0.6f };
            return PlayerInput.None;
        }

        PlayerInput Steer(Player p, float tx, float tz, float arrive, float speedMul, out float d, out float diffAng)
        {
            float dx = tx - p.X, dz = tz - p.Z;
            d = Hyp(dx, dz);
            float desired = MathF.Atan2(dx, dz);
            diffAng = Wrap(desired - p.H);
            var inp = new PlayerInput { Turn = Clamp(diffAng * 3.2f, -1, 1) };
            float spd = Hyp(p.Vx, p.Vz);
            if (d < arrive) inp.Throttle = spd > 1.5f ? -1 : 0;
            else
            {
                inp.Throttle = MathF.Abs(diffAng) < 1.3f ? speedMul : 0.15f;
                if (MathF.Abs(diffAng) > 2.2f && spd > 5) inp.Throttle = -1;
            }
            return inp;
        }
        PlayerInput Steer(Player p, float tx, float tz, float arrive = 1.5f, float speedMul = 1) => Steer(p, tx, tz, arrive, speedMul, out _, out _);

        PlayerInput BotInput(Player p, float dt)
        {
            var pk = Puck;
            int dir = Rink.TeamDir[p.Team];
            float oppGoalX = dir * Rink.GoalX, ownGoalX = -dir * Rink.GoalX;
            var b = p.Bot;

            if (b.ShootT >= 0)
            {
                b.ShootT += dt;
                var si = Steer(p, oppGoalX, 0, 0);
                if (pk.Owner != p.Idx && b.ShootT > 0.1f) { b.ShootT = -1; si.Shoot = false; return si; }
                si.Shoot = b.ShootT < b.ChargeFor;
                if (!si.Shoot) b.ShootT = -1;
                si.Throttle = 0.3f;
                return si;
            }

            if (pk.Owner == p.Idx)
            {
                float gdx = oppGoalX - p.X, gdz = -p.Z;
                float gd = Hyp(gdx, gdz);
                float gang = MathF.Abs(Wrap(MathF.Atan2(gdx, gdz) - p.H));
                int pressure = 0; Player nearOpp = null; float nd = float.MaxValue;
                foreach (var o in Players)
                {
                    if (o.Team == p.Team || o.Goalie) continue;
                    float d = Hyp(o.X - p.X, o.Z - p.Z);
                    if (d < nd) { nd = d; nearOpp = o; }
                    if (d < 3.2f) pressure++;
                }
                bool inZone = dir * p.X > Rink.GoalX - 18 && dir * p.X < Rink.GoalX - 1.5f;
                if (inZone && gd < 17 && gang < 0.45f && Rand() < (2.2f + (17 - gd) * 0.25f) * dt * diff.React)
                {
                    b.ShootT = 0; b.ChargeFor = Rnd(0.15f, 0.85f);
                    var si = Steer(p, oppGoalX, 0, 0); si.Shoot = true; return si;
                }
                if (pressure > 0 && Rand() < 1.4f * dt * diff.React)
                {
                    var pi = Steer(p, oppGoalX, 0); pi.Pass = true; return pi;
                }
                float tx = oppGoalX - dir * 7, tz = Clamp(p.Z * 0.5f, -4, 4);
                if (dir * p.X > Rink.GoalX - 10) { tx = oppGoalX - dir * 3; tz = 0; }
                if (nearOpp != null && nd < 5 && (nearOpp.X - p.X) * dir > -0.5f) tz += (p.Z > nearOpp.Z ? 1 : -1) * 4.5f;
                if (MathF.Abs(p.Z) > 11) tz = 0;
                var r = Steer(p, tx, tz, 0);
                r.Sprint = nd > 4 && p.Stamina > 0.4f && dir * p.X < Rink.GoalX - 15;
                return r;
            }

            if (carrier != null && carrier.Team == p.Team)
            {
                float[] lanes = { -7, 7, 0, -11, 11, -3, 3 };
                float lz = lanes[p.Lane % lanes.Length];
                float tx = Clamp(carrier.X + dir * Rnd(5, 7), -Rink.GoalX + 5, Rink.GoalX - 5);
                if (carrier.Goalie) tx = carrier.X + dir * 9;
                var r = Steer(p, tx, lz, 2, 0.95f, out float d, out _);
                r.Sprint = d > 8 && p.Stamina > 0.6f;
                return r;
            }

            if (chaser[p.Team] == p.Idx)
            {
                if (carrier != null && carrier.Goalie)
                    return Steer(p, carrier.X + Rink.TeamDir[carrier.Team] * 6, carrier.Z, 2, 0.7f);
                float lead = carrier != null ? 0.35f : Clamp(Hyp(pk.X - p.X, pk.Z - p.Z) / 14, 0.1f, 0.6f);
                float tx = pk.X + pk.Vx * lead, tz = pk.Z + pk.Vz * lead;
                var r = Steer(p, tx, tz, 0, 1, out float d, out float da);
                if (carrier != null && d < 2.6f && MathF.Abs(da) < 0.4f && p.CheckCd <= 0 && Rand() < diff.CheckP * dt * 2) r.Check = true;
                r.Sprint = d > 5 && p.Stamina > 0.3f;
                if (carrier == null && pk.Owner < 0 && d < 1.6f && Rand() < 0.05f && dir * (oppGoalX - pk.X) > 0
                    && MathF.Abs(Wrap(MathF.Atan2(oppGoalX - p.X, -p.Z) - p.H)) < 0.4f && Hyp(oppGoalX - p.X, p.Z) < 14)
                {
                    b.ShootT = 0; b.ChargeFor = 0.05f; r.Shoot = true; // бросок в одно касание
                }
                return r;
            }

            {
                float[] lanes = { -6, 6, 0, -10, 10 };
                float lz = lanes[p.Lane % lanes.Length];
                float tx, tz;
                if (carrier != null)
                {
                    tx = ownGoalX + (carrier.X - ownGoalX) * 0.55f;
                    tz = carrier.Z * 0.45f + lz * 0.6f;
                }
                else
                {
                    tx = Clamp(pk.X - dir * 6, -Rink.GoalX + 4, Rink.GoalX - 4);
                    tz = lz;
                }
                return Steer(p, tx, tz, 1.8f, 0.9f);
            }
        }

        // ---------------- состояние для отрисовки ----------------
        public void FillView(ViewState v)
        {
            v.Resize(Players.Count);
            for (int i = 0; i < Players.Count; i++)
            {
                var p = Players[i]; var o = v.Players[i];
                o.X = p.X; o.Z = p.Z; o.H = p.H; o.Spd = Hyp(p.Vx, p.Vz); o.Charge = p.Charge;
                o.Stun = p.Stun > 0; o.Swing = p.Swing; o.Save = p.Save; o.Stamina = p.Stamina;
                o.Back = p.Vx * MathF.Sin(p.H) + p.Vz * MathF.Cos(p.H) < -0.5f;
                o.Goals = p.Goals; o.Assists = p.Assists;
            }
            v.PuckX = Puck.X; v.PuckY = Puck.Y; v.PuckZ = Puck.Z; v.PuckOwner = Puck.Owner;
            v.Score0 = Score[0]; v.Score1 = Score[1];
            v.Time = Time; v.Phase = Phase; v.PhaseT = PhaseT; v.Overtime = Overtime; v.SimTime = SimTime;
            v.LastGoal = LastGoal;
        }
    }
}
