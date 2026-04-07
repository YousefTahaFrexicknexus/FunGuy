namespace Funguy.IdkPlatformer
{
    public interface IBounceSurface
    {
        BounceSurfaceResponse GetBounceResponse(in BounceContext context);
    }
}
