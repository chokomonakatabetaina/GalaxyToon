using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace GalaxyToon.Editor
{
    /// <summary>
    /// SDF 影マップ(GalaxyShader/Toon の「SDF 顔影」)をメッシュから自動で作る。
    /// 各テクセルについて「光が正面から横へ回っていくと、何度で影に入るか(0=正面 1=真後ろ)」を、光が右から(R)・左から(G)の2通り書き込む。
    /// 形はメッシュの法線をなめらかにしたもの(顔・髪は頭の楕円体へも寄せる)から作るので、鼻や髪の束の細かい影が出ない、すっきりした影の形になる。
    /// </summary>
    public sealed class GalaxyToonSdfBaker : EditorWindow
    {
        public enum Kind { Face, Body, Hair }
        public enum Axis { Auto, PlusZ, MinusZ, PlusX, MinusX }

        private Renderer target;
        private int materialIndex;
        private Kind kind = Kind.Face;
        private int resolution = 1024;
        private Axis forwardAxis = Axis.Auto;
        private float smoothness = 0.6f;      // 法線をなめらかにする強さ
        private float shapeBlend = 0.7f;      // 頭の楕円体へ寄せる強さ(顔・髪)
        private string savePath = "";
        private bool applyToMaterial = true;
        private Texture2D preview;
        private string message;

        private static readonly string[] KindNames = { "顔", "体", "髪" };
        private static readonly string[] AxisNames = { "自動 (キャラのルートの正面)", "メッシュの +Z", "メッシュの -Z", "メッシュの +X", "メッシュの -X" };
        private static readonly int[] Resolutions = { 256, 512, 1024, 2048 };

        [MenuItem("Tools/GalaxyToon/SDF 影マップを作る")]
        public static void Open() => GetWindow<GalaxyToonSdfBaker>("SDF 影マップ");

        private void OnSelectionChange()
        {
            if (Selection.activeGameObject != null && Selection.activeGameObject.TryGetComponent(out Renderer r) && r != target) { target = r; materialIndex = 0; savePath = ""; Repaint(); }
        }

        private void OnGUI()
        {
            EditorGUILayout.HelpBox("顔・体・髪のメッシュから SDF 影マップ(R=光が右から G=光が左から)を作ります。\nヒエラルキーで Renderer を選ぶか、下に入れてください。", MessageType.Info);
            target = (Renderer)EditorGUILayout.ObjectField(new GUIContent("対象の Renderer", "SkinnedMeshRenderer か MeshRenderer。スキンメッシュは基本の姿勢(バインドポーズ)で作ります。"), target, typeof(Renderer), true);
            if (target == null) return;

            Material[] mats = target.sharedMaterials;
            var names = new string[mats.Length];
            for (int i = 0; i < mats.Length; i++) names[i] = $"{i}: {(mats[i] != null ? mats[i].name : "(なし)")}";
            int newIndex = EditorGUILayout.Popup(new GUIContent("マテリアル(サブメッシュ)", "このマテリアルが使う部分だけで作ります。"), Mathf.Clamp(materialIndex, 0, Mathf.Max(0, mats.Length - 1)), names);
            if (newIndex != materialIndex) { materialIndex = newIndex; savePath = ""; }

            EditorGUI.BeginChangeCheck();
            kind = (Kind)EditorGUILayout.Popup(new GUIContent("種類", "顔: 頭の楕円体へ強く寄せ、鼻・口の影を消す。\n体: なめらかにした体の形のまま。\n髪: 頭の球へ寄せ、髪の束ごとの細かい影を消す。"), (int)kind, KindNames);
            if (EditorGUI.EndChangeCheck()) { smoothness = kind == Kind.Body ? 0.8f : 0.6f; shapeBlend = kind == Kind.Face ? 0.7f : kind == Kind.Hair ? 0.5f : 0f; }
            if (kind == Kind.Body)
                EditorGUILayout.HelpBox("SDF は焼いたときの姿勢(T ポーズ)が前提です。腕・脚のように曲がる部位に使うと、影が肌に張り付いて関節でガタガタになります。胴体だけの固いメッシュ以外には使わないでください。", MessageType.Warning);
            resolution = EditorGUILayout.IntPopup(new GUIContent("解像度"), resolution, System.Array.ConvertAll(Resolutions, r => new GUIContent(r.ToString())), Resolutions);
            forwardAxis = (Axis)EditorGUILayout.Popup(new GUIContent("キャラの正面", "顔が向いている向き。自動: キャラのいちばん上の親の正面(+Z)を使う。FBX の軸の違い(Blender の Z 上など)も自動で合わせる。"), (int)forwardAxis, AxisNames);
            smoothness = EditorGUILayout.Slider(new GUIContent("なめらかさ", "法線をなめらかにする強さ。上げるほど影の形が大きく単純になる。"), smoothness, 0f, 1f);
            using (new EditorGUI.DisabledScope(kind == Kind.Body))
                shapeBlend = EditorGUILayout.Slider(new GUIContent("頭の形へ寄せる", "顔・髪で、頭を楕円体とみなした形へ寄せる強さ。1 で完全に楕円体の影。"), shapeBlend, 0f, 1f);

            using (new EditorGUILayout.HorizontalScope())
            {
                if (string.IsNullOrEmpty(savePath)) savePath = DefaultPath(mats, materialIndex);
                savePath = EditorGUILayout.TextField(new GUIContent("保存先", "Assets からのパス(.png)。"), savePath);
                if (GUILayout.Button("選ぶ", GUILayout.Width(44f)))
                {
                    string dir = Path.GetDirectoryName(savePath);
                    string picked = EditorUtility.SaveFilePanelInProject("SDF 影マップの保存先", Path.GetFileNameWithoutExtension(savePath), "png", "保存先を選んでください。", dir);
                    if (!string.IsNullOrEmpty(picked)) savePath = picked;
                }
            }
            applyToMaterial = EditorGUILayout.Toggle(new GUIContent("マテリアルに反映", "作ったら、このマテリアルの「SDF 顔影」に入れて ON にする(GalaxyShader/Toon のとき)。"), applyToMaterial);

            EditorGUILayout.Space(6f);
            if (GUILayout.Button("作成", GUILayout.Height(28f))) Bake();
            if (!string.IsNullOrEmpty(message)) EditorGUILayout.HelpBox(message, MessageType.None);
            if (preview != null)
            {
                Rect rect = GUILayoutUtility.GetAspectRect(1f, GUILayout.MaxWidth(320f));
                EditorGUI.DrawPreviewTexture(rect, preview, null, ScaleMode.ScaleToFit);
            }
        }

        private static string DefaultPath(Material[] mats, int index)
        {
            Material m = index < mats.Length ? mats[index] : null;
            string matPath = m != null ? AssetDatabase.GetAssetPath(m) : "";
            string dir = string.IsNullOrEmpty(matPath) ? "Assets" : Path.GetDirectoryName(matPath).Replace('\\', '/');
            return $"{dir}/{(m != null ? m.name : "Mesh")}_SDF.png";
        }

        private Mesh SourceMesh() => target is SkinnedMeshRenderer smr ? smr.sharedMesh : target.GetComponent<MeshFilter>()?.sharedMesh;

        private void Bake()
        {
            Mesh mesh = SourceMesh();
            if (mesh == null) { message = "メッシュが見つかりません。"; return; }
            if (materialIndex >= mesh.subMeshCount) { message = "このマテリアルに対応するサブメッシュがありません。"; return; }

            Vector3 forward, right;
            switch (forwardAxis)
            {
                case Axis.Auto:
                    // メッシュの空間 = Renderer の空間。キャラのルートの正面・右を、その空間へ写す
                    forward = target.transform.InverseTransformDirection(target.transform.root.forward).normalized;
                    right = target.transform.InverseTransformDirection(target.transform.root.right).normalized;
                    break;
                case Axis.MinusZ: forward = Vector3.back; right = Vector3.left; break;
                case Axis.PlusX: forward = Vector3.right; right = Vector3.back; break;
                case Axis.MinusX: forward = Vector3.left; right = Vector3.forward; break;
                default: forward = Vector3.forward; right = Vector3.right; break;
            }

            Color[] pixels = GalaxyToonSdf.Bake(mesh, materialIndex, resolution, forward, right, smoothness, kind == Kind.Body ? 0f : shapeBlend, kind == Kind.Hair);
            var tex = new Texture2D(resolution, resolution, TextureFormat.RGBA32, false, true);
            tex.SetPixels(pixels);
            tex.Apply();

            string full = Path.GetFullPath(savePath);
            Directory.CreateDirectory(Path.GetDirectoryName(full));
            File.WriteAllBytes(full, tex.EncodeToPNG());
            DestroyImmediate(tex);
            AssetDatabase.ImportAsset(savePath);
            if (AssetImporter.GetAtPath(savePath) is TextureImporter importer)
            {
                // しきい値のデータなので、色空間の変換・圧縮・ミップをかけない
                importer.sRGBTexture = false;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.mipmapEnabled = false;
                importer.wrapMode = TextureWrapMode.Clamp;
                importer.SaveAndReimport();
            }
            preview = AssetDatabase.LoadAssetAtPath<Texture2D>(savePath);

            Material mat = materialIndex < target.sharedMaterials.Length ? target.sharedMaterials[materialIndex] : null;
            if (applyToMaterial && mat != null && mat.HasProperty("_FaceSdfTex"))
            {
                Undo.RecordObject(mat, "SDF 影マップを反映");
                mat.SetTexture("_FaceSdfTex", preview);
                mat.SetFloat("_UseFaceSdf", 1f);
                mat.EnableKeyword("_FACE_SDF");
                mat.SetFloat("_SdfFollowHead", kind == Kind.Body ? 0f : 1f); // 顔・髪は首の向きに、体は体の向きについてくる
                EditorUtility.SetDirty(mat);
                AssetDatabase.SaveAssets();
                message = $"作りました: {savePath}\n「{mat.name}」に反映しました。";
            }
            else message = $"作りました: {savePath}" + (applyToMaterial && mat != null ? "\n(GalaxyShader/Toon ではないので、マテリアルには入れていません)" : "");
        }
    }

    /// <summary>SDF 影マップの計算(エディタ外からも呼べるように分けてある)。</summary>
    public static class GalaxyToonSdf
    {
        /// <summary>R=光が右から、G=光が左から回ってきたとき影に入る角度(0〜1 = 0〜180°)。メッシュの無い所は周りの値で埋める。</summary>
        public static Color[] Bake(Mesh mesh, int subMesh, int size, Vector3 forward, Vector3 right, float smoothness, float shapeBlend, bool sphereShape)
        {
            Vector3[] positions = mesh.vertices;
            Vector3[] normals = mesh.normals;
            Vector2[] uvs = mesh.uv;
            int[] tris = mesh.GetTriangles(subMesh);

            // ---- 使う頂点と、その範囲(キャラの 右・上・正面 の座標で。楕円体の中心と半径に使う)
            var used = new HashSet<int>(tris);
            forward.Normalize(); right.Normalize();
            Vector3 up = Vector3.Cross(forward, right).normalized;
            Vector3 Local(Vector3 p) => new Vector3(Vector3.Dot(p, right), Vector3.Dot(p, up), Vector3.Dot(p, forward));
            Bounds bounds = new Bounds(Local(positions[tris[0]]), Vector3.zero);
            foreach (int i in used) bounds.Encapsulate(Local(positions[i]));

            // ---- 法線をなめらかにする: 同じ位置の頂点(UV の継ぎ目)を1つとみなし、隣どうしで平均を繰り返す
            var weld = new Dictionary<Vector3Int, int>();
            var vertexGroup = new int[positions.Length];
            var groupNormals = new List<Vector3>();
            foreach (int i in used)
            {
                Vector3Int key = Vector3Int.RoundToInt(positions[i] * 10000f);
                if (!weld.TryGetValue(key, out int g)) { g = groupNormals.Count; weld[key] = g; groupNormals.Add(Vector3.zero); }
                vertexGroup[i] = g;
                groupNormals[g] += normals[i];
            }
            var neighbors = new List<int>[groupNormals.Count];
            for (int g = 0; g < neighbors.Length; g++) neighbors[g] = new List<int>();
            for (int t = 0; t < tris.Length; t += 3)
                for (int k = 0; k < 3; k++)
                {
                    int a = vertexGroup[tris[t + k]], b = vertexGroup[tris[t + (k + 1) % 3]];
                    if (a != b) { neighbors[a].Add(b); neighbors[b].Add(a); }
                }
            var smooth = groupNormals.ConvertAll(n => n.normalized);
            int iterations = Mathf.RoundToInt(smoothness * 40f);
            for (int it = 0; it < iterations; it++)
            {
                var next = new Vector3[smooth.Count];
                for (int g = 0; g < smooth.Count; g++)
                {
                    Vector3 sum = smooth[g];
                    foreach (int nb in neighbors[g]) sum += smooth[nb];
                    next[g] = sum.normalized;
                }
                smooth = new List<Vector3>(next);
            }

            // ---- 頭の形(顔=楕円体 / 髪=球)へ寄せる。座標は 右・上・正面。顔の中心は少し後ろ(顔の面より奥)
            Vector3 center = bounds.center - new Vector3(0f, 0f, sphereShape ? 0f : bounds.extents.magnitude * 0.35f);
            Vector3 radii = sphereShape ? Vector3.one * bounds.extents.magnitude : new Vector3(Mathf.Max(bounds.extents.x, 1e-4f), Mathf.Max(bounds.extents.y, 1e-4f), Mathf.Max(bounds.extents.z, 1e-4f) * 1.6f);

            // ---- 頂点ごとのしきい値
            var thresholdR = new float[positions.Length];
            var thresholdL = new float[positions.Length];
            foreach (int i in used)
            {
                Vector3 n = smooth[vertexGroup[i]];
                if (shapeBlend > 0f)
                {
                    Vector3 d = Local(positions[i]) - center;
                    Vector3 e = new Vector3(d.x / (radii.x * radii.x), d.y / (radii.y * radii.y), d.z / (radii.z * radii.z)).normalized;
                    Vector3 ellipsoidNormal = right * e.x + up * e.y + forward * e.z; // メッシュの空間へ戻す
                    n = Vector3.Slerp(n, ellipsoidNormal, shapeBlend);
                }
                // 法線の水平の向き φ(正面=0、右=+90°)。光が右から θ 回ったとき、|θ-φ|<90° の間は光が当たる
                float nf = Vector3.Dot(n, forward), nr = Vector3.Dot(n, right);
                float phi = Mathf.Atan2(nr, nf) * Mathf.Rad2Deg;
                thresholdR[i] = Mathf.Clamp01((phi + 90f) / 180f);
                thresholdL[i] = Mathf.Clamp01((-phi + 90f) / 180f);
            }

            // ---- UV 空間へ三角形を塗る
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
                if (Mathf.Abs(area) < 1e-8f) continue;
                for (int y = minY; y <= maxY; y++)
                    for (int x = minX; x <= maxX; x++)
                    {
                        Vector2 p = new Vector2(x + 0.5f, y + 0.5f);
                        float w1 = ((p.x - p0.x) * (p2.y - p0.y) - (p2.x - p0.x) * (p.y - p0.y)) / area;
                        float w2 = ((p1.x - p0.x) * (p.y - p0.y) - (p.x - p0.x) * (p1.y - p0.y)) / area;
                        float w0 = 1f - w1 - w2;
                        if (w0 < -1e-4f || w1 < -1e-4f || w2 < -1e-4f) continue;
                        int idx = y * size + x;
                        pixels[idx] = new Color(
                            thresholdR[i0] * w0 + thresholdR[i1] * w1 + thresholdR[i2] * w2,
                            thresholdL[i0] * w0 + thresholdL[i1] * w1 + thresholdL[i2] * w2, 0f, 1f);
                        filled[idx] = true;
                    }
            }

            // ---- 継ぎ目が出ないよう、塗っていない所を周りの値で数ピクセル埋める
            int pad = Mathf.Max(4, size / 128);
            for (int pass = 0; pass < pad; pass++)
            {
                var add = new List<(int, Color)>();
                for (int y = 0; y < size; y++)
                    for (int x = 0; x < size; x++)
                    {
                        int idx = y * size + x;
                        if (filled[idx]) continue;
                        Color sum = Color.clear; int count = 0;
                        for (int dy = -1; dy <= 1; dy++)
                            for (int dx = -1; dx <= 1; dx++)
                            {
                                int nx = x + dx, ny = y + dy;
                                if (nx < 0 || ny < 0 || nx >= size || ny >= size) continue;
                                int n = ny * size + nx;
                                if (filled[n]) { sum += pixels[n]; count++; }
                            }
                        if (count > 0) add.Add((idx, sum / count));
                    }
                foreach (var (idx, c) in add) { pixels[idx] = c; filled[idx] = true; }
            }
            for (int i = 0; i < pixels.Length; i++) if (!filled[i]) pixels[i] = new Color(0.5f, 0.5f, 0f, 1f);
            return pixels;
        }
    }
}
