namespace FunGuy.Runner
{
    public readonly struct MovementIntent
    {
        public static MovementIntent None => new(0, 0, false);

        public MovementIntent(int laneDelta, int layerDelta, bool forceForward)
        {
            LaneDelta = laneDelta;
            LayerDelta = layerDelta;
            ForceForward = forceForward;
        }

        public int LaneDelta { get; }
        public int LayerDelta { get; }
        public bool ForceForward { get; }
        public bool HasInput => LaneDelta != 0 || LayerDelta != 0 || ForceForward;
    }
}
