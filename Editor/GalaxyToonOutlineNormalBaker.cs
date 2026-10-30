using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace GalaxyToon.Editor
{
    /// <summary>輪郭線用の「なめらか法線」をメッシュに焼く。同じ位置の頂点(UV や硬い角で分かれた頂点)の法線を面積・角度で平均し、
    /// 各頂点の接空間に直して UV8(TEXCOORD7)へ入れる。輪郭線パスはこれで押し出すので、角で線が割れない・途切れない。
    /// 元のメッシュ(FBX)は書き換えず、複製を OutlineNormals/ に保存して Renderer に差し替える。頂点の並びは変えない(頂点番号を使う機能はそのまま動く)。</summary>
    public static class GalaxyToonOutlineNormalBaker
    {
        private const int Channel = 7;

        [MenuItem("Tools/GalaxyToon/輪郭線用のなめらか法線を焼く (選択中のプレハブ・オブジェクト)")]
        private static void BakeSelected()
        {
            int n = 0;
            foreach (GameObject go in Selection.gameObjects) n += Bake(go);
            Debug.Log($"[輪郭線] {n} 個のメッシュに なめらか法線を焼きました。");
        }

        /// <summary>go(プレハブアセットでも可)の中の、GalaxyShader/Toon を使う Renderer のメッシュを焼いて差し替える。焼いた数を返す。
        /// folder を省くと、プレハブの隣(シーンの物なら Assets)の OutlineNormals/ に保存する。</summary>
        public static int Bake(GameObject go, string folder = null)
        {
            string assetPath = AssetDatabase.GetAssetPath(go);
            bool isPrefabAsset = !string.IsNullOrEmpty(assetPath) && assetPath.EndsWith(".prefab");
            GameObject root = isPrefabAsset ? PrefabUtility.LoadPrefabContents(assetPath) : go;
            if (string.IsNullOrEmpty(folder)) folder = Path.Combine(Path.GetDirectoryName(string.IsNullOrEmpty(assetPath) ? "Assets/x" : assetPath), "OutlineNormals").Replace('\\', '/');
            int count = 0;
            try
            {
                foreach (Renderer r in root.GetComponentsInChildren<Renderer>(true))
                {
                    bool toon = false;
                    foreach (Material m in r.sharedMaterials) if (m != null && m.shader != null && m.shader.name == "GalaxyShader/Toon") toon = true;
                    if (!toon) continue;
                    Mesh src = r is SkinnedMeshRenderer smr ? smr.sharedMesh : r.GetComponent<MeshFilter>()?.sharedMesh;
                    if (src == null || (src.hideFlags & HideFlags.DontSave) != 0) continue; // 実行時に作る一時メッシュ(涙・糸)は焼かない
                    Mesh baked = BakeMesh(src);
                    if (!AssetDatabase.IsValidFolder(folder)) { Directory.CreateDirectory(folder); AssetDatabase.ImportAsset(folder); }
                    string path = $"{folder}/{SafeName(src.name)}.asset";
                    var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
                    if (existing != null) { EditorUtility.CopySerialized(baked, existing); Object.DestroyImmediate(baked); baked = existing; }
                    else AssetDatabase.CreateAsset(baked, path);
                    if (!isPrefabAsset) Undo.RecordObject(r is SkinnedMeshRenderer ? (Object)r : r.GetComponent<MeshFilter>(), "なめらか法線を焼く");
                    if (r is SkinnedMeshRenderer s2) s2.sharedMesh = baked; else r.GetComponent<MeshFilter>().sharedMesh = baked;
                    count++;
                }
                if (isPrefabAsset) PrefabUtility.SaveAsPrefabAsset(root, assetPath);
                AssetDatabase.SaveAssets();
            }
            finally
            {
                if (isPrefabAsset) PrefabUtility.UnloadPrefabContents(root);
            }
            return count;
        }

        private static string SafeName(string name)
        {
            foreach (char c in Path.GetInvalidFileNameChars()) name = name.Replace(c, '_');
            return name;
        }

        /// <summary>src の複製に、なめらか法線(接空間)を UV8 に入れて返す。src が焼いた物ならそのまま焼き直す。</summary>
        public static Mesh BakeMesh(Mesh src)
        {
            Mesh mesh = Object.Instantiate(src);
            mesh.name = src.name;
            Vector3[] v = mesh.vertices;
            Vector3[] n = mesh.normals;
            if (n == null || n.Length != v.Length) { mesh.RecalculateNormals(); n = mesh.normals; }
            Vector4[] t = mesh.tangents;
            if (t == null || t.Length != v.Length) { mesh.RecalculateTangents(); t = mesh.tangents; }

            // 同じ位置の頂点をまとめ、面の法線を角度で重みづけして足す(全サブメッシュ)
            var group = new int[v.Length];
            var keyToGroup = new Dictionary<Vector3Int, int>();
            for (int i = 0; i < v.Length; i++)
            {
                var key = Vector3Int.RoundToInt(v[i] * 100000f);
                if (!keyToGroup.TryGetValue(key, out int g)) { g = i; keyToGroup[key] = g; }
                group[i] = g;
            }
            var smooth = new Vector3[v.Length];
            for (int sm = 0; sm < mesh.subMeshCount; sm++)
            {
                int[] tri = mesh.GetTriangles(sm);
                for (int k = 0; k < tri.Length; k += 3)
                {
                    int a = tri[k], b = tri[k + 1], c = tri[k + 2];
                    Vector3 fn = Vector3.Cross(v[b] - v[a], v[c] - v[a]);
                    if (fn.sqrMagnitude < 1e-20f) continue;
                    fn.Normalize();
                    smooth[group[a]] += fn * Vector3.Angle(v[b] - v[a], v[c] - v[a]);
                    smooth[group[b]] += fn * Vector3.Angle(v[c] - v[b], v[a] - v[b]);
                    smooth[group[c]] += fn * Vector3.Angle(v[a] - v[c], v[b] - v[c]);
                }
            }
            var ts = new List<Vector3>(v.Length);
            for (int i = 0; i < v.Length; i++)
            {
                Vector3 s = smooth[group[i]];
                s = s.sqrMagnitude > 1e-12f ? s.normalized : n[i];
                Vector3 tangent = new Vector3(t[i].x, t[i].y, t[i].z);
                Vector3 bitangent = Vector3.Cross(n[i], tangent) * (t[i].w >= 0f ? 1f : -1f);
                ts.Add(new Vector3(Vector3.Dot(s, tangent), Vector3.Dot(s, bitangent), Vector3.Dot(s, n[i])));
            }
            mesh.SetUVs(Channel, ts);
            return mesh;
        }
    }
}
