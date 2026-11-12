# GalaxyToon

[日本語](README.md) | English

**From an anime face to rain-soaked hair and skin that dents under a touch. One shader.**

A toon shader for URP. Besides stepped shadows, rim light, MatCap, and outlines, a single shader covers eyes, blush, hair shadows, wet / sweat / liquid / rain, glitter and refraction, dissolve and teleport effects, soft-touch skin, and more.

### What makes GalaxyToon different

- **One shader for a whole character**: face (SDF shadow that follows the head, nose line), eyes (parallax, teary, sparkle), hair (two angel rings, hair shadow on the face, tip color), skin (blush at the terminator, backlight translucency) and clothes (latex, stockings, metal) without switching shaders.
- **Unused features cost nothing**: every feature is a keyword, so unused ones are compiled out. A profiling tool measures each feature one by one.
- **Migrate as-is**: MToon (VRM), lilToon, UTS and URP Lit characters are converted together with face, eyes, outline and hair-shadow setup.
- **Effects that run in the game**: wet, sweat, rain, tears, sticky strings, teleport and GPU soft-touch skin (up to 32 contacts) are driven from scripts.
- **Japanese Inspector with presets**: pick a body part (skin, face, hair, clothes, metal) to load fitting values; switch to English at any time.

Author: @matechagame

Detailed explanations of every setting are in the **[English documentation](Documentation~/en/README.md)**.

![GalaxyToon feature overview](Documentation~/Images/00_Overview.png)

## Videos

Click an image to play it on YouTube.

<a href="https://youtu.be/qjRz-8kvV6A"><img src="https://img.youtube.com/vi/qjRz-8kvV6A/maxresdefault.jpg" width="800" alt="Character in the rain"></a>

| <a href="https://youtu.be/XWpdVHJWSKs"><img src="https://img.youtube.com/vi/XWpdVHJWSKs/maxresdefault.jpg" width="395" alt="Face close-up"></a> | <a href="https://youtu.be/r_uW-JBRyRg"><img src="https://img.youtube.com/vi/r_uW-JBRyRg/maxresdefault.jpg" width="395" alt="Face close-up (different lighting)"></a> |
|---|---|
| Face close-up | Face close-up (different lighting) |

## Contents

- [Videos](#videos)
- [Requirements](#requirements)
- [Installation](#installation)
- [Usage](#usage)
- [Components](#components)
- [Menu](#menu)
- [Sample Scene](#sample-scene)
- [Features](#features)
- [Documentation](#documentation)
- [License](#license)

## Requirements

- Unity 6000.5 or later
- Universal Render Pipeline 17 or later

## Installation

Use either of the following.

- **Package Manager**: In `Window > Package Manager`, click `+` → `Install package from git URL...` and enter `https://github.com/chokomonakatabetaina/GalaxyToon.git`.
- **Copy the folder**: Copy the contents of this repository into your project, e.g. `Assets/GalaxyToon/`.

| Folder / File | Contents |
|---|---|
| `Shaders/` | Shaders `GalaxyShader/Toon` and `GalaxyShader/ToonFur`, and HLSL |
| `Runtime/` | Runtime components (assembly `GalaxyToon.Runtime`) |
| `Editor/` | Material Inspector and tools (assembly `GalaxyToon.Editor`) |
| `Materials/` | Materials for tears, strings, and slime |
| `Ramps/` | Shadow / highlight ramp templates |
| `Prefabs/Weather/` | Rain particles (`Rain`: raindrops, ripples, splashes) and the teleport spark material |
| `Samples/` | Sample scene with one sphere per feature |
| `Documentation~/` | Images and documentation (not imported by Unity) |
| `package.json` | Package Manager manifest |
| `LICENSE` | License (MIT) |

The assemblies reference only URP.

## Usage

1. Set the material's shader to `GalaxyShader/Toon`.
2. Pick a body part (skin, face, hair, clothes, metal, ...) from **Presets** in the Inspector to fill in suitable values at once.
3. Open the section of each feature you want and adjust its values. Sections whose feature is active are marked **● In use**.

- Shader keywords are switched automatically by the Inspector based on the values. If you change values from a script, call `GalaxyToon.Editor.GalaxyToonGUI.Apply(material)`.
- Switch the Inspector between 日本語 / English with the dropdown at the top left.
- `Ctrl+Shift+T` temporarily draws every material with URP Lit for comparison (materials are not changed).

Menu items and the tool windows are shown in Japanese. In this document they are written as the Japanese name followed by an English translation.

### Migrating from Other Shaders (MToon, lilToon, etc.)

For VRM and other characters, select the character in the Hierarchy, open `Tools > GalaxyToon > GalaxyToon に移行 (MToon・lilToon・Lit など)` (Migrate to GalaxyToon), and press **Migrate** to switch everything to GalaxyToon at once. This window follows the Inspector language (Japanese / English).

- Supported: MToon (VRM 0.x / 1.0), lilToon, UTS, URP Lit / Unlit, Standard
- The body part (skin, face, hair, clothes, face parts, etc.) is guessed from the material name, a preset is applied, and the original colors, shade colors, and outlines are copied.
- It can also add Galaxy Toon Face, find the eyes and cheeks, bake smooth outline normals, and set up hair shadows.
- The original materials are not changed; copies are created and assigned. Everything can be undone.

See [Editor Tools in the documentation](Documentation~/en/editor-tools.md#migrate-to-galaxytoon) for details.

### Hair Shadow

To cast bangs shadows on the face, run `Tools > GalaxyToon > 髪の影を有効にする` (Enable Hair Shadow) to add **Galaxy Hair Shadow Feature** to the URP Renderer you use (adding it manually with Add Renderer Feature works the same), and add **Galaxy Toon Face** to the character root. Use the preset **Cast Hair Shadow** on hair materials and **Receive Hair Shadow** on face / eye materials.

### Fishnet Dent

Turn on **Fishnet Dent** on a skin material and the net drawn in the base texture (dark lines, or alpha) presses into the skin while the cells bulge (shown with both normals and color, so it also reads in anime shading). You can change the dent width and depth, the shadow along the strands, and the cell bulge, and limit the area with `_NetDentMask`.

### Character Light (optional)

Place **Galaxy Toon Light** and light traveling along that object's forward direction becomes a dedicated light for characters. Without it, the scene's main light is used.

### Toon Plus (all off by default)

The "Toon Plus" section of the Inspector groups these; only the ones you turn on take effect: light color normalize and soft upper limit, shadow side tinted by the sky, receive-shadow offset, additional light tuning, rim shade and soft depth rim, MatCap perspective/roll correction with a mask, depth-only Z offset and perspective removal, global dither alpha, day/night ramp, hair highlight shape and specular anti-flicker, emission hue cycling and view color, iridescence, vertex color meanings (R shadow, G border, B outline depth, A outline width), outline wider on the lit side and fading with distance, contact shadows, starry layer, nose line in profile (paint its mask to match the face texture), shadow-only normals from UV7 (turn on "Use Shadow-Only Normals"), and a packed mask with per-material-ID colors.

### Soft Touch

Turn on "Soft Touch" and pass contact capsules through a MaterialPropertyBlock (`_SoftContactA`, `_SoftContactB`, `_SoftContactCount`, up to 32) to dent the skin in the vertex stage of every pass. The surroundings sink smoothly and bulge slightly outside; the mask's R/G/B are softness, flesh thickness and flatness. Pressed skin blushes and approaching contacts cast a soft shadow.

### Audio reactive

**Galaxy Audio React** measures the playing audio in low, mid and high bands and writes the global `_GalaxyAudio`. Pick a band per material in Toon Plus and raise the audio amounts for emission, rim and outline width.

### Angel ring modes

Besides the anisotropic ring, the angel ring can come from a MatCap (`_RingTex`, decided by the view direction) or from a band along UV2 that shifts with the view height.

### Screen effects (renderer features)

Add these from "Add Renderer Feature" on your Universal Renderer Data.

- **Galaxy Screen Line**: silhouette and inner lines (fingers, overlaps, part borders) from depth and normal differences. Lines sit outside the silhouette, so characters do not get thinner. Works together with the extruded outline.
- **Galaxy Glow**: a soft diffusion glow for bright areas, tuned separately from Bloom.
- **Galaxy Character Shadow**: a high-resolution shadow map fitted tightly around the characters, for crisp self-shadows from bangs, fingers, collars and the chin; it also gives the character-only light a shadow.
- **Galaxy Toon Volume Feature**: applies the "Galaxy Toon" Volume override (rim, shadow darken and tint, saturation, brightness, outline width) to every character, so scenes and times of day can switch the look with Volumes.
- **Galaxy Paint**: Kuwahara smoothing plus brightness steps that keep the hue. Meant for backgrounds.

### Motion Vectors

A MotionVectors pass is included for TAA, STP and motion blur.

## Components

Add them from `Add Component > GalaxyToon`.

| Component | Purpose |
|---|---|
| Galaxy Toon Light | Fixed light dedicated to characters (optional) |
| Galaxy Toon Face | Makes face shading, SDF shadows, and hair shadows follow the head direction |
| Galaxy Toon Effects | Changes the amount of wet / sweat / liquid / foam / rain / dirt / scratches / blush during gameplay |
| Galaxy Toon Hair Sway | Sways hair that has no bones |
| Galaxy Toon Fur | Grows fur (shell method). Uses the `GalaxyShader/ToonFur` material |
| Galaxy Toon See-Through Bangs | Makes the tips of the bangs semi-transparent so brows and eyes show through (in Play mode) |
| Galaxy Tears | Tears or sticky liquid run along the skin and drip off |
| Galaxy Tongue Drip | Drips liquid from the tongue tip |
| Galaxy Sticky Strings | Stretchy strings between two points that snap and reconnect |
| Galaxy Teleport | Vanish / appear / outfit swap with a teleport or warp effect |
| Galaxy Audio React | Measures the audio in low / mid / high bands and drives emission, rim, and outline width |
| Galaxy Hair Shadow Feature | URP Renderer Feature for hair shadows |
| Galaxy Character Shadow / Screen Line / Glow / Paint / Toon Volume Feature | URP Renderer Features for screen effects (see [Screen Effects, Audio, Volume](Documentation~/en/rendering.md)) |
| Galaxy Toon Sample Contact | For the samples. Shows how to feed Soft Touch, Leg Push, and Skirt Shadow |

## Menu

Found under `Tools > GalaxyToon`.

| Menu | Description |
|---|---|
| GalaxyToon に移行 (Migrate to GalaxyToon) | Converts materials from other shaders (MToon, lilToon, UTS, Lit, etc.) to GalaxyToon. For VRM characters, also sets up the face, eyes, outline, and hair shadow |
| サンプルシーンを作る (Create Sample Scene) | Rebuilds the sample scene in `Samples/` |
| 髪の影を有効にする (Enable Hair Shadow) | Adds Galaxy Hair Shadow Feature to the URP Renderers in use |
| Volume を有効にする (Enable Volume) | Adds Galaxy Toon Volume Feature to the URP Renderers in use |
| 瞳の範囲を探す / 頬の位置を探す (Find Iris Area / Find Cheek Positions) | Finds the iris / cheek UVs of the selected character and sets them on the material |
| 前髪を透過する (See-Through Bangs) | Adds Galaxy Toon See-Through Bangs to the selected character |
| 輪郭線用のなめらか法線を焼く (Bake Smooth Outline Normals) | Bakes smoothed normals so outlines don't break at hard edges |
| SDF 影マップを作る (Create SDF Shadow Map) | Creates SDF shadow maps for the face, hair, or body |
| ノーマルマップを作る (Create Normal Map) | Creates a normal map from a texture |
| ランプのひな形を作る (Create Ramp Templates) | Creates shadow / highlight ramps (4 rows, one per body part) |
| ランプを編集・作成する (Ramp Editor) | Edits shadow / highlight ramps as gradients while showing them on the material |
| 厚みマップを作る (Create Thickness Map) | Builds the SSS (skin translucency) thickness mask from the mesh shape |
| 開発用 > 全パラメータ・ライト反映フルテスト実行 / 全項目の総当たりテスト (Developer > Full Test / Property Sweep) | Checks automatically that every feature and property affects the image (for shader modifications) |
| 全マテリアルの機能を整理 (Clean Up All Materials) | Removes keywords of features that have no effect |
| 使っている機能の一覧 (Feature Usage Report) | Prints feature combinations and counts of all materials to the Console |
| 負荷を測る (Measure Performance) | Turns features off one at a time in the playing scene and measures GPU / CPU time |
| シェーダー OFF (Shader OFF) | Same as `Ctrl+Shift+T` |

## Sample Scene

Open `Samples/GalaxyToon_FeatureSamples.unity` and press Play. Use **◀ 前へ / Prev** and **次へ / Next ▶** at the bottom of the screen (or the ← → keys) to zoom in on each sample, or click a sample sphere to zoom in on it. Double-click a sample to put its material on the preview model and zoom in on the model. It starts on the large preview model at the left (the Blender monkey); ◀ ▶ next to "Material" at the bottom put any sample material on it, dragging with the mouse rotates it, and the mouse wheel zooms in. You can grab and move the small ball next to it. With the "Pressable Skin" material (the first one), pressing the ball into the model dents the skin. With "Sticky Strings", touching the model and pulling the ball away stretches strings from where it touched. The buttons on the right switch Volume (Galaxy Toon) presets (Off, Default, Sunset, Night, Pastel, Crisp) and the light (Day, Sunset, Night, Backlight, Top, Side); the slider below them sets the light brightness, and the color squares set the background color. For Volume, run `Tools > GalaxyToon > Volume を有効にする` (Enable Volume) first. **全体 / All** (Home key) returns to the overview.

Tears, strings, teleport, hair sway, fur, rain, soft touch, and leg push move in Play mode. Version 1.0.2 adds four rows (30 samples) for the new features. The "Contact Shadow" sample needs the camera depth texture. The "Hair Shadow" sphere shows its shadow after you run `Tools > GalaxyToon > 髪の影を有効にする` (Enable Hair Shadow). You can rebuild the scene at any time with `Tools > GalaxyToon > サンプルシーンを作る` (Create Sample Scene). When installed through the Package Manager, the package itself is read-only, so the scene is created in `Assets/GalaxyToon Samples/` instead.

## Features

Each image is a close-up of one sphere in the sample scene.

### Basic

| <img src="Documentation~/Images/01_Default.png" width="240" alt="Default"> | <img src="Documentation~/Images/02_Normal_Map.png" width="240" alt="Normal Map"> | <img src="Documentation~/Images/03_Color_Adjust.png" width="240" alt="Color Adjust"> | <img src="Documentation~/Images/04_Metal_Reflection.png" width="240" alt="Metal / Reflection"> |
|---|---|---|---|
| **Default** | **Normal Map** | **Color Adjust** | **Metal / Reflection** |
| <img src="Documentation~/Images/05_Glitter.png" width="240" alt="Glitter"> | <img src="Documentation~/Images/06_Refraction_Gem.png" width="240" alt="Refraction (Gem)"> | <img src="Documentation~/Images/07_Triplanar.png" width="240" alt="Triplanar"> | <img src="Documentation~/Images/08_Lace_Cutout.png" width="240" alt="Lace (Cutout)"> |
| **Glitter** | **Refraction (Gem)** | **Triplanar** | **Lace (Cutout)** |

### Shadow / Light

| <img src="Documentation~/Images/09_2-Step_Shadow.png" width="240" alt="2-Step Shadow"> | <img src="Documentation~/Images/10_3-Step_Shadow.png" width="240" alt="3-Step Shadow"> | <img src="Documentation~/Images/11_Shadow_Ramp.png" width="240" alt="Shadow Ramp"> | <img src="Documentation~/Images/12_Specular_Ramp.png" width="240" alt="Specular Ramp"> |
|---|---|---|---|
| **2-Step Shadow** | **3-Step Shadow** | **Shadow Ramp** | **Specular Ramp** |
| <img src="Documentation~/Images/13_Anime_Shading.png" width="240" alt="Anime Shading"> | <img src="Documentation~/Images/14_Flat_Face_Shading.png" width="240" alt="Flat Face Shading"> | <img src="Documentation~/Images/15_Face_SDF_Shadow.png" width="240" alt="Face SDF Shadow"> | <img src="Documentation~/Images/16_Scene_Lights.png" width="240" alt="Scene Lights"> |
| **Anime Shading** | **Flat Face Shading** | **Face SDF Shadow** | **Scene Lights** |

### Highlight / Outline

| <img src="Documentation~/Images/17_Specular.png" width="240" alt="Specular"> | <img src="Documentation~/Images/18_MatCap.png" width="240" alt="MatCap"> | <img src="Documentation~/Images/19_MatCap_2.png" width="240" alt="MatCap 2"> | <img src="Documentation~/Images/20_Anisotropic.png" width="240" alt="Anisotropic"> |
|---|---|---|---|
| **Specular** | **MatCap** | **MatCap 2** | **Anisotropic** |
| <img src="Documentation~/Images/21_Backlight.png" width="240" alt="Backlight"> | <img src="Documentation~/Images/22_Rim_Light.png" width="240" alt="Rim Light"> | <img src="Documentation~/Images/23_Depth_Rim.png" width="240" alt="Depth Rim"> | <img src="Documentation~/Images/24_Outline.png" width="240" alt="Outline"> |
| **Backlight** | **Rim Light** | **Depth Rim** | **Outline** |

### Emission / Layers

| <img src="Documentation~/Images/25_Emission_Blink.png" width="240" alt="Emission (Blink)"> | <img src="Documentation~/Images/26_Emission_Scroll.png" width="240" alt="Emission (Scroll)"> | <img src="Documentation~/Images/27_Emission_Layer_2.png" width="240" alt="Emission Layer 2"> | <img src="Documentation~/Images/28_Layer_1_Scroll.png" width="240" alt="Layer 1 (Scroll)"> |
|---|---|---|---|
| **Emission (Blink)** | **Emission (Scroll)** | **Emission Layer 2** | **Layer 1 (Scroll)** |
| <img src="Documentation~/Images/29_Layer_2_Blink.png" width="240" alt="Layer 2 (Blink)"> | <img src="Documentation~/Images/30_Layer_3_Unlit.png" width="240" alt="Layer 3 (Unlit)"> | <img src="Documentation~/Images/31_Magma.png" width="240" alt="Magma"> | <img src="Documentation~/Images/32_Emissive_Panel.png" width="240" alt="Emissive Panel"> |
| **Layer 2 (Blink)** | **Layer 3 (Unlit)** | **Magma** | **Emissive Panel** |

### Wet / Sweat / Liquid

| <img src="Documentation~/Images/33_Wet.png" width="240" alt="Wet"> | <img src="Documentation~/Images/34_Sweat.png" width="240" alt="Sweat"> | <img src="Documentation~/Images/35_Flowing_Sweat.png" width="240" alt="Flowing Sweat"> | <img src="Documentation~/Images/36_Liquid.png" width="240" alt="Liquid"> |
|---|---|---|---|
| **Wet** | **Sweat** | **Flowing Sweat** | **Liquid** |
| <img src="Documentation~/Images/37_Sticky.png" width="240" alt="Sticky"> | <img src="Documentation~/Images/38_Foam.png" width="240" alt="Foam"> | <img src="Documentation~/Images/39_Rain.png" width="240" alt="Rain"> | <img src="Documentation~/Images/40_Rain_Soak.png" width="240" alt="Rain Soak"> |
| **Sticky** | **Foam** | **Rain** | **Rain Soak** |

### Dirt / Surface

| <img src="Documentation~/Images/41_Soot.png" width="240" alt="Soot"> | <img src="Documentation~/Images/42_Mud.png" width="240" alt="Mud"> | <img src="Documentation~/Images/43_Scratches.png" width="240" alt="Scratches"> | <img src="Documentation~/Images/44_Wet_Hair.png" width="240" alt="Wet Hair"> |
|---|---|---|---|
| **Soot** | **Mud** | **Scratches** | **Wet Hair** |
| <img src="Documentation~/Images/45_Latex.png" width="240" alt="Latex"> | <img src="Documentation~/Images/46_Water.png" width="240" alt="Water"> | <img src="Documentation~/Images/47_Clear_Plastic.png" width="240" alt="Clear Plastic"> | <img src="Documentation~/Images/48_Diamond.png" width="240" alt="Diamond"> |
| **Latex** | **Water** | **Clear Plastic** | **Diamond** |

### Eyes / Face / Hair

| <img src="Documentation~/Images/49_Eyes_Crisp.png" width="240" alt="Eyes Crisp"> | <img src="Documentation~/Images/50_Eyes_Teary.png" width="240" alt="Eyes Teary"> | <img src="Documentation~/Images/51_Eyes_Sparkle.png" width="240" alt="Eyes Sparkle"> | <img src="Documentation~/Images/52_Blush.png" width="240" alt="Blush"> |
|---|---|---|---|
| **Eyes Crisp** | **Eyes Teary** | **Eyes Sparkle** | **Blush** |
| <img src="Documentation~/Images/53_GalaxyTears.png" width="240" alt="GalaxyTears"> | <img src="Documentation~/Images/54_Hair_Shadow.png" width="240" alt="Hair Shadow"> | <img src="Documentation~/Images/55_Hair_Sway.png" width="240" alt="Hair Sway"> | <img src="Documentation~/Images/56_GalaxyToonFur.png" width="240" alt="GalaxyToonFur"> |
| **GalaxyTears** | **Hair Shadow** | **Hair Sway** | **GalaxyToonFur** |

### Effects

| <img src="Documentation~/Images/57_Dissolve.png" width="240" alt="Dissolve"> | <img src="Documentation~/Images/58_GalaxyTeleport.png" width="240" alt="GalaxyTeleport"> | <img src="Documentation~/Images/59_GalaxyStickyStrings.png" width="240" alt="GalaxyStickyStrings"> | <img src="Documentation~/Images/60_GalaxyToonEffects.png" width="240" alt="GalaxyToonEffects"> |
|---|---|---|---|
| **Dissolve** | **GalaxyTeleport** | **GalaxyStickyStrings** | **GalaxyToonEffects** |
| <img src="Documentation~/Images/61_Near_Fade.png" width="240" alt="Near Fade"> | <img src="Documentation~/Images/62_Stencil.png" width="240" alt="Stencil"> | <img src="Documentation~/Images/63_Toon_vs_URP_Lit.png" width="240" alt="Toon vs URP Lit"> | <img src="Documentation~/Images/64_GalaxyTears_viscous.png" width="240" alt="GalaxyTears (viscous)"> |
| **Near Fade** | **Stencil** | **Toon vs URP Lit** | **GalaxyTears (viscous)** |

### Surface 2

| <img src="Documentation~/Images/65_SSS.png" width="240" alt="SSS"> | <img src="Documentation~/Images/66_Sheer_Stockings.png" width="240" alt="Sheer Stockings"> | <img src="Documentation~/Images/67_Fishnet_Dent.png" width="240" alt="Fishnet Dent"> | <img src="Documentation~/Images/68_Clear_Coat.png" width="240" alt="Clear Coat"> |
|---|---|---|---|
| **SSS** | **Sheer Stockings** | **Fishnet Dent** | **Clear Coat** |
| <img src="Documentation~/Images/69_Velvet.png" width="240" alt="Velvet"> | <img src="Documentation~/Images/70_Iridescence.png" width="240" alt="Iridescence"> | <img src="Documentation~/Images/71_Starry.png" width="240" alt="Starry"> | <img src="Documentation~/Images/72_Hair_Tip_Gradient.png" width="240" alt="Hair Tip Gradient"> |
| **Velvet** | **Iridescence** | **Starry** | **Hair Tip Gradient** |

### Shadow 2

| <img src="Documentation~/Images/73_Shade_Map.png" width="240" alt="Shade Map"> | <img src="Documentation~/Images/74_Shadow_Bias_Mask.png" width="240" alt="Shadow Bias Mask"> | <img src="Documentation~/Images/75_Shadow_Pattern.png" width="240" alt="Shadow Pattern"> | <img src="Documentation~/Images/76_Hatching.png" width="240" alt="Hatching"> |
|---|---|---|---|
| **Shade Map** | **Shadow Bias Mask** | **Shadow Pattern** | **Hatching** |
| <img src="Documentation~/Images/77_Halftone.png" width="240" alt="Halftone"> | <img src="Documentation~/Images/78_Fill_Light.png" width="240" alt="Fill Light"> | <img src="Documentation~/Images/79_Contact_Shadow.png" width="240" alt="Contact Shadow"> | <img src="Documentation~/Images/80_Skirt_Shadow.png" width="240" alt="Skirt Shadow"> |
| **Halftone** | **Fill Light** | **Contact Shadow** | **Skirt Shadow** |

### Eyes / Hair / Rim 2

| <img src="Documentation~/Images/81_Eyelid_Shadow_Caustic.png" width="240" alt="Eyelid Shadow / Caustic"> | <img src="Documentation~/Images/82_Dual_Angel_Ring.png" width="240" alt="Dual Angel Ring"> | <img src="Documentation~/Images/83_Angel_Ring_MatCap.png" width="240" alt="Angel Ring (MatCap)"> | <img src="Documentation~/Images/84_Rim_Toward_Light.png" width="240" alt="Rim Toward Light"> |
|---|---|---|---|
| **Eyelid Shadow / Caustic** | **Dual Angel Ring** | **Angel Ring (MatCap)** | **Rim Toward Light** |
| <img src="Documentation~/Images/85_Opposite_Rim.png" width="240" alt="Opposite Rim"> | <img src="Documentation~/Images/86_Rim_Shade.png" width="240" alt="Rim Shade"> | <img src="Documentation~/Images/87_Color_Trace_Outline.png" width="240" alt="Color Trace Outline"> | <img src="Documentation~/Images/88_Outline_Boil.png" width="240" alt="Outline Boil"> |
| **Opposite Rim** | **Rim Shade** | **Color Trace Outline** | **Outline Boil** |

### Shape / Emission 2

| <img src="Documentation~/Images/89_Inflate.png" width="240" alt="Inflate"> | <img src="Documentation~/Images/90_Soft_Touch.png" width="240" alt="Soft Touch"> | <img src="Documentation~/Images/91_Leg_Push.png" width="240" alt="Leg Push"> | <img src="Documentation~/Images/92_Dither_Alpha.png" width="240" alt="Dither Alpha"> |
|---|---|---|---|
| **Inflate** | **Soft Touch** | **Leg Push** | **Dither Alpha** |
| <img src="Documentation~/Images/93_Emission_by_View_Angle.png" width="240" alt="Emission by View Angle"> | <img src="Documentation~/Images/94_Emission_Hue_Cycle.png" width="240" alt="Emission Hue Cycle"> |   |   |
| **Emission by View Angle** | **Emission Hue Cycle** |   |   |

## Documentation

Explanations of every Inspector setting, preset, and tool are in the [English documentation](Documentation~/en/README.md).

| Page | Contents |
|---|---|
| [Inspector](Documentation~/en/inspector.md) | Render mode, presets, search, texture comparison, reset |
| [Presets](Documentation~/en/presets.md) | What every preset sets |
| [Basic, Lighting, Shadow](Documentation~/en/shader-basic.md) | Basic / Color Adjustment / Metallic, Smoothness, Reflection / Triplanar / Lighting / Shadow / Anime Shading |
| [Face, Eyes, Hair](Documentation~/en/shader-face-hair.md) | Face / Eyes / Blush / Hair Sway / Hair Shadow / Stencil |
| [Highlight, Rim, Emission](Documentation~/en/shader-highlight.md) | Highlight, MatCap / Anisotropic / Backlight / Rim / Glitter, Refraction / Emission / Layers |
| [Wet, Sweat, Liquid, Dirt](Documentation~/en/shader-surface-fx.md) | Wet / Sweat / Liquid / Foam / Rain / Dirt, Scratches |
| [Outline, Dissolve, Near Fade](Documentation~/en/shader-outline-dissolve.md) | Outline / Dissolve / Fade When Camera Is Close |
| [Skin and Cloth](Documentation~/en/shader-skin-cloth.md) | SSS / Stockings / Fishnet Dent / Clear Coat, Velvet |
| [Toon Plus](Documentation~/en/shader-toon-plus.md) | Light, shadow, rim, perspective, vertex colors, contact shadow, starry sky, nose line, packed mask, audio |
| [Shape Changes and Script Inputs](Documentation~/en/shader-shape.md) | Inflate / Leg Push / Soft Touch / Skirt Shadow / Back Light / Flatten |
| [Screen Effects, Audio, Volume](Documentation~/en/rendering.md) | Screen Line / Glow / Paint / Character Shadow / Toon Volume / Audio React |
| [Fur](Documentation~/en/shader-fur.md) | `GalaxyShader/ToonFur` |
| [Editor Tools](Documentation~/en/editor-tools.md) | Every tool in the `Tools > GalaxyToon` menu |

## License

[MIT License](LICENSE)

The sample 3D model `Samples/Models/Monkey.obj` is "Suzanne", the monkey head built into Blender (Add > Mesh > Monkey), subdivided, UV-mapped, and exported to OBJ with Blender 5.2 (not a downloaded model). Blender itself is GPL, but works created with Blender, including its built-in primitives, are not covered by the GPL and belong to their creator ([Blender License](https://www.blender.org/about/license/)). The model is distributed under the same MIT License as the package.
