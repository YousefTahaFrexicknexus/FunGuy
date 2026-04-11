using UnityEngine;
using UnityEngine.EventSystems;

namespace Funguy.IdkPlatformer
{
    public sealed class FloatingJoystick : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
    {
        [SerializeField] private RectTransform joystickRoot;
        [SerializeField] private RectTransform handle;
        [SerializeField] private RectTransform movementArea;
        [SerializeField] private CanvasGroup visuals;
        [SerializeField] private float movementRadius = 110f;
        [SerializeField] private bool repositionToPointer = true;
        [SerializeField] private bool hideWhenInactive = true;

        private int activePointerId = -1;

        public Vector2 Value { get; private set; }

        public bool IsHeld => activePointerId >= 0;

        private void Reset()
        {
            joystickRoot = transform as RectTransform;

            if (joystickRoot != null && joystickRoot.childCount > 0)
            {
                handle = joystickRoot.GetChild(0) as RectTransform;
            }

            movementArea = joystickRoot != null ? joystickRoot.parent as RectTransform : null;
            visuals = GetComponent<CanvasGroup>();
        }

        private void Awake()
        {
            if (joystickRoot == null)
            {
                joystickRoot = transform as RectTransform;
            }

            if (movementArea == null && joystickRoot != null)
            {
                movementArea = joystickRoot.parent as RectTransform;
            }

            SetVisualState(!hideWhenInactive);
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            // Let the latest touch take control so players can replant their thumb without waiting for the old touch to lift.
            activePointerId = eventData.pointerId;
            SetVisualState(true);

            if (repositionToPointer && joystickRoot != null)
            {
                RectTransform targetRect = movementArea != null ? movementArea : joystickRoot;
                if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
                        targetRect,
                        eventData.position,
                        eventData.pressEventCamera,
                        out Vector2 localPoint))
                {
                    joystickRoot.anchoredPosition = localPoint;
                }
            }

            UpdateJoystick(eventData);
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (eventData.pointerId != activePointerId)
            {
                return;
            }

            UpdateJoystick(eventData);
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            if (eventData.pointerId != activePointerId)
            {
                return;
            }

            activePointerId = -1;
            Value = Vector2.zero;

            if (handle != null)
            {
                handle.anchoredPosition = Vector2.zero;
            }

            if (hideWhenInactive)
            {
                SetVisualState(false);
            }
        }

        private void OnDisable()
        {
            activePointerId = -1;
            Value = Vector2.zero;

            if (handle != null)
            {
                handle.anchoredPosition = Vector2.zero;
            }

            SetVisualState(!hideWhenInactive);
        }

        private void UpdateJoystick(PointerEventData eventData)
        {
            if (joystickRoot == null)
            {
                Value = Vector2.zero;
                return;
            }

            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    joystickRoot,
                    eventData.position,
                    eventData.pressEventCamera,
                    out Vector2 localPoint))
            {
                Value = Vector2.zero;
                return;
            }

            Vector2 clampedDelta = Vector2.ClampMagnitude(localPoint, movementRadius);
            Value = clampedDelta / Mathf.Max(0.0001f, movementRadius);

            if (handle != null)
            {
                handle.anchoredPosition = clampedDelta;
            }
        }

        private void SetVisualState(bool isVisible)
        {
            if (visuals == null)
            {
                return;
            }

            visuals.alpha = isVisible ? 1f : 0f;
            visuals.blocksRaycasts = true;
            visuals.interactable = true;
        }
    }
}
