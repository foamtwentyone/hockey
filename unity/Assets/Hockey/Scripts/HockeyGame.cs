// Главный скрипт: меню, лобби, режимы игры, игровой цикл, камеры «из-за спины», HUD.
// Создаётся автоматически в любой сцене — достаточно нажать Play.
using System.Collections.Generic;
using UnityEngine;

namespace Hockey
{
    public partial class HockeyGame : MonoBehaviour
    {
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
        enum UiScreen { Menu, Lobby, Match }

        class View
        {
            public Camera Cam; public int Follow = -1; public float Yaw; public bool Init; public Vector3 Pos, Look;
            public Rect Rect;
        }

        // ---------- состояние ----------
        Mode mode = Mode.None;
        UiScreen screen = UiScreen.Menu;
        Sim sim, demo;
        readonly ViewState simView = new ViewState(), netView = new ViewState();
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
        bool paused, showHelp, endShown; float endTimer = -1;
        string message = "";
        string playerName, address;
        int splitMode;

        // ---------- рендер ----------
        Arena arena;
        GameAudio audioFx;
        readonly List<CharacterRig> chars = new List<CharacterRig>();
        readonly List<GameObject> extras = new List<GameObject>();
        readonly Dictionary<int, Transform> rings = new Dictionary<int, Transform>();
        Transform puck, puckRing;
        LineRenderer trail;
        readonly List<Vector3> trailPts = new List<Vector3>();
        readonly List<View> views = new List<View>();
        Camera menuCam;
        float shake, time;

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

            playerName = PlayerPrefs.GetString("hockey.name", "Игрок");
            address = PlayerPrefs.GetString("hockey.addr", "192.168.1.");
            cfg.Skaters = PlayerPrefs.GetInt("hockey.skaters", 3);
            cfg.Duration = PlayerPrefs.GetInt("hockey.duration", 180);
            cfg.Difficulty = PlayerPrefs.GetInt("hockey.diff", 1);

            arena = new Arena(transform);
            audioFx = new GameAudio(gameObject);
            BuildPuck();
            menuCam = MakeCamera("MenuCam");
            menuCam.fieldOfView = 50;
            finder = new LanFinder();
            StartAutotestIfRequested();
        }

        Camera MakeCamera(string name)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            var c = go.AddComponent<Camera>();
            c.clearFlags = CameraClearFlags.SolidColor;
            c.backgroundColor = Mats.Hex("#1a2033");
            c.fieldOfView = 62; c.nearClipPlane = 0.1f; c.farClipPlane = 300;
            c.allowMSAA = true;
            return c;
        }

        void BuildPuck()
        {
            var go = new GameObject("Puck");
            go.transform.SetParent(transform, false);
            var body = new GameObject("body");
            body.transform.SetParent(go.transform, false);
            body.transform.localPosition = new Vector3(0, 0.035f, 0);
            body.AddComponent<MeshFilter>().sharedMesh = MeshKit.Cylinder(0.13f, 0.07f, 20);
            body.AddComponent<MeshRenderer>().sharedMaterial = Mats.Get("#141418", 0.6f);
            puck = go.transform;

            var ring = new GameObject("PuckRing");
            ring.transform.SetParent(transform, false);
            ring.AddComponent<MeshFilter>().sharedMesh = MeshKit.FlatRing(0.28f, 0.36f);
            var rr = ring.AddComponent<MeshRenderer>();
            rr.sharedMaterial = Mats.Fade("#5fe1ff", 0.6f, true);
            rr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            puckRing = ring.transform;

            var tr = new GameObject("Trail");
            tr.transform.SetParent(transform, false);
            trail = tr.AddComponent<LineRenderer>();
            trail.sharedMaterial = Mats.Fade("#bff3ff", 0.55f, true);
            trail.widthMultiplier = 0.07f;
            trail.positionCount = 0;
            trail.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
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
            lobby = new List<LobbyEntry> { new LobbyEntry { Key = "local0", Name = MyName, Team = 0 } };
            if (!split)
            {
                sources.Add(new InputSource { Key = "local0", Scheme = Scheme.Solo, Pad = 0, Mouse = true });
            }
            else
            {
                lobby.Add(new LobbyEntry { Key = "local1", Name = "Игрок 2", Team = splitMode == 1 ? 0 : 1 });
                sources.Add(new InputSource { Key = "local0", Scheme = Scheme.P1, Pad = 0 });
                sources.Add(new InputSource { Key = "local1", Scheme = Scheme.P2, Pad = 1 });
            }
            foreach (var s in sources) myKeys.Add(s.Key);
            NewMatch();
        }

        void NewMatch()
        {
            var humans = new List<HumanSlot>();
            foreach (var l in lobby) humans.Add(new HumanSlot(l.Key, l.Name, l.Team));
            sim = new Sim(new MatchConfig { Skaters = cfg.Skaters, Duration = cfg.Duration, Difficulty = cfg.Difficulty }, humans);
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
            demo = null;
            ApplyRoster(r, true);
            screen = UiScreen.Match;
            paused = false; endShown = false; endTimer = -1;
            message = "";
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
                    var v = new View { Cam = MakeCamera("ViewCam" + i), Follow = follow[i] };
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
                    ring.AddComponent<MeshFilter>().sharedMesh = MeshKit.FlatRing(0.52f, 0.66f);
                    var mr = ring.AddComponent<MeshRenderer>();
                    mr.sharedMaterial = Mats.Fade(col, 0.85f, true);
                    mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                    rings[i] = ring.transform;
                    extras.Add(ring);
                    var arrow = new GameObject("Arrow");
                    arrow.transform.SetParent(c.Root, false);
                    arrow.transform.localPosition = new Vector3(0, 2.75f, 0);
                    arrow.AddComponent<MeshFilter>().sharedMesh = MeshKit.Cone(0.16f, 0.3f);
                    var amr = arrow.AddComponent<MeshRenderer>();
                    amr.sharedMaterial = Mats.Emissive(col, 1f);
                    amr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                }
            }
        }

        string SlotColor(string human) => SlotColors[(slots.TryGetValue(human, out int s) ? s : 0) % SlotColors.Length];

        void ToMenu(string msg = "")
        {
            CloseNet();
            mode = Mode.None;
            screen = UiScreen.Menu;
            sim = null; view = null;
            snaps.Clear();
            foreach (var v in views) Destroy(v.Cam.gameObject);
            views.Clear();
            menuCam.gameObject.SetActive(true);
            paused = false;
            message = msg;
            finder ??= new LanFinder();
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
            lobby = new List<LobbyEntry> { new LobbyEntry { Key = "local0", Name = MyName, Team = 0 } };
            session.InMatch = false; session.Seq++;
            host.InMatch = false;
            host.GetSession = () =>
            {
                session.Lobby = lobby; session.Cfg = cfg;
                host.PlayerCount = lobby.Count;
                return session;
            };
            host.OnJoin += (key, name) =>
            {
                int t0 = lobby.FindAll(l => l.Team == 0).Count, t1 = lobby.Count - t0;
                var entry = new LobbyEntry { Key = key, Name = name, Team = t0 <= t1 ? 0 : 1 };
                lobby.Add(entry);
                if (sim != null && screen == UiScreen.Match)
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
                if (sim != null && screen == UiScreen.Match) { sim.HumanLeft(key); RefreshHostRoster(); }
                session.Seq++;
                host.BroadcastSession();
            };
            host.OnSetTeam += (key, team) =>
            {
                if (screen == UiScreen.Match) return;
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
            try { client = new ClientNet(addr, MyName, Time.realtimeSinceStartupAsDouble); }
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
                if (client.MyKey != null && (myKeys.Count == 0 || myKeys[0] != client.MyKey)) { myKeys.Clear(); myKeys.Add(client.MyKey); }
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
                foreach (var e in evs) HandleEvent(e);
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
            var o = interp;
            o.Resize(b.Players.Length);
            for (int i = 0; i < b.Players.Length; i++)
            {
                var pb = b.Players[i]; var po = o.Players[i];
                var pa = i < a.Players.Length ? a.Players[i] : pb;
                bool tele = Mathf.Abs(pa.X - pb.X) + Mathf.Abs(pa.Z - pb.Z) > 4;
                float kk = tele ? 1 : k;
                po.X = Mathf.Lerp(pa.X, pb.X, kk); po.Z = Mathf.Lerp(pa.Z, pb.Z, kk);
                po.H = pa.H + Sim.Wrap(pb.H - pa.H) * kk;
                po.Spd = Mathf.Lerp(pa.Spd, pb.Spd, kk); po.Charge = Mathf.Lerp(pa.Charge, pb.Charge, kk);
                po.Stun = pb.Stun; po.Back = pb.Back; po.Swing = pb.Swing; po.Save = pb.Save; po.Stamina = pb.Stamina;
                po.Goals = pb.Goals; po.Assists = pb.Assists;
            }
            bool ptele = Mathf.Abs(a.PuckX - b.PuckX) + Mathf.Abs(a.PuckZ - b.PuckZ) > 4;
            float pk = ptele ? 1 : k;
            o.PuckX = Mathf.Lerp(a.PuckX, b.PuckX, pk); o.PuckY = Mathf.Lerp(a.PuckY, b.PuckY, pk); o.PuckZ = Mathf.Lerp(a.PuckZ, b.PuckZ, pk);
            o.PuckOwner = b.PuckOwner;
            o.Score0 = b.Score0; o.Score1 = b.Score1; o.Time = b.Time; o.Phase = b.Phase; o.PhaseT = b.PhaseT;
            o.Overtime = b.Overtime; o.LastGoal = b.LastGoal; o.SimTime = b.SimTime;
            return o;
        }

        // ================================================================ цикл
        void Update()
        {
            float dt = Mathf.Min(0.05f, Time.unscaledDeltaTime);
            time += dt;
            double now = Time.realtimeSinceStartupAsDouble;

            if (screen == UiScreen.Match && GameInput.KeyDown(KeyCode.Escape) && !endShown) paused = !paused;
            if (screen == UiScreen.Match && GameInput.KeyDown(KeyCode.H)) showHelp = !showHelp;
            if (screen == UiScreen.Match && GameInput.KeyDown(KeyCode.M)) audioFx.Muted = !audioFx.Muted;

            finder?.Update(now);
            host?.Update(now);
            if (client != null)
            {
                var inp = paused || screen != UiScreen.Match ? new PlayerInput() : GameInput.Read(sources.Count > 0 ? sources[0] : null);
                client.Update(now, inp);
                if (client != null && client.Error != null) { ToMenu(client.Error); }
            }

            view = null;
            if ((mode == Mode.Local || mode == Mode.Host) && sim != null && screen == UiScreen.Match)
            {
                if (!(paused && mode == Mode.Local) && !autoFreeze)
                {
                    acc += Mathf.Min(0.25f, Time.unscaledDeltaTime);
                    var inputs = new Dictionary<string, PlayerInput>();
                    while (acc >= Step)
                    {
                        inputs.Clear();
                        if (host != null) foreach (var k in host.ClientKeys) if (host.TryGetInput(k, out var ri)) inputs[k] = ri;
                        foreach (var s in sources) inputs[s.Key] = paused ? new PlayerInput() : ReadSource(s);
                        sim.Step(Step, inputs);
                        acc -= Step;
                        steps++;
                        foreach (var e in sim.Events) HandleEvent(e);
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
                }
                sim.FillView(simView);
                view = simView;
            }
            else if (mode == Mode.Client && screen == UiScreen.Match)
            {
                view = ClientState();
            }

            if (view != null && screen == UiScreen.Match && chars.Count == view.Players.Length)
            {
                UpdateScene(view, dt);
                if (view.Phase == Phase.End && !endShown)
                {
                    if (endTimer < 0) endTimer = 1.2f;
                    endTimer -= dt;
                    if (endTimer <= 0) { endShown = true; paused = false; }
                }
            }
            else if (screen != UiScreen.Match)
            {
                MenuDemo(dt);
            }
            arena.Update(dt, time);
            audioFx.Update(dt);
        }

        void HandleEvent(GameEvent e)
        {
            audioFx.Event(e);
            if (e.Type == EvType.Hit) shake = Mathf.Max(shake, 0.25f);
            if (e.Type == EvType.Goal) { shake = 0.4f; arena.Excite = 1; }
        }

        void MenuDemo(float dt)
        {
            if (demo == null || demo.Phase == Phase.End)
            {
                demo = new Sim(new MatchConfig { Skaters = 3, Duration = 180, Difficulty = 2 }, null);
                foreach (var v in views) Destroy(v.Cam.gameObject);
                views.Clear();
                myKeys.Clear();
                ApplyRoster(demo.Roster(), true);
            }
            demo.Step(dt, null);
            foreach (var e in demo.Events) if (e.Type == EvType.Goal) arena.Excite = 1;
            demo.Events.Clear();
            demo.FillView(simView);
            UpdateScene(simView, dt);
            float a = time * 0.08f;
            menuCam.transform.position = new Vector3(Mathf.Sin(a) * 34, 15, Mathf.Cos(a) * 26);
            menuCam.transform.LookAt(new Vector3(simView.PuckX * 0.4f, 0, simView.PuckZ * 0.4f));
        }

        void UpdateScene(ViewState st, float dt)
        {
            int celebrateTeam = st.Phase == Phase.Goal ? st.LastGoal.Team : -1;
            for (int i = 0; i < chars.Count && i < st.Players.Length; i++)
            {
                var c = chars[i]; var s = st.Players[i];
                c.Root.localPosition = new Vector3(s.X, 0, s.Z);
                c.Root.localRotation = Quaternion.Euler(0, s.H * Mathf.Rad2Deg, 0);
                c.Animate(s, st.PuckOwner == i, c.Team == celebrateTeam && !c.Goalie, dt, time);
                if (rings.TryGetValue(i, out var ring)) ring.localPosition = new Vector3(s.X, 0.015f, s.Z);
            }
            puck.localPosition = new Vector3(st.PuckX, st.PuckY, st.PuckZ);
            puckRing.localPosition = new Vector3(st.PuckX, 0.012f, st.PuckZ);
            puckRing.gameObject.SetActive(st.PuckOwner < 0);

            trailPts.Add(new Vector3(st.PuckX, st.PuckY + 0.04f, st.PuckZ));
            if (trailPts.Count > 12) trailPts.RemoveAt(0);
            bool fast = trailPts.Count > 4 && (trailPts[trailPts.Count - 1] - trailPts[trailPts.Count - 4]).magnitude > 1.2f && st.PuckOwner < 0;
            if (fast) { trail.positionCount = trailPts.Count; trail.SetPositions(trailPts.ToArray()); }
            else trail.positionCount = 0;

            if (st.Phase == Phase.Goal) arena.Excite = Mathf.Max(arena.Excite, 0.9f);
            arena.DrawScore(st.Score0, st.Score1, st.Time);

            foreach (var v in views)
            {
                if (v.Follow < 0 || v.Follow >= st.Players.Length) continue;
                var s = st.Players[v.Follow];
                if (!v.Init) v.Yaw = s.H;
                v.Yaw += Sim.Wrap(s.H - v.Yaw) * (1 - Mathf.Exp(-dt * 3.2f));
                float dist = 6.4f + Mathf.Min(s.Spd, 10) * 0.12f;
                float fx = Mathf.Sin(v.Yaw), fz = Mathf.Cos(v.Yaw);
                float cx = Mathf.Clamp(s.X - fx * dist, -Rink.L / 2 - 3.2f, Rink.L / 2 + 3.2f);
                float cz = Mathf.Clamp(s.Z - fz * dist, -Rink.W / 2 - 3.2f, Rink.W / 2 + 3.2f);
                var target = new Vector3(cx, 3.4f, cz);
                var look = new Vector3(s.X + fx * 3.5f, 0.9f, s.Z + fz * 3.5f);
                if (!v.Init) { v.Pos = target; v.Look = look; v.Init = true; }
                v.Pos = Vector3.Lerp(v.Pos, target, 1 - Mathf.Exp(-dt * 8));
                v.Look = Vector3.Lerp(v.Look, look, 1 - Mathf.Exp(-dt * 10));
                var pos = v.Pos;
                if (shake > 0) pos += new Vector3(Random.Range(-0.5f, 0.5f), Random.Range(-0.5f, 0.5f), 0) * shake;
                v.Cam.transform.position = pos;
                v.Cam.transform.LookAt(v.Look);
            }
            shake = Mathf.Max(0, shake - dt * 1.5f);
        }

        // ================================================================ интерфейс (IMGUI)
        float uiScale, VW, VH;
        GUIStyle sPanel, sTitle, sH2, sLabel, sSmall, sField, sBanner, sBannerSmall, sCenter, sTag, sBig, sScore, sCode;
        readonly Dictionary<string, GUIStyle> btnStyles = new Dictionary<string, GUIStyle>();
        readonly Dictionary<string, Texture2D> texCache = new Dictionary<string, Texture2D>();
        Texture2D white;
        bool stylesReady;

        Texture2D T(string hex, float alpha = 1)
        {
            string key = hex + alpha;
            if (texCache.TryGetValue(key, out var t)) return t;
            var c = Mats.Hex(hex); c.a = alpha;
            t = Tex.Solid(c);
            texCache[key] = t;
            return t;
        }

        void InitStyles()
        {
            if (stylesReady) return;
            stylesReady = true;
            white = Tex.Solid(Color.white);
            sPanel = new GUIStyle { padding = new RectOffset(28, 28, 22, 22) };
            sPanel.normal.background = T("#24325a", 0.96f);
            sTitle = new GUIStyle(GUI.skin.label) { fontSize = 52, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter, richText = true };
            sTitle.normal.textColor = Color.white;
            sH2 = new GUIStyle(sTitle) { fontSize = 34 };
            sLabel = new GUIStyle(GUI.skin.label) { fontSize = 19, richText = true, wordWrap = true };
            sLabel.normal.textColor = Mats.Hex("#dbe5ff");
            sSmall = new GUIStyle(sLabel) { fontSize = 15 };
            sSmall.normal.textColor = Mats.Hex("#a9b8de");
            sCenter = new GUIStyle(sLabel) { alignment = TextAnchor.MiddleCenter };
            sField = new GUIStyle(GUI.skin.textField) { fontSize = 21, padding = new RectOffset(12, 12, 8, 8), fixedHeight = 42 };
            sField.normal.background = T("#131a30"); sField.focused.background = T("#0d1326"); sField.hover.background = T("#131a30");
            sField.normal.textColor = sField.focused.textColor = sField.hover.textColor = Color.white;
            sBanner = new GUIStyle(sTitle) { fontSize = 96 };
            sBannerSmall = new GUIStyle(sTitle) { fontSize = 28 };
            sTag = new GUIStyle(sLabel) { fontSize = 16, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter, wordWrap = false, padding = new RectOffset(10, 10, 3, 3) };
            sTag.normal.background = T("#141824", 0.75f);
            sTag.normal.textColor = Color.white;
            sBig = new GUIStyle(sLabel) { fontSize = 26, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            sScore = new GUIStyle(sBig) { fontSize = 34 };
            sScore.normal.textColor = Color.white;
            sCode = new GUIStyle(sBig) { fontSize = 30 };
            sCode.normal.textColor = Mats.Hex("#ffd23f");
        }

        GUIStyle Btn(string hex, int size = 20)
        {
            string key = hex + size;
            if (btnStyles.TryGetValue(key, out var s)) return s;
            var c = Mats.Hex(hex);
            s = new GUIStyle(GUI.skin.button) { fontSize = size, fontStyle = FontStyle.Bold, padding = new RectOffset(14, 14, 10, 10), margin = new RectOffset(4, 4, 5, 5), wordWrap = true };
            s.normal.background = T(hex); s.hover.background = Tex.Solid(Color.Lerp(c, Color.white, 0.15f)); s.active.background = Tex.Solid(Color.Lerp(c, Color.black, 0.2f));
            s.onNormal.background = Tex.Solid(Color.Lerp(c, Color.white, 0.3f));
            s.focused.background = T(hex);
            s.normal.textColor = s.hover.textColor = s.active.textColor = s.focused.textColor = s.onNormal.textColor = Color.white;
            btnStyles[key] = s;
            return s;
        }

        void OnGUI()
        {
            InitStyles();
            uiScale = Screen.height / 900f;
            VW = Screen.width / uiScale; VH = 900;
            GUI.matrix = Matrix4x4.Scale(new Vector3(uiScale, uiScale, 1));

            switch (screen)
            {
                case UiScreen.Menu: DrawMenu(); break;
                case UiScreen.Lobby: DrawLobby(); break;
                case UiScreen.Match:
                    if (view != null) DrawHud(view);
                    if (endShown) DrawEnd();
                    else if (paused) DrawPause();
                    break;
            }
        }

        void Dim() { GUI.color = new Color(0.03f, 0.05f, 0.1f, 0.55f); GUI.DrawTexture(new Rect(0, 0, VW, VH), white); GUI.color = Color.white; }

        Vector2 menuScroll;
        bool menuHelp;

        void DrawMenu()
        {
            Dim();
            float w = 640;
            GUILayout.BeginArea(new Rect((VW - w) / 2, 30, w, VH - 60), sPanel);
            menuScroll = GUILayout.BeginScrollView(menuScroll);
            GUILayout.Label("ДВОРОВЫЙ\n<color=#7fd2ff>ХОККЕЙ 3D</color>", sTitle);
            GUILayout.Space(8);
            GUILayout.Label("Твоё имя", sSmall);
            playerName = GUILayout.TextField(playerName ?? "", 14, sField);
            GUILayout.Space(6);

            GUILayout.Label("Игроков в команде", sSmall);
            cfg.Skaters = 1 + GUILayout.SelectionGrid(cfg.Skaters - 1, new[] { "1", "2", "3", "4", "5" }, 5, Btn("#34406a", 18));
            int[] durs = { 120, 180, 300, 480 };
            int di = System.Array.IndexOf(durs, cfg.Duration); if (di < 0) di = 1;
            GUILayout.Label("Время матча", sSmall);
            cfg.Duration = durs[GUILayout.SelectionGrid(di, new[] { "2 мин", "3 мин", "5 мин", "8 мин" }, 4, Btn("#34406a", 18))];
            GUILayout.Label("Боты", sSmall);
            cfg.Difficulty = GUILayout.SelectionGrid(cfg.Difficulty, new[] { "Легко", "Нормально", "Сложно" }, 3, Btn("#34406a", 18));
            GUILayout.Space(12);

            if (GUILayout.Button("Играть против ботов", Btn("#e2483d", 24))) StartLocal(false);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Вдвоём на одном экране", Btn("#4766b8"), GUILayout.Width(330))) StartLocal(true);
            splitMode = GUILayout.SelectionGrid(splitMode, new[] { "друг против друга", "в одной команде" }, 1, Btn("#34406a", 15));
            GUILayout.EndHorizontal();
            GUILayout.Space(8);
            if (GUILayout.Button("Создать сетевой матч", Btn("#2fae6a", 22))) StartHost();
            GUILayout.BeginHorizontal();
            address = GUILayout.TextField(address ?? "", 60, sField, GUILayout.Width(330));
            if (GUILayout.Button("Подключиться по IP", Btn("#2fae6a", 18))) StartClient(address);
            GUILayout.EndHorizontal();

            if (finder != null && finder.Games.Count > 0)
            {
                GUILayout.Label("Матчи в локальной сети:", sSmall);
                foreach (var g in finder.Games)
                {
                    GUILayout.BeginHorizontal();
                    GUILayout.Label($"<b>{g.Name}</b>  · игроков: {g.Players}{(g.InMatch ? " · идёт матч" : "")}", sLabel);
                    if (GUILayout.Button("Войти", Btn("#2fae6a", 17), GUILayout.Width(120))) { address = g.Address; StartClient(g.Address); }
                    GUILayout.EndHorizontal();
                }
            }
            else GUILayout.Label("Матчи в локальной сети появятся здесь автоматически.", sSmall);

            if (!string.IsNullOrEmpty(message)) GUILayout.Label($"<color=#ffd23f><b>{message}</b></color>", sCenter);
            GUILayout.Space(6);
            menuHelp = GUILayout.Toggle(menuHelp, menuHelp ? "▼ Управление" : "► Управление", Btn("#34406a", 17));
            if (menuHelp) GUILayout.Label(ControlsText, sSmall);
            GUILayout.EndScrollView();
            GUILayout.EndArea();
        }

        const string ControlsText =
            "<b>Один игрок / по сети</b>\n" +
            "W/S или ↑/↓ — разгон / торможение, задний ход\n" +
            "A/D или ←/→ — поворот\n" +
            "Пробел или ЛКМ — бросок (держи — сильнее)\n" +
            "Q или ПКМ — пас партнёру,  E — силовой приём,  Shift — ускорение\n" +
            "<b>Игрок 2 на одном экране</b>\n" +
            "Стрелки — движение, Enter / Num0 — бросок, правый Shift / Num1 — пас,\n" +
            "правый Ctrl / Num2 — силовой, / или Num3 — ускорение\n" +
            "<b>Геймпад</b>: стик и курки — движение, A — бросок, B/Y — пас, X — силовой, LB/RB — ускорение\n" +
            "Esc — пауза,  H — подсказка,  M — звук";

        Vector2 lobbyScroll;
        List<string> myIps;

        void DrawLobby()
        {
            Dim();
            float w = 680;
            GUILayout.BeginArea(new Rect((VW - w) / 2, 40, w, VH - 80), sPanel);
            lobbyScroll = GUILayout.BeginScrollView(lobbyScroll);
            GUILayout.Label("Сетевой матч", sH2);
            if (mode == Mode.Host)
            {
                myIps ??= NetUtil.LocalAddresses();
                GUILayout.Label("Друзья подключаются по адресу:", sCenter);
                foreach (var ip in myIps) GUILayout.Label(ip + ":" + NetConst.GamePort, sCode);
                GUILayout.Label("В одной Wi-Fi сети матч найдётся сам. Через интернет: Radmin VPN / ZeroTier / Tailscale или проброс порта 7777 (UDP) на роутере.", sSmall);
            }
            else
            {
                string st = client == null ? "" : client.Connected ? "Подключено. Ждём, пока хост начнёт матч…" : "Подключаемся к " + address + "…";
                GUILayout.Label(st, sCenter);
            }
            GUILayout.Space(10);
            GUILayout.BeginHorizontal();
            for (int t = 0; t < 2; t++)
            {
                GUILayout.BeginVertical(GUILayout.Width((w - 70) / 2));
                GUILayout.Label($"<color={(t == 0 ? "#ff7a6f" : "#7fb5ff")}><b>{Rink.TeamNames[t]}</b></color>", sBig);
                int count = 0;
                foreach (var l in lobby.ToArray())
                {
                    if (l.Team != t) continue;
                    count++;
                    bool me = mode == Mode.Host ? l.Key == "local0" : client != null && l.Key == client.MyKey;
                    string label = l.Name + (l.Key == "local0" ? " ★" : "") + (me ? "  (ты)" : "");
                    if (GUILayout.Button(label, Btn(t == 0 ? "#a8352d" : "#2d5fa8", 18)))
                    {
                        if (mode == Mode.Host) { l.Team = 1 - l.Team; session.Seq++; host.BroadcastSession(); }
                        else if (me) client.SetTeam(1 - l.Team);
                    }
                }
                for (int i = count; i < cfg.Skaters; i++) GUILayout.Label("<i>бот</i>", sCenter);
                GUILayout.EndVertical();
            }
            GUILayout.EndHorizontal();
            GUILayout.Label("Свободные места займут боты. Нажми на игрока, чтобы перевести его в другую команду.", sSmall);
            GUILayout.Space(8);
            if (mode == Mode.Host)
            {
                if (GUILayout.Button("Начать матч", Btn("#e2483d", 24))) NewMatch();
            }
            else if (client != null && client.Connected)
            {
                var me = lobby.Find(l => l.Key == client.MyKey);
                if (me != null && GUILayout.Button("Сменить команду", Btn("#4766b8"))) client.SetTeam(1 - me.Team);
            }
            if (GUILayout.Button("Выйти", Btn("#34406a"))) { myIps = null; ToMenu(); }
            GUILayout.EndScrollView();
            GUILayout.EndArea();
        }

        void DrawPause()
        {
            Dim();
            float w = 380;
            GUILayout.BeginArea(new Rect((VW - w) / 2, VH / 2 - 140, w, 280), sPanel);
            GUILayout.Label("Пауза", sH2);
            if (mode != Mode.Local) GUILayout.Label("В сетевом матче игра не останавливается", sSmall);
            if (GUILayout.Button("Продолжить", Btn("#e2483d", 22))) paused = false;
            if (GUILayout.Button("Выйти в меню", Btn("#34406a"))) ToMenu();
            GUILayout.EndArea();
        }

        void DrawEnd()
        {
            Dim();
            var v = view;
            if (v == null) return;
            float w = 600;
            GUILayout.BeginArea(new Rect((VW - w) / 2, 60, w, VH - 120), sPanel);
            string title = "Ничья";
            if (v.Score0 != v.Score1)
            {
                int win = v.Score0 > v.Score1 ? 0 : 1;
                title = "Победа: " + Rink.TeamNames[win] + "!";
                var myTeams = new HashSet<int>();
                foreach (var k in myKeys) { var e = roster.Find(r => r.Human == k); if (e != null) myTeams.Add(e.Team); }
                if (myTeams.Count == 1) title = myTeams.Contains(win) ? "ПОБЕДА!" : "Поражение…";
            }
            GUILayout.Label(title, sH2);
            GUILayout.Label($"<color=#ff7a6f>{v.Score0}</color> : <color=#7fb5ff>{v.Score1}</color>", new GUIStyle(sTitle) { fontSize = 56 });
            var rows = new List<int>();
            for (int i = 0; i < roster.Count && i < v.Players.Length; i++) if (!roster[i].Goalie) rows.Add(i);
            rows.Sort((a, b) => (v.Players[b].Goals * 2 + v.Players[b].Assists).CompareTo(v.Players[a].Goals * 2 + v.Players[a].Assists));
            GUILayout.BeginHorizontal();
            GUILayout.Label("Игрок", sSmall, GUILayout.Width(300)); GUILayout.Label("Голы", sSmall, GUILayout.Width(100)); GUILayout.Label("Пасы", sSmall);
            GUILayout.EndHorizontal();
            foreach (int i in rows)
            {
                var r = roster[i];
                string col = r.Team == 0 ? "#ff7a6f" : "#7fb5ff";
                string nm = r.Human != null ? $"<b>{r.Name}</b>" : r.Name;
                GUILayout.BeginHorizontal();
                GUILayout.Label($"<color={col}>■</color> {nm}", sLabel, GUILayout.Width(300));
                GUILayout.Label(v.Players[i].Goals.ToString(), sLabel, GUILayout.Width(100));
                GUILayout.Label(v.Players[i].Assists.ToString(), sLabel);
                GUILayout.EndHorizontal();
            }
            GUILayout.FlexibleSpace();
            if (mode != Mode.Client)
            {
                if (GUILayout.Button("Ещё матч", Btn("#e2483d", 22))) NewMatch();
            }
            else GUILayout.Label("Хост может начать новый матч", sCenter);
            if (GUILayout.Button("В меню", Btn("#34406a"))) ToMenu();
            GUILayout.EndArea();
        }

        void Box(Rect r, string hex, float a = 1) { GUI.color = WithA(Mats.Hex(hex), a); GUI.DrawTexture(r, white); GUI.color = Color.white; }
        static Color WithA(Color c, float a) { c.a = a; return c; }

        void DrawHud(ViewState st)
        {
            // табло
            float sbW = 460, sbX = (VW - sbW) / 2, sbY = 12;
            Box(new Rect(sbX, sbY, 175, 56), "#e2483d");
            Box(new Rect(sbX + 175, sbY, 110, 56), "#151b2e");
            Box(new Rect(sbX + 285, sbY, 175, 56), "#3f86e8");
            GUI.Label(new Rect(sbX + 10, sbY, 110, 56), "КРАСНЫЕ", new GUIStyle(sBig) { fontSize = 17 });
            GUI.Label(new Rect(sbX + 120, sbY, 50, 56), st.Score0.ToString(), sScore);
            GUI.Label(new Rect(sbX + 290, sbY, 50, 56), st.Score1.ToString(), sScore);
            GUI.Label(new Rect(sbX + 340, sbY, 115, 56), "СИНИЕ", new GUIStyle(sBig) { fontSize = 17 });
            int tt = Mathf.Max(0, Mathf.CeilToInt(st.Time));
            GUI.Label(new Rect(sbX + 175, sbY - 6, 110, 56), st.Overtime ? "∞" : $"{tt / 60}:{tt % 60:00}", sScore);
            if (st.Overtime) GUI.Label(new Rect(sbX + 175, sbY + 30, 110, 26), "<color=#ffd23f>ОВЕРТАЙМ</color>", new GUIStyle(sSmall) { alignment = TextAnchor.MiddleCenter, fontSize = 12 });

            // баннер
            string big = "", small = "";
            if (st.Phase == Phase.Faceoff)
            {
                big = st.PhaseT > 0.6f ? Mathf.CeilToInt(st.PhaseT - 0.4f).ToString() : "ШАЙБА!";
                small = st.Overtime ? "Овертайм — до первой шайбы" : "Вбрасывание";
            }
            else if (st.Phase == Phase.Goal && st.LastGoal.Team >= 0)
            {
                big = "ГОЛ!";
                var sc = st.LastGoal.Scorer >= 0 && st.LastGoal.Scorer < roster.Count ? roster[st.LastGoal.Scorer] : null;
                var asr = st.LastGoal.Assist >= 0 && st.LastGoal.Assist < roster.Count ? roster[st.LastGoal.Assist] : null;
                small = Rink.TeamNames[st.LastGoal.Team] + " · " + (sc != null ? "забил " + sc.Name + (asr != null ? ", пас " + asr.Name : "") : "автогол");
            }
            if (big != "")
            {
                string col = st.Phase == Phase.Goal ? (st.LastGoal.Team == 0 ? "#e2483d" : "#3f86e8") : "#e2483d";
                float pulse = st.Phase == Phase.Goal ? 1 + Mathf.Max(0, 0.25f - (3.6f - st.PhaseT)) * 2 : 1;
                var bs = new GUIStyle(sBanner) { fontSize = (int)(96 * pulse) };
                GUI.Label(new Rect(0, VH * 0.24f + 6, VW, 120), $"<color={col}>{big}</color>", bs);
                GUI.Label(new Rect(0, VH * 0.24f, VW, 120), big, bs);
                GUI.Label(new Rect(0, VH * 0.24f + 110, VW, 40), small, sBannerSmall);
            }

            // имена над людьми
            foreach (var v in views)
            {
                for (int i = 0; i < roster.Count && i < chars.Count; i++)
                {
                    if (roster[i].Human == null) continue;
                    var wp = chars[i].Root.position + Vector3.up * 2.35f;
                    var sp = v.Cam.WorldToScreenPoint(wp);
                    if (sp.z < 0.5f) continue;
                    var pr = v.Cam.pixelRect;
                    if (sp.x < pr.xMin || sp.x > pr.xMax) continue;
                    float gx = sp.x / uiScale, gy = (Screen.height - sp.y) / uiScale;
                    var content = new GUIContent(roster[i].Name);
                    var size = sTag.CalcSize(content);
                    GUI.color = Mats.Hex(SlotColor(roster[i].Human));
                    GUI.DrawTexture(new Rect(gx - size.x / 2 - 2, gy - size.y / 2 - 2, size.x + 4, size.y + 4), white);
                    GUI.color = Color.white;
                    GUI.Label(new Rect(gx - size.x / 2, gy - size.y / 2, size.x, size.y), content, sTag);
                }
            }

            // полоски силы броска и выносливости
            foreach (var v in views)
            {
                if (v.Follow < 0 || v.Follow >= st.Players.Length) continue;
                var p = st.Players[v.Follow];
                float vx = v.Rect.x * VW, vw = v.Rect.width * VW;
                float bw = Mathf.Min(280, vw * 0.6f), bx = vx + (vw - bw) / 2, by = VH - 46;
                Box(new Rect(bx, by, bw, 14), "#000000", 0.45f);
                Box(new Rect(bx, by, bw * p.Charge, 14), p.Charge > 0.7f ? "#ff6a3d" : "#ffd23f", p.Charge > 0 ? 1 : 0.3f);
                Box(new Rect(bx, by + 20, bw, 9), "#000000", 0.45f);
                Box(new Rect(bx, by + 20, bw * p.Stamina, 9), "#5cf28a");
                if (views.Count > 1 && v.Follow < roster.Count)
                    GUI.Label(new Rect(vx + 14, 80, 300, 30), $"<color={SlotColor(roster[v.Follow].Human ?? "")}><b>{roster[v.Follow].Name}</b></color>", sLabel);
            }
            if (views.Count > 1) Box(new Rect(VW / 2 - 2, 0, 4, VH), "#10151f");

            DrawMinimap(st);

            if (mode == Mode.Host) GUI.Label(new Rect(VW - 330, 14, 316, 30), $"Хост · игроков по сети: {lobby.Count - 1}", new GUIStyle(sSmall) { alignment = TextAnchor.UpperRight });
            else if (mode == Mode.Client && client != null) GUI.Label(new Rect(VW - 330, 14, 316, 30), $"Пинг: {(client.PingMs >= 0 ? client.PingMs + " мс" : "…")}", new GUIStyle(sSmall) { alignment = TextAnchor.UpperRight });
            GUI.Label(new Rect(VW - 520, VH - 28, 506, 24), "Esc — пауза · H — управление · M — звук", new GUIStyle(sSmall) { alignment = TextAnchor.LowerRight, fontSize = 13 });
            if (showHelp)
            {
                Box(new Rect(VW - 640, VH - 330, 626, 296), "#0f1423", 0.88f);
                GUI.Label(new Rect(VW - 628, VH - 322, 606, 284), ControlsText, sSmall);
            }
        }

        void DrawMinimap(ViewState st)
        {
            bool split = views.Count > 1;
            float mw = split ? 190 : 250, mh = mw * Rink.W / Rink.L + 16;
            float mx = split ? (VW - mw) / 2 : 16, my = split ? 78 : VH - mh - 16;
            Box(new Rect(mx - 8, my - 8, mw + 16, mh + 16), "#0f1423", 0.6f);
            Box(new Rect(mx, my, mw, mh), "#ebf5ff", 0.92f);
            float sx = mw / Rink.L, sz = mh / Rink.W;
            float X(float x) => mx + (x + Rink.L / 2) * sx;
            float Z(float z) => my + (Rink.W / 2 - z) * sz; // сверху вниз: +Z вверху, как на виде сверху
            Box(new Rect(X(0) - 1, my, 2, mh), "#d9363e");
            Box(new Rect(X(Rink.BlueX) - 1, my, 2, mh), "#2f6fd6");
            Box(new Rect(X(-Rink.BlueX) - 1, my, 2, mh), "#2f6fd6");
            Box(new Rect(X(Rink.GoalX), Z(Rink.GoalW / 2), 3, Rink.GoalW * sz), "#d9363e");
            Box(new Rect(X(-Rink.GoalX) - 3, Z(Rink.GoalW / 2), 3, Rink.GoalW * sz), "#d9363e");
            var mine = new HashSet<int>();
            foreach (var v in views) mine.Add(v.Follow);
            for (int i = 0; i < st.Players.Length && i < roster.Count; i++)
            {
                var p = st.Players[i]; var r = roster[i];
                float s = mine.Contains(i) ? 12 : r.Human != null ? 9 : 7;
                if (r.Human != null) Box(new Rect(X(p.X) - s / 2 - 2, Z(p.Z) - s / 2 - 2, s + 4, s + 4), SlotColor(r.Human));
                Box(new Rect(X(p.X) - s / 2, Z(p.Z) - s / 2, s, s), r.Team == 0 ? "#e2483d" : "#3f86e8");
                if (mine.Contains(i))
                {
                    float hx = X(p.X) + Mathf.Sin(p.H) * 9, hz = Z(p.Z) - Mathf.Cos(p.H) * 9;
                    Box(new Rect(hx - 2.5f, hz - 2.5f, 5, 5), "#ffffff");
                }
            }
            Box(new Rect(X(st.PuckX) - 4, Z(st.PuckZ) - 4, 8, 8), "#ffffff");
            Box(new Rect(X(st.PuckX) - 3, Z(st.PuckZ) - 3, 6, 6), "#111111");
        }
    }
}
