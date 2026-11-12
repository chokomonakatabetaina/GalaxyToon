using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace GalaxyToon.Editor
{
    /// <summary>
    /// SSS(表面下散乱・逆光透け)用の「厚みマップ(_ThicknessMap)」をメッシュから自動で作る。
    /// 各頂点から法線の内側(-Normal)へ複数の光線(コーン状のレイ)を飛ばして反対側の面までの距離(肉厚)を測り、
    /// 耳たぶ・鼻先・指先・薄い布などの「薄い所 = 白(1)」、胴体・頭の中心などの「厚い所 = 黒/暗め」として UV 空間へ焼く。
    /// </summary>
    public sealed class GalaxyToonThicknessBaker : EditorWindow
    {
        private Renderer _target;
        private int _materialIndex;
        private int _resolution = 512;
        private float _maxThicknessCm = 4.5f;   // これ以上の厚みは「厚い(基本透け値)」とみなす距離(cm)
        private float _baseSkinValue = 0.25f;   // 厚い肌でも明暗境界の血色がうっすら乗る最低値(0 で完全に薄い部位だけ)
        private float _smoothness = 0.35f;      // 頂点ごとの厚みを隣の頂点となめらかに平均する強さ
        private string _savePath = "";
        private bool _applyToMaterial = true;
        private Texture2D _preview;
        private string _message;

        private static readonly int[] Resolutions = { 256, 512, 1024, 2048 };

        [MenuItem("Tools/GalaxyToon/厚みマップを作る (SSS・肌透け用)", false, 42)]
        public static void Open()
        {
            GetWindow<GalaxyToonThicknessBaker>("厚みマップ (SSS)");
        }

        private void OnSelectionChange()
        {
            if (Selection.activeGameObject != null
                && Selection.activeGameObject.TryGetComponent(out Renderer renderer)
                && renderer != _target)
            {
                _target = renderer;
                _materialIndex = 0;
                _savePath = "";
                Repaint();
            }
        }

        private void OnGUI()
        {
            EditorGUILayout.HelpBox(
                "メッシュの肉厚を測り、SSS 用の厚みマップ(白い所 = 耳・鼻先・指先など薄く透ける部位)を作ります。\nヒエラルキーで Renderer を選ぶか、下に入れてください。",
                MessageType.Info);
            _target = (Renderer)EditorGUILayout.ObjectField(new GUIContent("対象の Renderer"), _target, typeof(Renderer), true);
            if (_target == null)
            {
                return;
            }

            Material[] mats = _target.sharedMaterials;
            var names = new string[mats.Length];
            for (int i = 0; i < mats.Length; i++)
            {
                names[i] = $"{i}: {(mats[i] != null ? mats[i].name : "(なし)")}";
            }
            int newIndex = EditorGUILayout.Popup(
                new GUIContent("マテリアル(サブメッシュ)", "このマテリアルが使う部分だけで作ります。"),
                Mathf.Clamp(_materialIndex, 0, Mathf.Max(0, mats.Length - 1)),
                names);
            if (newIndex != _materialIndex)
            {
                _materialIndex = newIndex;
                _savePath = "";
            }

            _resolution = EditorGUILayout.IntPopup(
                new GUIContent("解像度"),
                _resolution,
                System.Array.ConvertAll(Resolutions, r => new GUIContent(r.ToString())),
                Resolutions);
            _maxThicknessCm = EditorGUILayout.Slider(
                new GUIContent("薄い部位とみなす厚み (cm)", "表面から反対側の面までの厚みがこれより薄い場所(耳・指先・鼻先)が白くなります。"),
                _maxThicknessCm, 0.5f, 15f);
            _baseSkinValue = EditorGUILayout.Slider(
                new GUIContent("厚い部分の最低値", "頬や胴体など厚い部分にも「影の境目の血色」を残す強さ。逆光の耳透けだけにするなら 0〜0.1。"),
                _baseSkinValue, 0f, 0.8f);
            _smoothness = EditorGUILayout.Slider(
                new GUIContent("なめらかさ", "頂点どうしで厚みをぼかして、ポリゴンの角を目立たなくします。"),
                _smoothness, 0f, 1f);

            using (new EditorGUILayout.HorizontalScope())
            {
                if (string.IsNullOrEmpty(_savePath))
                {
                    _savePath = DefaultPath(mats, _materialIndex);
                }
                _savePath = EditorGUILayout.TextField(new GUIContent("保存先"), _savePath);
                if (GUILayout.Button("選ぶ", GUILayout.Width(44f)))
                {
                    string dir = Path.GetDirectoryName(_savePath);
                    string picked = EditorUtility.SaveFilePanelInProject(
                        "厚みマップの保存先",
                        Path.GetFileNameWithoutExtension(_savePath),
                        "png",
                        "保存先を選んでください。",
                        dir);
                    if (!string.IsNullOrEmpty(picked))
                    {
                        _savePath = picked;
                    }
                }
            }
            _applyToMaterial = EditorGUILayout.Toggle(
                new GUIContent("マテリアルに反映", "作ったら、このマテリアルの「厚み・SSSマスク」に入れて SSS を ON にします。"),
                _applyToMaterial);

            EditorGUILayout.Space(6f);
            if (GUILayout.Button("作成", GUILayout.Height(28f)))
            {
                Bake();
            }
            if (!string.IsNullOrEmpty(_message))
            {
                EditorGUILayout.HelpBox(_message, MessageType.None);
            }
            if (_preview != null)
            {
                Rect rect = GUILayoutUtility.GetAspectRect(1f, GUILayout.MaxWidth(320f));
                EditorGUI.DrawPreviewTexture(rect, _preview, null, ScaleMode.ScaleToFit);
            }
        }

        private static string DefaultPath(Material[] mats, int index)
        {
            Material m = index < mats.Length ? mats[index] : null;
            string matPath = m != null ? AssetDatabase.GetAssetPath(m) : "";
            string dir = string.IsNullOrEmpty(matPath) ? "Assets" : Path.GetDirectoryName(matPath).Replace('\\', '/');
            return $"{dir}/{(m != null ? m.name : "Mesh")}_Thickness.png";
        }

        private Mesh SourceMesh()
        {
            return _target is SkinnedMeshRenderer smr ? smr.sharedMesh : _target.GetComponent<MeshFilter>()?.sharedMesh;
        }

        private void Bake()
        {
            Mesh mesh = SourceMesh();
            if (mesh == null)
            {
                _message = "メッシュが見つかりません。";
                return;
            }
            if (_materialIndex >= mesh.subMeshCount)
            {
                _message = "このマテリアルに対応するサブメッシュがありません。";
                return;
            }

            float scale = _target.transform.lossyScale.magnitude / Mathf.Sqrt(3f);
            float maxDistInMesh = (_maxThicknessCm * 0.01f) / Mathf.Max(scale, 1e-4f);
            Color[] pixels;
            try { pixels = GalaxyToonThickness.Bake(mesh, _materialIndex, _resolution, maxDistInMesh, _baseSkinValue, _smoothness); }
            catch (System.ArgumentException e) { _message = e.Message; return; }

            var tex = new Texture2D(_resolution, _resolution, TextureFormat.RGBA32, false, true);
            tex.SetPixels(pixels);
            tex.Apply();

            string full = Path.GetFullPath(_savePath);
            Directory.CreateDirectory(Path.GetDirectoryName(full));
            File.WriteAllBytes(full, tex.EncodeToPNG());
            DestroyImmediate(tex);
            AssetDatabase.ImportAsset(_savePath);
            if (AssetImporter.GetAtPath(_savePath) is TextureImporter importer)
            {
                importer.sRGBTexture = false;
                importer.wrapMode = TextureWrapMode.Clamp;
                importer.SaveAndReimport();
            }
            _preview = AssetDatabase.LoadAssetAtPath<Texture2D>(_savePath);

            Material mat = _materialIndex < _target.sharedMaterials.Length ? _target.sharedMaterials[_materialIndex] : null;
            if (_applyToMaterial && mat != null && mat.HasProperty("_ThicknessMap"))
            {
                Undo.RecordObject(mat, "厚みマップを反映");
                mat.SetTexture("_ThicknessMap", _preview);
                mat.SetFloat("_UseSss", 1f);
                GalaxyToonGUI.Apply(mat);
                EditorUtility.SetDirty(mat);
                AssetDatabase.SaveAssets();
                _message = $"作りました: {_savePath}\n「{mat.name}」の SSS に反映しました。";
            }
            else
            {
                _message = $"作りました: {_savePath}";
            }
        }
    }

    /// <summary>厚みマップの計算(エディタ外やテストからも呼べるように分けてある)。</summary>
    public static class GalaxyToonThickness
    {
        public static Color[] Bake(Mesh mesh, int subMesh, int size, float maxDistance, float baseValue, float smoothness)
        {
            return Prepare(mesh, subMesh, size, maxDistance, baseValue, smoothness)();
        }

        /// <summary>
        /// メッシュから値を読む所だけをいまのスレッド(メインスレッド)で行い、残りの計算を返す。
        /// 返した計算は Unity の API を使わないので、別のスレッドで動かせる(読み込み中の表示を止めない)。
        /// </summary>
        public static System.Func<Color[]> Prepare(Mesh mesh, int subMesh, int size, float maxDistance, float baseValue, float smoothness)
        {
            GalaxyToonMeshInput.Read(mesh, subMesh, out Vector3[] positions, out Vector3[] normals, out Vector2[] uvs, out int[] tris);
            return () => Compute(positions, normals, uvs, tris, size, maxDistance, baseValue, smoothness);
        }

        private static Color[] Compute(Vector3[] positions, Vector3[] normals, Vector2[] uvs, int[] tris, int size, float maxDistance, float baseValue, float smoothness)
        {
            var used = new HashSet<int>(tris);

            // 同じ位置の頂点をグループ化して法線と隣接頂点をまとめる
            var weld = new Dictionary<Vector3Int, int>();
            var vertexGroup = new int[positions.Length];
            var groupPos = new List<Vector3>();
            var groupNormals = new List<Vector3>();
            foreach (int i in used)
            {
                Vector3Int key = Vector3Int.RoundToInt(positions[i] * 10000f);
                if (!weld.TryGetValue(key, out int g))
                {
                    g = groupPos.Count;
                    weld[key] = g;
                    groupPos.Add(positions[i]);
                    groupNormals.Add(Vector3.zero);
                }
                vertexGroup[i] = g;
                groupNormals[g] += normals[i];
            }
            var neighbors = new List<int>[groupPos.Count];
            for (int g = 0; g < neighbors.Length; g++)
            {
                neighbors[g] = new List<int>();
            }
            for (int t = 0; t < tris.Length; t += 3)
            {
                for (int k = 0; k < 3; k++)
                {
                    int a = vertexGroup[tris[t + k]];
                    int b = vertexGroup[tris[t + (k + 1) % 3]];
                    if (a != b)
                    {
                        neighbors[a].Add(b);
                        neighbors[b].Add(a);
                    }
                }
            }

            // 各頂点グループから内向き(-Normal)のコーン(中心 + 4方向)へレイを飛ばし、サブメッシュの反対側の三角形までの最短距離から薄さを求める。
            // レイは maxDistance までしか見ないので、三角形を格子に分けて、レイの通る格子の三角形だけを調べる(全部を調べたのと同じ結果)。
            // 頂点グループごとに別々に計算できるので、CPU のコアに分けて並べて計算する
            var grid = new TriangleGrid(positions, tris, maxDistance);
            var thinness = new float[groupPos.Count];
            float invMax = 1f / Mathf.Max(maxDistance, 1e-4f);
            System.Threading.Tasks.Parallel.For(0, groupPos.Count, g =>
            {
                Vector3 p = groupPos[g];
                Vector3 n = groupNormals[g].sqrMagnitude > 1e-8f ? groupNormals[g].normalized : Vector3.up;
                Vector3 inward = -n;
                Vector3 t1 = Vector3.Cross(inward, Mathf.Abs(inward.y) < 0.9f ? Vector3.up : Vector3.right).normalized;
                Vector3 t2 = Vector3.Cross(inward, t1).normalized;

                Vector3 origin = p + inward * (maxDistance * 0.015f);
                Vector3[] dirs =
                {
                    inward,
                    (inward + t1 * 0.35f).normalized,
                    (inward - t1 * 0.35f).normalized,
                    (inward + t2 * 0.35f).normalized,
                    (inward - t2 * 0.35f).normalized,
                };

                float sumThin = 0f;
                for (int d = 0; d < dirs.Length; d++)
                {
                    float hitDist = grid.Raycast(origin, dirs[d], maxDistance, positions, tris);
                    float ratio = Mathf.Clamp01(1f - hitDist * invMax);
                    sumThin += ratio * ratio;
                }
                // 局所曲率(鼻先や指先など、周囲の頂点が法線の後ろ側にある凸形状ほど薄く透けやすい)も少し加味する
                float convex = 0f;
                if (neighbors[g].Count > 0)
                {
                    Vector3 avgNb = Vector3.zero;
                    foreach (int nb in neighbors[g])
                    {
                        avgNb += groupPos[nb];
                    }
                    avgNb /= neighbors[g].Count;
                    convex = Mathf.Clamp01(Vector3.Dot(p - avgNb, n) * invMax * 4f);
                }
                float rawThin = Mathf.Clamp01(sumThin / dirs.Length + convex * 0.35f);
                thinness[g] = Mathf.Lerp(baseValue, 1f, rawThin);
            });

            // 隣接頂点間でなめらかにする
            int iterations = Mathf.RoundToInt(smoothness * 16f);
            for (int it = 0; it < iterations; it++)
            {
                var next = new float[thinness.Length];
                for (int g = 0; g < thinness.Length; g++)
                {
                    float sum = thinness[g];
                    int cnt = 1;
                    foreach (int nb in neighbors[g])
                    {
                        sum += thinness[nb];
                        cnt++;
                    }
                    next[g] = sum / cnt;
                }
                thinness = next;
            }

            // UV 空間へ三角形をラスタライズ
            var pixels = new Color[size * size];
            var filled = new bool[size * size];
            for (int t = 0; t < tris.Length; t += 3)
            {
                int i0 = tris[t], i1 = tris[t + 1], i2 = tris[t + 2];
                Vector2 p0 = uvs[i0] * size, p1 = uvs[i1] * size, p2 = uvs[i2] * size;
                int minX = Mathf.Max(0, Mathf.FloorToInt(Mathf.Min(p0.x, Mathf.Min(p1.x, p2.x))));
                int maxX = Mathf.Min(size - 1, Mathf.CeilToInt(Mathf.Max(p0.x, Mathf.Max(p1.x, p2.x))));
                int minY = Mathf.Max(0, Mathf.FloorToInt(Mathf.Min(p0.y, Mathf.Min(p1.y, p2.y))));
                int maxY = Mathf.Min(size - 1, Mathf.CeilToInt(Mathf.Max(p0.y, Mathf.Max(p1.y, p2.y))));
                float area = (p1.x - p0.x) * (p2.y - p0.y) - (p2.x - p0.x) * (p1.y - p0.y);
                if (Mathf.Abs(area) < 1e-8f)
                {
                    continue;
                }
                float v0 = thinness[vertexGroup[i0]], v1 = thinness[vertexGroup[i1]], v2 = thinness[vertexGroup[i2]];
                for (int y = minY; y <= maxY; y++)
                {
                    for (int x = minX; x <= maxX; x++)
                    {
                        Vector2 p = new Vector2(x + 0.5f, y + 0.5f);
                        float w1 = ((p.x - p0.x) * (p2.y - p0.y) - (p2.x - p0.x) * (p.y - p0.y)) / area;
                        float w2 = ((p1.x - p0.x) * (p.y - p0.y) - (p.x - p0.x) * (p1.y - p0.y)) / area;
                        float w0 = 1f - w1 - w2;
                        if (w0 < -1e-4f || w1 < -1e-4f || w2 < -1e-4f)
                        {
                            continue;
                        }
                        float val = Mathf.Clamp01(v0 * w0 + v1 * w1 + v2 * w2);
                        int idx = y * size + x;
                        pixels[idx] = new Color(val, val, val, 1f);
                        filled[idx] = true;
                    }
                }
            }

            // UV の継ぎ目が出ないよう周囲の値でパディング
            int pad = Mathf.Max(4, size / 128);
            for (int pass = 0; pass < pad; pass++)
            {
                var add = new List<(int, Color)>();
                for (int y = 0; y < size; y++)
                {
                    for (int x = 0; x < size; x++)
                    {
                        int idx = y * size + x;
                        if (filled[idx])
                        {
                            continue;
                        }
                        Color sum = Color.clear;
                        int count = 0;
                        for (int dy = -1; dy <= 1; dy++)
                        {
                            for (int dx = -1; dx <= 1; dx++)
                            {
                                int nx = x + dx, ny = y + dy;
                                if (nx < 0 || ny < 0 || nx >= size || ny >= size)
                                {
                                    continue;
                                }
                                int n = ny * size + nx;
                                if (filled[n])
                                {
                                    sum += pixels[n];
                                    count++;
                                }
                            }
                        }
                        if (count > 0)
                        {
                            add.Add((idx, sum / count));
                        }
                    }
                }
                foreach (var (idx, c) in add)
                {
                    pixels[idx] = c;
                    filled[idx] = true;
                }
            }
            for (int i = 0; i < pixels.Length; i++)
            {
                if (!filled[i])
                {
                    pixels[i] = new Color(baseValue, baseValue, baseValue, 1f);
                }
            }
            return pixels;
        }

        /// <summary>三角形を入れた等間隔の格子(各三角形は、範囲の箱が重なる格子すべてに入れる)。</summary>
        private sealed class TriangleGrid
        {
            private readonly Vector3 min;
            private readonly float cell;
            private readonly int nx, ny, nz;
            private readonly Dictionary<int, List<int>> cells = new Dictionary<int, List<int>>();

            public TriangleGrid(Vector3[] vertices, int[] tris, float maxDistance)
            {
                var lo = Vector3.one * float.MaxValue;
                var hi = Vector3.one * float.MinValue;
                foreach (int i in tris)
                {
                    lo = Vector3.Min(lo, vertices[i]);
                    hi = Vector3.Max(hi, vertices[i]);
                }
                if (tris.Length == 0) { lo = hi = Vector3.zero; }
                var extent = hi - lo;
                float largest = Mathf.Max(extent.x, Mathf.Max(extent.y, extent.z));
                cell = Mathf.Max(Mathf.Max(maxDistance, largest / 96f), 1e-4f);
                min = lo;
                nx = Mathf.Clamp(Mathf.FloorToInt(extent.x / cell) + 1, 1, 256);
                ny = Mathf.Clamp(Mathf.FloorToInt(extent.y / cell) + 1, 1, 256);
                nz = Mathf.Clamp(Mathf.FloorToInt(extent.z / cell) + 1, 1, 256);
                for (int t = 0; t < tris.Length; t += 3)
                {
                    var a = vertices[tris[t]];
                    var b = vertices[tris[t + 1]];
                    var c = vertices[tris[t + 2]];
                    Range(Vector3.Min(a, Vector3.Min(b, c)), Vector3.Max(a, Vector3.Max(b, c)), out var x0, out var y0, out var z0, out var x1, out var y1, out var z1);
                    for (int z = z0; z <= z1; z++)
                        for (int y = y0; y <= y1; y++)
                            for (int x = x0; x <= x1; x++)
                            {
                                int key = x + nx * (y + ny * z);
                                if (!cells.TryGetValue(key, out var list)) cells[key] = list = new List<int>();
                                list.Add(t);
                            }
                }
            }

            private void Range(Vector3 lo, Vector3 hi, out int x0, out int y0, out int z0, out int x1, out int y1, out int z1)
            {
                x0 = Mathf.Clamp(Mathf.FloorToInt((lo.x - min.x) / cell), 0, nx - 1);
                y0 = Mathf.Clamp(Mathf.FloorToInt((lo.y - min.y) / cell), 0, ny - 1);
                z0 = Mathf.Clamp(Mathf.FloorToInt((lo.z - min.z) / cell), 0, nz - 1);
                x1 = Mathf.Clamp(Mathf.FloorToInt((hi.x - min.x) / cell), 0, nx - 1);
                y1 = Mathf.Clamp(Mathf.FloorToInt((hi.y - min.y) / cell), 0, ny - 1);
                z1 = Mathf.Clamp(Mathf.FloorToInt((hi.z - min.z) / cell), 0, nz - 1);
            }

            /// <summary>origin から dir へ maxDist までの、いちばん近い三角形までの距離(当たらなければ maxDist)。</summary>
            public float Raycast(Vector3 origin, Vector3 dir, float maxDist, Vector3[] vertices, int[] tris)
            {
                var end = origin + dir * maxDist;
                Range(Vector3.Min(origin, end), Vector3.Max(origin, end), out var x0, out var y0, out var z0, out var x1, out var y1, out var z1);
                float closest = maxDist;
                for (int z = z0; z <= z1; z++)
                    for (int y = y0; y <= y1; y++)
                        for (int x = x0; x <= x1; x++)
                        {
                            if (!cells.TryGetValue(x + nx * (y + ny * z), out var list)) continue;
                            foreach (int t in list)
                            {
                                float hit = RayTriangle(origin, dir, vertices[tris[t]], vertices[tris[t + 1]], vertices[tris[t + 2]]);
                                if (hit > 1e-4f && hit < closest) closest = hit;
                            }
                        }
                return closest;
            }
        }

        /// <summary>レイと三角形が当たる距離(当たらなければ -1)。</summary>
        private static float RayTriangle(Vector3 origin, Vector3 dir, Vector3 v0, Vector3 v1, Vector3 v2)
        {
            Vector3 e1 = v1 - v0;
            Vector3 e2 = v2 - v0;
            Vector3 h = Vector3.Cross(dir, e2);
            float a = Vector3.Dot(e1, h);
            if (Mathf.Abs(a) < 1e-7f) return -1f;
            float f = 1f / a;
            Vector3 s = origin - v0;
            float u = f * Vector3.Dot(s, h);
            if (u < 0f || u > 1f) return -1f;
            Vector3 q = Vector3.Cross(s, e1);
            float v = f * Vector3.Dot(dir, q);
            if (v < 0f || u + v > 1f) return -1f;
            return f * Vector3.Dot(e2, q);
        }
    }
}

