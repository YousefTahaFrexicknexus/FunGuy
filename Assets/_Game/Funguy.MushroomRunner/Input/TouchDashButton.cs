using UnityEngine;
using UnityEngine.EventSystems;

public class TouchDashButton : MonoBehaviour, IPointerDownHandler
{
    bool pressedThisFrame;

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

    void OnDisable()
    {
        pressedThisFrame = false;
    }
}