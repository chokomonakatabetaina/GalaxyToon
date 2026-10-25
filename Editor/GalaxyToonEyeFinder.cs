using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace GalaxyToon.Editor
{
    /// <summary>瞳の範囲(UV の長方形)を自動で探して、顔のマテリアルの _EyeRect1/_EyeRect2 に入れる。
    /// 瞳は顔テクスチャの中の独立した円盤(メッシュの島)として作られていることが多いので、
    /// 「白目のサブメッシュの範囲の中に丸ごと収まる、顔サブメッシュの島」を瞳とみなし、その UV の範囲を使う。</summary>
    public static class GalaxyToonEyeFinder
    {
        [MenuItem("Tools/GalaxyToon/瞳の範囲を探す (選択中のキャラ)")]
        private static void FindSelected()
        {
            int n = 0;
            foreach (GameObject go in Selection.gameObjects)
                foreach (SkinnedMeshRenderer smr in go.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                    if (TryFind(smr, out Material face, out Vector4 r1, out Vector4 r2))
                    {
                        Undo.RecordObject(face, "瞳の範囲");
                        face.SetVector("_EyeRect1", r1); face.SetVector("_EyeRect2", r2);
                        face.SetFloat("_UseEye", 1f);
                        GalaxyToonGUI.Apply(face);
                        Debug.Log($"[瞳] {smr.name}: {face.name} 瞳1 {r1} 瞳2 {r2}", face);
                        n++;
                    }
            if (n == 0) Debug.LogWarning("[瞳] 見つかりませんでした。顔(肌)と白目が同じ SkinnedMeshRenderer の別サブメッシュで、白目のマテリアル名に Sclera か Eye が入っている必要があります。");
        }

        [MenuItem("Tools/GalaxyToon/頬の位置を探す (選択中のキャラ)")]
        private static void FindCheeksSelected()
        {
            int n = 0;
            foreach (GameObject go in Selection.gameObjects)
                foreach (SkinnedMeshRenderer smr in go.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                    if (TryFindCheeks(smr, out Material skin, out Vector4 uv, out float size))
                    {
                        Undo.RecordObject(skin, "頬の位置");
                        skin.SetVector("_BlushUV", uv); skin.SetFloat("_BlushSize", size);
                        Debug.Log($"[頬] {smr.name}: {skin.name} 頬 {uv} 大きさ {size:F3}", skin);
                        n++;
                    }
            if (n == 0) Debug.LogWarning("[頬] 見つかりませんでした。白目(EyeWhite/Sclera)と肌(Skin/Face)が同じ SkinnedMeshRenderer の別サブメッシュである必要があります。");
        }

        /// <summary>白目の少し下・外側で前を向いた肌の頂点を頬とみなし、その UV と、頬の大きさ(UV)を返す。</summary>
        public static bool TryFindCheeks(SkinnedMeshRenderer smr, out Material skin, out Vector4 uv, out float size)
        {
            skin = null; uv = Vector4.zero; size = 0.06f;
            Mesh mesh = smr.sharedMesh; Material[] mats = smr.sharedMaterials;
            if (mesh == null || mats.Length < 2) return false;
            int sclera = -1, skinSub = -1;
            for (int i = 0; i < mats.Length; i++)
            {
                if (mats[i] == null) continue;
                string n = mats[i].name.ToLowerInvariant();
                if (sclera < 0 && (n.Contains("sclera") || n.Contains("eyewhite") || n.Contains("白目"))) sclera = i;
                bool notSkin = n.Contains("eye") || n.Contains("brow") || n.Contains("lash") || n.Contains("line") || n.Contains("mouth") || n.Contains("oral") || n.Contains("tongue") || n.Contains("teeth") || n.Contains("hair");
                if (skinSub < 0 && mats[i].HasProperty("_BlushUV") && !notSkin && (n.Contains("skin") || n.Contains("face") || n.Contains("肌") || n.Contains("顔"))) skinSub = i;
            }
            if (sclera < 0 || skinSub < 0) return false;
            var baked = new Mesh(); smr.BakeMesh(baked, true);
            Vector3[] v = baked.vertices; Vector3[] nrm = baked.normals; Vector2[] uvs = mesh.uv;
            Matrix4x4 m = smr.transform.localToWorldMatrix;
            Transform root = smr.transform.root;
            int[] skinTri = mesh.GetTriangles(skinSub);
            var result = new float[4]; int found = 0;
            foreach (float side in new[] { 1f, -1f })
            {
                Bounds eye = default; bool has = false;
                foreach (int i in mesh.GetTriangles(sclera))
                {
                    Vector3 p = m.MultiplyPoint3x4(v[i]);
                    if (Vector3.Dot(p - root.position, root.right) * side <= 0f) continue;
                    if (has) eye.Encapsulate(p); else { eye = new Bounds(p, Vector3.zero); has = true; }
                }
                if (!has) continue;
                // 頬 = 目の中心から、目の高さ 1.8 個ぶん下、少し外側
                Vector3 target = eye.center - root.up * (eye.size.y * 1.8f + 0.005f) + root.right * side * eye.size.x * 0.15f;
                int Nearest(Vector3 t)
                {
                    int best = -1; float bd = float.MaxValue;
                    foreach (int i in skinTri)
                    {
                        if (Vector3.Dot(m.MultiplyVector(nrm[i]), root.forward) < 0.2f) continue; // 前を向いた面だけ
                        Vector3 p = m.MultiplyPoint3x4(v[i]); p += root.forward * Vector3.Dot(t - p, root.forward); // 前後は無視
                        float d = (p - t).sqrMagnitude; if (d < bd) { bd = d; best = i; }
                    }
                    return best;
                }
                int c = Nearest(target), c2 = Nearest(target + root.right * side * 0.015f);
                if (c < 0 || c2 < 0) continue;
                result[found * 2] = uvs[c].x; result[found * 2 + 1] = uvs[c].y; found++;
                size = Mathf.Max(size, Vector2.Distance(uvs[c], uvs[c2]) / 0.015f * 0.022f); // 1.5cm の UV 距離から、頬の半径 2.2cm を UV に直す
            }
            Object.DestroyImmediate(baked);
            if (found == 0) return false;
            if (found == 1) { result[2] = result[0]; result[3] = result[1]; }
            skin = mats[skinSub]; uv = new Vector4(result[0], result[1], result[2], result[3]);
            return true;
        }

        /// <summary>smr の中から顔(肌)と白目のサブメッシュを見つけ、瞳2つの UV 範囲を返す。</summary>
        public static bool TryFind(SkinnedMeshRenderer smr, out Material face, out Vector4 rect1, out Vector4 rect2)
        {
            face = null; rect1 = rect2 = Vector4.zero;
            Mesh mesh = smr.sharedMesh;
            Material[] mats = smr.sharedMaterials;
            if (mesh == null || mats.Length < 2) return false;
            int sclera = -1;
            for (int i = 0; i < mats.Length; i++)
            {
                if (mats[i] == null) continue;
                string n = mats[i].name.ToLowerInvariant();
                bool white = n.Contains("sclera") || n.Contains("eyewhite") || n.Contains("白目");
                bool eyeOther = n.Contains("iris") || n.Contains("highlight") || n.Contains("line") || n.Contains("lash") || n.Contains("brow") || n.Contains("extra");
                if (white || (n.Contains("eye") && !eyeOther && sclera < 0)) sclera = i;
                if (white) break;
            }
            if (sclera < 0) return false;
            int irisSub = -1;
            for (int i = 0; i < mats.Length; i++)
            {
                if (mats[i] == null || !mats[i].HasProperty("_EyeRect1")) continue;
                string n = mats[i].name.ToLowerInvariant();
                if (n.Contains("iris") || n.Contains("pupil") || n.Contains("瞳")) irisSub = i;
            }
            // 瞳は肌と同じサブメッシュ(顔テクスチャに描かれている)か、別のサブメッシュ(瞳だけのメッシュなど)。白目以外を全部調べる

            var baked = new Mesh();
            smr.BakeMesh(baked, true);
            Vector3[] v = baked.vertices; Vector2[] uv = mesh.uv; Vector3[] vOrig = mesh.vertices;
            Matrix4x4 m = smr.transform.localToWorldMatrix;
            Vector3 root = smr.transform.root.position;
            Vector3 right = smr.transform.root.right;
            var rects = new List<Vector4>();
            int skin = -1;
            foreach (float side in new[] { 1f, -1f })
            {
                // 白目の範囲(この側)
                Bounds eye = default; bool has = false;
                foreach (int i in mesh.GetTriangles(sclera))
                {
                    Vector3 p = m.MultiplyPoint3x4(v[i]);
                    if (Vector3.Dot(p - root, right) * side <= 0f) continue;
                    if (has) eye.Encapsulate(p); else { eye = new Bounds(p, Vector3.zero); has = true; }
                }
                if (!has) continue;
                eye.Expand(0.004f);
                eye.Encapsulate(eye.center + smr.transform.root.forward * 0.04f); // 瞳は白目より少し手前(角膜側)にあるので、前方向に余裕を持たせる
                // 白目以外の三角形を島(頂点のつながり)に分け、白目の範囲に丸ごと入る島の UV 範囲をまとめる
                var triList = new List<int>(); var triSub = new List<int>();
                for (int sm = 0; sm < mesh.subMeshCount; sm++) { if (sm == sclera || mats[sm] == null || !mats[sm].HasProperty("_EyeRect1") || (irisSub >= 0 && sm != irisSub)) continue; int[] st = mesh.GetTriangles(sm); triList.AddRange(st); for (int k = 0; k < st.Length; k++) triSub.Add(sm); }
                int[] tri = triList.ToArray();
                // 同じ位置・同じ UV の頂点は1つとみなしてつなぐ(三角形ごとに頂点が分かれたメッシュでも島になる。UV の継ぎ目では分かれる)
                var weld = new Dictionary<(Vector3Int, Vector2Int), int>();
                int Key(int i) { var k = (Vector3Int.RoundToInt(vOrig[i] * 100000f), Vector2Int.RoundToInt(uv[i] * 10000f)); if (!weld.TryGetValue(k, out int w)) { w = i; weld[k] = w; } return w; }
                var parent = new Dictionary<int, int>();
                int Find(int a) { while (parent.TryGetValue(a, out int p) && p != a) { a = p; } return a; }
                void Union(int a, int b) { a = Find(a); b = Find(b); if (a != b) parent[a] = b; }
                var welded = new int[tri.Length];
                for (int t = 0; t < tri.Length; t++) { welded[t] = Key(tri[t]); parent.TryAdd(welded[t], welded[t]); }
                for (int t = 0; t < tri.Length; t += 3) { Union(welded[t], welded[t + 1]); Union(welded[t], welded[t + 2]); }
                var inside = new Dictionary<int, bool>();
                var uvMin = new Dictionary<int, Vector2>(); var uvMax = new Dictionary<int, Vector2>();
                var wMin = new Dictionary<int, Vector3>(); var wMax = new Dictionary<int, Vector3>();
                var subOf = new Dictionary<int, int>();
                for (int t = 0; t < tri.Length; t++)
                {
                    int i = tri[t];
                    int g = Find(welded[t]);
                    subOf[g] = triSub[t];
                    Vector3 p = m.MultiplyPoint3x4(v[i]);
                    bool ok = irisSub >= 0 ? Vector3.Dot(p - root, right) * side > 0f : eye.Contains(p); // 瞳メッシュがあるときは左右の側だけで判定
                    inside[g] = inside.TryGetValue(g, out bool prev) ? prev && ok : ok;
                    uvMin[g] = uvMin.TryGetValue(g, out Vector2 a) ? Vector2.Min(a, uv[i]) : uv[i];
                    uvMax[g] = uvMax.TryGetValue(g, out Vector2 b) ? Vector2.Max(b, uv[i]) : uv[i];
                    wMin[g] = wMin.TryGetValue(g, out Vector3 c) ? Vector3.Min(c, p) : p;
                    wMax[g] = wMax.TryGetValue(g, out Vector3 d) ? Vector3.Max(d, p) : p;
                }
                // 白目の範囲に丸ごと入る島のうち、正面から見た面積が最大のもの = 瞳の円盤(まつ毛などの細い島は外れる)
                int best = -1; float bestArea = 0f;
                foreach (var kv in inside)
                {
                    if (!kv.Value) continue;
                    Vector3 size = wMax[kv.Key] - wMin[kv.Key];
                    float area = Vector3.ProjectOnPlane(size, smr.transform.root.forward).sqrMagnitude;
                    string mn = mats[subOf[kv.Key]].name.ToLowerInvariant();
                    if (mn.Contains("iris") || mn.Contains("pupil") || mn.Contains("瞳")) area *= 100f; // 瞳のマテリアルがあればそれを優先(まつ毛の島より)
                    if (area > bestArea) { bestArea = area; best = kv.Key; }
                }
                if (best >= 0) { rects.Add(new Vector4(uvMin[best].x, uvMin[best].y, uvMax[best].x, uvMax[best].y)); skin = subOf[best]; }
            }
            Object.DestroyImmediate(baked);
            if (rects.Count == 0 || skin < 0) return false;
            face = mats[skin];
            rect1 = rects[0];
            rect2 = rects.Count > 1 ? rects[1] : rects[0];
            return true;
        }
    }
}
