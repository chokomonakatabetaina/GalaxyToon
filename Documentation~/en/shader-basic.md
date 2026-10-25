# Shader: Basic, Lighting, Shadow

[Documentation index](README.md)

Sections of `GalaxyShader/Toon` for color, surface, lighting, and shadows.

- [Basic](#basic)
- [Color Adjustment](#color-adjustment)
- [Metallic, Smoothness, Reflection](#metallic-smoothness-reflection)
- [Triplanar](#triplanar)
- [Lighting](#lighting)
- [Shadow](#shadow)
- [Anime Shading](#anime-shading)

---

## Basic

Section **Basic (Color / Normal)**: base color and surface bumps.

| Setting | Description |
|---|---|
| Main Texture | Base color texture. Alpha is used as opacity in Cutout and Transparent modes |
| Main Color | Color multiplied with the texture. Alpha is opacity |
| Normal Map | Texture describing the direction of bumps. Shading, highlights, and rim follow the bumps |
| Normal Strength | How strongly the normal map applies (0–2) |
| Detail Normal Map | A second normal map with fine bumps layered on the first. Can be tiled finely |
| Detail Normal Strength | How strongly the detail normal map applies (0–2) |
| Occlusion (AO Map) | Darkness of crevices and overlaps. Darker areas fall into shadow more easily |
| Occlusion Strength | How strongly the AO map applies |

> If you have no normal map, create one from the main texture with **Tools > GalaxyToon > ノーマルマップを作る** (Create Normal Map).

## Color Adjustment

Changes the colors without repainting the texture. Turns on when any value differs from its default.

| Setting | Description |
|---|---|
| Hue Shift | Rotates the hue (-0.5 to 0.5; ±0.5 gives the opposite color) |
| Saturation | 0 = grayscale, 1 = unchanged, 2 = vivid |
| Brightness | 0 = black, 1 = unchanged |
| Gamma | Changes the midtones. 1 = unchanged |

## Metallic, Smoothness, Reflection

Section **Metallic / Smoothness / Reflection**. The values mean the same as URP's Metallic and Smoothness. Turns on when Metallic or Smoothness is above 0.

| Setting | Description |
|---|---|
| Metallic | 0 = non-metal, 1 = metal. The more metallic, the darker the base color and the more reflections take on the base color |
| Smoothness | 0 = rough, 1 = glossy. The glossier, the smaller and sharper the highlight and reflection |
| Metallic / Smoothness Map | R = metallic, A = smoothness (same layout as URP) |
| Reflection Strength | Strength of reflection probe reflections |
| Reflection Steps | How many bands reflections are split into. 0 = smooth; more steps look more toon-like |

## Triplanar

Section **Triplanar (No UVs Needed)**. Projects the texture from three world axes onto models without UVs (buildings, rocks) or with stretched UVs.

| Setting | Description |
|---|---|
| Triplanar | When ON, the main texture is applied in world space instead of by UV |
| Triplanar Tiling | Repeats per meter |
| Triplanar Edge Sharpness | How the three projections blend. Higher values give sharper seams |

## Lighting

Which light illuminates the material and the brightness range.

| Setting | Description |
|---|---|
| Main Light | **CharacterLight**: the character light from `GalaxyToonLight` (the scene's main light if none is placed)<br>**SceneMainLight**: the scene's main light<br>**BakedLightAndShadowOnly**: only lightmap light and cast shadows (for baked environments) |
| Min Brightness | Never darker than this. Prevents pitch-black results in dark scenes |
| Max Brightness | Never brighter than this. Prevents blown-out highlights |
| Ignore Light Color | At 1, the light color is made grayscale and only its brightness is used |
| Ambient Strength | How much scene ambient light (Skybox / Ambient) is added |
| Additional Light Strength | How much point and spot lights are added. At 0, additional lights are not computed |
| Baked Light Steps | How many bands lightmap light is split into. 0 = smooth |

- Cast shadows are always read from the scene main light's shadow map, regardless of the Main Light setting.
- Additional lights are cut into lit / unlit at the Shadow 1 border, which gives a toon-like look.

## Shadow

Splits lighting into up to three shadow steps.

### Stepped Shadows

| Setting | Description |
|---|---|
| Shadow 1 Color | Color of the first shadow step. Alpha is opacity |
| Shadow 1 Border | Where the shadow starts. Higher values give a wider shadow |
| Shadow 1 Blur | Blur of the border. 0 = crisp |
| Shadow 2 Color / Border / Blur | Second shadow step. Turns on when the color alpha is above 0 |
| Shadow 3 Color / Border / Blur | Third shadow step. Turns on when the color alpha is above 0 |
| Shadow Strength Mask | Darker R gives weaker shadows. Paint areas that should not get shadows |
| Receive Shadows from Other Objects | How much shadow cast by other objects (or itself) is received |
| Border Anti-aliasing | Reduces jaggies on shadow borders |

### Ramp

Defines shadow colors with a gradient image. When a texture is assigned, the ramp replaces the stepped shadows.

| Setting | Description |
|---|---|
| Shadow Ramp | Gradient image: left = darkest, right = lit |
| Ramp Rows | How many gradients (one per body part) are stacked vertically in the image |
| Ramp Row | Which row to use when there is no part ID map (0 = top) |
| Part ID Map | R selects the row per pixel (0 = top … 1 = bottom) |

> Create a ramp template with **Tools > GalaxyToon > ランプのひな形を作る** (Create Ramp Templates) (four rows from the top: skin / hair / clothes / metal). Repaint it in an image editor.

## Anime Shading

Section **Anime Shading (Japanese Anime Style)**. Turns on with the **Anime Shading** toggle; when OFF, the look is unchanged.

| Setting | Description |
|---|---|
| Anime Shading | ON / OFF |
| Crisp Shadow Edges | Makes shadow borders thin like cel shading |
| Shadow Hue Shift | Rotates the shadow hue. - toward blue-violet, + toward reddish |
| Shadow Saturation | Saturation of the shadow color derived from the base color |
| Shadow Brightness | Brightness of the shadow color. Lower is darker |
| Shadow Edge Color | Color of a thin band at the shadow border (e.g. a red terminator line on skin). Alpha is strength |
| Shadow Edge Color Width | Width of the band |
| Keep Painted Colors | Keeps painted colors from being dulled by uneven lighting |
| Remove Fine Bumps | Weakens normal maps for flat fills |
| Remove Fine Texture Detail | Evens out dirt and unevenness in the texture into flat colors |
| Detail Removal Strength | Blur strength used to even out detail (mip level) |
| Remove Realistic Gloss / Reflections | Removes realistic metallic, gloss, and reflection |

The setting for a constant screen-space outline width is under [Outline](shader-outline-dissolve.md#outline).
