// PUCKO — главный скрипт: режимы игры, игровой цикл, камеры «из-за спины», эффекты.
// Интерфейс — в HockeyGame.UI.cs, автотест — в HockeyGame.Autotest.cs.
// Создаётся автоматически в любой сцене — достаточно нажать Play.
using System.Collections.Generic;
using UnityEngine;

namespace Hockey
{
    public partial class HockeyGame : MonoBehaviour
    {
        public const string GameTitle = "PUCKO";
        const float Step = 1f / 60;
        static readonly string[] SlotColors = { "#ffd23f", "#5cf28a", "#ff7ad9", "#4fe3ff", "#ffa94d", "#b48cff" };

#pragma warning disable CS0618
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            if (FindObjectOfType<HockeyGame>() == null) new GameObject("Hockey").AddComponent<HockeyGame>();
        }
#pragma warning restore CS0618

        enum Mode { None, Local, Host, Client }

        class View
        {
            public Camera Cam; public int Follow = -1; public InputSource Src;
            public float Yaw, Pitch = 0.32f, LastManual = -10; public bool Init;
            public Vector3 Pos, Look; public Rect Rect;
            public string Toast; public Color ToastColor; public float ToastT;
            public bool HudInit; public float ChargeA, LastCharge;
            public float Side, GoalW;
        }

        // ---------- состояние ----------
        Mode mode = Mode.None;
        Sim sim;
        readonly ViewState simView = new ViewState(), netView = new ViewState();
        readonly int[] shots = new int[2];
        readonly ViewState prevView = new ViewState(), curView = new ViewState(), smoothView = new ViewState();
        ViewState view;
        List<RosterEntry> roster = new List<RosterEntry>();
        readonly Dictionary<string, int> slots = new Dictionary<string, int>();
        readonly List<InputSource> sources = new List<InputSource>();
        readonly List<string> myKeys = new List<string>();
        List<LobbyEntry> lobby = new List<LobbyEntry>();
        MatchConfig cfg = new MatchConfig();
        readonly SessionInfo session = new SessionInfo();
        HostNet host; ClientNet client; LanFinder finder;
        int matchId, clientMatchId = -1;
        float acc; int steps;
        readonly List<GameEvent> netEvents = new List<GameEvent>();
        readonly List<ViewState> snaps = new List<ViewState>();
        double offset; bool offsetInit;
        readonly ViewState interp = new ViewState();
        bool paused, endShown; float endTimer = -1;
        string message = "";
        string playerName, address;
        bool splitCoop;
        int profNum = 17, profSkin, profTape, profHelm;   // профиль игрока из «Раздевалки»
        /// Обмотка клюшки и цвет шлема одним числом (так они передаются и по сети).
        int ProfLook => profTape + 8 * profHelm;
        float flash;                            // белая вспышка экрана на гол
        PlayerInput clientInput;

        // ---------- рендер ----------
        Arena arena;
        GameAudio audioFx;
        Fx fx;
        readonly List<CharacterRig> chars = new List<CharacterRig>();
        readonly List<GameObject> extras = new List<GameObject>();
        readonly Dictionary<int, Transform> rings = new Dictionary<int, Transform>();
        readonly List<CharacterRig> showcase = new List<CharacterRig>();
        LockerRoom locker;
        bool lockerShown; float lockerFade;
        static readonly Vector3 ShowPos0 = new Vector3(-1.3f, 0, 0.4f);
        Transform puck, puckRing, puckFire;
        LineRenderer trail;
        readonly List<Vector3> trailPts = new List<Vector3>();
        readonly List<View> views = new List<View>();
        Camera menuCam;
        float shake, time;
        Vector3 menuCamPos, menuLook;
        bool inMatch => screen == UiScreen.Match;

        // ================================================================ запуск
        void Awake()
        {
            Application.runInBackground = true;
            Application.targetFrameRate = 120;
            QualitySettings.vSyncCount = 1;
#pragma warning disable CS0618
            foreach (var c in FindObjectsOfType<Camera>()) c.gameObject.SetActive(false);
            foreach (var l in FindObjectsOfType<Light>()) l.gameObject.SetActive(false);
            foreach (var a in FindObjectsOfType<AudioListener>()) a.enabled = false;
#pragma warning restore CS0618
            gameObject.AddComponent<AudioListener>();

            Settings.Load();
            playerName = PlayerPrefs.GetString("hockey.name", "Игрок");
            address = PlayerPrefs.GetString("hockey.addr", "192.168.1.");
            cfg.Skaters = PlayerPrefs.GetInt("hockey.skaters", 3);
            cfg.Duration = PlayerPrefs.GetInt("hockey.duration", 180);
            cfg.Difficulty = PlayerPrefs.GetInt("hockey.diff", 1);
            cfg.Arcade = PlayerPrefs.GetInt("hockey.arcade", 1) == 1;
            profNum = PlayerPrefs.GetInt("hockey.num", 17);
            profSkin = PlayerPrefs.GetInt("hockey.skin", 0);
            profTape = PlayerPrefs.GetInt("hockey.tape", 0);
            profHelm = Mathf.Clamp(PlayerPrefs.GetInt("hockey.helm", 0), 0, 3);

            arena = new Arena(transform);
            audioFx = new GameAudio(gameObject);
            fx = new Fx(transform);
            BuildPuck();
            menuCam = MakeCamera("MenuCam");
            menuCam.fieldOfView = 40;
            BuildShowcase();
            locker = new LockerRoom(transform, new Vector3(0, -60, 0), profNum);
            ApplySettings();
            finder = new LanFinder();
            StartAutotestIfRequested();
        }

        void ApplySettings()
        {
            arena.SetQuality(Settings.Quality);
            arena.SetBrightness(Settings.Brightness);
            Settings.ApplyDisplay();
        }

        Camera MakeCamera(string name)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            var c = go.AddComponent<Camera>();
            c.clearFlags = CameraClearFlags.SolidColor;
            c.backgroundColor = Mats.Hex("#1b1f4a");
            c.fieldOfView = Settings.Fov; c.nearClipPlane = 0.1f; c.farClipPlane = 400;
            c.allowMSAA = true;
            c.allowHDR = true;
            go.AddComponent<PostFx>();
            return c;
        }

        void BuildPuck()
        {
            var go = new GameObject("Puck");
            go.transform.SetParent(transform, false);
            var body = new GameObject("body");
            body.transform.SetParent(go.transform, false);
            body.transform.localPosition = new Vector3(0, 0.04f, 0);
            // выточенная шайба: фаска по краям и рифлёный обод
            var profile = new[]
            {
                new Vector2(0.0f, 0.004f), new Vector2(0.1f, 0.0f), new Vector2(0.125f, 0.004f), new Vector2(0.134f, 0.016f),
                new Vector2(0.135f, 0.028f), new Vector2(0.135f, 0.052f), new Vector2(0.134f, 0.064f), new Vector2(0.125f, 0.076f),
                new Vector2(0.1f, 0.08f), new Vector2(0.03f, 0.077f), new Vector2(0.0f, 0.078f),
            };
            body.AddComponent<MeshFilter>().sharedMesh = MeshKit.Lathe(profile, 40, (a, y) => y > 0.026f && y < 0.054f ? 1 + 0.025f * Mathf.Sign(Mathf.Sin(a * 30)) : 1, "puck");
            body.transform.localPosition = Vector3.zero;
            body.AddComponent<MeshRenderer>().sharedMaterial = Mats.Get("#1a1a22", 0.9f);
            var logo = new GameObject("logo");
            logo.transform.SetParent(go.transform, false);
            logo.transform.localPosition = new Vector3(0, 0.081f, 0);
            logo.transform.localRotation = Quaternion.Euler(90, 0, 0);
            logo.AddComponent<MeshFilter>().sharedMesh = MeshKit.Star(0.06f, 0.45f);
            logo.AddComponent<MeshRenderer>().sharedMaterial = Mats.Get("#ffd23f", 0.6f);
            var fire = new GameObject("fire");
            fire.transform.SetParent(go.transform, false);
            fire.transform.localPosition = new Vector3(0, 0.1f, 0);
            fire.AddComponent<MeshFilter>().sharedMesh = MeshKit.Sphere(0.3f, 12, 0);
            var fmr = fire.AddComponent<MeshRenderer>();
            fmr.sharedMaterial = Mats.Fade("#ff6a2e", 0.55f, true);
            fmr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            puckFire = fire.transform;
            puck = go.transform;

            var ring = new GameObject("PuckRing");
            ring.transform.SetParent(transform, false);
            ring.AddComponent<MeshFilter>().sharedMesh = MeshKit.FlatRing(0.3f, 0.4f);
            var rr = ring.AddComponent<MeshRenderer>();
            rr.sharedMaterial = Mats.Fade("#5fe1ff", 0.65f, true);
            rr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            puckRing = ring.transform;

            var tr = new GameObject("Trail");
            tr.transform.SetParent(transform, false);
            trail = tr.AddComponent<LineRenderer>();
            trail.sharedMaterial = new Material(Mats.Fade("#bff3ff", 0.55f, true));
            trail.widthCurve = new AnimationCurve(new Keyframe(0, 0.01f), new Keyframe(1, 0.12f));
            trail.positionCount = 0;
            trail.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }

        /// Персонажи для главного меню: стоят у центрального круга и пританцовывают.
        void BuildShowcase()
        {
            var specs = new (int team, int skin, int hair, int num, bool goalie, Vector3 pos, float yaw)[]
            {
                (0, profSkin, ProfLook, profNum, false, new Vector3(-1.3f, 0, 0.4f), 200),
                (1, 3, 1, 9, false, new Vector3(1.4f, 0, 0.2f), 160),
                (0, 4, 3, 1, true, new Vector3(0.05f, 0, 1.6f), 180),
            };
            foreach (var s in specs)
            {
                var c = CharacterRig.Build(s.team, s.skin, s.hair, s.num, s.goalie, transform);
                c.Root.localPosition = s.pos;
                c.Root.localRotation = Quaternion.Euler(0, s.yaw, 0);
                showcase.Add(c);
            }
        }

        /// Пересобирает «своего» персонажа на витрине после изменений в раздевалке.
        void RebuildPreview()
        {
            if (showcase.Count == 0) return;
            var old = showcase[0];
            var pos = old.Root.localPosition; var rot = old.Root.localRotation; bool act = old.Root.gameObject.activeSelf;
            Destroy(old.Root.gameObject);
            var c = CharacterRig.Build(0, profSkin, ProfLook, profNum, false, transform);
            c.Root.localPosition = pos; c.Root.localRotation = rot; c.Root.gameObject.SetActive(act);
            showcase[0] = c;
            locker?.SetNumber(profNum);
        }

        void OnDestroy() { CloseNet(); finder?.Dispose(); }
        void OnApplicationQuit() { CloseNet(); finder?.Dispose(); }

        void SavePrefs()
        {
            PlayerPrefs.SetString("hockey.name", playerName);
            PlayerPrefs.SetString("hockey.addr", address);
            PlayerPrefs.SetInt("hockey.skaters", cfg.Skaters);
            PlayerPrefs.SetInt("hockey.duration", cfg.Duration);
            PlayerPrefs.SetInt("hockey.diff", cfg.Difficulty);
            PlayerPrefs.SetInt("hockey.arcade", cfg.Arcade ? 1 : 0);
            PlayerPrefs.SetInt("hockey.num", profNum);
            PlayerPrefs.SetInt("hockey.skin", profSkin);
            PlayerPrefs.SetInt("hockey.tape", profTape);
            PlayerPrefs.SetInt("hockey.helm", profHelm);
            PlayerPrefs.Save();
        }

        string MyName => string.IsNullOrWhiteSpace(playerName) ? "Игрок" : (playerName.Length > 14 ? playerName.Substring(0, 14) : playerName.Trim());

        // ================================================================ режимы
        void StartLocal(bool split)
        {
            CloseNet();
            SavePrefs();
            mode = Mode.Local;
            sources.Clear(); myKeys.Clear();
            lobby = new List<LobbyEntry> { new LobbyEntry { Key = "local0", Name = MyName, Team = 0, Number = profNum, Skin = profSkin, Hair = ProfLook } };
            if (!split)
                sources.Add(new InputSource { Key = "local0", Scheme = Scheme.Solo, Pad = 0, Mouse = true });
            else
            {
                lobby.Add(new LobbyEntry { Key = "local1", Name = "Игрок 2", Team = splitCoop ? 0 : 1 });
                sources.Add(new InputSource { Key = "local0", Scheme = Scheme.P1, Pad = 0 });
                sources.Add(new InputSource { Key = "local1", Scheme = Scheme.P2, Pad = 1 });
            }
            foreach (var s in sources) myKeys.Add(s.Key);
            NewMatch();
        }

        void NewMatch()
        {
            var humans = new List<HumanSlot>();
            foreach (var l in lobby) humans.Add(new HumanSlot(l.Key, l.Name, l.Team, l.Number, l.Skin, l.Hair));
            sim = new Sim(new MatchConfig { Skaters = cfg.Skaters, Duration = cfg.Duration, Difficulty = cfg.Difficulty, Arcade = cfg.Arcade }, humans);
            acc = 0; steps = 0;
            matchId++;
            netEvents.Clear();
            SetupMatch(sim.Roster());
            if (mode == Mode.Host)
            {
                session.InMatch = true; session.MatchId = matchId; session.Roster = roster; session.Seq++;
                host.InMatch = true;
                host.BroadcastSession();
            }
        }

        void SetupMatch(List<RosterEntry> r)
        {
            ApplyRoster(r, true);
            screen = UiScreen.Match;
            paused = false; endShown = false; endTimer = -1;
            shots[0] = shots[1] = 0;
            message = "";
            foreach (var c in showcase) c.Root.gameObject.SetActive(false);
            audioFx.SetMusic(false);
        }

        void ApplyRoster(List<RosterEntry> r, bool resetCams)
        {
            roster = r;
            slots.Clear();
            foreach (var e in r) if (e.Human != null && !slots.ContainsKey(e.Human)) slots[e.Human] = slots.Count;
            BuildCharacters(r);
            var follow = new List<int>();
            foreach (var k in myKeys) { int i = r.FindIndex(e => e.Human == k); if (i >= 0) follow.Add(i); }
            if (resetCams || follow.Count != views.Count)
            {
                foreach (var v in views) Destroy(v.Cam.gameObject);
                views.Clear();
                for (int i = 0; i < follow.Count; i++)
                {
                    var v = new View { Cam = MakeCamera("ViewCam" + i), Follow = follow[i], Src = i < sources.Count ? sources[i] : null };
                    v.Rect = follow.Count == 1 ? new Rect(0, 0, 1, 1) : new Rect(i * 0.5f, 0, 0.5f, 1);
                    v.Cam.rect = v.Rect;
                    views.Add(v);
                }
            }
            else for (int i = 0; i < follow.Count; i++) views[i].Follow = follow[i];
            menuCam.gameObject.SetActive(views.Count == 0);
        }

        void BuildCharacters(List<RosterEntry> r)
        {
            foreach (var c in chars) Destroy(c.Root.gameObject);
            foreach (var g in extras) Destroy(g);
            chars.Clear(); extras.Clear(); rings.Clear();
            for (int i = 0; i < r.Count; i++)
            {
                var e = r[i];
                var c = CharacterRig.Build(e.Team, e.Skin, e.Hair, e.Number, e.Goalie, transform);
                chars.Add(c);
                if (e.Human != null)
                {
                    string col = SlotColor(e.Human);
                    var ring = new GameObject("Ring");
                    ring.transform.SetParent(transform, false);
                    ring.AddComponent<MeshFilter>().sharedMesh = MeshKit.FlatRing(0.55f, 0.7f);
                    var mr = ring.AddComponent<MeshRenderer>();
                    mr.sharedMaterial = Mats.Fade(col, 0.85f, true);
                    mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                    rings[i] = ring.transform;
                    extras.Add(ring);
                    var arrow = new GameObject("Arrow");
                    arrow.transform.SetParent(c.Root, false);
                    arrow.transform.localPosition = new Vector3(0, 2.85f, 0);
                    arrow.AddComponent<MeshFilter>().sharedMesh = MeshKit.Cone(0.17f, 0.3f);
                    var amr = arrow.AddComponent<MeshRenderer>();
                    amr.sharedMaterial = Mats.Emissive(col, 0.8f);
                    amr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                }
            }
        }

        string SlotColor(string human) => SlotColors[(human != null && slots.TryGetValue(human, out int s) ? s : 0) % SlotColors.Length];

        void ToMenu(string msg = "")
        {
            CloseNet();
            mode = Mode.None;
            screen = UiScreen.Menu;
            uiPage = UiPage.Main;
            sim = null; view = null;
            snaps.Clear();
            foreach (var v in views) Destroy(v.Cam.gameObject);
            views.Clear();
            BuildCharacters(new List<RosterEntry>());
            menuCam.gameObject.SetActive(true);
            foreach (var c in showcase) c.Root.gameObject.SetActive(true);
            paused = false; endShown = false;
            message = msg;
            finder ??= new LanFinder();
            audioFx.SetMusic(true);
            fx.UpdateItems(new List<ItemView>(), time);
            puckRing.gameObject.SetActive(false);
            trail.positionCount = 0;
        }

        void CloseNet()
        {
            host?.Dispose(); host = null;
            client?.Dispose(); client = null;
        }

        // ---------------------------------------------------------------- хост
        void StartHost()
        {
            CloseNet();
            SavePrefs();
            finder?.Dispose(); finder = null;
            try { host = new HostNet(MyName); }
            catch (System.Exception e) { message = "Не удалось открыть порт 7777: " + e.Message; finder = new LanFinder(); return; }
            mode = Mode.Host;
            screen = UiScreen.Lobby;
            sources.Clear(); myKeys.Clear();
            sources.Add(new InputSource { Key = "local0", Scheme = Scheme.Solo, Pad = 0, Mouse = true });
            myKeys.Add("local0");
            lobby = new List<LobbyEntry> { new LobbyEntry { Key = "local0", Name = MyName, Team = 0, Number = profNum, Skin = profSkin, Hair = ProfLook } };
            session.InMatch = false; session.Seq++;
            host.InMatch = false;
            host.GetSession = () =>
            {
                session.Lobby = lobby; session.Cfg = cfg;
                host.PlayerCount = lobby.Count;
                return session;
            };
            host.OnJoin += (key, name, num, skin, tape) =>
            {
                int t0 = lobby.FindAll(l => l.Team == 0).Count, t1 = lobby.Count - t0;
                var entry = new LobbyEntry { Key = key, Name = name, Team = t0 <= t1 ? 0 : 1, Number = num > 0 ? num : -1, Skin = skin, Hair = tape };
                lobby.Add(entry);
                if (sim != null && inMatch)
                {
                    var p = sim.ReplaceBotWithHuman(key, name, entry.Team);
                    if (p != null) entry.Team = p.Team;
                    RefreshHostRoster();
                }
                session.Seq++;
                host.BroadcastSession();
            };
            host.OnLeave += key =>
            {
                lobby.RemoveAll(l => l.Key == key);
                if (sim != null && inMatch) { sim.HumanLeft(key); RefreshHostRoster(); }
                session.Seq++;
                host.BroadcastSession();
            };
            host.OnSetTeam += (key, team) =>
            {
                if (inMatch) return;
                var e = lobby.Find(l => l.Key == key);
                if (e != null && e.Team != team) { e.Team = team; session.Seq++; host.BroadcastSession(); }
            };
        }

        void RefreshHostRoster()
        {
            var r = sim.Roster();
            ApplyRoster(r, false);
            session.Roster = r;
        }

        // ---------------------------------------------------------------- гость
        void StartClient(string addr)
        {
            CloseNet();
            SavePrefs();
            try { client = new ClientNet(addr, MyName, Time.realtimeSinceStartupAsDouble, profNum, profSkin, ProfLook); }
            catch (System.Exception e) { message = e.Message; return; }
            mode = Mode.Client;
            screen = UiScreen.Lobby;
            sources.Clear(); myKeys.Clear();
            sources.Add(new InputSource { Key = "me", Scheme = Scheme.Solo, Pad = 0, Mouse = true });
            lobby = new List<LobbyEntry>();
            clientMatchId = -1;
            snaps.Clear(); offsetInit = false;
            client.OnSession += s =>
            {
                lobby = s.Lobby; cfg = s.Cfg;
                if (client.MyKey != null && (myKeys.Count == 0 || myKeys[0] != client.MyKey)) { myKeys.Clear(); myKeys.Add(client.MyKey); sources[0].Key = client.MyKey; }
                if (s.InMatch && s.MatchId != clientMatchId)
                {
                    clientMatchId = s.MatchId;
                    snaps.Clear(); offsetInit = false;
                    SetupMatch(s.Roster);
                }
                else if (s.InMatch && RosterChanged(s.Roster)) ApplyRoster(s.Roster, false);
            };
            client.OnSnapshot += (v, mid, evs) =>
            {
                if (mid != clientMatchId) return;
                if (snaps.Count > 0 && v.SimTime <= snaps[snaps.Count - 1].SimTime) return;
                double now = Time.realtimeSinceStartupAsDouble;
                double off = now - v.SimTime;
                if (!offsetInit || off < offset) { offset = off; offsetInit = true; }
                else offset += (off - offset) * 0.02;
                snaps.Add(v);
                if (snaps.Count > 30) snaps.RemoveAt(0);
                foreach (var e in evs) HandleEvent(e, v);
            };
        }

        bool RosterChanged(List<RosterEntry> r)
        {
            if (r.Count != roster.Count) return true;
            for (int i = 0; i < r.Count; i++) if (r[i].Human != roster[i].Human || r[i].Name != roster[i].Name) return true;
            return false;
        }

        ViewState ClientState()
        {
            if (snaps.Count == 0) return null;
            double t = Time.realtimeSinceStartupAsDouble - offset - 0.1;
            var a = snaps[0]; var b = snaps[snaps.Count - 1];
            if (t >= b.SimTime) return b;
            if (t <= a.SimTime) return a;
            for (int i = 0; i < snaps.Count - 1; i++)
                if (snaps[i].SimTime <= t && snaps[i + 1].SimTime >= t) { a = snaps[i]; b = snaps[i + 1]; break; }
            float k = (float)((t - a.SimTime) / System.Math.Max(1e-4, b.SimTime - a.SimTime));
            LerpView(a, b, k, interp);
            return interp;
        }

        /// Промежуточное состояние между двумя снимками: физика идёт шагами по 1/60 с, а кадры чаще и неровно,
        /// поэтому без этого персонажи двигаются рывками и «двоятся».
        static void LerpView(ViewState a, ViewState b, float k, ViewState o)
        {
            o.Resize(b.Players.Length);
            for (int i = 0; i < b.Players.Length; i++)
            {
                var pb = b.Players[i]; var po = o.Players[i];
                var pa = i < a.Players.Length ? a.Players[i] : pb;
                bool tele = Mathf.Abs(pa.X - pb.X) + Mathf.Abs(pa.Z - pb.Z) > 4;
                float kk = tele ? 1 : k;
                po.X = Mathf.Lerp(pa.X, pb.X, kk); po.Z = Mathf.Lerp(pa.Z, pb.Z, kk); po.Y = Mathf.Lerp(pa.Y, pb.Y, kk);
                po.H = pa.H + Sim.Wrap(pb.H - pa.H) * kk;
                po.Spd = Mathf.Lerp(pa.Spd, pb.Spd, kk); po.Charge = Mathf.Lerp(pa.Charge, pb.Charge, kk);
                po.Stun = pb.Stun; po.Back = pb.Back; po.Dive = pb.Dive; po.Swing = pb.Swing; po.Save = pb.Save; po.Stamina = pb.Stamina;
                po.Buffs = pb.Buffs; po.Goals = pb.Goals; po.Assists = pb.Assists;
                po.Helmet = pb.Helmet; po.Punch = pb.Punch; po.Brawl = pb.Brawl; po.Stagger = pb.Stagger; po.BrawlPower = pb.BrawlPower;
            }
            bool ptele = Mathf.Abs(a.PuckX - b.PuckX) + Mathf.Abs(a.PuckZ - b.PuckZ) > 4;
            float pk = ptele ? 1 : k;
            o.PuckX = Mathf.Lerp(a.PuckX, b.PuckX, pk); o.PuckY = Mathf.Lerp(a.PuckY, b.PuckY, pk); o.PuckZ = Mathf.Lerp(a.PuckZ, b.PuckZ, pk);
            o.PuckOwner = b.PuckOwner; o.PuckMega = b.PuckMega; o.Items = b.Items;
            o.Score0 = b.Score0; o.Score1 = b.Score1; o.Time = b.Time; o.Phase = b.Phase; o.PhaseT = b.PhaseT;
            o.Overtime = b.Overtime; o.LastGoal = b.LastGoal; o.SimTime = b.SimTime;
        }

        // ================================================================ ввод
        /// Читает устройство, поворачивает камеру и переводит стик в мировое направление относительно камеры.
        PlayerInput ReadPlayer(View v, float dt)
        {
            if (v == null || v.Src == null) return new PlayerInput();
            var raw = GameInput.Read(v.Src, Settings.MouseSens);
            bool lockedMouse = v.Src.Mouse && Cursor.lockState == CursorLockMode.Locked;
            float lx = lockedMouse ? raw.LookX : 0, ly = lockedMouse ? raw.LookY : 0;
            lx += raw.LookRateX * 160 * dt; ly += raw.LookRateY * 110 * dt;
            if (Mathf.Abs(lx) + Mathf.Abs(ly) > 0.01f) v.LastManual = time;
            v.Yaw += lx * Mathf.Deg2Rad;
            // ниже 0.06 камера уже не опускается, а смотрит вверх — можно целиться в «девятку»
            v.Pitch = Mathf.Clamp(v.Pitch + (Settings.InvertY ? ly : -ly) * Mathf.Deg2Rad, -0.35f, 0.85f);
            float fx = Mathf.Sin(v.Yaw), fz = Mathf.Cos(v.Yaw);
            var aim = v.Cam != null ? AimPoint(v, out _) : Vector3.zero;
            return new PlayerInput
            {
                MoveX = fx * raw.MoveY + fz * raw.MoveX,
                MoveZ = fz * raw.MoveY - fx * raw.MoveX,
                Shoot = raw.Shoot, Pass = raw.Pass, Check = raw.Check, Sprint = raw.Sprint, Jump = raw.Jump, Fight = raw.Fight,
                AimYaw = v.Yaw, HasAim = v.Cam != null, AimX = aim.x, AimY = aim.y, AimZ = aim.z,
            };
        }

        /// Точка, в которую смотрит центр экрана (прицел): на плоскости чужих ворот, если камера смотрит в их сторону,
        /// иначе — в 30 м по лучу; не ниже льда. onGoal — луч попадает в створ ворот.
        Vector3 AimPoint(View v, out bool onGoal)
        {
            onGoal = false;
            var cam = v.Cam.transform; var o = cam.position; var d = cam.forward;
            int team = v.Follow >= 0 && v.Follow < roster.Count ? roster[v.Follow].Team : 0;
            float gx = Rink.TeamDir[team] * Rink.GoalX;
            Vector3 p = o + d * 30;
            bool toGoal = d.x * Mathf.Sign(gx) > 0.05f;
            if (toGoal) { float t = (gx - o.x) / d.x; if (t > 0 && t < 80) p = o + d * t; else toGoal = false; }
            if (p.y < 0.03f)
            {
                if (d.y < -1e-3f) { float t = (0.03f - o.y) / d.y; p = o + d * t; toGoal = false; }
                p.y = 0.03f;
            }
            onGoal = toGoal && Mathf.Abs(p.z) < Rink.GoalW / 2 - 0.05f && p.y < Rink.GoalH - 0.05f;
            return p;
        }

        View ViewFor(string key)
        {
            foreach (var v in views) if (v.Src != null && v.Src.Key == key) return v;
            return null;
        }

        // ================================================================ цикл
        void Update()
        {
            float dt = Mathf.Min(0.05f, Time.unscaledDeltaTime);
            time += dt;
            double now = Time.realtimeSinceStartupAsDouble;

            if (inMatch && GameInput.PausePressed() && !endShown) { paused = !paused; uiPage = UiPage.Main; }
            if (inMatch && GameInput.KeyDown(KeyCode.M)) audioFx.Muted = !audioFx.Muted;
            UpdateCursor();

            finder?.Update(now);
            host?.Update(now);
            if (client != null)
            {
                var v0 = views.Count > 0 ? views[0] : null;
                clientInput = paused || !inMatch || v0 == null ? new PlayerInput() : (autotest ? autoInput : ReadPlayer(v0, dt));
                client.Update(now, clientInput);
                if (client != null && client.Error != null) ToMenu(client.Error);
            }

            view = null;
            bool stepThisFrame = false;
            if ((mode == Mode.Local || mode == Mode.Host) && sim != null && inMatch)
            {
                // ввод (и поворот камеры) читаем раз за кадр, применяем на каждом шаге физики
                var frameInputs = new Dictionary<string, PlayerInput>();
                foreach (var s in sources)
                    frameInputs[s.Key] = paused ? new PlayerInput() : autotest ? autoInput : ReadPlayer(ViewFor(s.Key), dt);
                if (!(paused && mode == Mode.Local) && !autoFreeze)
                {
                    acc += Mathf.Min(0.25f, Time.unscaledDeltaTime);
                    var inputs = new Dictionary<string, PlayerInput>();
                    bool stepped = false;
                    while (acc >= Step)
                    {
                        if (acc - Step < Step) sim.FillView(prevView);   // состояние перед последним шагом кадра
                        stepped = true;
                        inputs.Clear();
                        if (host != null) foreach (var k in host.ClientKeys) if (host.TryGetInput(k, out var ri)) inputs[k] = ri;
                        foreach (var kv in frameInputs) inputs[kv.Key] = kv.Value;
                        sim.Step(Step, inputs);
                        acc -= Step;
                        steps++;
                        if (sim.Events.Count > 0) { sim.FillView(simView); foreach (var e in sim.Events) HandleEvent(e, simView); }
                        if (mode == Mode.Host)
                        {
                            netEvents.AddRange(sim.Events);
                            if (steps % 3 == 0)
                            {
                                sim.FillView(netView);
                                host.BroadcastSnapshot(matchId, netView, netEvents);
                                netEvents.Clear();
                            }
                        }
                        sim.Events.Clear();
                    }
                    stepThisFrame = stepped;
                }
                else stepThisFrame = false;
                sim.FillView(simView);
                if (curView.Players.Length != simView.Players.Length) { sim.FillView(prevView); sim.FillView(curView); }
                else if (stepThisFrame) sim.FillView(curView);
                LerpView(prevView, curView, Mathf.Clamp01(acc / Step), smoothView);
                view = smoothView;
            }
            else if (mode == Mode.Client && inMatch)
            {
                view = ClientState();
            }

            if (view != null && inMatch && chars.Count == view.Players.Length)
            {
                UpdateScene(view, dt);
                if (view.Phase == Phase.End && !endShown)
                {
                    if (endTimer < 0) { endTimer = 1.2f; fx.ConfettiRain(Vector3.zero, 400); }
                    endTimer -= Time.unscaledDeltaTime;
                    if (endTimer <= 0) { endShown = true; paused = false; }
                }
            }
            else if (!inMatch) MenuScene(dt);
            arena.Update(dt, time);
            audioFx.Update(dt);
            fx.Update(dt);
            flash = Mathf.Max(0, flash - dt * 1.6f);
        }

        void UpdateCursor()
        {
            bool wantLock = inMatch && !paused && !endShown && views.Count == 1 && views[0].Src != null && views[0].Src.Mouse && !autotest && Application.isFocused;
            var want = wantLock ? CursorLockMode.Locked : CursorLockMode.None;
            if (Cursor.lockState != want) { Cursor.lockState = want; Cursor.visible = !wantLock; }
        }

        static Vector3 PlayerPos(ViewState st, int i, float y = 0) =>
            st != null && i >= 0 && i < st.Players.Length ? new Vector3(st.Players[i].X, st.Players[i].Y + y, st.Players[i].Z) : new Vector3(st?.PuckX ?? 0, y, st?.PuckZ ?? 0);

        void HandleEvent(GameEvent e, ViewState st)
        {
            audioFx.Event(e);
            if (e.Type == EvType.Shot && e.Who >= 0 && e.Who < roster.Count) shots[roster[e.Who].Team & 1]++;
            if (st == null) return;
            var who = PlayerPos(st, e.Who);
            switch (e.Type)
            {
                case EvType.Shot:
                    {   // бросок: ледяная крошка из-под крюка, кольцо по льду; сильный щелчок — искры и лёгкая встряска
                        var pp = new Vector3(st.PuckX, 0.05f, st.PuckZ);
                        var sd = who.sqrMagnitude > 0 ? (pp - new Vector3(who.x, 0.05f, who.z)).normalized : Vector3.forward;
                        fx.Chips(pp, sd, 4 + (int)(e.Value * 10), 3 + e.Value * 4);
                        fx.Shockwave(pp, new Color(1, 1, 1, 0.8f), 0.9f + e.Value * 1.3f);
                        if (e.Value > 0.7f)
                        {
                            fx.Sparks(pp + Vector3.up * 0.1f, Mats.Hex("#ffd23f"), 10, 5);
                            fx.Pow(pp + Vector3.up * 0.5f, 0.5f);
                            if (Settings.ScreenShake) shake = Mathf.Max(shake, 0.1f);
                        }
                        break;
                    }
                case EvType.Pass:
                    fx.Chips(new Vector3(st.PuckX, 0.05f, st.PuckZ), Vector3.up, 3, 2);
                    break;
                case EvType.Hit:
                    if (Settings.ScreenShake) shake = Mathf.Max(shake, 0.25f);
                    fx.Sparks(who + Vector3.up * 1.6f, Mats.Hex("#ffd23f"), 18);
                    fx.Chips(who, Vector3.up, 14, 5);
                    fx.Pow(who + Vector3.up * 1.3f, 1f);
                    fx.Shockwave(who, Color.white, 2.6f);
                    fx.Puff(who, 6, Color.white, 0.5f, 2f, 0.45f);
                    break;
                case EvType.HelmetOff:
                    {
                        if (Settings.ScreenShake) shake = Mathf.Max(shake, 0.2f);
                        var dir = new Vector3(Mathf.Sin(e.Value), 0, Mathf.Cos(e.Value));
                        if (e.Who >= 0 && e.Who < chars.Count && chars[e.Who].HelmetParts != null)
                        {
                            fx.FlyingHelmet(chars[e.Who].HelmetParts, dir * 5f + Vector3.up * 6f);
                            chars[e.Who].SetHelmet(false);
                            chars[e.Who].KickHead(9);
                            chars[e.Who].Kick(-2);
                        }
                        fx.Pow(who + Vector3.up * 1.7f, 0.9f);
                        fx.Sparks(who + Vector3.up * 1.6f, Color.white, 16, 4);
                        Toast(e.Who, "ШЛЕМ СЛЕТЕЛ!\nберегите голову", Mats.Hex("#ffd23f"));
                        break;
                    }
                case EvType.Knockout:
                    if (Settings.ScreenShake) shake = Mathf.Max(shake, 0.35f);
                    if (e.Who >= 0 && e.Who < chars.Count) chars[e.Who].KickHead(14);
                    fx.Pow(who + Vector3.up * 1.6f, 1.5f);
                    fx.Sparks(who + Vector3.up * 1.6f, Mats.Hex("#ffd23f"), 30, 5);
                    fx.Shockwave(who, Mats.Hex("#ffd23f"), 3f);
                    Toast(e.Who, "НОКАУТ!\nотдыхает со звёздочками", Mats.Hex("#ff6a4e"));
                    break;
                case EvType.Punch:
                    if (Settings.ScreenShake) shake = Mathf.Max(shake, 0.12f);
                    fx.Pow(who + Vector3.up * 1.4f, 0.7f);
                    fx.Sparks(who + Vector3.up * 1.4f, Color.white, 8, 3);
                    break;
                case EvType.BrawlStart:
                    fx.Puff(who + Vector3.up * 0.5f, 18, Color.white, 0.8f, 3f, 0.6f);
                    fx.Pow(who + Vector3.up * 1.4f, 1.2f);
                    break;
                case EvType.BrawlEnd:
                    if (Settings.ScreenShake) shake = Mathf.Max(shake, 0.3f);
                    var loser = PlayerPos(st, (int)e.Value);
                    fx.Pow(loser + Vector3.up * 1.2f, 1.4f);
                    fx.Shockwave(loser, Mats.Hex("#ffd23f"), 3.2f);
                    fx.Puff(loser, 14, Color.white, 0.8f, 3f, 0.55f);
                    fx.Confetti(who + Vector3.up * 1.5f, 40);
                    break;
                case EvType.Goal:
                    {
                        if (Settings.ScreenShake) shake = 0.4f;
                        arena.Excite = 1;
                        arena.GoalFlash(Mathf.Clamp((int)e.Value, 0, 1) == 0 ? 1 : -1);
                        int team = Mathf.Clamp((int)e.Value, 0, 1);
                        float gx = team == 0 ? Rink.GoalX : -Rink.GoalX;
                        fx.Confetti(new Vector3(gx, 1.2f, 0), 260, Mats.Hex(TeamColors.All[team].Main));
                        fx.ConfettiRain(new Vector3(gx * 0.6f, 0, 0), 300);
                        fx.Fireworks(new Vector3(gx * 0.5f, 0, 0), 8);
                        fx.Shockwave(new Vector3(gx, 0, 0), Mats.Hex("#ffd23f"), 7f);
                        flash = 0.7f;
                        break;
                    }
                case EvType.Stop: fx.Chips(who + Vector3.up * 0.05f, Vector3.up, 16, 5); break;
                case EvType.Land:
                    fx.Chips(who, Vector3.up, 10, 3);
                    fx.Puff(who, 5, Color.white, 0.4f, 1.6f, 0.35f);
                    if (e.Value > 6) fx.Shockwave(who, Color.white, 1.8f);
                    break;
                case EvType.Dive: fx.Chips(who, Vector3.up, 8, 3); fx.Puff(who, 4, Color.white, 0.3f, 1.5f, 0.3f); break;
                case EvType.PowerUp:
                    {
                        int t = Mathf.Clamp((int)e.Value, 0, 4);
                        fx.Sparks(who + Vector3.up, Mats.Hex(Fx.ItemColors[t]), 40, 5);
                        foreach (var v in views)
                            if (v.Follow == e.Who) { v.Toast = Fx.ItemNames[t].ToUpper() + "!\n" + Fx.ItemHints[t]; v.ToastColor = Mats.Hex(Fx.ItemColors[t]); v.ToastT = 2.4f; }
                        break;
                    }
                case EvType.Freeze:
                    for (int i = 0; i < st.Players.Length && i < roster.Count; i++)
                        if (roster[i].Team != (int)e.Value) fx.Sparks(PlayerPos(st, i, 1), Mats.Hex("#bff3ff"), 20, 3);
                    break;
                case EvType.MegaShot: fx.Sparks(new Vector3(st.PuckX, 0.3f, st.PuckZ), Mats.Hex("#ff6a2e"), 30, 6); break;
                case EvType.Save: fx.Sparks(who + Vector3.up * 0.8f, Color.white, 12, 3); fx.Shockwave(who, Mats.Hex("#5fe1ff"), 2.4f); fx.Pow(who + Vector3.up * 1.2f, 0.6f); break;
                case EvType.Post: fx.Sparks(new Vector3(st.PuckX, 0.6f, st.PuckZ), Color.white, 14, 4); break;
            }
        }

        void Toast(int who, string text, Color c)
        {
            foreach (var v in views)
                if (v.Follow == who) { v.Toast = text; v.ToastColor = c; v.ToastT = 2.4f; }
        }

        void MenuScene(float dt)
        {
            // «Раздевалка» переносит в комнату под ареной: свой игрок сидит на скамье у шкафчика
            bool wantLocker = uiPage == UiPage.Locker && locker != null;
            if (wantLocker != lockerShown) { lockerFade = Mathf.Min(1, lockerFade + Time.unscaledDeltaTime * 3.5f); if (lockerFade >= 1) lockerShown = wantLocker; }
            else lockerFade = Mathf.Max(0, lockerFade - Time.unscaledDeltaTime * 2.5f);
            bool inLocker = lockerShown;
            if (showcase.Count > 0)
            {
                var me = showcase[0].Root;
                me.position = inLocker ? locker.Seat : transform.TransformPoint(ShowPos0);
                me.localRotation = Quaternion.Euler(0, inLocker ? 180 : 200, 0);
            }
            for (int i = 0; i < showcase.Count; i++)
            {
                var c = showcase[i];
                bool sit = inLocker && i == 0;
                var pv = new PlayerView { H = c.Root.localEulerAngles.y * Mathf.Deg2Rad, Save = c.Goalie ? Mathf.Max(0, Mathf.Sin(time * 0.9f) - 0.75f) * 2.4f : 0 };
                bool hasPuckMenu = false;
                if (i == 1)
                {   // живая сцена: синий игрок катается по кругу с шайбой за спинами остальных
                    float ang = time * 0.55f, rx = 5.2f, rz = 2.6f;
                    var pos = new Vector3(1.2f + Mathf.Cos(ang) * rx, 0, 3.4f + Mathf.Sin(ang) * rz);
                    var vel = new Vector3(-Mathf.Sin(ang) * rx, 0, Mathf.Cos(ang) * rz) * 0.55f;
                    c.Root.localPosition = pos;
                    pv.H = Mathf.Atan2(vel.x, vel.z); pv.Spd = vel.magnitude * 2.2f;
                    c.Root.localRotation = Quaternion.Euler(0, pv.H * Mathf.Rad2Deg, 0);
                    hasPuckMenu = true;
                }
                bool dance = !sit && i == 0 && Mathf.Repeat(time, 14) > 11.5f;
                c.Animate(pv, new CharacterRig.AnimCtx { Dt = dt, Time = time + i * 1.7f, Dance = dance, Sit = sit, HasPuck = hasPuckMenu, LookAt = menuCam.transform.position });
            }
            if (!inLocker && Mathf.Repeat(time, 6) < dt) fx.Confetti(new Vector3(0, 0.5f, 0.5f), 80);
            float sway = Mathf.Sin(time * 0.25f) * 0.6f;
            var camT = inLocker ? locker.CamPos + new Vector3(sway * 0.25f, 0, 0) : new Vector3(-2.6f + sway, 1.7f, -6.4f);
            var lookT = inLocker ? locker.CamLook : new Vector3(-1.5f + sway * 0.5f, 1.0f, 0.6f);
            float ck = 1 - Mathf.Exp(-dt * 4);
            bool jump = menuCamPos == Vector3.zero || (menuCamPos - camT).sqrMagnitude > 400;   // между ареной и раздевалкой — склейка
            menuCamPos = jump ? camT : Vector3.Lerp(menuCamPos, camT, ck);
            menuLook = jump ? lookT : Vector3.Lerp(menuLook, lookT, ck);
            menuCam.transform.position = menuCamPos;
            menuCam.transform.LookAt(menuLook);
            menuCam.fieldOfView = inLocker ? 46 : 40;
            fx.FacingCam = menuCam;
            arena.DrawScore(0, 0, 0);
        }

        void UpdateScene(ViewState st, float dt)
        {
            int celebrateTeam = st.Phase == Phase.Goal ? st.LastGoal.Team : st.Phase == Phase.End && st.Score0 != st.Score1 ? (st.Score0 > st.Score1 ? 0 : 1) : -1;
            var puckPos = new Vector3(st.PuckX, st.PuckY, st.PuckZ);
            for (int i = 0; i < chars.Count && i < st.Players.Length; i++)
            {
                var c = chars[i]; var s = st.Players[i];
                c.Root.localPosition = new Vector3(s.X, s.Y, s.Z);
                c.Root.localRotation = Quaternion.Euler(0, s.H * Mathf.Rad2Deg, 0);
                c.Animate(s, new CharacterRig.AnimCtx { HasPuck = st.PuckOwner == i, Celebrate = c.Team == celebrateTeam && !c.Goalie, Sad = celebrateTeam >= 0 && c.Team != celebrateTeam, Dt = dt, Time = time + i, LookAt = puckPos });
                if (rings.TryGetValue(i, out var ring)) ring.localPosition = new Vector3(s.X, 0.015f, s.Z);
                // ледяная крошка из-под коньков на скорости и в нырке
                if (!s.Stun && s.Y < 0.05f && (s.Spd > 8.5f || s.Dive) && Random.value < dt * (s.Dive ? 30 : 6))
                    fx.Chips(new Vector3(s.X, 0.05f, s.Z), -new Vector3(Mathf.Sin(s.H), 0, Mathf.Cos(s.H)), 3, 3);
                if (s.Has(Buff.Turbo) && Random.value < dt * 20) fx.Sparks(new Vector3(s.X, 0.3f, s.Z), Mats.Hex(Fx.ItemColors[0]), 1, 1);
                if ((s.Spd > 9.5f || s.Has(Buff.Turbo)) && !s.Brawl && Random.value < dt * 25)
                    fx.Streak(new Vector3(s.X, 0, s.Z), new Vector3(Mathf.Sin(s.H), 0, Mathf.Cos(s.H)) * s.Spd);
            }
            // облака драк: пары дерущихся рядом друг с другом
            for (int i = 0; i < st.Players.Length; i++)
            {
                bool active = false; Vector3 mid = Vector3.zero;
                if (st.Players[i].Brawl)
                    for (int j = i + 1; j < st.Players.Length; j++)
                        if (st.Players[j].Brawl && Mathf.Abs(st.Players[j].X - st.Players[i].X) + Mathf.Abs(st.Players[j].Z - st.Players[i].Z) < 2.5f)
                        { active = true; mid = (PlayerPos(st, i) + PlayerPos(st, j)) / 2; break; }
                fx.BrawlCloud(i, mid, active, dt);
            }
            if (views.Count > 0) fx.FacingCam = views[0].Cam;
            puck.localPosition = puckPos;
            arena.UpdateNets(puck.position, dt);
            puckFire.gameObject.SetActive(st.PuckMega);
            if (st.PuckMega) puckFire.localScale = Vector3.one * (1 + Mathf.Sin(time * 30) * 0.15f);
            puckRing.localPosition = new Vector3(st.PuckX, 0.012f, st.PuckZ);
            puckRing.gameObject.SetActive(st.PuckOwner < 0);
            fx.UpdateItems(st.Items, time);

            trailPts.Add(puckPos + Vector3.up * 0.05f);
            if (trailPts.Count > 12) trailPts.RemoveAt(0);
            bool fast = trailPts.Count > 4 && (trailPts[trailPts.Count - 1] - trailPts[trailPts.Count - 4]).magnitude > 1.2f && st.PuckOwner < 0;
            if (fast) { trail.positionCount = trailPts.Count; trail.SetPositions(trailPts.ToArray()); }
            else trail.positionCount = 0;
            var tc = st.PuckMega ? Mats.Hex("#ff8a3d") : Mats.Hex("#bff3ff"); tc.a = 0.6f;
            trail.sharedMaterial.color = tc;

            if (st.Phase == Phase.Goal) arena.Excite = Mathf.Max(arena.Excite, 0.9f);
            arena.DrawScore(st.Score0, st.Score1, st.Time);

            foreach (var v in views)
            {
                v.ToastT = Mathf.Max(0, v.ToastT - dt);
                if (v.Follow < 0 || v.Follow >= st.Players.Length) continue;
                var s = st.Players[v.Follow];
                if (!v.Init) v.Yaw = s.H;
                // авто-камера: если игрок не крутит камеру сам, она плавно заходит за спину
                bool mouseDriven = v.Src != null && v.Src.Mouse && !autotest;
                bool auto = Settings.AutoCamera && time - v.LastManual > (mouseDriven ? 2.5f : 0.6f) && s.Spd > 2.5f && !s.Stun;
                if (auto || autotest) v.Yaw += Sim.Wrap(s.H - v.Yaw) * (1 - Mathf.Exp(-dt * (mouseDriven ? 1.2f : 2.4f)));
                float dist = (5.8f + Mathf.Min(s.Spd, 10) * 0.12f) * Settings.CamDistance * (s.Has(Buff.Giant) ? 1.3f : 1);
                float fx2 = Mathf.Sin(v.Yaw), fz = Mathf.Cos(v.Yaw);
                // наклон камеры: выше 0.06 — камера поднимается над игроком; ниже — остаётся низко,
                // а точка взгляда поднимается: прицел уходит вверх, к перекладине и «девяткам»
                float pe = Mathf.Max(v.Pitch, 0.06f), lift = Mathf.Max(0, 0.06f - v.Pitch) * 4.5f;
                float horiz = dist * Mathf.Cos(pe);
                float cx = Mathf.Clamp(s.X - fx2 * horiz, -Rink.L / 2 - 3.2f, Rink.L / 2 + 3.2f);
                float cz = Mathf.Clamp(s.Z - fz * horiz, -Rink.W / 2 - 3.2f, Rink.W / 2 + 3.2f);
                var target = new Vector3(cx, 1.3f + dist * Mathf.Sin(pe) + s.Y * 0.5f, cz);
                var look = new Vector3(s.X + fx2 * 2.2f, 1.1f + lift + s.Y * 0.5f, s.Z + fz * 2.2f);
                // гол: камера облетает автора гола спереди — показываем празднование
                int scorer = st.LastGoal != null ? st.LastGoal.Scorer : -1;
                bool goalShow = st.Phase == Phase.Goal && scorer >= 0 && scorer < st.Players.Length && st.PhaseT > 0.5f;
                v.GoalW = Mathf.Lerp(v.GoalW, goalShow ? 1 : 0, 1 - Mathf.Exp(-dt * (goalShow ? 3.5f : 6)));
                if (v.GoalW > 0.01f && scorer >= 0 && scorer < st.Players.Length)
                {
                    var sc = st.Players[scorer];
                    var sf = new Vector3(Mathf.Sin(sc.H), 0, Mathf.Cos(sc.H)); var sr = new Vector3(sf.z, 0, -sf.x);
                    var sp0 = new Vector3(sc.X, sc.Y, sc.Z);
                    var gpos = sp0 + sf * 3.6f + sr * Mathf.Sin(time * 0.7f) * 1.6f + Vector3.up * 1.5f;
                    gpos.x = Mathf.Clamp(gpos.x, -Rink.L / 2 + 0.8f, Rink.L / 2 - 0.8f); gpos.z = Mathf.Clamp(gpos.z, -Rink.W / 2 + 0.8f, Rink.W / 2 - 0.8f);
                    target = Vector3.Lerp(target, gpos, v.GoalW);
                    look = Vector3.Lerp(look, sp0 + Vector3.up * 0.95f, v.GoalW);
                }
                // с шайбой камера уходит за правое плечо (клюшка и шайба слева): игрок смещается влево на экране
                // и не загораживает ворота и прицел — как прицеливание «из-за плеча» в играх от третьего лица
                bool aiming = st.PuckOwner == v.Follow || s.Charge > 0.02f;
                v.Side = Mathf.Lerp(v.Side, aiming ? 1 : 0, 1 - Mathf.Exp(-dt * 5));
                var sideOff = new Vector3(fz, 0, -fx2) * (1.15f * v.Side) + Vector3.up * (0.25f * v.Side);
                target += sideOff; look += sideOff;
                if (!v.Init) { v.Pos = target; v.Look = look; v.Init = true; }
                v.Pos = Vector3.Lerp(v.Pos, target, 1 - Mathf.Exp(-dt * 10));
                v.Look = Vector3.Lerp(v.Look, look, 1 - Mathf.Exp(-dt * 12));
                var pos = v.Pos;
                if (shake > 0) pos += new Vector3(Random.Range(-0.5f, 0.5f), Random.Range(-0.5f, 0.5f), 0) * shake;
                v.Cam.transform.position = pos;
                v.Cam.transform.LookAt(v.Look);
                v.Cam.fieldOfView = Mathf.Lerp(v.Cam.fieldOfView, Settings.Fov + (s.Has(Buff.Turbo) ? 8 : 0) + Mathf.Max(0, s.Spd - 8) * 1.2f, 1 - Mathf.Exp(-dt * 4));
                var pfx = v.Cam.GetComponent<PostFx>();
                if (pfx != null)
                {
                    float sp = Mathf.Clamp01((s.Spd - 8.5f) / 4f) + (s.Has(Buff.Turbo) ? 0.4f : 0);
                    pfx.Speed = Mathf.Lerp(pfx.Speed, Mathf.Clamp01(sp), 1 - Mathf.Exp(-dt * 5));
                    pfx.ExtraBloom = Mathf.Lerp(pfx.ExtraBloom, flash > 0 ? 0.35f : 0, 1 - Mathf.Exp(-dt * 6));
                }
            }
            shake = Mathf.Max(0, shake - dt * 1.5f);
        }
    }
}
