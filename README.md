# GalaxyToon

日本語 | [English](README.en.md)

URP 用のトゥーンシェーダーです。段影・リム・マットキャップ・輪郭線に加えて、瞳・頬染め・髪の影、濡れ・汗・液体・雨、ラメ・屈折、ディゾルブ・転送などを 1 つのシェーダーで扱えます。

Author: @matechagame

各機能の項目ごとの説明は **[日本語ドキュメント](Documentation~/ja/README.md)** にあります。

![GalaxyToon feature overview](Documentation~/Images/00_Overview.png)

## 動画

画像をクリックすると YouTube で再生されます。

<a href="https://youtu.be/qjRz-8kvV6A"><img src="https://img.youtube.com/vi/qjRz-8kvV6A/maxresdefault.jpg" width="800" alt="雨の中のキャラクター"></a>

| <a href="https://youtu.be/XWpdVHJWSKs"><img src="https://img.youtube.com/vi/XWpdVHJWSKs/maxresdefault.jpg" width="395" alt="顔のアップ"></a> | <a href="https://youtu.be/r_uW-JBRyRg"><img src="https://img.youtube.com/vi/r_uW-JBRyRg/maxresdefault.jpg" width="395" alt="顔のアップ(照明違い)"></a> |
|---|---|
| 顔のアップ | 顔のアップ(照明違い) |

## 目次

- [動画](#動画)
- [動作環境](#動作環境)
- [導入](#導入)
- [使い方](#使い方)
- [コンポーネント](#コンポーネント)
- [メニュー](#メニュー)
- [サンプルシーン](#サンプルシーン)
- [機能一覧](#機能一覧)
- [詳しいドキュメント](#詳しいドキュメント)
- [ライセンス](#ライセンス)

## 動作環境

- Unity 6000.5 以降
- Universal Render Pipeline 17 以降

## 導入

次のどちらかで入れます。

- **Package Manager から入れる**: `Window > Package Manager` の `+` → `Install package from git URL...` に、`https://github.com/chokomonakatabetaina/GalaxyToon.git` を入れます。
- **フォルダごとコピーする**: このリポジトリの中身を `Assets/GalaxyToon/` などにコピーします。

| フォルダ・ファイル | 中身 |
|---|---|
| `Shaders/` | シェーダー `GalaxyShader/Toon`・`GalaxyShader/ToonFur` と hlsl |
| `Runtime/` | 実行時コンポーネント(アセンブリ `GalaxyToon.Runtime`) |
| `Editor/` | マテリアルの Inspector とツール(アセンブリ `GalaxyToon.Editor`) |
| `Materials/` | 涙・糸・粘液用のマテリアル |
| `Ramps/` | 影・ハイライトのランプのひな形 |
| `Prefabs/Weather/` | 雨のパーティクル(`Rain`: 雨粒・波紋・しぶき)と、転送の光の粒のマテリアル |
| `Samples/` | 機能ごとの球を並べたサンプルシーン |
| `Documentation~/` | この説明の画像と、日本語・英語のドキュメント(Unity には読み込まれません) |
| `package.json` | Package Manager 用の情報 |
| `LICENSE` | ライセンス(MIT) |

アセンブリが参照するのは URP だけです。

## 使い方

1. マテリアルのシェーダーを `GalaxyShader/Toon` にします。
2. Inspector の「プリセット」から部位(肌・顔・髪・服・金属など)を選ぶと、それらしい値がまとめて入ります。
3. 使う機能の見出しを開いて値を調整します。使っている機能の見出しには「● 使用中」が付きます。

- 機能のキーワードは Inspector が値を見て自動で切り替えます。スクリプトから値を変えたときは `GalaxyToon.Editor.GalaxyToonGUI.Apply(material)` を呼んでください。
- Inspector 左上で日本語 / English を切り替えられます。
- `Ctrl+Shift+T` で、全マテリアルを URP Lit に一時的に切り替えて見比べられます(マテリアルは変わりません)。

### 髪の影

前髪の影を顔に落とすには、`Tools > GalaxyToon > 髪の影を有効にする` で使っている URP Renderer に **Galaxy Hair Shadow Feature** を追加し(Renderer の Add Renderer Feature から手で足しても同じです)、キャラのルートに **Galaxy Toon Face** を付けます。髪のマテリアルはプリセット「髪の影を落とす」、顔・瞳は「髪の影を受ける」にします。

### キャラ専用ライト(任意)

**Galaxy Toon Light** を置くと、そのオブジェクトの前方へ向かう光がキャラ専用の光源になります。置かなければシーンのメインライトで描きます。

## コンポーネント

`Add Component > GalaxyToon` から追加します。

| コンポーネント | 用途 |
|---|---|
| Galaxy Toon Light | キャラ専用の固定ライト(任意) |
| Galaxy Toon Face | 首の向きに顔の陰影・SDF 影・髪の影を合わせる |
| Galaxy Toon Effects | 濡れ・汗・液体・泡・雨・汚れ・傷・頬染めの量をゲーム中に動かす |
| Galaxy Toon Hair Sway | ボーンの無い髪を揺らす |
| Galaxy Toon Fur | 毛(シェル法)を生やす。マテリアルは `GalaxyShader/ToonFur` |
| Galaxy Toon See-Through Bangs | 前髪の毛先を半透明にして、眉・目を前髪越しに見せる(再生中) |
| Galaxy Tears | 涙・粘る液が肌を伝って流れ、垂れ落ちる |
| Galaxy Tongue Drip | 舌先から液を垂らす |
| Galaxy Sticky Strings | 2 点の間に伸びて切れる糸を引く |
| Galaxy Teleport | 転送・ワープで消える / 現れる / 服を入れ替える |
| Galaxy Hair Shadow Feature | 髪の影を描く URP Renderer Feature |

## メニュー

`Tools > GalaxyToon` にあります。

| メニュー | 内容 |
|---|---|
| サンプルシーンを作る (機能ごとの球) | `Samples/` のサンプルシーンを作り直す |
| 髪の影を有効にする | 使っている URP Renderer に Galaxy Hair Shadow Feature を足す |
| 瞳の範囲を探す / 頬の位置を探す | 選択中のキャラから瞳・頬の UV を探してマテリアルに入れる |
| 前髪を透過する | 選択中のキャラに Galaxy Toon See-Through Bangs を付ける |
| 輪郭線用のなめらか法線を焼く | 輪郭線が割れないように、なめらかな法線を焼く |
| SDF 影マップを作る | 顔・髪・体の SDF 影マップを作る |
| ノーマルマップを作る | テクスチャからノーマルマップを作る |
| ランプのひな形を作る | 影・ハイライトのランプ(部位 4 段)を作る |
| 全マテリアルの機能を整理 | 効いていない機能のキーワードを外す |
| 使っている機能の一覧 | 全マテリアルの機能の組み合わせと使用数を Console に出す |
| 負荷を測る | 再生中のシーンで機能を 1 つずつ切って、GPU / CPU の時間を測る |
| シェーダー OFF (Lit で見比べ) | `Ctrl+Shift+T` と同じ |

## サンプルシーン

`Samples/GalaxyToon_FeatureSamples.unity` を開いて再生します。画面下の「◀ 前へ / 次へ ▶」(または ← → キー)で 1 つずつ寄って見られます。「全体」(Home キー)で全体に戻ります。

涙・糸・転送・髪の揺れ・毛・雨は再生中に動きます。「髪の影」の球は、先に `Tools > GalaxyToon > 髪の影を有効にする` を実行しておくと影が出ます。シーンは `Tools > GalaxyToon > サンプルシーンを作る` でいつでも作り直せます。Package Manager から入れた場合はパッケージの中に書き込めないので、`Assets/GalaxyToon Samples/` に作ります。

## 機能一覧

各画像はサンプルシーンの球を 1 つずつ寄って撮ったものです。

### 基本 — Basic

| <img src="Documentation~/Images/01_Default.png" width="240" alt="Default"> | <img src="Documentation~/Images/02_Normal_Map.png" width="240" alt="Normal Map"> | <img src="Documentation~/Images/03_Color_Adjust.png" width="240" alt="Color Adjust"> | <img src="Documentation~/Images/04_Metal_Reflection.png" width="240" alt="Metal / Reflection"> |
|---|---|---|---|
| **初期値**<br>Default | **ノーマルマップ**<br>Normal Map | **色調補正**<br>Color Adjust | **金属・映り込み**<br>Metal / Reflection |
| <img src="Documentation~/Images/05_Glitter.png" width="240" alt="Glitter"> | <img src="Documentation~/Images/06_Refraction_Gem.png" width="240" alt="Refraction (Gem)"> | <img src="Documentation~/Images/07_Triplanar.png" width="240" alt="Triplanar"> | <img src="Documentation~/Images/08_Lace_Cutout.png" width="240" alt="Lace (Cutout)"> |
| **ラメ**<br>Glitter | **屈折 (宝石)**<br>Refraction (Gem) | **トライプラナー**<br>Triplanar | **レース (切り抜き)**<br>Lace (Cutout) |

### 影・ライト — Shadow / Light

| <img src="Documentation~/Images/09_2-Step_Shadow.png" width="240" alt="2-Step Shadow"> | <img src="Documentation~/Images/10_3-Step_Shadow.png" width="240" alt="3-Step Shadow"> | <img src="Documentation~/Images/11_Shadow_Ramp.png" width="240" alt="Shadow Ramp"> | <img src="Documentation~/Images/12_Specular_Ramp.png" width="240" alt="Specular Ramp"> |
|---|---|---|---|
| **影 2段**<br>2-Step Shadow | **影 3段**<br>3-Step Shadow | **影のランプ**<br>Shadow Ramp | **ハイライトのランプ**<br>Specular Ramp |
| <img src="Documentation~/Images/13_Anime_Shading.png" width="240" alt="Anime Shading"> | <img src="Documentation~/Images/14_Flat_Face_Shading.png" width="240" alt="Flat Face Shading"> | <img src="Documentation~/Images/15_Face_SDF_Shadow.png" width="240" alt="Face SDF Shadow"> | <img src="Documentation~/Images/16_Scene_Lights.png" width="240" alt="Scene Lights"> |
| **アニメ塗り**<br>Anime Shading | **顔の陰影を平らに**<br>Flat Face Shading | **SDF 顔影**<br>Face SDF Shadow | **シーンのライトを受ける**<br>Scene Lights |

### ハイライト・輪郭 — Highlight / Outline

| <img src="Documentation~/Images/17_Specular.png" width="240" alt="Specular"> | <img src="Documentation~/Images/18_MatCap.png" width="240" alt="MatCap"> | <img src="Documentation~/Images/19_MatCap_2.png" width="240" alt="MatCap 2"> | <img src="Documentation~/Images/20_Anisotropic.png" width="240" alt="Anisotropic"> |
|---|---|---|---|
| **ハイライト**<br>Specular | **マットキャップ**<br>MatCap | **マットキャップ2**<br>MatCap 2 | **異方性 (天使の輪)**<br>Anisotropic |
| <img src="Documentation~/Images/21_Backlight.png" width="240" alt="Backlight"> | <img src="Documentation~/Images/22_Rim_Light.png" width="240" alt="Rim Light"> | <img src="Documentation~/Images/23_Depth_Rim.png" width="240" alt="Depth Rim"> | <img src="Documentation~/Images/24_Outline.png" width="240" alt="Outline"> |
| **逆光**<br>Backlight | **リムライト**<br>Rim Light | **深度リム**<br>Depth Rim | **輪郭線**<br>Outline |

### 発光・レイヤー — Emission / Layers

| <img src="Documentation~/Images/25_Emission_Blink.png" width="240" alt="Emission (Blink)"> | <img src="Documentation~/Images/26_Emission_Scroll.png" width="240" alt="Emission (Scroll)"> | <img src="Documentation~/Images/27_Emission_Layer_2.png" width="240" alt="Emission Layer 2"> | <img src="Documentation~/Images/28_Layer_1_Scroll.png" width="240" alt="Layer 1 (Scroll)"> |
|---|---|---|---|
| **発光 (明滅)**<br>Emission (Blink) | **発光 (流れる)**<br>Emission (Scroll) | **発光レイヤー2**<br>Emission Layer 2 | **レイヤー1 (流れる)**<br>Layer 1 (Scroll) |
| <img src="Documentation~/Images/29_Layer_2_Blink.png" width="240" alt="Layer 2 (Blink)"> | <img src="Documentation~/Images/30_Layer_3_Unlit.png" width="240" alt="Layer 3 (Unlit)"> | <img src="Documentation~/Images/31_Magma.png" width="240" alt="Magma"> | <img src="Documentation~/Images/32_Emissive_Panel.png" width="240" alt="Emissive Panel"> |
| **レイヤー2 (明滅)**<br>Layer 2 (Blink) | **レイヤー3 (光る)**<br>Layer 3 (Unlit) | **マグマ**<br>Magma | **発光パネル**<br>Emissive Panel |

### 濡れ・汗・液体 — Wet / Sweat / Liquid

| <img src="Documentation~/Images/33_Wet.png" width="240" alt="Wet"> | <img src="Documentation~/Images/34_Sweat.png" width="240" alt="Sweat"> | <img src="Documentation~/Images/35_Flowing_Sweat.png" width="240" alt="Flowing Sweat"> | <img src="Documentation~/Images/36_Liquid.png" width="240" alt="Liquid"> |
|---|---|---|---|
| **濡れ**<br>Wet | **汗**<br>Sweat | **流れる汗**<br>Flowing Sweat | **液体**<br>Liquid |
| <img src="Documentation~/Images/37_Sticky.png" width="240" alt="Sticky"> | <img src="Documentation~/Images/38_Foam.png" width="240" alt="Foam"> | <img src="Documentation~/Images/39_Rain.png" width="240" alt="Rain"> | <img src="Documentation~/Images/40_Rain_Soak.png" width="240" alt="Rain Soak"> |
| **ネバネバ**<br>Sticky | **泡**<br>Foam | **雨**<br>Rain | **雨 染み込み**<br>Rain Soak |

### 汚れ・質感 — Dirt / Surface

| <img src="Documentation~/Images/41_Soot.png" width="240" alt="Soot"> | <img src="Documentation~/Images/42_Mud.png" width="240" alt="Mud"> | <img src="Documentation~/Images/43_Scratches.png" width="240" alt="Scratches"> | <img src="Documentation~/Images/44_Wet_Hair.png" width="240" alt="Wet Hair"> |
|---|---|---|---|
| **汚れ・すす**<br>Soot | **泥汚れ**<br>Mud | **傷**<br>Scratches | **髪 濡れ**<br>Wet Hair |
| <img src="Documentation~/Images/45_Latex.png" width="240" alt="Latex"> | <img src="Documentation~/Images/46_Water.png" width="240" alt="Water"> | <img src="Documentation~/Images/47_Clear_Plastic.png" width="240" alt="Clear Plastic"> | <img src="Documentation~/Images/48_Diamond.png" width="240" alt="Diamond"> |
| **服 ラテックス**<br>Latex | **水**<br>Water | **ペットボトル**<br>Clear Plastic | **ダイヤモンド**<br>Diamond |

### 瞳・顔・髪 — Eyes / Face / Hair

| <img src="Documentation~/Images/49_Eyes_Crisp.png" width="240" alt="Eyes Crisp"> | <img src="Documentation~/Images/50_Eyes_Teary.png" width="240" alt="Eyes Teary"> | <img src="Documentation~/Images/51_Eyes_Sparkle.png" width="240" alt="Eyes Sparkle"> | <img src="Documentation~/Images/52_Blush.png" width="240" alt="Blush"> |
|---|---|---|---|
| **瞳 くっきり**<br>Eyes Crisp | **瞳 うるうる**<br>Eyes Teary | **瞳 きらめき**<br>Eyes Sparkle | **照れ顔**<br>Blush |
| <img src="Documentation~/Images/53_GalaxyTears.png" width="240" alt="GalaxyTears"> | <img src="Documentation~/Images/54_Hair_Shadow.png" width="240" alt="Hair Shadow"> | <img src="Documentation~/Images/55_Hair_Sway.png" width="240" alt="Hair Sway"> | <img src="Documentation~/Images/56_GalaxyToonFur.png" width="240" alt="GalaxyToonFur"> |
| **涙**<br>GalaxyTears | **髪の影**<br>Hair Shadow | **髪の揺れ**<br>Hair Sway | **毛**<br>GalaxyToonFur |

### 演出 — Effects

| <img src="Documentation~/Images/57_Dissolve.png" width="240" alt="Dissolve"> | <img src="Documentation~/Images/58_GalaxyTeleport.png" width="240" alt="GalaxyTeleport"> | <img src="Documentation~/Images/59_GalaxyStickyStrings.png" width="240" alt="GalaxyStickyStrings"> | <img src="Documentation~/Images/60_GalaxyToonEffects.png" width="240" alt="GalaxyToonEffects"> |
|---|---|---|---|
| **ディゾルブ**<br>Dissolve | **転送**<br>GalaxyTeleport | **糸**<br>GalaxyStickyStrings | **雨の量を動かす**<br>GalaxyToonEffects |
| <img src="Documentation~/Images/61_Near_Fade.png" width="240" alt="Near Fade"> | <img src="Documentation~/Images/62_Stencil.png" width="240" alt="Stencil"> | <img src="Documentation~/Images/63_Toon_vs_URP_Lit.png" width="240" alt="Toon vs URP Lit"> | <img src="Documentation~/Images/64_GalaxyTears_viscous.png" width="240" alt="GalaxyTears (viscous)"> |
| **近づくと透ける**<br>Near Fade | **ステンシル**<br>Stencil | **左 Toon / 右 Lit**<br>Toon vs URP Lit | **白い液 どろどろ**<br>GalaxyTears (viscous) |


## 詳しいドキュメント

Inspector の各項目・プリセット・ツールの説明は [日本語ドキュメント](Documentation~/ja/README.md) にあります。

| ページ | 内容 |
|---|---|
| [Inspector の使い方](Documentation~/ja/inspector.md) | 描画モード、プリセット、検索、テクスチャの見比べ、初期化 |
| [プリセット一覧](Documentation~/ja/presets.md) | 全プリセットの内容 |
| [基本・ライト・影](Documentation~/ja/shader-basic.md) | 基本 / 色調補正 / 金属・つや・映り込み / トライプラナー / ライト / 影 / アニメ塗り |
| [顔・瞳・髪](Documentation~/ja/shader-face-hair.md) | 顔 / 瞳 / 頬染め / 髪の揺れ / 髪の影 / ステンシル |
| [ハイライト・リム・発光](Documentation~/ja/shader-highlight.md) | ハイライト・マットキャップ / 異方性 / 逆光 / リム / ラメ・屈折 / 発光 / レイヤー |
| [濡れ・汗・液体・汚れ](Documentation~/ja/shader-surface-fx.md) | 濡れ / 汗 / 液体 / 泡 / 雨 / 汚れ・傷 |
| [輪郭線・ディゾルブ・透け](Documentation~/ja/shader-outline-dissolve.md) | 輪郭線 / ディゾルブ / カメラが近いと透ける |
| [ファー](Documentation~/ja/shader-fur.md) | `GalaxyShader/ToonFur` |
| [エディタツール](Documentation~/ja/editor-tools.md) | `Tools > GalaxyToon` メニューの全ツール |

## ライセンス

[MIT License](LICENSE)
