// Автотест: игра сама проходит сценарий (меню → матч с ботами → бросок и гол → крупный план → сплит-скрин),
// сохраняет скриншоты и лог, затем закрывается. Нужен, чтобы проверять картинку без человека за компьютером.
//
// Запуск:
//   • в редакторе — меню Hockey → «Автотест (Play)»;
//   • в собранной игре — с аргументом -autotest [-autotestDir <папка>].
// Результат: папка Autotest/ рядом с проектом (или указанная) — PNG-скриншоты и autotest.log.
using System;
using System.Collections;
using System.IO;
using System.Text;
using UnityEngine;

namespace Hockey
{
    public partial class HockeyGame
    {
        bool autotest, autoFreeze;
        string autotestDir;
        PlayerInput autoInput;
        readonly StringBuilder autoLog = new StringBuilder();
        int autoErrors, autoFrames;
        float autoFpsTime;


        void StartAutotestIfRequested()
        {
            var args = Environment.GetCommandLineArgs();
            bool req = Array.IndexOf(args, "-autotest") >= 0;
#if UNITY_EDITOR
            if (UnityEditor.SessionState.GetBool("hockey.autotest", false))
            {
                req = true;
                UnityEditor.SessionState.SetBool("hockey.autotest", false);
            }
#endif
            if (!req) return;
            autotest = true;
            int di = Array.IndexOf(args, "-autotestDir");
            autotestDir = di >= 0 && di + 1 < args.Length ? args[di + 1] : DefaultAutotestDir();
            Directory.CreateDirectory(autotestDir);
            foreach (var f in Directory.GetFiles(autotestDir, "*.png")) File.Delete(f);
            Application.logMessageReceived += OnLog;
            Log($"Автотест: Unity {Application.unityVersion}, {SystemInfo.graphicsDeviceName} ({SystemInfo.graphicsDeviceType}), экран {Screen.width}x{Screen.height}, цвет {QualitySettings.activeColorSpace}");
            StartCoroutine(AutotestRun());
        }

        static string DefaultAutotestDir()
        {
#if UNITY_EDITOR
            return Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Autotest"));
#else
            // рядом с папкой собранной игры
            return Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Autotest"));
#endif
        }

        void OnLog(string msg, string stack, LogType type)
        {
            if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert)
            {
                autoErrors++;
                Log($"[{type}] {msg}\n{stack}");
            }
            else if (type == LogType.Warning) Log($"[Warning] {msg}");
        }

        void Log(string s)
        {
            string line = $"[{Time.realtimeSinceStartup,7:F2}] {s}";
            autoLog.AppendLine(line);
            Debug.Log("[Autotest] " + s);
        }

        IEnumerator Shot(string name)
        {
            yield return new WaitForEndOfFrame();
            // ReadPixels из кадра целиком (с интерфейсом) — без модуля ScreenCapture
            var tex = new Texture2D(Screen.width, Screen.height, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, Screen.width, Screen.height), 0, 0);
            tex.Apply();
            string path = Path.Combine(autotestDir, name + ".png");
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Destroy(tex);
            Log($"Скриншот {name}.png" + StateLine());
        }

        string StateLine()
        {
            if (sim == null) return "";
            var me = sim.Players.Find(p => p.Human == "local0");
            string meStr = me != null ? $" игрок=({me.X:F1},{me.Z:F1}) курс={me.H:F2}" : "";
            return $" | фаза={sim.Phase} счёт={sim.Score[0]}:{sim.Score[1]} время={sim.Time:F0} шайба=({sim.Puck.X:F1},{sim.Puck.Y:F2},{sim.Puck.Z:F1}) владелец={sim.Puck.Owner}{meStr}";
        }

        IEnumerator Wait(float seconds)
        {
            float end = Time.realtimeSinceStartup + seconds;
            while (Time.realtimeSinceStartup < end) { autoFrames++; yield return null; }
        }

        PlayerInput Toward(float dirX, float dirZ, bool sprint = false) => new PlayerInput { MoveX = dirX, MoveZ = dirZ, Sprint = sprint };

        IEnumerator AutotestRun()
        {
            autoFpsTime = Time.realtimeSinceStartup;
            yield return Wait(3f);
            yield return Shot("01_menu");
            uiPage = UiPage.Play; yield return Wait(0.5f); yield return Shot("02_menu_play");
            uiPage = UiPage.Settings; yield return Wait(0.5f); yield return Shot("03_settings");
            uiPage = UiPage.Controls; yield return Wait(0.5f); yield return Shot("04_controls");
            uiPage = UiPage.Locker; yield return Wait(1.2f); yield return Shot("04b_locker");
            uiPage = UiPage.Credits; yield return Wait(0.5f); yield return Shot("04c_credits");
            uiPage = UiPage.Main; yield return Wait(1.2f);

            // одиночный матч в режиме «Аркада»
            cfg.Skaters = 3; cfg.Duration = 180; cfg.Difficulty = 1; cfg.Arcade = true;
            StartLocal(false);
            yield return Wait(1.0f);
            yield return Shot("05_faceoff");
            var me = sim.Players.Find(p => p.Human == "local0");
            int dir = me != null ? Rink.TeamDir[me.Team] : 1;

            autoInput = Toward(dir, 0, true);
            yield return Wait(2.5f);
            yield return Shot("06_skating");
            autoInput = Toward(dir * 0.3f, 1);
            yield return Wait(1.2f);
            yield return Shot("07_turning");

            // прыжок
            autoInput = new PlayerInput { Jump = true };
            yield return Wait(0.25f);
            yield return Shot("08_jump");
            autoInput = default;
            yield return Wait(0.6f);

            // бросок по воротам
            if (me != null)
            {
                me.X = dir * 13; me.Z = 1.5f; me.H = dir > 0 ? Mathf.PI / 2 : -Mathf.PI / 2; me.Vx = dir * 4; me.Vz = 0; me.Y = 0; me.Vy = 0; me.Stun = 0;
                sim.Puck.Owner = me.Idx; sim.Puck.InNet = false;
                if (sim.Phase != Phase.Play) sim.Phase = Phase.Play;
                autoInput = new PlayerInput { MoveX = dir * 0.5f, Shoot = true };
                yield return Wait(0.45f);
                yield return Shot("09_windup");
                autoInput = default;
                yield return Wait(0.3f);
                yield return Shot("10_shot");
                yield return Wait(1.2f);
                yield return Shot("11_after_shot");
                Log($"После броска: фаза={sim.Phase}, счёт {sim.Score[0]}:{sim.Score[1]}");
                while (sim.Phase != Phase.Play && sim.Phase != Phase.End) yield return Wait(0.2f);

                // бонусы: великан и заморозка
                me.X = 0; me.Z = 5; me.H = dir > 0 ? Mathf.PI / 2 : -Mathf.PI / 2; me.Vx = 0; me.Vz = 0;
                sim.Items.Clear();
                sim.Items.Add(new Item { X = dir * 1.6f, Z = 5, Type = (int)Buff.Giant, Life = 12 });
                sim.Items.Add(new Item { X = dir * 4.5f, Z = 5, Type = (int)Buff.Frozen, Life = 12 });
                sim.Items.Add(new Item { X = dir * 4, Z = 9, Type = (int)Buff.Turbo, Life = 12 });
                sim.Items.Add(new Item { X = -dir * 4, Z = 9, Type = (int)Buff.Mega, Life = 12 });
                autoInput = Toward(dir, 0);
                foreach (var v in views) v.Init = false; // камера сразу за спину после телепорта
                yield return Wait(0.2f);
                yield return Shot("12_items");
                yield return Wait(0.6f);
                yield return Shot("13_giant");
                yield return Wait(0.8f);
                yield return Shot("14_freeze");
                Log($"Бонусы: великан={me.Has(Buff.Giant)}, у соперников заморозка={sim.Players.Exists(q => q.Team != me.Team && q.Has(Buff.Frozen))}");

                // нырок
                autoInput = Toward(-dir, 0, true);
                yield return Wait(1.2f);
                autoInput = new PlayerInput { MoveX = -dir, Jump = true };
                yield return Wait(0.2f);
                yield return Shot("15_dive");
                autoInput = default;
                yield return Wait(1f);
            }

            // крупные планы
            if (views.Count > 0 && me != null && sim != null)
            {
                autoFreeze = true; // останавливаем физику без меню паузы
                var cam = views[0].Cam;
                var keep = views.ToArray();
                views.Clear();
                var p = new Vector3(me.X, 0, me.Z);
                var fwd = new Vector3(Mathf.Sin(me.H), 0, Mathf.Cos(me.H));
                cam.transform.position = p + fwd * 3.6f + Vector3.up * 1.7f + Vector3.Cross(Vector3.up, fwd) * 1.2f;
                cam.transform.LookAt(p + Vector3.up * 1.1f);
                yield return Wait(0.4f);
                yield return Shot("16_closeup");
                var g = sim.Players.Find(q => q.Goalie);
                if (g != null)
                {
                    var gp = new Vector3(g.X, 0, g.Z);
                    var gf = new Vector3(Mathf.Sin(g.H), 0, Mathf.Cos(g.H));
                    cam.transform.position = gp + gf * 3.2f + Vector3.up * 1.6f;
                    cam.transform.LookAt(gp + Vector3.up * 0.9f);
                    yield return Wait(0.4f);
                    yield return Shot("17_goalie");
                }
                // лицо и шлем крупно
                var head = p + Vector3.up * 1.35f;
                cam.transform.position = head + fwd * 1.5f + Vector3.up * 0.1f + Vector3.Cross(Vector3.up, fwd) * 0.5f;
                cam.transform.LookAt(head);
                yield return Wait(0.4f);
                yield return Shot("17b_face");
                // руки и краги крупно, с двух сторон
                var hands = p + Vector3.up * 0.8f;
                cam.transform.position = hands + fwd * 3.4f + Vector3.up * 0.4f;
                cam.transform.LookAt(hands);
                yield return Wait(0.4f);
                yield return Shot("17c_hands_front");
                cam.transform.position = hands - Vector3.Cross(Vector3.up, fwd) * 3.4f + fwd * 0.8f + Vector3.up * 0.4f;   // слева — видно левую руку
                cam.transform.LookAt(hands);
                yield return Wait(0.4f);
                yield return Shot("17d_hands_side");
                cam.transform.position = new Vector3(0, 46, -0.01f);
                cam.transform.rotation = Quaternion.Euler(90, 0, 0);
                yield return Wait(0.4f);
                yield return Shot("18_topdown");
                cam.transform.position = new Vector3(-24, 9, -22);
                cam.transform.LookAt(new Vector3(0, 0, 0));
                yield return Wait(0.4f);
                yield return Shot("19_arena");
                // сетка ворот: шайба влетает и прогибает её
                cam.transform.position = new Vector3(Rink.GoalX + 0.9f, 1.5f, 3.6f);   // сбоку от ворот, вратарь не загораживает
                cam.transform.LookAt(new Vector3(Rink.GoalX + 0.6f, 0.55f, 0));
                yield return Wait(0.3f);
                yield return Shot("19a_net");
                for (int i = 0; i < 9; i++) arena.UpdateNets(new Vector3(Rink.GoalX - 0.4f + i * 0.2f, 0.6f, 0.5f), 1 / 60f);
                yield return null;
                yield return Shot("19a_net_hit");
                views.AddRange(keep);
                autoFreeze = false;
            }

            // шайба в голову: сначала слетает шлем, потом нокаут
            if (me != null)
            {
                me.Stun = 0; me.Brawl = 0; me.Vx = me.Vz = 0;
                foreach (var v in views) v.Init = false;
                autoInput = default;
                yield return Wait(0.5f);
                sim.TestHeadShot(me.Idx);
                yield return Wait(0.35f);
                yield return Shot("19d_helmet_off");
                yield return Wait(0.9f);
                yield return Shot("19e_helmet_on_ice");
                Log($"После попадания: шлем={me.Helmet}");
                me.Stagger = 0;
                sim.TestHeadShot(me.Idx);
                yield return Wait(1.6f);
                yield return Shot("19f_knockout");
                Log($"Нокаут: оглушён={me.Stun > 0} ({me.Stun:F1} c)");
                yield return Wait(2.5f);
            }

            // драка: ставим соперника рядом и сцепляем
            if (me != null)
            {
                var opp = sim.Players.Find(q => q.Team != me.Team && !q.Goalie);
                if (opp != null)
                {
                    opp.X = me.X + 1f; opp.Z = me.Z; opp.Vx = opp.Vz = 0;
                    sim.ForceBrawl(me.Idx, opp.Idx);
                    autoInput = new PlayerInput { Fight = true };
                    yield return Wait(0.4f);
                    autoInput = default;
                    yield return Wait(0.3f);
                    yield return Shot("19b_brawl");
                    for (int i = 0; i < 6; i++) { autoInput = new PlayerInput { Fight = i % 2 == 0 }; yield return Wait(0.15f); }
                    yield return Wait(2.2f);
                    yield return Shot("19c_after_brawl");
                    Log($"После драки: я оглушён={me.Stun > 0}, соперник оглушён={opp.Stun > 0}");
                }
            }

            // пусть боты поиграют
            autoInput = Toward(0.4f, 0.6f);
            yield return Wait(8f);
            yield return Shot("20_gameplay");
            Log($"Матч идёт: {StateLine()}");

            // конец матча
            sim.Time = 0.4f;
            if (sim.Score[0] == sim.Score[1]) sim.Score[0]++;
            yield return Wait(3f);
            yield return Shot("21_end");

            // сплит-скрин
            ToMenu();
            cfg.Arcade = false;
            StartLocal(true);
            autoInput = Toward(1, 0);
            yield return Wait(4f);
            yield return Shot("22_split");

            autoInput = default;
            ToMenu();
            yield return Wait(1.5f);
            yield return Shot("23_menu_again");

            float elapsed = Time.realtimeSinceStartup - autoFpsTime;
            Log($"Готово. Кадров: {autoFrames}, средний FPS: {autoFrames / Mathf.Max(0.01f, elapsed):F1}, ошибок в логе: {autoErrors}, мультяшный шейдер: {Mats.ToonAvailable}");
            File.WriteAllText(Path.Combine(autotestDir, "autotest.log"), autoLog.ToString(), Encoding.UTF8);
            Application.logMessageReceived -= OnLog;
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit(autoErrors > 0 ? 1 : 0);
#endif
        }
    }
}
