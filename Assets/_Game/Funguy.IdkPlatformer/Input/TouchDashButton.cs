using UnityEngine;
using UnityEngine.EventSystems;

namespace Funguy.IdkPlatformer
{
    public sealed class TouchDashButton : MonoBehaviour, IPointerDownHandler
    {
        private bool pressedThisFrame;

        public void OnPointerDown(PointerEventData eventData)
        {
            pressedThisFrame = true;
        }

        public bool ConsumePress()
        {
            bool wasPressed = pressedThisFrame;
            pressedThisFrame = false;
            return wasPressed;
        }

        private void OnDisable()
        {
            pressedThisFrame = false;
        }
    }
}
