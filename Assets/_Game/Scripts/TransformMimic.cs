using UnityEngine;

public class TransformMimic : MonoBehaviour
{
    [Header("References")]
    [SerializeField] Transform target;

    [Header("Position")]
    [SerializeField] bool followPosition = true;
    [SerializeField] bool followXPosition = true;
    [SerializeField] bool followYPosition = true;
    [SerializeField] bool followZPosition = true;
    [SerializeField] Vector3 positionOffset = Vector3.zero;

    [Header("Rotation")]
    [SerializeField] bool followRotation = true;
    [SerializeField] bool followXRotation = true;
    [SerializeField] bool followYRotation = true;
    [SerializeField] bool followZRotation = true;

    void LateUpdate()
    {
        if (target == null)
        {
            return;
        }

        if (followPosition)
        {
            transform.position = new Vector3(followXPosition ? target.position.x : transform.position.x,
                                           followYPosition ? target.position.y : transform.position.y,
                                           followZPosition ? target.position.z : transform.position.z) + positionOffset;
        }

        if(followRotation)
        {
            transform.rotation = Quaternion.Euler(followXRotation ? target.rotation.eulerAngles.x : 0,
                                                followYRotation ? target.rotation.eulerAngles.y : 0,
                                                followZRotation ? target.rotation.eulerAngles.z : 0);
        }
    }
}