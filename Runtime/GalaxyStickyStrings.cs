using System.Collections.Generic;
using UnityEngine;

namespace GalaxyToon
{
    /// <summary>
    /// 2点の間に「ネバーッ」と糸を引く(納豆を食べて口を開けたとき、指の間に伸びる粘液など)。
    /// 離れるほど伸びて真ん中が細くなり、重さでたるみ、揺れる。伸び切るとプツッと切れて両側へ縮み、近づくとまたつながる。
    /// 端は Transform か、スキンメッシュの頂点(表情のブレンドシェイプで動く唇など)を追いかける。
    /// </summary>
    [AddComponentMenu("GalaxyToon/Galaxy Sticky Strings")]
    [ExecuteAlways] // エディタでも口の形を変えて糸を確かめられるように
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public sealed class GalaxyStickyStrings : MonoBehaviour
    {
        [Header("端 (Transform を入れればそちら、空ならスキンメッシュの頂点)")]
        public Transform anchorA;
        public Transform anchorB;
        public SkinnedMeshRenderer skin;
        [Tooltip("端B が別のメッシュにあるとき(上歯と下歯が別 Renderer など)。空なら skin と同じ。")] public SkinnedMeshRenderer skinB;
        [Tooltip("端A の頂点番号(上唇など)。")] public int vertexA = -1;
        [Tooltip("端B の頂点番号(下唇など)。")] public int vertexB = -1;

        [Header("糸")]
        [Range(1, 12)] public int strandCount = 6;
        [Tooltip("糸を横に並べる幅(m)。")] public float spread = 0.02f;
        [Tooltip("糸の太さ(半径 m)。糸ごとに 0.55〜1.35 倍ばらつく。")] public float radius = 0.0005f;
        [Range(0.05f, 1f), Tooltip("伸びたとき真ん中がどこまで細くなるか。")] public float midThin = 0.35f;
        [Tooltip("重さでたるむ量(糸の長さに対する割合)。")] public float sag = 0.18f;
        [Tooltip("ゆらゆら揺れる幅(m)。")] public float wobble = 0.0012f;
        [Tooltip("画面上の最低の太さ(px)。遠いカメラでも糸が消えないように。0 なら太さそのまま。")] public float minScreenPixels = 1.2f;

        [Header("切れる・つながる")]
        [Tooltip("これより長く伸びると切れる(m)。糸ごとに ±30% ばらつく。")] public float breakLength = 0.035f;
        [Tooltip("切れてから縮み切るまでの秒数。")] public float retractTime = 0.35f;
        [Tooltip("これより近づくと、切れた糸がまたつながる(m)。")] public float reformDistance = 0.008f;

        private const int Segments = 14;

        private sealed class Strand
        {
            public float lateral, breakAt, phase, thickness, sagScale, skew; // 糸ごとのばらつき(平行に並んだ作り物っぽさを消す)
            public bool broken;
            public float brokenTime;
            public Vector3 breakA, breakB; // 切れた瞬間の両端の位置(縮む起点)
        }

        private readonly List<Strand> strands = new List<Strand>();
        private GalaxyTubeMesh tube;
        private Mesh baked, bakedB;
        private readonly Vector3[] points = new Vector3[Segments + 1];
        private readonly float[] radii = new float[Segments + 1];

        private void OnEnable()
        {
            tube = new GalaxyTubeMesh(transform, "Sticky Strings");
            GetComponent<MeshFilter>().sharedMesh = tube.Mesh;
            BuildStrands();
        }

        private void OnDisable()
        {
            if (tube != null) DestroyObj(tube.Mesh);
            if (baked != null) DestroyObj(baked);
            if (bakedB != null) DestroyObj(bakedB);
        }

        private void BuildStrands()
        {
            strands.Clear();
            var rnd = new System.Random(1234);
            for (int i = 0; i < strandCount; i++)
            {
                float u = strandCount == 1 ? 0.5f : i / (float)(strandCount - 1);
                strands.Add(new Strand
                {
                    lateral = (u - 0.5f) * spread + ((float)rnd.NextDouble() - 0.5f) * spread * 0.25f,
                    breakAt = breakLength * (0.7f + 0.6f * (float)rnd.NextDouble()),
                    phase = (float)rnd.NextDouble() * 10f,
                    thickness = 0.55f + 0.8f * (float)rnd.NextDouble(),
                    sagScale = 0.4f + 1.2f * (float)rnd.NextDouble(),
                    skew = ((float)rnd.NextDouble() - 0.5f) * spread * 0.5f,
                });
            }
        }

        private bool TryGetEnds(out Vector3 a, out Vector3 b)
        {
            a = b = default;
            if (anchorA != null && anchorB != null) { a = anchorA.position; b = anchorB.position; return true; }
            if (skin == null || vertexA < 0 || vertexB < 0) return false;
            if (baked == null) baked = new Mesh();
            skin.BakeMesh(baked, true);
            var v = baked.vertices;
            if (vertexA >= v.Length) return false;
            a = skin.transform.localToWorldMatrix.MultiplyPoint3x4(v[vertexA]);
            SkinnedMeshRenderer sb = skinB != null ? skinB : skin;
            if (sb != skin) { if (bakedB == null) bakedB = new Mesh(); sb.BakeMesh(bakedB, true); v = bakedB.vertices; }
            if (vertexB >= v.Length) return false;
            b = sb.transform.localToWorldMatrix.MultiplyPoint3x4(v[vertexB]);
            return true;
        }

        private void LateUpdate()
        {
            if (strands.Count != strandCount) BuildStrands();
            tube.Begin();
            if (TryGetEnds(out Vector3 a, out Vector3 b))
            {
                Transform basis = skin != null ? skin.transform.root : transform.root;
                Vector3 side = basis.right, down = -basis.up;
                float time = Application.isPlaying ? Time.time : 0f;
                float dt = Application.isPlaying ? Time.deltaTime : 0f;
                // 遠くても画面で minScreenPixels px は残す太さ(m)。距離 1m あたりの 1px の大きさから求める
                float minRadius = 0f;
                Camera cam = Camera.main;
                if (cam != null && minScreenPixels > 0f && !cam.orthographic)
                {
                    float dist = Vector3.Distance(cam.transform.position, (a + b) * 0.5f);
                    float pixelSize = 2f * dist * Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad) / Mathf.Max(1, cam.pixelHeight);
                    minRadius = pixelSize * minScreenPixels * 0.5f;
                }
                foreach (Strand s in strands)
                {
                    // 唇は横に丸いので、外側の糸ほど端を少し内へ寄せる
                    float edge = Mathf.Abs(s.lateral) / Mathf.Max(spread * 0.5f, 1e-5f);
                    Vector3 ai = a + side * s.lateral, bi = b + side * (s.lateral * (1f - 0.15f * edge) + s.skew);
                    float length = Vector3.Distance(ai, bi);

                    if (!s.broken && length > s.breakAt) { s.broken = true; s.brokenTime = 0f; s.breakA = ai; s.breakB = bi; }
                    if (s.broken)
                    {
                        s.brokenTime += dt;
                        if (s.brokenTime >= retractTime && length < reformDistance) s.broken = false;
                    }

                    if (!s.broken)
                    {
                        float stretch = Mathf.Clamp(Mathf.Sqrt(reformDistance * 2f / Mathf.Max(length, 1e-4f)), 0.35f, 1.2f);
                        for (int k = 0; k <= Segments; k++)
                        {
                            float t = k / (float)Segments, bell = Mathf.Sin(Mathf.PI * t);
                            points[k] = Vector3.Lerp(ai, bi, t) + down * (sag * s.sagScale * length * bell)
                                      + side * (Mathf.Sin(time * 3.1f + s.phase + t * 4f) * wobble * bell);
                            radii[k] = Mathf.Max(minRadius, radius * s.thickness * Mathf.Lerp(1f, midThin, bell) * stretch);
                        }
                        tube.AddTube(points, radii, Segments + 1);
                    }
                    else
                    {
                        // 切れた: 両側の半分が、垂れながら端へ縮んでいく
                        float k01 = Mathf.Clamp01(s.brokenTime / Mathf.Max(retractTime, 1e-3f));
                        float remain = (1f - k01) * (1f - k01);
                        if (remain > 0.02f)
                        {
                            Vector3 mid = (s.breakA + s.breakB) * 0.5f;
                            AddHalf(ai, mid, remain, down, s, time, side);
                            AddHalf(bi, mid, remain, down, s, time + 1.7f, side);
                        }
                    }
                }
            }
            tube.End();
        }

        private void AddHalf(Vector3 root, Vector3 mid, float remain, Vector3 down, Strand s, float time, Vector3 side)
        {
            Vector3 tip = Vector3.Lerp(root, mid, remain) + down * (Vector3.Distance(root, mid) * 0.6f * (1f - remain) * remain + 0.002f * remain);
            for (int k = 0; k <= Segments; k++)
            {
                float t = k / (float)Segments;
                points[k] = Vector3.Lerp(root, tip, t) + down * (0.004f * remain * t * t) + side * (Mathf.Sin(time * 5f + s.phase + t * 6f) * wobble * t);
                radii[k] = radius * Mathf.Lerp(1f, 0.15f, t) * Mathf.Lerp(0.6f, 1f, remain); // 先端ほど細く、しずく状に
            }
            tube.AddTube(points, radii, Segments + 1);
        }

        /// <summary>口を開けるシェイプキーの名前の候補(先に見つかったものを使う)。</summary>
        public static readonly string[] MouthOpenShapes = { "MouthOpen", "Fcl_MTH_A", "A", "aa", "MouthA", "vrc.v_aa", "あ" };

        /// <summary>口を開けるシェイプキーの番号(無ければ -1)。</summary>
        public static int FindMouthOpenShape(Mesh mesh)
        {
            foreach (string n in MouthOpenShapes) { int i = mesh.GetBlendShapeIndex(n); if (i >= 0) return i; }
            return -1;
        }

        /// <summary>口を開ける表情で大きく動く頂点(下唇)と、動かない頂点(上唇)を自動で選ぶ。口の中心の前面を選ぶ。</summary>
        [ContextMenu("唇を自動で探す (口を開けるシェイプキー)")]
        public void FindLips()
        {
            if (skin == null || skin.sharedMesh == null) { Debug.LogWarning("[StickyStrings] 顔の SkinnedMeshRenderer を入れてください。"); return; }
            int si = FindMouthOpenShape(skin.sharedMesh);
            if (si < 0) { Debug.LogWarning("[StickyStrings] 口を開けるシェイプキー(MouthOpen / Fcl_MTH_A など)がありません。頂点番号を手で入れてください。"); return; }
            float prev = skin.GetBlendShapeWeight(si);
            var closed = new Mesh(); var open = new Mesh();
            skin.SetBlendShapeWeight(si, 0f); skin.BakeMesh(closed, true);
            skin.SetBlendShapeWeight(si, 100f); skin.BakeMesh(open, true);
            skin.SetBlendShapeWeight(si, prev);
            Vector3[] vc = closed.vertices, vo = open.vertices;
            Matrix4x4 m = skin.transform.localToWorldMatrix;
            Transform root = skin.transform.root;
            Vector3 right = root.right, up = root.up, fwd = root.forward;
            float maxMove = 0f; // ワールド単位(焼いたメッシュは拡大率を外した座標なので、行列で直してから比べる)
            for (int i = 0; i < vc.Length; i++) maxMove = Mathf.Max(maxMove, m.MultiplyVector(vo[i] - vc[i]).magnitude);
            float scale = Mathf.Max(0.001f, root.lossyScale.y);

            // 下唇: 口を開けると「下へ」大きく動く頂点のうち、口の中心線上で、いちばん前・上(下唇の外側のふち)
            Vector3 center = Vector3.zero; int count = 0;
            for (int i = 0; i < vc.Length; i++)
            {
                Vector3 d = m.MultiplyVector(vo[i] - vc[i]);
                if (d.magnitude > maxMove * 0.3f && Vector3.Dot(d.normalized, up) < -0.3f) { center += m.MultiplyPoint3x4(vc[i]); count++; }
            }
            if (count == 0) { Debug.LogWarning("[StickyStrings] 口が見つかりません。"); return; }
            center /= count;
            int lower = -1; float bestLower = float.MinValue;
            for (int i = 0; i < vc.Length; i++)
            {
                Vector3 move = m.MultiplyVector(vo[i] - vc[i]);
                if (move.magnitude < maxMove * 0.3f || Vector3.Dot(move.normalized, up) > -0.3f) continue;
                Vector3 d = m.MultiplyPoint3x4(vc[i]) - center;
                if (Mathf.Abs(Vector3.Dot(d, right)) > 0.004f * scale) continue;
                float score = Vector3.Dot(d, fwd) + Vector3.Dot(d, up) * 0.5f;
                if (score > bestLower) { bestLower = score; lower = i; }
            }
            if (lower < 0) { Debug.LogWarning("[StickyStrings] 下唇が見つかりません。"); return; }
            // 上唇: 下へは動かない頂点のうち、下唇の真上 2cm 以内で、近くて前にあるもの
            Vector3 ll = m.MultiplyPoint3x4(vc[lower]);
            int upper = -1; float bestUpper = float.MaxValue;
            for (int i = 0; i < vc.Length; i++)
            {
                Vector3 move = m.MultiplyVector(vo[i] - vc[i]);
                if (move.magnitude > maxMove * 0.15f && Vector3.Dot(move.normalized, up) < 0f) continue;
                Vector3 d = m.MultiplyPoint3x4(vc[i]) - ll;
                if (Mathf.Abs(Vector3.Dot(d, right)) > 0.004f * scale || Vector3.Dot(d, up) < 0.001f * scale || Vector3.Dot(d, up) > 0.02f * scale) continue;
                float score = d.magnitude - Vector3.Dot(d, fwd) * 0.5f;
                if (score < bestUpper) { bestUpper = score; upper = i; }
            }
            if (upper < 0) { Debug.LogWarning("[StickyStrings] 上唇が見つかりません。"); return; }
            vertexA = upper; vertexB = lower;
            DestroyObj(closed); DestroyObj(open);
            Debug.Log($"[StickyStrings] 上唇 {upper} / 下唇 {lower}");
        }

        private static void DestroyObj(Object o)
        {
            if (Application.isPlaying) Object.Destroy(o); else Object.DestroyImmediate(o);
        }
    }
}
