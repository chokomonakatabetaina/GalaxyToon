using UnityEngine;

namespace GalaxyToon
{
    /// <summary>サンプルシーン用: この物の位置のカプセル(上下に halfLength、太さ radius)を、シェーダーが外から受け取る値として毎フレーム入れる。
    /// 自分のゲームで「触れるとへこむ」「脚に押される布」「スカートの影」を動かすときの書き方の見本。</summary>
    [AddComponentMenu("GalaxyToon/Samples/Galaxy Toon Sample Contact")]
    [ExecuteAlways] // エディタでも形・影が見えるように
    public sealed class GalaxyToonSampleContact : MonoBehaviour
    {
        public enum Kind { SoftTouch, LegPush, SkirtShadow }

        [Tooltip("SoftTouch: target の肌をへこませる(MaterialPropertyBlock)。LegPush: 脚のカプセル(グローバル)。SkirtShadow: この位置を中心にスカートの影(グローバル)。")]
        public Kind kind = Kind.SoftTouch;
        [Tooltip("へこませる Renderer(SoftTouch のとき)。")] public Renderer target;
        [Tooltip("カプセルの太さ(m)。SkirtShadow では影の範囲の半径。")] public float radius = 0.15f;
        [Tooltip("カプセルの中心から両端までの長さ(m、この物の上向き)。")] public float halfLength = 0.2f;

        private static readonly int ContactA = Shader.PropertyToID("_SoftContactA"), ContactB = Shader.PropertyToID("_SoftContactB"), ContactCount = Shader.PropertyToID("_SoftContactCount");
        private static readonly int LegStart = Shader.PropertyToID("_GalaxyLegStart0"), LegEnd = Shader.PropertyToID("_GalaxyLegEnd0");
        private static readonly int Skirt = Shader.PropertyToID("_GalaxySkirtShadow"), SkirtCenter = Shader.PropertyToID("_GalaxySkirtShadowCenter");
        private readonly Vector4[] a = new Vector4[32], b = new Vector4[32]; // シェーダーの配列は 32 個。最初に入れた長さで固定されるので常に 32 で渡す
        private MaterialPropertyBlock block;

        private void LateUpdate()
        {
            Vector3 p = transform.position, up = transform.up * halfLength;
            switch (kind)
            {
                case Kind.SoftTouch:
                    if (target == null) return;
                    block ??= new MaterialPropertyBlock();
                    target.GetPropertyBlock(block);
                    Vector3 s = p - up, e = p + up;
                    a[0] = new Vector4(s.x, s.y, s.z, radius); // xyz=端 A、w=太さ
                    b[0] = new Vector4(e.x, e.y, e.z, 1f);     // xyz=端 B、w=押す強さ
                    block.SetVectorArray(ContactA, a);
                    block.SetVectorArray(ContactB, b);
                    block.SetFloat(ContactCount, 1f);
                    target.SetPropertyBlock(block);
                    break;
                case Kind.LegPush:
                    Shader.SetGlobalVector(LegStart, new Vector4(p.x + up.x, p.y + up.y, p.z + up.z, radius)); // xyz=付け根、w=太さ
                    Shader.SetGlobalVector(LegEnd, p - up);                                                   // xyz=先
                    break;
                case Kind.SkirtShadow:
                    Shader.SetGlobalVector(Skirt, new Vector4(p.y - halfLength, 0.1f, 1f, radius)); // x=裾の高さ y=裾の下の帯 z=強さ w=半径
                    Shader.SetGlobalVector(SkirtCenter, new Vector4(p.x, p.z, p.y + halfLength, 0f)); // x=中心の X y=中心の Z z=腰の高さ
                    break;
            }
        }

        private void OnDisable()
        {
            if (kind == Kind.LegPush) Shader.SetGlobalVector(LegStart, Vector4.zero);
            if (kind == Kind.SkirtShadow) Shader.SetGlobalVector(Skirt, Vector4.zero);
            if (kind == Kind.SoftTouch && target != null) { target.GetPropertyBlock(block ??= new MaterialPropertyBlock()); block.SetFloat(ContactCount, 0f); target.SetPropertyBlock(block); }
        }
    }
}
