# GalaxyToon

[日本語](README.md) | English

A toon shader for URP. Besides stepped shadows, rim light, MatCap, and outlines, a single shader covers eyes, blush, hair shadows, wet / sweat / liquid / rain, glitter and refraction, dissolve and teleport effects, and more.

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

### Hair Shadow

To cast bangs shadows on the face, run `Tools > GalaxyToon > 髪の影を有効にする` (Enable Hair Shadow) to add **Galaxy Hair Shadow Feature** to the URP Renderer you use (adding it manually with Add Renderer Feature works the same), and add **Galaxy Toon Face** to the character root. Use the preset **Cast Hair Shadow** on hair materials and **Receive Hair Shadow** on face / eye materials.

### Character Light (optional)

Place **Galaxy Toon Light** and light traveling along that object's forward direction becomes a dedicated light for characters. Without it, the scene's main light is used.

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
| Galaxy Hair Shadow Feature | URP Renderer Feature for hair shadows |

## Menu

Found under `Tools > GalaxyToon`.

| Menu | Description |
|---|---|
| GalaxyToon に移行 (Migrate to GalaxyToon) | Converts materials from other shaders (MToon, lilToon, UTS, Lit, etc.) to GalaxyToon. For VRM (VRoid) characters, also sets up the face, eyes, outline, and hair shadow |
| サンプルシーンを作る (Create Sample Scene) | Rebuilds the sample scene in `Samples/` |
| 髪の影を有効にする (Enable Hair Shadow) | Adds Galaxy Hair Shadow Feature to the URP Renderers in use |
| 瞳の範囲を探す / 頬の位置を探す (Find Iris Area / Find Cheek Positions) | Finds the iris / cheek UVs of the selected character and sets them on the material |
| 前髪を透過する (See-Through Bangs) | Adds Galaxy Toon See-Through Bangs to the selected character |
| 輪郭線用のなめらか法線を焼く (Bake Smooth Outline Normals) | Bakes smoothed normals so outlines don't break at hard edges |
| SDF 影マップを作る (Create SDF Shadow Map) | Creates SDF shadow maps for the face, hair, or body |
| ノーマルマップを作る (Create Normal Map) | Creates a normal map from a texture |
| ランプのひな形を作る (Create Ramp Templates) | Creates shadow / highlight ramps (4 rows, one per body part) |
| 全マテリアルの機能を整理 (Clean Up All Materials) | Removes keywords of features that have no effect |
| 使っている機能の一覧 (Feature Usage Report) | Prints feature combinations and counts of all materials to the Console |
| 負荷を測る (Measure Performance) | Turns features off one at a time in the playing scene and measures GPU / CPU time |
| シェーダー OFF (Shader OFF) | Same as `Ctrl+Shift+T` |

## Sample Scene

Open `Samples/GalaxyToon_FeatureSamples.unity` and press Play. Use **◀ 前へ / Prev** and **次へ / Next ▶** at the bottom of the screen (or the ← → keys) to zoom in on each sample. **全体 / All** (Home key) returns to the overview.

Tears, strings, teleport, hair sway, fur, and rain move in Play mode. The "Hair Shadow" sphere shows its shadow after you run `Tools > GalaxyToon > 髪の影を有効にする` (Enable Hair Shadow). You can rebuild the scene at any time with `Tools > GalaxyToon > サンプルシーンを作る` (Create Sample Scene). When installed through the Package Manager, the package itself is read-only, so the scene is created in `Assets/GalaxyToon Samples/` instead.

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
| [Fur](Documentation~/en/shader-fur.md) | `GalaxyShader/ToonFur` |
| [Editor Tools](Documentation~/en/editor-tools.md) | Every tool in the `Tools > GalaxyToon` menu |

## License

[MIT License](LICENSE)
