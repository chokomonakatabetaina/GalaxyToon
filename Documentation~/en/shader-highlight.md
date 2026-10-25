# Shader: Highlight, Rim, Emission

[Documentation index](README.md)

Sections of `GalaxyShader/Toon` for gloss, edge light, emission, and patterns.

- [Highlight and MatCap](#highlight-and-matcap)
- [Anisotropic Highlight](#anisotropic-highlight)
- [Backlight](#backlight)
- [Rim Light](#rim-light)
- [Glitter and Refraction](#glitter-and-refraction)
- [Emission](#emission)
- [Layers 1–3](#layers-13)

Settings named "Hide ... in Shadow" weaken that light inside shadows (0 = still shines in shadow, 1 = hidden in shadow).

---

## Highlight and MatCap

Section **Highlight / MatCap**.

### Highlight

Stepped gloss for hair, eyes, metal, and so on. Turns on when the color alpha is above 0.

| Setting | Description |
|---|---|
| Highlight Color | Color. Alpha is strength |
| Highlight Size | Size of the lit area |
| Highlight Blur | Edge blur. 0 = crisp |
| Highlight Ramp | Gradient image: left = outer edge, right = center. Paint the highlight shape (steps, stripes) and color. Rows are shared with the shadow ramp |

### MatCap

Maps a sphere texture by the normal direction seen from the camera. Used for metal and glossy looks.

| Setting | Description |
|---|---|
| MatCap | Sphere texture. Turns on when a texture is assigned and the color alpha is above 0 |
| MatCap Color | Multiplied color. Alpha is strength |
| Hide MatCap in Shadow | How much it is weakened in shadow |
| MatCap 2 | Second sphere texture |
| MatCap 2 Color | Multiplied color. Alpha is strength |
| MatCap 2 Blend | Add or Multiply |
| MatCap 2 Mask (R) | Appears only where R is white |
| Hide MatCap 2 in Shadow | How much it is weakened in shadow |

## Anisotropic Highlight

Section **Anisotropic Highlight (Hair Angel Ring)**. A long, thin highlight along the hair flow. Turns on when the color alpha is above 0.

| Setting | Description |
|---|---|
| Anisotropic Highlight Color | Color. Alpha is strength |
| Anisotropic Direction | Direction of the hair flow. Tangent or Bitangent. If the ring appears vertical, switch to the other |
| Anisotropic Shift | Moves the ring up and down |
| Anisotropic Sharpness | Higher values give a thinner, sharper ring |
| Anisotropic Border | Size of the lit area |
| Anisotropic Blur | Edge blur |
| Hide Anisotropic Highlight in Shadow | How much it is weakened in shadow |
| Shape the Angel Ring with the Highlight Ramp | When ON, the ring's shape and color come from the Highlight Ramp |
| Wet Hair | When wet, hair clumps into strands, the gaps darken, and the angel ring gets thin and sharp. The amount is the Wet value |
| Wet Hair Clump Density | Number of clumps (per UV unit) |

## Backlight

When the light is behind the character (opposite the camera), the edges glow. Turns on when the color alpha is above 0.

| Setting | Description |
|---|---|
| Backlight Color | Color. Alpha is strength |
| Backlight Border | Size of the lit area |
| Backlight Blur | Edge blur |
| Backlight Directivity | Higher values glow only when the light is directly behind |
| Backlight View Influence | Influence of the view angle. At 1, only edges seen from the camera glow |
| Mix Texture Color into Backlight | How much base color is mixed into the light color |

## Rim Light

Lights the inner edge of the silhouette. Turns on when the color alpha is above 0.

| Setting | Description |
|---|---|
| Rim Light Color | Color. Alpha is strength |
| Rim Border | Size of the lit area |
| Rim Blur | Edge blur |
| Rim Thinness | Higher values give a thinner rim |
| Hide Rim in Shadow | How much it is weakened in shadow |
| Depth Rim | When ON, uses the depth texture to light only the silhouette edge (not bumps inside the body) |
| Depth Rim Width | Width of the lit edge (cm) |
| Depth Rim Depth Threshold | Treated as an edge when the object behind is farther than this (m) |

> Depth Rim requires **Depth Texture** to be ON in the URP Asset.

## Glitter and Refraction

Section **Glitter / Refraction (Gems, Glass)**.

### Glitter

Tiny mirror flakes twinkle with the light and view direction. Turns on when the color alpha is above 0.

| Setting | Description |
|---|---|
| Glitter Color | Color. Alpha is strength |
| Glitter Mask (R) | Appears only where R is white |
| Glitter Density | Flakes per UV unit |
| Glitter Flake Size | Size of each flake |
| Glitter Amount | How many flakes |
| Glitter Sparkle Sharpness | Higher values give brief flashes |
| Hide Glitter in Shadow | How much it is weakened in shadow |

### Refraction

Only in **Transparent** render mode. Distorts what is behind by the normals.

| Setting | Description |
|---|---|
| Refraction | Distortion strength. 0 = off |
| Refraction Rainbow Dispersion | Shifts RGB for a rainbow fringe |

> Refraction requires **Opaque Texture** to be ON in the URP Asset.

## Emission

Glows regardless of lighting. Turns on when Emission Color is not black. Two emission layers can be stacked.

| Setting | Description |
|---|---|
| Emission Mask | Where it glows (white = glows) |
| Emission Color | Glow color (HDR). Brighter colors bloom |
| Emission Auto Scroll | When ON, the mask moves according to Motion below |
| Emission Motion | XY = scroll per second, Z = rotation (°), W = rotation per second (°) |
| Emission Blink | Blinks per second. 0 = no blinking |
| Emission Layer 2 Mask / Color / Auto Scroll / Motion / Blink | Second emission layer. Same meaning as the first |

## Layers 1–3

Stacks up to three patterns, prints, or light bands on top of the main texture. Turns on when a texture is assigned.

| Setting | Description |
|---|---|
| Layer Mask (R) | Applied only where R is white |
| Layer Texture | Texture to stack. Tiling and offset work too |
| Layer Color | Multiplied color. Alpha is opacity |
| Layer Blend | Normal = overlay, Add, Screen, Multiply |
| Layer Auto Scroll | When ON, moves according to Motion below |
| Layer Motion | XY = scroll per second, Z = rotation (°), W = rotation per second (°) |
| Layer Fade | 0 = hidden, 1 = shown. Animate it from a script to show and hide |
| Layer Blink | Blinks per second. 0 = no blinking |
| Layer Blink Phase | Shifts the blink timing. Use it to make several layers alternate |
| Layer Glow | At 1, ignores shadows and lighting and is added as emission (Multiply does not glow) |
