using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.EnhancedTouch;
using InputTouchPhase = UnityEngine.InputSystem.TouchPhase;
using Touch = UnityEngine.InputSystem.EnhancedTouch.Touch;

namespace FunGuy.Runner
{
    public sealed class RunnerInputHandler : MonoBehaviour
    {
        [Header("Gesture Thresholds")]
        [SerializeField] private float swipeMinPixels = 80f;
        [SerializeField] private float maxTapDuration = 0.25f;

        [Header("Fallback Controls")]
        [SerializeField] private bool enableKeyboardFallback = true;

        private MovementIntent bufferedIntent;
        private bool hasBufferedIntent;

        private Vector2 pointerStartPosition;
        private double pointerStartTime;
        private bool trackingMouse;
        private int trackedTouchId = -1;

        private void OnEnable()
        {
            EnhancedTouchSupport.Enable();
        }

        private void OnDisable()
        {
            if (EnhancedTouchSupport.enabled)
            {
                EnhancedTouchSupport.Disable();
            }

            trackingMouse = false;
            trackedTouchId = -1;
            ClearBufferedIntent();
        }

        private void Update()
        {
            ReadKeyboard();
            ReadMouse();
            ReadTouch();
        }

        public MovementIntent ConsumeBufferedIntent()
        {
            if (!hasBufferedIntent)
            {
                return MovementIntent.None;
            }

            MovementIntent intent = bufferedIntent;
            bufferedIntent = MovementIntent.None;
            hasBufferedIntent = false;
            return intent;
        }

        public void ClearBufferedIntent()
        {
            bufferedIntent = MovementIntent.None;
            hasBufferedIntent = false;
        }

        private void ReadKeyboard()
        {
            if (!enableKeyboardFallback || Keyboard.current == null)
            {
                return;
            }

            if (Keyboard.current.leftArrowKey.wasPressedThisFrame || Keyboard.current.aKey.wasPressedThisFrame)
            {
                BufferIntent(new MovementIntent(-1, 0, false));
                return;
            }

            if (Keyboard.current.rightArrowKey.wasPressedThisFrame || Keyboard.current.dKey.wasPressedThisFrame)
            {
                BufferIntent(new MovementIntent(1, 0, false));
                return;
            }

            if (Keyboard.current.upArrowKey.wasPressedThisFrame || Keyboard.current.wKey.wasPressedThisFrame)
            {
                BufferIntent(new MovementIntent(0, 1, false));
                return;
            }

            if (Keyboard.current.downArrowKey.wasPressedThisFrame || Keyboard.current.sKey.wasPressedThisFrame)
            {
                BufferIntent(new MovementIntent(0, -1, false));
                return;
            }

            if (Keyboard.current.spaceKey.wasPressedThisFrame || Keyboard.current.enterKey.wasPressedThisFrame)
            {
                BufferIntent(new MovementIntent(0, 0, true));
                return;
            }
        }

        private void ReadMouse()
        {
            if (Mouse.current == null)
            {
                return;
            }

            if (Mouse.current.leftButton.wasPressedThisFrame)
            {
                trackingMouse = true;
                pointerStartPosition = Mouse.current.position.ReadValue();
                pointerStartTime = Time.unscaledTimeAsDouble;
            }

            if (trackingMouse && Mouse.current.leftButton.wasReleasedThisFrame)
            {
                Vector2 endPosition = Mouse.current.position.ReadValue();
                EvaluateGesture(pointerStartPosition, endPosition, Time.unscaledTimeAsDouble - pointerStartTime);
                trackingMouse = false;
            }
        }

        private void ReadTouch()
        {
            var touches = Touch.activeTouches;

            if (trackedTouchId < 0)
            {
                for (int i = 0; i < touches.Count; i++)
                {
                    if (touches[i].phase != InputTouchPhase.Began)
                    {
                        continue;
                    }

                    trackedTouchId = touches[i].touchId;
                    pointerStartPosition = touches[i].screenPosition;
                    pointerStartTime = touches[i].time;
                    break;
                }

                return;
            }

            for (int i = 0; i < touches.Count; i++)
            {
                if (touches[i].touchId != trackedTouchId)
                {
                    continue;
                }

                if (touches[i].phase == InputTouchPhase.Ended || touches[i].phase == InputTouchPhase.Canceled)
                {
                    EvaluateGesture(pointerStartPosition, touches[i].screenPosition, touches[i].time - pointerStartTime);
                    trackedTouchId = -1;
                }

                return;
            }
        }

        private void EvaluateGesture(Vector2 start, Vector2 end, double duration)
        {
            Vector2 delta = end - start;
            float minDistanceSquared = swipeMinPixels * swipeMinPixels;

            if (duration <= maxTapDuration && delta.sqrMagnitude < minDistanceSquared)
            {
                BufferIntent(new MovementIntent(0, 0, true));
                return;
            }

            if (Mathf.Abs(delta.x) > Mathf.Abs(delta.y))
            {
                BufferIntent(new MovementIntent(delta.x > 0f ? 1 : -1, 0, false));
                return;
            }

            BufferIntent(new MovementIntent(0, delta.y > 0f ? 1 : -1, false));
        }

        private void BufferIntent(MovementIntent intent)
        {
            bufferedIntent = intent;
            hasBufferedIntent = intent.HasInput;
        }
    }
}
