using UnityEngine;

public readonly struct MushroomBounceModifierContext
{
    public MushroomBounceModifierContext(
        SimpleBounceMushroom mushroom,
        RunnerMovementMotor movementMotor,
        Collider triggerCollider,
        Collider playerCollider)
    {
        Mushroom = mushroom;
        MovementMotor = movementMotor;
        TriggerCollider = triggerCollider;
        PlayerCollider = playerCollider;
    }

    public SimpleBounceMushroom Mushroom { get; }

    public RunnerMovementMotor MovementMotor { get; }

    public Collider TriggerCollider { get; }

    public Collider PlayerCollider { get; }
}

public interface IMushroomBounceModifier
{
    void OnMushroomBounce(in MushroomBounceModifierContext context);
}