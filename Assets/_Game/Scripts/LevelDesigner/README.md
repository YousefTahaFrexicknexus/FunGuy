# Level Designer — Designer Edition (v0.2)

A dockable Unity editor tool for selecting similar level objects and trying transform variations. Target: Unity 6000.3.9f1.

**Status:** implementation candidate, not a certified commercial release. C# syntax parsing was checked outside Unity. Unity compilation, visual appearance and runtime diagnostics must still be verified in the target Editor. See `RELEASE_CHECKLIST.md`.

## Install or upgrade

1. Extract the ZIP.
2. Copy `Assets/LevelDesigner` into your project's `Assets` folder, replacing the older files if installed.
3. Let Unity finish compiling.
4. Open **Tools > Level Designer > Batch Transform** and dock the window beside the Inspector.

Keep all supplied code and the preview shader together in the Editor folder. No runtime components or third-party packages are required. The Diagnostics menu is optional verification tooling.

## Your first variation

1. In **Choose objects**, select **Use Selection**. Select some level objects and click **Capture Selected Objects**.
2. In **Adjust**, choose **Rotate**, then **Random Facing**.
3. Click **Preview Changes**. Look in the Scene view: cyan shapes show the proposed result over the original objects.
4. Use **Shuffle** to try another variation, **Apply Changes** to commit, or **Cancel** to dismiss it.
5. Undo with Ctrl/Cmd+Z. Save the scene normally when ready.

Preview is an overlay: it never moves the originals, creates prefab overrides, or adds Undo entries. Closing the window, changing settings, entering Play Mode, undoing, or reloading scripts discards it. Applying uses the exact sampled values displayed by preview. Shuffling always works from unchanged originals, so values do not stack up while experimenting.

## Find matching objects

Switch to **Find Similar**, choose an **Example Object**, and assign a scene parent to **Search Inside**. Choose **Same Prefab**, **Same Name**, or **Same 3D Model**, then click **Find Similar Objects**.

Advanced matching lets you combine rules and choose **All checked rules** or **Any checked rule**. It also offers **Same Component** for technical workflows.

- The search includes descendants of the parent. **Include the parent itself** is optional; inactive objects are included by default.
- **Same Prefab** matches the exact corresponding source GameObject. A prefab root matches equivalent roots; a child matches equivalent children. Variants are distinct. Unpacked objects do not match by prefab.
- **Same Name** is case-sensitive and can ignore trailing copy numbers and `(Clone)`.
- **Same 3D Model** looks for the same mesh asset on a MeshFilter or SkinnedMeshRenderer on the object itself, not its children.
- **Same Component** looks for a chosen component type on the object itself, including derived types.

The captured working set stays in the tool even when Unity's selection changes. **Frame Objects** focuses the Scene camera. **Select in Scene** reselects the working set. Highlight shows amber bounds without changing selection.

Expand **Review objects / exclude** to temporarily uncheck objects. Every item is accessible through pages of 20. **Include All** clears exclusions. Excluding a child does not stop it from inheriting changes to its parent.

**Avoid editing children twice** skips a child when its ancestor is also targeted. Disable this only when you want both transforms edited. Search settings or hierarchy changes require another search in Find Similar mode.

## Rotate

- **Random Facing:** new Y rotation, 0–360 degrees.
- **Slight Tilt:** add small X/Z angle offsets.
- Enable only axes you want to change.
- **Choose a new angle** replaces the selected Euler components. **Add to current angle** adds to them.
- **Angle steps:** enter 15, 45 or 90 to choose multiples of that angle within the min/max range. Zero means continuous random angles. A range with no valid step is rejected.
- **World** uses scene axes. **Relative to Parent** uses local Euler angles.

Equal min/max values give a fixed result. Euler angles can display differently in Unity while representing the same orientation. Multi-axis Euler sampling is not a uniform distribution of 3D orientations.

## Scale

Start with **Use one value for all axes** for a compact control. Turn it off to choose axes and independent values.

| Operation | Starting X = 2, entered value = 1.2 |
| --- | --- |
| Increase by | X = 3.2 |
| Multiply by | X = 2.4 |
| Random size (replace) | X becomes a random value in the entered range |
| Random size (multiply) | Current X is multiplied by a random value in the range |

Multiplying all axes by the same value preserves proportions. Adding the same number or replacing values does not necessarily preserve proportions. **Natural Size Variation** sets up random multiplication; **Set up +1** fills an Add operation. Preview and Apply are still required.

Scale is always local, like the Transform Inspector. Zero and negative values are supported. Positive multipliers retain negative scale signs, and multiplying zero leaves zero.

## Move

Use **Set position**, **Move by offset**, or **Random offset**. Check the desired axes and enter values or min/max offsets.

**Relative to Parent** means parent-local coordinates, not the object's rotated forward/right/up directions. Parent scale and rotation affect world displacement. World movement beneath a zero or near-zero scale parent is rejected because the conversion cannot be computed reliably.

## Quick starts and saved presets

Expand **Quick starts & my presets** to load Trees, Rocks, Ground Clutter or Buildings. These fill all three tabs; **only the active tab** is previewed/applied at a time. Buildings uses 90-degree Y steps; its scale multiplier is 1.

To save your own, type a name and click **Save My Preset**. It appears in the same dropdown. Duplicate names get a numeric suffix so earlier presets are retained. Named presets are shared within this project via `ProjectSettings/LevelDesignerPresets.json`, which can be version-controlled.

Export/Import File allows sharing JSON presets between projects. Importing settings preserves the current working set and search settings. v1 JSON presets are supported. Presets do not contain scene references or object transforms. Current window options are also remembered on this computer.

In **Randomness options**, enable **Repeatable variations** and choose a seed. Shuffle advances the variation number. Reproducing an exact result requires the same seed, unchanged objects, target ordering and starting transforms. After Apply, offsets/multipliers intentionally use the new current values. Undo before repeating from an earlier starting point.

## What preview shows

Static meshes render as translucent cyan geometry. Skinned meshes are baked into temporary preview meshes. Other renderer types and objects without meshes use bounds/pivot boxes. This is a shape preview: original materials, lighting, shaders, particles and animation are not simulated. If the preview shader is unavailable, meshes also fall back to bounds.

Preview accounts for parent transforms and proposed parent changes. External transform/hierarchy edits invalidate the proposal before it can be committed. Changes to renderer content or animations during preview are not a live simulation; refresh preview if those change. Large, detailed target sets may be expensive to draw or bake.

The preview shader and overlay require visual verification with your render pipeline; Built-in, URP and HDRP have not been tested in this environment.

## Scene and prefab behavior

Only normal loaded scene instances can be edited. Prefab assets, Prefab Mode objects, preview scenes, noneditable objects and Play Mode are excluded. Apply records prefab instance overrides and marks affected scenes dirty; it never applies overrides back to prefab assets or saves scenes automatically.

Each Apply is one Undo group. All results are validated before committing. If a commit fails, its Undo group is rolled back. Preview has no serialized scene state, so saving while previewing saves the originals.

## Validation

Run **Tools > Level Designer > Diagnostics > Run Scene Checks** in Edit Mode. The checks create a temporary additive scene, exercise transforms/preview/Undo, then close that temporary scene and restore the prior selection and active scene. They do not save scenes or create project assets. Results appear in the Console.

These checks are provided but were not run here: Unity is not available. Do not advertise commercial compatibility until the release checklist passes in Unity 6000.3.9f1.

## Deferred features

Drop to Ground / surface alignment, distribution/scattering, preset deletion/renaming UI, live original-material preview, and Asset Store packaging are not included in this iteration.
