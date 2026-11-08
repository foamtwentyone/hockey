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

        PlayerInput ReadSource(InputSource s) => autotest ? autoInput : GameInput.Read(s);

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
            Log($"Автотест: Unity {Application.unityVersion}, {SystemInfo.graphicsDeviceName} ({SystemInfo.graphicsDeviceType}), экран {Screen.width}x{Screen.height}");
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
            var tex = ScreenCapture.CaptureScreenshotAsTexture();
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

        IEnumerator AutotestRun()
        {
            autoFpsTime = Time.realtimeSinceStartup;
            yield return Wait(2.5f);
            yield return Shot("01_menu");

            // одиночный матч
            cfg.Skaters = 3; cfg.Duration = 180; cfg.Difficulty = 1;
            StartLocal(false);
            yield return Wait(1.0f);
            yield return Shot("02_faceoff");

            // едем вперёд с поворотами
            autoInput = new PlayerInput { Throttle = 1 };
            yield return Wait(2.5f);
            yield return Shot("03_skating");
            autoInput = new PlayerInput { Throttle = 1, Turn = 0.6f, Sprint = true };
            yield return Wait(1.2f);
            autoInput = new PlayerInput { Throttle = 1, Turn = -0.6f };
            yield return Wait(1.2f);
            yield return Shot("04_turning");

            // выводим игрока с шайбой к чужим воротам и бросаем
            var me = sim.Players.Find(p => p.Human == "local0");
            if (me != null)
            {
                int dir = Rink.TeamDir[me.Team];
                me.X = dir * 14; me.Z = 1.5f; me.H = dir > 0 ? Mathf.PI / 2 : -Mathf.PI / 2; me.Vx = dir * 5; me.Vz = 0;
                sim.Puck.Owner = me.Idx; sim.Puck.InNet = false;
                if (sim.Phase != Phase.Play) { sim.Phase = Phase.Play; }
                autoInput = new PlayerInput { Throttle = 1 };
                yield return Wait(0.6f);
                autoInput = new PlayerInput { Throttle = 1, Shoot = true };
                yield return Wait(0.5f);
                yield return Shot("05_windup");
                autoInput = new PlayerInput { Throttle = 0.5f };
                yield return Wait(0.35f);
                yield return Shot("06_shot");
                yield return Wait(1.0f);
                yield return Shot("07_after_shot");
                Log($"После броска: фаза={sim.Phase}, счёт {sim.Score[0]}:{sim.Score[1]}");
            }
            autoInput = default;

            // крупный план персонажей
            if (views.Count > 0 && me != null && sim != null)
            {
                autoFreeze = true; // останавливаем физику без меню паузы
                var cam = views[0].Cam;
                var keep = views.ToArray();
                views.Clear(); // временно отключаем управление камерой
                var p = new Vector3(me.X, 0, me.Z);
                var fwd = new Vector3(Mathf.Sin(me.H), 0, Mathf.Cos(me.H));
                cam.transform.position = p + fwd * 3.2f + Vector3.up * 1.6f + Vector3.Cross(Vector3.up, fwd) * 1.0f;
                cam.transform.LookAt(p + Vector3.up * 1.0f);
                yield return Wait(0.3f);
                yield return Shot("08_closeup");
                // вратарь крупно
                var g = sim.Players.Find(q => q.Goalie);
                if (g != null)
                {
                    var gp = new Vector3(g.X, 0, g.Z);
                    var gf = new Vector3(Mathf.Sin(g.H), 0, Mathf.Cos(g.H));
                    cam.transform.position = gp + gf * 3f + Vector3.up * 1.5f;
                    cam.transform.LookAt(gp + Vector3.up * 0.8f);
                    yield return Wait(0.3f);
                    yield return Shot("09_goalie");
                }
                // вид сверху на всю площадку
                cam.transform.position = new Vector3(0, 45, -0.01f);
                cam.transform.rotation = Quaternion.Euler(90, 0, 0);
                yield return Wait(0.3f);
                yield return Shot("10_topdown");
                views.AddRange(keep);
                autoFreeze = false;
            }

            // пусть боты поиграют, проверим, что всё стабильно
            autoInput = new PlayerInput { Throttle = 0.6f, Turn = 0.2f };
            yield return Wait(8f);
            yield return Shot("11_gameplay");
            Log($"Матч идёт: {StateLine()}");

            // сплит-скрин
            StartLocal(true);
            autoInput = new PlayerInput { Throttle = 1 };
            yield return Wait(4f);
            yield return Shot("12_split");

            // меню снова
            autoInput = default;
            ToMenu();
            yield return Wait(1f);
            yield return Shot("13_menu_again");

            float elapsed = Time.realtimeSinceStartup - autoFpsTime;
            Log($"Готово. Кадров: {autoFrames}, средний FPS: {autoFrames / Mathf.Max(0.01f, elapsed):F1}, ошибок в логе: {autoErrors}");
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
