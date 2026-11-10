using UnityEngine;
using DG.Tweening;

using Sirenix.OdinInspector;
using System.Collections;

public class LilypadMotion : MonoBehaviour
{
    [Header("Movement")]
    [SerializeField] bool shouldMove = true;
    [SerializeField] Transform lilyPadTransform;
    [SerializeField] Transform lilyPadVisual;
    [SerializeField] Transform leftPoint;
    [SerializeField] Transform rightPoint;
    [SerializeField] float movementDuration = 5f;
    [SerializeField] Ease movementEase = Ease.InOutSine;

    [Header("Rotation")]
    [SerializeField] bool shouldRotate = true;
    [SerializeField] float rotationValue = 180;
    [SerializeField] float rotationDuration = 10f;
    [SerializeField] Ease rotationEase = Ease.InOutSine;

    [Header("Jump Reaction")]
    [SerializeField] float jumpDownDistance = 0.15f;
    [SerializeField] float jumpDownDuration = 0.15f;
    [SerializeField] float jumpHoldDuration = 0.1f;
    [SerializeField] float jumpUpDuration = 0.35f;
    [SerializeField] Ease jumpDownEase = Ease.OutQuad;
    [SerializeField] Ease jumpUpEase = Ease.OutBack;

    Vector3 originalPosition;
    Vector3 nextPosition;

    Sequence movementSequence;
    Tween rotationTween;
    Tween jumpTween;

    void CacheEndpoints()
    {
        // Pool reuse moves the entire block: recalculate world-space endpoints on every enable.
        if (!shouldMove)
        {
            originalPosition = lilyPadTransform.position;
            nextPosition = originalPosition;
            return;
        }

        if(Random.Range(0, 2) == 0)
        {
            originalPosition = rightPoint.position;
            nextPosition = leftPoint.position;
        }
        else
        {
            originalPosition = leftPoint.position;
            nextPosition = rightPoint.position;
        }
    }

    void OnEnable()
    {
        CacheEndpoints();
        Init();
        Reset();
        StartMovement();
        StartRotating();
    }

    void Init()
    {
        movementDuration = Random.Range(10, 15);
        rotationDuration = Random.Range(10, 15);
    }

    void Reset()
    {
        lilyPadTransform.position = originalPosition;
        lilyPadTransform.rotation = Quaternion.Euler(0, Random.Range(0, 360), 0);
        
        lilyPadVisual.localPosition = Vector3.zero;
    }

    void StartMovement()
    {
        if(shouldMove == false)
        {
            return;
        }

        movementSequence = DOTween.Sequence();
        movementSequence.Append(lilyPadTransform.DOMove(nextPosition, movementDuration).SetEase(movementEase))
            .Append(lilyPadTransform.DOMove(originalPosition, movementDuration).SetEase(movementEase))
            .SetLoops(-1, LoopType.Restart);
    }

    void StartRotating()
    {
        if(shouldRotate == false)
        {
            return;
        }

        // Infinite loops belong on the tween itself, not inside a Sequence.
        rotationTween = lilyPadTransform.DORotate(new Vector3(0f, rotationValue, 0), rotationDuration)
            .SetEase(rotationEase).SetLoops(-1, LoopType.Incremental);
    }

    [Button("Play launchpad landing")]
    public void DipLaunchPad()
    {
        Vector3 downPosition = lilyPadVisual.localPosition + Vector3.down * jumpDownDistance;

        jumpTween?.Kill();
        jumpTween = lilyPadVisual.DOLocalMove(downPosition, jumpDownDuration).SetEase(jumpDownEase);

        CancelInvoke(nameof(ResurfaceLaunchPad));
        Invoke(nameof(ResurfaceLaunchPad), jumpHoldDuration);
    }

    public void ResurfaceLaunchPad()
    {
        jumpTween?.Kill();
        jumpTween = lilyPadVisual.DOLocalMove(Vector3.zero, jumpUpDuration).SetEase(jumpUpEase);
    }

    void OnDisable()
    {
        CancelInvoke(nameof(ResurfaceLaunchPad));
        movementSequence?.Kill();
        rotationTween?.Kill();
        jumpTween?.Kill();
    }

    void OnDestroy()
    {
        CancelInvoke(nameof(ResurfaceLaunchPad));
        movementSequence?.Kill();
        rotationTween?.Kill();
        jumpTween?.Kill();
    }
}