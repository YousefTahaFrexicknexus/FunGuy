using UnityEngine;

public class TransformMimic : MonoBehaviour
{
    [Header("References")]
    [SerializeField] Transform target;

    [Header("Settings")]
    [SerializeField] bool isUpdate = true;
    [SerializeField] bool followPosition = true;
    [SerializeField] bool followRotation = true;
    [SerializeField] bool followScale = true;

    [Header("Position")]
    [SerializeField] bool followXPosition = true;
    [SerializeField] bool followYPosition = true;
    [SerializeField] bool followZPosition = true;
    [SerializeField] Vector3 positionOffset = Vector3.zero;

    [Header("Rotation")]
    [SerializeField] bool followXRotation = true;
    [SerializeField] bool followYRotation = true;
    [SerializeField] bool followZRotation = true;

    [Header("Scale")]
    [SerializeField] bool MimicScale = true;

    void OnEnable()
    {
        UpdateTransform();
    }

    void LateUpdate()
    {
        if(isUpdate == false)
        {
            return;
        }

        UpdateTransform();
    }

    void UpdateTransform()
    {
        if (followPosition)
        {
            UpdatePosition();
        }

        if(followRotation)
        {
            UpdateRotation();
        }

        if(followScale)
        {
            UpdateScale();
        }
    }

    void UpdatePosition()
    {
        transform.position = new Vector3(followXPosition ? target.position.x : transform.position.x,
                                       followYPosition ? target.position.y : transform.position.y,
                                       followZPosition ? target.position.z : transform.position.z) + positionOffset;
    }

    void UpdateRotation()
    {
        transform.rotation = Quaternion.Euler(followXRotation ? target.rotation.eulerAngles.x : 0,
                                            followYRotation ? target.rotation.eulerAngles.y : 0,
                                            followZRotation ? target.rotation.eulerAngles.z : 0);
    }

    void UpdateScale()
    {
        this.transform.localScale = target.transform.localScale;
    }
}