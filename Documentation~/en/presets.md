# Presets

[Documentation index](README.md) / [Inspector](inspector.md)

The buttons under **Presets** in the Inspector. Pressing one fills in values suited to that body part or material.

- Textures and Main Color are not changed.
- Render Mode changes only for presets that require Transparent or Cutout (Tears / Drops, Gem, Lace, Glass, ...).
- Feel free to adjust the values afterwards.

---

## Character

Basic settings per body part. Start character materials here.

| Preset | Contents |
|---|---|
| Skin | Soft single shadow (reddish), weak rim, thin outline |
| Face | Same colors as skin, removes small shadows on the face (Flatten Face Shading 0.8). Add `GalaxyToonFace` to the character to follow the head direction |
| Hair | Crisp two-step shadow, angel-ring-like highlight, thin rim, outline |
| Clothes | Cloth-like two-step shadow, thin rim, outline. No gloss |
| Metal | Strong highlight and rim, dark shadow. Put a metal sphere texture in MatCap to look even more metallic |
| Face Parts | Eyes, teeth, tongue, eyelashes, etc. Almost no shadow and no outline |

## Character Finish

Adds a feature without changing other values.

| Preset | Material | Contents |
|---|---|---|
| Cast Hair Shadow | Hair | This hair casts bangs shadows on the face and eyes |
| Receive Hair Shadow | Face, eyes | Hair shadows fall according to the light direction (length 1.2 cm) |
| Eyes Crisp | Face, eyes | Adds depth and a large and a small highlight to the iris |
| Eyes Teary | Face, eyes | Teary eyes. Highlights tremble and water shimmer ripples below the iris |
| Eyes Sparkle | Face, eyes | Cross-shaped stars twinkle in the eyes |
| Embarrassed | Face | Cheeks turn slightly red with diagonal blush lines |
| Glitter | Costumes, props | Tiny mirror flakes twinkle with the light and view direction. Limit where it appears with the Glitter Mask |
| Fade When Close | Hair, body | Dithers out when the camera is closer than 30 cm and disappears at 10 cm, so you don't see inside when a close camera clips into the model |
| Anime Shading | All | Japanese anime style. Shadow colors are derived from the base color, borders are crisp, and outlines keep a constant screen width. For skin, a positive Shadow Hue Shift gives reddish shadows |

- Hair shadows require `GalaxyHairShadowFeature` in URP (add it with **Tools > GalaxyToon > 髪の影を有効にする** (Enable Hair Shadow)).
- The iris area and cheek positions can be set automatically with **Tools > GalaxyToon > 瞳の範囲を探す** (Find Iris Area) and **頬の位置を探す** (Find Cheek Positions).

## Skin / Cloth Surface

Overwrites only the surface look and keeps the current shadow, highlight, and other values.

| Preset | Contents |
|---|---|
| Skin Sweat | Sweat droplets on skin (sweat 0.7) |
| Skin Wet | The whole skin gets wet, darker and shiny (wet 1) |
| Skin Liquid (White) | White liquid flows down in streaks (liquid 0.6). Change the color with Liquid Color |
| Skin Rain | Skin in the rain. Water does not soak in; drops bead up and run off |
| Tears / Drops | Clear water for tears, water drops, and raindrops. Transparent, double-sided, sharp highlight and rim light, receives no shadows. For `GalaxyTears` and string materials |
| Gem (Refraction) | Gems, crystals, thick glass. Transparent; the background is distorted with rainbow dispersion and small facets sparkle |
| Clothes Soft | Soft fabric (cotton, knit). Blurry shadow, no gloss |
| Clothes Latex | Latex and enamel. Crisp shadow, small sharp highlight, strong edge shine and reflection |
| Clothes Leather | Leather (jackets, boots, belts). Slightly wide highlight, weak reflection, solid shadow |
| Clothes Wet | Clothes wet from rain or water. The color sinks deep and the fabric loses volume and clings to the body (shrink 1.5 cm) |
| Hair Wet | Rain-soaked hair. Hair clumps into strands (wet hair), loses volume (1 cm), and gradually darkens |
| Leather Wet | Rain-soaked leather. Absorbs a little water and deepens in color, and gets surface gloss sooner than cloth. Does not shrink |

The amounts can be changed during gameplay with `wet` / `sweat` / `liquid` / `rain` on `GalaxyToonEffects`.

## Add (on top of current)

Adds only the effect and keeps other values. Can be stacked (Sweat + Wet, etc.).

| Preset | Contents |
|---|---|
| Sweat | Adds sweat droplets (sweat 0.7, not flowing) |
| Flowing Sweat | Sweat droplets slowly flow down (sweat 0.7, flow speed 0.3) |
| Liquid | Adds white liquid streaks (liquid 0.6) |
| Flowing Liquid | Liquid streaks drip downward (liquid 0.6, drip speed 0.5) |
| Sticky | Adds thick, sticky, translucent liquid (viscosity 0.9, slow drip). Use `GalaxyStickyStrings` to pull strings |
| Wet | Makes everything wet (wet 1) |
| Foam | Adds white foam. It slowly slides down, bubbles merge, and they pop and shrink (foam 0.7) |
| Rain | Ripples spread on upward faces, drops run down vertical faces, and everything gets wet |
| Rain Soak | Concrete, stone, and fabric absorb water and darken in patches. Little gloss |
| Dirt / Soot | Soot stains stick to the body and remove gloss (dirt 0.5) |
| Mud | Brown mud splashes (dirt 0.45) |
| Scratches | Thin scratches. The lighter base color shows as lines with a slight dent (scratch 0.5) |
| Teleport (Vanish) | A glowing line rises from the feet and erases what it passes. Animate Dissolve Amount 0 → 1 |
| Teleport (Appear) | Appears with a glowing line. Animate Dissolve Amount 1 → 0 |

Add `GalaxyTeleport` to play the teleport automatically (outfit swaps are supported too).

## Environment

For buildings and props. Receives the scene's main light, ambient light, additional lights, and shadows from other objects. No outline.

| Preset | Contents |
|---|---|
| Wood Grain | Varnished or waxed wood (floors, furniture, instruments). Streaky gloss along the grain and weak reflection |
| Raw Wood | Unfinished wood (lumber, boards, crates). No gloss or reflection, soft shadow |
| Metal | Bare metal (pipes, handrails, machines). Metallic 1, smoothness 0.7 |
| Painted Metal | Painted metal (doors, lockers, walls). Metallic 0, smoothness 0.5 |
| Concrete | Concrete, stone, floors. No gloss, soft shadow. Turn on Triplanar if there are no UVs |
| Plastic | Small plastic props. Metallic 0, smoothness 0.65, white highlight |
| PET Bottle | Thin clear plastic. Transparent and double-sided, sharp highlight and white glowing edges. The content color comes from Main Color (A) |
| Clear Plastic | Colored plastic such as light covers. Transparent with strong reflection and rim light. Show it lit with Emission Color |
| Glass | Windows and lenses. Transparent with strong reflection at smoothness 0.95 |
| Diamond | Gems and crystals. Mirror-like reflection, sharp highlight, strong rim light |
| Water | Water surfaces and puddles. Transparent with distortion behind and mirror-like reflection. Rain ripples slowly spread on upward faces (set Rain to 0 to stop them) |
| Lace | Lace and mesh. Holes are cut out with the texture alpha (Cutout), double-sided, no gloss |
| Paper | Paper and cardboard. No gloss or reflection, soft and light shadow |
| Light Panel | Lights, monitors, signs. Lighter shadow and an emission color. Lights up the surroundings when lightmaps are baked |
| Magma | Lava and molten metal. Red-orange emission slowly pulses. The emission shape comes from the bright parts of the texture |
| Building Wall | Matte paint for exterior and interior walls. Smoothness 0.2, soft shadow |

## Reset

| Button | Contents |
|---|---|
| Reset All but Basic | Keeps textures, Main Color, normal maps, Render Mode, and Render Face, and resets everything else to defaults |
