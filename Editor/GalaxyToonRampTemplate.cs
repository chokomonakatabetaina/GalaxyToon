using System.IO;
using UnityEditor;
using UnityEngine;

namespace GalaxyToon.Editor
{
    /// <summary>部位ごとの影ランプ・ハイライトランプのひな形を作る。段は上から 肌 / 髪 / 服 / 金属。
    /// マテリアルの「ランプの段数」を 4 にし、「ランプの段」か「部位IDマップ」で段を選ぶ。絵は画像編集ソフトで描き直してよい。</summary>
    public static class GalaxyToonRampTemplate
    {
        public const int Rows = 4, Width = 256, RowHeight = 4;
        private static readonly string[] RowNames = { "肌", "髪", "服", "金属" };

        [MenuItem("Tools/GalaxyToon/ランプのひな形を作る (影・ハイライト、部位4段)")]
        public static void CreateMenu()
        {
            string folder = "Assets";
            if (Selection.activeObject != null)
            {
                string selected = AssetDatabase.GetAssetPath(Selection.activeObject);
                if (AssetDatabase.IsValidFolder(selected)) folder = selected;
            }
            var (shadow, spec) = Create(folder);
            Debug.Log($"[ランプ] 作りました: {shadow} / {spec}\n段(上から): {string.Join(" / ", RowNames)}。マテリアルの「ランプの段数」を {Rows} にしてください。");
            Selection.activeObject = AssetDatabase.LoadAssetAtPath<Texture2D>(shadow);
        }

        public static (string shadow, string spec) Create(string folder, bool overwrite = false)
        {
            if (!AssetDatabase.IsValidFolder(folder)) { Directory.CreateDirectory(folder); AssetDatabase.Refresh(); }
            string shadowPath = $"{folder}/GalaxyToon_ShadowRamp_4Rows.png", specPath = $"{folder}/GalaxyToon_SpecularRamp_4Rows.png";
            if (!overwrite) { shadowPath = AssetDatabase.GenerateUniqueAssetPath(shadowPath); specPath = AssetDatabase.GenerateUniqueAssetPath(specPath); }
            Write(shadowPath, ShadowRow);
            Write(specPath, SpecularRow);
            return (shadowPath, specPath);
        }

        // ---- 影: 左=いちばん暗い → 右=光。部位ごとに影の色と段のつき方を変える(アニメ塗りの 1影・2影)
        private static Color ShadowRow(int row, float x)
        {
            Color lit = Color.white;
            switch (row)
            {
                case 0: // 肌: 赤みのある 1 影、境界はやや柔らかい
                    return Step(new Color(0.80f, 0.48f, 0.50f), new Color(0.98f, 0.74f, 0.70f), lit, x, 0.30f, 0.5f, 0.03f);
                case 1: // 髪: 紫寄りの 2 段、境界くっきり
                    return Step(new Color(0.42f, 0.36f, 0.72f), new Color(0.70f, 0.62f, 0.92f), lit, x, 0.3f, 0.5f, 0.012f);
                case 2: // 服: 青みのある 2 段
                    return Step(new Color(0.38f, 0.44f, 0.72f), new Color(0.66f, 0.70f, 0.92f), lit, x, 0.28f, 0.5f, 0.015f);
                default: // 金属: 暗い影と、明るい側に照り返しの帯
                    Color c = Step(new Color(0.30f, 0.30f, 0.38f), new Color(0.62f, 0.62f, 0.72f), lit, x, 0.35f, 0.55f, 0.01f);
                    return Color.Lerp(c, new Color(1.15f, 1.12f, 1.05f), Band(x, 0.8f, 0.06f, 0.01f) * 0.6f);
            }
        }

        // ---- ハイライト: 左=光の外側 → 右=光の中心。黒=光らない。形(段・縞)と色を決める
        private static Color SpecularRow(int row, float x)
        {
            switch (row)
            {
                case 0: return Color.white * Smooth(x, 0.55f, 0.15f) * 0.6f;           // 肌: 柔らかい小さな照り
                case 1: // 髪(天使の輪): 中心の鋭い白と、外側の淡い帯の 2 段
                    return new Color(1f, 0.98f, 0.94f) * Mathf.Max(Smooth(x, 0.7f, 0.02f), Smooth(x, 0.3f, 0.02f) * 0.5f);
                case 2: return Color.black;                                            // 服: 光らない
                default: // 金属: 縞の入った強い光
                    return new Color(1f, 0.97f, 0.9f) * Mathf.Max(Smooth(x, 0.85f, 0.01f), Band(x, 0.55f, 0.08f, 0.01f) * 0.7f);
            }
        }

        private static Color Step(Color dark, Color mid, Color lit, float x, float b1, float b2, float soft)
        {
            Color c = Color.Lerp(dark, mid, Smooth(x, b1, soft));
            return Color.Lerp(c, lit, Smooth(x, b2, soft));
        }

        private static float Smooth(float x, float edge, float soft) => Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(edge - soft, edge + soft, x));
        private static float Band(float x, float center, float half, float soft) => Smooth(x, center - half, soft) * (1f - Smooth(x, center + half, soft));

        private static void Write(string path, System.Func<int, float, Color> pixel)
        {
            var tex = new Texture2D(Width, Rows * RowHeight, TextureFormat.RGBA32, false, false);
            for (int y = 0; y < Rows * RowHeight; y++)
            {
                int row = Rows - 1 - y / RowHeight; // 画像の上 = 段 0
                for (int x = 0; x < Width; x++)
                {
                    Color c = pixel(row, x / (Width - 1f));
                    c.a = 1f;
                    tex.SetPixel(x, y, c); // 色はそのまま(sRGB で読む)
                }
            }
            tex.Apply();
            File.WriteAllBytes(Path.GetFullPath(path), tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(path);
            if (AssetImporter.GetAtPath(path) is TextureImporter importer)
            {
                // 段の境目でにじまないよう、ミップ・圧縮なし、端で繰り返さない
                importer.mipmapEnabled = false;
                importer.wrapMode = TextureWrapMode.Clamp;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.filterMode = FilterMode.Bilinear;
                importer.sRGBTexture = true;
                importer.SaveAndReimport();
            }
        }
    }
}
