using UnityEngine;
#if ENABLE_INPUT_SYSTEM && WILDLENS_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace WildLens.Core
{
    /// <summary>
    /// Single point of contact between gameplay code and the input backend.
    /// Uses the Input System package when it is installed and active, otherwise the legacy Input Manager,
    /// so the slice runs in either project configuration. Rebinding / gamepad support can be added here later
    /// without touching gameplay code.
    /// </summary>
    public static class GameInput
    {
        // The new Input System reports raw pixel deltas; the legacy "Mouse X/Y" axes are pixels * 0.1.
        private const float NewInputMouseScale = 0.1f;

        public static Vector2 Move
        {
            get
            {
                float x = 0f, y = 0f;
                if (Held(Key.Left)) x -= 1f;
                if (Held(Key.Right)) x += 1f;
                if (Held(Key.Back)) y -= 1f;
                if (Held(Key.Forward)) y += 1f;
                var v = new Vector2(x, y);
                return v.sqrMagnitude > 1f ? v.normalized : v;
            }
        }

        public static Vector2 Look
        {
            get
            {
#if ENABLE_INPUT_SYSTEM && WILDLENS_INPUT_SYSTEM
                var mouse = Mouse.current;
                return mouse == null ? Vector2.zero : mouse.delta.ReadValue() * NewInputMouseScale;
#else
                return new Vector2(Input.GetAxisRaw("Mouse X"), Input.GetAxisRaw("Mouse Y"));
#endif
            }
        }

        /// <summary>Scroll wheel in "notches" (positive = away from the user).</summary>
        public static float Zoom
        {
            get
            {
#if ENABLE_INPUT_SYSTEM && WILDLENS_INPUT_SYSTEM
                var mouse = Mouse.current;
                return mouse == null ? 0f : mouse.scroll.ReadValue().y / 120f;
#else
                return Input.mouseScrollDelta.y;
#endif
            }
        }

        public static bool SprintHeld => Held(Key.Sprint);
        public static bool CameraRaiseHeld => MouseHeld(1);
        public static bool ShutterPressed => MouseDown(0) || Pressed(Key.Shutter);
        public static bool PrimaryClickPressed => MouseDown(0);
        public static bool JournalTogglePressed => Pressed(Key.Journal) || Pressed(Key.JournalAlt);
        public static bool CancelPressed => Pressed(Key.Cancel);
        public static bool NavigateUpPressed => Pressed(Key.Forward) || Pressed(Key.Up);
        public static bool NavigateDownPressed => Pressed(Key.Back) || Pressed(Key.Down);
        public static bool ToggleHintsPressed => Pressed(Key.Hints);
        /// <summary>
        /// C toggles crouch (primary). Holding Left Ctrl also crouches; handy in builds, but in the Unity Editor
        /// Ctrl combinations can trigger editor shortcuts (Ctrl+P stops Play mode), so C is the advertised key.
        /// </summary>
        public static bool CrouchTogglePressed => Pressed(Key.CrouchToggle);
        public static bool CrouchHeld => Held(Key.CrouchHold);

        private enum Key { Forward, Back, Left, Right, Up, Down, Sprint, Shutter, Journal, JournalAlt, Cancel, Hints, CrouchToggle, CrouchHold }

#if ENABLE_INPUT_SYSTEM && WILDLENS_INPUT_SYSTEM
        private static UnityEngine.InputSystem.Controls.KeyControl Control(Keyboard k, Key key)
        {
            switch (key)
            {
                case Key.Forward: return k.wKey;
                case Key.Back: return k.sKey;
                case Key.Left: return k.aKey;
                case Key.Right: return k.dKey;
                case Key.Up: return k.upArrowKey;
                case Key.Down: return k.downArrowKey;
                case Key.Sprint: return k.leftShiftKey;
                case Key.Shutter: return k.fKey;
                case Key.Journal: return k.jKey;
                case Key.JournalAlt: return k.tabKey;
                case Key.Cancel: return k.escapeKey;
                case Key.Hints: return k.hKey;
                case Key.CrouchToggle: return k.cKey;
                case Key.CrouchHold: return k.leftCtrlKey;
                default: return null;
            }
        }

        private static bool Held(Key key)
        {
            var k = Keyboard.current;
            var c = k == null ? null : Control(k, key);
            return c != null && c.isPressed;
        }

        private static bool Pressed(Key key)
        {
            var k = Keyboard.current;
            var c = k == null ? null : Control(k, key);
            return c != null && c.wasPressedThisFrame;
        }

        private static bool MouseHeld(int button)
        {
            var m = Mouse.current;
            if (m == null) return false;
            return button == 0 ? m.leftButton.isPressed : m.rightButton.isPressed;
        }

        private static bool MouseDown(int button)
        {
            var m = Mouse.current;
            if (m == null) return false;
            return button == 0 ? m.leftButton.wasPressedThisFrame : m.rightButton.wasPressedThisFrame;
        }
#else
        private static KeyCode Code(Key key)
        {
            switch (key)
            {
                case Key.Forward: return KeyCode.W;
                case Key.Back: return KeyCode.S;
                case Key.Left: return KeyCode.A;
                case Key.Right: return KeyCode.D;
                case Key.Up: return KeyCode.UpArrow;
                case Key.Down: return KeyCode.DownArrow;
                case Key.Sprint: return KeyCode.LeftShift;
                case Key.Shutter: return KeyCode.F;
                case Key.Journal: return KeyCode.J;
                case Key.JournalAlt: return KeyCode.Tab;
                case Key.Cancel: return KeyCode.Escape;
                case Key.Hints: return KeyCode.H;
                case Key.CrouchToggle: return KeyCode.C;
                case Key.CrouchHold: return KeyCode.LeftControl;
                default: return KeyCode.None;
            }
        }

        private static bool Held(Key key) => Input.GetKey(Code(key));
        private static bool Pressed(Key key) => Input.GetKeyDown(Code(key));
        private static bool MouseHeld(int button) => Input.GetMouseButton(button);
        private static bool MouseDown(int button) => Input.GetMouseButtonDown(button);
#endif
    }
}
