using System.Collections.Generic;
using Unity.Collections;
using UnityEngine;
using UnityEngine.Rendering;

namespace GalaxyToon
{
    /// <summary>前髪の毛先をグラデーションで半透明にする(眉・目が前髪越しに見える)。キャラのルートに付ける。再生中だけ動く。
    /// 髪は「前髪以外 = 不透明(深度・髪影はそのまま)」と「前髪 = 顔パーツのあとに半透明で重ねる複製」に分けて描く。
    /// 複製は前髪の範囲にかかる三角形だけを抜き出した小さなメッシュ(深度用・色用の2組のサブメッシュ、スキニングは1回)。</summary>
    [AddComponentMenu("GalaxyToon/Galaxy Toon See-Through Bangs")]
    public sealed class GalaxyToonSeeThroughBangs : MonoBehaviour
    {
        [Range(0f, 1f), Tooltip("毛先がいちばん透けるときの透け具合(1 で完全に透ける)。")]
        public float strength = 0.6f;
        [Tooltip("前髪の付け根(生え際)の高さ(目の高さから上へ m)。これより下・顔の前の髪を前髪として扱う。")]
        public float hairlineAboveEyes = 0.13f;
        [Tooltip("前髪の毛先の高さ(目の高さから上へ m)。ここでいちばん透ける。")]
        public float tipAboveEyes = -0.01f;
        [Range(0f, 1f), Tooltip("付け根から毛先までのうち、透けない(黒地)割合。0.6 = 付け根から 3/5 までは透けない。")]
        public float opaquePortion = 0.6f;
        [Tooltip("透ける左右の幅の半分(m)。こめかみより内側。")]
        public float halfWidth = 0.075f;
        [Tooltip("髪のマテリアル名に含まれる文字(大文字小文字は区別しない)。")]
        public string hairMaterialKeyword = "hair";

        private const int TipsQueue = 3001; // 眉毛(2999)・まつ毛(3000)のあと。+0 で深度、+1 で色
        private static readonly int CenterId = Shader.PropertyToID("_GalaxyBangCenter");
        private static readonly int UpId = Shader.PropertyToID("_GalaxyBangUp");
        private static readonly int ForwardId = Shader.PropertyToID("_GalaxyBangForward");
        private static readonly int RangeId = Shader.PropertyToID("_GalaxyBangRange");
        private static readonly int RegionId = Shader.PropertyToID("_GalaxyBangRegion");
        private readonly List<(SkinnedMeshRenderer source, SkinnedMeshRenderer copy)> copies = new List<(SkinnedMeshRenderer, SkinnedMeshRenderer)>();
        private readonly List<Material> created = new List<Material>();
        private readonly List<Mesh> createdMeshes = new List<Mesh>();
        private Transform head;
        private Vector3 upInHead, forwardInHead;
        private float eyeHeight, eyeForward;
        private MaterialPropertyBlock block;

        private System.Collections.IEnumerator Start()
        {
            if (!Application.isPlaying) yield break;
            yield return null; // Animator が1度姿勢を書いてから測る(バインド姿勢が使えないリグがある)
            block = new MaterialPropertyBlock();
            var animator = GetComponent<Animator>();
            bool human = animator != null && animator.isHuman;
            head = human ? animator.GetBoneTransform(HumanBodyBones.Head) : null;
            if (head == null) // Generic リグは名前で頭ボーンを探す
                foreach (Transform t in GetComponentsInChildren<Transform>(true))
                    if ((t.name == "head" || t.name == "Head") && t.GetComponent<Renderer>() == null) { head = t; break; }
            if (head == null) { Debug.LogWarning("[SeeThroughBangs] 頭ボーンがありません", this); enabled = false; yield break; }
            upInHead = Quaternion.Inverse(head.rotation) * transform.up;
            forwardInHead = Quaternion.Inverse(head.rotation) * transform.forward;
            var eye = human ? animator.GetBoneTransform(HumanBodyBones.LeftEye) : null;
            Vector3 eyePos = eye != null ? eye.position : FindEyeBones(head, out Vector3 avg) ? avg : head.position + transform.up * 0.07f + transform.forward * 0.06f;
            eyeHeight = Vector3.Dot(eyePos - head.position, transform.up);
            eyeForward = Vector3.Dot(eyePos - head.position, transform.forward);

            foreach (var r in GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                if (r.sharedMesh == null) continue;
                var mats = r.sharedMaterials;
                var subs = new List<int>();
                var depthMats = new List<Material>();
                var tipMats = new List<Material>();
                for (int i = 0; i < mats.Length && i < r.sharedMesh.subMeshCount; i++)
                {
                    var m = mats[i];
                    if (m == null || !m.name.ToLowerInvariant().Contains(hairMaterialKeyword) || !m.HasProperty("_BangFadeMode")) continue;
                    var tips = Make(m, " (Bangs Tips)");
                    tips.SetFloat("_BangFadeMode", 2f);
                    tips.SetFloat("_BangFadeStrength", strength);
                    tips.SetFloat("_ZWrite", 0f);
                    tips.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
                    tips.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
                    tips.renderQueue = TipsQueue + 1;
                    // 前髪の深度だけ先に書く: 半透明側はいちばん手前の束だけが重なる(奥の層が透けて白っぽくならない)
                    var depth = Make(tips, " (Depth)");
                    depth.SetFloat("_ZWrite", 1f);
                    depth.SetFloat("_SrcBlend", (float)BlendMode.Zero);
                    depth.SetFloat("_DstBlend", (float)BlendMode.One);
                    if (depth.HasProperty("_OutlineWidth")) depth.SetFloat("_OutlineWidth", 0f);
                    depth.renderQueue = TipsQueue;
                    subs.Add(i); depthMats.Add(depth); tipMats.Add(tips);
                }
                if (subs.Count == 0) continue;
                Mesh bangs = ExtractBangs(r, subs);
                if (bangs == null) continue; // このメッシュには前髪がない(後ろ髪など): 元のまま不透明で描く
                foreach (int i in subs) { var solid = Make(mats[i], " (Bangs Solid)"); solid.SetFloat("_BangFadeMode", 1f); mats[i] = solid; }
                r.sharedMaterials = mats;
                createdMeshes.Add(bangs);
                depthMats.AddRange(tipMats);
                copies.Add((r, CopyRenderer(r, r.name + " (Bangs Tips)", bangs, depthMats.ToArray())));
            }
        }

        /// <summary>今の姿勢で、前髪の範囲(顔の前・生え際より下・左右の幅の内側。少し余裕をもたせる)に頂点がかかる三角形だけを集めたメッシュ。
        /// サブメッシュは [深度用 × n, 色用 × n](同じ三角形)。頂点は使うものだけに詰める。</summary>
        private Mesh ExtractBangs(SkinnedMeshRenderer r, List<int> subs)
        {
            Mesh src = r.sharedMesh;
            var baked = new Mesh();
            r.BakeMesh(baked, true); // 拡大率を外した頂点になるので TransformPoint で戻す(FBX の 100 倍などが付いたレンダラーでも正しく)
            Vector3[] posePos = baked.vertices;
            Destroy(baked);
            Vector3 up = transform.up, fwd = transform.forward;
            float top = eyeHeight + hairlineAboveEyes + 0.02f, front = eyeForward - 0.03f, width = halfWidth + 0.02f;
            bool In(int v)
            {
                Vector3 d = r.transform.TransformPoint(posePos[v]) - head.position;
                float h = Vector3.Dot(d, up), f = Vector3.Dot(d, fwd);
                return h < top && f > front && (d - up * h - fwd * f).magnitude < width;
            }

            var remap = new Dictionary<int, int>();
            var order = new List<int>();
            var tris = new List<int[]>();
            foreach (int s in subs)
            {
                int[] t = src.GetTriangles(s);
                var keep = new List<int>();
                for (int i = 0; i < t.Length; i += 3)
                {
                    if (!In(t[i]) && !In(t[i + 1]) && !In(t[i + 2])) continue;
                    for (int k = 0; k < 3; k++)
                    {
                        if (!remap.TryGetValue(t[i + k], out int n)) { n = order.Count; remap[t[i + k]] = n; order.Add(t[i + k]); }
                        keep.Add(n);
                    }
                }
                tris.Add(keep.ToArray());
            }
            if (order.Count == 0) return null;

            T[] Pick<T>(T[] a)
            {
                if (a == null || a.Length != src.vertexCount) return null;
                var o = new T[order.Count];
                for (int i = 0; i < o.Length; i++) o[i] = a[order[i]];
                return o;
            }
            var m = new Mesh { name = src.name + " (Bangs)" };
            m.indexFormat = order.Count > 65535 ? IndexFormat.UInt32 : IndexFormat.UInt16;
            m.vertices = Pick(src.vertices);
            var normals = Pick(src.normals); if (normals != null) m.normals = normals;
            var tangents = Pick(src.tangents); if (tangents != null) m.tangents = tangents;
            var colors = Pick(src.colors32); if (colors != null) m.colors32 = colors;
            var uv = new List<Vector4>();
            for (int ch = 0; ch < 8; ch++)
            {
                uv.Clear();
                src.GetUVs(ch, uv);
                if (uv.Count != src.vertexCount) continue;
                var o = new List<Vector4>(order.Count);
                foreach (int v in order) o.Add(uv[v]);
                m.SetUVs(ch, o); // 輪郭線のなめらか法線など、uv に入れた値もそのまま
            }
            // ボーンの重み(1頂点あたりの本数は可変)
            var perVertex = src.GetBonesPerVertex();
            var weights = src.GetAllBoneWeights();
            var start = new int[src.vertexCount];
            for (int i = 1; i < start.Length; i++) start[i] = start[i - 1] + perVertex[i - 1];
            var newPer = new NativeArray<byte>(order.Count, Allocator.Temp);
            var newW = new List<BoneWeight1>();
            for (int i = 0; i < order.Count; i++)
            {
                int v = order[i];
                newPer[i] = perVertex[v];
                for (int k = 0; k < perVertex[v]; k++) newW.Add(weights[start[v] + k]);
            }
            var newWeights = new NativeArray<BoneWeight1>(newW.ToArray(), Allocator.Temp);
            m.SetBoneWeights(newPer, newWeights);
            newPer.Dispose(); newWeights.Dispose();
            m.bindposes = src.bindposes;
            // ブレンドシェイプ(順番は元と同じなので重みはそのまま写せる)
            var dv = new Vector3[src.vertexCount]; var dn = new Vector3[src.vertexCount]; var dt = new Vector3[src.vertexCount];
            for (int s = 0; s < src.blendShapeCount; s++)
                for (int f = 0; f < src.GetBlendShapeFrameCount(s); f++)
                {
                    src.GetBlendShapeFrameVertices(s, f, dv, dn, dt);
                    m.AddBlendShapeFrame(src.GetBlendShapeName(s), src.GetBlendShapeFrameWeight(s, f), Pick(dv), Pick(dn), Pick(dt));
                }
            m.subMeshCount = tris.Count * 2;
            for (int i = 0; i < tris.Count; i++) { m.SetTriangles(tris[i], i); m.SetTriangles(tris[i], tris.Count + i); }
            m.bounds = src.bounds;
            m.UploadMeshData(true); // CPU 側のコピーは捨てる
            return m;
        }

        /// <summary>Humanoid の目が未割り当てのとき(EyeBone L/R などの独自ボーン)、頭の下の「eye」を含むボーンの平均位置を目とする。</summary>
        private static bool FindEyeBones(Transform head, out Vector3 average)
        {
            average = Vector3.zero;
            int n = 0;
            foreach (var t in head.GetComponentsInChildren<Transform>(true))
            {
                string name = t.name.ToLowerInvariant();
                if (t == head || !name.Contains("eye") || name.Contains("brow") || name.Contains("lash") || name.Contains("star")) continue;
                if (t.GetComponent<Renderer>() != null) continue; // メッシュのオブジェクトは除く
                average += t.position; n++;
            }
            if (n == 0) return false;
            average /= n;
            return true;
        }

        private Material Make(Material src, string suffix)
        {
            var m = new Material(src) { name = src.name + suffix };
            created.Add(m);
            return m;
        }

        private static SkinnedMeshRenderer CopyRenderer(SkinnedMeshRenderer src, string name, Mesh mesh, Material[] mats)
        {
            var go = new GameObject(name);
            go.transform.SetParent(src.transform, false);
            var r = go.AddComponent<SkinnedMeshRenderer>();
            r.sharedMesh = mesh;
            r.bones = src.bones;
            r.rootBone = src.rootBone;
            r.localBounds = src.localBounds;
            r.updateWhenOffscreen = src.updateWhenOffscreen;
            r.shadowCastingMode = ShadowCastingMode.Off;
            r.receiveShadows = false;
            r.sharedMaterials = mats;
            return r;
        }

        private void LateUpdate()
        {
            if (head == null) return;
            Vector3 up = head.rotation * upInHead, forward = head.rotation * forwardInHead;
            Shader.SetGlobalVector(CenterId, new Vector4(head.position.x, head.position.y, head.position.z, copies.Count > 0 ? 1f : 0f));
            Shader.SetGlobalVector(UpId, up);
            Shader.SetGlobalVector(ForwardId, forward);
            float top = eyeHeight + hairlineAboveEyes, tip = eyeHeight + tipAboveEyes;
            float fadeStart = Mathf.Lerp(top, tip, opaquePortion); // 付け根から opaquePortion までは透けない
            Shader.SetGlobalVector(RangeId, new Vector4(fadeStart, tip, halfWidth, eyeForward - 0.01f));
            Shader.SetGlobalVector(RegionId, new Vector4(top, 0f, 0f, 0f));
            foreach (var (source, copy) in copies)
            {
                // 髪の揺れ(Renderer ごとの上書き値)とブレンドシェイプを写す
                source.GetPropertyBlock(block);
                copy.SetPropertyBlock(block);
                int n = copy.sharedMesh.blendShapeCount;
                for (int i = 0; i < n; i++) copy.SetBlendShapeWeight(i, source.GetBlendShapeWeight(i));
                copy.enabled = source.enabled;
            }
        }

        private void OnDisable()
        {
            if (Application.isPlaying) Shader.SetGlobalVector(CenterId, Vector4.zero); // 透けを止める
        }

        private void OnDestroy()
        {
            foreach (var (_, copy) in copies) if (copy != null) Destroy(copy.gameObject);
            foreach (var m in created) if (m != null) Destroy(m);
            foreach (var m in createdMeshes) if (m != null) Destroy(m);
        }
    }
}
