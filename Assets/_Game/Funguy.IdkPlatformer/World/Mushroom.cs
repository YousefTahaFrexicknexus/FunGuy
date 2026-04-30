using UnityEngine;

namespace Funguy.IdkPlatformer
{
    [DisallowMultipleComponent]
    public sealed class Mushroom : MonoBehaviour, IBounceSurface
    {
        [SerializeField] private MushroomBounceProfile bounceProfile;

        public MushroomBounceProfile BounceProfile => bounceProfile;

        public void SetBounceProfile(MushroomBounceProfile profile)
        {
            bounceProfile = profile;
        }

        public BounceSurfaceResponse GetBounceResponse(in BounceContext context)
        {
            if (bounceProfile != null)
            {
                return bounceProfile.CreateResponse(transform, context);
            }

            return new BounceSurfaceResponse(
                1f,
                0.4f,
                0f,
                context.BaseJumpForce,
                0.25f,
                transform.up,
                1f);
        }
    }
}
