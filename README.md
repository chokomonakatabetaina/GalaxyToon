# GalaxyToon

日本語 | [English](README.en.md)

**アニメの顔から、雨に濡れた髪、触れるとへこむ肌まで。1 本のシェーダーで。**

URP 用のトゥーンシェーダーです。段影・リム・マットキャップ・輪郭線に加えて、瞳・頬染め・髪の影、濡れ・汗・液体・雨、ラメ・屈折、ディゾルブ・転送、触れるとへこむ肌などを 1 つのシェーダーで扱えます。

### GalaxyToon のここが違う

- **キャラ 1 体ぶんが 1 本で済む**：顔（SDF 影・首の向きへの追従・鼻線）、瞳（奥行き・うるうる・きらめき）、髪（天使の輪 2 本・前髪の影・毛先の色）、肌（血色のにじみ・逆光の透け）、服（ラテックス・ストッキング・金属）まで、シェーダーを替えずに作れる。
- **使わない機能は計算もビルドもしない**：機能ごとのキーワードで切り替える。使っていない機能は、組み合わせごと消える。負荷を 1 つずつ測るツールも付いている。
- **ほかのシェーダーからそのまま移れる**：MToon（VRM）・lilToon・UTS・URP Lit のキャラを、顔・瞳・輪郭線・髪の影までまとめて移す。
- **ゲームの中で動く表現**：濡れ・汗・雨・涙・糸・転送・触れるとへこむ肌（GPU で 32 か所まで）を、スクリプトから量を変えて動かせる。
- **日本語の Inspector とプリセット**：部位（肌・顔・髪・服・金属）を選ぶだけで、それらしい値がまとめて入る。英語にも切り替えられる。

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

### ほかのシェーダーから移る(MToon・lilToon など)

VRM などのキャラは、ヒエラルキーでキャラを選び、`Tools > GalaxyToon > GalaxyToon に移行 (MToon・lilToon・Lit など)` を開いて **移行する** を押すと、まとめて GalaxyToon に切り替わります。

- 対応: MToon(VRM 0.x / 1.0)・lilToon・UTS・URP Lit / Unlit・Standard
- 部位(肌・顔・髪・服・顔パーツなど)をマテリアル名から推測してプリセットを当て、元の色・影色・輪郭線を写します。
- Galaxy Toon Face・瞳と頬の位置・輪郭線のなめらか法線・髪の影まで、まとめて設定できます。
- 元のマテリアルは書き換えず、複製を作って差し替えます。Undo で戻せます。

詳しくは [説明書のエディタツール](Documentation~/ja/editor-tools.md#galaxytoon-に移行) を見てください。

### 髪の影

前髪の影を顔に落とすには、`Tools > GalaxyToon > 髪の影を有効にする` で使っている URP Renderer に **Galaxy Hair Shadow Feature** を追加し(Renderer の Add Renderer Feature から手で足しても同じです)、キャラのルートに **Galaxy Toon Face** を付けます。髪のマテリアルはプリセット「髪の影を落とす」、顔・瞳は「髪の影を受ける」にします。

### 網タイツの食い込み

肌のマテリアルの「網タイツの食い込み」をオンにすると、地のテクスチャに描かれた網(暗い線、またはアルファ)で肌が押され、網目の間がふくらんで見えます(法線と色の両方で表すので、アニメ塗りでも見えます)。食い込みの幅・深さ・網の際の影・網目のふくらみを変えられ、`_NetDentMask` で出す範囲を絞れます。

### キャラ専用ライト(任意)

**Galaxy Toon Light** を置くと、そのオブジェクトの前方へ向かう光がキャラ専用の光源になります。置かなければシーンのメインライトで描きます。
「対象」にキャラを入れると、そのキャラだけを照らします(キャラごとに光の向き・色を変えられます)。「夜の量」は、昼／夜のランプを混ぜる量です。

### トゥーンの強化(既定はすべてオフ)

Inspector の「トゥーンの強化」にまとめています。オンにしたものだけ効きます。

| 項目 | 内容 |
|---|---|
| 光の色の正規化・明るさの上限をなめらかに | 暗い色のライトでも塗りが暗くならない。明るすぎる所は急に頭打ちにしない |
| 影の側を空の色に寄せる | 環境光(SH)の色味を影の所だけに掛ける。夕焼け・青空で影が空の色になる |
| 落ち影を受ける位置のずらし | 顔・体の自己影の汚れを減らす |
| 追加ライトの調整 | 面の向きを見ずに距離だけで明るく、専用の境界・上限・灰色化 |
| リムシェード・やわらかい深度リム | 輪郭の内側を掛け算で締める。深度リムを 3 か所で読んでなめらかに |
| マットキャップの補正 | 画面の端の歪み・カメラの傾きを直す。マスク付き |
| 深度だけ手前へ・遠近の除去 | 眉・目のふちを髪の手前に。広角で寄っても顔がゆがまない(頭の中心は Galaxy Toon Face が入れる) |
| 全体のディザ透明度 | 出入り・遮蔽物の向こうで、全パス同じ網目で抜く |
| 昼／夜のランプ | ランプの上半分を昼、下半分を夜として混ぜる |
| 髪のハイライトの形・チラつきを抑える | 正面は丸く、縁は細長く。遠い所の点滅を抑える |
| 発光の色の移り変わり・見る角度の色 | 時間で色相を回す。縁ほど別の色 |
| 虹色の薄膜 | 見る角度で色相が回る(シャボン・油膜・玉虫) |
| 頂点カラーの意味 | R＝影の出やすさ、G＝境界のずらし、B＝輪郭線を奥へ、A＝輪郭線の太さ |
| 輪郭線を光の側で太く・遠いと消す | 光の当たる輪郭を強く。遠くのキャラの線がつぶれない |
| 接地影 | 画面の深度を光の向きへたどって、指・襟の接点に細い影 |
| 星空 | 画面に貼りつく星を 3 層、視差と瞬き、星雲の色(マスクの所だけ) |
| 鼻線 | 横顔のときだけ鼻筋に線(マスクは顔テクスチャに合わせて描く) |
| 影専用の法線 | UV7 に入れた法線を光の当たり具合にだけ使う(ハイライト・リムは元の法線)。「影専用の法線を使う」をオンにする |
| パック型マスク | R＝ハイライトの強さ、G＝影のしきい値、B＝ハイライトのマスク、A＝素材 ID。ID ごとにリム・輪郭線・光る色 |

### 触れるとへこむ

Inspector の「触れるとへこむ」をオンにし、触れるもの(カプセル)を MaterialPropertyBlock の `_SoftContactA`・`_SoftContactB`・`_SoftContactCount` で渡すと、頂点の段階で肌がへこみます(最大 32 か所。全パスで同じ形なので、影・輪郭線もへこむ)。

- まわりがなめらかに沈み、外側は少し盛り上がる(体積を保つ)。
- マスクの R＝やわらかさ、G＝肉の厚み、B＝平らさ(丸い所ほどへこみが面に沿う)。
- 押した所の血色と、近づくものの影も出る。

### 音に反応する

**Galaxy Audio React** を置くと、鳴っている音を低音・中音・高音に分けて測り、グローバル値 `_GalaxyAudio` に入れます。
マテリアルの「トゥーンの強化」で帯域を選び、「音で発光を強める量」「音でリムを強める量」「音で輪郭線を太くする量」を上げると、音に合わせて光る・線が太くなります。

### 天使の輪の作り方

「異方性ハイライト」の「天使の輪の作り方」で、髪の流れと光で決まる輪(今まで)のほかに、マットキャップ(見る向きで決まる輪。`_RingTex` に輪を描いた球)と、UV2 の帯(2 番目の UV に沿った帯。見る高さでずれる)を選べます。

### 画面の効果(レンダラー機能)

URP のレンダラー(Universal Renderer Data)の「Add Renderer Feature」から足します。

| 機能 | 内容 |
|---|---|
| Galaxy Screen Line | 深度と法線の差で、外形の線と内側の線(指・重なり・部位の境目)を描く。線はシルエットの外側に出るのでキャラが痩せない。押し出しの輪郭線と一緒に使える |
| Galaxy Glow | 明るい所だけをやわらかくにじませる拡散グロー。ブルームとは別に調整できる |
| Galaxy Character Shadow | キャラだけを囲む狭い範囲で、高解像度の影を描く。前髪・指・襟・顎の自己影がくっきり出る。キャラ専用の固定ライトにも影が付く |
| Galaxy Toon Volume Feature | Volume の「Galaxy Toon」(リム・影を暗く・影の色・彩度・明るさ・線の太さ)を全キャラに当てる。場面・時間帯ごとに Volume で切り替えられる |
| Galaxy Paint | 塗りのムラをならし(桑原フィルタ)、明るさを段にする。色相は変えない。背景向け |

### 動きのベクトル

TAA・STP・モーションブラー用の MotionVectors パスがあります(動くキャラがぶれません)。

## コンポーネント

`Add Component > GalaxyToon` から追加します。

| コンポーネント | 用途 |
|---|---|
| Galaxy Toon Light | キャラ専用の固定ライト(任意)。キャラ 1 体だけにも当てられる |
| Galaxy Toon Face | 首の向きに顔の陰影・SDF 影・髪の影を合わせる。高さの毛先・遠近の除去の中心も入れる |
| Galaxy Toon Effects | 濡れ・汗・液体・泡・雨・汚れ・傷・頬染めの量をゲーム中に動かす |
| Galaxy Toon Hair Sway | ボーンの無い髪を揺らす |
| Galaxy Toon Fur | 毛(シェル法)を生やす。マテリアルは `GalaxyShader/ToonFur` |
| Galaxy Toon See-Through Bangs | 前髪の毛先を半透明にして、眉・目を前髪越しに見せる(再生中) |
| Galaxy Tears | 涙・粘る液が肌を伝って流れ、垂れ落ちる |
| Galaxy Tongue Drip | 舌先から液を垂らす |
| Galaxy Sticky Strings | 2 点の間に伸びて切れる糸を引く |
| Galaxy Teleport | 転送・ワープで消える / 現れる / 服を入れ替える |
| Galaxy Audio React | 鳴っている音を低音・中音・高音に分けて測り、発光・リム・輪郭線を音に合わせる |
| Galaxy Hair Shadow Feature | 髪の影を描く URP Renderer Feature |
| Galaxy Character Shadow / Screen Line / Glow / Paint / Toon Volume Feature | 画面の効果の URP Renderer Feature([画面の効果](#画面の効果レンダラー機能)) |
| Galaxy Toon Sample Contact | サンプル用。触れるとへこむ・脚に押される布・スカートの影に値を渡す見本 |

## メニュー

`Tools > GalaxyToon` にあります。

| メニュー | 内容 |
|---|---|
| GalaxyToon に移行 (MToon・lilToon・Lit など) | ほかのシェーダーのマテリアルを GalaxyToon に移す。VRM のキャラは顔・瞳・輪郭線・髪の影までまとめて設定 |
| サンプルシーンを作る (機能ごとの球) | `Samples/` のサンプルシーンを作り直す |
| 髪の影を有効にする | 使っている URP Renderer に Galaxy Hair Shadow Feature を足す |
| Volume を有効にする | 使っている URP Renderer に Galaxy Toon Volume Feature を足す |
| 瞳の範囲を探す / 頬の位置を探す | 選択中のキャラから瞳・頬の UV を探してマテリアルに入れる |
| 前髪を透過する | 選択中のキャラに Galaxy Toon See-Through Bangs を付ける |
| 輪郭線用のなめらか法線を焼く | 輪郭線が割れないように、なめらかな法線を焼く |
| SDF 影マップを作る | 顔・髪・体の SDF 影マップを作る |
| ノーマルマップを作る | テクスチャからノーマルマップを作る |
| ランプのひな形を作る | 影・ハイライトのランプ(部位 4 段)を作る |
| ランプを編集・作成する | グラデーションで影・ハイライトのランプを編集し、マテリアルに映しながら作る |
| 厚みマップを作る | SSS(肌の透け)用の厚みマスクを、メッシュの形から作る |
| 開発用 > 全パラメータ・ライト反映フルテスト / 全項目の総当たりテスト | シェーダーの全機能・全項目が画面に効いているかを自動で確かめる(改造したとき用) |
| 全マテリアルの機能を整理 | 効いていない機能のキーワードを外す |
| 使っている機能の一覧 | 全マテリアルの機能の組み合わせと使用数を Console に出す |
| 負荷を測る | 再生中のシーンで機能を 1 つずつ切って、GPU / CPU の時間を測る |
| シェーダー OFF (Lit で見比べ) | `Ctrl+Shift+T` と同じ |

## サンプルシーン

`Samples/GalaxyToon_FeatureSamples.unity` を開いて再生します。画面下の「◀ 前へ / 次へ ▶」(または ← → キー)で 1 つずつ寄って見られます。見本の球をクリックすると、その見本へ寄ります。ダブルクリックすると、その見本の素材をプレビューのモデルに着せて、モデルへ寄ります。はじめは左のプレビューのモデル(Blender のサル)を大きく映します。画面下の「素材」の ◀ ▶ で、どの見本の素材にも着せ替えられます。モデルはマウスでドラッグすると回せ、ホイールで寄れます。横の小さい球をつかんで動かせます。素材が「押せる肌」(はじめの素材)なら、モデルに押し当てると肌がへこみます。「糸」なら、押し当ててから引き離すと、触れた所から糸を引きます。右側のボタンで、Volume(Galaxy Toon)のプリセット(オフ・標準・夕方・夜・パステル・くっきり)とライト(昼・夕方・夜・逆光・真上・真横)を切り替えられ、その下のスライダーでライトの明るさ、色の四角で背景の色を変えられます。Volume は先に `Tools > GalaxyToon > Volume を有効にする` を実行してください。「全体」(Home キー)で全体に戻ります。

涙・糸・転送・髪の揺れ・毛・雨・触れるとへこむ・脚に押される布は再生中に動きます。1.0.2 で、新しい機能の見本を 4 行(30 個)足しました。「接地影」の球は、カメラの深度テクスチャが要ります。「髪の影」の球は、先に `Tools > GalaxyToon > 髪の影を有効にする` を実行しておくと影が出ます。シーンは `Tools > GalaxyToon > サンプルシーンを作る` でいつでも作り直せます。Package Manager から入れた場合はパッケージの中に書き込めないので、`Assets/GalaxyToon Samples/` に作ります。

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

### 質感 2 — Surface 2

| <img src="Documentation~/Images/65_SSS.png" width="240" alt="SSS"> | <img src="Documentation~/Images/66_Sheer_Stockings.png" width="240" alt="Sheer Stockings"> | <img src="Documentation~/Images/67_Fishnet_Dent.png" width="240" alt="Fishnet Dent"> | <img src="Documentation~/Images/68_Clear_Coat.png" width="240" alt="Clear Coat"> |
|---|---|---|---|
| **SSS (血色・透け)**<br>SSS | **ストッキング**<br>Sheer Stockings | **網タイツの食い込み**<br>Fishnet Dent | **クリアコート**<br>Clear Coat |
| <img src="Documentation~/Images/69_Velvet.png" width="240" alt="Velvet"> | <img src="Documentation~/Images/70_Iridescence.png" width="240" alt="Iridescence"> | <img src="Documentation~/Images/71_Starry.png" width="240" alt="Starry"> | <img src="Documentation~/Images/72_Hair_Tip_Gradient.png" width="240" alt="Hair Tip Gradient"> |
| **ベルベット**<br>Velvet | **虹色の薄膜**<br>Iridescence | **星空**<br>Starry | **毛先のグラデーション**<br>Hair Tip Gradient |

### 影 2 — Shadow 2

| <img src="Documentation~/Images/73_Shade_Map.png" width="240" alt="Shade Map"> | <img src="Documentation~/Images/74_Shadow_Bias_Mask.png" width="240" alt="Shadow Bias Mask"> | <img src="Documentation~/Images/75_Shadow_Pattern.png" width="240" alt="Shadow Pattern"> | <img src="Documentation~/Images/76_Hatching.png" width="240" alt="Hatching"> |
|---|---|---|---|
| **影色テクスチャ**<br>Shade Map | **影の出やすさマスク**<br>Shadow Bias Mask | **影の模様**<br>Shadow Pattern | **ハッチング**<br>Hatching |
| <img src="Documentation~/Images/77_Halftone.png" width="240" alt="Halftone"> | <img src="Documentation~/Images/78_Fill_Light.png" width="240" alt="Fill Light"> | <img src="Documentation~/Images/79_Contact_Shadow.png" width="240" alt="Contact Shadow"> | <img src="Documentation~/Images/80_Skirt_Shadow.png" width="240" alt="Skirt Shadow"> |
| **網点**<br>Halftone | **カメラからの補助光**<br>Fill Light | **接地影**<br>Contact Shadow | **スカートの影**<br>Skirt Shadow |

### 瞳・髪・リム 2 — Eyes / Hair / Rim 2

| <img src="Documentation~/Images/81_Eyelid_Shadow_Caustic.png" width="240" alt="Eyelid Shadow / Caustic"> | <img src="Documentation~/Images/82_Dual_Angel_Ring.png" width="240" alt="Dual Angel Ring"> | <img src="Documentation~/Images/83_Angel_Ring_MatCap.png" width="240" alt="Angel Ring (MatCap)"> | <img src="Documentation~/Images/84_Rim_Toward_Light.png" width="240" alt="Rim Toward Light"> |
|---|---|---|---|
| **まぶたの影・光の溜まり**<br>Eyelid Shadow / Caustic | **天使の輪 2本**<br>Dual Angel Ring | **天使の輪 (マットキャップ)**<br>Angel Ring (MatCap) | **リムを光の側へ**<br>Rim Toward Light |
| <img src="Documentation~/Images/85_Opposite_Rim.png" width="240" alt="Opposite Rim"> | <img src="Documentation~/Images/86_Rim_Shade.png" width="240" alt="Rim Shade"> | <img src="Documentation~/Images/87_Color_Trace_Outline.png" width="240" alt="Color Trace Outline"> | <img src="Documentation~/Images/88_Outline_Boil.png" width="240" alt="Outline Boil"> |
| **影側の環境リム**<br>Opposite Rim | **リムシェード**<br>Rim Shade | **色トレスの輪郭線**<br>Color Trace Outline | **輪郭線のゆらぎ**<br>Outline Boil |

### 形・発光 2 — Shape / Emission 2

| <img src="Documentation~/Images/89_Inflate.png" width="240" alt="Inflate"> | <img src="Documentation~/Images/90_Soft_Touch.png" width="240" alt="Soft Touch"> | <img src="Documentation~/Images/91_Leg_Push.png" width="240" alt="Leg Push"> | <img src="Documentation~/Images/92_Dither_Alpha.png" width="240" alt="Dither Alpha"> |
|---|---|---|---|
| **ふくらむ**<br>Inflate | **触れるとへこむ**<br>Soft Touch | **脚に押される布**<br>Leg Push | **全体のディザ透明度**<br>Dither Alpha |
| <img src="Documentation~/Images/93_Emission_by_View_Angle.png" width="240" alt="Emission by View Angle"> | <img src="Documentation~/Images/94_Emission_Hue_Cycle.png" width="240" alt="Emission Hue Cycle"> |  |  |
| **見る角度の発光**<br>Emission by View Angle | **発光の色の移り変わり**<br>Emission Hue Cycle |  |  |


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
| [肌・布の質感](Documentation~/ja/shader-skin-cloth.md) | SSS / ストッキング / 網タイツの食い込み / クリアコート・ベルベット |
| [トゥーンの強化](Documentation~/ja/shader-toon-plus.md) | 光・影・リム・遠近・頂点カラー・接地影・星空・鼻線・パック型マスク・音 |
| [形を変える・スクリプトから渡す値](Documentation~/ja/shader-shape.md) | ふくらむ / 脚に押される布 / 触れるとへこむ / スカートの影 / 後ろからの光 / 平面化 |
| [画面の効果・音・Volume](Documentation~/ja/rendering.md) | Screen Line / Glow / Paint / Character Shadow / Toon Volume / Audio React |
| [ファー](Documentation~/ja/shader-fur.md) | `GalaxyShader/ToonFur` |
| [エディタツール](Documentation~/ja/editor-tools.md) | `Tools > GalaxyToon` メニューの全ツール |

## ライセンス

[MIT License](LICENSE)

サンプルの 3D モデル `Samples/Models/Monkey.obj` は、Blender に組み込まれているサルの形「Suzanne」(Add > Mesh > Monkey)を、Blender 5.2 でなめらかにして UV を付け、OBJ に書き出した物です(外からダウンロードしたモデルではありません)。Blender 本体は GPL ですが、Blender で作った物(組み込みの形を含む)は GPL の対象ではなく、作った人の物です([Blender のライセンス](https://www.blender.org/about/license/))。このモデルも、パッケージと同じ MIT License で配布します。
