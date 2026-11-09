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

    /// Ввод игрока. MoveX/MoveZ — желаемое направление движения в мировых координатах (длина 0..1),
    /// его считает клиент из стика/клавиш с учётом поворота камеры.
    public struct PlayerInput
    {
        public float MoveX, MoveZ;
        public bool Shoot, Pass, Check, Sprint, Jump, Fight;
        /// Куда смотрит камера (курс в радианах): бросок и пас идут в прицел по центру экрана.
        public float AimYaw; public bool HasAim;
        public static readonly PlayerInput None = default;

        public byte Bits => (byte)((Shoot ? 1 : 0) | (Pass ? 2 : 0) | (Check ? 4 : 0) | (Sprint ? 8 : 0) | (Jump ? 16 : 0) | (Fight ? 32 : 0) | (HasAim ? 64 : 0));
        public static PlayerInput FromBits(float mx, float mz, byte bits, float aim = 0)
        {
            if (float.IsNaN(aim) || float.IsInfinity(aim)) aim = 0;
            float l = MathF.Sqrt(mx * mx + mz * mz);
            if (float.IsNaN(l)) { mx = mz = 0; l = 0; }
            if (l > 1) { mx /= l; mz /= l; }
            return new PlayerInput
            {
                MoveX = mx, MoveZ = mz,
                Shoot = (bits & 1) != 0, Pass = (bits & 2) != 0, Check = (bits & 4) != 0, Sprint = (bits & 8) != 0, Jump = (bits & 16) != 0, Fight = (bits & 32) != 0,
                HasAim = (bits & 64) != 0, AimYaw = aim,
            };
        }
        public PlayerInput Scaled(float k) { var c = this; c.MoveX *= k; c.MoveZ *= k; return c; }
    }

    public enum Buff { Turbo = 0, Mega = 1, Giant = 2, Magnet = 3, Frozen = 4 }

    public class Item
    {
        public float X, Z, Life;
        public int Type;
    }

    public enum EvType : byte { Shot, Pass, Receive, Swish, Whoosh, Board, BoardPuck, Post, Hit, Body, Save, Deflect, Steal, Whistle, Goal, Horn, Overtime, End,
        Jump, Dive, Land, Stop, PowerUp, Freeze, MegaShot, ItemSpawn, Punch, BrawlStart, BrawlEnd, HelmetOff, Knockout }

    public struct GameEvent
    {
        public EvType Type;
        public float Value;
        public int Who;   // индекс игрока, к которому относится событие (-1 — нет)
        public GameEvent(EvType t, float v = 0, int who = -1) { Type = t; Value = v; Who = who; }
    }

    public enum Phase : byte { Faceoff, Play, Goal, End }

    public class HumanSlot
    {
        public string Key, Name;
        public int Team, Number = -1, Skin = -1, Hair = -1;   // -1 — случайно
        public HumanSlot(string key, string name, int team, int number = -1, int skin = -1, int hair = -1)
        { Key = key; Name = name; Team = team; Number = number; Skin = skin; Hair = hair; }
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
        public bool Arcade;   // бонусы на льду
    }

    public class GoalInfo
    {
        public int Team = -1, Scorer = -1, Assist = -1;
        public bool Own;
    }

    /// Всё, что нужно для отрисовки кадра. Одинаково у хоста и у гостей.
    public class PlayerView
    {
        public float X, Z, H, Spd, Charge, Swing, Save, Stamina, Y;
        public bool Stun, Back, Dive, Punch, Brawl, Stagger, Helmet = true;
        public int BrawlPower;
        public byte Buffs;          // биты Buff
        public int Goals, Assists;
        public bool Has(Buff b) => (Buffs & (1 << (int)b)) != 0;
    }

    public struct ItemView { public float X, Z; public int Type; }

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
        public bool PuckMega;
        public List<ItemView> Items = new List<ItemView>();

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
        public float Y, Vy, Dive, Recover, StopFx;
        public float Punch, PunchCd, Stagger, Brawl, BrawlX, BrawlZ, LastHitT = -9;
        public int BrawlWith = -1, BrawlPower, LastHitBy = -1;
        public bool Helmet = true;
        public float HeadCd;
        public readonly float[] Buffs = new float[5];
        public bool Has(Buff b) => Buffs[(int)b] > 0;
        public float Radius => Has(Buff.Giant) ? 0.62f : 0.45f;
        public PlayerInput Prev;
        public float AimH; public bool HasAim;
        /// Направление броска/паса: у человека — по прицелу камеры, у ботов — куда смотрит игрок.
        public float ShotH => HasAim && Human != null ? AimH : H;
        internal BotState Bot = new BotState();
    }

    public class Puck
    {
        public float X, Y, Z, Vx, Vy, Vz, HoldT, PassT;
        public int Owner = -1, PassTarget = -1;
        public int[] LastTouch = { -1, -1 };
        public Dictionary<int, bool> SaveCheck = new Dictionary<int, bool>();
        public bool InNet, Mega;
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

        const float PuckR = 0.12f, G = 9.8f;

        public readonly MatchConfig Cfg;
        public readonly List<Player> Players = new List<Player>();
        public readonly Puck Puck = new Puck();
        public readonly int[] Score = new int[2];
        public float Time, SimTime, PhaseT;
        public bool Overtime;
        public Phase Phase = Phase.Faceoff;
        public GoalInfo LastGoal = new GoalInfo();
        public readonly List<GameEvent> Events = new List<GameEvent>();
        public readonly List<Item> Items = new List<Item>();
        float itemTimer = 6;

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
                    int num = h != null && h.Number > 0 && !used[t].Contains(h.Number) ? h.Number : Num(t);
                    used[t].Add(num);
                    Add(t, h?.Key, h != null ? h.Name : NextBotName(), num, false);
                    if (h != null && h.Skin >= 0) Players[Players.Count - 1].Skin = h.Skin;
                    if (h != null && h.Hair >= 0) Players[Players.Count - 1].Hair = h.Hair;
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

        void Ev(EvType t, float v = 0, int who = -1) => Events.Add(new GameEvent(t, v, who));

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
                    p.Y = p.Vy = p.Dive = p.Recover = 0; p.Buffs[(int)Buff.Frozen] = 0;
                    p.Punch = p.Stagger = p.Brawl = 0; p.BrawlWith = -1;
                    p.Helmet = true; p.HeadCd = 0;
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
            pk.Owner = -1; pk.PassTarget = -1; pk.SaveCheck.Clear(); pk.InNet = false; pk.Mega = false;
            Items.Clear();
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
            UpdateItems(dt);
        }

        // ---------------- бонусы (режим «Аркада») ----------------
        void UpdateItems(float dt)
        {
            if (!Cfg.Arcade || Phase != Phase.Play) return;
            itemTimer -= dt;
            if (itemTimer <= 0)
            {
                itemTimer = Rnd(6, 11);
                if (Items.Count < 3)
                {
                    float r = Rand();
                    int type = r < 0.26f ? 0 : r < 0.48f ? 1 : r < 0.68f ? 2 : r < 0.86f ? 3 : 4;
                    Items.Add(new Item { X = Rnd(-20, 20), Z = Rnd(-11, 11), Type = type, Life = 16 });
                    Ev(EvType.ItemSpawn);
                }
            }
            for (int i = Items.Count - 1; i >= 0; i--)
            {
                var it = Items[i];
                it.Life -= dt;
                if (it.Life <= 0) { Items.RemoveAt(i); continue; }
                foreach (var p in Players)
                {
                    if (p.Goalie || p.Y > 1 || p.Stun > 0) continue;
                    if (Hyp(p.X - it.X, p.Z - it.Z) > 1.15f) continue;
                    ApplyItem(p, it.Type);
                    Items.RemoveAt(i);
                    break;
                }
            }
        }

        void ApplyItem(Player p, int type)
        {
            switch ((Buff)type)
            {
                case Buff.Turbo: p.Buffs[0] = 6; p.Stamina = 1; break;
                case Buff.Mega: p.Buffs[1] = 15; break;
                case Buff.Giant: p.Buffs[2] = 8; break;
                case Buff.Magnet: p.Buffs[3] = 8; break;
                case Buff.Frozen:
                    foreach (var q in Players) if (q.Team != p.Team) { q.Buffs[4] = 2.6f; q.Charge = 0; }
                    Ev(EvType.Freeze, p.Team);
                    break;
            }
            Ev(EvType.PowerUp, type, p.Idx);
        }

        // ---------------- полевые игроки ----------------
        public static void Blade(Player p, out float bx, out float bz)
        {
            float fx = MathF.Sin(p.H), fz = MathF.Cos(p.H);
            float rx = fz, rz = -fx;   // правая сторона игрока (клюшка в правой руке)
            bx = p.X + fx * 0.95f + rx * 0.28f;
            bz = p.Z + fz * 0.95f + rz * 0.28f;
        }

        void UpdateSkater(Player p, PlayerInput inp, float dt, bool live)
        {
            p.CheckCd = MathF.Max(0, p.CheckCd - dt);
            p.NoPick = MathF.Max(0, p.NoPick - dt);
            p.Swing = MathF.Max(0, p.Swing - dt);
            p.Checking = MathF.Max(0, p.Checking - dt);
            p.Recover = MathF.Max(0, p.Recover - dt);
            p.StopFx = MathF.Max(0, p.StopFx - dt);
            for (int b = 0; b < p.Buffs.Length; b++) p.Buffs[b] = MathF.Max(0, p.Buffs[b] - dt);
            bool frozen = p.Has(Buff.Frozen), turbo = p.Has(Buff.Turbo);
            if (p.Stun > 0)
            {
                p.Stun -= dt; inp = PlayerInput.None; p.Charge = 0;
                if (p.Stun <= 0) p.Recover = 0.25f;
            }
            p.Punch = MathF.Max(0, p.Punch - dt);
            p.HeadCd = MathF.Max(0, p.HeadCd - dt);
            p.PunchCd = MathF.Max(0, p.PunchCd - dt);
            p.Stagger = MathF.Max(0, p.Stagger - dt);
            if (p.Brawl > 0) { UpdateBrawl(p, inp, dt); p.Prev = inp; return; }
            if (p.Stagger > 0) { inp.MoveX *= 0.2f; inp.MoveZ *= 0.2f; inp.Shoot = inp.Pass = inp.Jump = inp.Fight = false; }
            if (live && inp.Fight && !p.Prev.Fight) TryPunch(p);
            bool diving = p.Dive > 0;
            if (diving)
            {
                p.Dive -= dt;
                if (p.Dive <= 0) p.Recover = 0.3f;
                inp = PlayerInput.None;
            }
            if (p.Recover > 0) { inp.MoveX = inp.MoveZ = 0; inp.Jump = false; inp.Check = false; }
            bool air = p.Y > 0.02f || p.Vy > 0;
            float spd = Hyp(p.Vx, p.Vz);

            bool hasPuck = Puck.Owner == p.Idx;
            float botMul = p.Human != null ? 1 : diff.Speed;
            float mx = inp.MoveX, mz = inp.MoveZ;
            float mag = MathF.Min(1, Hyp(mx, mz));
            float control = (air ? 0.35f : 1) * (frozen ? 0.45f : 1);
            bool sprinting = inp.Sprint && (p.Stamina > 0.05f || turbo) && mag > 0.2f;
            if (sprinting && !turbo) p.Stamina = MathF.Max(0, p.Stamina - dt * 0.33f);
            else if (!sprinting) p.Stamina = MathF.Min(1, p.Stamina + dt * 0.18f);
            float maxS = (sprinting ? 10.8f : 8.2f) * botMul * (hasPuck ? 0.93f : 1) * (turbo ? 1.3f : 1) * (frozen ? 0.4f : 1);
            float accel = (sprinting ? 13.5f : 11f) * (turbo ? 1.3f : 1);

            // поворот к направлению стика: чем медленнее едешь, тем быстрее разворот
            float diffAng = 0;
            if (mag > 0.08f && !diving)
            {
                float desired = MathF.Atan2(mx, mz);
                diffAng = Wrap(desired - p.H);
                float turnRate = Clamp(9f - spd * 0.55f, 3.2f, 9f) * control;
                p.H = Wrap(p.H + Clamp(diffAng, -turnRate * dt, turnRate * dt));
            }

            // сцепление коньков: боковая скорость частично переходит в продольную
            float nfx = MathF.Sin(p.H), nfz = MathF.Cos(p.H);
            float fwd = p.Vx * nfx + p.Vz * nfz;
            float lx = p.Vx - fwd * nfx, lz = p.Vz - fwd * nfz;
            float lat = Hyp(lx, lz);
            float gripK = diving ? 0.6f : p.Stun > 0 ? 0.5f : air ? 0.3f : 5.5f;
            float grip = 1 - MathF.Exp(-gripK * dt);
            fwd += (fwd >= 0 ? 1 : -1) * lat * grip * 0.75f;
            lx *= 1 - grip; lz *= 1 - grip;

            if (mag > 0.08f && !diving)
            {
                float align = MathF.Max(0, MathF.Cos(diffAng));
                float target = maxS * mag;
                if (fwd < target) fwd = MathF.Min(target, fwd + accel * align * mag * control * dt);
                // хоккейная остановка: стик против движения
                float vdot = (p.Vx * mx + p.Vz * mz) / MathF.Max(1e-3f, mag);
                if (spd > 3 && vdot < -0.35f * spd && !air)
                {
                    float k = MathF.Min(1, 3.2f * dt);
                    fwd *= 1 - k; lx *= 1 - k; lz *= 1 - k;
                    if (p.StopFx <= 0) { p.StopFx = 0.25f; Ev(EvType.Stop, spd, p.Idx); }
                }
            }
            p.Vx = fwd * nfx + lx;
            p.Vz = fwd * nfz + lz;
            float fr = diving ? 0.9f : mag > 0.08f ? 0.2f : 0.55f;
            p.Vx *= 1 - fr * dt; p.Vz *= 1 - fr * dt;
            spd = Hyp(p.Vx, p.Vz);
            float cap = maxS * 1.4f + (diving ? 3 : 0);
            if (spd > cap) { p.Vx *= cap / spd; p.Vz *= cap / spd; }

            if (live)
            {
                // пробел: прыжок на месте или нырок в движении (за шайбой)
                if (inp.Jump && !p.Prev.Jump && !air && !diving && p.Stun <= 0 && p.Recover <= 0 && !frozen)
                {
                    if (spd > 4.5f && !hasPuck)
                    {
                        p.Dive = 0.8f; p.Vy = 2.6f; p.Charge = 0;
                        p.Vx += nfx * 4.5f; p.Vz += nfz * 4.5f;
                        Ev(EvType.Dive, 0, p.Idx);
                    }
                    else { p.Vy = 5.6f; Ev(EvType.Jump, 0, p.Idx); }
                    air = true;
                }
                if (inp.Check && !p.Prev.Check && p.CheckCd <= 0 && p.Stun <= 0 && !air && !frozen)
                {
                    p.Checking = 0.35f; p.CheckCd = 1.3f;
                    p.Vx += nfx * 5; p.Vz += nfz * 5;
                    Ev(EvType.Whoosh);
                }
                if (!diving && !frozen)
                {
                    if (inp.HasAim) { p.AimH = inp.AimYaw; p.HasAim = true; }
                    if (inp.Shoot) p.Charge = MathF.Min(1, p.Charge + dt / 0.9f);
                    else if (p.Prev.Shoot && p.Stun <= 0) { Shoot(p, p.Charge); p.Charge = 0; }
                    else p.Charge = 0;
                    if (inp.Pass && !p.Prev.Pass && p.Stun <= 0) DoPass(p);
                }
                else p.Charge = 0;
            }
            else p.Charge = 0;

            // вертикаль: прыжок и нырок
            if (air)
            {
                p.Vy -= 20 * dt;
                p.Y += p.Vy * dt;
                if (p.Y <= 0)
                {
                    if (p.Vy < -3) Ev(EvType.Land, -p.Vy, p.Idx);
                    p.Y = 0; p.Vy = 0;
                }
            }

            p.X += p.Vx * dt; p.Z += p.Vz * dt;
            float pr = p.Radius;
            if (Rink.Clamp(p.X, p.Z, pr, out var c))
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

        // ---------------- толчки кулаком и драки ----------------
        void TryPunch(Player p)
        {
            if (p.PunchCd > 0 || p.Stun > 0 || p.Dive > 0 || p.Y > 0.1f || p.Has(Buff.Frozen) || p.Recover > 0) return;
            p.Punch = 0.35f; p.PunchCd = 0.55f; p.Charge = 0;
            float fx = MathF.Sin(p.H), fz = MathF.Cos(p.H);
            Player target = null; float best = 1.7f;
            foreach (var q in Players)
            {
                if (q.Team == p.Team || q.Goalie || q.Stun > 0 || q.Brawl > 0 || q.Y > 0.3f) continue;
                float dx = q.X - p.X, dz = q.Z - p.Z, d = Hyp(dx, dz);
                if (d < best && d > 1e-3f && (dx * fx + dz * fz) / d > 0.2f) { best = d; target = q; }
            }
            if (target == null) { Ev(EvType.Swish, 0, p.Idx); return; }
            bool retaliation = p.LastHitBy == target.Idx && SimTime - p.LastHitT < 1.6f;
            target.LastHitBy = p.Idx; target.LastHitT = SimTime;
            if (retaliation || Rand() < 0.18f) { StartBrawl(p, target); return; }
            // просто удар: отбрасывает и сбивает с ритма, может выбить шайбу
            float dxn = target.X - p.X, dzn = target.Z - p.Z, dn = MathF.Max(1e-3f, Hyp(dxn, dzn));
            float k = target.Has(Buff.Giant) ? 0.3f : p.Has(Buff.Giant) ? 1.7f : 1;
            target.Vx += dxn / dn * 4.5f * k; target.Vz += dzn / dn * 4.5f * k;
            target.Stagger = 0.45f * k; target.Charge = 0;
            if (Puck.Owner == target.Idx && Rand() < 0.6f)
            {
                Puck.Owner = -1; Puck.Vx = target.Vx * 0.6f + Rnd(-2, 2); Puck.Vz = target.Vz * 0.6f + Rnd(-2, 2);
                target.NoPick = 0.5f;
            }
            Ev(EvType.Punch, p.Idx, target.Idx);
        }

        /// Для автотеста: запустить шайбу игроку в голову.
        public void TestHeadShot(int idx)
        {
            var p = Players[idx];
            float fx = MathF.Sin(p.H), fz = MathF.Cos(p.H);
            Puck.Owner = -1; Puck.InNet = false;
            Puck.X = p.X + fx * 2.5f; Puck.Z = p.Z + fz * 2.5f; Puck.Y = p.Y + 1.5f;
            Puck.Vx = -fx * 22; Puck.Vz = -fz * 22; Puck.Vy = 1.2f;
            Puck.LastTouch[0] = -1;
            p.NoPick = 0; p.HeadCd = 0;
        }

        /// Для автотеста: принудительно начать драку.
        public void ForceBrawl(int a, int b) => StartBrawl(Players[a], Players[b]);

        void StartBrawl(Player a, Player b)
        {
            float cx = (a.X + b.X) / 2, cz = (a.Z + b.Z) / 2;
            foreach (var q in new[] { a, b })
            {
                q.Brawl = 2.4f; q.BrawlX = cx; q.BrawlZ = cz; q.BrawlPower = 0;
                q.Vx = q.Vz = 0; q.Charge = 0; q.Dive = 0; q.Punch = 0;
                if (Puck.Owner == q.Idx) { Puck.Owner = -1; Puck.Vx = Rnd(-3, 3); Puck.Vz = Rnd(-3, 3); }
                q.NoPick = 2.6f;
            }
            a.BrawlWith = b.Idx; b.BrawlWith = a.Idx;
            Ev(EvType.BrawlStart, 0, a.Idx);
        }

        void UpdateBrawl(Player p, PlayerInput inp, float dt)
        {
            p.Brawl -= dt;
            p.Vx = p.Vz = 0; p.Charge = 0;
            // клубок дерущихся слегка катается по льду
            float a = SimTime * 7 + p.Idx;
            p.X = p.BrawlX + MathF.Sin(a) * 0.25f; p.Z = p.BrawlZ + MathF.Cos(a * 1.3f) * 0.25f;
            bool mash = p.Human != null ? (inp.Fight && !p.Prev.Fight) || (inp.Check && !p.Prev.Check) : Rand() < (4.5f + 3.5f * diff.React) * dt;
            if (mash) p.BrawlPower++;
            if (p.Brawl > 0 || p.BrawlWith < 0) return;
            var o = Players[p.BrawlWith];
            float sp = p.BrawlPower + Rnd(0, 2.5f), so = o.BrawlPower + Rnd(0, 2.5f);
            var winner = sp >= so ? p : o; var loser = winner == p ? o : p;
            foreach (var q in new[] { p, o }) { q.Brawl = 0; q.BrawlWith = -1; q.NoPick = 0.3f; }
            loser.Stun = 1.8f;
            loser.Vx = (loser.X - winner.X) * 6; loser.Vz = (loser.Z - winner.Z) * 6;
            Ev(EvType.BrawlEnd, loser.Idx, winner.Idx);
        }

        void CollideGoalFrames(Player o)
        {
            float r = o.Radius;
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
                    float min = a.Radius + b.Radius;
                    if (d >= min || d < 1e-5f) continue;
                    float nx = dx / d, nz = dz / d;
                    float ma = a.Goalie ? 6 : a.Has(Buff.Giant) ? 4 : 1, mb = b.Goalie ? 6 : b.Has(Buff.Giant) ? 4 : 1;
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
            if (victim.Y > 0.15f) return;                                       // перепрыгнул
            if (victim.Has(Buff.Giant) && !hitter.Has(Buff.Giant)) { hitter.Checking = 0; Ev(EvType.Body); return; }
            victim.Stun = hitter.Has(Buff.Giant) ? 2.0f : 1.4f;
            victim.Dive = 0;
            if (hitter.Has(Buff.Giant)) closing *= 1.6f;
            victim.Vx = nx * (4 + closing * 0.5f) + hitter.Vx * 0.3f;
            victim.Vz = nz * (4 + closing * 0.5f) + hitter.Vz * 0.3f;
            victim.Charge = 0;
            hitter.Checking = 0;
            Ev(EvType.Hit, closing, victim.Idx);
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
            float speed = ShotSpeed(charge, oneTimer);
            float sh = p.ShotH;
            float dx = MathF.Sin(sh), dz = MathF.Cos(sh);
            float vy;
            float gx = Rink.TeamDir[p.Team] * Rink.GoalX;
            if (p.Human != null && p.HasAim)
            {
                // человек: шайба летит ровно в прицел; высота у ворот зависит только от силы замаха
                // (короткий щелчок — по льду, полный замах — под перекладину). Никаких случайностей.
                float along = MathF.Abs(dx) > 0.05f ? (gx - bx) / dx : -1;
                float dist = along > 1 && along < 45 ? along : 14;
                vy = ShotVy(charge, dist, speed);
                float err = oneTimer ? 0.02f : 0.006f;
                float a0 = MathF.Atan2(dx, dz) + Rnd(-err, err);
                dx = MathF.Sin(a0); dz = MathF.Cos(a0);
            }
            else
            {
                vy = 0.4f + 3.2f * power * Rnd(0.6f, 1);
                // боты: автоприцел, если смотрят примерно в ворота
                Player keeper = null;
                foreach (var q in Players) if (q.Goalie && q.Team != p.Team) keeper = q;
                float side = keeper != null && keeper.Z > 0 ? -1 : keeper != null && keeper.Z < 0 ? 1 : (Rand() < 0.5f ? -1 : 1);
                float tz = Rnd(0.55f, 1.25f) * side;
                float tx = gx - pk.X, tzz = tz - pk.Z;
                float dist = Hyp(tx, tzz);
                float ang = MathF.Abs(Wrap(MathF.Atan2(tx, tzz) - sh));
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
                float err = diff.ShootErr * (0.6f + power) * (oneTimer ? 1.6f : 1);
                float a = MathF.Atan2(dx, dz) + Rnd(-err, err);
                dx = MathF.Sin(a); dz = MathF.Cos(a);
            }

            pk.Owner = -1;
            pk.X = bx; pk.Z = bz; pk.Y = 0.08f;
            pk.Vx = dx * speed + p.Vx * 0.25f; pk.Vz = dz * speed + p.Vz * 0.25f; pk.Vy = vy;
            pk.SaveCheck.Clear();
            pk.PassTarget = -1;
            Touch(p);
            p.NoPick = 0.35f;
            if (p.Has(Buff.Mega))
            {
                p.Buffs[(int)Buff.Mega] = 0;
                pk.Mega = true;
                pk.Vx *= 1.45f; pk.Vz *= 1.45f;
                Ev(EvType.MegaShot, 0, p.Idx);
            }
            Ev(EvType.Shot, power, p.Idx);
        }

        /// Скорость броска по силе замаха (0..1); с лёта — быстрее.
        public static float ShotSpeed(float charge, bool oneTimer = false)
        {
            float power = charge < 0.12f ? 0.3f : charge;
            return 15 + 22 * power + (oneTimer ? 4 : 0);
        }

        /// Высота шайбы над льдом на линии ворот: от льда (щелчок) до ~1.05 м (полный замах).
        public static float ShotHeight(float charge) => 0.05f + 1.0f * Math.Clamp(charge, 0, 1);

        /// Вертикальная скорость, чтобы на расстоянии dist шайба была на высоте ShotHeight.
        public static float ShotVy(float charge, float dist, float speed)
        {
            float t = dist / speed;
            return Math.Clamp((ShotHeight(charge) - 0.08f + 0.5f * G * t * t) / t, 0, 9);
        }

        void DoPass(Player p)
        {
            var pk = Puck;
            if (pk.Owner != p.Idx) { Ev(EvType.Swish); p.Swing = 0.2f; return; }
            float fx = MathF.Sin(p.ShotH), fz = MathF.Cos(p.ShotH);
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
            for (int b = 0; b < g.Buffs.Length; b++) g.Buffs[b] = MathF.Max(0, g.Buffs[b] - dt);
            g.NoPick = MathF.Max(0, g.NoPick - dt);
            int s = -Rink.TeamDir[g.Team];
            int outDir = -s;
            float gx = s * Rink.GoalX, gz = 0;
            var pk = Puck;
            float tx, tz, maxSpd = 4.6f;
            float pspd = Hyp(pk.Vx, pk.Vz);
            Player carrier = pk.Owner >= 0 ? Players[pk.Owner] : null;
            if (s * pk.X > Rink.GoalX - 0.2f)
            {
                // шайба за воротами: прижимаемся к штанге с её стороны
                tx = gx + outDir * 0.55f;
                tz = (pk.Z >= 0 ? 1 : -1) * (Rink.GoalW / 2 - 0.25f);
            }
            else if (pk.Owner < 0 && pk.Vx * s > 3 && pspd > 6)
            {
                // бросок по воротам: считаем, где шайба пересечёт линию вратаря, и скользим туда
                float lineX = g.X;
                float tHit = (lineX - pk.X) / pk.Vx;
                float hitZ = tHit > 0 ? pk.Z + pk.Vz * tHit : pk.Z;
                tx = g.X;
                tz = Clamp(hitZ, -Rink.GoalW / 2 - 0.5f, Rink.GoalW / 2 + 0.5f);
                maxSpd = 6.2f;
            }
            else
            {
                // стоим на линии «шайба — центр ворот», перекрывая угол; выходим дальше, когда шайба далеко
                float px = pk.X, pz = pk.Z;
                if (carrier != null && carrier.Team != g.Team)
                {   // упреждение: куда шайба поедет через мгновение
                    px += carrier.Vx * 0.2f; pz += carrier.Vz * 0.2f;
                }
                float dx = px - gx, dz = pz - gz;
                float d = Hyp(dx, dz); if (d < 1e-4f) d = 1;
                float r = Clamp(0.5f + d * 0.07f, 0.6f, 1.9f);
                if (d < 4) r = Clamp(d * 0.3f, 0.6f, r);   // соперник у ворот: откатываемся в створ
                tx = gx + dx / d * r; tz = gz + dz / d * r;
                if (outDir * (tx - gx) < 0.45f) tx = gx + outDir * 0.45f;
                tz = Clamp(tz, -Rink.GoalW / 2 - 0.3f, Rink.GoalW / 2 + 0.3f);
                // ничейная медленная шайба рядом с воротами: выкатываемся и накрываем её
                if (pk.Owner < 0 && pspd < 5 && d < 3.2f && pk.Y < 0.3f && g.NoPick <= 0 && Phase == Phase.Play)
                {
                    float myD = Hyp(pk.X - g.X, pk.Z - g.Z), oppD = 99;
                    foreach (var q in Players) if (q.Team != g.Team && !q.Goalie) oppD = MathF.Min(oppD, Hyp(pk.X - q.X, pk.Z - q.Z));
                    if (myD < oppD * 0.8f) { tx = pk.X; tz = pk.Z; maxSpd = 5.5f; }
                }
                // выход на ворота: соперник с шайбой совсем близко — выбиваем клюшкой
                if (carrier != null && carrier.Team != g.Team && g.Swing <= 0 && Phase == Phase.Play && !g.Has(Buff.Frozen))
                {
                    float cd = Hyp(pk.X - g.X, pk.Z - g.Z);
                    if (cd < 1.5f && Rand() < dt * 1.1f * (0.5f + diff.SaveP)) GoaliePoke(g);
                }
            }
            float mx = tx - g.X, mz = tz - g.Z;
            float md = Hyp(mx, mz);
            float want = MathF.Min(g.Has(Buff.Frozen) ? 1.4f : maxSpd, md * 7);
            float vx = md > 1e-3f ? mx / md * want : 0, vz = md > 1e-3f ? mz / md * want : 0;
            float k = MathF.Min(1, dt * (maxSpd > 5 ? 20 : 12));
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

        /// Вратарь выбивает шайбу у соперника клюшкой в сторону от ворот.
        void GoaliePoke(Player g)
        {
            var pk = Puck;
            int outDir = Rink.TeamDir[g.Team];
            var c = Players[pk.Owner];
            c.NoPick = 0.5f;
            pk.Owner = -1;
            float side = pk.Z >= g.Z ? 1 : -1;
            float a = Rnd(0.5f, 1.1f);
            pk.Vx = outDir * MathF.Cos(a) * 9; pk.Vz = side * MathF.Sin(a) * 9; pk.Vy = 0;
            pk.Y = 0.02f;
            pk.SaveCheck.Clear();
            g.Swing = 0.35f; g.Save = 0.45f;
            Touch(g);
            Ev(EvType.Save, 0, g.Idx);
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
                        if (q.Team == o.Team || q.Goalie || q.Stun > 0 || q.NoPick > 0 || q.Has(Buff.Frozen) || q.Y > 0.6f) continue;
                        Blade(q, out float qx, out float qz);
                        bool diveReach = q.Dive > 0 && Hyp(q.X + MathF.Sin(q.H) * 1.3f - pk.X, q.Z + MathF.Cos(q.H) * 1.3f - pk.Z) < 1.1f;
                        float reach = q.Has(Buff.Magnet) ? 1.3f : q.Has(Buff.Giant) ? 1.0f : 0.75f;
                        if (diveReach || Hyp(qx - pk.X, qz - pk.Z) < reach || Hyp(q.X - pk.X, q.Z - pk.Z) < 0.55f)
                        {
                            float rate = (q.Human != null ? 2.6f : diff.Steal) * (q.Has(Buff.Magnet) ? 2.2f : 1);
                            float resist = (o.Human != null ? 1 : 1.15f) * (o.Has(Buff.Giant) ? 1.8f : 1);
                            if (diveReach || Rand() < rate / resist * dt)
                            {
                                pk.Owner = q.Idx; o.NoPick = 0.7f; Touch(q); Ev(EvType.Steal, 0, q.Idx);
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
                        pk.InNet = true; pk.Mega = false;
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
                // занял позицию заранее (шайба летит в корпус) — шанс выше
                // бросок в угол ворот тяжелее взять, даже если вратарь на месте
                float tg = (s * Rink.GoalX - pk.X) / (MathF.Abs(pk.Vx) > 0.1f ? pk.Vx : 0.1f);
                float zg = MathF.Abs(pk.Z + pk.Vz * MathF.Max(0, tg));
                float corner = MathF.Max(0, zg - 0.7f) * 0.32f + MathF.Max(0, pk.Y - 0.7f) * 0.15f;
                float prob = diff.SaveP + 0.04f + (lateral < 0.35f ? 0.05f : 0) - corner - (spd - 14) / 70 - lateral * 0.22f - MathF.Max(0, pk.Y - 0.9f) * 0.3f;
                if (pk.Mega) prob -= 0.35f;
                if (g.Has(Buff.Frozen)) prob -= 0.25f;
                int lt = pk.LastTouch[0];
                if (lt >= 0 && Players[lt].Team == g.Team) prob = 1;
                prob = Clamp(prob, 0.18f, 0.95f);
                bool ok = Rand() < prob;
                pk.SaveCheck[g.Idx] = ok;
                if (!ok) continue;
                g.Save = 0.6f;
                Ev(EvType.Save, 0, g.Idx);
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
            pk.Owner = g.Idx; pk.HoldT = t; pk.Mega = false;
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
                if (p.Goalie || p.Stun > 0 || p.NoPick > 0 || p.Y > 0.7f) continue;
                Blade(p, out float bx, out float bz);
                float d = MathF.Min(Hyp(bx - pk.X, bz - pk.Z), Hyp(p.X - pk.X, p.Z - pk.Z) + 0.25f);
                if (p.Dive > 0) d = MathF.Min(d, Hyp(p.X + MathF.Sin(p.H) * 1.3f - pk.X, p.Z + MathF.Cos(p.H) * 1.3f - pk.Z));
                float r = pk.PassTarget == p.Idx ? 1.25f : 0.8f;
                if (p.Has(Buff.Magnet)) r += 0.8f;
                else if (p.Has(Buff.Giant)) r += 0.3f;
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
            pk.Mega = false;
            if (best.Dive > 0) best.Dive = MathF.Min(best.Dive, 0.15f);
            Touch(best);
            if (spd > 6) Ev(EvType.Receive);
            return true;
        }

        /// Шайба в голову: первый раз слетает шлем, без шлема — нокаут со звёздочками.
        bool PuckHead()
        {
            var pk = Puck;
            if (pk.Y < 1.0f) return false;
            foreach (var p in Players)
            {
                if (p.Goalie || p.HeadCd > 0 || p.NoPick > 0.15f || p.Brawl > 0) continue;
                float headY = p.Y + 1.45f;
                if (MathF.Abs(pk.Y - headY) > 0.4f) continue;
                float dx = pk.X - p.X, dz = pk.Z - p.Z, d = Hyp(dx, dz);
                if (d > 0.45f || d < 1e-4f) continue;
                float rvx = pk.Vx - p.Vx, rvz = pk.Vz - p.Vz;
                float spd = Hyp(rvx, rvz);
                if (spd < 7 || (rvx * dx + rvz * dz) > 0) continue;   // должна лететь в голову, а не от неё
                p.HeadCd = 0.6f;
                float ang = MathF.Atan2(pk.Vx, pk.Vz);
                // шайба отскакивает от головы вверх-назад
                float nx = dx / d, nz = dz / d;
                pk.Vx = nx * spd * 0.35f + Rnd(-1, 1); pk.Vz = nz * spd * 0.35f + Rnd(-1, 1); pk.Vy = Rnd(2, 4);
                pk.X = p.X + nx * 0.5f; pk.Z = p.Z + nz * 0.5f;
                pk.Mega = false;
                if (p.Helmet)
                {
                    p.Helmet = false;
                    p.Stagger = 0.6f;
                    p.Vx += pk.Vx * 0.15f; p.Vz += pk.Vz * 0.15f;
                    Ev(EvType.HelmetOff, ang, p.Idx);
                }
                else
                {
                    p.Stun = 3.5f; p.Charge = 0; p.Dive = 0;
                    p.Vx = MathF.Sin(ang) * 3; p.Vz = MathF.Cos(ang) * 3;
                    if (Puck.Owner == p.Idx) Puck.Owner = -1;
                    Ev(EvType.Knockout, ang, p.Idx);
                }
                return true;
            }
            return false;
        }

        void PuckBodies()
        {
            var pk = Puck;
            if (PuckHead()) return;
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
            if (Phase == Phase.Goal && LastGoal.Team == p.Team)
            {
                // празднование: кружатся и подпрыгивают
                float a = SimTime * 2.2f + p.Idx;
                return new PlayerInput { MoveX = MathF.Sin(a) * 0.5f, MoveZ = MathF.Cos(a) * 0.5f, Jump = Rand() < 0.03f };
            }
            return PlayerInput.None;
        }

        PlayerInput Steer(Player p, float tx, float tz, float arrive, float speedMul, out float d, out float diffAng)
        {
            float dx = tx - p.X, dz = tz - p.Z;
            d = Hyp(dx, dz);
            float desired = MathF.Atan2(dx, dz);
            diffAng = Wrap(desired - p.H);
            var inp = new PlayerInput();
            float spd = Hyp(p.Vx, p.Vz);
            if (d < arrive)
            {
                // приехали — тормозим против скорости
                if (spd > 1.5f) { inp.MoveX = -p.Vx / spd; inp.MoveZ = -p.Vz / spd; }
            }
            else if (d > 1e-3f)
            {
                inp.MoveX = dx / d * speedMul; inp.MoveZ = dz / d * speedMul;
            }
            return inp;
        }
        PlayerInput Steer(Player p, float tx, float tz, float arrive = 1.5f, float speedMul = 1) => Steer(p, tx, tz, arrive, speedMul, out _, out _);

        PlayerInput BotInput(Player p, float dt)
        {
            var inp = BotInputCore(p, dt);
            if (p.PunchCd <= 0 && p.Y < 0.05f)
            {
                // ответ обидчику или удар по владельцу шайбы рядом
                bool revenge = p.LastHitBy >= 0 && SimTime - p.LastHitT < 1.2f && Hyp(Players[p.LastHitBy].X - p.X, Players[p.LastHitBy].Z - p.Z) < 1.6f;
                if (revenge && Rand() < 2.5f * dt * diff.React) inp.Fight = true;
                else if (carrier != null && carrier.Team != p.Team && !carrier.Goalie && Hyp(carrier.X - p.X, carrier.Z - p.Z) < 1.5f && Rand() < 0.6f * dt * diff.CheckP) inp.Fight = true;
            }
            if (p.Y <= 0.02f && Rand() < 3 * dt * diff.React)
            {
                // перепрыгнуть летящий силовой приём
                foreach (var o in Players)
                    if (o.Team != p.Team && o.Checking > 0 && Hyp(o.X - p.X, o.Z - p.Z) < 2.2f) { inp.Jump = true; break; }
            }
            return inp;
        }

        Item NearestItem(Player p, float maxD)
        {
            Item best = null; float bd = maxD;
            foreach (var it in Items) { float d = Hyp(it.X - p.X, it.Z - p.Z); if (d < bd) { bd = d; best = it; } }
            return best;
        }

        PlayerInput BotInputCore(Player p, float dt)
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
                return si.Scaled(0.3f);
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

            if (Cfg.Arcade && pk.Owner != p.Idx && chaser[p.Team] != p.Idx)
            {
                var it = NearestItem(p, 7);
                if (it != null) { var ri = Steer(p, it.X, it.Z, 0); ri.Sprint = true; return ri; }
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
                else if (carrier != null && d > 1.8f && d < 3.4f && MathF.Abs(da) < 0.3f && Hyp(p.Vx, p.Vz) > 5 && Rand() < 0.7f * dt * diff.React) r.Jump = true;
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
                o.Back = false;
                o.Goals = p.Goals; o.Assists = p.Assists;
                o.Y = p.Y; o.Dive = p.Dive > 0;
                o.Punch = p.Punch > 0; o.Brawl = p.Brawl > 0; o.Stagger = p.Stagger > 0; o.BrawlPower = p.BrawlPower;
                o.Helmet = p.Helmet || p.Goalie;
                byte bits = 0;
                for (int b = 0; b < p.Buffs.Length; b++) if (p.Buffs[b] > 0) bits |= (byte)(1 << b);
                o.Buffs = bits;
            }
            v.Items.Clear();
            foreach (var it in Items) v.Items.Add(new ItemView { X = it.X, Z = it.Z, Type = it.Type });
            v.PuckMega = Puck.Mega;
            v.PuckX = Puck.X; v.PuckY = Puck.Y; v.PuckZ = Puck.Z; v.PuckOwner = Puck.Owner;
            v.Score0 = Score[0]; v.Score1 = Score[1];
            v.Time = Time; v.Phase = Phase; v.PhaseT = PhaseT; v.Overtime = Overtime; v.SimTime = SimTime;
            v.LastGoal = LastGoal;
        }
    }
}
