# FunGuy Simple GDD Template

Use this as a lightweight working doc. This version is partially pre-filled from the current `IdkPlatformer` prototype so you can keep the parts we already know and replace the `TBD` bits later.

## 1. Project Snapshot

**Working Title:**  
FunGuy / IdkFunguy

**One-Sentence Pitch:**  
A momentum-driven endless bounce platformer where the player chains mushroom jumps, steers through the air, follows risky coin paths, and uses skillful control to build score multipliers as the game gets faster.

**Genre:**  
Momentum platformer / endless score attack

**Platform(s):**  
PC prototype with keyboard support, plus mobile-friendly touch HUD support

**Target Audience:**  
Players who enjoy short replayable runs, score chasing, and movement skill expression

**Player Fantasy:**  
Feel like a fast, springy little creature surfing momentum across dangerous mushroom routes while staying in control at high speed.

**Design Goal:**  
Make bounce movement feel expressive, readable, and satisfying, while keeping the player in control even as speed, rewards, and progression systems ramp up.

## 2. Game Pillars

Keep this to 3-4 pillars max. These are the rules that protect the identity of the game.

### Pillar 1
**Name:**  
Player Is Always In Control

**Promise:**  
Even when the game gets faster, the player should feel responsible for success or failure.

**In Gameplay This Means:**  
Air control stays readable, dash remains responsive, routes can be reacted to at speed, and different characters change movement feel without making the game feel automatic or unfair.

**This Also Means We Avoid:**  
Auto-play feeling movement, random-feeling failures, or speed ramps that remove meaningful decision making.

### Pillar 2
**Name:**  
Momentum Feels Good

**Promise:**  
The player should enjoy carrying, redirecting, and recovering speed from bounce to bounce.

**In Gameplay This Means:**  
Bounces preserve forward flow, mushrooms can boost or dampen planar speed, air control matters, and the player can dash to extend a route or recover from a mistake.

**This Also Means We Avoid:**  
Sticky movement, overly harsh braking, or surfaces that kill speed for no interesting reason.

### Pillar 3
**Name:**  
Every Bounce Is A Decision

**Promise:**  
Each landing should set up the next choice, not just act as a passive trampoline.

**In Gameplay This Means:**  
Players read the next mushroom placement, choose safe or risky coin paths, steer in the air, choose when to spend a limited dash, and adapt to different bounce profiles like standard, boost, and slow mushrooms.

**This Also Means We Avoid:**  
Long empty airtime with no choices, routes that only work by holding forward, or collectible placement that does not ask the player to take a real risk.

### Optional Pillar 4
**Name:**  
Skilled Risk Pays More

**Promise:**  
Players who stay fast, take better paths, and keep momentum under control should earn meaningfully better scores and rewards.

**In Gameplay This Means:**  
Riskier coin lines, a momentum bar that increases score multiplier as it fills, faster pacing after score thresholds, and reward structures that favor mastery instead of passive play.

**This Also Means We Avoid:**  
Flat reward curves where safe and skillful play feel the same, or progression systems that overshadow good movement.

## 3. Core Loop

**Moment-to-Moment Loop:**  
Spot the next mushroom, choose whether to follow the main route or a riskier coin path, steer toward it, bounce, preserve or redirect momentum, use dash if needed, and keep the momentum bar filled to boost score multiplier.

**Short-Term Loop:**  
Survive a run, collect coins, maintain flow, react to route variation, push your multiplier, and handle speed increases as the run progresses.

**Long-Term Loop:**  
Improve mechanical mastery, unlock or build characters with different movement stats, collect shards and other rewards, and return through daily systems plus meta progression.

## 4. Core Gameplay

**Player Actions:**  
Select a character, move in the air, bounce off mushrooms, dash with limited charges, follow reward paths, collect coins, maintain momentum, and recover from bad approach angles.

**Primary Challenge:**  
Timing, momentum management, route reading, collectible routing, and staying in control as run speed increases.

**Fail State:**  
Miss the route, fall below the death plane, or lose enough control to drop off the playable path.

**Win State / Success State:**  
The current prototype is endless, so success means surviving longer, traveling farther, and setting a higher score.

**Skill Expression:**  
Efficient line choice, preserving speed after each bounce, timing dash usage, reading special mushrooms, choosing when to chase coin paths, and keeping the momentum bar high for a stronger multiplier.

## 5. Progression

**What Improves Over Time:**  
Player skill, run consistency, route reading, speed control, score distance, and a roster of characters with different movement stats such as air control, speed, and related handling values.

**Reward Structure:**  
Immediate rewards come from clean movement, bigger distance, coin collection, and keeping the momentum bar filled for a higher score multiplier. Meta rewards are planned to include character shards and other progression resources.

**Difficulty Curve:**  
The endless generator ramps challenge over distance with route spacing, layout variation, environmental theme escalation, and planned speed increases after key point or score thresholds. The current prototype already includes intro areas before more demanding patterns.

## 6. Content

**Main Content Types:**  
Endless mushroom routes, standard mushrooms, boost mushrooms, slow mushrooms, coin paths, multiple playable characters, environmental theme blocks, a death/reset floor, and HUD feedback like score, speed, momentum, and multiplier.

**Replayability Hooks:**  
Procedural generation, randomized seeds, score attack structure, route variation, character differences, movement mastery, collectible routing, and meta progression.

**Estimated Session Length:**  
Short arcade-style runs. Exact target session length is still TBD.

## 7. World And Theme

**Setting:**  
A stylized mushroom traversal course built from endless forward chunks in a fantasy forest / gloomy environment space.

**Tone:**  
Playful, slightly weird, energetic, and arcade-focused.

**Visual Direction:**  
Readable 3D shapes, colorful mushroom bounce targets, clear forward camera framing, and theme swaps that help long runs feel like they are progressing through new spaces.

**Audio Direction:**  
TBD. The current game would benefit from punchy bounce sounds, readable dash feedback, and music that supports speed and rhythm.

## 8. UX And Controls

**Control Scheme:**  
Camera-relative movement with touch joystick plus dash button, and keyboard fallback using WASD / arrow keys with Space or Shift for dash.

**Accessibility Needs:**  
Simple input complexity is already a strength. Future needs likely include stronger readability for mushroom types, better feedback for dash availability, clearer momentum/multiplier feedback, and tuning options for touch controls.

**HUD Needs:**  
The player needs clear score feedback, current multiplier, momentum bar fill, speed feedback, coin feedback, and readable movement controls on touch. Dash state/readiness could likely use stronger feedback later.

## 9. Scope

**Must-Have Features:**  
Responsive bounce movement, air steering, dash, endless route generation, speed escalation, multiple mushroom bounce types, coin paths, character stat variety, score tracking, momentum-to-multiplier scoring, fail/reset flow, camera follow, and a readable HUD.

**Nice-to-Have Features:**  
Audio polish, stronger onboarding, more mushroom types, leaderboards, cosmetics, richer character progression, deeper meta systems, and stronger retention features.

**Out of Scope:**  
Combat-heavy systems, inventory-driven progression, large narrative campaign content, or sprawling exploration outside the core bounce-run fantasy.

## 10. Production Notes

**Current Prototype Status:**  
Playable. The project already has bounce-based player movement, procedural endless mushroom generation, multiple bounce profiles, forward-progress score tracking, death/reset flow, touch controls, keyboard fallback, theme tiers, and a speed meter HUD. Planned next layers include characters, coin paths, momentum-based score multipliers, speed escalation, and broader meta progression.

**Biggest Risks:**  
Keeping procedural routes fair at high speed, making special mushrooms and coin paths readable at a glance, balancing different characters without breaking player control, and building long-term motivation beyond pure score chasing.

**Next 3 Priorities:**  
1. Define the momentum bar, score multiplier, and speed escalation rules.
2. Design the first set of characters and how their movement stats differ without breaking control.
3. Define the economy layer for coins, shards, daily energy, and rewarded ads.

## 11. Open Questions

- Is the final product centered on endless score attack, or does it eventually need hand-authored stages or modes?
- Should dash remain one charge per bounce for every character, or vary by character kit?
- How generous should coins, shards, energy, and rewarded ads be without making the game feel overly gated or pay-driven?
- What actions fill the momentum bar fastest, and how quickly should the score multiplier decay?

## 12. Economy And Meta Progression

**Coins:**  
Coins are planned as an on-run collectible, often placed on riskier paths to reward skillful routing.

**Character Shards:**  
Character shards are planned as a long-term unlock or upgrade resource for building out the roster.

**Characters:**  
Different characters are planned to have different movement stats such as air control, speed, and related handling values, while still preserving the core promise that the player stays in control.

**Energy System:**  
Daily energy is planned as a retention layer. Exact limits, refill rates, and player-facing friction are still TBD.

**Ads:**  
Rewarded ads are planned as an optional support system, likely for bonus rewards, energy recovery, or similar utility. Exact implementation is TBD.

**Meta Progression Goal:**  
Give players reasons to return beyond score chasing, without letting monetization or progression systems overpower the core movement skill fantasy.

---

## Fast Version: One-Page Summary

If you want an even shorter version later, fill only these:

- **Pitch:** A high-speed endless bounce platformer about chaining mushroom landings, taking risky coin paths, and building a score multiplier through strong control.
- **Player Fantasy:** Be a fast, springy creature surfing momentum through dangerous routes while staying in control.
- **Top 3 Pillars:** Player Is Always In Control, Momentum Feels Good, Every Bounce Is A Decision
- **Core Loop:** Bounce, steer, collect, dash, maintain momentum, and score farther than your last run.
- **Main Challenge:** Managing momentum and route choices as speed increases.
- **Progression Hook:** Immediate score chasing plus characters, shards, coins, energy, and planned meta progression.
- **Must-Haves:** Great movement feel, endless generation, readable bounce variety, momentum multiplier scoring, fast reset, and a clear HUD.
- **Biggest Risk:** Balancing speed, procedural fairness, character variety, and retention systems without losing player control.
