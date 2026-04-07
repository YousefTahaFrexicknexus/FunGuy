using UnityEngine;

namespace Funguy.IdkPlatformer
{
    [DisallowMultipleComponent]
    public sealed class SimpleCameraFollow : MonoBehaviour
    {
        [SerializeField] private Transform target;
        [SerializeField] private Vector3 followOffset = new(0f, 4.5f, -9.5f);
        [SerializeField] private Vector3 lookOffset = new(0f, 1f, 0f);
        [SerializeField] private float followSharpness = 8f;
        [SerializeField] private bool lookAtTarget = true;

        public void SetTarget(Transform followTarget)
        {
            target = followTarget;
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
    }
}
