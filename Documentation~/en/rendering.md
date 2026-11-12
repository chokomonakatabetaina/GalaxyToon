# Screen Effects, Audio, Volume

[Documentation index](README.md)

Features that work outside the material (added in 1.0.2). To add a Renderer Feature, select the **Universal Renderer Data** you use and click **Add Renderer Feature** in the Inspector.

- [Galaxy Screen Line](#galaxy-screen-line)
- [Galaxy Glow](#galaxy-glow)
- [Galaxy Paint](#galaxy-paint)
- [Galaxy Character Shadow](#galaxy-character-shadow)
- [Galaxy Toon Volume](#galaxy-toon-volume)
- [Galaxy Audio React](#galaxy-audio-react)
- [Galaxy Toon Light (per character)](#galaxy-toon-light-per-character)
- [Motion Vectors](#motion-vectors)

---

## Galaxy Screen Line

Draws silhouette lines and inner lines (fingers, overlaps, part borders) on screen from depth and normal differences. Lines sit outside the silhouette, so characters don't look thinner. Works together with the extruded outline.

| Property | Description |
|---|---|
| Thickness | Line width |
| Depth Threshold | Silhouette lines: a line where the neighbor is closer by this ratio (depth difference ÷ depth) |
| Normal Threshold | Inner lines: a line where the normal difference (1 − dot) exceeds this |
| Strength | Line opacity |
| Darken | Color multiplier on the line. Lower = darker |
| Max Distance | No lines beyond this distance (m) |
| Object Lines | Also draws lines where the object (the material's object ID `_ObjectId`) changes, e.g. where clothes overlap the body |

> Uses the camera depth and normal textures.

## Galaxy Glow

Softly blooms only the bright areas. Tunable separately from Bloom.

| Property | Description |
|---|---|
| Threshold | Areas brighter than this glow (brightest color channel) |
| Softness | Smoothness of the threshold |
| Intensity | Glow strength |
| Spread | Glow spread (pixels) |
| Character Depth | Areas farther than this from the camera (m, the background) don't glow. 0 = whole screen |

## Galaxy Paint

Smooths out uneven paint (Kuwahara filter) and steps the brightness, without changing hue. Intended for backgrounds.

| Property | Description |
|---|---|
| Radius | Smoothing radius (1–4) |
| Levels | Number of brightness steps. 0 = no stepping |
| Strength | Effect strength |

## Galaxy Character Shadow

A high-resolution shadow map in a tight area around the characters. Self-shadows from bangs, fingers, collars, and chins come out crisp. **Galaxy Toon Light** (the dedicated character light) also casts shadows. Applies to characters with **Galaxy Toon Face**.

| Property | Description |
|---|---|
| Resolution | Shadow map size (pixels) |
| Strength | Shadow darkness |
| Softness | Shadow edge blur (pixels) |
| Depth Bias / Normal Bias | Offsets that prevent self-shadow artifacts (multiples of the texel size) |

## Galaxy Toon Volume

With **Add Override > GalaxyToon > Galaxy Toon** on a Volume, you can change rim, shadow, color, and outline width for all characters per scene or time of day. Also add **Galaxy Toon Volume Feature** to the Renderer (**Tools > GalaxyToon > Volume を有効にする** / Enable Volume does it for you). In the sample scene, the buttons on the right switch presets (Off, Default, Sunset, Night, Pastel, Crisp).

| Property | Description |
|---|---|
| Rim | Rim strength multiplier |
| Shadow Darken | Extra darkening on the shadow side |
| Shadow Tint | Color multiplied onto the shadow side (white = unchanged) |
| Saturation | Saturation (-1 = gray, +1 = double) |
| Brightness | Brightness (-1 to +1) |
| Outline Width | Outline width multiplier |

For materials that should not be affected (such as eyes), turn off **Volume Receive** in Toon Plus.

## Galaxy Audio React

Measures the playing audio in low, mid, and high bands and writes the global `_GalaxyAudio` (x = low, y = mid, z = high, w = all). Pick a band in the material's Toon Plus section and raise "Audio Emission" and the others to pulse glow and line width with the sound.

| Property | Description |
|---|---|
| Source | The audio source to measure. Empty = everything you hear |
| Gain | Level multiplier (match it to the song volume) |
| Attack / Release | How fast the level rises / falls |
| Low Max / Mid Max | Low/mid and mid/high band borders (Hz) |

## Galaxy Toon Light (per character)

Since 1.0.2, putting a character in **Target** lights only that character (light direction, color, and brightness per character). Empty = all characters, as before. **Night** is the day/night ramp mix amount (Ramp Day / Night in Toon Plus).

## Motion Vectors

A **MotionVectors** pass is included for TAA, STP, and motion blur. Shapes moved by hair sway, soft touch, wet shrink, and so on go straight into the motion vectors, so moving characters don't smear.
