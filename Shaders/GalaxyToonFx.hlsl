// GalaxyShader/Toon の「濡れ・汗・液体」。テクスチャ無しでシェーダ内で作る(汗の粒 = UV のセルノイズ、液体の筋 = 物体空間の3Dノイズ)。
// 粒・筋の高さから法線をずらす(Mikkelsen の「UV 無しバンプ」: 画面微分で高さの傾きを法線へ写す)ので、ハイライトが粒の形に光る。
#ifndef GALAXY_TOON_FX_INCLUDED
#define GALAXY_TOON_FX_INCLUDED

struct GalaxyToonFxResult
{
    half3 normal;     // 粒・筋でずらした法線
    half wet;         // 濡れ(0〜1)
    half sweat;       // 汗の粒の高さ(0〜1)
    half liquid;      // 液体の覆い具合(0〜1)
    half gloss;       // つや(ハイライトの強さ)
    half foam;        // 泡の覆い具合(0〜1)
    half rain;        // 雨の波紋・しずく(0〜1)
    half soak;        // 染み込み(多孔質の面が水を吸ってまだらに濃くなる 0〜1)
    half dirt;        // 汚れ・すす(0〜1)
    half scratch;     // 傷(0〜1)
    half foamEdge;    // 泡の膜(縁・泡どうしの境目, 0〜1)
};

float2 GalaxyHash22(float2 p)
{
    p = float2(dot(p, float2(127.1, 311.7)), dot(p, float2(269.5, 183.3)));
    return frac(sin(p) * 43758.5453);
}

float GalaxyHash31(float3 p)
{
    p = frac(p * 0.1031);
    p += dot(p, p.yzx + 33.33);
    return frac((p.x + p.y) * p.z);
}

// 3D の値ノイズ(0〜1)。液体の筋に使う
float GalaxyValueNoise3(float3 p)
{
    float3 i = floor(p);
    float3 f = frac(p);
    f = f * f * (3.0 - 2.0 * f);
    float a = GalaxyHash31(i), b = GalaxyHash31(i + float3(1, 0, 0));
    float c = GalaxyHash31(i + float3(0, 1, 0)), d = GalaxyHash31(i + float3(1, 1, 0));
    float e = GalaxyHash31(i + float3(0, 0, 1)), g = GalaxyHash31(i + float3(1, 0, 1));
    float h = GalaxyHash31(i + float3(0, 1, 1)), k = GalaxyHash31(i + float3(1, 1, 1));
    return lerp(lerp(lerp(a, b, f.x), lerp(c, d, f.x), f.y), lerp(lerp(e, g, f.x), lerp(h, k, f.x), f.y), f.z);
}

// 汗の粒: UV をセルに分け、セルごとにランダムな位置・大きさの丸い粒を置く(amount で粒の数が増える)。
// flow > 0 なら、粒がゆっくり下(UV の -V)へ流れる。高さ(0〜1、ドーム形)を返す
half GalaxySweatDroplets(float2 uv, half scale, half size, half amount, half flow)
{
    float2 p = uv * scale;
    p.y += _Time.y * flow;
    float2 cell = floor(p);
    float2 f = frac(p);
    half height = 0;
    [unroll] for (int y = -1; y <= 1; y++)
    [unroll] for (int x = -1; x <= 1; x++)
    {
        float2 o = float2(x, y);
        float2 rnd = GalaxyHash22(cell + o);
        float2 rnd2 = GalaxyHash22(cell + o + 17.31);
        if (rnd2.x > amount) continue;                       // 量が少ないと粒が出ないセルが増える
        float2 center = o + 0.15 + rnd * 0.7;
        float radius = size * (0.45 + 0.55 * rnd2.y);
        float2 d = (f - center) * float2(1.0, 0.8);          // 少し縦長(垂れた形)
        half dome = saturate(1.0 - dot(d, d) / (radius * radius));
        height = max(height, sqrt(dome));
    }
    return height;
}

// 高さ h で法線 n をずらす(Mikkelsen 2010)。scale は凹凸の強さ
half3 GalaxyBumpFromHeight(half3 n, float3 positionWS, float h, half scale)
{
    float3 dpdx = ddx(positionWS);
    float3 dpdy = ddy(positionWS);
    float3 r1 = cross(dpdy, n);
    float3 r2 = cross(n, dpdx);
    float det = dot(dpdx, r1);
    float3 grad = sign(det) * (ddx(h) * r1 + ddy(h) * r2);
    return (half3)normalize(abs(det) * n - grad * scale);
}

// ---- 泡: UV 上のセルに泡を1つずつ置き、なめらかにつなげる(smooth min)。大・中・小の3層を重ねて、大きな泡のすき間を小さな泡が埋める。
// セルごとに寿命があり、終わりぎわに縮んで消える(割れる)。UV に貼るので肌に張り付き、体を動かしても滑らない。
float GalaxySmin(float a, float b, float k)
{
    float h = saturate(0.5 + 0.5 * (b - a) / max(k, 1e-4));
    return lerp(b, a, h) - k * h * (1.0 - h);
}

/// 1層ぶん。戻り値 x=泡の距離(負=泡の中, セル単位) y=いちばん近い泡と2番目の差(小さい=泡どうしの境目) z=泡の盛り上がり(0〜1)
float3 GalaxyFoamLayer(float2 p, half amount, half size, half pop, half merge)
{
    float2 cell = floor(p);
    float2 f = frac(p);
    float d = 1e3, d1 = 1e3, d2 = 1e3, dome = 0;
    [unroll] for (int y = -1; y <= 1; y++)
    [unroll] for (int x = -1; x <= 1; x++)
    {
        float2 o = float2(x, y);
        float2 c = cell + o;
        float2 rAB = GalaxyHash22(c), rC2 = GalaxyHash22(c + 41.3);
        if (rAB.x > amount) continue;                                // 量が少ないと泡の無いセルが増える
        float2 center = o + 0.15 + 0.7 * float2(rAB.y, rC2.x);
        // 寿命: 0→1 で一周。終わり(0.85〜1)で縮んで割れ、また別の泡が生まれる
        float life = frac(_Time.y * pop * (0.5 + rAB.y) + rC2.y);
        float radius = size * (0.35 + 0.65 * rC2.x) * (1.0 - smoothstep(0.85, 1.0, life)) * smoothstep(0.0, 0.06, life);
        float dist = length(f - center);
        float sd = dist - radius;
        d = GalaxySmin(d, sd, merge * size);                          // 近い泡どうしがつながる
        if (sd < d1) { d2 = d1; d1 = sd; } else if (sd < d2) d2 = sd;
        dome = max(dome, radius > 1e-4 ? sqrt(saturate(1.0 - dist * dist / (radius * radius))) : 0);
    }
    return float3(d, d2 - d1, dome);
}

/// 大・中・小の3層を重ねる。p は大きい泡のセル単位。戻り値は大きいセル単位(x=距離 y=境目 z=盛り上がり)
float3 GalaxyFoam(float2 p, half density, half size, half pop, half merge)
{
    float3 big = GalaxyFoamLayer(p, density, size, pop, merge);
    float3 mid = GalaxyFoamLayer(p * 2.3 + 17.3, saturate(density * 1.25h), size * 0.9h, pop * 1.3h, merge);
    float3 small = GalaxyFoamLayer(p * 5.2 + 5.1, saturate(density * 1.4h), size * 0.85h, pop * 1.7h, merge);
    mid.xy /= 2.3; small.xy /= 5.2;
    float k = merge * size * 0.35;
    float d = GalaxySmin(GalaxySmin(big.x, mid.x, k), small.x, k * 0.5);
    float wall = min(big.y, min(mid.y, small.y));
    float dome = max(big.z, max(mid.z * 0.8, small.z * 0.6));
    return float3(d, wall, dome);
}

// ---- 雨: ワールド座標で作るので、地面・屋根・壁・キャラのどこに貼っても同じ雨になる
/// 上を向いた面の波紋。マスごとに雨粒が落ち、輪が広がって消える。2層ずらして重ねる。高さ(-1〜1)を返す
float GalaxyRainRipples(float2 p, half amount, half speed)
{
    float h = 0;
    [unroll] for (int layer = 0; layer < 2; layer++)
    {
        float2 q = p + layer * float2(0.37, 0.71);
        float2 cell = floor(q);
        [unroll] for (int y = -1; y <= 1; y++)
        [unroll] for (int x = -1; x <= 1; x++)
        {
            float2 c = cell + float2(x, y);
            float2 rnd = GalaxyHash22(c + layer * 13.7), rnd2 = GalaxyHash22(c + 51.9 + layer * 7.3);
            if (rnd2.x > amount) continue;                                  // 小雨ほど粒が落ちないマスが増える
            float t = frac(_Time.y * speed * (0.8 + 0.4 * rnd2.y) + rnd.x);  // 落ちてから 0→1
            float d = length(q - (c + 0.2 + 0.6 * rnd)) - t * 0.9;         // 輪からの距離(輪は広がる)
            float ring = saturate(1.0 - abs(d) * 14.0);
            h += sin(d * 60.0) * ring * (1.0 - t) * (1.0 - t);              // 広がるほど弱く
        }
    }
    return h;
}

/// 立った面を流れ落ちるしずく。縦の列ごとに1粒、ときどき止まりながら下へ。粒の後ろに細い濡れた跡。高さ(0〜1)を返す
float GalaxyRainStreaks(float2 p, half amount, half speed)
{
    float col = floor(p.x);
    float2 rnd = GalaxyHash22(float2(col, 3.1)), rnd2 = GalaxyHash22(float2(col, 9.7));
    if (rnd2.x > amount) return 0;
    float x = frac(p.x) - (0.2 + 0.6 * rnd.x);
    float t = _Time.y * speed * (0.3 + 0.5 * rnd.y) + rnd2.y * 7.0;
    float y = frac(p.y * 0.25 + t + 0.15 * sin(t * 6.0 + col));             // 0=しずくの位置 1=列の上端。sin でつーっ、つーっ
    // 縦長の筋(丸い頭+細長い尾だとオタマジャクシに見える)。跡は線ではなく途切れた小粒
    float drop = saturate(1.0 - (x * x / 0.005 + (y - 0.08) * (y - 0.08) / 0.016));
    float seg = floor(y * 10.0);
    float beadOn = step(0.45, GalaxyHash22(float2(col, seg + 17.0)).x);
    float bead = saturate(1.0 - (x * x / 0.0025 + (frac(y * 10.0) - 0.5) * (frac(y * 10.0) - 0.5) / 0.03));
    float trail = sqrt(bead) * beadOn * saturate(1.0 - y * 2.5) * 0.5 * step(0.2, y);
    return max(sqrt(drop), trail);
}

// ---- 傷: UV のマスごとに1本、向き・長さがランダムな細い線分。量が多いほど線のあるマスが増える
float GalaxyScratches(float2 p, half amount)
{
    float2 cell = floor(p);
    float s = 0;
    // 展開しない(展開して画面微分を使うとシェーダが重くなりすぎ、ドライバのコンパイルで GPU が時間切れになった)
    [loop] for (int y = -1; y <= 1; y++)
    [loop] for (int x = -1; x <= 1; x++)
    {
        float2 c = cell + float2(x, y);
        float2 r1 = GalaxyHash22(c + 31.7), r2 = GalaxyHash22(c + 77.1);
        if (r2.x > amount) continue;
        float ang = r1.x * 6.2832;
        float2 a = c + 0.2 + 0.6 * GalaxyHash22(c + 5.3);
        float2 b = a + float2(cos(ang), sin(ang)) * (0.5 + 1.1 * r1.y);
        float2 pa = p - a, ba = b - a;
        float h = saturate(dot(pa, ba) / dot(ba, ba));
        float d = length(pa - ba * h);
        float w = 0.012 + 0.025 * r2.y;
        s = max(s, saturate((w - d) / 0.01 + 0.5) * (1.0 - 0.6 * abs(h * 2.0 - 1.0)));   // 両端ほど浅い
    }
    return s;
}

GalaxyToonFxResult GalaxyToonFx(float2 uv, float3 positionWS, float3 positionOS, half3 normal, float3 tangentWS, float3 bitangentWS)
{
    GalaxyToonFxResult r;
    half4 maskA = SAMPLE_TEXTURE2D(_FxMask, sampler_linear_repeat, uv);
    half3 mask = maskA.rgb;

    // 量(キャラ全体で GalaxyToonEffects が動かす) × 受ける強さ(部位ごと。服は汗 0 など) × 場所のマスク
    half wetAmount = _WetAmount * _WetReceive * mask.r;
    half sweatAmount = _SweatAmount * _SweatReceive * mask.g;
    half liquidAmount = _LiquidAmount * _LiquidReceive * mask.b;

    // 外側の分岐はマテリアルの値(全ピクセル共通)で切る。ピクセルごとの値だけで切るとコンパイラが分岐をやめ、量 0 でも全部計算していた。
    // 内側でピクセルごとにも切る(マスクの外を飛ばす)
    r.sweat = 0;
    UNITY_BRANCH if (_SweatAmount * _SweatReceive > 0.001h)
    {
        if (sweatAmount > 0.001h) r.sweat = GalaxySweatDroplets(uv, _SweatScale, _SweatSize, sweatAmount, _SweatFlow);
    }
    r.wet = saturate(max(wetAmount, sweatAmount * 0.4h)); // 汗ばむと肌全体もうっすら濡れてつやが出る

    // 液体: 物体空間で縦に伸ばしたノイズ(=垂れた筋)を、量に応じたしきい値で切る。時間で下へ流れる
    half liquid = 0;
    UNITY_BRANCH if (_LiquidAmount * _LiquidReceive > 0.001h)
    if (liquidAmount > 0.001h)
    {
        // 横に細かく・縦に長いノイズ = 垂れた筋。量 1 でもおよそ半分までしか覆わない(全身ベタ塗りにしない)
        // 粘り(_LiquidViscosity): 流れが遅く、筋より丸い塊になり、縁から中へ厚みが増す(ネバネバ・とろみ)
        half v = _LiquidViscosity;
        // 流れる向き(_LiquidFlowDir)を「下」に見立てた座標にする(既定の下向きなら元のまま)
        float3 flowDir = normalize(_LiquidFlowDir.xyz + float3(0, -1e-4, 0));
        float3 side = normalize(cross(flowDir, abs(flowDir.x) < 0.9 ? float3(1, 0, 0) : float3(0, 0, 1)));
        float3 side2 = cross(flowDir, side);
        float3 q = float3(dot(positionOS, side), -dot(positionOS, flowDir), dot(positionOS, side2)) * _LiquidScale;
        q.xz *= lerp(2.0, 1.0, v);
        q.y = q.y * lerp(0.15, 0.4, v) + _Time.y * _LiquidFlow * (1.0 - 0.85 * v);
        float n = GalaxyValueNoise3(q) * 0.65 + GalaxyValueNoise3(q * 2.3 + 7.1) * 0.35;
        half threshold = 1.0h - liquidAmount * 0.5h;
        liquid = saturate((n - threshold) / lerp(0.025h, 0.1h, v)); // 粘るほど縁がなだらか = 中央が盛り上がった厚い膜
    }
    r.liquid = liquid;

    // 泡(出る場所はマスクの A)
    r.foam = 0; r.foamEdge = 0;
    half foamAmount = _FoamAmount * _FoamReceive * maskA.a;
    half foamDome = 0;
    UNITY_BRANCH if (_FoamAmount * _FoamReceive > 0.001h) // 中で fwidth を使うので、ピクセルごとに変わる条件では切らない
    {
        // 滑り落ちる向き: ワールドの下を、その場所の UV の向き(接線・従法線)に写す
        float2 downUV = float2(dot(float3(0, -1, 0), tangentWS), dot(float3(0, -1, 0), bitangentWS));
        float2 p = uv * _FoamScale;
        // 泡はかたまりで付く: 大きなノイズで「泡の多い所」を作り、そこほど密に(すき間を小さな泡が埋める)
        half clump = GalaxyValueNoise2(p * 0.22 + 3.7) * 0.7h + GalaxyValueNoise2(p * 0.6 + 9.1) * 0.3h;
        // 量が多いほど、かたまりの範囲が広がる(量 1 でも全面は覆わない)
        half edge0 = 0.72h - 0.35h * foamAmount;
        half density = smoothstep(edge0, edge0 + 0.12h, clump) * 0.95h;
        // 流れ: 2つの位相を交互に使う(フローマップと同じ。伸び切る前に入れ替える)
        const float period = 3.0;
        float ph0 = frac(_Time.y / period), ph1 = frac(_Time.y / period + 0.5);
        float travel = _FoamFlow * 3.0 * period;
        float3 f0 = GalaxyFoam(p - downUV * ph0 * travel, density, _FoamSize, _FoamPop, _FoamMerge);
        float3 f1 = GalaxyFoam(p - downUV * ph1 * travel + 0.5, density, _FoamSize, _FoamPop, _FoamMerge);
        float w = abs(ph0 * 2.0 - 1.0);                                          // ph0 が端ほど f1 を使う
        float3 fo = lerp(f0, f1, w);
        float aa = max(fwidth(fo.x), 1e-4);
        r.foam = saturate(-fo.x / aa) * step(0.001h, foamAmount);              // 泡の中(マスクの外は出さない)
        half membrane = saturate(1.0 - (-fo.x) / (_FoamSize * 0.15)) * r.foam;  // 泡の縁(膜)
        half wall = saturate(1.0 - fo.y / (_FoamSize * 0.1)) * r.foam;          // 泡どうしの境目
        r.foamEdge = saturate(max(membrane, wall));
        foamDome = fo.z * r.foam;
    }

    // 雨: 上向きの面は波紋、立った面は流れ落ちるしずく。雨が当たる面は全体も濡れる
    r.rain = 0;
    float rainHeight = 0;
    half rainAmount = _RainAmount * _RainReceive;
    UNITY_BRANCH if (rainAmount > 0.001h)
    {
        half up = smoothstep(0.6h, 0.9h, normal.y);
        half side = 1.0h - smoothstep(0.3h, 0.7h, abs(normal.y));
        half upRipple = up * _RainRipples; // 服・髪の跳ね返りは表面パーティクルで描く
        if (upRipple > 0.001h)
        {
            float ripple = GalaxyRainRipples(positionWS.xz * _RainScale, rainAmount, _RainSpeed);
            rainHeight += ripple * 0.5 * upRipple;
            r.rain = max(r.rain, saturate(abs(ripple)) * upRipple);
        }
        if (side > 0.001h && _RainFlowAmount > 0.001h)
        {
            // 面に沿った横の座標: 法線を水平にした向きと直交する向き
            float2 across = normalize(float2(-normal.z, normal.x) + 1e-5);
            float streak = GalaxyRainStreaks(float2(dot(positionWS.xz, across), positionWS.y) * _RainScale * 1.5, rainAmount * _RainFlowAmount, _RainSpeed);
            rainHeight += streak * side;
            r.rain = max(r.rain, streak * side);
        }
        r.wet = saturate(max(r.wet, rainAmount * 0.35h * max(up, side))); // 雨そのものの濡れは控えめ(じわっと広がる濡れは _WetAmount 側)
    }

    // 染み込み: 多孔質(コンクリート・石・布)は水を吸い、まだらに色が濃くなる。濡れが増えるほどしみが広がってつながる(ワールド座標)
    r.soak = 0;
    UNITY_BRANCH if (_WetSoak > 0.001h && _WetAmount * _WetReceive + _SweatAmount * _SweatReceive + rainAmount > 0.001h)
    if (r.wet > 0.001h)
    {
        float3 sq = positionWS * (_RainScale * 0.6);
        float sn = GalaxyValueNoise3(sq) * 0.55 + GalaxyValueNoise3(sq * 2.7 + 4.2) * 0.3 + GalaxyValueNoise3(sq * 7.1 + 1.3) * 0.15;
        half thr = 0.78h - 0.95h * r.wet;                       // 濡れが進むほどしきい値が下がり、染みが広がって最後は全面
        r.soak = smoothstep(thr, thr + 0.16h, (half)sn) * _WetSoak;
    }

    // 汚れ・すす: 物体空間の3層ノイズ(体に張り付く)。量が増えるとしみが広がってつながる
    r.dirt = 0; r.scratch = 0;
    half dirtAmount = _DirtAmount * _DirtReceive;
    UNITY_BRANCH if (dirtAmount > 0.001h)
    {
        float3 dq = positionOS * _DirtScale;
        float dn = GalaxyValueNoise3(dq) * 0.5 + GalaxyValueNoise3(dq * 2.7 + 3.1) * 0.3 + GalaxyValueNoise3(dq * 7.3 + 1.7) * 0.2;
        r.dirt = saturate((dn - (1.0 - dirtAmount * 1.05)) / 0.12);
    }
    half scratchAmount = _ScratchAmount * _DirtReceive;
    UNITY_BRANCH if (scratchAmount > 0.001h) r.scratch = GalaxyScratches(uv * _ScratchScale, scratchAmount);

    // 粒・筋・泡・波紋の高さから法線をずらす(粘る液・泡は盛り上がる、傷はへこむ)
    float height = r.sweat * 0.6 + liquid * (0.4 + 0.8 * _LiquidViscosity) + foamDome * 0.8 + rainHeight * 0.7 - r.scratch * 0.4;
    r.normal = normal;
    UNITY_BRANCH if (_SweatAmount * _SweatReceive + _LiquidAmount * _LiquidReceive + _FoamAmount * _FoamReceive + rainAmount + scratchAmount > 0.001h)
    {
        // 画面微分(ddx/ddy)は全ピクセル共通の分岐の中なので安全
        bool hasBump = (r.sweat + liquid + r.foam + r.rain + r.scratch) > 0.001h;
        r.normal = hasBump ? GalaxyBumpFromHeight(normal, positionWS, height, _FxBump * 0.02) : normal;
    }
    // 多孔質は水を吸っている間つやが出ず、吸いきれなくなった終盤(濡れ 0.65〜1)で表面に水の膜ができて少しテカる
    half wetGloss = r.wet * _WetGloss * lerp(1.0h, smoothstep(0.65h, 1.0h, r.wet), _WetSoak);
    r.gloss = saturate(wetGloss + r.sweat + liquid * _LiquidGloss * (1.0 + _LiquidViscosity) + r.foam * 0.8 + r.rain) * (1.0h - r.dirt * 0.8h); // すすはつやを消す
    return r;
}

#endif
