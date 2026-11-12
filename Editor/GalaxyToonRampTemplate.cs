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

        [MenuItem("Tools/GalaxyToon/ランプのひな形を作る (影・ハイライト、部位4段)", false, 43)]
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
            WriteRows(path, Rows, pixel);
        }

        public static void WriteRows(string path, int rows, System.Func<int, float, Color> pixel)
        {
            var tex = new Texture2D(Width, rows * RowHeight, TextureFormat.RGBA32, false, false);
            for (int y = 0; y < rows * RowHeight; y++)
            {
                int row = rows - 1 - y / RowHeight; // 画像の上 = 段 0
                for (int x = 0; x < Width; x++)
                {
                    Color c = pixel(row, x / (Width - 1f));
                    c.a = 1f;
                    tex.SetPixel(x, y, c); // 色はそのまま(sRGB で読む)
                }
            }
            tex.Apply();
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path)));
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

    /// <summary>
    /// 部位ごとの影ランプ・ハイライトランプを Unity のグラデーション(Gradient)でリアルタイムに編集・プレビューして PNG 保存するウィンドウ。
    /// 「明暗境界の血色帯(Pre-Integrated SSS)」や「部位別マルチランプ」を画像ソフト無しで直接作れる。
    /// </summary>
    public sealed class GalaxyToonRampEditorWindow : EditorWindow
    {
        public enum TargetSlot { ShadowRamp = 0, SpecularRamp = 1 }

        private Renderer _targetRenderer;
        private int _materialIndex;
        private Material _targetMaterial;
        private TargetSlot _slot = TargetSlot.ShadowRamp;
        private int _rowCount = 4;
        [SerializeField] private Gradient[] _gradients;
        [SerializeField] private string[] _rowLabels = { "段0: 肌 (SSS血色帯つき)", "段1: 髪 (2段影)", "段2: 服 (ソフト2段)", "段3: 金属 (コントラスト+照り返し)", "段4", "段5", "段6", "段7" };
        private bool _livePreview = true;
        private string _savePath = "";
        private Texture2D _previewTex;
        private string _message;

        private static readonly string[] SlotNames = { "影のランプ (_ShadowRampTex)", "ハイライトのランプ (_SpecularRampTex)" };

        [MenuItem("Tools/GalaxyToon/ランプを編集・作成する (リアルタイム・グラデーション)", false, 44)]
        public static void Open()
        {
            GetWindow<GalaxyToonRampEditorWindow>("ランプ編集");
        }

        private void OnEnable()
        {
            if (_gradients == null || _gradients.Length != 8)
            {
                InitDefaultGradients(TargetSlot.ShadowRamp);
            }
            UpdateSelection();
        }

        // リアルタイム反映の前の値(保存せずに閉じたら戻す。一時テクスチャは閉じると消えるので、マテリアルに残すと空になる)
        private Material _previewedMaterial;
        private Texture _originalShadowRamp, _originalSpecularRamp;
        private float _originalRows;

        private void RestorePreviewedMaterial()
        {
            if (_previewedMaterial != null)
            {
                if (_previewedMaterial.GetTexture("_ShadowRampTex") == _previewTex) _previewedMaterial.SetTexture("_ShadowRampTex", _originalShadowRamp);
                if (_previewedMaterial.GetTexture("_SpecularRampTex") == _previewTex) _previewedMaterial.SetTexture("_SpecularRampTex", _originalSpecularRamp);
                _previewedMaterial.SetFloat("_ShadowRampRows", _originalRows);
                GalaxyToonGUI.Apply(_previewedMaterial);
            }
            _previewedMaterial = null;
        }

        private void OnDisable()
        {
            RestorePreviewedMaterial();
            if (_previewTex != null)
            {
                DestroyImmediate(_previewTex);
                _previewTex = null;
            }
        }

        private void OnSelectionChange()
        {
            UpdateSelection();
            Repaint();
        }

        private void UpdateSelection()
        {
            if (Selection.activeObject is Material mat && mat.shader != null && mat.shader.name == "GalaxyShader/Toon")
            {
                _targetMaterial = mat;
                _targetRenderer = null;
            }
            else if (Selection.activeGameObject != null && Selection.activeGameObject.TryGetComponent(out Renderer r) && r != _targetRenderer)
            {
                _targetRenderer = r;
                _materialIndex = 0;
                SyncMaterialFromRenderer();
            }
        }

        private void SyncMaterialFromRenderer()
        {
            if (_targetRenderer == null)
            {
                return;
            }
            Material[] mats = _targetRenderer.sharedMaterials;
            _targetMaterial = _materialIndex < mats.Length ? mats[_materialIndex] : null;
        }

        private void InitDefaultGradients(TargetSlot slot)
        {
            _gradients = new Gradient[8];
            for (int i = 0; i < 8; i++)
            {
                _gradients[i] = CreatePresetGradient(slot, i);
            }
        }

        private static Gradient CreatePresetGradient(TargetSlot slot, int row)
        {
            var g = new Gradient();
            if (slot == TargetSlot.ShadowRamp)
            {
                switch (row)
                {
                    case 0: // 肌(Pre-Integrated SSS: 暗い影 → 境界の鮮やかな赤橙色 → 明るい肌)
                        g.SetKeys(
                            new[]
                            {
                                new GradientColorKey(new Color(0.80f, 0.52f, 0.54f), 0.0f),
                                new GradientColorKey(new Color(0.95f, 0.68f, 0.66f), 0.44f),
                                new GradientColorKey(new Color(1.00f, 0.56f, 0.46f), 0.50f),
                                new GradientColorKey(Color.white, 0.56f),
                                new GradientColorKey(Color.white, 1.0f),
                            },
                            new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 1f) });
                        break;
                    case 1: // 髪(紫寄りの2段影)
                        g.SetKeys(
                            new[]
                            {
                                new GradientColorKey(new Color(0.45f, 0.38f, 0.72f), 0.0f),
                                new GradientColorKey(new Color(0.45f, 0.38f, 0.72f), 0.28f),
                                new GradientColorKey(new Color(0.72f, 0.64f, 0.92f), 0.31f),
                                new GradientColorKey(new Color(0.72f, 0.64f, 0.92f), 0.50f),
                                new GradientColorKey(Color.white, 0.53f),
                                new GradientColorKey(Color.white, 1.0f),
                            },
                            new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 1f) });
                        break;
                    case 2: // 服(青みのある2段)
                        g.SetKeys(
                            new[]
                            {
                                new GradientColorKey(new Color(0.42f, 0.46f, 0.72f), 0.0f),
                                new GradientColorKey(new Color(0.68f, 0.72f, 0.90f), 0.32f),
                                new GradientColorKey(new Color(0.68f, 0.72f, 0.90f), 0.48f),
                                new GradientColorKey(Color.white, 0.54f),
                                new GradientColorKey(Color.white, 1.0f),
                            },
                            new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 1f) });
                        break;
                    default: // 金属(深い影 + 照り返し)
                        g.SetKeys(
                            new[]
                            {
                                new GradientColorKey(new Color(0.28f, 0.28f, 0.36f), 0.0f),
                                new GradientColorKey(new Color(0.62f, 0.64f, 0.74f), 0.36f),
                                new GradientColorKey(Color.white, 0.55f),
                                new GradientColorKey(new Color(1.0f, 0.97f, 0.90f), 0.80f),
                                new GradientColorKey(Color.white, 1.0f),
                            },
                            new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 1f) });
                        break;
                }
            }
            else
            {
                switch (row)
                {
                    case 0: // 肌ハイライト
                        g.SetKeys(
                            new[] { new GradientColorKey(Color.black, 0f), new GradientColorKey(Color.black, 0.5f), new GradientColorKey(new Color(0.65f, 0.62f, 0.60f), 1f) },
                            new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 1f) });
                        break;
                    case 1: // 髪(2段リング)
                        g.SetKeys(
                            new[]
                            {
                                new GradientColorKey(Color.black, 0f),
                                new GradientColorKey(new Color(0.45f, 0.42f, 0.40f), 0.32f),
                                new GradientColorKey(Color.black, 0.55f),
                                new GradientColorKey(new Color(1f, 0.98f, 0.92f), 0.75f),
                                new GradientColorKey(Color.white, 1f),
                            },
                            new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 1f) });
                        break;
                    default: // 金属ハイライト
                        g.SetKeys(
                            new[]
                            {
                                new GradientColorKey(Color.black, 0f),
                                new GradientColorKey(new Color(0.7f, 0.68f, 0.62f), 0.52f),
                                new GradientColorKey(Color.black, 0.65f),
                                new GradientColorKey(Color.white, 0.85f),
                                new GradientColorKey(Color.white, 1f),
                            },
                            new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 1f) });
                        break;
                }
            }
            return g;
        }

        private void OnGUI()
        {
            EditorGUILayout.HelpBox(
                "部位ごとの影ランプ・ハイライトランプをグラデーションで直接編集します。\n「リアルタイム反映」が ON なら、グラデーションを動かすと即座にシーンのモデルへ反映されます。",
                MessageType.Info);

            EditorGUI.BeginChangeCheck();
            _targetRenderer = (Renderer)EditorGUILayout.ObjectField(new GUIContent("対象の Renderer (任意)"), _targetRenderer, typeof(Renderer), true);
            if (EditorGUI.EndChangeCheck() && _targetRenderer != null)
            {
                _materialIndex = 0;
                SyncMaterialFromRenderer();
            }
            if (_targetRenderer != null)
            {
                Material[] mats = _targetRenderer.sharedMaterials;
                var names = new string[mats.Length];
                for (int i = 0; i < mats.Length; i++)
                {
                    names[i] = $"{i}: {(mats[i] != null ? mats[i].name : "(なし)")}";
                }
                int newIdx = EditorGUILayout.Popup(new GUIContent("マテリアル"), Mathf.Clamp(_materialIndex, 0, Mathf.Max(0, mats.Length - 1)), names);
                if (newIdx != _materialIndex)
                {
                    _materialIndex = newIdx;
                    SyncMaterialFromRenderer();
                }
            }
            _targetMaterial = (Material)EditorGUILayout.ObjectField(new GUIContent("反映するマテリアル"), _targetMaterial, typeof(Material), false);

            EditorGUI.BeginChangeCheck();
            _slot = (TargetSlot)EditorGUILayout.Popup(new GUIContent("編集するランプ"), (int)_slot, SlotNames);
            if (EditorGUI.EndChangeCheck())
            {
                _savePath = "";
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                _rowCount = EditorGUILayout.IntSlider(new GUIContent("段数 (部位の数)"), _rowCount, 1, 8);
                if (GUILayout.Button("初期グラデーションに戻す", GUILayout.Width(160f)))
                {
                    InitDefaultGradients(_slot);
                    ApplyLivePreview();
                }
            }

            EditorGUI.BeginChangeCheck();
            for (int r = 0; r < _rowCount; r++)
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    _rowLabels[r] = EditorGUILayout.TextField(_rowLabels[r], GUILayout.Width(190f));
                    _gradients[r] = EditorGUILayout.GradientField(_gradients[r]);
                }
            }
            bool gradChanged = EditorGUI.EndChangeCheck();

            _livePreview = EditorGUILayout.Toggle(new GUIContent("マテリアルにリアルタイム反映", "編集中の一時テクスチャをマテリアルに入れて、その場で見た目を確認します。"), _livePreview);
            if (gradChanged)
            {
                RebuildPreviewTexture();
                if (_livePreview)
                {
                    ApplyLivePreview();
                }
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                if (string.IsNullOrEmpty(_savePath))
                {
                    _savePath = DefaultSavePath();
                }
                _savePath = EditorGUILayout.TextField(new GUIContent("保存先 (.png)"), _savePath);
                if (GUILayout.Button("選ぶ", GUILayout.Width(44f)))
                {
                    string dir = Path.GetDirectoryName(_savePath);
                    string picked = EditorUtility.SaveFilePanelInProject(
                        "ランプの保存先",
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

            EditorGUILayout.Space(6f);
            if (GUILayout.Button("PNG に保存してマテリアルに反映", GUILayout.Height(28f)))
            {
                SaveAndAssign();
            }
            if (!string.IsNullOrEmpty(_message))
            {
                EditorGUILayout.HelpBox(_message, MessageType.None);
            }
            if (_previewTex != null)
            {
                Rect rect = GUILayoutUtility.GetAspectRect(4f, GUILayout.MaxWidth(360f));
                EditorGUI.DrawPreviewTexture(rect, _previewTex, null, ScaleMode.StretchToFill);
            }
        }

        private string DefaultSavePath()
        {
            string matPath = _targetMaterial != null ? AssetDatabase.GetAssetPath(_targetMaterial) : "";
            string dir = string.IsNullOrEmpty(matPath) ? "Assets" : Path.GetDirectoryName(matPath).Replace('\\', '/');
            string baseName = _targetMaterial != null ? _targetMaterial.name : "GalaxyToon";
            string suffix = _slot == TargetSlot.ShadowRamp ? "ShadowRamp" : "SpecularRamp";
            return $"{dir}/{baseName}_{suffix}.png";
        }

        private void RebuildPreviewTexture()
        {
            int h = _rowCount * GalaxyToonRampTemplate.RowHeight;
            if (_previewTex == null || _previewTex.height != h)
            {
                if (_previewTex != null)
                {
                    DestroyImmediate(_previewTex);
                }
                _previewTex = new Texture2D(GalaxyToonRampTemplate.Width, h, TextureFormat.RGBA32, false, false)
                {
                    wrapMode = TextureWrapMode.Clamp,
                    filterMode = FilterMode.Bilinear,
                    name = "GalaxyToon_LiveRampPreview",
                };
            }
            for (int y = 0; y < h; y++)
            {
                int row = _rowCount - 1 - y / GalaxyToonRampTemplate.RowHeight;
                Gradient grad = _gradients[Mathf.Clamp(row, 0, _gradients.Length - 1)];
                for (int x = 0; x < GalaxyToonRampTemplate.Width; x++)
                {
                    Color c = grad.Evaluate(x / (GalaxyToonRampTemplate.Width - 1f));
                    c.a = 1f;
                    _previewTex.SetPixel(x, y, c);
                }
            }
            _previewTex.Apply();
        }

        private void ApplyLivePreview()
        {
            if (_targetMaterial == null)
            {
                return;
            }
            if (_previewTex == null)
            {
                RebuildPreviewTexture();
            }
            string prop = _slot == TargetSlot.ShadowRamp ? "_ShadowRampTex" : "_SpecularRampTex";
            if (!_targetMaterial.HasProperty(prop))
            {
                return;
            }
            if (_previewedMaterial != _targetMaterial)
            {
                RestorePreviewedMaterial(); // 前に映していたマテリアルを元に戻してから、いまの値を覚える
                _previewedMaterial = _targetMaterial;
                _originalShadowRamp = _targetMaterial.GetTexture("_ShadowRampTex");
                _originalSpecularRamp = _targetMaterial.GetTexture("_SpecularRampTex");
                _originalRows = _targetMaterial.GetFloat("_ShadowRampRows");
            }
            _targetMaterial.SetTexture(prop, _previewTex);
            _targetMaterial.SetFloat("_ShadowRampRows", _rowCount);
            GalaxyToonGUI.Apply(_targetMaterial);
            SceneView.RepaintAll();
        }

        private void SaveAndAssign()
        {
            if (string.IsNullOrEmpty(_savePath))
            {
                _savePath = DefaultSavePath();
            }
            GalaxyToonRampTemplate.WriteRows(_savePath, _rowCount, (row, t) =>
            {
                Gradient grad = _gradients[Mathf.Clamp(row, 0, _gradients.Length - 1)];
                return grad.Evaluate(t);
            });
            var savedAsset = AssetDatabase.LoadAssetAtPath<Texture2D>(_savePath);
            if (_targetMaterial != null)
            {
                string prop = _slot == TargetSlot.ShadowRamp ? "_ShadowRampTex" : "_SpecularRampTex";
                if (_targetMaterial.HasProperty(prop))
                {
                    Undo.RecordObject(_targetMaterial, "ランプを反映");
                    _targetMaterial.SetTexture(prop, savedAsset);
                    if (_previewedMaterial == _targetMaterial)
                    {
                        // 保存した側は閉じても戻さない。もう一方のランプに一時テクスチャが残っていれば元に戻す
                        string other = prop == "_ShadowRampTex" ? "_SpecularRampTex" : "_ShadowRampTex";
                        if (_targetMaterial.GetTexture(other) == _previewTex) _targetMaterial.SetTexture(other, other == "_ShadowRampTex" ? _originalShadowRamp : _originalSpecularRamp);
                        _previewedMaterial = null;
                    }
                    _targetMaterial.SetFloat("_ShadowRampRows", _rowCount);
                    GalaxyToonGUI.Apply(_targetMaterial);
                    EditorUtility.SetDirty(_targetMaterial);
                    AssetDatabase.SaveAssets();
                }
            }
            _message = $"保存しました: {_savePath}";
        }
    }
}
