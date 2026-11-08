// Клавиатура, мышь и геймпады. Работает и со старым Input Manager, и с новым Input System.
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace Hockey
{
    public enum Scheme { None, Solo, P1, P2 }

    public class InputSource
    {
        public string Key;
        public Scheme Scheme;
        public int Pad = -1;
        public bool Mouse;
    }

    public static class GameInput
    {
        struct Map { public KeyCode[] Up, Down, Left, Right, Shoot, Pass, Check, Sprint; }

        static readonly Map Solo = new Map
        {
            Up = new[] { KeyCode.W, KeyCode.UpArrow }, Down = new[] { KeyCode.S, KeyCode.DownArrow },
            Left = new[] { KeyCode.A, KeyCode.LeftArrow }, Right = new[] { KeyCode.D, KeyCode.RightArrow },
            Shoot = new[] { KeyCode.Space, KeyCode.K }, Pass = new[] { KeyCode.Q, KeyCode.J }, Check = new[] { KeyCode.E, KeyCode.L },
            Sprint = new[] { KeyCode.LeftShift, KeyCode.RightShift },
        };
        static readonly Map P1 = new Map
        {
            Up = new[] { KeyCode.W }, Down = new[] { KeyCode.S }, Left = new[] { KeyCode.A }, Right = new[] { KeyCode.D },
            Shoot = new[] { KeyCode.Space }, Pass = new[] { KeyCode.Q }, Check = new[] { KeyCode.E }, Sprint = new[] { KeyCode.LeftShift },
        };
        static readonly Map P2 = new Map
        {
            Up = new[] { KeyCode.UpArrow }, Down = new[] { KeyCode.DownArrow }, Left = new[] { KeyCode.LeftArrow }, Right = new[] { KeyCode.RightArrow },
            Shoot = new[] { KeyCode.Return, KeyCode.Keypad0 }, Pass = new[] { KeyCode.RightShift, KeyCode.Keypad1 },
            Check = new[] { KeyCode.RightControl, KeyCode.Keypad2 }, Sprint = new[] { KeyCode.Slash, KeyCode.Keypad3 },
        };

        public static bool Key(KeyCode k)
        {
#if ENABLE_INPUT_SYSTEM
            var kb = Keyboard.current;
            if (kb == null) return false;
            var key = ToKey(k);
            return key != UnityEngine.InputSystem.Key.None && kb[key].isPressed;
#else
            return Input.GetKey(k);
#endif
        }

        public static bool KeyDown(KeyCode k)
        {
#if ENABLE_INPUT_SYSTEM
            var kb = Keyboard.current;
            if (kb == null) return false;
            var key = ToKey(k);
            return key != UnityEngine.InputSystem.Key.None && kb[key].wasPressedThisFrame;
#else
            return Input.GetKeyDown(k);
#endif
        }

#if ENABLE_INPUT_SYSTEM
        static Key ToKey(KeyCode k)
        {
            switch (k)
            {
                case KeyCode.W: return UnityEngine.InputSystem.Key.W;
                case KeyCode.A: return UnityEngine.InputSystem.Key.A;
                case KeyCode.S: return UnityEngine.InputSystem.Key.S;
                case KeyCode.D: return UnityEngine.InputSystem.Key.D;
                case KeyCode.Q: return UnityEngine.InputSystem.Key.Q;
                case KeyCode.E: return UnityEngine.InputSystem.Key.E;
                case KeyCode.J: return UnityEngine.InputSystem.Key.J;
                case KeyCode.K: return UnityEngine.InputSystem.Key.K;
                case KeyCode.L: return UnityEngine.InputSystem.Key.L;
                case KeyCode.H: return UnityEngine.InputSystem.Key.H;
                case KeyCode.M: return UnityEngine.InputSystem.Key.M;
                case KeyCode.Space: return UnityEngine.InputSystem.Key.Space;
                case KeyCode.LeftShift: return UnityEngine.InputSystem.Key.LeftShift;
                case KeyCode.RightShift: return UnityEngine.InputSystem.Key.RightShift;
                case KeyCode.RightControl: return UnityEngine.InputSystem.Key.RightCtrl;
                case KeyCode.UpArrow: return UnityEngine.InputSystem.Key.UpArrow;
                case KeyCode.DownArrow: return UnityEngine.InputSystem.Key.DownArrow;
                case KeyCode.LeftArrow: return UnityEngine.InputSystem.Key.LeftArrow;
                case KeyCode.RightArrow: return UnityEngine.InputSystem.Key.RightArrow;
                case KeyCode.Return: return UnityEngine.InputSystem.Key.Enter;
                case KeyCode.Slash: return UnityEngine.InputSystem.Key.Slash;
                case KeyCode.Escape: return UnityEngine.InputSystem.Key.Escape;
                case KeyCode.Keypad0: return UnityEngine.InputSystem.Key.Numpad0;
                case KeyCode.Keypad1: return UnityEngine.InputSystem.Key.Numpad1;
                case KeyCode.Keypad2: return UnityEngine.InputSystem.Key.Numpad2;
                case KeyCode.Keypad3: return UnityEngine.InputSystem.Key.Numpad3;
                default: return UnityEngine.InputSystem.Key.None;
            }
        }
#endif

        static bool Any(KeyCode[] keys) { foreach (var k in keys) if (Key(k)) return true; return false; }

        public static PlayerInput Read(InputSource src)
        {
            var inp = new PlayerInput();
            if (src == null) return inp;
            Map? map = src.Scheme switch { Scheme.Solo => Solo, Scheme.P1 => P1, Scheme.P2 => P2, _ => (Map?)null };
            if (map.HasValue)
            {
                var m = map.Value;
                inp.Throttle = (Any(m.Up) ? 1 : 0) - (Any(m.Down) ? 1 : 0);
                // В Unity рост угла курса — поворот направо (по часовой стрелке при виде сверху)
                inp.Turn = (Any(m.Right) ? 1 : 0) - (Any(m.Left) ? 1 : 0);
                inp.Shoot = Any(m.Shoot); inp.Pass = Any(m.Pass); inp.Check = Any(m.Check); inp.Sprint = Any(m.Sprint);
            }
            if (src.Mouse)
            {
#if ENABLE_INPUT_SYSTEM
                var ms = UnityEngine.InputSystem.Mouse.current;
                if (ms != null) { inp.Shoot |= ms.leftButton.isPressed; inp.Pass |= ms.rightButton.isPressed; }
#else
                inp.Shoot |= Input.GetMouseButton(0); inp.Pass |= Input.GetMouseButton(1);
#endif
            }
            if (src.Pad >= 0) ReadPad(src.Pad, ref inp);
            return inp;
        }

        static float Dz(float v) => Mathf.Abs(v) < 0.18f ? 0 : v;

        static void ReadPad(int index, ref PlayerInput inp)
        {
            float ax, ay, rt, lt;
            bool a, b, x, y, lb, rb, du, dd, dl, dr;
#if ENABLE_INPUT_SYSTEM
            if (index >= Gamepad.all.Count) return;
            var gp = Gamepad.all[index];
            var st = gp.leftStick.ReadValue();
            ax = st.x; ay = st.y;
            rt = gp.rightTrigger.ReadValue(); lt = gp.leftTrigger.ReadValue();
            a = gp.buttonSouth.isPressed; b = gp.buttonEast.isPressed; x = gp.buttonWest.isPressed; y = gp.buttonNorth.isPressed;
            lb = gp.leftShoulder.isPressed; rb = gp.rightShoulder.isPressed;
            du = gp.dpad.up.isPressed; dd = gp.dpad.down.isPressed; dl = gp.dpad.left.isPressed; dr = gp.dpad.right.isPressed;
#else
            // Старый Input Manager: кнопки геймпада доступны без настройки осей, стик — через стандартные оси.
            string j = "joystick " + (index + 1) + " button ";
            bool B(int n) { try { return Input.GetKey(j + n); } catch { return false; } }
            ax = 0; ay = 0; rt = 0; lt = 0; // стик доступен только с пакетом Input System
            a = B(0); b = B(1); x = B(2); y = B(3); lb = B(4); rb = B(5);
            du = dd = dl = dr = false;
#endif
            float thr = Dz(ay);
            if (rt > 0.1f) thr = Mathf.Max(thr, rt);
            if (lt > 0.1f) thr = Mathf.Min(thr, -lt);
            if (du) thr = 1; if (dd) thr = -1;
            if (Mathf.Abs(thr) > Mathf.Abs(inp.Throttle)) inp.Throttle = thr;
            float tr = Dz(ax);
            if (dl) tr = -1; if (dr) tr = 1;
            if (Mathf.Abs(tr) > Mathf.Abs(inp.Turn)) inp.Turn = tr;
            inp.Shoot |= a; inp.Pass |= b || y; inp.Check |= x; inp.Sprint |= lb || rb;
        }

        public static bool AnyPad()
        {
#if ENABLE_INPUT_SYSTEM
            return Gamepad.all.Count > 0;
#else
            return Input.GetJoystickNames().Length > 0;
#endif
        }
    }
}
