using System.Collections.Generic;
using UnityEngine;

namespace GalaxyToon
{
    /// <summary>GalaxyToon が使うキャラ専用の固定ライト(任意)。このオブジェクトの「前方」へ向かう光として、
    /// 向きと色をシェーダに渡す。置かなければシーンのメインライトで描く。
    /// 対象(target)を空にすると全キャラ(グローバル値)、入れるとそのキャラだけ(Renderer ごとの MaterialPropertyBlock)。
    /// キャラごとに光の向き・色・明るさを変えられる(ほかのキャラはグローバル値か、シーンのメインライト)。
    /// 夜の量(night)は昼／夜のランプ(_RampDayNight)を混ぜる量。</summary>
    [ExecuteAlways]
    [AddComponentMenu("GalaxyToon/Galaxy Toon Light")]
    public sealed class GalaxyToonLight : MonoBehaviour
    {
        public Color color = new Color(1f, 0.98f, 0.95f);
        [Range(0f, 2f)] public float intensity = 0.95f;

        [Tooltip("このキャラだけを照らす(空なら全キャラ)")]
        public GameObject target;

        [Tooltip("夜の量(0=昼 1=夜)。ランプの上半分=昼・下半分=夜のマテリアルで混ぜる")]
        [Range(0f, 1f)] public float night;

        private static readonly int DirectionId = Shader.PropertyToID("_AdvToonLightDir");
        private static readonly int ColorId = Shader.PropertyToID("_AdvToonLightColor");
        private static readonly int NightId = Shader.PropertyToID("_GalaxyNight");
        private static readonly int LocalDirectionId = Shader.PropertyToID("_GalaxyLocalLightDir");
        private static readonly int LocalColorId = Shader.PropertyToID("_GalaxyLocalLightColor");

        // いまキャラ 1 体だけのライトを入れている Renderer(対象を替えた・外したときに 0 へ戻す)
        private readonly List<Renderer> renderers = new List<Renderer>();
        private GameObject scannedTarget;
        private float nextScan;
        private static GalaxyToonLight globalOwner; // いまグローバルの値を入れているライト(ほかのライトが消さないように)
        private MaterialPropertyBlock block;

        private void OnEnable() => Apply();
        private void Update() => Apply();
        private void OnValidate() => Apply();

        // 無効にしたらメインライトへ戻す(方向 0 = 未設定扱い)。キャラ 1 体だけのライトは 0 にして、グローバルの値に戻す
        private void OnDisable()
        {
            ReleaseGlobal();
            ReleaseRenderers();
        }

        private void Apply()
        {
            Vector3 toLight = -transform.forward;
            if (target == null)
            {
                ReleaseRenderers();
                Shader.SetGlobalVector(DirectionId, new Vector4(toLight.x, toLight.y, toLight.z, 0f));
                Shader.SetGlobalColor(ColorId, color * intensity);
                Shader.SetGlobalFloat(NightId, night);
                globalOwner = this;
                return;
            }

            ReleaseGlobal();
            if (scannedTarget != target || !Application.isPlaying || Time.unscaledTime >= nextScan)
            {
                // 対象を替えたら、前のキャラは 0(グローバルの値)へ戻してから読み直す
                if (scannedTarget != target) ReleaseRenderers();
                scannedTarget = target;
                nextScan = Time.unscaledTime + 1f;
                renderers.Clear();
                target.GetComponentsInChildren(true, renderers);
            }

            // w = 夜の量。SetColor はリニアに直すので、グローバル(そのままの値)と明るさがそろうよう Vector で入れる
            Write(new Vector4(toLight.x, toLight.y, toLight.z, night), (Vector4)(color * intensity));
        }

        private void ReleaseGlobal()
        {
            if (globalOwner != this) return; // 自分が入れた値だけを消す(キャラ 1 体だけのライトが、全キャラ用のライトの値を消さない)
            globalOwner = null;
            Shader.SetGlobalVector(DirectionId, Vector4.zero);
            Shader.SetGlobalFloat(NightId, 0f);
        }

        private void ReleaseRenderers()
        {
            Write(Vector4.zero, Vector4.zero);
            renderers.Clear();
            scannedTarget = null;
        }

        private void Write(Vector4 direction, Vector4 lightColor)
        {
            block ??= new MaterialPropertyBlock();
            foreach (var r in renderers)
            {
                if (r == null) continue;
                r.GetPropertyBlock(block);
                block.SetVector(LocalDirectionId, direction);
                block.SetVector(LocalColorId, lightColor);
                r.SetPropertyBlock(block);
            }
        }
    }
}
