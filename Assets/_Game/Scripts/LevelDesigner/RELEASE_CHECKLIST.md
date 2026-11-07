# Release checks — v0.2

## Completed outside Unity

- Parsed both C# files with the tree-sitter C# grammar; no syntax errors.
- Reviewed non-mutating preview: calculations and drawing do not assign scene transforms or invoke Undo recording.
- Reviewed Apply: validates the captured proposal, then records transforms, applies cached values, records prefab overrides and marks scenes dirty.
- ZIP content integrity checked.

This is not C# compilation or Unity execution. Shader compilation and visuals remain unverified.

## Required in Unity 6000.3.9f1

- [ ] Import all files; no compile warnings/errors attributable to this tool.
- [ ] Run Diagnostics > Run Scene Checks; all 17 assertions pass. This exercises add/multiply, unchanged axes, negative scale, uniform scale, non-mutating preview, non-accumulating shuffle, exact commit, Undo, Redo, invalid range rejection, rotation steps, name matching, hierarchy scope, exclusions, parent deduplication and stale-preview rejection.
- [ ] Test the docked window at minimum width and with a wide Inspector; light/dark themes and 100%/150%/200% display scaling.
- [ ] Test Create/Use Selection, Find Similar, capture stability while selecting other objects, Frame Objects, highlights and all target pages.
- [ ] Test All/Any matching, inactive objects, meshless objects, custom components and derived components.
- [ ] Verify Scale simple/advanced switches, random ranges, one-axis edits, equal ranges and Set up +1.
- [ ] Verify preset shortcuts, unique named presets, reopening Unity, JSON import/export and older v1 JSON imports. Confirm importing does not replace targets.
- [ ] Preview meshes with Built-in, URP and HDRP. Confirm shader import succeeds, cyan appearance is usable and fallback bounds are clear.
- [ ] Test skinned meshes, objects without renderers, terrain/sprites/particle bounds, nested renderers and negative scale.
- [ ] Preview Move/Rotate/Scale beneath rotated/nonuniformly scaled parents. Test both parent+child targets, with skipping on and off; ensure applied transforms agree with the proposed overlay.
- [ ] Undo/Redo after multiple applies, including a separate unrelated Unity edit between applies.
- [ ] Preview then Cancel, close window, save scene, start Play Mode, reload scripts and make external transform/hierarchy edits. No preview result should become serialized.
- [ ] Preview then change roots, rules, tabs, exclusions, seed or values. Apply must never commit stale values.
- [ ] Verify prefab roots, corresponding children, nested prefabs, variants and unpacked objects. Prefab Mode and prefab assets must be excluded.
- [ ] Apply to prefab instances, save/reopen scenes; overrides persist, source assets are unchanged.
- [ ] Multi-scene editing: only affected scenes dirty; all targets undo together.
- [ ] Profile 10, 100 and 1,000 objects, including detailed/skinned meshes. Establish and document practical supported limits.

## Commercial packaging before release

- [ ] Decide product name, license, support contact and supported Unity/render-pipeline versions.
- [ ] Generate and preserve Unity .meta GUIDs, export a clean Unity package and test installation/upgrades in an empty project.
- [ ] Provide a small demo scene, screenshots and a short onboarding video captured from the tested Editor.
- [ ] Resolve every failure above; record Editor version, platform and render pipeline with the release.
