using UnityEngine;

namespace FunGuy.Runner
{
    public sealed class RunnerCameraFollow : MonoBehaviour
    {
        [SerializeField] private Transform target;
        [SerializeField] private Vector3 offset = new(0f, 4.2f, -10.8f);
        [SerializeField] private Vector3 lookAhead = new(0f, -0.1f, 10.5f);
        [SerializeField] private float smoothTime = 0.28f;
        [SerializeField] private float lookSpeed = 5f;

        private Vector3 velocity;

        public void SetTarget(Transform newTarget)
        {
            target = newTarget;

            if (target == null)
            {
                return;
            }

            transform.position = target.position + offset;
            transform.rotation = Quaternion.LookRotation((target.position + lookAhead) - transform.position, Vector3.up);
        }

        private void LateUpdate()
        {
            if (target == null)
            {
                return;
            }

            Vector3 desiredPosition = target.position + offset;
            transform.position = Vector3.SmoothDamp(transform.position, desiredPosition, ref velocity, smoothTime);

            Vector3 focusPoint = target.position + lookAhead;
            Quaternion targetRotation = Quaternion.LookRotation(focusPoint - transform.position, Vector3.up);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, Time.deltaTime * lookSpeed);
        }
    }
}
