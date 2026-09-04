using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

[System.Serializable]
public class FloatingJoystick : Joystick
{
    [Header("Floating Joystick Settings")]
    [SerializeField] float pointerDownThreshold = 0.05f;
    [SerializeField] float pointerDownTimer = 0;

    bool pressedThisFrame;
    Coroutine pointerDownCoroutine;

    protected override void Start()
    {
        base.Start();
        background.gameObject.SetActive(false);
    }

    public override void OnPointerDown(PointerEventData eventData)
    {
        pointerDownCoroutine = StartCoroutine(PointerDownTimerCoroutine());

        background.anchoredPosition = ScreenPointToAnchoredPosition(eventData.position);
        background.gameObject.SetActive(true);
        base.OnPointerDown(eventData);
    }

    public override void OnPointerUp(PointerEventData eventData)
    {
        if(pointerDownCoroutine != null)
        {
            StopCoroutine(pointerDownCoroutine);
            pointerDownCoroutine = null;
        }

        if(pointerDownTimer < pointerDownThreshold)
        {
            pressedThisFrame = true;
        }

        background.gameObject.SetActive(false);
        base.OnPointerUp(eventData);

        pointerDownTimer = 0;
    }

    IEnumerator PointerDownTimerCoroutine()
    {
        pressedThisFrame = false;
        pointerDownTimer = 0;

        while(pointerDownTimer < pointerDownThreshold)
        {
            pointerDownTimer += Time.deltaTime;
            yield return null;
        }
    }

    public bool ConsumePress()
    {
        bool wasPressed = pressedThisFrame;
        pressedThisFrame = false;
        return wasPressed;
    }
}