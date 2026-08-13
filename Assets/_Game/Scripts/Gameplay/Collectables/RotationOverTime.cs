using UnityEngine;

public class RotationOverTime : MonoBehaviour
{
    [Header("Rotation")]
    [SerializeField] Vector3 rotationAxis = Vector3.up;
    [SerializeField] float rotationSpeed = 180f;

    [Header("Runtime")]
    [SerializeField] bool randomizeDirection = false;

    Vector3 finalAxis;

    void OnEnable()
    {
        finalAxis = rotationAxis.normalized;

        if(randomizeDirection)
        {
            finalAxis *= Random.value > 0.5f ? 1 : -1;
        }
    }


    void Update()
    {
        transform.Rotate(finalAxis, rotationSpeed * Time.deltaTime, Space.Self);
    }
}