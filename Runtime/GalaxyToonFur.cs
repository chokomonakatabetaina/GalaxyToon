using UnityEngine;
using UnityEngine.Rendering;

namespace GalaxyToon
{
    /// <summary>
    /// ファー(毛並み)。この Renderer のメッシュを GalaxyShader/ToonFur で何層も重ねて描く(シェル法)。
    /// スキンメッシュは毎フレーム焼いて使うので、動いても毛がついてくる。体の動きで毛先がなびく。
    /// 毛の長さ・密度・色などはファーのマテリアル側。LateUpdate で「このフレームの全カメラ」へ描く(カメラ描画の直前に出すと、スクリプトから撮るカメラに間に合わなかった)。
    /// </summary>
    [AddComponentMenu("GalaxyToon/Galaxy Toon Fur")]
    [ExecuteAlways]
    [RequireComponent(typeof(Renderer))]
    public sealed class GalaxyToonFur : MonoBehaviour
    {
        [Tooltip("ファーのマテリアル(GalaxyShader/ToonFur)。空なら描かない。")] public Material furMaterial;
        [Tooltip("層の数。多いほどふわふわだが重い(8〜16 が目安)。")] [Range(2, 32)] public int shells = 12;
        [Tooltip("対象のサブメッシュ(-1=全部)。")] public int subMesh = -1;
        [Tooltip("体が動いたとき毛がなびく強さ。")] public float sway = 0.6f;
        [Tooltip("なびきが戻る速さ。")] public float stiffness = 6f;

        /// <summary>診断用: 描いた回数(層×サブメッシュ)。</summary>
        public static int DrawCount;
        private static readonly int ShellId = Shader.PropertyToID("_FurShell");
        private static readonly int ForceId = Shader.PropertyToID("_FurForce");
        private Renderer target;
        private Mesh baked;
        private MaterialPropertyBlock[] blocks;
        private Vector3 prevPos, velocity, force;
        private int bakedFrame = -1;

        private void OnEnable()
        {
            target = GetComponent<Renderer>();
            prevPos = transform.position;
            #if UNITY_EDITOR
            if (furMaterial != null) UnityEditor.ShaderUtil.CompilePass(furMaterial, 0, true); // 描画中の同期コンパイルを避ける
            #endif
        }

        private void OnDisable()
        {
            if (baked != null) { if (Application.isPlaying) Destroy(baked); else DestroyImmediate(baked); baked = null; }
        }

        private void LateUpdate()
        {
            if (Application.isPlaying && Time.deltaTime > 0f)
            {
                float dt = Time.deltaTime;
                Vector3 pos = transform.position;
                velocity = (pos - prevPos) / dt;
                prevPos = pos;
                // なびき: 動きと逆向きへ倒れ、止まると戻る
                force = Vector3.Lerp(force, -velocity * sway * 0.1f, 1f - Mathf.Exp(-stiffness * dt));
            }
            Draw();
        }

        private Mesh CurrentMesh()
        {
            if (target is SkinnedMeshRenderer smr)
            {
                if (smr.sharedMesh == null) return null;
                if (baked == null) baked = new Mesh { name = "Fur Baked", hideFlags = HideFlags.DontSave };
                if (bakedFrame != Time.frameCount) { smr.BakeMesh(baked, true); bakedFrame = Time.frameCount; }
                return baked;
            }
            return target.GetComponent<MeshFilter>()?.sharedMesh;
        }

        private void Draw()
        {
            if (furMaterial == null || target == null || !target.enabled || !gameObject.activeInHierarchy) return;
            Mesh mesh = CurrentMesh();
            if (mesh == null) return;
            if (blocks == null || blocks.Length != shells) { blocks = new MaterialPropertyBlock[shells]; for (int i = 0; i < shells; i++) blocks[i] = new MaterialPropertyBlock(); }
            // 焼いたメッシュ(useScale=true)は拡大率を外したローカル座標なので、拡大込みの行列で描く(FBX の 100 倍などの読み込み拡大があっても正しい大きさ)
            Matrix4x4 matrix = transform.localToWorldMatrix;
            Bounds bounds = target.bounds; bounds.Expand(0.1f);
            for (int i = 0; i < shells; i++)
            {
                MaterialPropertyBlock b = blocks[i];
                b.SetFloat(ShellId, (i + 1f) / shells);
                b.SetVector(ForceId, force);
                var rp = new RenderParams(furMaterial)
                {
                    layer = gameObject.layer, matProps = b, worldBounds = bounds,
                    shadowCastingMode = ShadowCastingMode.Off, receiveShadows = false,
                };
                if (subMesh >= 0) { Graphics.RenderMesh(rp, mesh, Mathf.Min(subMesh, mesh.subMeshCount - 1), matrix); DrawCount++; }
                else for (int s = 0; s < mesh.subMeshCount; s++) { Graphics.RenderMesh(rp, mesh, s, matrix); DrawCount++; }
            }
        }
    }
}
