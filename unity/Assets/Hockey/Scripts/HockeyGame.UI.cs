// Интерфейс «ШАЙБЯТОК» (IMGUI со скруглёнными градиентными плашками):
// главное меню, выбор режима, сетевая игра, настройки, управление, лобби, HUD, пауза, итоги матча.
using System.Collections.Generic;
using UnityEngine;

namespace Hockey
{
    public partial class HockeyGame
    {
        enum UiScreen { Menu, Lobby, Match }
        enum UiPage { Main, Play, Split, Online, Settings, Controls }

        UiScreen screen = UiScreen.Menu;
        UiPage uiPage = UiPage.Main;
        float uiScale, VW, VH;
        bool stylesReady;
        Texture2D white, circleTex;
        GUIStyle sPanel, sCard, sCardOn, sTitle, sH1, sH2, sLabel, sSmall, sCenter, sField, sTag, sKey, sBig, sScore, sCode, sSliderBg, sThumb, sPill;
        readonly Dictionary<string, GUIStyle> btn = new Dictionary<string, GUIStyle>();
        readonly Dictionary<string, Texture2D> solid = new Dictionary<string, Texture2D>();
        Vector2 scroll;
        UiPage settingsReturn = UiPage.Main;

        // ---------------------------------------------------------------- стили
        Texture2D Solid(string hex, float a = 1)
        {
            string k = hex + a;
            if (solid.TryGetValue(k, out var t)) return t;
            var c = Mats.Hex(hex); c.a = a;
            t = Tex.Solid(c);
            solid[k] = t;
            return t;
        }

        static GUIStyle Panel(Color top, Color bottom, Color border, int radius = 22, int shadow = 6)
        {
            var s = new GUIStyle { padding = new RectOffset(26, 26, 22, 24 + shadow), border = new RectOffset(radius + 2, radius + 2, radius + 2, radius + 2 + shadow) };
            s.normal.background = TexArt.RoundRect(radius * 2 + 12, radius, top, bottom, border, 3, shadow);
            return s;
        }

        GUIStyle Btn(string top, string bottom, int size = 22)
        {
            string key = top + bottom + size;
            if (btn.TryGetValue(key, out var s)) return s;
            Color t = Mats.Hex(top), b = Mats.Hex(bottom), br = Color.Lerp(b, Color.black, 0.35f);
            s = new GUIStyle(GUI.skin.button)
            {
                fontSize = size, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter, wordWrap = true, richText = true,
                padding = new RectOffset(18, 18, 12, 16), margin = new RectOffset(4, 4, 6, 6), border = new RectOffset(18, 18, 18, 22),
            };
            s.normal.background = TexArt.RoundRect(48, 14, t, b, br, 3, 5);
            s.hover.background = TexArt.RoundRect(48, 14, Color.Lerp(t, Color.white, 0.2f), Color.Lerp(b, Color.white, 0.15f), br, 3, 5);
            s.active.background = TexArt.RoundRect(48, 14, Color.Lerp(b, Color.black, 0.1f), t, br, 3, 2);
            s.onNormal.background = TexArt.RoundRect(48, 14, Color.Lerp(t, Color.white, 0.35f), Color.Lerp(b, Color.white, 0.25f), Mats.Hex("#ffffff"), 3, 5);
            s.onHover.background = s.onNormal.background; s.onActive.background = s.active.background;
            s.focused.background = s.normal.background;
            s.normal.textColor = s.hover.textColor = s.active.textColor = s.focused.textColor = s.onNormal.textColor = s.onHover.textColor = Color.white;
            btn[key] = s;
            return s;
        }

        GUIStyle BtnMain => Btn("#ffa04a", "#ff4f5e", 30);
        GUIStyle BtnBlue => Btn("#6aa8ff", "#3a63d8", 24);
        GUIStyle BtnGreen => Btn("#4fe39a", "#1fa863", 24);
        GUIStyle BtnGhost => Btn("#4a5196", "#353b7a", 21);
        GUIStyle BtnSel => Btn("#4a5196", "#353b7a", 19);

        void InitStyles()
        {
            if (stylesReady) return;
            stylesReady = true;
            white = Tex.Solid(Color.white);
            circleTex = TexArt.Circle(64, Color.white);
            sPanel = Panel(new Color(0.16f, 0.19f, 0.42f, 0.94f), new Color(0.1f, 0.12f, 0.3f, 0.94f), new Color(0.45f, 0.55f, 1f, 0.9f));
            sCard = Panel(new Color(0.22f, 0.26f, 0.55f, 0.95f), new Color(0.16f, 0.19f, 0.44f, 0.95f), new Color(0.4f, 0.48f, 0.9f, 0.8f), 16, 4);
            sCardOn = Panel(new Color(1f, 0.6f, 0.3f, 0.97f), new Color(0.95f, 0.3f, 0.4f, 0.97f), Color.white, 16, 4);
            sCard.richText = sCardOn.richText = true;
            sCard.wordWrap = sCardOn.wordWrap = true;
            sCard.fontSize = sCardOn.fontSize = 18;
            sCard.normal.textColor = sCardOn.normal.textColor = Color.white;
            sTitle = new GUIStyle(GUI.skin.label) { fontSize = 92, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleLeft, richText = true, wordWrap = false };
            sTitle.normal.textColor = Color.white;
            sH1 = new GUIStyle(sTitle) { fontSize = 44, alignment = TextAnchor.MiddleCenter };
            sH2 = new GUIStyle(sTitle) { fontSize = 26, alignment = TextAnchor.MiddleLeft };
            sH2.normal.textColor = Mats.Hex("#ffd23f");
            sLabel = new GUIStyle(GUI.skin.label) { fontSize = 20, richText = true, wordWrap = true };
            sLabel.normal.textColor = Mats.Hex("#e6ecff");
            sSmall = new GUIStyle(sLabel) { fontSize = 16 };
            sSmall.normal.textColor = Mats.Hex("#aab6e8");
            sCenter = new GUIStyle(sLabel) { alignment = TextAnchor.MiddleCenter };
            sField = new GUIStyle(GUI.skin.textField) { fontSize = 22, padding = new RectOffset(14, 14, 10, 10), fixedHeight = 48, border = new RectOffset(14, 14, 14, 14) };
            sField.normal.background = sField.hover.background = TexArt.RoundRect(40, 12, Mats.Hex("#10143a"), Mats.Hex("#151a46"), Mats.Hex("#5a68c8"), 2);
            sField.focused.background = TexArt.RoundRect(40, 12, Mats.Hex("#10143a"), Mats.Hex("#151a46"), Mats.Hex("#ffd23f"), 2);
            sField.normal.textColor = sField.focused.textColor = sField.hover.textColor = Color.white;
            sTag = new GUIStyle(sLabel) { fontSize = 17, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter, wordWrap = false, padding = new RectOffset(12, 12, 4, 6), border = new RectOffset(10, 10, 10, 10) };
            sTag.normal.background = TexArt.RoundRect(32, 10, new Color(0.08f, 0.1f, 0.25f, 0.85f), new Color(0.05f, 0.06f, 0.18f, 0.85f), new Color(1, 1, 1, 0.6f), 2);
            sTag.normal.textColor = Color.white;
            sPill = new GUIStyle(sTag) { fontSize = 16 };
            sKey = new GUIStyle(sTag) { fontSize = 15, padding = new RectOffset(8, 8, 3, 5) };
            sKey.normal.background = TexArt.RoundRect(28, 8, Mats.Hex("#ffffff"), Mats.Hex("#d6dcf5"), Mats.Hex("#6f7bb8"), 2, 3);
            sKey.normal.textColor = Mats.Hex("#1d2250");
            sBig = new GUIStyle(sLabel) { fontSize = 28, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter, wordWrap = false };
            sScore = new GUIStyle(sBig) { fontSize = 40 };
            sScore.normal.textColor = Color.white;
            sCode = new GUIStyle(sBig) { fontSize = 34 };
            sCode.normal.textColor = Mats.Hex("#ffd23f");
            sSliderBg = new GUIStyle(GUI.skin.horizontalSlider) { fixedHeight = 14, border = new RectOffset(7, 7, 7, 7), margin = new RectOffset(4, 4, 14, 14) };
            sSliderBg.normal.background = TexArt.RoundRect(24, 7, Mats.Hex("#0e1238"), Mats.Hex("#1b2155"), Mats.Hex("#5a68c8"), 2);
            sThumb = new GUIStyle(GUI.skin.horizontalSliderThumb) { fixedWidth = 28, fixedHeight = 28, margin = new RectOffset(0, 0, -7, 0) };
            sThumb.normal.background = sThumb.hover.background = sThumb.active.background = TexArt.Circle(56, Mats.Hex("#ffd23f"));
        }

        bool Button(string text, GUIStyle style, params GUILayoutOption[] opts)
        {
            bool r = GUILayout.Button(text, style, opts);
            if (r) audioFx.Ui(true);
            return r;
        }

        int Choice(int cur, string[] options, int size = 19)
        {
            GUILayout.BeginHorizontal();
            for (int i = 0; i < options.Length; i++)
            {
                var st = i == cur ? Btn("#ffb15a", "#ff6a4e", size) : Btn("#4a5196", "#353b7a", size);
                if (GUILayout.Button(options[i], st)) { cur = i; audioFx.Ui(true); }
            }
            GUILayout.EndHorizontal();
            return cur;
        }

        float Slider(string label, float v, float min, float max, string fmt)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(label, sLabel, GUILayout.Width(235));
            v = GUILayout.HorizontalSlider(v, min, max, sSliderBg, sThumb, GUILayout.Width(190));
            GUILayout.Label(fmt, sSmall, GUILayout.Width(62));
            GUILayout.EndHorizontal();
            return v;
        }

        bool Toggle(string label, bool v)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(label, sLabel, GUILayout.Width(235));
            var st = v ? Btn("#4fe39a", "#1fa863", 17) : Btn("#5a5f8a", "#40446e", 17);
            if (GUILayout.Button(v ? "ВКЛ" : "ВЫКЛ", st, GUILayout.Width(120))) { v = !v; audioFx.Ui(true); }
            GUILayout.EndHorizontal();
            return v;
        }

        void Shadowed(Rect r, string text, GUIStyle st, Color main, Color shadow, float off = 5)
        {
            var c = st.normal.textColor;
            st.normal.textColor = new Color(0.05f, 0.05f, 0.15f, 0.9f);
            for (int i = 0; i < 8; i++)
            {
                float a = i * Mathf.PI / 4;
                GUI.Label(new Rect(r.x + Mathf.Cos(a) * 3, r.y + Mathf.Sin(a) * 3 + off, r.width, r.height), text, st);
            }
            st.normal.textColor = shadow;
            GUI.Label(new Rect(r.x, r.y + off, r.width, r.height), text, st);
            st.normal.textColor = main;
            GUI.Label(r, text, st);
            st.normal.textColor = c;
        }

        void Box(Rect r, string hex, float a = 1) { GUI.color = WithA(Mats.Hex(hex), a); GUI.DrawTexture(r, white); GUI.color = Color.white; }
        static Color WithA(Color c, float a) { c.a = a; return c; }
        void Dim(float a = 0.55f) { GUI.color = new Color(0.03f, 0.04f, 0.12f, a); GUI.DrawTexture(new Rect(0, 0, VW, VH), white); GUI.color = Color.white; }

        // ---------------------------------------------------------------- OnGUI
        void OnGUI()
        {
            InitStyles();
            uiScale = Screen.height / 1000f * Mathf.Clamp(Settings.UiScale, 0.7f, 1.4f);
            VW = Screen.width / uiScale; VH = Screen.height / uiScale;
            GUI.matrix = Matrix4x4.Scale(new Vector3(uiScale, uiScale, 1));

            switch (screen)
            {
                case UiScreen.Menu: DrawMenu(); break;
                case UiScreen.Lobby: DrawLobby(); break;
                case UiScreen.Match:
                    if (view != null) DrawHud(view);
                    if (endShown) DrawEnd();
                    else if (paused) { if (uiPage == UiPage.Settings) DrawSettingsPanel(); else if (uiPage == UiPage.Controls) DrawControlsPanel(); else DrawPause(); }
                    break;
            }
        }

        // ---------------------------------------------------------------- главное меню
        void DrawTitle(float x, float y)
        {
            float bounce = Mathf.Sin(time * 2.2f) * 4;
            Shadowed(new Rect(x, y + bounce, 900, 110), GameTitle, sTitle, Mats.Hex("#ffd23f"), Mats.Hex("#ff4f5e"), 7);
            var sub = new GUIStyle(sH2) { fontSize = 24 };
            Shadowed(new Rect(x + 8, y + 104, 700, 40), "хоккей с характером", sub, Mats.Hex("#bfe9ff"), Mats.Hex("#3a63d8"), 3);
        }

        void DrawMenu()
        {
            // лёгкое затемнение слева, чтобы читались кнопки
            for (int i = 0; i < 30; i++)
            {
                GUI.color = new Color(0.04f, 0.05f, 0.16f, 0.5f * (1 - i / 30f));
                GUI.DrawTexture(new Rect(i * 22, 0, 22, VH), white);
            }
            GUI.color = Color.white;
            DrawTitle(60, 40);
            switch (uiPage)
            {
                case UiPage.Main: DrawMainButtons(); break;
                case UiPage.Play: DrawPlayPage(false); break;
                case UiPage.Split: DrawPlayPage(true); break;
                case UiPage.Online: DrawOnlinePage(); break;
                case UiPage.Settings: DrawSettingsPanel(); break;
                case UiPage.Controls: DrawControlsPanel(); break;
            }
            GUI.Label(new Rect(VW - 420, VH - 40, 400, 30), "v2.0 · сделано на Unity", new GUIStyle(sSmall) { alignment = TextAnchor.LowerRight });
        }

        void DrawMainButtons()
        {
            GUILayout.BeginArea(new Rect(60, 210, 470, VH - 240));
            if (Button("ИГРАТЬ", BtnMain, GUILayout.Height(84))) uiPage = UiPage.Play;
            GUILayout.Space(6);
            if (Button("Вдвоём на одном экране", BtnBlue, GUILayout.Height(62))) uiPage = UiPage.Split;
            if (Button("Игра по сети", BtnGreen, GUILayout.Height(62))) uiPage = UiPage.Online;
            if (Button("Настройки", BtnGhost, GUILayout.Height(56))) { settingsReturn = UiPage.Main; uiPage = UiPage.Settings; }
            if (Button("Управление", BtnGhost, GUILayout.Height(56))) { settingsReturn = UiPage.Main; uiPage = UiPage.Controls; }
            if (!Application.isEditor && Button("Выход", BtnGhost, GUILayout.Height(52))) Application.Quit();
            GUILayout.Space(16);
            GUILayout.Label("Твоё имя", sSmall);
            playerName = GUILayout.TextField(playerName ?? "", 14, sField);
            if (!string.IsNullOrEmpty(message)) GUILayout.Label($"<color=#ffd23f><b>{message}</b></color>", sLabel);
            GUILayout.EndArea();
        }

        void DrawMatchSettings()
        {
            GUILayout.Label("Режим", sH2);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("<size=24><b>КЛАССИКА</b></size>\nчестный хоккей: шайба, клюшка и смекалка", cfg.Arcade ? sCard : sCardOn, GUILayout.Height(110), GUILayout.Width(330))) { cfg.Arcade = false; audioFx.Ui(true); }
            if (GUILayout.Button("<size=24><b>АРКАДА</b></size>\nбонусы на льду: турбо, мега-бросок, великан, магнит, заморозка", cfg.Arcade ? sCardOn : sCard, GUILayout.Height(110), GUILayout.Width(330))) { cfg.Arcade = true; audioFx.Ui(true); }
            GUILayout.EndHorizontal();
            GUILayout.Space(6);
            GUILayout.Label("Игроков в команде", sSmall);
            cfg.Skaters = 1 + Choice(cfg.Skaters - 1, new[] { "1", "2", "3", "4", "5" });
            int[] durs = { 120, 180, 300, 480 };
            int di = System.Array.IndexOf(durs, cfg.Duration); if (di < 0) di = 1;
            GUILayout.Label("Время матча", sSmall);
            cfg.Duration = durs[Choice(di, new[] { "2 мин", "3 мин", "5 мин", "8 мин" })];
            GUILayout.Label("Соперники-боты", sSmall);
            cfg.Difficulty = Choice(cfg.Difficulty, new[] { "Новички", "Любители", "Профи" });
        }

        void DrawPlayPage(bool split)
        {
            GUILayout.BeginArea(new Rect(60, 200, 760, VH - 220), sPanel);
            scroll = GUILayout.BeginScrollView(scroll);
            GUILayout.Label(split ? "Вдвоём на одном экране" : "Быстрый матч", sH1);
            DrawMatchSettings();
            if (split)
            {
                GUILayout.Label("Как играем", sSmall);
                splitCoop = Choice(splitCoop ? 1 : 0, new[] { "Друг против друга", "Вместе против ботов" }) == 1;
                GUILayout.Label("Игрок 1: <b>WASD</b>, F — бросок, Q — пас, Пробел — прыжок, E — толчок, Shift — рывок\n" +
                                "Игрок 2: <b>стрелки</b>, Num0 — бросок, Num1 — пас, Num3 — прыжок, Num2 — толчок, Num4 — рывок\n" +
                                "Геймпады подключаются автоматически: первый — игроку 1, второй — игроку 2.", sSmall);
            }
            GUILayout.Space(10);
            GUILayout.BeginHorizontal();
            if (Button("Назад", BtnGhost, GUILayout.Width(200), GUILayout.Height(64))) uiPage = UiPage.Main;
            if (Button("НА ЛЁД!", BtnMain, GUILayout.Height(64))) StartLocal(split);
            GUILayout.EndHorizontal();
            GUILayout.EndScrollView();
            GUILayout.EndArea();
        }

        List<string> myIps;

        void DrawOnlinePage()
        {
            GUILayout.BeginArea(new Rect(60, 200, 760, VH - 220), sPanel);
            scroll = GUILayout.BeginScrollView(scroll);
            GUILayout.Label("Игра по сети", sH1);
            GUILayout.Label("Создай матч — друзья подключатся по твоему адресу. В одной Wi-Fi сети матч найдётся сам. Через интернет: Radmin VPN, ZeroTier или Tailscale (или проброс порта 7777 UDP).", sSmall);
            GUILayout.Space(6);
            DrawMatchSettings();
            GUILayout.Space(8);
            if (Button("Создать матч", BtnGreen, GUILayout.Height(64))) StartHost();
            GUILayout.Space(8);
            GUILayout.Label("Подключиться к другу", sH2);
            GUILayout.BeginHorizontal();
            address = GUILayout.TextField(address ?? "", 60, sField, GUILayout.Width(400));
            if (Button("Подключиться", BtnBlue, GUILayout.Height(52))) StartClient(address);
            GUILayout.EndHorizontal();
            if (finder != null && finder.Games.Count > 0)
            {
                GUILayout.Label("Матчи рядом:", sSmall);
                foreach (var g in finder.Games)
                {
                    GUILayout.BeginHorizontal();
                    GUILayout.Label($"<b>{g.Name}</b>  · игроков: {g.Players}{(g.InMatch ? " · идёт матч" : "")}", sLabel);
                    if (Button("Войти", BtnGreen, GUILayout.Width(140))) { address = g.Address; StartClient(g.Address); }
                    GUILayout.EndHorizontal();
                }
            }
            else GUILayout.Label("Матчи в локальной сети появятся здесь автоматически.", sSmall);
            if (!string.IsNullOrEmpty(message)) GUILayout.Label($"<color=#ffd23f><b>{message}</b></color>", sLabel);
            GUILayout.Space(8);
            if (Button("Назад", BtnGhost, GUILayout.Width(200), GUILayout.Height(58))) uiPage = UiPage.Main;
            GUILayout.EndScrollView();
            GUILayout.EndArea();
        }

        // ---------------------------------------------------------------- настройки
        void DrawSettingsPanel()
        {
            if (screen == UiScreen.Match) Dim(0.6f);
            float w = Mathf.Min(1180, VW - 80);
            float x = screen == UiScreen.Match ? (VW - w) / 2 : 60;
            GUILayout.BeginArea(new Rect(x, screen == UiScreen.Match ? 60 : 200, w, VH - (screen == UiScreen.Match ? 120 : 220)), sPanel);
            scroll = GUILayout.BeginScrollView(scroll);
            GUILayout.Label("Настройки", sH1);
            GUILayout.BeginHorizontal();
            GUILayout.BeginVertical(GUILayout.Width(w / 2 - 40));
            GUILayout.Label("Звук", sH2);
            Settings.Master = Slider("Общая громкость", Settings.Master, 0, 1, $"{Settings.Master * 100:0}%");
            Settings.Sfx = Slider("Эффекты", Settings.Sfx, 0, 1, $"{Settings.Sfx * 100:0}%");
            Settings.Crowd = Slider("Трибуны", Settings.Crowd, 0, 1, $"{Settings.Crowd * 100:0}%");
            Settings.Music = Slider("Музыка", Settings.Music, 0, 1, $"{Settings.Music * 100:0}%");
            GUILayout.Space(8);
            GUILayout.Label("Графика", sH2);
            GUILayout.Label("Качество", sSmall);
            int q = Choice(Settings.Quality, new[] { "Низкое", "Среднее", "Высокое" });
            if (q != Settings.Quality) { Settings.Quality = q; ApplySettings(); }
            bool fs = Toggle("Полный экран", Settings.Fullscreen);
            if (fs != Settings.Fullscreen) { Settings.Fullscreen = fs; ApplySettings(); }
            GUILayout.EndVertical();
            GUILayout.Space(30);
            GUILayout.BeginVertical();
            GUILayout.Label("Камера и управление", sH2);
            Settings.Fov = Slider("Угол обзора", Settings.Fov, 50, 85, $"{Settings.Fov:0}°");
            Settings.CamDistance = Slider("Дистанция камеры", Settings.CamDistance, 0.7f, 1.5f, $"{Settings.CamDistance:0.0}×");
            Settings.MouseSens = Slider("Чувствительность мыши", Settings.MouseSens, 0.2f, 3f, $"{Settings.MouseSens:0.0}");
            Settings.InvertY = Toggle("Инвертировать по вертикали", Settings.InvertY);
            Settings.AutoCamera = Toggle("Камера сама за спину", Settings.AutoCamera);
            GUILayout.Space(8);
            GUILayout.Label("Интерфейс", sH2);
            Settings.Hints = Toggle("Подсказки кнопок", Settings.Hints);
            Settings.Names = Toggle("Имена над игроками", Settings.Names);
            Settings.ScreenShake = Toggle("Тряска экрана", Settings.ScreenShake);
            Settings.UiScale = Slider("Размер интерфейса", Settings.UiScale, 0.7f, 1.4f, $"{Settings.UiScale * 100:0}%");
            GUILayout.EndVertical();
            GUILayout.EndHorizontal();
            GUILayout.Space(10);
            GUILayout.BeginHorizontal();
            if (Button("Сбросить", BtnGhost, GUILayout.Width(220), GUILayout.Height(58))) { Settings.ResetDefaults(); ApplySettings(); }
            if (Button("Готово", BtnMain, GUILayout.Height(58))) { Settings.Save(); ApplySettings(); uiPage = screen == UiScreen.Match ? UiPage.Main : settingsReturn; }
            GUILayout.EndHorizontal();
            GUILayout.EndScrollView();
            GUILayout.EndArea();
        }

        // ---------------------------------------------------------------- управление
        static readonly string[,] ControlRows =
        {
            { "Ехать", "W A S D", "W A S D", "стрелки", "левый стик" },
            { "Камера", "мышь", "сама", "сама", "правый стик" },
            { "Бросок (держи — сильнее)", "ЛКМ / F", "F", "Num0", "RT" },
            { "Пас партнёру", "ПКМ / Q", "Q", "Num1", "X" },
            { "Прыжок / нырок за шайбой", "Пробел", "Пробел", "Num3", "A" },
            { "Силовой толчок", "E", "E", "Num2", "B" },
            { "Рывок", "Shift", "Shift", "Num4", "LT / LB" },
            { "Пауза", "Esc", "Esc", "Esc", "Start" },
        };

        void DrawControlsPanel()
        {
            if (screen == UiScreen.Match) Dim(0.6f);
            float w = Mathf.Min(1150, VW - 80);
            float x = screen == UiScreen.Match ? (VW - w) / 2 : 60;
            GUILayout.BeginArea(new Rect(x, screen == UiScreen.Match ? 60 : 200, w, VH - (screen == UiScreen.Match ? 120 : 220)), sPanel);
            scroll = GUILayout.BeginScrollView(scroll);
            GUILayout.Label("Управление", sH1);
            GUILayout.Label("Жми направление — игрок поедет туда относительно камеры. Пробел на месте — прыжок (перепрыгни силовой приём!), в движении — нырок «рыбкой» за шайбой. Стик против движения — эффектная остановка.", sSmall);
            GUILayout.Space(8);
            string[] head = { "", "Один игрок / по сети", "Игрок 1", "Игрок 2", "Геймпад" };
            GUILayout.BeginHorizontal();
            for (int c = 0; c < 5; c++) GUILayout.Label($"<b>{head[c]}</b>", sSmall, GUILayout.Width(c == 0 ? 300 : 200));
            GUILayout.EndHorizontal();
            for (int r = 0; r < ControlRows.GetLength(0); r++)
            {
                GUILayout.BeginHorizontal();
                GUILayout.Label(ControlRows[r, 0], sLabel, GUILayout.Width(300));
                for (int c = 1; c < 5; c++) GUILayout.Label(ControlRows[r, c], sKey, GUILayout.Width(190), GUILayout.Height(32));
                GUILayout.EndHorizontal();
            }
            GUILayout.Space(10);
            GUILayout.Label("<b>Бонусы в режиме «Аркада»</b>", sH2);
            for (int i = 0; i < Fx.ItemNames.Length; i++)
                GUILayout.Label($"<color={Fx.ItemColors[i]}>●</color> <b>{Fx.ItemNames[i]}</b> — {Fx.ItemHints[i]}", sLabel);
            GUILayout.Space(10);
            if (Button("Назад", BtnMain, GUILayout.Width(240), GUILayout.Height(58))) uiPage = screen == UiScreen.Match ? UiPage.Main : settingsReturn;
            GUILayout.EndScrollView();
            GUILayout.EndArea();
        }

        // ---------------------------------------------------------------- лобби
        void DrawLobby()
        {
            Dim(0.35f);
            DrawTitle(60, 40);
            float w = 780;
            GUILayout.BeginArea(new Rect(60, 200, w, VH - 220), sPanel);
            scroll = GUILayout.BeginScrollView(scroll);
            GUILayout.Label("Сетевой матч", sH1);
            if (mode == Mode.Host)
            {
                myIps ??= NetUtil.LocalAddresses();
                GUILayout.Label("Друзья подключаются по адресу:", sCenter);
                foreach (var ip in myIps) GUILayout.Label(ip + ":" + NetConst.GamePort, sCode);
                GUILayout.Label("В одной Wi-Fi сети матч найдётся сам. Через интернет: Radmin VPN / ZeroTier / Tailscale или проброс порта 7777 (UDP).", sSmall);
                GUILayout.Label($"Режим: <b>{(cfg.Arcade ? "Аркада" : "Классика")}</b> · {cfg.Skaters} в команде · {cfg.Duration / 60} мин", sCenter);
            }
            else
            {
                string st = client == null ? "" : client.Connected ? "Подключено! Ждём, пока хост начнёт матч…" : "Подключаемся к " + address + "…";
                GUILayout.Label(st, sCenter);
                if (client != null && client.Connected) GUILayout.Label($"Режим: <b>{(cfg.Arcade ? "Аркада" : "Классика")}</b> · {cfg.Skaters} в команде · {cfg.Duration / 60} мин", sCenter);
            }
            GUILayout.Space(10);
            GUILayout.BeginHorizontal();
            for (int t = 0; t < 2; t++)
            {
                GUILayout.BeginVertical(GUILayout.Width((w - 90) / 2));
                GUILayout.Label($"<color={(t == 0 ? "#ff8a7f" : "#8fbcff")}><b>{Rink.TeamNames[t]}</b></color>", sBig);
                int count = 0;
                foreach (var l in lobby.ToArray())
                {
                    if (l.Team != t) continue;
                    count++;
                    bool me = mode == Mode.Host ? l.Key == "local0" : client != null && l.Key == client.MyKey;
                    string label = l.Name + (l.Key == "local0" ? "  ★" : "") + (me ? "  (ты)" : "");
                    if (GUILayout.Button(label, t == 0 ? Btn("#ff7a6e", "#c8392f", 19) : Btn("#6aa8ff", "#2f5cc4", 19)))
                    {
                        audioFx.Ui(true);
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
                if (Button("НАЧАТЬ МАТЧ", BtnMain, GUILayout.Height(70))) NewMatch();
            }
            else if (client != null && client.Connected)
            {
                var me = lobby.Find(l => l.Key == client.MyKey);
                if (me != null && Button("Сменить команду", BtnBlue)) client.SetTeam(1 - me.Team);
            }
            if (Button("Выйти", BtnGhost)) { myIps = null; ToMenu(); }
            GUILayout.EndScrollView();
            GUILayout.EndArea();
        }

        // ---------------------------------------------------------------- пауза и итоги
        void DrawPause()
        {
            Dim();
            float w = 460;
            GUILayout.BeginArea(new Rect((VW - w) / 2, VH / 2 - 250, w, 500), sPanel);
            GUILayout.Label("Пауза", sH1);
            if (mode != Mode.Local) GUILayout.Label("В сетевом матче игра не останавливается", new GUIStyle(sSmall) { alignment = TextAnchor.MiddleCenter });
            if (Button("Продолжить", BtnMain, GUILayout.Height(70))) paused = false;
            if (Button("Настройки", BtnGhost, GUILayout.Height(56))) uiPage = UiPage.Settings;
            if (Button("Управление", BtnGhost, GUILayout.Height(56))) uiPage = UiPage.Controls;
            if (Button("Выйти в меню", BtnGhost, GUILayout.Height(56))) ToMenu();
            GUILayout.EndArea();
        }

        void DrawEnd()
        {
            Dim(0.45f);
            var v = view;
            if (v == null) return;
            float w = 720;
            GUILayout.BeginArea(new Rect((VW - w) / 2, 50, w, VH - 100), sPanel);
            string title = "Ничья!";
            if (v.Score0 != v.Score1)
            {
                int win = v.Score0 > v.Score1 ? 0 : 1;
                title = "Победа: " + Rink.TeamNames[win] + "!";
                var myTeams = new HashSet<int>();
                foreach (var k in myKeys) { var e = roster.Find(r => r.Human == k); if (e != null) myTeams.Add(e.Team); }
                if (myTeams.Count == 1) title = myTeams.Contains(win) ? "ПОБЕДА!" : "Почти получилось…";
            }
            var r0 = GUILayoutUtility.GetRect(w - 60, 70);
            Shadowed(r0, title, sH1, Mats.Hex("#ffd23f"), Mats.Hex("#ff4f5e"), 4);
            GUILayout.Label($"<color=#ff8a7f>{v.Score0}</color>  :  <color=#8fbcff>{v.Score1}</color>", new GUIStyle(sTitle) { fontSize = 72, alignment = TextAnchor.MiddleCenter });
            var rows = new List<int>();
            for (int i = 0; i < roster.Count && i < v.Players.Length; i++) if (!roster[i].Goalie) rows.Add(i);
            rows.Sort((a, b) => (v.Players[b].Goals * 2 + v.Players[b].Assists).CompareTo(v.Players[a].Goals * 2 + v.Players[a].Assists));
            if (rows.Count > 0 && v.Players[rows[0]].Goals + v.Players[rows[0]].Assists > 0)
                GUILayout.Label($"Звезда матча: <b><color=#ffd23f>{roster[rows[0]].Name}</color></b>", sCenter);
            GUILayout.BeginHorizontal();
            GUILayout.Label("Игрок", sSmall, GUILayout.Width(360)); GUILayout.Label("Голы", sSmall, GUILayout.Width(120)); GUILayout.Label("Пасы", sSmall);
            GUILayout.EndHorizontal();
            foreach (int i in rows)
            {
                var r = roster[i];
                string col = r.Team == 0 ? "#ff8a7f" : "#8fbcff";
                string nm = r.Human != null ? $"<b>{r.Name}</b>" : r.Name;
                GUILayout.BeginHorizontal();
                GUILayout.Label($"<color={col}>●</color> {nm}", sLabel, GUILayout.Width(360));
                GUILayout.Label(v.Players[i].Goals.ToString(), sLabel, GUILayout.Width(120));
                GUILayout.Label(v.Players[i].Assists.ToString(), sLabel);
                GUILayout.EndHorizontal();
            }
            GUILayout.FlexibleSpace();
            if (mode != Mode.Client) { if (Button("Ещё матч!", BtnMain, GUILayout.Height(66))) NewMatch(); }
            else GUILayout.Label("Хост может начать новый матч", sCenter);
            if (Button("В меню", BtnGhost, GUILayout.Height(56))) ToMenu();
            GUILayout.EndArea();
        }

        // ---------------------------------------------------------------- HUD
        string KeyFor(View v, int action)
        {
            // 0 бросок, 1 пас, 2 прыжок, 3 толчок, 4 рывок
            bool pad = v.Src != null && v.Src.Pad >= 0 && GameInput.AnyPad() && v.Src.Scheme != Scheme.Solo;
            var scheme = v.Src?.Scheme ?? Scheme.Solo;
            string[] solo = { "ЛКМ", "ПКМ", "Пробел", "E", "Shift" };
            string[] p1 = { "F", "Q", "Пробел", "E", "Shift" };
            string[] p2 = { "Num0", "Num1", "Num3", "Num2", "Num4" };
            string[] gp = { "RT", "X", "A", "B", "LT" };
            if (pad) return gp[action];
            return scheme == Scheme.P1 ? p1[action] : scheme == Scheme.P2 ? p2[action] : solo[action];
        }

        void DrawHints(View v, ViewState st, Rect area)
        {
            if (!Settings.Hints || v.Follow < 0 || v.Follow >= st.Players.Length || st.Phase != Phase.Play) return;
            var p = st.Players[v.Follow];
            var items = new List<(string key, string act)>();
            bool mine = st.PuckOwner == v.Follow;
            bool oppHas = st.PuckOwner >= 0 && st.PuckOwner < roster.Count && roster[st.PuckOwner].Team != roster[v.Follow].Team;
            if (mine) { items.Add((KeyFor(v, 0), "бросок (держи)")); items.Add((KeyFor(v, 1), "пас")); }
            else if (oppHas) { items.Add((KeyFor(v, 2), p.Spd > 4.5f ? "нырок за шайбой" : "прыжок")); items.Add((KeyFor(v, 3), "толчок")); }
            else { items.Add((KeyFor(v, 2), p.Spd > 4.5f ? "нырок" : "прыжок")); items.Add((KeyFor(v, 0), "бросок с лёта")); }
            items.Add((KeyFor(v, 4), "рывок"));
            float total = 0;
            var widths = new List<float>();
            foreach (var it in items)
            {
                float kw = sKey.CalcSize(new GUIContent(it.key)).x, aw = sSmall.CalcSize(new GUIContent(it.act)).x;
                widths.Add(kw); total += kw + aw + 34;
            }
            float x = area.x + (area.width - total) / 2, y = area.yMax - 120;
            GUI.Label(new Rect(x - 12, y - 8, total + 4, 46), "", sTag);
            for (int i = 0; i < items.Count; i++)
            {
                GUI.Label(new Rect(x, y, widths[i], 30), items[i].key, sKey);
                x += widths[i] + 6;
                var aw = sSmall.CalcSize(new GUIContent(items[i].act)).x;
                GUI.Label(new Rect(x, y + 3, aw + 4, 28), $"<color=#ffffff>{items[i].act}</color>", sSmall);
                x += aw + 28;
            }
        }

        void DrawHud(ViewState st)
        {
            // табло-«пилюля»
            float sbW = 560, sbX = (VW - sbW) / 2, sbY = 16;
            GUI.Label(new Rect(sbX - 6, sbY - 4, sbW + 12, 72), "", sTag);
            Box(new Rect(sbX + 8, sbY + 6, 200, 52), "#ef5a4c", 0.95f);
            Box(new Rect(sbX + sbW - 208, sbY + 6, 200, 52), "#4a8ff0", 0.95f);
            GUI.Label(new Rect(sbX + 14, sbY + 6, 130, 52), "КРАСНЫЕ", new GUIStyle(sBig) { fontSize = 19 });
            GUI.Label(new Rect(sbX + 146, sbY + 6, 60, 52), st.Score0.ToString(), sScore);
            GUI.Label(new Rect(sbX + sbW - 206, sbY + 6, 60, 52), st.Score1.ToString(), sScore);
            GUI.Label(new Rect(sbX + sbW - 144, sbY + 6, 130, 52), "СИНИЕ", new GUIStyle(sBig) { fontSize = 19 });
            int tt = Mathf.Max(0, Mathf.CeilToInt(st.Time));
            GUI.Label(new Rect(sbX + 208, sbY + 2, sbW - 416, 52), st.Overtime ? "ОТ" : $"{tt / 60}:{tt % 60:00}", sScore);
            if (st.Overtime) GUI.Label(new Rect(sbX + 208, sbY + 40, sbW - 416, 24), "<color=#ffd23f>до гола</color>", new GUIStyle(sSmall) { alignment = TextAnchor.MiddleCenter, fontSize = 13 });

            // баннер
            string big = "", small = "";
            float pop = 1;
            if (st.Phase == Phase.Faceoff)
            {
                big = st.PhaseT > 0.6f ? Mathf.CeilToInt(st.PhaseT - 0.4f).ToString() : "ШАЙБУ!";
                small = st.Overtime ? "Овертайм — до первой шайбы" : cfg.Arcade && mode != Mode.Client ? "Аркада: собирай бонусы на льду!" : "Вбрасывание";
                pop = 1 + Mathf.Max(0, Mathf.Repeat(st.PhaseT - 0.4f, 1) - 0.75f) * 2;
            }
            else if (st.Phase == Phase.Goal && st.LastGoal.Team >= 0)
            {
                big = "ГОЛ!";
                var sc = st.LastGoal.Scorer >= 0 && st.LastGoal.Scorer < roster.Count ? roster[st.LastGoal.Scorer] : null;
                var asr = st.LastGoal.Assist >= 0 && st.LastGoal.Assist < roster.Count ? roster[st.LastGoal.Assist] : null;
                small = Rink.TeamNames[st.LastGoal.Team] + " · " + (sc != null ? "забил " + sc.Name + (asr != null ? ", пас " + asr.Name : "") : "автогол");
                pop = 1 + Mathf.Max(0, 0.3f - (3.6f - st.PhaseT)) * 2.5f + Mathf.Sin(time * 8) * 0.03f;
            }
            if (big != "")
            {
                string col = st.Phase == Phase.Goal ? (st.LastGoal.Team == 0 ? "#ff5a4c" : "#4a8ff0") : "#ff4f5e";
                var bs = new GUIStyle(sTitle) { fontSize = (int)(120 * pop), alignment = TextAnchor.MiddleCenter };
                Shadowed(new Rect(0, VH * 0.2f, VW, 160), big, bs, Mats.Hex("#ffd23f"), Mats.Hex(col), 8);
                Shadowed(new Rect(0, VH * 0.2f + 150, VW, 44), small, new GUIStyle(sH2) { alignment = TextAnchor.MiddleCenter, fontSize = 30 }, Color.white, Mats.Hex("#1d2250"), 3);
            }

            // имена над людьми
            if (Settings.Names)
                foreach (var v in views)
                    for (int i = 0; i < roster.Count && i < chars.Count; i++)
                    {
                        if (roster[i].Human == null) continue;
                        var sp = v.Cam.WorldToScreenPoint(chars[i].Root.position + Vector3.up * 2.45f);
                        if (sp.z < 0.5f) continue;
                        var pr = v.Cam.pixelRect;
                        if (sp.x < pr.xMin || sp.x > pr.xMax) continue;
                        float gx = sp.x / uiScale, gy = (Screen.height - sp.y) / uiScale;
                        var content = new GUIContent(roster[i].Name);
                        var size = sTag.CalcSize(content);
                        GUI.color = Mats.Hex(SlotColor(roster[i].Human));
                        GUI.Label(new Rect(gx - size.x / 2, gy - size.y / 2, size.x, size.y), content, sTag);
                        GUI.color = Color.white;
                    }

            foreach (var v in views)
            {
                if (v.Follow < 0 || v.Follow >= st.Players.Length) continue;
                var p = st.Players[v.Follow];
                var area = new Rect(v.Rect.x * VW, 0, v.Rect.width * VW, VH);
                // полоски силы броска и выносливости
                float bw = Mathf.Min(320, area.width * 0.6f), bx = area.x + (area.width - bw) / 2, by = VH - 70;
                GUI.Label(new Rect(bx - 8, by - 8, bw + 16, 50), "", sTag);
                Box(new Rect(bx, by, bw * p.Charge, 14), p.Charge > 0.7f ? "#ff6a3d" : "#ffd23f", p.Charge > 0 ? 1 : 0.3f);
                Box(new Rect(bx, by + 21, bw * p.Stamina, 9), p.Has(Buff.Turbo) ? Fx.ItemColors[0] : "#5cf28a");
                DrawHints(v, st, area);
                // активные бонусы
                float ix = area.x + area.width / 2 - 200, iy = 96;
                for (int b = 0; b < 5; b++)
                {
                    if (!p.Has((Buff)b)) continue;
                    GUI.color = Mats.Hex(Fx.ItemColors[b]);
                    GUI.Label(new Rect(ix, iy, 130, 32), Fx.ItemNames[b], sPill);
                    GUI.color = Color.white;
                    ix += 136;
                }
                // всплывающее сообщение о бонусе
                if (v.ToastT > 0 && !string.IsNullOrEmpty(v.Toast))
                {
                    float a = Mathf.Clamp01(v.ToastT * 2);
                    var ts = new GUIStyle(sH2) { alignment = TextAnchor.MiddleCenter, fontSize = 34 };
                    var c = v.ToastColor; c.a = a;
                    Shadowed(new Rect(area.x, VH * 0.62f - (2.4f - v.ToastT) * 20, area.width, 90), v.Toast, ts, c, new Color(0.05f, 0.05f, 0.2f, a), 3);
                }
                if (views.Count > 1 && v.Follow < roster.Count)
                    GUI.Label(new Rect(area.x + 16, 90, 300, 34), $"<color={SlotColor(roster[v.Follow].Human)}><b>{roster[v.Follow].Name}</b></color>", sLabel);
            }
            if (views.Count > 1) Box(new Rect(VW / 2 - 3, 0, 6, VH), "#0d1030");

            DrawMinimap(st);

            if (mode == Mode.Host) GUI.Label(new Rect(VW - 360, 16, 344, 30), $"Хост · игроков по сети: {lobby.Count - 1}", new GUIStyle(sSmall) { alignment = TextAnchor.UpperRight });
            else if (mode == Mode.Client && client != null) GUI.Label(new Rect(VW - 360, 16, 344, 30), $"Пинг: {(client.PingMs >= 0 ? client.PingMs + " мс" : "…")}", new GUIStyle(sSmall) { alignment = TextAnchor.UpperRight });
            GUI.Label(new Rect(VW - 560, VH - 32, 546, 26), "Esc — пауза · H — подсказки · M — звук", new GUIStyle(sSmall) { alignment = TextAnchor.LowerRight, fontSize = 13 });
        }

        void DrawMinimap(ViewState st)
        {
            bool split = views.Count > 1;
            float mw = split ? 200 : 260, mh = mw * Rink.W / Rink.L + 16;
            float mx = split ? (VW - mw) / 2 : 22, my = split ? 96 : VH - mh - 22;
            GUI.Label(new Rect(mx - 10, my - 10, mw + 20, mh + 20), "", sTag);
            Box(new Rect(mx, my, mw, mh), "#eef6ff", 0.92f);
            float sx = mw / Rink.L, sz = mh / Rink.W;
            float X(float x) => mx + (x + Rink.L / 2) * sx;
            float Z(float z) => my + (Rink.W / 2 - z) * sz; // сверху вниз: +Z вверху, как на виде сверху
            Box(new Rect(X(0) - 1, my, 2, mh), "#e8443c");
            Box(new Rect(X(Rink.BlueX) - 1, my, 2, mh), "#2f74e4");
            Box(new Rect(X(-Rink.BlueX) - 1, my, 2, mh), "#2f74e4");
            Box(new Rect(X(Rink.GoalX), Z(Rink.GoalW / 2), 3, Rink.GoalW * sz), "#e8443c");
            Box(new Rect(X(-Rink.GoalX) - 3, Z(Rink.GoalW / 2), 3, Rink.GoalW * sz), "#e8443c");
            foreach (var it in st.Items)
            {
                GUI.color = Mats.Hex(Fx.ItemColors[Mathf.Clamp(it.Type, 0, 4)]);
                GUI.DrawTexture(new Rect(X(it.X) - 6, Z(it.Z) - 6, 12, 12), circleTex);
            }
            GUI.color = Color.white;
            var mine = new HashSet<int>();
            foreach (var v in views) mine.Add(v.Follow);
            for (int i = 0; i < st.Players.Length && i < roster.Count; i++)
            {
                var p = st.Players[i]; var r = roster[i];
                float s = mine.Contains(i) ? 14 : r.Human != null ? 11 : 9;
                if (r.Human != null) { GUI.color = Mats.Hex(SlotColor(r.Human)); GUI.DrawTexture(new Rect(X(p.X) - s / 2 - 3, Z(p.Z) - s / 2 - 3, s + 6, s + 6), circleTex); }
                GUI.color = Mats.Hex(r.Team == 0 ? "#ef5a4c" : "#4a8ff0");
                GUI.DrawTexture(new Rect(X(p.X) - s / 2, Z(p.Z) - s / 2, s, s), circleTex);
                if (mine.Contains(i))
                {
                    float hx = X(p.X) + Mathf.Sin(p.H) * 10, hz = Z(p.Z) - Mathf.Cos(p.H) * 10;
                    GUI.color = Color.white;
                    GUI.DrawTexture(new Rect(hx - 3, hz - 3, 6, 6), circleTex);
                }
            }
            GUI.color = Color.white;
            GUI.DrawTexture(new Rect(X(st.PuckX) - 5, Z(st.PuckZ) - 5, 10, 10), circleTex);
            GUI.color = Mats.Hex("#111111");
            GUI.DrawTexture(new Rect(X(st.PuckX) - 3.5f, Z(st.PuckZ) - 3.5f, 7, 7), circleTex);
            GUI.color = Color.white;
        }
    }
}
