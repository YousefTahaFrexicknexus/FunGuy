using DG.Tweening;
using UnityEngine;

// Place on the mesh child, leaving the root free for attraction and physics.
[DisallowMultipleComponent]
public sealed class CoinIdleAnimation : MonoBehaviour
{
    [SerializeField, Min(0.1f)] float rotationDuration = 6f;
    [SerializeField, Min(0f)] float hoverHeight = 0.2f;
    [SerializeField, Min(0.1f)] float hoverHalfDuration = 1.2f;

    Vector3 basePosition;
    Quaternion baseRotation;
    Tween rotationTween;
    Tween hoverTween;
    bool attractionPaused;

    void Awake()
    {
        basePosition = transform.localPosition;
        baseRotation = transform.localRotation;
    }

    void OnEnable()
    {
        attractionPaused = false;
        // Two reusable tweens; no per-frame Update or tween creation.
        if (rotationTween == null)
        {
            rotationTween = transform.DOLocalRotate(new Vector3(0f, 360f, 0f),
                    rotationDuration, RotateMode.WorldAxisAdd)
                .SetEase(Ease.Linear).SetLoops(-1).SetAutoKill(false).Pause();
            hoverTween = transform.DOLocalMoveY(basePosition.y + hoverHeight, hoverHalfDuration)
                .SetEase(Ease.InOutSine).SetLoops(-1, LoopType.Yoyo).SetAutoKill(false).Pause();
        }
        rotationTween.Restart();
        hoverTween.Restart();
    }

    public void StopForAttraction()
    {
        // Freeze the current pose instead of snapping the mesh back mid-flight.
        attractionPaused = true;
        rotationTween?.Pause();
        hoverTween?.Pause();
    }

    void OnDisable()
    {
        rotationTween?.Pause();
        hoverTween?.Pause();
        transform.localPosition = basePosition;
        transform.localRotation = baseRotation;
    }

    void OnBecameInvisible()
    {
        rotationTween?.Pause();
        hoverTween?.Pause();
    }

    void OnBecameVisible()
    {
        if (!isActiveAndEnabled || attractionPaused) return;
        rotationTween?.Play();
        hoverTween?.Play();
    }

    void OnDestroy()
    {
        rotationTween?.Kill();
        hoverTween?.Kill();
    }
}
