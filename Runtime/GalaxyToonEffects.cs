using System.Collections.Generic;
using UnityEngine;

namespace GalaxyToon
{
    /// <summary>キャラ全体の「濡れ・汗・液体」の量をゲーム中に変える。キャラのルートに付け、スライダーやスクリプトから wet / sweat / liquid を動かす。
    /// 対象は GalaxyShader/Toon で「濡れ・汗・液体を使う」が ON のマテリアル(OFF の部位は変わらない)。量は各マテリアルの値を上書きする。</summary>
    [AddComponentMenu("GalaxyToon/Galaxy Toon Effects")]
    [ExecuteAlways]
    public sealed class GalaxyToonEffects : MonoBehaviour
    {
        [Tooltip("ON: 下の量で常にマテリアルの値を上書きする(0 も書く)。OFF: 0 より大きい量だけ上書きし、0 の量はマテリアルの値(プリセットなど)をそのまま使う。")]
        public bool overrideMaterials;
        [Range(0f, 1f)] public float wet;
        [Range(0f, 1f)] public float sweat;
        [Range(0f, 1f)] public float liquid;
        [Range(0f, 1f)] public float foam;
        [Tooltip("雨に打たれる量。地面は波紋、服・髪は跳ね返りと少量のしずく。雨粒は Rain プレハブ。")]
        [Range(0f, 1f)] public float rain;
        [Tooltip("服・髪に当たって跳ねる小さなしぶきのマテリアル。各材質の「表面で雨が跳ねる強さ」で対象を選ぶ。")]
        public Material rainSplashMaterial;
        [Tooltip("汚れ・すすの量(火事・爆発・事故のあと)。")]
        [Range(0f, 1f)] public float dirt;
        [Tooltip("傷の量。")]
        [Range(0f, 1f)] public float scratch;
        [Tooltip("頬染め(照れ)。頬の位置は顔マテリアルの「頬の位置」(メニュー「頬の位置を探す」で自動)。")]
        [Range(0f, 1f)] public float blush;
        [Tooltip("液体の色(A=不透明さ)。")]
        public Color liquidColor = new Color(1f, 1f, 1f, 0.45f);

        [Tooltip("雨に打たれて濡れていく速さ(1秒あたり。雨の量を掛ける)。雨 > 0 のあいだ wet がじわじわ 1 まで増え、染み(染み込み)も広がる。0 = 増えない")]
        [Min(0f)] public float wetSoakPerSecond = 0.08f;

        [Header("乾く速さ (1秒あたりに減る量。0 = 乾かない)")]
        [Tooltip("例: 0.1 なら、濡れ 1 が10秒で乾く。")]
        [Min(0f)] public float wetDryPerSecond;
        [Min(0f)] public float sweatDryPerSecond;
        [Tooltip("液体が流れ落ちて消えていく速さ。垂れる速さはマテリアルの「液体が垂れる速さ」。")]
        [Min(0f)] public float liquidDryPerSecond;
        [Tooltip("泡が消えていく速さ。")]
        [Min(0f)] public float foamDryPerSecond;

        private static readonly int WetId = Shader.PropertyToID("_WetAmount");
        private static readonly int SweatId = Shader.PropertyToID("_SweatAmount");
        private static readonly int LiquidId = Shader.PropertyToID("_LiquidAmount");
        private static readonly int FoamId = Shader.PropertyToID("_FoamAmount");
        private static readonly int RainId = Shader.PropertyToID("_RainAmount");
        private static readonly int DirtId = Shader.PropertyToID("_DirtAmount");
        private static readonly int ScratchId = Shader.PropertyToID("_ScratchAmount");
        private static readonly int BlushId = Shader.PropertyToID("_BlushAmount");
        private static readonly int LiquidColorId = Shader.PropertyToID("_LiquidColor");
        private const string FxKeyword = "_GALAXY_FX";

        private readonly List<Renderer> targets = new List<Renderer>();
        private MaterialPropertyBlock block;
        private Vector4 applied = new Vector4(-1f, -1f, -1f, -1f);
        private Color appliedColor;
        private Vector4 appliedExtra = new Vector4(-1f, -1f, -1f, -1f); // 雨・汚れ・傷・照れ
        private bool driving; // 今、マテリアルの値を上書き中か

        private bool ShouldDrive => overrideMaterials || wet > 0f || sweat > 0f || liquid > 0f || foam > 0f || rain > 0f || dirt > 0f || scratch > 0f || blush > 0f;

        private void OnEnable()
        {
            if (Application.isPlaying && rainSplashMaterial != null && !GetComponent<GalaxyRainSurfaceSplashes>())
                gameObject.AddComponent<GalaxyRainSurfaceSplashes>();
            block ??= new MaterialPropertyBlock();
            targets.Clear();
            foreach (Renderer r in GetComponentsInChildren<Renderer>(true))
                foreach (Material m in r.sharedMaterials)
                    if (m != null && (m.IsKeywordEnabled(FxKeyword) || m.HasProperty(BlushId))) { targets.Add(r); break; }
            applied = new Vector4(-1f, -1f, -1f, -1f);
            Apply();
        }

        private void OnDisable()
        {
            // 外したら各マテリアルの値に戻す
            // (GalaxyToonFace の値も消えるが、あちらは毎フレーム入れ直す)
            foreach (Renderer r in targets) if (r != null) r.SetPropertyBlock(null);
        }

        private void LateUpdate()
        {
            if (Application.isPlaying)
            {
                float dt = Time.deltaTime;
                if (rain > 0f && wetSoakPerSecond > 0f) wet = Mathf.Min(1f, wet + wetSoakPerSecond * rain * dt); // 雨でじわっと濡れていく
                wet = Mathf.Max(0f, wet - wetDryPerSecond * dt);
                sweat = Mathf.Max(0f, sweat - sweatDryPerSecond * dt);
                liquid = Mathf.Max(0f, liquid - liquidDryPerSecond * dt);
                foam = Mathf.Max(0f, foam - foamDryPerSecond * dt);
            }
            Apply();
        }

        private void OnValidate() { if (isActiveAndEnabled && block != null) Apply(); } // エディタでスライダーを動かしたとき

        private void Apply()
        {
            if (!ShouldDrive)
            {
                // 量が全部 0 のときは上書きしない(マテリアルに入れた汗・濡れがそのまま見える)。上書きしていた分は消す
                if (driving) { foreach (Renderer r in targets) if (r != null) r.SetPropertyBlock(null); driving = false; applied = new Vector4(-1f, -1f, -1f, -1f); }
                return;
            }
            driving = true;
            var now = new Vector4(wet, sweat, liquid, foam);
            var extra = new Vector4(rain, dirt, scratch, blush);
            if (now == applied && liquidColor == appliedColor && extra == appliedExtra) return; // 変わったときだけ書く
            applied = now;
            appliedExtra = extra;
            appliedColor = liquidColor;
            foreach (Renderer r in targets)
            {
                if (r == null) continue;
                // 0 の量は書かない(OFF のとき): 汗を出しても、口のマテリアルに入れた液体はそのまま残る。
                // 以前書いた値を消すためブロックは作り直す(GalaxyToonFace の値は毎フレーム入れ直される)
                block.Clear();
                Set(WetId, wet); Set(SweatId, sweat); Set(LiquidId, liquid); Set(FoamId, foam);
                Set(RainId, rain); Set(DirtId, dirt); Set(ScratchId, scratch); Set(BlushId, blush);
                if (overrideMaterials || liquid > 0f) block.SetColor(LiquidColorId, liquidColor);
                r.SetPropertyBlock(block);
            }
            void Set(int id, float v) { if (overrideMaterials || v > 0f) block.SetFloat(id, v); }
        }
    }
}
