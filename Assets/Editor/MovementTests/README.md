# Movement baseline

Captured in Unity 6000.3.9f1 from the pre-refactor sources and BalancedMomentum asset saved before implementation. The original formulas run at a 48 soft ceiling with a 0.02-second step. Runtime code is checked against these samples with small floating-point tolerances.

Source SHA-256 hashes:

- `MovementTuningProfile.cs`: `16ec2f0018ddaf2b440008df11191d22639a03e72a25f6edbead2411f7243c6d`

- `BounceMovementMath.cs`: `2cf6747878fa07677ebe7ad562c43ff3a0fbf9acd5a663eb31eb095b93888290`

- `RunnerMovementMotor.cs`: `e8300f681edfa7f178b10d7cb3b44017e1b5c47e5a84a363402b60a4eb0c21ed`

- `BalancedMomentum.asset`: `27fdff5d830bbb35e38ff41184a14183b630913262f946066c1467dd8788f1b7`


The fixture covers steering and flight math. Motor state, score binding, scoring, and scene wiring are checked separately; this fixture does not claim to capture a complete interactive physics run.

The air-grip redesign keeps this fixture unchanged. Forward, coast, and twelve no-input flight references run with the original 0.18 drag to isolate launch/gravity regressions. Actual BalancedMomentum now uses 0.8 drag; motor checks require this resistance to remain effective after a bounce. The old diagonal/reversal/brake traces are historical only. New checks cover bounded sideways speed, release stopping distance, countersteering, analog input, dive recovery, and finite lateral reach.

Playtest the same mushroom sequence with keyboard and touch: tap sideways, release over a target, reverse a late correction, pull back to land, and recover using an air jump. Automated physics checks include released-input drift at 40/60, but cannot establish subjective handling acceptance.
