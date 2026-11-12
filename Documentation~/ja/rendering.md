# 画面の効果・音・Volume

[ドキュメント目次](README.md)

マテリアルの外で動く機能の説明です(1.0.2 で追加)。Renderer Feature は、使っている URP の **Universal Renderer Data** を選び、Inspector の **Add Renderer Feature** から追加します。

- [Galaxy Screen Line(画面の線)](#galaxy-screen-line画面の線)
- [Galaxy Glow(拡散グロー)](#galaxy-glow拡散グロー)
- [Galaxy Paint(塗りのならし)](#galaxy-paint塗りのならし)
- [Galaxy Character Shadow(キャラ専用の高解像度の影)](#galaxy-character-shadowキャラ専用の高解像度の影)
- [Galaxy Toon Volume(場面ごとの調整)](#galaxy-toon-volume場面ごとの調整)
- [Galaxy Audio React(音に反応する)](#galaxy-audio-react音に反応する)
- [Galaxy Toon Light(キャラ 1 体だけのライト)](#galaxy-toon-lightキャラ-1-体だけのライト)
- [動きのベクトル](#動きのベクトル)

---

## Galaxy Screen Line(画面の線)

深度と法線の差から、外形の線と内側の線(指・重なり・部位の境目)を画面に描きます。線はシルエットの外側に出るので、キャラが痩せて見えません。押し出し方式の輪郭線と一緒に使えます。

| 項目 | 説明 |
|---|---|
| Thickness | 線の太さ |
| Depth Threshold | 外形の線: となりがこの割合だけ手前なら線(深度の差 ÷ 奥行き) |
| Normal Threshold | 内側の線: 法線の差(1 − 内積)がこれより大きいと線 |
| Strength | 線の濃さ |
| Darken | 線の所の色の倍率。小さいほど濃い線 |
| Max Distance | この距離(m)より遠い所には線を描きません |
| Object Lines | 物(マテリアルの「物の番号」`_ObjectId`)が変わる所にも線を描きます(重なった服と体の境目など) |

> カメラの深度と法線のテクスチャを使います。

## Galaxy Glow(拡散グロー)

明るい所だけを、やわらかくにじませます。ブルームとは別に調整できます。

| 項目 | 説明 |
|---|---|
| Threshold | これより明るい所が光ります(色のいちばん明るいチャンネル) |
| Softness | 光り始めの境目のなめらかさ |
| Intensity | にじみの強さ |
| Spread | 光の広がり(画素) |
| Character Depth | カメラからこの距離(m)より奥(背景)は光らせません。0 で画面全体 |

## Galaxy Paint(塗りのならし)

塗りのムラをならし(桑原フィルタ)、明るさを段にします。色相は変えません。背景向けです。

| 項目 | 説明 |
|---|---|
| Radius | ならす範囲(1〜4) |
| Levels | 明るさの段の数。0 で段にしません |
| Strength | 効きの強さ |

## Galaxy Character Shadow(キャラ専用の高解像度の影)

キャラだけを囲む狭い範囲で、高解像度の影を描きます。前髪・指・襟・顎の自己影がくっきり出ます。**Galaxy Toon Light**(キャラ専用の固定ライト)にも影が付きます。対象は **Galaxy Toon Face** を付けたキャラです。

| 項目 | 説明 |
|---|---|
| Resolution | 影の画像の大きさ(画素) |
| Strength | 影の濃さ |
| Softness | 影の縁のぼかし(画素) |
| Depth Bias / Normal Bias | 自分の影の汚れを防ぐずらし(画素の大きさに対する倍率) |

## Galaxy Toon Volume(場面ごとの調整)

Volume の **Add Override > GalaxyToon > Galaxy Toon** で、全キャラのリム・影・色・線の太さを、場面や時間帯ごとに変えられます。使うには、Renderer に **Galaxy Toon Volume Feature** も追加してください(**Tools > GalaxyToon > Volume を有効にする** で足せます)。サンプルシーンでは、右側のボタンでプリセット(オフ・標準・夕方・夜・パステル・くっきり)を切り替えて試せます。

| 項目 | 説明 |
|---|---|
| Rim | リムの強さの倍率 |
| Shadow Darken | 影の側をさらに暗くする量 |
| Shadow Tint | 影の側に掛ける色(白でそのまま) |
| Saturation | 彩度(-1 で灰色、+1 で 2 倍) |
| Brightness | 明るさ(-1〜+1) |
| Outline Width | 輪郭線の太さの倍率 |

瞳など、調整を受けたくないマテリアルは、「トゥーンの強化」の **Volume の調整を受ける** をオフにします。

## Galaxy Audio React(音に反応する)

鳴っている音を、低音・中音・高音に分けて測り、グローバル値 `_GalaxyAudio`(x=低音 y=中音 z=高音 w=全体)に入れます。マテリアルの「トゥーンの強化」で帯域を選び、「音で発光を強める量」などを上げると、音に合わせて光り、線が太くなります。

| 項目 | 説明 |
|---|---|
| Source | 測る音の元。空なら、聞こえている音全体 |
| Gain | 大きさの倍率(曲の音量に合わせる) |
| Attack / Release | 大きくなるとき・小さくなるときの速さ |
| Low Max / Mid Max | 低音と中音、中音と高音の境目(Hz) |

## Galaxy Toon Light(キャラ 1 体だけのライト)

1.0.2 から、**Target** にキャラを入れると、そのキャラだけを照らせるようになりました(キャラごとに光の向き・色・明るさを変えられます)。空なら、これまでどおり全キャラを照らします。**Night** は、昼／夜のランプを混ぜる量です(「トゥーンの強化」の昼／夜のランプ)。

## 動きのベクトル

TAA・STP・モーションブラー用の **MotionVectors** パスがあります。髪の揺れ・触れるとへこむ・濡れて縮むなどで動いた形も、そのまま動きのベクトルに入るので、動くキャラがぶれません。
