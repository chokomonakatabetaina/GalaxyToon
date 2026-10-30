# Editor Tools

[Documentation index](README.md)

The tools in the **Tools > GalaxyToon** menu. They all run only in the Editor and are not included in builds. Menu items and tool windows are shown in Japanese, so the Japanese label is given with each English name.

| Tool | Menu item | Purpose | File |
|---|---|---|---|
| [Migrate to GalaxyToon](#migrate-to-galaxytoon) | GalaxyToon に移行 | Converts MToon, lilToon, UTS, Lit, and other materials | `GalaxyToonMigrator.cs` |
| [Find Iris Area](#find-iris-area) | 瞳の範囲を探す | Sets the iris UV areas on the face material | `GalaxyToonEyeFinder.cs` |
| [Find Cheek Positions](#find-cheek-positions) | 頬の位置を探す | Sets the cheek UV positions on the face material | `GalaxyToonEyeFinder.cs` |
| [Create Normal Map](#create-normal-map) | ノーマルマップを作る | Creates a normal map from a texture | `GalaxyToonNormalBaker.cs` |
| [Bake Smooth Outline Normals](#bake-smooth-outline-normals) | 輪郭線用のなめらか法線を焼く | Keeps outlines from breaking at corners | `GalaxyToonOutlineNormalBaker.cs` |
| [Create SDF Shadow Map](#create-sdf-shadow-map) | SDF 影マップを作る | Creates SDF shadow maps for the face, hair, or body | `GalaxyToonSdfBaker.cs` |
| [Create Ramp Templates](#create-ramp-templates) | ランプのひな形を作る | Creates shadow / highlight ramp images | `GalaxyToonRampTemplate.cs` |
| [See-Through Bangs](#see-through-bangs) | 前髪を透過する | Adds the see-through bangs component to a character | `GalaxyToonSeeThroughBangsMenu.cs` |
| [Enable Hair Shadow](#enable-hair-shadow) | 髪の影を有効にする | Adds the hair shadow feature to the URP Renderers in use | `GalaxyToonTools.cs` |
| [Clean Up All Materials](#clean-up-all-materials) | 全マテリアルの機能を整理 | Removes keywords of unused features | `GalaxyToonTools.cs` |
| [Feature Usage Report](#feature-usage-report) | 使っている機能の一覧 | Prints feature combinations to the Console | `GalaxyToonTools.cs` |
| [Shader OFF](#shader-off) | シェーダー OFF | Compares with URP Lit (Ctrl+Shift+T) | `GalaxyToonTools.cs` |
| [Measure Performance](#measure-performance) | 負荷を測る | Measures frame time per feature | `GalaxyToonPerfSweep.cs` |
| Create Sample Scene | サンプルシーンを作る | Rebuilds the sample scene (see the [README](../../README.en.md#sample-scene)) | `GalaxyToonSampleSceneCreator.cs` |

For the Inspector (`GalaxyToonGUI.cs` / `GalaxyToonText.cs`), see [Inspector](inspector.md).

---

## Migrate to GalaxyToon

Converts materials from other shaders to `GalaxyShader/Toon` in one go. For a VRM (VRoid) character, select it and run the tool to also set up the face, eyes, outline, and hair shadow.

**How to use**

1. Select a character in the Hierarchy (or materials in the Project window).
2. Open **Tools > GalaxyToon > GalaxyToon に移行 (MToon・lilToon・Lit など)**. The window follows the Inspector language (Japanese / English).
3. Check the materials and body parts in the list. The body part is guessed from the material name; change it if it is wrong.
4. Press **Migrate**. The results are printed to the Console.

**Supported shaders**

| Source shader | What is copied |
|---|---|
| MToon (VRM 0.x / VRM 1.0) | Color, texture, shade color, shading border and blur, normal map, emission, matcap, rim, outline (width, color, mask), render mode, double-sided |
| lilToon | Color, texture, shadows 1–3 (color, border, blur, mask), rim, matcap, emission, outline, render mode, cull |
| UTS (Unity-Chan Toon Shader) | Color, texture, 1st / 2nd shade (color, step, feather), normal map, rim, matcap, emission, outline, render mode |
| URP Lit / Simple Lit / Unlit, Standard | Color, texture, normal map, AO, metallic, smoothness, emission, render mode, cull |
| Others | Color, texture, normal map, and emission only |

**How it works**

- A body-part preset (skin, face, hair, clothes, metal, face parts) is applied first, then the original values are copied on top, so the original shade colors and outlines win.
- MToon and UTS store the shadow color itself, so it is divided by the base color to get GalaxyToon's "color multiplied onto the base".
- Tiny placeholder textures (the 8×8 matcap and flat normal map that VRoid assigns) and normal maps with a strength of 0 are not copied, so unused features add no cost.
- Part guessing also understands VRoid names (`Face_00_SKIN`, `HairBack_00_HAIR`, `EyeWhite_00_EYE`, `Tops_01_CLOTH`, etc.).

**Settings**

| Setting | Description |
|---|---|
| Overwrite Original Materials | OFF (recommended): creates copies and assigns them to the renderers; the originals are kept<br>ON: changes the shader of the originals. Read-only materials (imported from VRM, etc.) are copied instead |
| Output Folder | Where copies are saved. Existing materials with the same name are updated in place (GUIDs are kept, so references don't break) |
| Add Galaxy Toon Face | Adds `GalaxyToonFace` to the character root |
| Find Eyes and Cheeks | Same as [Find Iris Area](#find-iris-area) and [Find Cheek Positions](#find-cheek-positions) |
| Bake Smooth Outline Normals | Same as [Bake Smooth Outline Normals](#bake-smooth-outline-normals). Baked meshes go into `OutlineNormals/` inside the output folder |
| Set Up Hair Shadow | Applies "Cast Hair Shadow" to hair and "Receive Hair Shadow" to the face and irises, and runs [Enable Hair Shadow](#enable-hair-shadow) |

- Everything can be undone with Undo (Ctrl+Z).
- Anything that cannot be copied (an MToon shade texture that differs from the main texture, screen-space outline width, etc.) is noted in the Console.
- GalaxyToon materials cannot be exported in a VRM file (the VRM spec only allows MToon and a few others). Migration is for characters used inside Unity.

## Find Iris Area

Finds the irises on the face mesh automatically and sets **Iris 1 Area** and **Iris 2 Area** on the face material. **Use Eye Effects** is turned ON too.

**How to use**

1. Select the character (or the face GameObject) in the Hierarchy.
2. Run **Tools > GalaxyToon > 瞳の範囲を探す (選択中のキャラ)** (Find Iris Area, selected character).
3. The result is printed to the Console.

**How it searches**

- A "mesh island" that fits entirely inside the area of the sclera submesh is treated as an iris.
- Thin islands such as eyelashes are excluded, and the largest island seen from the front is chosen.
- It also works when the iris is a separate submesh (such as an iris-only mesh).

**Requirements**

- The face (skin) and the sclera are separate submeshes of the same SkinnedMeshRenderer.
- The sclera material name contains `Sclera`, `EyeWhite`, `白目`, or `Eye`.
- If an iris material name contains `Iris`, `Pupil`, or `瞳`, that submesh is preferred.

## Find Cheek Positions

Treats front-facing skin vertices slightly below and outside the sclera as cheeks, and sets **Cheek Positions** and **Blush Size** on the face material.

**How to use**

1. Select the character.
2. Run **Tools > GalaxyToon > 頬の位置を探す (選択中のキャラ)** (Find Cheek Positions, selected character).

**Requirements**

- The sclera (`EyeWhite` / `Sclera`) and the skin (`Skin` / `Face`) are separate submeshes of the same SkinnedMeshRenderer.

## Create Normal Map

Treats texture brightness as height and creates a normal map. Works with textures that have Read/Write OFF or are compressed.

**How to use**

1. Open the window with **Tools > GalaxyToon > ノーマルマップを作る (テクスチャから)** (Create Normal Map, from texture).
2. Select a renderer in the Hierarchy to fill in its main texture automatically, or assign a texture directly.
3. Adjust the settings and create.

**Settings**

| Setting | Description |
|---|---|
| Target Renderer (対象の Renderer) | Optional. Fills in the material and main texture when selected |
| Material (マテリアル) | Which material slot of the renderer to use |
| Source Texture (元のテクスチャ) | Bright areas are high, dark areas are low |
| Strength (強さ) | Depth of the bumps |
| Smoothness, px (なめらかさ) | Blurs first to remove noise |
| Fine Detail Only (細かい模様だけ) | Removes large light/dark areas such as shading and color blocks and picks up only fine bumps such as weave, hair strands, and wrinkles. Usually ON for characters |
| Large Area Size, px (大きな明暗とみなす大きさ) | Size of the light/dark areas removed by Fine Detail Only |
| Invert (反転) | Makes dark areas high |
| Max Size (最大サイズ) | Maximum size of the created texture (256–4096) |
| Save Path (保存先) | PNG save path |
| Apply to Material (マテリアルに反映) | Assigns the result to the material's **Normal Map** |
| Normal Strength on Apply (反映時の立体感の強さ) | **Normal Strength** set when applying |

## Bake Smooth Outline Normals

Averages the normals of vertices at the same position and bakes these smoothed normals into UV8 (TEXCOORD7) of the mesh. Outlines are pushed out along them, so lines no longer break or gap at corners.

**How to use**

1. Select prefabs (Project window) or objects in the scene. Multiple selection works.
2. Run **Tools > GalaxyToon > 輪郭線用のなめらか法線を焼く (選択中のプレハブ・オブジェクト)** (Bake Smooth Outline Normals, selected prefabs / objects).

**Behavior**

- Only meshes of renderers using `GalaxyShader/Toon` are baked.
- The original mesh (FBX) is not modified. A copy is saved in an `OutlineNormals/` folder (next to the prefab; `Assets/OutlineNormals/` for scene objects) and swapped into the renderer.
- The vertex order is unchanged, so blend shapes and features that use vertex indices keep working.
- Running it again re-bakes into the same files.

## Create SDF Shadow Map

Creates an SDF shadow map from the mesh shape. As the light moves from the front to the side, the angle at which each pixel enters shadow is written to R (light from the right) and G (light from the left).

**How to use**

1. Open the window with **Tools > GalaxyToon > SDF 影マップを作る** (Create SDF Shadow Map).
2. Select the face renderer in the Hierarchy.
3. Choose the kind and press **作成** (Create).

**Settings**

| Setting | Description |
|---|---|
| Target Renderer (対象の Renderer) | SkinnedMeshRenderer or MeshRenderer. Skinned meshes are baked in the bind pose |
| Material / Submesh (マテリアル(サブメッシュ)) | Bakes only the part used by this material |
| Kind (種類) | **顔 (Face)**: pulled strongly toward a head ellipsoid; removes nose and mouth shadows<br>**体 (Body)**: the smoothed body shape as is<br>**髪 (Hair)**: pulled toward a head sphere; removes small shadows per hair strand |
| Resolution (解像度) | 256–2048 |
| Character Front (キャラの正面) | The direction the face is looking. **自動 (Auto)** uses the front (+Z) of the topmost parent and also corrects FBX axis differences |
| Smoothness (なめらかさ) | How strongly normals are smoothed. Higher values give larger, simpler shadow shapes |
| Pull to Head Shape (頭の形へ寄せる) | For face and hair, how strongly the shape is pulled toward a head ellipsoid. 1 = a pure ellipsoid shadow |
| Save Path (保存先) | PNG save path |
| Apply to Material (マテリアルに反映) | Assigns the result to the material's **SDF Face Shadow** and turns it ON |

> **Body** assumes a T-pose bake. On bending parts such as arms and legs the shadow becomes jagged at the joints; use it only on rigid meshes such as the torso.

## Create Ramp Templates

Creates two ramp image templates at once, for shadows and highlights. Each has four gradient rows, one per body part (from the top: skin / hair / clothes / metal).

**How to use**

1. Select the destination folder in the Project window (`Assets/` if none is selected).
2. Run **Tools > GalaxyToon > ランプのひな形を作る (影・ハイライト、部位4段)** (Create Ramp Templates).
3. Assign the images to **Shadow Ramp** and **Highlight Ramp** on the material.
4. Set **Ramp Rows** on the material to **4**.
5. Choose the row with **Ramp Row** or a **Part ID Map**.

**Created images**

| Image | Contents |
|---|---|
| `GalaxyToon_ShadowRamp_4Rows.png` | Shadow. Left = darkest, right = lit |
| `GalaxyToon_SpecularRamp_4Rows.png` | Highlight. Left = outer edge, right = center. Black = no highlight |

They are imported without mipmaps or compression so the rows don't bleed into each other. Repaint them in an image editor.

## See-Through Bangs

Adds **GalaxyToonSeeThroughBangs** to the root of the selected character. In Play mode, the tips of the bangs become semi-transparent with a gradient, so brows and eyes show through.

**How to use**

1. Select the character (a child object is fine) in the Hierarchy.
2. Run **Tools > GalaxyToon > 前髪を透過する (選択中のキャラ)** (See-Through Bangs, selected character).
3. Adjust the transparent range on the added component.

**Requirements**

- The character has a Humanoid Animator.
- The hair material uses `GalaxyShader/Toon` and its name contains `hair`.

## Enable Hair Shadow

Adds **GalaxyHairShadowFeature** to the URP Renderers used by the project (the renderers of the Graphics default pipeline asset and of each quality level's pipeline asset). Renderers that already have it are left unchanged. The result is printed to the Console.

Hair shadows also need **GalaxyToonFace** on the character root and the hair / face presets ([Face, Eyes, Hair](shader-face-hair.md#hair-shadow)).

## Clean Up All Materials

Re-sets the keywords (feature ON/OFF) of every `GalaxyShader/Toon` material in the project to match its values.

- Keywords of features with no effect are removed, so less is computed.
- Fewer shader combinations go into the build, so builds are smaller and load faster.
- Run it after changing material values from a script or importing old materials.
- When done, the [Feature Usage Report](#feature-usage-report) is printed to the Console too.

## Feature Usage Report

Scans every `GalaxyShader/Toon` material in the project and prints the following to the Console.

- The number of feature combinations (= the number of shader combinations in the build)
- The number of materials and example materials for each combination
- How many materials use each feature

If there are too many combinations, aligning the settings of similar materials makes the build smaller.

## Shader OFF

Draws every GalaxyToon material with the same textures and the same lighting as the standard URP Lit. Outlines are hidden too. Use it to compare against the toon look.

- The shortcut is **Ctrl+Shift+T**. Press it again to switch back.
- Material values are not changed.
- You can also toggle it with the button at the top of the Inspector.

## Measure Performance

Measures GPU and CPU frame time in the playing scene while turning features off one at a time.

**How to use**

1. Play the scene.
2. Run **Tools > GalaxyToon > 負荷を測る (再生中のシーン)** (Measure Performance, playing scene).
3. When finished, the result is printed to the Console. Everything that was turned off is restored.

**Stages measured**

| Stage | Contents |
|---|---|
| Everything on (baseline) | Nothing turned off |
| No depth of field / No bloom | Post effects turned off |
| No (Renderer Feature name) | Each URP Renderer Feature turned off in turn |
| No outlines | GalaxyToon outlines turned off |
| No light shadows | Light shadows turned off |
| No characters | Characters with `GalaxyToonFace` hidden |
| Render scale 0.5 | URP render scale halved |

Each stage warms up for 1 second and measures for 3 seconds. SetPass calls, draw calls, and triangle counts are recorded too.

> The post-effect stages are measured only when depth of field / bloom are enabled in the scene's Volume.
