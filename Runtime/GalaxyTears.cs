using System.Collections.Generic;
using UnityEngine;

namespace GalaxyToon
{
    /// <summary>
    /// 涙。目の下から顔の表面に沿って細い筋が伸び、先の粒がつーっと流れ、あご(流れ道の終わり)まで来ると 1 粒のしずくになって落ちる。
    /// 流れ道は顔メッシュを「いちばん低い隣の頂点」へたどって作るので、顔の形どおりに流れ、表情・首の動きにもついていく。
    /// crying を ON にしている間、左右の目から間隔をあけて流れる。
    /// </summary>
    [AddComponentMenu("GalaxyToon/Galaxy Tears")]
    [ExecuteAlways]
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public sealed class GalaxyTears : MonoBehaviour
    {
        public enum Source { UnderEye, OuterCorner, InnerCorner }

        [Tooltip("顔の SkinnedMeshRenderer。")] public SkinnedMeshRenderer face;
        [Tooltip("顔の肌のサブメッシュ番号。涙はこの面だけをたどる(白目・口の中へ入らない)。")] public int skinSubMesh = 0;
        [Tooltip("涙の出始めの頂点(目の下)。空なら目のボーンから自動で探す。")] public int[] startVertices = new int[0];
        [Tooltip("ON の間、涙が流れる。")] public bool crying;
        [Tooltip("涙が出る所。UnderEye=下まぶたの中ほど OuterCorner=目じり InnerCorner=目頭。変えると出始めを探し直す。")] public Source source = Source.UnderEye;
        [Tooltip("流れ出す前に、まぶたのふちに涙が溜まってふくらむ秒数。0 ですぐ流れる。")] public float poolTime = 1.2f;
        [Tooltip("溜まった涙の大きさ(粒の何倍)。")] public float poolSize = 1.6f;
        [SerializeField, HideInInspector] private Source foundFor;

        [Header("流れ")]
        [Tooltip("流れる速さ(m/秒)。")] public float speed = 0.03f;
        [Tooltip("流れが一瞬止まってはまた進む(つーっ、つーっ)強さ。0 で一定。")] [Range(0f, 1f)] public float stopAndGo = 0.6f;
        [Tooltip("次の涙までの秒数(ばらつきあり)。")] public float interval = 2.5f;
        [Tooltip("筋が消えるまでの秒数。")] public float fadeTime = 3f;
        [Tooltip("粘り。0=水(珠が落ちる) 1=どろどろ(先端から糸状にゆっくり伸びて垂れ、限界で切れる)。")] [Range(0f, 1f)] public float viscosity = 0f;
        [Tooltip("粘った液が切れるまでの伸びる長さ(m)。")] public float strandLength = 0.025f;
        [Tooltip("流れ道が越えられる盛り上がりの高さ(m)。唇の縁を越えてあごへ伝わらせるなら大きめ(0.008)。")] public float climb = 0.0015f;

        [Header("見た目")]
        [Tooltip("筋の太さ(半径 m)。")] public float streakRadius = 0.0009f;
        [Tooltip("先の粒の大きさ(半径 m)。")] public float dropRadius = 0.0018f;
        [Tooltip("肌から浮かせる距離(m)。")] public float surfaceOffset = 0.0005f;

        private sealed class Tear
        {
            public List<int> path;            // 頂点の並び(上から下)
            public float head;                // 先端が何 m 進んだか
            public float length;              // 流れ道の長さ(m)
            public bool fell;                 // しずくになって落ちた
            public Vector3 fallPos, fallVel;
            public float age;                 // 落ちてからの秒数(筋が消えるまで)
            public bool snapped;              // 粘った糸が切れた
            public float pooled;              // 溜まっている秒数(poolTime まで溜まったら流れ出す)
        }

        private readonly List<Tear> tears = new List<Tear>();

        /// <summary>まだ肌(舌先など)にくっついて残っている液があるか。落ちた・糸が切れた後の消えかけの筋は数えない。</summary>
        public bool HasLiquid { get { foreach (Tear t in tears) if (!t.fell || (viscosity > 0f && !t.snapped)) return true; return false; } }
        private readonly float[] nextTime = new float[2];
        private GalaxyTubeMesh tube;
        private Mesh baked;
        private List<int>[] neighbors;
        private Vector3[] worldPos, worldNrm;
        private readonly Vector3[] pts = new Vector3[64];
        private readonly float[] rad = new float[64];

        private void OnEnable()
        {
            tube = new GalaxyTubeMesh(transform, "Tears");
            GetComponent<MeshFilter>().sharedMesh = tube.Mesh;
            neighbors = null;
        }

        private void OnDisable()
        {
            if (tube != null) DestroyObj(tube.Mesh);
            if (baked != null) DestroyObj(baked);
            tears.Clear();
        }

        /// <summary>今すぐ左右(または指定の目)から1粒流す。</summary>
        public void Cry(int eye = -1)
        {
            if (!Prepare()) return;
            for (int i = 0; i < startVertices.Length; i++)
                if (eye < 0 || eye == i) StartTear(startVertices[i]);
        }

        private bool Prepare()
        {
            if (face == null || face.sharedMesh == null) return false;
            if (neighbors == null)
            {
                // 頂点のつながり(同じ位置の頂点は UV の継ぎ目でも1つとみなす)。焼いたメッシュから読む(元の FBX が Read/Write OFF でも読める)
                Bake();
                Mesh m = baked;
                Vector3[] v = m.vertices;
                int[] t = m.GetTriangles(Mathf.Clamp(skinSubMesh, 0, m.subMeshCount - 1));
                var weld = new Dictionary<Vector3Int, int>();
                var group = new int[v.Length];
                for (int i = 0; i < v.Length; i++)
                {
                    var key = Vector3Int.RoundToInt(v[i] * 100000f);
                    if (!weld.TryGetValue(key, out int g)) { g = i; weld[key] = i; }
                    group[i] = g;
                }
                neighbors = new List<int>[v.Length];
                for (int i = 0; i < v.Length; i++) neighbors[i] = new List<int>();
                for (int i = 0; i < t.Length; i += 3)
                    for (int k = 0; k < 3; k++)
                    {
                        int a = group[t[i + k]], b = group[t[i + (k + 1) % 3]];
                        if (a != b) { neighbors[a].Add(b); neighbors[b].Add(a); }
                    }
                for (int i = 0; i < v.Length; i++) if (group[i] != i) neighbors[i] = neighbors[group[i]];
            }
            if (startVertices == null || startVertices.Length == 0 || foundFor != source) FindStartVertices();
            return startVertices.Length > 0;
        }

        private void Bake()
        {
            if (baked == null) baked = new Mesh { hideFlags = HideFlags.DontSave };
            face.BakeMesh(baked, true);
            Vector3[] v = baked.vertices, n = baked.normals;
            Matrix4x4 m = face.transform.localToWorldMatrix;
            if (worldPos == null || worldPos.Length != v.Length) { worldPos = new Vector3[v.Length]; worldNrm = new Vector3[v.Length]; }
            for (int i = 0; i < v.Length; i++) { worldPos[i] = m.MultiplyPoint3x4(v[i]); worldNrm[i] = m.MultiplyVector(n[i]).normalized; }
        }

        private void StartTear(int start)
        {
            Bake();
            // いちばん低い隣へたどる。前へ出すぎ(鼻の下)・後ろへ行きすぎ(耳)は避けるため、下がる量を優先
            var path = new List<int> { start };
            var visited = new HashSet<int> { start };
            int cur = start, climbs = 0;
            for (int step = 0; step < 200; step++)
            {
                int best = -1; float bestY = float.MaxValue;
                foreach (int nb in neighbors[cur])
                    if (!visited.Contains(nb) && worldPos[nb].y < bestY) { bestY = worldPos[nb].y; best = nb; }
                // 下が無ければ、少しだけ横・上へ回り込む(唇の端などをよける)。それでもだめなら終点 = あごの先など
                if (best < 0 || bestY > worldPos[cur].y + climb || (bestY >= worldPos[cur].y && ++climbs > 30)) break;
                path.Add(best); visited.Add(best); cur = best;
            }
            int lowest = 0; for (int i = 1; i < path.Count; i++) if (worldPos[path[i]].y < worldPos[path[lowest]].y) lowest = i;
            path.RemoveRange(lowest + 1, path.Count - lowest - 1);
            if (path.Count < 1) return; // 1点だけ(舌先など、そこが一番低い所)でも、溜まってから垂れる
            float len = 0; for (int i = 1; i < path.Count; i++) len += Vector3.Distance(worldPos[path[i - 1]], worldPos[path[i]]);
            tears.Add(new Tear { path = path, length = len });
        }

        private void LateUpdate()
        {
            if (tube == null || !Prepare()) return;
            float dt = Application.isPlaying ? Time.deltaTime : 0f, time = Application.isPlaying ? Time.time : 0f;
            if (crying && Application.isPlaying)
                for (int i = 0; i < startVertices.Length && i < nextTime.Length; i++)
                    if (time >= nextTime[i]) { StartTear(startVertices[i]); nextTime[i] = time + interval * Random.Range(0.6f, 1.4f); }
            if (tears.Count == 0) { tube.Begin(); tube.End(); return; }

            Bake();
            tube.Begin();
            for (int ti = tears.Count - 1; ti >= 0; ti--)
            {
                Tear t = tears[ti];
                // つーっ、つーっ: 速さを周期的に落とす
                float pulse = 1f - stopAndGo * Mathf.Clamp01(Mathf.Sin(time * 4f + ti) * 0.5f + 0.5f);
                bool pooling = t.pooled < poolTime;
                if (pooling) t.pooled += dt;
                else if (!t.fell) t.head = Mathf.Min(t.length, t.head + speed * pulse * dt * (t.head < 0.004f ? 2.5f : 1f)); // あふれた直後は勢いよく
                else t.age += dt;

                // 筋: 先端までの道を細い管に(上ほど細く、古いほど薄く=細く)
                float fade = t.fell ? Mathf.Clamp01(1f - t.age / Mathf.Max(fadeTime, 0.01f)) : 1f;
                int count = 0; float walked = 0; Vector3 headPos = worldPos[t.path[0]], headDir = Vector3.down;
                for (int i = 0; i < t.path.Count && count < pts.Length; i++)
                {
                    int vi = t.path[i];
                    Vector3 p = worldPos[vi] + worldNrm[vi] * surfaceOffset;
                    if (i > 0)
                    {
                        Vector3 prev = worldPos[t.path[i - 1]] + worldNrm[t.path[i - 1]] * surfaceOffset;
                        float seg = Vector3.Distance(prev, p);
                        if (walked + seg >= t.head) { float k = seg > 1e-6f ? (t.head - walked) / seg : 0f; p = Vector3.Lerp(prev, p, k); headDir = (p - prev).normalized; pts[count] = p; rad[count] = streakRadius * fade; count++; headPos = p; break; }
                        walked += seg; headDir = (p - prev).normalized;
                    }
                    pts[count] = p; rad[count] = streakRadius * fade * Mathf.Lerp(0.5f, 1f, i / (float)t.path.Count); count++; headPos = p;
                }
                for (int pass = 0; pass < 2; pass++)
                    for (int i = 1; i < count - 1; i++) pts[i] = pts[i] * 0.5f + (pts[i - 1] + pts[i + 1]) * 0.25f;
                if (fade > 0.01f && count >= 2) tube.AddTube(pts, rad, count, 5);

                if (pooling)
                {
                    // まぶたのふちに溜まる: だんだんふくらみ、ふちに沿って横長に。あふれる直前に小さく震える
                    float k = Mathf.Clamp01(t.pooled / Mathf.Max(poolTime, 0.01f));
                    float r = dropRadius * poolSize * Mathf.Lerp(0.25f, 1f, Mathf.SmoothStep(0f, 1f, k)) * (1f + (k > 0.85f ? 0.06f * Mathf.Sin(time * 40f) : 0f));
                    Vector3 p0 = worldPos[t.path[0]], n0 = worldNrm[t.path[0]];
                    Vector3 lid = Vector3.Cross(n0, Vector3.up).normalized;           // ふちの向き(肌に沿った横)
                    if (viscosity > 0f) tube.AddDrop(p0 + n0 * (surfaceOffset + r * 0.1f), r, lid, 1.9f); // 粘る液: 面に沿って平たく広がる
                    else tube.AddDrop(p0 + n0 * (surfaceOffset + r * 0.3f), r, lid, 1.5f);
                }
                else if (!t.fell)
                {
                    tube.AddDrop(headPos, dropRadius, headDir, 1.4f);          // 流れている先の粒
                    if (t.head >= t.length) { t.fell = true; t.fallPos = headPos; t.fallVel = headDir * speed; }
                }
                else if (viscosity > 0f && !t.snapped)
                {
                    // どろどろ: 先端にぶら下がったまま、糸状にゆっくり伸びる。限界まで伸びたら切れて落ちる
                    t.fallPos += Vector3.down * (speed * (1.5f - viscosity) * dt);
                    float hang = Vector3.Distance(headPos, t.fallPos);
                    float k = Mathf.Clamp01(hang / Mathf.Max(strandLength, 1e-4f));
                    pts[0] = headPos; pts[1] = Vector3.Lerp(headPos, t.fallPos, 0.2f); pts[2] = Vector3.Lerp(headPos, t.fallPos, 0.6f); pts[3] = t.fallPos;
                    rad[0] = Mathf.Lerp(dropRadius * 0.6f, streakRadius, k);                      // 接続部は細い糸(伸びるほど細く)
                    rad[1] = Mathf.Lerp(dropRadius * 0.45f, streakRadius * 0.7f, k);
                    rad[2] = Mathf.Lerp(dropRadius * 0.4f, streakRadius * 0.6f, k);
                    rad[3] = dropRadius * 0.45f;
                    tube.AddTube(pts, rad, 4, 8);
                    tube.AddDrop(t.fallPos, dropRadius * (0.7f + 0.3f * k), Vector3.down, 1.4f);   // 下にたまった小さな玉
                    if (hang >= strandLength) { t.snapped = true; t.fallVel = Vector3.down * speed; }
                    t.age = 0f; // 筋はぶら下がっている間は消さない
                }
                else
                {
                    // 落ちる 1 粒(重力で加速、少し伸びる)
                    t.fallVel += Physics.gravity * dt;
                    t.fallPos += t.fallVel * dt;
                    if (t.age < 1.2f) tube.AddDrop(t.fallPos, dropRadius * (viscosity > 0f ? 0.9f : 1.1f), t.fallVel, Mathf.Lerp(1.2f, 1.8f, Mathf.Clamp01(t.fallVel.magnitude / 3f)));
                    if (t.age >= Mathf.Max(fadeTime, 1.2f)) tears.RemoveAt(ti);
                }
            }
            tube.End();
        }

        /// <summary>目のボーン(Humanoid の LeftEye/RightEye、または名前に Eye)の少し下・前にある顔の頂点を選ぶ。</summary>
        [ContextMenu("涙の出始めを自動で探す")]
        public void FindStartVertices()
        {
            if (face == null) return;
            if (neighbors == null) { startVertices = new int[0]; Prepare(); return; } // つながりを作ってから戻ってくる
            var eyes = new List<Transform>();
            var animator = face.GetComponentInParent<Animator>();
            if (animator != null && animator.isHuman)
                foreach (var b in new[] { HumanBodyBones.LeftEye, HumanBodyBones.RightEye }) { var t = animator.GetBoneTransform(b); if (t != null) eyes.Add(t); }
            if (eyes.Count == 0)
                foreach (Transform t in face.transform.root.GetComponentsInChildren<Transform>(true))
                    if (t.name.Contains("Eye") && t.GetComponent<Renderer>() == null && (t.name.Contains("_L_") || t.name.Contains("_R_") || t.name.Contains("Left") || t.name.Contains("Right"))) eyes.Add(t);
            if (eyes.Count == 0) { Debug.LogWarning("[Tears] 目のボーンが見つかりません。startVertices を手で入れてください。"); return; }
            Bake();
            Transform root = face.transform.root;
            var result = new List<int>();
            Mesh src = baked;   // Bake() 済み。元メッシュは Read/Write OFF だと読めない
            foreach (Transform eye in eyes)
            {
                // 目の本当の位置: 目のボーンと同じ側・同じ高さ(±3cm)にある肌以外の頂点(白目など)の範囲。ボーンは眼球の奥・内寄りにあるため
                Vector3 side = Vector3.Project(eye.position - root.position, root.right);
                Vector3 min = Vector3.positiveInfinity, max = Vector3.negativeInfinity;
                for (int sm = 0; sm < src.subMeshCount; sm++)
                {
                    if (sm == skinSubMesh) continue;
                    foreach (int i in src.GetTriangles(sm))
                        if (Vector3.Dot(worldPos[i] - root.position, side) > 0f && Mathf.Abs(Vector3.Dot(worldPos[i] - eye.position, root.up)) < 0.03f) { min = Vector3.Min(min, worldPos[i]); max = Vector3.Max(max, worldPos[i]); }
                }
                Vector3 target;
                if (float.IsInfinity(min.x)) target = eye.position - root.up * 0.02f + root.forward * 0.05f;
                else
                {
                    // 白目の範囲から、出る所を決める(バインドポーズ=ワールドの軸に沿って立っている前提)
                    float outerX = side.x > 0f ? max.x : min.x, innerX = side.x > 0f ? min.x : max.x;
                    float lowY = min.y + (max.y - min.y) * 0.2f;
                    target = source == Source.OuterCorner ? new Vector3(Mathf.Lerp(outerX, innerX, 0.08f), lowY, max.z)
                           : source == Source.InnerCorner ? new Vector3(Mathf.Lerp(innerX, outerX, 0.08f), lowY, max.z)
                           : new Vector3((min.x + max.x) * 0.5f, min.y - 0.002f, max.z);   // 下まぶたのすぐ下、目の中ほど
                }
                int best = -1; float bestD = float.MaxValue;
                for (int i = 0; i < worldPos.Length; i++)
                {
                    if (neighbors[i].Count == 0) continue;                           // 肌以外(白目など)
                    if (Vector3.Dot(worldNrm[i], root.forward) < 0.3f) continue;    // 前を向いた面(頬)
                    float d = Vector3.ProjectOnPlane(worldPos[i] - target, root.forward).sqrMagnitude; // 正面から見た位置だけで選ぶ(奥行きは前向きの面で絞る)
                    if (d < bestD) { bestD = d; best = i; }
                }
                if (best >= 0) result.Add(best);
            }
            startVertices = result.ToArray();
            foundFor = source;
        }

        private static void DestroyObj(Object o)
        {
            if (Application.isPlaying) Destroy(o); else DestroyImmediate(o);
        }
    }
}
