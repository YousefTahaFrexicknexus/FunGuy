// using System.Collections;
// using System.Collections.Generic;
// using UnityEngine;
// using UnityEngine.EventSystems;

// [System.Serializable]
// public class FloatingJoystick : Joystick
// {
//     [Header("Floating Joystick Settings")]
//     [SerializeField] float pointerDownThreshold = 0.05f;
//     [SerializeField] float pointerDownTimer = 0;

//     bool pressedThisFrame;
//     Coroutine pointerDownCoroutine;

//     protected override void Start()
//     {
//         base.Start();
//         background.gameObject.SetActive(false);
//     }

//     public override void OnPointerDown(PointerEventData eventData)
//     {
//         pointerDownCoroutine = StartCoroutine(PointerDownTimerCoroutine());

//         background.anchoredPosition = ScreenPointToAnchoredPosition(eventData.position);
//         background.gameObject.SetActive(true);
//         base.OnPointerDown(eventData);
//     }

//     public override void OnPointerUp(PointerEventData eventData)
//     {
//         if(pointerDownCoroutine != null)
//         {
//             StopCoroutine(pointerDownCoroutine);
//             pointerDownCoroutine = null;
//         }

//         if(pointerDownTimer < pointerDownThreshold)
//         {
//             pressedThisFrame = true;
//         }

//         background.gameObject.SetActive(false);
//         base.OnPointerUp(eventData);

//         pointerDownTimer = 0;
//     }

//     IEnumerator PointerDownTimerCoroutine()
//     {
//         pressedThisFrame = false;
//         pointerDownTimer = 0;

//         while(pointerDownTimer < pointerDownThreshold)
//         {
//             pointerDownTimer += Time.deltaTime;
//             yield return null;
//         }
//     }

//     public bool ConsumePress()
//     {
//         bool wasPressed = pressedThisFrame;
//         pressedThisFrame = false;
//         return wasPressed;
//     }
// }

using UnityEngine;
using UnityEngine.EventSystems;

public class FloatingJoystick : Joystick
{
    [Header("Floating Joystick Settings")]
    [SerializeField, Tooltip("Max time (seconds) between touch down and up to count as a tap.")]
    float tapMaxDuration = 0.25f;

    [SerializeField, Tooltip("Max finger travel (screen pixels) for the touch to still count as a tap.")]
    float tapMaxDistance = 30f;

    bool tapQueued;
    float downTime;
    Vector2 downPosition;

    protected override void Start()
    {
        base.Start();
        background.gameObject.SetActive(false);
    }

    public override void OnPointerDown(PointerEventData eventData)
    {
        downTime = Time.unscaledTime;
        downPosition = eventData.position;

        background.anchoredPosition = ScreenPointToAnchoredPosition(eventData.position);
        background.gameObject.SetActive(true);
        base.OnPointerDown(eventData);
    }

    public override void OnPointerUp(PointerEventData eventData)
    {
        bool quick = Time.unscaledTime - downTime <= tapMaxDuration;
        bool still = (eventData.position - downPosition).sqrMagnitude <= tapMaxDistance * tapMaxDistance;

        if (quick && still)
        {
            tapQueued = true;
        }

        background.gameObject.SetActive(false);
        base.OnPointerUp(eventData);
    }

    /// <summary>True once per tap.</summary>
    public bool ConsumeTap()
    {
        bool wasTapped = tapQueued;
        tapQueued = false;
        return wasTapped;
    }
}