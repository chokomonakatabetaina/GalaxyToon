# Shader: Outline, Dissolve, Near Fade

[Documentation index](README.md)

Sections of `GalaxyShader/Toon` for outlines and effects.

- [Outline](#outline)
- [Dissolve](#dissolve)
- [Fade When Camera Is Close](#fade-when-camera-is-close)

---

## Outline

Draws back faces pushed out along the normals. Not drawn in **Transparent** render mode.

| Setting | Description |
|---|---|
| Outline Width | Width (cm). At 0, outline drawing stops entirely |
| Use Smoothed Normals for Outline | Uses smoothed normals baked with the tool, so lines don't break or gap at corners |
| Outline Color | Line color |
| Mix Texture Color into Outline | How much base color is mixed into the line color |
| Match Outline to Light Brightness | Follows the main light's brightness so lines don't stand out in dark scenes |
| Thinner When Close | How much thinner lines get when the camera is closer than 1 m |
| Constant Screen-Space Outline Width | At 1, lines don't get thinner with distance (anime lines) |
| Outline Width Mask (R) | Varies the width per area by R (black = no line) |

### Baking smoothed normals

Outlines can break at model corners (UV seams or hard edges). Fix them as follows.

1. Select a prefab or the character in the scene.
2. Run **Tools > GalaxyToon > 輪郭線用のなめらか法線を焼く** (Bake Smooth Outline Normals).
3. Check that **Use Smoothed Normals for Outline** is ON (it is ON by default).

The baked meshes are saved in an `OutlineNormals/` folder and swapped into the renderers. The original FBX is not changed.

## Dissolve

Section **Dissolve (Vanish Effect)**: vanish and appear effects. Turns on with **Use Dissolve**. Dissolved areas also disappear from shadows, depth, and outlines.

| Setting | Description |
|---|---|
| Use Dissolve | ON / OFF |
| Dissolve Amount | 0 = intact, 1 = gone |
| Dissolve Mode | **Noise**: disappears scattered by a noise pattern<br>**Position**: disappears in order along a chosen direction |
| Position: Dissolve Direction | XYZ = direction (world), W = range (m) |
| Position: Start Offset | Distance from the object's origin (m). To dissolve from the top, use direction (0, -1, 0) and start = -height |
| Noise Texture | Uses R as noise. Uses built-in noise if empty |
| Noise Scale | Fineness of the noise |
| Noise Strength | Jaggedness of the border. 2 or more uses the noise texture |
| Invert | Appears instead of vanishing (for the new outfit in an outfit swap) |
| Position: Measure by World Height | The line is at the same height across all body parts (for teleport effects) |
| Edge Width | Width of the glowing border |
| Edge Color | Border color (HDR). Alpha is strength |

> The **GalaxyTeleport** component plays teleport and outfit-change effects with a glowing line running over the body automatically. Builds must include at least one material with dissolve ON.

## Fade When Camera Is Close

Dithers the object away when the camera gets close, so you don't see inside hair or the body when a close camera clips into them. It stays opaque, so the render order does not change.

| Setting | Description |
|---|---|
| Fade When Camera Is Close | ON / OFF |
| Fade: Fully Hidden Closer Than | Completely hidden closer than this distance (m) |
| Fade: Fully Visible Farther Than | Not faded farther than this distance (m) |
