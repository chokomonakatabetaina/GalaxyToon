# Shader: Wet, Sweat, Liquid, Dirt

[Documentation index](README.md)

The **Wet / Sweat / Liquid / Dirt** section of `GalaxyShader/Toon`. Sweat droplets, liquid streaks, foam, rain ripples, dirt, and scratches are generated in the shader without extra textures.

- [Common](#common)
- [Wet](#wet)
- [Sweat](#sweat)
- [Liquid](#liquid)
- [Foam](#foam)
- [Rain](#rain)
- [Dirt and Scratches](#dirt-and-scratches)
- [Changing Amounts During Gameplay](#changing-amounts-during-gameplay)

---

## Common

| Setting | Description |
|---|---|
| Use Wet / Sweat / Liquid | When ON, every feature in this section is enabled. Materials with it OFF do not change even if `GalaxyToonEffects` changes the amounts |
| Effect Mask | Mask of where effects appear. R = wet, G = sweat, B = liquid, A = foam (white = appears) |
| Droplet / Streak Bump | Bump strength of sweat droplets, liquid streaks, foam, and ripples. Highlights follow the droplet shapes |

Each effect has an **amount** and a **receive strength**.

- **Amount**: how much appears. Can be driven for the whole character with `GalaxyToonEffects`.
- **Receive strength**: how much this material receives. For example, set clothes to 0 so they don't sweat.

## Wet

| Setting | Description |
|---|---|
| Wet | Amount (0–1) |
| Wet Receive Strength | How much this material receives |
| Darken When Wet | Absorbed water multiplies by the texture's own color (0.5 = squared, 1 = cubed). Cloth and leather deepen in color |
| Wet Gloss | Gloss strength of wet surfaces |
| Soak | Concrete, stone, cloth: wet areas darken in patches. The patches spread as wetness increases until the whole surface is dark |
| Shrink When Wet | Cloth and fur lose volume and cling to the body. Inward offset along the normal at wet 1 (cm). 4–6 for fluffy tails |

- Sweating also makes the whole skin slightly wet and glossy.
- For wet hair, combine with **Wet Hair** in [Anisotropic Highlight](shader-highlight.md#anisotropic-highlight).

## Sweat

Splits the UV into a grid and places a sweat droplet of random position and size in each cell.

| Setting | Description |
|---|---|
| Sweat | Amount (0–1). More droplets as it increases |
| Sweat Receive Strength | Skin = 1, clothes = 0 |
| Sweat Droplet Density | Number of grid cells |
| Sweat Droplet Size | Droplet size |
| Sweat Flow Speed | 0 = still. Higher values make droplets slowly run down |

## Liquid

Creates streaks dripping along the body.

| Setting | Description |
|---|---|
| Liquid | Amount (0–1). Even at 1 it covers only about half |
| Liquid Receive Strength | How much this material receives |
| Liquid Color | Color. Alpha is opacity |
| Liquid Streak Density | Fineness of the streaks |
| Liquid Drip Speed | How fast the streaks flow |
| Liquid Gloss | Gloss strength |
| Liquid Viscosity | 0 = runny, 1 = sticky. Stickier liquid flows slower, forms round blobs, and becomes a thick film raised in the middle |
| Liquid Flow Direction | Flow direction (object-space XYZ). Default is down; inside a mouth, point it toward the tongue tip |

## Foam

Three layers of large, medium, and small bubbles; small bubbles fill the gaps between large ones. Applied in UV space, so the foam does not slide when the body moves.

| Setting | Description |
|---|---|
| Foam | Amount (0–1). Foam sticks in clusters; more foam covers a wider area |
| Foam Receive Strength | How much this material receives |
| Foam Color | Color. Alpha is opacity |
| Foam Density | Large bubbles per UV unit |
| Foam Size | Bubble radius relative to the cell |
| Foam Slide Speed | Speed of sliding down in world space |
| Foam Pop Rate | Per second. Bubbles shrink and pop at the end of their life, and new ones appear |
| Foam Bubble Merging | How much nearby bubbles merge |

## Rain

Generated in world space, so ground, roofs, walls, and characters all get the same rain.

| Setting | Description |
|---|---|
| Rain | Amount (0–1) |
| Rain Receive Strength | Set to 0 under a roof |
| Rain Ripples | Ripples on upward faces. Ground and puddles = 1, clothes, hair, skin = 0 |
| Rain Drip Amount | Drops running down vertical faces. Keep it small for clothes and hair |
| Rain Splash on Surface | Strength of splashes bouncing off the body. Used when `rainSplashMaterial` is set on `GalaxyToonEffects` |
| Rain Ripple / Drip Density | Fineness per meter |
| Rain Speed | Ripple spread and drip flow speed |

Surfaces hit by rain also get slightly wet overall.

## Dirt and Scratches

| Setting | Description |
|---|---|
| Dirt / Soot | Amount (0–1). Stains stuck to the object; they spread and connect as the amount increases. Gloss disappears too |
| Dirt / Scratch Receive Strength | How much this material receives |
| Dirt Color | Color. Alpha is opacity |
| Dirt Density | Fineness per meter |
| Scratches | Amount (0–1). Thin scratches in random directions and lengths |
| Scratch Color | Color of the exposed base. Alpha is opacity |
| Scratch Density | Fineness per UV unit |

## Changing Amounts During Gameplay

Add **GalaxyToonEffects** to the character root to change the amounts for the whole character at once.

```csharp
using GalaxyToon;

var fx = GetComponent<GalaxyToonEffects>();
fx.wet = 1f;      // wet
fx.sweat = 0.7f;  // sweat
fx.liquid = 0.5f; // liquid
fx.foam = 0.5f;   // foam
fx.rain = 1f;     // rain
fx.dirt = 0.3f;   // dirt
fx.scratch = 0.2f;// scratches
fx.blush = 0.8f;  // blush
```

- Each material's receive strength and effect mask decide how strongly each body part is affected.
- How fast the character gets wet in the rain and how fast it dries are also set on the component.
