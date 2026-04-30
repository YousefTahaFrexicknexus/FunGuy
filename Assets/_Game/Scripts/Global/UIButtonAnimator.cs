using System.Collections;
using UnityEngine;
using DG.Tweening;
using UnityEngine.Events;
using UnityEngine.UI; // Make sure to include this namespace
using Sirenix.OdinInspector; // Include Odin Inspector namespace

[RequireComponent(typeof(Button))]
public class UIButtonAnimator : MonoBehaviour
{
    [TabGroup("Animation Parameters")]
    [Header("Animation params"), Space]
    [SerializeField] private bool isAnimating = false;
    [TabGroup("Animation Parameters"), LabelText("Ease Type")] public Ease ease = Ease.InOutBack;
    [TabGroup("Animation Parameters"), Range(0.1f, 3f)] public float duration = 0.2f;

    [TabGroup("Scale Parameters")]
    [Header("Scale parameters"), Space]
    [SerializeField, LabelText("Start Value")] public Vector3 startVal = Vector3.one * 0.9f;
    [TabGroup("Scale Parameters"), SerializeField, LabelText("Final Value")] public Vector3 finalVal = Vector3.one;

    [TabGroup("SFX Parameters")]
    [Header("SFX"), Space]
    [SerializeField] private ButtonSFXType buttonSFXType;

    [TabGroup("Event Callback")]
    [Header("Event callback"), Space]
    public UnityEvent callBackFn;

    private Button button; // Reference to the Button component

    private void OnEnable()
    {
        isAnimating = false;
        this.transform.localScale = finalVal;

        button = GetComponent<Button>();
        if (button != null)
        {
            // Remove any existing listeners to avoid duplication
            button.onClick.RemoveListener(OnClick);
            // Add the OnClick method to the Button's OnClick event
            button.onClick.AddListener(OnClick);
        }
    }

    public virtual void OnClick()
    {
        if (isAnimating)
            return;

        isAnimating = true;

        PlayOnClickSFX();

        this.transform.DOScale(startVal, 0.1f).SetEase(Ease.InBack).OnComplete(() =>
        {
            this.transform.DOScale(finalVal, duration).SetEase(ease);
        });

        StartCoroutine(OnClickProcess(duration));
    }

    public virtual IEnumerator OnClickProcess(float timer)
    {
        yield return new WaitForSeconds(timer);
        callBackFn?.Invoke();
        isAnimating = false;
    }

    public void PlayOnClickSFX()
    {
        if (AudioManager.Instance && buttonSFXType != ButtonSFXType.none)
            AudioManager.Instance.PlaySFX(buttonSFXType.ToString());
    }

    private void OnDisable()
    {
        // Remove the OnClick listener when the object is disabled to avoid potential issues
        if (button != null)
        {
            button.onClick.RemoveListener(OnClick);
        }
    }

    public enum ButtonSFXType
    {
        none,
        click1,
        click2,
        UI_Select,
        UI_Confirm,
        UI_Popup_Close,
        UI_Advance,
        UI_Back
    }
}