using UnityEngine;

namespace Funguy.IdkPlatformer
{
    [DisallowMultipleComponent]
    public sealed class SimpleCameraFollow : MonoBehaviour
    {
        [SerializeField] private Transform target;
        [SerializeField] private Vector3 followOffset = new(0f, 6f, -10.5f);
        [SerializeField] private Vector3 lookOffset = new(0f, 0.35f, 0f);
        [SerializeField] private float followSharpness = 8f;
        [SerializeField] private bool lookAtTarget = true;
        [SerializeField] private float baseFieldOfView = 50f;
        [SerializeField] private float maxFieldOfView = 66f;
        [SerializeField] private float speedForMaxFieldOfView = 20f;
        [SerializeField] private float fieldOfViewSharpness = 6f;

        private Camera attachedCamera;
        private Rigidbody cachedTargetBody;

        public void SetTarget(Transform followTarget)
        {
            target = followTarget;
            cachedTargetBody = null;
        }

        private void Awake()
        {
            attachedCamera = GetComponent<Camera>();
        }

        private void LateUpdate()
        {
            if (target == null)
            {
                return;
            }

            Vector3 desiredPosition = target.position + followOffset;
            float interpolation = 1f - Mathf.Exp(-Mathf.Max(0f, followSharpness) * Time.deltaTime);
            transform.position = Vector3.Lerp(transform.position, desiredPosition, interpolation);
            UpdateFieldOfView();

            if (!lookAtTarget)
            {
                return;
            }

            Vector3 lookPoint = target.position + lookOffset;
            Vector3 lookDirection = lookPoint - transform.position;
            if (lookDirection.sqrMagnitude <= 0.0001f)
            {
                return;
            }

            transform.rotation = Quaternion.LookRotation(lookDirection.normalized, Vector3.up);
        }

        private void UpdateFieldOfView()
        {
            if (attachedCamera == null)
            {
                return;
            }

            Rigidbody targetBody = ResolveTargetBody();
            float speed = 0f;

            if (targetBody != null)
            {
                Vector3 planarVelocity = Vector3.ProjectOnPlane(targetBody.linearVelocity, Vector3.up);
                speed = planarVelocity.magnitude;
            }

            float normalizedSpeed = Mathf.Clamp01(speed / Mathf.Max(0.001f, speedForMaxFieldOfView));
            float targetFieldOfView = Mathf.Lerp(baseFieldOfView, maxFieldOfView, normalizedSpeed);
            float interpolation = 1f - Mathf.Exp(-Mathf.Max(0f, fieldOfViewSharpness) * Time.deltaTime);
            attachedCamera.fieldOfView = Mathf.Lerp(attachedCamera.fieldOfView, targetFieldOfView, interpolation);
        }

        private Rigidbody ResolveTargetBody()
        {
            if (cachedTargetBody != null)
            {
                return cachedTargetBody;
            }

            cachedTargetBody = target != null ? target.GetComponentInParent<Rigidbody>() : null;
            return cachedTargetBody;
        }
    }
}
