using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace GalaxyToon.Editor
{
    /// <summary>GalaxyShader/Toon の全マテリアル向けの道具。
    /// 機能の ON/OFF は shader_feature(マテリアルで使っている組み合わせだけがビルドに入る)なので、
    /// 「値が効かない機能のキーワードを外す」= 計算が減り、ビルドに入るシェーダの組み合わせも減る。</summary>
    public static class GalaxyToonTools
    {
        private const string ShaderName = "GalaxyShader/Toon";

        private static IEnumerable<Material> AllToonMaterials()
        {
            foreach (string guid in AssetDatabase.FindAssets("t:Material"))
            {
                var m = AssetDatabase.LoadAssetAtPath<Material>(AssetDatabase.GUIDToAssetPath(guid));
                if (m != null && m.shader != null && m.shader.name == ShaderName) yield return m;
            }
        }

        [MenuItem("Tools/GalaxyToon/全マテリアルの機能を整理 (不要な機能を外す)")]
        public static void ReapplyAll()
        {
            int count = 0;
            foreach (Material m in AllToonMaterials()) { GalaxyToonGUI.Apply(m); count++; }
            AssetDatabase.SaveAssets();
            Debug.Log($"[GalaxyToon] {count} 個のマテリアルで、使っていない機能を外しました。");
            Report();
        }

        private static readonly int OffId = Shader.PropertyToID("_GalaxyToonOff");
        private const string OffMenu = "Tools/GalaxyToon/シェーダー OFF (Lit で見比べ) %#t";

        public static bool ShaderOff => Shader.GetGlobalFloat(OffId) > 0.5f;

        /// <summary>GalaxyToon の全マテリアルを「URP の Lit と同じ計算 + 輪郭線なし」で描く/元に戻す(Ctrl+Shift+T)。マテリアルは変えない。</summary>
        [MenuItem(OffMenu)]
        public static void ToggleShaderOff()
        {
            Shader.SetGlobalFloat(OffId, ShaderOff ? 0f : 1f);
            Menu.SetChecked(OffMenu.Substring(0, OffMenu.LastIndexOf(' ')), ShaderOff);
            SceneView.RepaintAll();
            UnityEditorInternal.InternalEditorUtility.RepaintAllViews();
        }

        /// <summary>プロジェクトで使っている URP Renderer(既定 + 各品質)に Galaxy Hair Shadow Feature を足す。入っていれば何もしない。</summary>
        [MenuItem("Tools/GalaxyToon/髪の影を有効にする (使っている URP Renderer に追加)")]
        public static void EnableHairShadow()
        {
            var pipelines = new HashSet<UnityEngine.Rendering.Universal.UniversalRenderPipelineAsset>();
            void AddPipeline(UnityEngine.Rendering.RenderPipelineAsset a) { if (a is UnityEngine.Rendering.Universal.UniversalRenderPipelineAsset u) pipelines.Add(u); }
            AddPipeline(UnityEngine.Rendering.GraphicsSettings.defaultRenderPipeline);
            for (int i = 0; i < QualitySettings.count; i++) AddPipeline(QualitySettings.GetRenderPipelineAssetAt(i));

            var added = new List<string>();
            var already = new List<string>();
            foreach (var pipeline in pipelines)
                foreach (var data in pipeline.rendererDataList)
                {
                    if (data == null || already.Contains(data.name) || added.Contains(data.name)) continue;
                    if (data.rendererFeatures.Any(f => f is GalaxyHairShadowFeature)) { already.Add(data.name); continue; }
                    // URP の「Add Renderer Feature」と同じ手順: サブアセットとして足し、一覧と ID の対応表に入れる
                    var feature = ScriptableObject.CreateInstance<GalaxyHairShadowFeature>();
                    feature.name = "GalaxyHairShadowFeature";
                    Undo.RegisterCreatedObjectUndo(feature, "髪の影を有効にする");
                    AssetDatabase.AddObjectToAsset(feature, data);
                    AssetDatabase.TryGetGUIDAndLocalFileIdentifier(feature, out string _, out long localId);
                    var so = new SerializedObject(data);
                    SerializedProperty list = so.FindProperty("m_RendererFeatures"), map = so.FindProperty("m_RendererFeatureMap");
                    list.arraySize++; list.GetArrayElementAtIndex(list.arraySize - 1).objectReferenceValue = feature;
                    map.arraySize++; map.GetArrayElementAtIndex(map.arraySize - 1).longValue = localId;
                    so.ApplyModifiedProperties();
                    data.SetDirty();
                    EditorUtility.SetDirty(data);
                    added.Add(data.name);
                }
            AssetDatabase.SaveAssets();
            if (pipelines.Count == 0) Debug.LogWarning("[GalaxyToon] URP のパイプラインアセットが設定されていません(Project Settings > Graphics / Quality)。");
            else Debug.Log($"[GalaxyToon] 髪の影: 追加した Renderer = {(added.Count > 0 ? string.Join(", ", added) : "なし")} / もともと入っていた Renderer = {(already.Count > 0 ? string.Join(", ", already) : "なし")}\n顔・髪のキャラのルートに Galaxy Toon Face を付けてください。");
        }

        [MenuItem("Tools/GalaxyToon/使っている機能の一覧")]
        public static void Report()
        {
            var sets = new Dictionary<string, List<string>>();
            var featureCount = new Dictionary<string, int>();
            foreach (Material m in AllToonMaterials())
            {
                string[] kws = m.shaderKeywords.OrderBy(k => k).ToArray();
                string key = kws.Length == 0 ? "(機能なし)" : string.Join(" ", kws);
                if (!sets.TryGetValue(key, out var list)) sets[key] = list = new List<string>();
                list.Add(m.name);
                foreach (string k in kws) featureCount[k] = featureCount.TryGetValue(k, out int c) ? c + 1 : 1;
            }
            var sb = new StringBuilder();
            sb.AppendLine($"[GalaxyToon] 機能の組み合わせ {sets.Count} 種類(=ビルドに入る組み合わせの数。少ないほどビルドが小さく、読み込みが速い)");
            foreach (var kv in sets.OrderByDescending(kv => kv.Value.Count))
                sb.AppendLine($"  {kv.Value.Count,3} 個: {kv.Key}   例: {string.Join(", ", kv.Value.Take(3))}");
            sb.AppendLine("機能ごとの使用数: " + string.Join(", ", featureCount.OrderByDescending(kv => kv.Value).Select(kv => $"{kv.Key}={kv.Value}")));
            Debug.Log(sb.ToString());
        }
    }
}
