using System.IO;
using UnityEditor;
using UnityEngine;

namespace GalaxyToon.Editor
{
    /// <summary>
    /// テクスチャ(ふつうはメインテクスチャ)の明るさを高さとみなして、ノーマルマップ(立体感)を自動で作る。
    /// 「細かい模様だけ」を ON にすると、大きな色の変化(影や塗り分け)を除き、布目・毛束・しわなどの細かい凹凸だけを拾う。
    /// 読み込み設定が Read/Write OFF や圧縮済みのテクスチャでも、GPU で一度描き写してから読むので使える。
    /// </summary>
    public sealed class GalaxyToonNormalBaker : EditorWindow
    {
        private Renderer target;
        private int materialIndex;
        private Texture2D source;
        private float strength = 1f;
        private int blur = 1;
        private bool detailOnly = true;
        private int detailRadius = 8;
        private bool invert;
        private int maxTextureSize = 2048;
        private string savePath = "";
        private bool applyToMaterial = true;
        private float bumpScale = 0.6f;
        private Texture2D preview;
        private string message;

        private static readonly int[] Sizes = { 256, 512, 1024, 2048, 4096 };

        [MenuItem("Tools/GalaxyToon/ノーマルマップを作る (テクスチャから)")]
        public static void Open() => GetWindow<GalaxyToonNormalBaker>("ノーマルマップ");

        private void OnSelectionChange()
        {
            if (Selection.activeGameObject != null && Selection.activeGameObject.TryGetComponent(out Renderer r) && r != target) { target = r; materialIndex = 0; source = null; savePath = ""; Repaint(); }
        }

        private Material CurrentMaterial()
        {
            if (target == null) return null;
            Material[] mats = target.sharedMaterials;
            return materialIndex < mats.Length ? mats[materialIndex] : null;
        }

        private void OnGUI()
        {
            EditorGUILayout.HelpBox("テクスチャの明るさから立体感(ノーマルマップ)を作ります。Renderer を選ぶとメインテクスチャが自動で入ります。", MessageType.Info);
            target = (Renderer)EditorGUILayout.ObjectField(new GUIContent("対象の Renderer (任意)"), target, typeof(Renderer), true);
            if (target != null)
            {
                Material[] mats = target.sharedMaterials;
                var names = new string[mats.Length];
                for (int i = 0; i < mats.Length; i++) names[i] = $"{i}: {(mats[i] != null ? mats[i].name : "(なし)")}";
                int newIndex = EditorGUILayout.Popup(new GUIContent("マテリアル"), Mathf.Clamp(materialIndex, 0, Mathf.Max(0, mats.Length - 1)), names);
                if (newIndex != materialIndex) { materialIndex = newIndex; source = null; savePath = ""; }
                Material m = CurrentMaterial();
                if (source == null && m != null && m.HasProperty("_BaseMap")) source = m.GetTexture("_BaseMap") as Texture2D;
            }
            source = (Texture2D)EditorGUILayout.ObjectField(new GUIContent("元のテクスチャ", "明るい所を高く、暗い所を低いとみなします。"), source, typeof(Texture2D), false);
            strength = EditorGUILayout.Slider(new GUIContent("強さ", "凹凸の深さ。"), strength, 0.1f, 5f);
            blur = EditorGUILayout.IntSlider(new GUIContent("なめらかさ (px)", "先にぼかしてノイズを消す。"), blur, 0, 8);
            detailOnly = EditorGUILayout.Toggle(new GUIContent("細かい模様だけ", "大きな明暗(影・塗り分け)を除いて、布目や毛束などの細かい凹凸だけを拾う。キャラにはふつう ON。"), detailOnly);
            using (new EditorGUI.DisabledScope(!detailOnly))
                detailRadius = EditorGUILayout.IntSlider(new GUIContent("大きな明暗とみなす大きさ (px)"), detailRadius, 2, 64);
            invert = EditorGUILayout.Toggle(new GUIContent("反転 (暗い所を高く)"), invert);
            maxTextureSize = EditorGUILayout.IntPopup(new GUIContent("最大サイズ"), maxTextureSize, System.Array.ConvertAll(Sizes, s => new GUIContent(s.ToString())), Sizes);

            using (new EditorGUILayout.HorizontalScope())
            {
                if (string.IsNullOrEmpty(savePath)) savePath = DefaultPath();
                savePath = EditorGUILayout.TextField(new GUIContent("保存先"), savePath);
                if (GUILayout.Button("選ぶ", GUILayout.Width(44f)))
                {
                    string picked = EditorUtility.SaveFilePanelInProject("ノーマルマップの保存先", Path.GetFileNameWithoutExtension(savePath), "png", "保存先を選んでください。", Path.GetDirectoryName(savePath));
                    if (!string.IsNullOrEmpty(picked)) savePath = picked;
                }
            }
            applyToMaterial = EditorGUILayout.Toggle(new GUIContent("マテリアルに反映", "作ったら「立体感 (ノーマルマップ)」に入れる(GalaxyShader/Toon のとき)。"), applyToMaterial);
            using (new EditorGUI.DisabledScope(!applyToMaterial))
                bumpScale = EditorGUILayout.Slider(new GUIContent("反映時の立体感の強さ"), bumpScale, 0f, 2f);

            EditorGUILayout.Space(6f);
            using (new EditorGUI.DisabledScope(source == null))
                if (GUILayout.Button("作成", GUILayout.Height(28f))) Bake();
            if (!string.IsNullOrEmpty(message)) EditorGUILayout.HelpBox(message, MessageType.None);
            if (preview != null)
            {
                Rect rect = GUILayoutUtility.GetAspectRect((float)preview.width / preview.height, GUILayout.MaxWidth(320f));
                EditorGUI.DrawPreviewTexture(rect, preview);
            }
        }

        private string DefaultPath()
        {
            Material m = CurrentMaterial();
            string basePath = m != null ? AssetDatabase.GetAssetPath(m) : source != null ? AssetDatabase.GetAssetPath(source) : "Assets/x";
            string dir = Path.GetDirectoryName(basePath).Replace('\\', '/');
            string name = m != null ? m.name : source != null ? source.name : "Texture";
            return $"{dir}/{name}_Normal.png";
        }

        private void Bake()
        {
            string path = GalaxyToonNormal.Bake(source, savePath, strength, blur, detailOnly ? detailRadius : 0, invert, maxTextureSize);
            preview = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            Material m = CurrentMaterial();
            if (applyToMaterial && m != null && m.HasProperty("_BumpMap"))
            {
                Undo.RecordObject(m, "ノーマルマップを反映");
                m.SetTexture("_BumpMap", preview);
                m.SetFloat("_BumpScale", bumpScale);
                GalaxyToonGUI.Apply(m);
                EditorUtility.SetDirty(m);
                AssetDatabase.SaveAssets();
                message = $"作りました: {path}\n「{m.name}」に反映しました。";
            }
            else message = $"作りました: {path}";
        }
    }

    /// <summary>ノーマルマップの計算(エディタ外からも呼べるように分けてある)。</summary>
    public static class GalaxyToonNormal
    {
        /// <summary>テクスチャからノーマルマップを作って PNG に保存し、ノーマルマップとして読み込む。戻り値は保存先。</summary>
        public static string Bake(Texture2D source, string savePath, float strength, int blur, int detailRadius, bool invert, int maxTextureSize)
        {
            int w = Mathf.Min(source.width, maxTextureSize), h = Mathf.Min(source.height, maxTextureSize);
            Color[] src = ReadPixels(source, w, h);

            // 高さ = 明るさ(0〜1)
            var height = new float[w * h];
            for (int i = 0; i < src.Length; i++) height[i] = src[i].r * 0.299f + src[i].g * 0.587f + src[i].b * 0.114f;
            if (blur > 0) height = BoxBlur(height, w, h, blur);
            if (detailRadius > 0)
            {
                // 細かい模様だけ: 大きくぼかしたものとの差を取る(影・塗り分けの大きな明暗を除く)
                float[] low = BoxBlur(height, w, h, detailRadius);
                for (int i = 0; i < height.Length; i++) height[i] = 0.5f + (height[i] - low[i]) * 2f;
            }
            if (invert) for (int i = 0; i < height.Length; i++) height[i] = 1f - height[i];

            // Sobel で傾きを取り、法線にする(繰り返しテクスチャとして端をつなぐ)
            var pixels = new Color[w * h];
            float H(int x, int y) => height[((y + h) % h) * w + (x + w) % w];
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    float dx = (H(x + 1, y - 1) + 2f * H(x + 1, y) + H(x + 1, y + 1)) - (H(x - 1, y - 1) + 2f * H(x - 1, y) + H(x - 1, y + 1));
                    float dy = (H(x - 1, y + 1) + 2f * H(x, y + 1) + H(x + 1, y + 1)) - (H(x - 1, y - 1) + 2f * H(x, y - 1) + H(x + 1, y - 1));
                    Vector3 n = new Vector3(-dx * strength, -dy * strength, 1f).normalized;
                    pixels[y * w + x] = new Color(n.x * 0.5f + 0.5f, n.y * 0.5f + 0.5f, n.z * 0.5f + 0.5f, 1f);
                }

            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false, true);
            tex.SetPixels(pixels);
            tex.Apply();
            string full = Path.GetFullPath(savePath);
            Directory.CreateDirectory(Path.GetDirectoryName(full));
            File.WriteAllBytes(full, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(savePath);
            if (AssetImporter.GetAtPath(savePath) is TextureImporter importer)
            {
                importer.textureType = TextureImporterType.NormalMap;
                importer.sRGBTexture = false;
                importer.wrapMode = source.wrapMode;
                importer.SaveAndReimport();
            }
            return savePath;
        }

        /// <summary>Read/Write OFF や圧縮済みでも読めるよう、GPU で描き写してから読む。</summary>
        private static Color[] ReadPixels(Texture2D source, int w, int h)
        {
            RenderTexture rt = RenderTexture.GetTemporary(w, h, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            RenderTexture prev = RenderTexture.active;
            Graphics.Blit(source, rt);
            RenderTexture.active = rt;
            var copy = new Texture2D(w, h, TextureFormat.RGBA32, false);
            copy.ReadPixels(new Rect(0, 0, w, h), 0, 0);
            copy.Apply();
            RenderTexture.active = prev;
            RenderTexture.ReleaseTemporary(rt);
            Color[] pixels = copy.GetPixels();
            Object.DestroyImmediate(copy);
            return pixels;
        }

        private static float[] BoxBlur(float[] src, int w, int h, int radius)
        {
            var tmp = new float[src.Length];
            var dst = new float[src.Length];
            float inv = 1f / (radius * 2 + 1);
            for (int y = 0; y < h; y++)
            {
                float sum = 0f;
                for (int k = -radius; k <= radius; k++) sum += src[y * w + ((k + w) % w)];
                for (int x = 0; x < w; x++)
                {
                    tmp[y * w + x] = sum * inv;
                    sum += src[y * w + ((x + radius + 1) % w)] - src[y * w + ((x - radius + w) % w)];
                }
            }
            for (int x = 0; x < w; x++)
            {
                float sum = 0f;
                for (int k = -radius; k <= radius; k++) sum += tmp[((k + h) % h) * w + x];
                for (int y = 0; y < h; y++)
                {
                    dst[y * w + x] = sum * inv;
                    sum += tmp[((y + radius + 1) % h) * w + x] - tmp[((y - radius + h) % h) * w + x];
                }
            }
            return dst;
        }
    }
}
