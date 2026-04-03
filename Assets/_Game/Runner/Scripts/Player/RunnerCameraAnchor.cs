using UnityEngine;

namespace FunGuy.Runner
{
    [DisallowMultipleComponent]
    public sealed class RunnerCameraAnchor : MonoBehaviour
    {
        [SerializeField] private Vector3 localOffset = new(0f, 1.45f, 0.55f);

        public Transform AnchorTransform => transform;

        private void Reset()
        {
            ApplyAnchorOffset();
        }

        private void OnValidate()
        {
            ApplyAnchorOffset();
        }

        public void ApplyAnchorOffset()
        {
            if (transform.parent != null)
            {
                transform.localPosition = localOffset;
                transform.localRotation = Quaternion.identity;
            }
        }
    }
}
