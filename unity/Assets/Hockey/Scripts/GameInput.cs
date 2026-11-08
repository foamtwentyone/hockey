// Клавиатура, мышь и геймпады. Работает и со старым Input Manager, и с новым Input System.
// Движение задаётся относительно камеры (как в Fall Guys / GTA), камеру вращает мышь или правый стик.
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

    /// Сырые данные с устройства: стик в координатах камеры, поворот камеры и кнопки.
    public struct RawInput
    {
        public float MoveX, MoveY;     // вправо / вперёд относительно камеры
        public float LookX, LookY;     // поворот камеры за кадр, градусы (мышь)
        public float LookRateX, LookRateY; // поворот камеры, -1..1 (правый стик)
        public bool Shoot, Pass, Check, Sprint, Jump;
        public bool AnyMove => MoveX * MoveX + MoveY * MoveY > 0.01f;
    }

    public static class GameInput
    {
        public struct Map { public KeyCode[] Up, Down, Left, Right, Shoot, Pass, Check, Sprint, Jump; }

        public static readonly Map Solo = new Map
        {
            Up = new[] { KeyCode.W, KeyCode.UpArrow }, Down = new[] { KeyCode.S, KeyCode.DownArrow },
            Left = new[] { KeyCode.A, KeyCode.LeftArrow }, Right = new[] { KeyCode.D, KeyCode.RightArrow },
            Shoot = new[] { KeyCode.F, KeyCode.K }, Pass = new[] { KeyCode.Q, KeyCode.J }, Check = new[] { KeyCode.E, KeyCode.L },
            Sprint = new[] { KeyCode.LeftShift, KeyCode.RightShift }, Jump = new[] { KeyCode.Space },
        };
        public static readonly Map P1 = new Map
        {
            Up = new[] { KeyCode.W }, Down = new[] { KeyCode.S }, Left = new[] { KeyCode.A }, Right = new[] { KeyCode.D },
            Shoot = new[] { KeyCode.F }, Pass = new[] { KeyCode.Q }, Check = new[] { KeyCode.E }, Sprint = new[] { KeyCode.LeftShift }, Jump = new[] { KeyCode.Space },
        };
        public static readonly Map P2 = new Map
        {
            Up = new[] { KeyCode.UpArrow }, Down = new[] { KeyCode.DownArrow }, Left = new[] { KeyCode.LeftArrow }, Right = new[] { KeyCode.RightArrow },
            Shoot = new[] { KeyCode.Keypad0, KeyCode.Return }, Pass = new[] { KeyCode.Keypad1, KeyCode.RightShift },
            Check = new[] { KeyCode.Keypad2, KeyCode.RightControl }, Jump = new[] { KeyCode.Keypad3, KeyCode.Slash },
            Sprint = new[] { KeyCode.Keypad4, KeyCode.Period },
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
                case KeyCode.F: return UnityEngine.InputSystem.Key.F;
                case KeyCode.J: return UnityEngine.InputSystem.Key.J;
                case KeyCode.K: return UnityEngine.InputSystem.Key.K;
                case KeyCode.L: return UnityEngine.InputSystem.Key.L;
                case KeyCode.H: return UnityEngine.InputSystem.Key.H;
                case KeyCode.M: return UnityEngine.InputSystem.Key.M;
                case KeyCode.Tab: return UnityEngine.InputSystem.Key.Tab;
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
                case KeyCode.Period: return UnityEngine.InputSystem.Key.Period;
                case KeyCode.Escape: return UnityEngine.InputSystem.Key.Escape;
                case KeyCode.Keypad0: return UnityEngine.InputSystem.Key.Numpad0;
                case KeyCode.Keypad1: return UnityEngine.InputSystem.Key.Numpad1;
                case KeyCode.Keypad2: return UnityEngine.InputSystem.Key.Numpad2;
                case KeyCode.Keypad3: return UnityEngine.InputSystem.Key.Numpad3;
                case KeyCode.Keypad4: return UnityEngine.InputSystem.Key.Numpad4;
                default: return UnityEngine.InputSystem.Key.None;
            }
        }
#endif

        static bool Any(KeyCode[] keys) { foreach (var k in keys) if (Key(k)) return true; return false; }

        public static RawInput Read(InputSource src, float mouseSens)
        {
            var inp = new RawInput();
            if (src == null) return inp;
            Map? map = src.Scheme switch { Scheme.Solo => Solo, Scheme.P1 => P1, Scheme.P2 => P2, _ => (Map?)null };
            if (map.HasValue)
            {
                var m = map.Value;
                inp.MoveY = (Any(m.Up) ? 1 : 0) - (Any(m.Down) ? 1 : 0);
                inp.MoveX = (Any(m.Right) ? 1 : 0) - (Any(m.Left) ? 1 : 0);
                inp.Shoot = Any(m.Shoot); inp.Pass = Any(m.Pass); inp.Check = Any(m.Check); inp.Sprint = Any(m.Sprint); inp.Jump = Any(m.Jump);
            }
            if (src.Mouse)
            {
#if ENABLE_INPUT_SYSTEM
                var ms = UnityEngine.InputSystem.Mouse.current;
                if (ms != null)
                {
                    inp.Shoot |= ms.leftButton.isPressed; inp.Pass |= ms.rightButton.isPressed;
                    var d = ms.delta.ReadValue();
                    inp.LookX = d.x * 0.09f * mouseSens; inp.LookY = d.y * 0.09f * mouseSens;
                }
#else
                inp.Shoot |= Input.GetMouseButton(0); inp.Pass |= Input.GetMouseButton(1);
                try { inp.LookX = Input.GetAxisRaw("Mouse X") * 2.2f * mouseSens; inp.LookY = Input.GetAxisRaw("Mouse Y") * 2.2f * mouseSens; } catch { }
#endif
            }
            if (src.Pad >= 0) ReadPad(src.Pad, ref inp);
            float l = Mathf.Sqrt(inp.MoveX * inp.MoveX + inp.MoveY * inp.MoveY);
            if (l > 1) { inp.MoveX /= l; inp.MoveY /= l; }
            return inp;
        }

        static float Dz(float v) => Mathf.Abs(v) < 0.15f ? 0 : (v - Mathf.Sign(v) * 0.15f) / 0.85f;

        static void ReadPad(int index, ref RawInput inp)
        {
#if ENABLE_INPUT_SYSTEM
            if (index >= Gamepad.all.Count) return;
            var gp = Gamepad.all[index];
            var st = gp.leftStick.ReadValue();
            var rs = gp.rightStick.ReadValue();
            float mx = Dz(st.x), my = Dz(st.y);
            if (gp.dpad.up.isPressed) my = 1; if (gp.dpad.down.isPressed) my = -1;
            if (gp.dpad.left.isPressed) mx = -1; if (gp.dpad.right.isPressed) mx = 1;
            if (mx * mx + my * my > inp.MoveX * inp.MoveX + inp.MoveY * inp.MoveY) { inp.MoveX = mx; inp.MoveY = my; }
            inp.LookRateX = Dz(rs.x); inp.LookRateY = Dz(rs.y);
            inp.Shoot |= gp.rightTrigger.ReadValue() > 0.3f;
            inp.Jump |= gp.buttonSouth.isPressed;
            inp.Pass |= gp.buttonWest.isPressed;
            inp.Check |= gp.buttonEast.isPressed;
            inp.Sprint |= gp.leftTrigger.ReadValue() > 0.3f || gp.leftShoulder.isPressed || gp.rightShoulder.isPressed;
#else
            // Старый Input Manager: кнопки геймпада без настройки осей (стики — только с пакетом Input System).
            string j = "joystick " + (index + 1) + " button ";
            bool B(int n) { try { return Input.GetKey(j + n); } catch { return false; } }
            inp.Jump |= B(0); inp.Check |= B(1); inp.Pass |= B(2); inp.Shoot |= B(3) || B(5); inp.Sprint |= B(4);
#endif
        }

        public static bool AnyPad()
        {
#if ENABLE_INPUT_SYSTEM
            return Gamepad.all.Count > 0;
#else
            return Input.GetJoystickNames().Length > 0;
#endif
        }

        /// Нажата ли «Пауза» на любом устройстве.
        public static bool PausePressed()
        {
            if (KeyDown(KeyCode.Escape)) return true;
#if ENABLE_INPUT_SYSTEM
            foreach (var gp in Gamepad.all) if (gp.startButton.wasPressedThisFrame) return true;
#endif
            return false;
        }
    }
}
