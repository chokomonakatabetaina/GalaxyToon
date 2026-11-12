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

        [MenuItem("Tools/GalaxyToon/全マテリアルの機能を整理 (不要な機能を外す)", false, 61)]
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
        [MenuItem(OffMenu, false, 60)]
        public static void ToggleShaderOff()
        {
            Shader.SetGlobalFloat(OffId, ShaderOff ? 0f : 1f);
            Menu.SetChecked(OffMenu.Substring(0, OffMenu.LastIndexOf(' ')), ShaderOff);
            SceneView.RepaintAll();
            UnityEditorInternal.InternalEditorUtility.RepaintAllViews();
        }

        /// <summary>プロジェクトで使っている URP Renderer(既定 + 各品質)に Galaxy Hair Shadow Feature を足す。入っていれば何もしない。</summary>
        [MenuItem("Tools/GalaxyToon/髪の影を有効にする (使っている URP Renderer に追加)", false, 20)]
        public static void EnableHairShadow() => AddRendererFeature<GalaxyHairShadowFeature>("髪の影", "顔・髪のキャラのルートに Galaxy Toon Face を付けてください。");

        [MenuItem("Tools/GalaxyToon/Volume を有効にする (使っている URP Renderer に追加)", false, 25)]
        public static void EnableVolume() => AddRendererFeature<GalaxyToonVolumeFeature>("Volume", "Volume の Add Override > GalaxyToon > Galaxy Toon で調整します。");

        /// <summary>使っている URP Renderer(既定 + 各品質)に、まだ無ければ T を足す。URP の「Add Renderer Feature」と同じ手順。</summary>
        public static void AddRendererFeature<T>(string label, string hint) where T : UnityEngine.Rendering.Universal.ScriptableRendererFeature
        {
            var pipelines = new HashSet<UnityEngine.Rendering.Universal.UniversalRenderPipelineAsset>();
            void AddPipeline(UnityEngine.Rendering.RenderPipelineAsset a) { if (a is UnityEngine.Rendering.Universal.UniversalRenderPipelineAsset u) pipelines.Add(u); }
            AddPipeline(UnityEngine.Rendering.GraphicsSettings.defaultRenderPipeline);
            for (int i = 0; i < QualitySettings.count; i++) AddPipeline(QualitySettings.GetRenderPipelineAssetAt(i));

            var added = new List<string>();
            var already = new List<string>();
            // 機能を作ることと、Renderer の一覧に入れることを、1 回の「元に戻す」にまとめる
            // (別々だと、元に戻すで機能だけが消えて一覧に空の欄が残り、URP が毎フレームエラーを出す)
            Undo.IncrementCurrentGroup();
            int undoGroup = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName(label + "を有効にする");
            foreach (var pipeline in pipelines)
                foreach (var data in pipeline.rendererDataList)
                {
                    if (data == null || already.Contains(data.name) || added.Contains(data.name)) continue;
                    if (data.rendererFeatures.Any(f => f is T)) { already.Add(data.name); continue; }
                    // サブアセットとして足し、一覧と ID の対応表に入れる
                    var feature = ScriptableObject.CreateInstance<T>();
                    feature.name = typeof(T).Name;
                    Undo.RegisterCreatedObjectUndo(feature, label + "を有効にする");
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
            Undo.CollapseUndoOperations(undoGroup);
            AssetDatabase.SaveAssets();
            if (pipelines.Count == 0) Debug.LogWarning("[GalaxyToon] URP のパイプラインアセットが設定されていません(Project Settings > Graphics / Quality)。");
            else Debug.Log($"[GalaxyToon] {label}: 追加した Renderer = {(added.Count > 0 ? string.Join(", ", added) : "なし")} / もともと入っていた Renderer = {(already.Count > 0 ? string.Join(", ", already) : "なし")}\n{hint}");
        }

        /// <summary>1.0.2 で名前を変えた項目(前の名前, 今の名前)。前の名前で保存されたマテリアルの値を、今の名前へ移すのに使う。</summary>
        public static readonly (string from, string to)[] RenamedProperties =
        {
            ("_AAStrength", "_EdgeSmooth"), ("_BacklightBlur", "_BackLightSoft"), ("_BacklightBorder", "_BackLightEdge"), ("_BacklightColor", "_BackLightTint"), ("_BacklightDirectivity", "_BackLightFocus"), ("_BacklightMainStrength", "_BackLightAlbedoMix"), ("_BacklightViewStrength", "_BackLightViewMix"), ("_DissolveNoiseStrength", "_DissolveNoiseAmount"), ("_EmissionBlink", "_EmissionFlicker"), ("_GlitterColor", "_GlitterTint"), ("_GlitterShadowMask", "_GlitterShadeMask"), ("_LightMaxLimit", "_LightCeiling"), ("_LightMinLimit", "_LightFloor"), ("_MonochromeLighting", "_LightGray"), ("_MatCapPerspective", "_MatCapViewFix"), ("_MatCapShadowMask", "_MatCapShadeMask"), ("_OutlineFixWidth", "_OutlineNearClamp"), ("_OutlineWidthMask", "_OutlineThicknessMap"), ("_RimBlur", "_RimSoft"), ("_RimBorder", "_RimEdge"), ("_RimFresnelPower", "_RimFalloff"), ("_RimShadeBlur", "_RimDarkSoft"), ("_RimShadeBorder", "_RimDarkEdge"), ("_RimShadeColor", "_RimDarkTint"), ("_RimShadowMask", "_RimShadeFade"), ("_ShadowColor", "_Shade1Tint"), ("_ShadowBlur", "_Shade1Soft"), ("_ShadowBorder", "_Shade1Edge"), ("_Shadow2ndBlur", "_Shade2Soft"), ("_Shadow2ndBorder", "_Shade2Edge"), ("_Shadow2ndColor", "_Shade2Tint"), ("_Shadow3rdBlur", "_Shade3Soft"), ("_Shadow3rdBorder", "_Shade3Edge"), ("_Shadow3rdColor", "_Shade3Tint"), ("_ShadowReceive", "_CastShadowAmount"), ("_ShadowStrengthMask", "_ShadeAmountMap"), ("_SpecularBlur", "_SpecularSoft"),
        };

        [MenuItem("Tools/GalaxyToon/前の版のマテリアルを直す (項目名の変更に合わせる)", false, 61)]
        public static void UpgradeMaterials()
        {
            int count = 0;
            foreach (string guid in AssetDatabase.FindAssets("t:Material"))
            {
                var m = AssetDatabase.LoadAssetAtPath<Material>(AssetDatabase.GUIDToAssetPath(guid));
                if (m != null && m.shader != null && m.shader.name.StartsWith("GalaxyShader/") && UpgradeMaterial(m)) count++;
            }
            AssetDatabase.SaveAssets();
            Debug.Log($"[GalaxyToon] 前の版の項目名で保存されていたマテリアルを {count} 個直しました。");
        }

        /// <summary>前の名前で保存された値(色・数値・テクスチャ)を、今の名前へ移す。移した物があれば true。</summary>
        public static bool UpgradeMaterial(Material m)
        {
            var map = new Dictionary<string, string>();
            foreach (var (from, to) in RenamedProperties) { map[from] = to; map[from + "_Stash"] = to + "_Stash"; }
            var so = new SerializedObject(m);
            bool changed = false;
            foreach (string list in new[] { "m_SavedProperties.m_TexEnvs", "m_SavedProperties.m_Floats", "m_SavedProperties.m_Colors", "m_SavedProperties.m_Ints" })
            {
                SerializedProperty array = so.FindProperty(list);
                if (array == null) continue;
                string NameAt(int i)
                {
                    SerializedProperty first = array.GetArrayElementAtIndex(i).FindPropertyRelative("first");
                    return first.propertyType == SerializedPropertyType.String ? first.stringValue : first.FindPropertyRelative("name")?.stringValue;
                }
                for (int i = 0; i < array.arraySize; i++)
                {
                    string name = NameAt(i);
                    if (name == null || !map.TryGetValue(name, out string to)) continue;
                    // 今の名前の欄(シェーダーの初期値で作られた物)があれば消して、前の名前の値を今の名前にする
                    for (int k = array.arraySize - 1; k >= 0; k--)
                        if (k != i && NameAt(k) == to) { array.DeleteArrayElementAtIndex(k); if (k < i) i--; }
                    SerializedProperty first = array.GetArrayElementAtIndex(i).FindPropertyRelative("first");
                    if (first.propertyType == SerializedPropertyType.String) first.stringValue = to; else first.FindPropertyRelative("name").stringValue = to;
                    changed = true;
                }
            }
            if (!changed) return false;
            so.ApplyModifiedProperties();
            GalaxyToonGUI.Apply(m);
            EditorUtility.SetDirty(m);
            return true;
        }

        [MenuItem("Tools/GalaxyToon/使っている機能の一覧", false, 62)]
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
