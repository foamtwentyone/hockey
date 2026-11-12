// Интерфейс «ШАЙБЯТОК» (IMGUI со скруглёнными градиентными плашками):
// главное меню, выбор режима, сетевая игра, настройки, управление, лобби, HUD, пауза, итоги матча.
using System.Collections.Generic;
using UnityEngine;

namespace Hockey
{
    public partial class HockeyGame
    {
        enum UiScreen { Menu, Lobby, Match }
        enum UiPage { Main, Play, Split, Online, Settings, Controls, Locker, Credits, Quit }

        UiScreen screen = UiScreen.Menu;
        UiPage uiPage = UiPage.Main;
        float uiScale, VW, VH;
        bool stylesReady;
        Font uiFont, logoFont;
        GUIStyle sLogo, sRibbon, sRibbonText;
        Texture2D white, circleTex;
        GUIStyle sHudRed, sHudBlue, sHudMid, sTrack, sHudNum, sHudTime, sHudName, sMeterText;
        readonly Dictionary<string, GUIStyle> fills = new Dictionary<string, GUIStyle>();
        Texture2D hudRing, hudReticle, hudPuck, hudStickE, hudStickF, hudBladeE, hudBladeF, hudRink, hudRays;
        readonly Texture2D[] hudShield = new Texture2D[2];
        GUIStyle sBoard, sBubbleText, sPillRed, sPillBlue, sIce, sSection;

        /// Заголовок раздела: жёлтая плашка с тёмно-синим текстом и шайбой — как табличка на борту.
        void Section(string title)
        {
            GUILayout.Space(6);
            var r = GUILayoutUtility.GetRect(new GUIContent(title), sSection, GUILayout.ExpandWidth(false));
            GUI.Label(r, title, sSection);
            GUI.DrawTexture(new Rect(r.x + 10, r.center.y - 10, 30, 19), hudPuck);
            GUILayout.Space(4);
        }
        Font bubbleFont;
        Texture2D bubSoap; readonly Texture2D[] bubTeam = new Texture2D[2];
        readonly int[] shownScore = { 0, 0 };
        readonly float[] scorePop = { 0, 0 };
        GUIStyle sPanel, sCard, sCardOn, sTitle, sH1, sH2, sLabel, sSmall, sCenter, sField, sTag, sKey, sBig, sScore, sCode, sSliderBg, sThumb, sPill;
        readonly Dictionary<string, GUIStyle> btn = new Dictionary<string, GUIStyle>();
        readonly Dictionary<string, Texture2D> solid = new Dictionary<string, Texture2D>();
        Vector2 scroll;
        UiPage settingsReturn = UiPage.Main;
        UiPage lastPage = UiPage.Main; float pageT;
        string hoverNow, hoverLast;
        static readonly string[] Tips =
        {
            "Пробел в движении — нырок «рыбкой» за шайбой. Пробел на месте — прыжок через силовой приём!",
            "R — удар кулаком. Ответь обидчику — и начнётся драка: жми R чаще соперника!",
            "Держи бросок дольше — шайба полетит сильнее. Бросок без шайбы рядом с ней — в одно касание!",
            "Стик против движения — эффектная остановка со снежной крошкой.",
            "В «Аркаде» собирай бонусы: Великан непробиваем, а Заморозка вмораживает соперников в лёд.",
            "В раздевалке можно выбрать номер, цвет кожи и обмотку клюшки.",
            "Вдвоём на одном экране: первый — WASD, второй — стрелки. Геймпады подключаются сами.",
        };

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
            var s = new GUIStyle { padding = new RectOffset(26, 26, 22, 24 + shadow), border = new RectOffset(radius + 2, radius + 2, radius + 2, radius + 2 + shadow), font = Resources.Load<Font>("Fonts/NunitoBlack") ?? Resources.Load<Font>("Fonts/RussoOne") };
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
                fontSize = size, fontStyle = uiFont != null ? FontStyle.Normal : FontStyle.Bold, alignment = TextAnchor.MiddleCenter, wordWrap = true, richText = true, font = uiFont,
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
            // шрифты (SIL OFL): Russo One — интерфейс с кириллицей, Lilita One — логотип
            // Nunito Black (OFL): округлый жирный шрифт в стиле логотипа, с кириллицей
            uiFont = Resources.Load<Font>("Fonts/NunitoBlack") ?? Resources.Load<Font>("Fonts/RussoOne");
            logoFont = Resources.Load<Font>("Fonts/LilitaOne") ?? uiFont;
            bubbleFont = uiFont;   // табло и баннеры — тем же шрифтом, что и логотип
            btn.Clear();
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
            if (uiFont != null)
                foreach (var st in new[] { sCard, sCardOn, sTitle, sH1, sH2, sLabel, sSmall, sCenter, sField, sTag, sPill, sKey, sBig, sScore, sCode })
                { st.font = uiFont; if (st.fontStyle == FontStyle.Bold) st.fontStyle = FontStyle.Normal; }
            sLogo = new GUIStyle(GUI.skin.label) { font = logoFont, fontSize = 128, alignment = TextAnchor.UpperLeft, wordWrap = false, clipping = TextClipping.Overflow };
            sRibbon = new GUIStyle { border = new RectOffset(18, 18, 18, 22) };
            sRibbon.normal.background = TexArt.RoundRect(48, 16, Mats.Hex("#ff6a4e"), Mats.Hex("#e0344a"), Mats.Hex("#7a1531"), 3, 5);
            sRibbonText = new GUIStyle(sLabel) { font = uiFont, fontSize = 22, alignment = TextAnchor.MiddleCenter, wordWrap = false };
            sRibbonText.normal.textColor = Color.white;
            // HUD: пухлые «леденцовые» плашки в стиле меню
            sHudRed = new GUIStyle { border = new RectOffset(24, 24, 24, 30) };
            sHudRed.normal.background = TexArt.RoundRect(64, 22, Mats.Hex("#ff8a72"), Mats.Hex("#e23c45"), Mats.Hex("#7a1531"), 4, 6);
            sHudBlue = new GUIStyle(sHudRed);
            sHudBlue.normal.background = TexArt.RoundRect(64, 22, Mats.Hex("#7cb8ff"), Mats.Hex("#3a5fd8"), Mats.Hex("#16235e"), 4, 6);
            sHudMid = new GUIStyle(sHudRed);
            sHudMid.normal.background = TexArt.RoundRect(64, 22, Mats.Hex("#3a3f8f"), Mats.Hex("#1b1f52"), Mats.Hex("#ffd23f"), 4, 6);
            sTrack = new GUIStyle { border = new RectOffset(16, 16, 16, 20) };
            sTrack.normal.background = TexArt.RoundRect(44, 14, new Color(0.1f, 0.12f, 0.32f, 0.9f), new Color(0.05f, 0.06f, 0.2f, 0.9f), new Color(1, 1, 1, 0.85f), 3, 4);
            sHudNum = new GUIStyle(GUI.skin.label) { font = uiFont, fontSize = 50, alignment = TextAnchor.MiddleCenter, wordWrap = false, clipping = TextClipping.Overflow };
            sHudTime = new GUIStyle(sHudNum) { fontSize = 42 };
            sHudName = new GUIStyle(GUI.skin.label) { font = uiFont, fontSize = 17, alignment = TextAnchor.MiddleCenter, wordWrap = false, clipping = TextClipping.Overflow };
            sMeterText = new GUIStyle(sHudName) { fontSize = 15 };
            // хоккейные элементы: шайба-часы, гербы, клюшка, лезвие конька, площадка
            hudPuck = HudArt.Puck();
            for (int t = 0; t < 2; t++) { var m = Mats.Hex(TeamColors.All[t].Main); hudShield[t] = HudArt.Shield(m, Color.Lerp(m, Color.black, 0.45f)); }
            hudStickE = HudArt.Stick(false); hudStickF = HudArt.Stick(true);
            hudBladeE = HudArt.Blade(false); hudBladeF = HudArt.Blade(true);
            hudRink = HudArt.Rink(320);
            hudRays = HudArt.Rays();
            sSection = new GUIStyle(sLabel) { font = uiFont, fontSize = 21, alignment = TextAnchor.MiddleLeft, wordWrap = false, padding = new RectOffset(48, 22, 7, 11), border = new RectOffset(16, 16, 16, 20), fixedHeight = 44 };
            sSection.normal.background = TexArt.RoundRect(44, 14, Mats.Hex("#ffe27a"), Mats.Hex("#ffb92e"), Mats.Hex("#1b1f52"), 3, 4);
            sSection.normal.textColor = Mats.Hex("#1b1f52");
            sIce = new GUIStyle { border = new RectOffset(28, 28, 28, 34) };
            sIce.normal.background = TexArt.RoundRect(72, 26, new Color(0.93f, 0.97f, 1f, 0.2f), new Color(0.75f, 0.86f, 1f, 0.12f), new Color(1, 1, 1, 0.65f), 3, 6);
            bubSoap = HudArt.BubbleBall(Color.white, true);
            for (int t = 0; t < 2; t++) bubTeam[t] = HudArt.BubbleBall(Mats.Hex(TeamColors.All[t].Main), false);
            sPillRed = new GUIStyle { border = new RectOffset(48, 48, 48, 52) };
            sPillRed.normal.background = HudArt.BubblePill(Mats.Hex(TeamColors.All[0].Main));
            sPillBlue = new GUIStyle(sPillRed);
            sPillBlue.normal.background = HudArt.BubblePill(Mats.Hex(TeamColors.All[1].Main));
            sBubbleText = new GUIStyle(GUI.skin.label) { font = bubbleFont, fontSize = 44, alignment = TextAnchor.MiddleCenter, wordWrap = false, clipping = TextClipping.Overflow };
            hudRing = HudArt.FaceoffRing();
            hudReticle = HudArt.Reticle();
            // табличка как хоккейный борт: белая доска, тёмная кайма
            sBoard = new GUIStyle { border = new RectOffset(22, 22, 22, 28) };
            sBoard.normal.background = TexArt.RoundRect(64, 18, Mats.Hex("#ffffff"), Mats.Hex("#dbe3f4"), Mats.Hex("#0d1030"), 4, 6);
        }

        /// Рисует текстуру, обрезанную слева направо до доли v (с учётом пустых полей текстуры).
        void DrawClipped(Rect r, Texture2D t, float v, float margin0, float margin1, float alpha = 1)
        {
            float w = r.width * (margin0 + Mathf.Clamp01(v) * (1 - margin0 - margin1));
            if (v <= 0.001f) return;
            var old = GUI.color; GUI.color = new Color(1, 1, 1, alpha);
            GUI.BeginGroup(new Rect(r.x, r.y, w, r.height));
            GUI.DrawTexture(new Rect(0, 0, r.width, r.height), t);
            GUI.EndGroup();
            GUI.color = old;
        }

        /// Заливка-«леденец» нужного цвета (кэшируется).
        GUIStyle Fill(string top, string bottom)
        {
            string k = top + bottom;
            if (fills.TryGetValue(k, out var st)) return st;
            st = new GUIStyle { border = new RectOffset(13, 13, 13, 13) };
            st.normal.background = TexArt.RoundRect(36, 12, Mats.Hex(top), Mats.Hex(bottom), Color.Lerp(Mats.Hex(top), Color.white, 0.55f), 2);
            fills[k] = st;
            return st;
        }

        /// Шкала: тёмная капсула с белой каймой, внутри цветная заливка с бликом.
        void Meter(Rect r, float v, string top, string bottom, string label, float alpha = 1)
        {
            var old = GUI.color;
            GUI.color = new Color(1, 1, 1, alpha);
            GUI.Label(r, "", sTrack);
            var inner = new Rect(r.x + 5, r.y + 5, r.width - 10, r.height - 14);
            float w = Mathf.Clamp01(v) * inner.width;
            if (w > 1)
            {
                GUI.BeginGroup(new Rect(inner.x, inner.y, w, inner.height));
                GUI.Label(new Rect(0, 0, Mathf.Max(inner.width * Mathf.Clamp01(v), inner.height), inner.height), "", Fill(top, bottom));
                GUI.color = new Color(1, 1, 1, 0.35f * alpha);
                GUI.DrawTexture(new Rect(8, 3, Mathf.Max(0, w - 16), Mathf.Max(2, inner.height * 0.18f)), white);
                GUI.EndGroup();
            }
            // деления
            GUI.color = new Color(0.05f, 0.06f, 0.2f, 0.45f * alpha);
            for (int i = 1; i < 4; i++) GUI.DrawTexture(new Rect(inner.x + inner.width * i / 4 - 1, inner.y + 3, 2, inner.height - 6), white);
            GUI.color = new Color(1, 1, 1, alpha);
            if (!string.IsNullOrEmpty(label)) Shadowed(new Rect(inner.x, inner.y - 1, inner.width, inner.height), label, sMeterText, new Color(1, 1, 1, alpha), new Color(0.05f, 0.06f, 0.2f, 0.8f * alpha), 1.5f);
            GUI.color = old;
        }

        readonly Dictionary<string, float> btnAnim = new Dictionary<string, float>();

        /// Кнопка с анимацией: плавно увеличивается под курсором, «проседает» при нажатии, на ней появляется шайба.
        bool Button(string text, GUIStyle style, params GUILayoutOption[] opts)
        {
            var rect = GUILayoutUtility.GetRect(new GUIContent(text), style, opts);
            var ev = Event.current;
            bool over = rect.Contains(ev.mousePosition);
            btnAnim.TryGetValue(text, out float h);
            if (ev.type == EventType.Repaint)
            {
                h = Mathf.MoveTowards(h, over ? 1 : 0, Time.unscaledDeltaTime * 9);
                btnAnim[text] = h;
                if (over) hoverNow = text;
            }
            bool pressed = over && GUIUtility.hotControl != 0;
            float sc = 1 + 0.045f * Mathf.SmoothStep(0, 1, h) - (pressed ? 0.05f : 0);
            var old = GUI.matrix;
            GUIUtility.ScaleAroundPivot(new Vector2(sc, sc), GUIUtility.GUIToScreenPoint(rect.center));
            bool r = GUI.Button(rect, text, style);
            if (ev.type == EventType.Repaint && h > 0.01f)
            {
                float ph = Mathf.Min(34, rect.height * 0.5f), pwd = ph * 1.6f;
                GUI.color = new Color(1, 1, 1, h);
                GUI.DrawTexture(new Rect(rect.xMax - pwd - 14 + Mathf.Sin(Time.realtimeSinceStartup * 7) * 3, rect.center.y - ph / 2 - 2, pwd, ph), hudPuck);
                GUI.color = Color.white;
            }
            GUI.matrix = old;
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

            if (Event.current.type == EventType.Layout) hoverNow = null;
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
            if (flash > 0) { GUI.color = new Color(1, 1, 1, flash * 0.6f); GUI.DrawTexture(new Rect(0, 0, VW, VH), white); GUI.color = Color.white; }
            if (Event.current.type == EventType.Repaint)
            {
                if (hoverNow != null && hoverNow != hoverLast) audioFx.Ui(false);
                hoverLast = hoverNow;
            }
        }

        // ---------------------------------------------------------------- главное меню
        /// Логотип: объёмные буквы с толстой обводкой, двухцветной заливкой и бликом, шайба с «хвостом», лента-подзаголовок.
        Texture2D logoTex; bool logoTried;

        void DrawTitle(float x, float y, float scale = 1)
        {
            var baseM = GUI.matrix;
            if (!logoTried) { logoTried = true; logoTex = Resources.Load<Texture2D>("UI/logo"); }
            if (logoTex != null)
            {
                // нарисованный логотип: шайба со шлейфом над пухлой надписью; слегка покачивается
                float w = 360 * scale, h = w * 531f / 809f;   // исходные пропорции картинки (при импорте её могут растянуть до степени двойки)
                float bob = Mathf.Sin(time * 2.2f) * 3 * scale;
                var lr = new Rect(x - 14 * scale, y - 44 * scale + bob, w, h);
                GUIUtility.RotateAroundPivot(Mathf.Sin(time * 1.3f) * 1.2f, lr.center * uiScale);
                GUI.DrawTexture(lr, logoTex, ScaleMode.StretchToFill, true);
                GUI.matrix = baseM;
                return;
            }
            float bounce = Mathf.Sin(time * 2.2f) * 3 * scale;
            var st = sLogo;
            st.fontSize = (int)(128 * scale);
            var size = st.CalcSize(new GUIContent(GameTitle));
            var r = new Rect(x, y + bounce, size.x + 40, size.y + 10);
            var pivot = new Vector2(x + size.x / 2, y + size.y / 2);
            GUIUtility.RotateAroundPivot(-3.5f + Mathf.Sin(time * 1.3f) * 0.8f, pivot * uiScale);
            void T(float dx, float dy, Color c) { st.normal.textColor = c; GUI.Label(new Rect(r.x + dx, r.y + dy, r.width, r.height), GameTitle, st); }
            float o = 7 * scale;
            // толстая тёмная обводка (с выдавливанием вниз)
            var outline = Mats.Hex("#1a1442");
            for (int i = 0; i < 16; i++)
            {
                float a = i * Mathf.PI / 8;
                T(Mathf.Cos(a) * o, Mathf.Sin(a) * o + 12 * scale, outline);
                T(Mathf.Cos(a) * o, Mathf.Sin(a) * o, outline);
            }
            // объём: слои от оранжевого к тёмно-красному
            for (int i = 10; i >= 1; i--) T(0, i * 1.1f * scale, Color.Lerp(Mats.Hex("#e8542a"), Mats.Hex("#8a1f2a"), i / 10f));
            // заливка: оранжевый низ и жёлтый верх
            T(0, 0, Mats.Hex("#ff9a1f"));
            GUI.BeginClip(new Rect(r.x, r.y, r.width, size.y * 0.56f));
            st.normal.textColor = Mats.Hex("#ffd84a");
            GUI.Label(new Rect(0, 0, r.width, r.height), GameTitle, st);
            GUI.EndClip();
            // узкий блик сверху букв
            GUI.BeginClip(new Rect(r.x, r.y + size.y * 0.22f, r.width, size.y * 0.06f));
            st.normal.textColor = new Color(1, 1, 0.92f, 0.85f);
            GUI.Label(new Rect(0, -size.y * 0.22f, r.width, r.height), GameTitle, st);
            GUI.EndClip();
            GUI.matrix = baseM;

            // шайба, летящая за логотипом
            float px = x + size.x + 18 * scale, py = y + size.y * 0.42f + bounce * 1.5f;
            for (int i = 0; i < 3; i++)
            {
                GUI.color = new Color(1, 1, 1, 0.75f - i * 0.2f);
                GUI.DrawTexture(new Rect(px - (30 + i * 26) * scale, py + (8 + i * 9) * scale, (22 - i * 4) * scale, 6 * scale), circleTex);
            }
            GUI.color = outline; GUI.DrawTexture(new Rect(px - 5 * scale, py - 3 * scale, 78 * scale, 46 * scale), circleTex);
            GUI.color = Mats.Hex("#26232f"); GUI.DrawTexture(new Rect(px, py + 10 * scale, 68 * scale, 30 * scale), circleTex);
            GUI.color = Mats.Hex("#3a3646"); GUI.DrawTexture(new Rect(px, py, 68 * scale, 30 * scale), circleTex);
            GUI.color = new Color(1, 1, 1, 0.35f); GUI.DrawTexture(new Rect(px + 12 * scale, py + 5 * scale, 30 * scale, 8 * scale), circleTex);
            GUI.color = Color.white;

            // лента с подзаголовком
            var rib = new Rect(x + 24 * scale, y + size.y * 0.9f, 380 * scale, 46 * scale);
            GUIUtility.RotateAroundPivot(-3.5f, rib.center * uiScale);
            GUI.Label(rib, "", sRibbon);
            sRibbonText.fontSize = (int)(22 * scale);
            GUI.Label(new Rect(rib.x, rib.y - 2 * scale, rib.width, rib.height), "хоккей с характером", sRibbonText);
            GUI.matrix = baseM;
        }

        /// Карточка игрока внизу слева: номер, имя, переход в раздевалку.
        void DrawPlayerCard(Rect r)
        {
            GUI.Label(r, "", sCard);
            var av = new Rect(r.x + 16, r.y + 14, r.height - 28, r.height - 28);
            GUI.color = Color.white; GUI.DrawTexture(new Rect(av.x - 3, av.y - 3, av.width + 6, av.height + 6), circleTex);
            GUI.color = Mats.Hex(TeamColors.All[0].Main); GUI.DrawTexture(av, circleTex);
            GUI.color = Color.white;
            GUI.Label(av, profNum.ToString(), new GUIStyle(sBig) { fontSize = 30 });
            GUI.Label(new Rect(av.xMax + 16, r.y + 12, r.width - av.width - 50, 34), MyName, new GUIStyle(sBig) { alignment = TextAnchor.MiddleLeft, fontSize = 26 });
            GUI.Label(new Rect(av.xMax + 16, r.y + 46, r.width - av.width - 50, 26), "нажми, чтобы переодеться", sSmall);
            if (GUI.Button(r, "", GUIStyle.none)) { audioFx.Ui(true); uiPage = UiPage.Locker; }
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
            // «ледовая» панель под кнопками: полупрозрачный лёд с красной и синей линиями и кругом вбрасывания
            if (uiPage == UiPage.Main)
            {
                var ice = new Rect(36, 212, 528, VH - 300);
                GUI.color = new Color(1, 1, 1, 0.9f); GUI.Label(ice, "", sIce);
                GUI.color = new Color(0.9f, 0.25f, 0.24f, 0.55f); GUI.DrawTexture(new Rect(ice.x + 14, ice.y + 16, 5, ice.height - 38), white);
                GUI.color = new Color(0.2f, 0.45f, 0.9f, 0.55f); GUI.DrawTexture(new Rect(ice.xMax - 19, ice.y + 16, 5, ice.height - 38), white);
                GUI.color = new Color(0.9f, 0.25f, 0.24f, 0.16f); GUI.DrawTexture(new Rect(ice.xMax - 190, ice.yMax - 200, 230, 230), hudRing);
                GUI.color = Color.white;
            }
            DrawTitle(60, 40);
            if (uiPage != lastPage) { lastPage = uiPage; pageT = Time.realtimeSinceStartup; scroll = Vector2.zero; }
            float k = Mathf.Clamp01((Time.realtimeSinceStartup - pageT) * 4.5f);
            float ease = 1 - (1 - k) * (1 - k) * (1 - k);
            var baseM = GUI.matrix;
            GUI.matrix = baseM * Matrix4x4.Translate(new Vector3((1 - ease) * -90, 0, 0));
            GUI.color = new Color(1, 1, 1, ease);
            switch (uiPage)
            {
                case UiPage.Locker: DrawLocker(); break;
                case UiPage.Credits: DrawCredits(); break;
                case UiPage.Quit: DrawQuit(); break;
                case UiPage.Main: DrawMainButtons(); break;
                case UiPage.Play: DrawPlayPage(false); break;
                case UiPage.Split: DrawPlayPage(true); break;
                case UiPage.Online: DrawOnlinePage(); break;
                case UiPage.Settings: DrawSettingsPanel(); break;
                case UiPage.Controls: DrawControlsPanel(); break;
            }
            GUI.matrix = baseM;
            GUI.color = Color.white;
            // бегущая строка с подсказками
            int tip = (int)(time / 6) % Tips.Length;
            float ta = Mathf.Clamp01(Mathf.Min(Mathf.Repeat(time, 6), 6 - Mathf.Repeat(time, 6)) * 2);
            var tipStyle = new GUIStyle(sSmall) { alignment = TextAnchor.MiddleCenter, fontSize = 17 };
            GUI.color = new Color(1, 1, 1, ta);
            if (uiPage == UiPage.Main)
            {
                // бегущая строка, как светодиодная лента на бортах арены
                GUI.color = new Color(0.05f, 0.06f, 0.18f, 0.88f); GUI.DrawTexture(new Rect(0, VH - 44, VW, 44), white);
                GUI.color = Mats.Hex("#ffd23f"); GUI.DrawTexture(new Rect(0, VH - 46, VW, 3), white);
                GUI.color = Color.white;
                var ts = new GUIStyle(sLabel) { fontSize = 19, wordWrap = false, alignment = TextAnchor.MiddleLeft, clipping = TextClipping.Overflow };
                string line = "";
                for (int i = 0; i < Tips.Length; i++) line += "   ●   " + Tips[i];
                float lw = ts.CalcSize(new GUIContent(line)).x;
                float off = Mathf.Repeat(Time.realtimeSinceStartup * 70, lw);
                GUI.BeginGroup(new Rect(0, VH - 42, VW, 40));
                GUI.Label(new Rect(-off, 0, lw + 10, 40), line, ts);
                GUI.Label(new Rect(-off + lw, 0, lw + 10, 40), line, ts);
                GUI.EndGroup();
            }
            // переход в раздевалку и обратно: затемнение со скользящей шайбой
            if (lockerFade > 0.001f)
            {
                float fa = Mathf.SmoothStep(0, 1, lockerFade);
                GUI.color = new Color(0.05f, 0.06f, 0.18f, fa); GUI.DrawTexture(new Rect(0, 0, VW, VH), white);
                GUI.color = new Color(1, 1, 1, fa);
                float pw = 220, px = Mathf.Lerp(-pw, VW, Mathf.Repeat(Time.realtimeSinceStartup * 0.9f, 1));
                GUI.DrawTexture(new Rect(px, VH / 2 - pw * 0.31f, pw, pw * 0.625f), hudPuck);
                GUI.color = Color.white;
            }
            GUI.color = Color.white;
            if (uiPage != UiPage.Main) GUI.Label(new Rect(VW - 420, VH - 24, 400, 22), "v3.2 · Unity · модели KayKit (CC0) · шрифты OFL", new GUIStyle(sSmall) { alignment = TextAnchor.LowerRight, fontSize = 13 });
        }

        void DrawMainButtons()
        {
            var baseM = GUI.matrix;
            int idx = 0;
            // кнопки выезжают слева по очереди
            void Enter()
            {
                float k = Mathf.Clamp01((Time.realtimeSinceStartup - pageT) * 5f - idx * 0.35f);
                float e = 1 - Mathf.Pow(1 - k, 3);
                GUI.matrix = baseM * Matrix4x4.Translate(new Vector3((1 - e) * -220, 0, 0));
                idx++;
            }
            GUILayout.BeginArea(new Rect(60, 226, 480, VH - 372));
            // кнопки с подписями: крупное название и строка пояснения
            string Cap(string title, string sub, int ts, int ss = 14) => $"<size={ts}>{title}</size>\n<size={ss}><color=#ffffffcc>{sub}</color></size>";
            Enter();
            float pulse = 1 + Mathf.Sin(time * 3.2f) * 0.015f;
            var playStyle = Btn("#ffb347", "#ff4f5e", 20);
            if (Button(Cap("ИГРАТЬ", "матч против ботов — один или с друзьями", (int)(38 * pulse), 15), playStyle, GUILayout.Height(104))) uiPage = UiPage.Play;
            GUILayout.Space(4);
            Enter(); if (Button(Cap("Вдвоём на одном экране", "сплит-скрин: WASD и стрелки или геймпады", 23), BtnBlue, GUILayout.Height(70))) uiPage = UiPage.Split;
            Enter(); if (Button(Cap("Игра по сети", "создай матч или найди игру друзей", 23), BtnGreen, GUILayout.Height(70))) uiPage = UiPage.Online;
            Enter(); if (Button(Cap("Раздевалка", "имя, номер, обмотка клюшки", 22), Btn("#ffd23f", "#f29a1f", 20), GUILayout.Height(66))) uiPage = UiPage.Locker;
            Enter();
            GUILayout.BeginHorizontal();
            if (Button("Настройки", BtnGhost, GUILayout.Height(50))) { settingsReturn = UiPage.Main; uiPage = UiPage.Settings; }
            if (Button("Управление", BtnGhost, GUILayout.Height(50))) { settingsReturn = UiPage.Main; uiPage = UiPage.Controls; }
            GUILayout.EndHorizontal();
            Enter();
            GUILayout.BeginHorizontal();
            if (Button("Авторы", BtnGhost, GUILayout.Height(46))) uiPage = UiPage.Credits;
            if (Button("Выход", BtnGhost, GUILayout.Height(46))) uiPage = UiPage.Quit;
            GUILayout.EndHorizontal();
            if (!string.IsNullOrEmpty(message)) GUILayout.Label($"<color=#ffd23f>{message}</color>", sLabel);
            GUILayout.EndArea();
            Enter();
            DrawPlayerCard(new Rect(VW - 420, 26, 390, 96));   // карточка игрока — в правом верхнем углу
            GUI.matrix = baseM;
        }

        void DrawLocker()
        {
            // перетаскивание мышью справа от панели — облёт камеры вокруг игрока
            var ev = Event.current;
            if (ev.type == EventType.MouseDrag && ev.mousePosition.x > 680)
            { lockerOrbit += ev.delta.x * 0.006f; lockerTilt = Mathf.Clamp(lockerTilt - ev.delta.y * 0.004f, -0.35f, 0.6f); ev.Use(); }
            GUI.Label(new Rect(VW - 470, VH - 60, 440, 30), "Потяни мышью, чтобы осмотреть игрока", new GUIStyle(sSmall) { alignment = TextAnchor.MiddleRight });
            GUILayout.BeginArea(new Rect(60, 226, 600, VH - 290), sPanel);
            scroll = GUILayout.BeginScrollView(scroll);
            GUILayout.Label("Раздевалка", sH1);
            GUILayout.Label("Твоё имя", sSmall);
            playerName = GUILayout.TextField(playerName ?? "", 14, sField);
            GUILayout.Space(6);
            GUILayout.Label("Номер на спине", sSmall);
            GUILayout.BeginHorizontal();
            int num = profNum;
            if (Button("−", BtnGhost, GUILayout.Width(64))) num = num <= 1 ? 99 : num - 1;
            GUILayout.Label(num.ToString(), new GUIStyle(sTitle) { fontSize = 48, alignment = TextAnchor.MiddleCenter }, GUILayout.Width(120), GUILayout.Height(60));
            if (Button("+", BtnGhost, GUILayout.Width(64))) num = num >= 99 ? 1 : num + 1;
            foreach (int quick in new[] { 7, 17, 87, 99 }) if (Button(quick.ToString(), BtnSel, GUILayout.Width(62))) num = quick;
            GUILayout.EndHorizontal();
            GUILayout.Label("Цвет кожи", sSmall);
            int skin = Swatches(profSkin, CharacterRig.Skins);
            GUILayout.Label("Обмотка клюшки", sSmall);
            int tape = Swatches(profTape, KayKitCharacter.TapeColors);
            GUILayout.Label("Цвет шлема", sSmall);
            int helm = Swatches(profHelm, new[] { TeamColors.All[0].Main, "#f6f6f2", "#2b2b33", "#ffd23f" });
            if (num != profNum || skin != profSkin || tape != profTape || helm != profHelm)
            {
                profNum = num; profSkin = skin; profTape = tape; profHelm = helm;
                SavePrefs();
                RebuildPreview();
            }
            GUILayout.Space(10);
            if (Button("Готово", BtnMain, GUILayout.Height(62))) { SavePrefs(); uiPage = UiPage.Main; }
            GUILayout.EndScrollView();
            GUILayout.EndArea();
        }

        int Swatches(int cur, string[] colors)
        {
            GUILayout.BeginHorizontal();
            for (int i = 0; i < colors.Length; i++)
            {
                var r = GUILayoutUtility.GetRect(58, 58, GUILayout.Width(58), GUILayout.Height(58));
                if (i == cur) { GUI.color = Color.white; GUI.DrawTexture(new Rect(r.x - 3, r.y - 3, r.width + 6, r.height + 6), circleTex); }
                GUI.color = Mats.Hex(colors[i]);
                GUI.DrawTexture(r, circleTex);
                GUI.color = Color.white;
                if (GUI.Button(r, "", GUIStyle.none)) { cur = i; audioFx.Ui(true); }
                GUILayout.Space(6);
            }
            GUILayout.EndHorizontal();
            return cur;
        }

        void DrawCredits()
        {
            GUILayout.BeginArea(new Rect(60, 200, 760, VH - 280), sPanel);
            scroll = GUILayout.BeginScrollView(scroll);
            GUILayout.Label("Авторы", sH1);
            GUILayout.Label("<b>PUCKO</b> («Шайбятки») — аркадный хоккей для весёлой компании.", sCenter);
            GUILayout.Space(10);
            GUILayout.Label("<color=#ffd23f><b>Игра, код, физика, эффекты и звуки</b></color>\nсделано вместе с Claude (Anthropic)", sLabel);
            GUILayout.Space(6);
            GUILayout.Label("<color=#ffd23f><b>Модели персонажей и анимации</b></color>\nKayKit — Adventurers Character Pack, Kay Lousberg (kaylousberg.com), лицензия CC0", sLabel);
            GUILayout.Space(6);
            GUILayout.Label("<color=#ffd23f><b>Шрифты</b></color>\nRusso One (Jovanny Lemonad) и Lilita One (Juan Montoreano), SIL Open Font License", sLabel);
            GUILayout.Space(6);
            GUILayout.Label("<color=#ffd23f><b>Экипировка, арена, текстуры, музыка</b></color>\nсоздаются кодом прямо в игре", sLabel);
            GUILayout.Space(6);
            GUILayout.Label("<color=#ffd23f><b>Движок</b></color>\nUnity", sLabel);
            GUILayout.Space(14);
            if (Button("Назад", BtnMain, GUILayout.Width(240), GUILayout.Height(58))) uiPage = UiPage.Main;
            GUILayout.EndScrollView();
            GUILayout.EndArea();
        }

        void DrawQuit()
        {
            GUILayout.BeginArea(new Rect(60, 220, 470, 260), sPanel);
            GUILayout.Label("Уже уходишь?", sH1);
            GUILayout.Label("PUCKO будет скучать!", sCenter);
            GUILayout.BeginHorizontal();
            if (Button("Остаться", BtnMain, GUILayout.Height(60))) uiPage = UiPage.Main;
            if (Button("Выйти", BtnGhost, GUILayout.Height(60), GUILayout.Width(150)))
            {
#if UNITY_EDITOR
                UnityEditor.EditorApplication.isPlaying = false;
#else
                Application.Quit();
#endif
            }
            GUILayout.EndHorizontal();
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
            Section("Звук");
            Settings.Master = Slider("Общая громкость", Settings.Master, 0, 1, $"{Settings.Master * 100:0}%");
            Settings.Sfx = Slider("Эффекты", Settings.Sfx, 0, 1, $"{Settings.Sfx * 100:0}%");
            Settings.Crowd = Slider("Трибуны", Settings.Crowd, 0, 1, $"{Settings.Crowd * 100:0}%");
            Settings.Music = Slider("Музыка", Settings.Music, 0, 1, $"{Settings.Music * 100:0}%");
            GUILayout.Space(8);
            Section("Графика");
            GUILayout.Label("Качество", sSmall);
            int q = Choice(Settings.Quality, new[] { "Низкое", "Среднее", "Высокое" });
            if (q != Settings.Quality) { Settings.Quality = q; ApplySettings(); }
            bool fs = Toggle("Полный экран", Settings.Fullscreen);
            if (fs != Settings.Fullscreen) { Settings.Fullscreen = fs; ApplySettings(); }
            GUILayout.EndVertical();
            GUILayout.Space(30);
            GUILayout.BeginVertical();
            Section("Камера и управление");
            Settings.Fov = Slider("Угол обзора", Settings.Fov, 50, 85, $"{Settings.Fov:0}°");
            Settings.CamDistance = Slider("Дистанция камеры", Settings.CamDistance, 0.7f, 1.5f, $"{Settings.CamDistance:0.0}×");
            Settings.MouseSens = Slider("Чувствительность мыши", Settings.MouseSens, 0.2f, 3f, $"{Settings.MouseSens:0.0}");
            Settings.InvertY = Toggle("Инвертировать по вертикали", Settings.InvertY);
            Settings.AutoCamera = Toggle("Камера сама за спину", Settings.AutoCamera);
            GUILayout.Space(8);
            Section("Интерфейс");
            Settings.Names = Toggle("Имена над игроками", Settings.Names);
            Settings.ScreenShake = Toggle("Тряска экрана", Settings.ScreenShake);
            Settings.UiScale = Slider("Размер интерфейса", Settings.UiScale, 0.7f, 1.4f, $"{Settings.UiScale * 100:0}%");
            float br = Slider("Яркость", Settings.Brightness, 0.5f, 1.3f, $"{Settings.Brightness * 100:0}%");
            if (Mathf.Abs(br - Settings.Brightness) > 0.001f) { Settings.Brightness = br; arena.SetBrightness(br); }
            Settings.Post = Toggle("Постобработка", Settings.Post);
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
            { "Удар кулаком / драка", "R", "R", "Num5", "Y" },
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
            Section("Как играть");
            GUILayout.Label("Бросок и пас летят в прицел по центру экрана: наведи камеру на угол ворот, подними её — шайба пойдёт верхом. Короткое нажатие — быстрый кистевой бросок, держи дольше — мощный щелчок.", sLabel);
            GUILayout.Label("Жми направление — игрок поедет туда относительно камеры. Пробел на месте — прыжок (перепрыгни силовой приём!), в движении — нырок «рыбкой» за шайбой. Стик против движения — эффектная остановка. R — удар кулаком: ответь обидчику, и начнётся драка — жми R чаще соперника!", sSmall);
            GUILayout.Space(8);
            Section("Кнопки");
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
            Section("Бонусы в режиме «Аркада»");
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
            DrawTitle((VW - 300) / 2 + 10, VH / 2 - 420, 0.85f);   // логотип над панелью
            // счёт матча на паузе
            if (view != null)
                Shadowed(new Rect(0, VH / 2 + 262, VW, 44), $"Красные  {view.Score0} : {view.Score1}  Синие", new GUIStyle(sH2) { alignment = TextAnchor.MiddleCenter, fontSize = 28 }, Color.white, Mats.Hex("#0d1030"), 3);
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
            var v = view;
            if (v == null) return;
            float tEnd = Time.realtimeSinceStartup - endShownAt;
            // лёгкое затемнение только сверху и снизу — сцена с героями остаётся на виду
            for (int i = 0; i < 24; i++)
            {
                float a = 0.55f * (1 - i / 24f);
                GUI.color = new Color(0.04f, 0.05f, 0.16f, a);
                GUI.DrawTexture(new Rect(0, i * 12, VW, 12), white);
                GUI.DrawTexture(new Rect(0, VH - (i + 1) * 10, VW, 10), white);
            }
            GUI.color = Color.white;
            string title = "НИЧЬЯ!";
            if (v.Score0 != v.Score1)
            {
                int win = v.Score0 > v.Score1 ? 0 : 1;
                title = "ПОБЕДА " + (win == 0 ? "КРАСНЫХ!" : "СИНИХ!");
                var myTeams = new HashSet<int>();
                foreach (var k in myKeys) { var e = roster.Find(r => r.Human == k); if (e != null) myTeams.Add(e.Team); }
                if (myTeams.Count == 1) title = myTeams.Contains(win) ? "ПОБЕДА!" : "ПОРАЖЕНИЕ";
            }
            // заголовок выпрыгивает
            float tp = 1 + Mathf.Max(0, 0.35f - tEnd) * 1.8f + Mathf.Sin(tEnd * 3) * 0.02f;
            var ts = new GUIStyle(sTitle) { fontSize = (int)(96 * tp), alignment = TextAnchor.MiddleCenter };
            Shadowed(new Rect(0, 28, VW, 120), title, ts, Mats.Hex("#ffd23f"), Mats.Hex("#c2422f"), 7);
            // счёт: гербы команд со звездой и числом, между ними «vs»
            float cy = 210, bw = 180, bh = 96;
            for (int t = 0; t < 2; t++)
            {
                float pop = Mathf.Clamp01((tEnd - 0.25f - t * 0.15f) * 5);
                float sc = pop < 1 ? Mathf.Lerp(0.3f, 1.1f, pop) : 1;
                float cx = VW / 2 + (t == 0 ? -1 : 1) * 160;
                var r = new Rect(cx - bw * sc / 2, cy - bh * sc / 2, bw * sc, bh * sc);
                GUI.Label(r, "", t == 0 ? sPillRed : sPillBlue);
                float sh = r.height * 0.95f;
                GUI.DrawTexture(new Rect(t == 0 ? r.x - sh * 0.35f : r.xMax - sh * 0.65f, r.center.y - sh * 0.55f, sh * 0.84f, sh), hudShield[t]);
                var nr = t == 0 ? new Rect(r.x + r.width * 0.4f, r.y, r.width * 0.55f, r.height - 8) : new Rect(r.x + r.width * 0.05f, r.y, r.width * 0.55f, r.height - 8);
                Shadowed(nr, (t == 0 ? v.Score0 : v.Score1).ToString(), new GUIStyle(sHudNum) { fontSize = (int)(64 * sc) }, Color.white, Mats.Hex("#0d1030"), 3);
            }
            Shadowed(new Rect(VW / 2 - 40, cy - 30, 80, 56), "vs", new GUIStyle(sHudNum) { fontSize = 34 }, Color.white, Mats.Hex("#0d1030"), 3);
            // звезда матча
            int best = -1, bestScore = 0;
            for (int i = 0; i < roster.Count && i < v.Players.Length; i++)
            {
                if (roster[i].Goalie) continue;
                int sc = v.Players[i].Goals * 2 + v.Players[i].Assists;
                if (sc > bestScore) { bestScore = sc; best = i; }
            }
            if (best >= 0)
                Shadowed(new Rect(0, cy + 62, VW, 34), $"Звезда матча: {roster[best].Name}  ·  голы {v.Players[best].Goals}, пасы {v.Players[best].Assists}", new GUIStyle(sH2) { alignment = TextAnchor.MiddleCenter, fontSize = 22 }, Color.white, Mats.Hex("#0d1030"), 2);
            // кнопки внизу
            float bwid = 300, by = VH - 110;
            GUILayout.BeginArea(new Rect((VW - bwid * 2 - 20) / 2, by, bwid * 2 + 20, 80));
            GUILayout.BeginHorizontal();
            if (mode != Mode.Client) { if (Button("Ещё матч!", Btn("#ffe27a", "#ffb92e", 26), GUILayout.Width(bwid), GUILayout.Height(66))) NewMatch(); }
            else GUILayout.Label("Хост может начать новый матч", sCenter, GUILayout.Width(bwid), GUILayout.Height(66));
            GUILayout.Space(20);
            if (Button("В меню", BtnGhost, GUILayout.Width(bwid), GUILayout.Height(66))) ToMenu();
            GUILayout.EndHorizontal();
            GUILayout.EndArea();
        }

        // ---------------------------------------------------------------- HUD
        string KeyFor(View v, int action)
        {
            // 0 бросок, 1 пас, 2 прыжок, 3 толчок, 4 рывок, 5 удар
            bool pad = v.Src != null && v.Src.Pad >= 0 && GameInput.AnyPad() && v.Src.Scheme != Scheme.Solo;
            var scheme = v.Src?.Scheme ?? Scheme.Solo;
            string[] solo = { "ЛКМ", "ПКМ", "Пробел", "E", "Shift", "R" };
            string[] p1 = { "F", "Q", "Пробел", "E", "Shift", "R" };
            string[] p2 = { "Num0", "Num1", "Num3", "Num2", "Num4", "Num5" };
            string[] gp = { "RT", "X", "A", "B", "LT", "Y" };
            if (pad) return gp[action];
            return scheme == Scheme.P1 ? p1[action] : scheme == Scheme.P2 ? p2[action] : solo[action];
        }

        void DrawHints(View v, ViewState st, Rect area)
        {
            if (v.Follow < 0 || v.Follow >= st.Players.Length || st.Phase != Phase.Play) return;
            var p = st.Players[v.Follow];
            if (p.Brawl)
            {
                // драка: крупная подсказка и шкалы силы
                int opp = -1;
                for (int i = 0; i < st.Players.Length; i++) if (i != v.Follow && st.Players[i].Brawl && Mathf.Abs(st.Players[i].X - p.X) + Mathf.Abs(st.Players[i].Z - p.Z) < 2.5f) opp = i;
                float pulse = 1 + Mathf.Sin(time * 14) * 0.08f;
                Shadowed(new Rect(area.x, VH * 0.55f, area.width, 90), $"ДРАКА! ЖМИ {KeyFor(v, 5)}!", new GUIStyle(sTitle) { fontSize = (int)(54 * pulse), alignment = TextAnchor.MiddleCenter }, Mats.Hex("#ffd23f"), Mats.Hex("#ff4f5e"), 4);
                float myPow = p.BrawlPower, theirs = opp >= 0 ? st.Players[opp].BrawlPower : 0;
                float tot = Mathf.Max(1, myPow + theirs);
                float bw = Mathf.Min(420, area.width * 0.7f), bx = area.x + (area.width - bw) / 2, by = VH * 0.55f + 92;
                Meter(new Rect(bx - 8, by - 8, bw + 16, 44), myPow / tot, "#ffe46a", "#ffa21f", "СИЛА УДАРОВ");
                return;
            }
        }

        void DrawHud(ViewState st)
        {
            if (phaseWipe > 0)
            {   // шторка: тёмно-синяя полоса с шайбой проезжает через экран
                if (Event.current.type == EventType.Repaint) phaseWipe = Mathf.Max(0, phaseWipe - Time.unscaledDeltaTime * 1.6f);
                float t = 1 - phaseWipe, x = Mathf.Lerp(-VW * 0.6f, VW * 1.1f, t);
                GUI.color = new Color(0.05f, 0.06f, 0.18f, 0.92f);
                GUI.DrawTexture(new Rect(x - VW * 0.5f, 0, VW * 0.5f, VH), white);
                GUI.color = Mats.Hex("#ffd23f"); GUI.DrawTexture(new Rect(x, 0, 8, VH), white);
                GUI.color = Color.white;
                GUI.DrawTexture(new Rect(x - 120, VH / 2 - 70, 220, 138), hudPuck);
            }
            // табло хоккейной арены: часы-шайба посередине, по бокам «борта» с гербами команд, счётом и бросками
            for (int t = 0; t < 2; t++)
            {
                int sc = t == 0 ? st.Score0 : st.Score1;
                if (sc != shownScore[t]) { if (sc > shownScore[t]) scorePop[t] = 1; shownScore[t] = sc; }
                if (Event.current.type == EventType.Repaint) scorePop[t] = Mathf.Max(0, scorePop[t] - Time.unscaledDeltaTime * 1.2f);
            }
            float cx0 = VW / 2, top = 10;
            float puckW = 200, puckH = puckW * 200f / 320f;
            float plateW = 300, plateH = 66;
            for (int t = 0; t < 2; t++)
            {
                bool left = t == 0;
                var pr = left ? new Rect(cx0 - plateW - 40, top + 4, plateW + 40, plateH) : new Rect(cx0, top + 4, plateW + 40, plateH);
                GUI.Label(pr, "", sBoard);
                // цветная полоса команды сверху и жёлтый «плинтус» снизу — как у бортов на арене
                Box(new Rect(pr.x + 10, pr.y + 7, pr.width - 20, 6), TeamColors.All[t].Main);
                Box(new Rect(pr.x + 10, pr.y + plateH - 22, pr.width - 20, 6), "#ffd23f");
                // герб
                float shW = 76, shH = shW * 190f / 160f;
                float shX = left ? pr.x - shW * 0.45f : pr.xMax - shW * 0.55f;
                float bob = scorePop[t] > 0 ? Mathf.Sin(scorePop[t] * Mathf.PI * 3) * 6 * scorePop[t] : 0;
                GUI.DrawTexture(new Rect(shX, top - 6 - bob, shW, shH), hudShield[t]);
                // название команды
                string name = left ? "КРАСНЫЕ" : "СИНИЕ";
                var nameR = left ? new Rect(shX + shW + 2, pr.y + 13, 130, 32) : new Rect(shX - 132, pr.y + 13, 130, 32);
                var ns = new GUIStyle(sHudName) { fontSize = 18, alignment = left ? TextAnchor.MiddleLeft : TextAnchor.MiddleRight };
                ns.normal.textColor = Mats.Hex("#1b1f52");
                GUI.Label(nameR, name, ns);
                // броски по воротам
                var ss = new GUIStyle(sHudName) { font = uiFont, fontSize = 12, alignment = left ? TextAnchor.MiddleLeft : TextAnchor.MiddleRight };
                ss.normal.textColor = Mats.Hex("#5a6396");
                GUI.Label(left ? new Rect(nameR.x, pr.y + 38, 130, 16) : new Rect(nameR.x, pr.y + 38, 130, 16), "БРОСКИ  " + shots[t], ss);
                // счёт
                float sp2 = 1 + Mathf.Sin(scorePop[t] * Mathf.PI) * 0.7f * scorePop[t];
                var nsty = new GUIStyle(sHudNum) { fontSize = (int)(54 * sp2) };
                var r = left ? new Rect(cx0 - puckW / 2 - 78, pr.y - 4, 70, plateH) : new Rect(cx0 + puckW / 2 + 8, pr.y - 4, 70, plateH);
                Shadowed(r, (t == 0 ? st.Score0 : st.Score1).ToString(), nsty, scorePop[t] > 0 ? Mats.Hex("#ffd23f") : Mats.Hex(TeamColors.All[t].Main), Mats.Hex("#0d1030"), 3);
            }
            // часы-шайба
            var puckR = new Rect(cx0 - puckW / 2, top - 8, puckW, puckH);
            bool goalNow = st.Phase == Phase.Goal;
            if (goalNow)
            {   // гол-лампа: красное зарево за шайбой
                GUI.color = new Color(1, 0.2f, 0.15f, 0.55f + Mathf.Sin(time * 12) * 0.3f);
                GUI.DrawTexture(new Rect(cx0 - 140, top - 90, 280, 280), circleTex);
                GUI.color = Color.white;
            }
            GUI.DrawTexture(puckR, hudPuck);
            int tt = Mathf.Max(0, Mathf.CeilToInt(st.Time));
            bool hurry = !st.Overtime && st.Phase == Phase.Play && tt <= 10;
            var tsty = new GUIStyle(sHudTime) { fontSize = (int)(44 * (hurry ? 1 + Mathf.Abs(Mathf.Sin(time * 6)) * 0.12f : 1)) };
            Shadowed(new Rect(puckR.x, puckR.y + puckH * 0.41f - 30, puckW, 54), st.Overtime ? "ОТ" : $"{tt / 60}:{tt % 60:00}", tsty, hurry ? Mats.Hex("#ff6a5c") : Color.white, Mats.Hex("#000000"), 2);
            string sub = st.Overtime ? "ДО ГОЛА" : goalNow ? "ГОЛ!" : st.Phase == Phase.Faceoff ? "ВБРАСЫВАНИЕ" : "МАТЧ";
            Shadowed(new Rect(puckR.x, puckR.y + puckH * 0.76f, puckW, puckH * 0.18f), sub, new GUIStyle(sMeterText) { fontSize = 12 }, Mats.Hex("#ffd23f"), Mats.Hex("#000000"), 1);

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
                var bs = new GUIStyle(sTitle) { font = bubbleFont, fontSize = (int)(120 * pop), alignment = TextAnchor.MiddleCenter };
                var centerB = new Vector2(VW / 2, VH * 0.2f + 80);
                var oldM = GUI.matrix;
                if (st.Phase == Phase.Goal)
                {   // вращающиеся лучи гол-лампы
                    GUIUtility.RotateAroundPivot(time * 40, centerB * uiScale);
                    GUI.color = WithA(Mats.Hex(col), 0.75f);
                    GUI.DrawTexture(new Rect(centerB.x - 330, centerB.y - 330, 660, 660), hudRays);
                    GUI.matrix = oldM;
                }
                else
                {   // круг вбрасывания за отсчётом
                    GUI.color = WithA(Mats.Hex("#ff4f5e"), 0.85f);
                    float rr = 88 * pop;
                    GUI.DrawTexture(new Rect(centerB.x - rr, centerB.y - rr, rr * 2, rr * 2), hudRing);
                }
                GUI.color = Color.white;
                Shadowed(new Rect(0, VH * 0.2f, VW, 160), big, bs, Mats.Hex("#ffd23f"), Mats.Hex(col), 8);
                Shadowed(new Rect(0, VH * 0.2f + 150, VW, 44), small, new GUIStyle(sH2) { font = bubbleFont, alignment = TextAnchor.MiddleCenter, fontSize = 30 }, Color.white, Mats.Hex("#1d2250"), 3);
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
                // шкалы: сила броска (появляется, пока замахиваешься) и рывок
                if (!v.HudInit) { v.ChargeA = 0; v.HudInit = true; }
                if (Event.current.type == EventType.Repaint) v.ChargeA = Mathf.MoveTowards(v.ChargeA, p.Charge > 0.02f ? 1 : 0, Time.unscaledDeltaTime * (p.Charge > 0.02f ? 8 : 2.5f));
                if (p.Charge > 0.02f) v.LastCharge = p.Charge;
                if (v.ChargeA > 0.01f)
                {
                    // сила броска — клюшка, которая «наливается» от рукоятки к крюку
                    bool hot = v.LastCharge > 0.7f;
                    float sw = Mathf.Min(360, area.width * 0.6f), sh = sw * 100f / 560f;
                    float jig = hot ? Mathf.Sin(time * 50) * 1.5f : 0;
                    var sr = new Rect(area.x + (area.width - sw) / 2 + jig, VH - 66 - sh + (1 - v.ChargeA) * 16, sw, sh);
                    GUI.color = new Color(1, 1, 1, v.ChargeA);
                    GUI.DrawTexture(sr, hudStickE);
                    DrawClipped(sr, hudStickF, v.LastCharge, 6f / 560, 16f / 560, v.ChargeA);
                    GUI.color = Color.white;
                    if (hot) Shadowed(new Rect(sr.xMax - 40, sr.y - 30, 160, 34), "МОЩНО!", new GUIStyle(sHudNum) { fontSize = 26, alignment = TextAnchor.MiddleLeft }, Mats.Hex("#ffd23f"), Mats.Hex("#a01c33"), 2);
                }
                // рывок — лезвие конька, заполненное «льдом»
                bool turbo = p.Has(Buff.Turbo);
                float bw2 = Mathf.Min(300, area.width * 0.5f), bh2 = bw2 * 72f / 520f;
                var brr = new Rect(area.x + (area.width - bw2) / 2, VH - 20 - bh2, bw2, bh2);
                float ba = p.Stamina < 0.999f || turbo ? 1 : 0.6f;
                GUI.color = new Color(1, 1, 1, ba);
                GUI.DrawTexture(brr, hudBladeE);
                DrawClipped(brr, hudBladeF, turbo ? 1 : p.Stamina, 3f / 520, 9f / 520, ba);
                GUI.color = Color.white;
                if (turbo) Shadowed(new Rect(brr.x, brr.y - 2, brr.width * 0.9f, brr.height * 0.8f), "ТУРБО", sMeterText, Color.white, Mats.Hex("#0d3a5a"), 1.5f);
                DrawHints(v, st, area);
                DrawCrosshair(v, st, p, area);
                // активные бонусы
                float ix = area.x + area.width / 2 - 200, iy = 96;
                for (int b = 0; b < 5; b++)
                {
                    if (!p.Has((Buff)b)) continue;
                    var ic = Mats.Hex(Fx.ItemColors[b]);
                    var br = new Rect(ix, iy + Mathf.Sin(time * 5 + b) * 2, 130, 34);
                    GUI.Label(br, "", Fill("#" + ColorUtility.ToHtmlStringRGB(Color.Lerp(ic, Color.white, 0.35f)), "#" + ColorUtility.ToHtmlStringRGB(Color.Lerp(ic, Color.black, 0.2f))));
                    Shadowed(new Rect(br.x, br.y - 1, br.width, br.height), Fx.ItemNames[b], sMeterText, Color.white, new Color(0.05f, 0.06f, 0.2f, 0.85f), 1.5f);
                    ix += 136;
                }
                // всплывающее сообщение о бонусе
                if (v.ToastT > 0 && !string.IsNullOrEmpty(v.Toast) && !endShown)   // под итогами матча сообщения не показываем
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
        }

        /// Прицел по центру: четыре скобы, которые сжимаются и поворачиваются при замахе.
        /// Бросок летит ровно в прицел; если прицел смотрит в ворота, на воротах видна шайба-метка —
        /// туда прилетит бросок (выше при более сильном замахе).
        void DrawCrosshair(View v, ViewState st, PlayerView p, Rect area)
        {
            if (v.Src == null || st.Phase != Phase.Play || p.Stun || p.Brawl || v.Follow >= roster.Count) return;
            float ch = Mathf.Clamp01(p.Charge);
            bool onGoal = false;
            if (v.Cam != null) AimPoint(v, out onGoal);
            Color col = ch > 0.7f ? Mats.Hex("#ff5a4c") : ch > 0.02f ? Mats.Hex("#ffb14a") : onGoal ? Mats.Hex("#5cf28a") : Color.white;

            var c = new Vector2(area.x + area.width / 2, VH / 2);
            float size = Mathf.Lerp(26, 17, ch) * (1 + Mathf.Sin(time * 7) * 0.04f * (onGoal ? 1 : 0));
            var oldM = GUI.matrix;
            GUIUtility.RotateAroundPivot(ch * 45, c * uiScale);
            GUI.color = col;
            GUI.DrawTexture(new Rect(c.x - size / 2, c.y - size / 2, size, size), hudReticle);
            GUI.matrix = oldM;
            GUI.color = new Color(0.04f, 0.05f, 0.15f, 0.8f);
            GUI.DrawTexture(new Rect(c.x - 2.5f, c.y - 2.5f, 5, 5), circleTex);
            GUI.color = col;
            GUI.DrawTexture(new Rect(c.x - 1.5f, c.y - 1.5f, 3, 3), circleTex);

            GUI.color = Color.white;
        }

        void DrawMinimap(ViewState st)
        {
            bool split = views.Count > 1;
            float mw = split ? 200 : 260, mh = mw * Rink.W / Rink.L;
            float mx = split ? (VW - mw) / 2 : 22, my = split ? 96 : VH - mh - 22;
            GUI.DrawTexture(new Rect(mx, my, mw, mh), hudRink);
            float sx = mw / Rink.L, sz = mh / Rink.W;
            float X(float x) => mx + (x + Rink.L / 2) * sx;
            float Z(float z) => my + (Rink.W / 2 - z) * sz; // сверху вниз: +Z вверху, как на виде сверху
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
                GUI.color = Mats.Hex("#0d1030");
                GUI.DrawTexture(new Rect(X(p.X) - s / 2 - 1.5f, Z(p.Z) - s / 2 - 1.5f, s + 3, s + 3), circleTex);
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
