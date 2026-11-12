# GalaxyToon ドキュメント(日本語)

日本語 | [English](../en/README.md)

GalaxyToon の機能を、画面ごと・機能ごとに説明します。導入方法とかんたんな使い方は、リポジトリトップの [README](../../README.md) を見てください。

## Inspector

- [Inspector の使い方](inspector.md) — 描画モード、プリセット、検索、テクスチャの見比べ、初期化
- [プリセット一覧](presets.md) — 全プリセットの内容

## シェーダー `GalaxyShader/Toon`

| ページ | 見出し |
|---|---|
| [基本・ライト・影](shader-basic.md) | 基本(色・立体感) / 色調補正 / 金属・つや・映り込み / トライプラナー / ライト / 影 / アニメ塗り |
| [顔・瞳・髪](shader-face-hair.md) | 顔 / 瞳 / 頬染め / 髪の揺れ / 髪の影 / ステンシル |
| [ハイライト・リム・発光](shader-highlight.md) | ハイライト・マットキャップ / 異方性ハイライト / 逆光ライト / リムライト / ラメ・屈折 / 発光 / レイヤー 1〜3 |
| [濡れ・汗・液体・汚れ](shader-surface-fx.md) | 濡れ / 汗 / 液体 / 泡 / 雨 / 汚れ・傷 |
| [輪郭線・ディゾルブ・透け](shader-outline-dissolve.md) | 輪郭線 / ディゾルブ / カメラが近いと透ける |
| [肌・布の質感](shader-skin-cloth.md) | SSS / ストッキング / 網タイツの食い込み / クリアコート・ベルベット |
| [トゥーンの強化](shader-toon-plus.md) | 光・影・リム・遠近・頂点カラー・接地影・星空・鼻線・パック型マスク・音 |
| [形を変える・スクリプトから渡す値](shader-shape.md) | ふくらむ / 脚に押される布 / 触れるとへこむ / スカートの影 / 後ろからの光 / 平面化 |

## シェーダー `GalaxyShader/ToonFur`

- [ファー](shader-fur.md)

## 画面の効果・音・Volume

- [画面の効果・音・Volume](rendering.md) — Galaxy Screen Line / Glow / Paint / Character Shadow / Toon Volume / Audio React

## エディタツール

- [エディタツール](editor-tools.md) — `Tools > GalaxyToon` メニューの全ツール(ほかのシェーダーからの移行、瞳・頬の自動検出、SDF 影マップなど)
