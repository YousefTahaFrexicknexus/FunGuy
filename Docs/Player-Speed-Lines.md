# Player speed air lines

The MushroomRunnerPlayer prefab includes **Speed Air Lines**, a tuned instance of Vefects' `VFX_Wind_Flows_Trails_02_Fast` prefab. `RunnerSpeedLines` on the player controls particle emission, travel speed, and width using forward world-Z velocity. Vertical bounce and falling speed do not trigger it.

Defaults: fade in above 12 world units/second, full intensity at 35, density 1.1, fade response 9. Response Exponent 0.65 brings the wind in visibly at intermediate speeds; deceleration fades at half the acceleration response. Maximum Particle Speed Multiplier (1.6) and Maximum Line Width Multiplier (1.25) strengthen the streaks as speed rises. Change these on the player's Runner Speed Lines component.

The emitter surrounds the player in a 9 x 5 x 8 box, travels backward, and uses pale blue-white trails with 0.9 starting alpha. The source prefab's 10-unit upward shape offset is overridden to zero so emission actually surrounds the player. Its base emission is 95 particles/second, capped at 100 live particles, with 0.24–0.45 second lifetimes. Reduced noise keeps the wind straighter. Local simulation keeps particles around the player even at very high world speeds. Travel speed changes independently of simulation speed so acceleration does not shorten visible lifetimes. The effect is under the physics root rather than the animated/squashed visual hierarchy.

Pause freezes its scaled-time fade and particles. Gameplay reset, player disable/death, and component disable clear the particles immediately. It does not modify player movement, momentum, or camera behavior.

Source: https://vefects.itch.io/wind-vfx-unity-urp

Downloaded free from the creator on 2026-10-31, package `Vefects_Wind_VFX_6000_URP.unitypackage`. Only the two fast-wind prefabs and their dependencies were imported under `Assets/Vefects/Wind VFX URP`; the demo and project settings were excluded. Original assets are preserved; player-specific tuning is stored as nested prefab overrides on MushroomRunnerPlayer.

The creator's published Free Asset License is preserved beside the assets, with its original scope and terms: https://github.com/vefects/Free-Unity-VFX/blob/main/LICENSE.txt
