# Player speed air lines

The MushroomRunnerPlayer prefab includes **Speed Air Lines**, a tuned instance of Vefects' `VFX_Wind_Flows_Trails_02_Fast` prefab. `RunnerSpeedLines` on the player controls particle emission and simulation speed using forward world-Z velocity. Vertical bounce and falling speed do not trigger it.

Defaults: fade in above 15 world units/second, full intensity at 45, density 0.65, fade response 5. Change these on the player's Runner Speed Lines component. The emitter surrounds the player, travels backward, and uses short, pale blue-white trails. The effect is under the physics root rather than the animated/squashed visual hierarchy.

Pause freezes its scaled-time fade and particles. Gameplay reset, player disable/death, and component disable clear the particles immediately. It does not modify player movement, momentum, or camera behavior.

Source: https://vefects.itch.io/wind-vfx-unity-urp

Downloaded free from the creator on 2026-10-31, package `Vefects_Wind_VFX_6000_URP.unitypackage`. Only the two fast-wind prefabs and their dependencies were imported under `Assets/Vefects/Wind VFX URP`; the demo and project settings were excluded. Original assets are preserved; player-specific tuning is stored as nested prefab overrides on MushroomRunnerPlayer.

The creator's published Free Asset License is preserved beside the assets, with its original scope and terms: https://github.com/vefects/Free-Unity-VFX/blob/main/LICENSE.txt
