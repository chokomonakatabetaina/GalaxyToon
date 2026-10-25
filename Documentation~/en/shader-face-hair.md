# Shader: Face, Eyes, Hair

[Documentation index](README.md)

Sections of `GalaxyShader/Toon` for the face, eyes, cheeks, and hair.

- [Face](#face)
- [Eyes](#eyes)
- [Blush](#blush)
- [Hair Sway](#hair-sway)
- [Hair Shadow](#hair-shadow)
- [Stencil](#stencil)

---

## Face

Removes small shadows around the nose and cheeks for clean, anime-like face shading.

| Setting | Description |
|---|---|
| Flatten Face Shading | Bends normals toward the front of the face to remove small shadows (0–1) |
| Shade Face as a Sphere | Bends normals toward a sphere around the head for round, smooth shading (0–1) |
| Use SDF Face Shadow | When ON, the shadow shape for each light angle comes from an SDF shadow map |
| SDF Face Shadow | The SDF shadow map. R = shadow border with light from the right, G = from the left |

### How to use

1. Add **GalaxyToonFace** to the character root. The head bone direction is passed to the face material every frame, so shading follows when the head turns.
2. Set Flatten Face Shading to about 0.8 (the **Face** preset does this).
3. For SDF face shadows, create a shadow map with **Tools > GalaxyToon > SDF 影マップを作る** (Create SDF Shadow Map). The texture is assigned to the material automatically.

> Without `GalaxyToonFace`, the model's +Z axis is treated as the front of the face.

## Eyes

Section **Eyes (Depth / Highlights / Teary / Sparkle)**. Adds depth, highlights, tears, and sparkles to the iris. Works for irises painted in the face texture and for separate iris meshes.

| Setting | Description |
|---|---|
| Use Eye Effects | ON / OFF |
| Iris 1 Area / Iris 2 Area | UV rectangle containing the iris (XY = bottom-left, ZW = top-right). Effects appear only inside the circle inscribed in this rectangle |
| Iris Depth | The iris pattern shifts inward with the view angle, so it looks recessed under the cornea |
| Eye Highlight Color | Highlight color. Alpha is strength |
| Eye Highlight Position | XY = large highlight, ZW = small highlight (-1 to 1; screen right = +X, up = +Y) |
| Eye Highlight Size | Size of the highlights |
| Teary Eyes | Highlights tremble and a shimmering water light appears below the iris |
| Sparkle Color | Color of the cross-shaped stars. Alpha is strength; 0 = none |
| Sparkle Count | Number of stars |
| Sparkle Speed | How fast they twinkle |

- Highlights are placed relative to the screen, so they face the same way even if the iris is rotated or mirrored in the texture.
- Select the character and run **Tools > GalaxyToon > 瞳の範囲を探す** (Find Iris Area) to fill in the iris areas automatically.

## Blush

Section **Blush (Embarrassed)**. Tints the cheeks and adds blush lines.

| Setting | Description |
|---|---|
| Blush | Amount (0–1). Can be changed during gameplay with `blush` on `GalaxyToonEffects` |
| Blush Color | Color. Alpha is opacity |
| Cheek Positions | UV of the cheek centers (XY = cheek 1, ZW = cheek 2) |
| Blush Size | Blush radius (UV) |
| Blush Softness | Edge softness |
| Blush Lines | Amount of diagonal hatching |

Select the character and run **Tools > GalaxyToon > 頬の位置を探す** (Find Cheek Positions) to fill in the cheek positions automatically.

## Hair Sway

Sways hair that has no bones, in the shader.

| Setting | Description |
|---|---|
| Hair Sway | Strength (0–2). 0 = no sway |

Add **GalaxyToonHairSway** to the character root and the hair tips softly lag behind as the head turns, the character walks, or stops. Roots stay still; tips move the most.

## Hair Shadow

Section **Hair Shadow (Bangs Cast Shadow on Face)**. Casts bangs shadows onto the face and eyes.

| Setting | Material | Description |
|---|---|---|
| Cast Hair Shadow | Hair | When ON, this hair's shape casts the shadow |
| Receive Hair Shadow | Face, eyes | How much shadow is received. Set to 1 |
| Hair Shadow Length | Face, eyes | How far the shadow is offset toward the light (cm) |
| Hair Shadow Gap | Face, eyes | Gap that keeps hair touching the skin from casting too much shadow (cm) |

### How to use

1. Run **Tools > GalaxyToon > 髪の影を有効にする** (Enable Hair Shadow) to add **GalaxyHairShadowFeature** to the URP Renderer you use (adding it manually with Add Renderer Feature works the same).
2. Apply the preset **Cast Hair Shadow** to hair materials.
3. Apply the preset **Receive Hair Shadow** to face and eye materials.
4. Add **GalaxyToonFace** to the character root.

- A pixel is in shadow if there is hair in front of it at a point shifted slightly toward the light on screen.
- When no character with an active `GalaxyToonFace` exists, the required processing is skipped entirely (zero cost).

## Stencil

Section **Stencil (Show Eyes Through Hair, etc.)**. Shows the eyes and brows through the bangs. Set it up with the buttons inside the section.

| Button | Material | Behavior |
|---|---|---|
| Write (Eyes, Brows) | Eyes, brows | Writes stencil 1. The render queue is unchanged |
| See Through (Hair) | Hair | Does not draw where stencil is 1. Moves the render queue after the eyes (Geometry+1) |
| Off | All | Turns the stencil off |

The stencil skips drawing that part of the hair, so the eyes stay crisp. To make the bangs semi-transparent with a gradient instead, use **Tools > GalaxyToon > 前髪を透過する** (See-Through Bangs, `GalaxyToonSeeThroughBangs`).
