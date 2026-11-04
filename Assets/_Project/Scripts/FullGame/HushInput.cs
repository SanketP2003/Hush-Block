using UnityEngine;
using UnityEngine.EventSystems;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace HushBlocks.FullGame
{
    /// <summary>
    /// Thin input facade for the game. Supports mouse, touch, keyboard and
    /// gamepad through the new Input System, with a legacy fallback, while
    /// making sure presses over the HUD never fall through to gameplay.
    /// </summary>
    public static class HushInput
    {
        public static bool TapDown()
        {
#if ENABLE_INPUT_SYSTEM
            var keyboard = Keyboard.current;
            if (keyboard != null &&
                (keyboard.spaceKey.wasPressedThisFrame ||
                 keyboard.enterKey.wasPressedThisFrame ||
                 keyboard.numpadEnterKey.wasPressedThisFrame))
            {
                return true;
            }

            var gamepad = Gamepad.current;
            if (gamepad != null && gamepad.buttonSouth.wasPressedThisFrame) return true;

            if (IsPointerOverUi()) return false;

            var mouse = Mouse.current;
            if (mouse != null && mouse.leftButton.wasPressedThisFrame) return true;

            var touch = Touchscreen.current;
            if (touch != null && touch.primaryTouch.press.wasPressedThisFrame) return true;
#elif ENABLE_LEGACY_INPUT_MANAGER
            if (Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
            {
                return true;
            }
            if (IsPointerOverUi()) return false;
            if (Input.GetMouseButtonDown(0)) return true;
            if (Input.touchCount > 0 && Input.GetTouch(0).phase == TouchPhase.Began) return true;
#endif
            return false;
        }

        public static bool ConfirmDown()
        {
#if ENABLE_INPUT_SYSTEM
            var keyboard = Keyboard.current;
            if (keyboard != null && (keyboard.enterKey.wasPressedThisFrame || keyboard.numpadEnterKey.wasPressedThisFrame)) return true;
            var gamepad = Gamepad.current;
            if (gamepad != null && gamepad.buttonSouth.wasPressedThisFrame) return true;
#elif ENABLE_LEGACY_INPUT_MANAGER
            if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter)) return true;
#endif
            return false;
        }

        public static bool PauseDown()
        {
#if ENABLE_INPUT_SYSTEM
            var keyboard = Keyboard.current;
            if (keyboard != null && keyboard.pKey.wasPressedThisFrame) return true;
            var gamepad = Gamepad.current;
            if (gamepad != null && gamepad.startButton.wasPressedThisFrame) return true;
#elif ENABLE_LEGACY_INPUT_MANAGER
            if (Input.GetKeyDown(KeyCode.P)) return true;
#endif
            return false;
        }

        public static bool BackDown()
        {
#if ENABLE_INPUT_SYSTEM
            var keyboard = Keyboard.current;
            if (keyboard != null && (keyboard.escapeKey.wasPressedThisFrame || keyboard.backspaceKey.wasPressedThisFrame)) return true;
            var gamepad = Gamepad.current;
            if (gamepad != null && gamepad.buttonEast.wasPressedThisFrame) return true;
#elif ENABLE_LEGACY_INPUT_MANAGER
            if (Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.Backspace)) return true;
#endif
            return false;
        }

        public static bool IsPointerOverUi()
        {
            var events = EventSystem.current;
            if (events == null) return false;

#if ENABLE_INPUT_SYSTEM
            var touch = Touchscreen.current;
            if (touch != null && touch.primaryTouch.press.isPressed)
            {
                return events.IsPointerOverGameObject(touch.primaryTouch.touchId.ReadValue());
            }
#endif
            return events.IsPointerOverGameObject();
        }
    }
}
