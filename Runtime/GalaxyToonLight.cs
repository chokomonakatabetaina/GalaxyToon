using UnityEngine;

namespace GalaxyToon
{
    /// <summary>GalaxyToon が使うキャラ専用の固定ライト(任意)。このオブジェクトの「前方」へ向かう光として、
    /// 向きと色をシェーダのグローバル値に渡す。置かなければシーンのメインライトで描く。</summary>
    [ExecuteAlways]
    [AddComponentMenu("GalaxyToon/Galaxy Toon Light")]
    public sealed class GalaxyToonLight : MonoBehaviour
    {
        public Color color = new Color(1f, 0.98f, 0.95f);
        [Range(0f, 2f)] public float intensity = 0.95f;

        private static readonly int DirectionId = Shader.PropertyToID("_AdvToonLightDir");
        private static readonly int ColorId = Shader.PropertyToID("_AdvToonLightColor");

        private void OnEnable() => Apply();
        private void Update() => Apply();
        private void OnValidate() => Apply();

        // 無効にしたらメインライトへ戻す(方向 0 = 未設定扱い)。
        private void OnDisable() => Shader.SetGlobalVector(DirectionId, Vector4.zero);

        private void Apply()
        {
            Vector3 toLight = -transform.forward;
            Shader.SetGlobalVector(DirectionId, new Vector4(toLight.x, toLight.y, toLight.z, 0f));
            Shader.SetGlobalColor(ColorId, color * intensity);
        }
    }
}
