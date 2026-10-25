# Shader: Fur

[Documentation index](README.md)

`GalaxyShader/ToonFur` draws fur. The same mesh is drawn in many layers, each pushed slightly outward along the normals, and each layer cuts out the cross-sections of the hairs (shell method).

This shader is not drawn just by assigning it to a material. It is drawn by the **GalaxyToonFur** component.

---

## How to use

1. Create a material with **GalaxyShader/ToonFur**.
2. Add **GalaxyToonFur** to the renderer (MeshRenderer or SkinnedMeshRenderer) that should grow fur.
3. Assign the material from step 1 to `furMaterial`.

Skinned meshes are re-sampled every frame, so the fur follows animation. Fur tips trail behind when the body moves.

## Material settings

The fur material's labels are shown in Japanese; the Japanese label is given in parentheses.

| Setting | Description |
|---|---|
| Fur Color texture (毛の色 (テクスチャ)) | Fur color texture |
| Fur Color (毛の色) | Color multiplied with the texture |
| Alpha cutoff (テクスチャの透明部分を切る) | Skips pixels whose texture alpha is below this |
| Fur Mask (毛の生える所) | Fur grows where R is white |
| Fur Length (毛の長さ) | Length (cm) |
| Fur Density (毛の密度) | Hairs per UV unit |
| Fur Thickness (毛の太さ) | Thickness. Upper layers get thinner |
| Gravity (重力で垂れる) | How much the tips droop with gravity |
| Root Darkness (根元の暗さ) | How much the roots are darkened (shadow from overlapping hair) |
| Tip Color (毛先の色) | Color mixed into the tips. Alpha is the mix strength |
| Shadow Color (影の色) | Shadow color |
| Shadow Border (影の境界) | Where the shadow starts |
| Shadow Blur (影のぼかし) | Blur of the shadow border |
| Tip Rim Light (毛先の縁の光) | Rim light on the tips (HDR). Alpha is strength |
| Min / Max Brightness (明るさの下限 / 上限) | Same as in `GalaxyShader/Toon` |

Lighting is the same as `GalaxyShader/Toon`: the character light from `GalaxyToonLight`, or the scene's main light if none is placed.

## Component settings (GalaxyToonFur)

| Setting | Description |
|---|---|
| furMaterial | The fur material. Nothing is drawn if empty |
| shells | Number of layers (2–32). More layers are fluffier but heavier. 8–16 is a good range |
| subMesh | Submesh index to grow fur on. -1 = all |
| sway | How strongly fur trails when the body moves |
| stiffness | How quickly the fur settles back |

> Each layer is an extra draw. Reduce the number of layers for large surfaces or many characters.
