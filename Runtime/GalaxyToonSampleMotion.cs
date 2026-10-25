using UnityEngine;

namespace GalaxyToon
{
    /// <summary>サンプルシーン用: 置いた位置から offset まで行ったり来たりする(糸を伸ばして切る・つなぐ見本など)。</summary>
    [AddComponentMenu("GalaxyToon/Samples/Galaxy Toon Sample Motion")]
    public sealed class GalaxyToonSampleMotion : MonoBehaviour
    {
        [Tooltip("一番離れたときのずれ(ローカル)。")] public Vector3 offset = new Vector3(1f, 0f, 0f);
        [Tooltip("1往復の秒数。")] public float period = 4f;

        private Vector3 start;

        private void Start() => start = transform.localPosition;

        private void Update() => transform.localPosition = start + offset * (0.5f - 0.5f * Mathf.Cos(Time.time * 2f * Mathf.PI / Mathf.Max(period, 0.01f)));
    }
}
