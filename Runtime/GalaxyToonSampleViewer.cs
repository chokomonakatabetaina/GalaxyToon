using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace GalaxyToon
{
    /// <summary>サンプルシーン用: 「前へ / 次へ」で見本を1つずつ寄って見せる(←→ キー、Home で全体)。見本をクリックすると、そこへ寄る。
    /// プレビューのモデルに見本の素材を着せ替え(モデルはドラッグで回せる)、右側のボタンで Volume とライトのプリセットを切り替える。カメラに付ける。</summary>
    [AddComponentMenu("GalaxyToon/Samples/Galaxy Toon Sample Viewer")]
    [RequireComponent(typeof(Camera))]
    public sealed class GalaxyToonSampleViewer : MonoBehaviour
    {
        [System.Serializable]
        public struct Entry { public Transform target; public string title; [Tooltip("大きさの倍率(寄る距離・クリックで拾う範囲)。0 なら 1。")] public float size; }

        public List<Entry> entries = new List<Entry>();
        [Tooltip("寄ったときの見本からの距離(m)。")] public float distance = 2.4f;
        [Tooltip("カメラが動く速さ。")] public float speed = 6f;
        [Tooltip("プリセットを切り替える Volume(Galaxy Toon)。空ならボタンを出さない。")] public Volume volume;

        /// <summary>Volume のプリセット: 名前・リム・影を暗く・影の色・彩度・明るさ・輪郭線の太さ。</summary>
        public static readonly (string name, float rim, float darken, Color tint, float saturation, float brightness, float outline)[] VolumePresets =
        {
            ("標準 / Default", 1f, 0f, Color.white, 0f, 0f, 1f),
            ("夕方 / Sunset", 1.6f, 0.25f, new Color(1f, 0.75f, 0.6f), 0.15f, -0.05f, 1f),
            ("夜 / Night", 0.6f, 0.5f, new Color(0.55f, 0.6f, 1f), -0.35f, -0.35f, 1f),
            ("パステル / Pastel", 0.8f, 0f, new Color(1f, 0.9f, 1f), -0.3f, 0.15f, 0.6f),
            ("くっきり / Crisp", 1.4f, 0.35f, Color.white, 0.35f, 0f, 1.8f),
        };
        private int volumePreset = -1; // -1 = オフ

        [Tooltip("プリセットで向き・色を変えるライト(GalaxyToonLight が付いていればそれも)。空ならボタンを出さない。")] public Light sun;

        /// <summary>ライトのプリセット: 名前・向き(オイラー角)・色・強さ・環境光・夜の量(GalaxyToonLight.night)。</summary>
        public static readonly (string name, Vector3 euler, Color color, float intensity, Color ambient, float night)[] LightPresets =
        {
            ("昼 / Day", new Vector3(35f, -35f, 0f), new Color(1f, 0.98f, 0.95f), 1.2f, new Color(0.35f, 0.36f, 0.42f), 0f),
            ("夕方 / Sunset", new Vector3(10f, -70f, 0f), new Color(1f, 0.62f, 0.4f), 1.1f, new Color(0.35f, 0.25f, 0.3f), 0.2f),
            ("夜 / Night", new Vector3(45f, 40f, 0f), new Color(0.55f, 0.65f, 1f), 0.45f, new Color(0.1f, 0.12f, 0.22f), 1f),
            ("逆光 / Backlight", new Vector3(20f, 165f, 0f), Color.white, 1.3f, new Color(0.3f, 0.32f, 0.4f), 0f),
            ("真上 / Top", new Vector3(88f, 0f, 0f), Color.white, 1.2f, new Color(0.3f, 0.3f, 0.35f), 0f),
            ("真横 / Side", new Vector3(5f, -90f, 0f), new Color(1f, 0.92f, 0.85f), 1.2f, new Color(0.3f, 0.3f, 0.35f), 0f),
        };
        private int lightPreset;

        /// <summary>背景の色(カメラの背景)。右の列のボタンで選ぶ。</summary>
        public static readonly (string name, Color color)[] Backgrounds =
        {
            ("夜空 / Night Sky", new Color(0.04f, 0.04f, 0.06f)),
            ("黒 / Black", Color.black),
            ("灰 / Gray", new Color(0.42f, 0.43f, 0.46f)),
            ("白 / White", new Color(0.93f, 0.93f, 0.93f)),
            ("空 / Sky", new Color(0.55f, 0.74f, 0.95f)),
            ("夕焼け / Sunset", new Color(0.93f, 0.6f, 0.42f)),
        };
        private int background;
        [Tooltip("ライトの明るさの倍率(右の列のスライダー)。プリセットの明るさに掛ける。")] [Range(0.3f, 3f)] public float lightScale = 1f;

        [Tooltip("素材を着せ替えるプレビューのモデル。空ならボタンを出さない。")] public Renderer preview;
        [Tooltip("着せ替える素材と、その名前。")] public Material[] previewMaterials;
        public string[] previewNames;
        [Tooltip("素材ごとに、その素材のときだけ出す物(白い液など。previewMaterials と同じ並び。無ければ空)。")] public GameObject[] previewExtras;
        private int previewIndex;
        private bool dragging; // プレビューのモデルをマウスで回している
        private int lastPicked = -1;   // 最後にクリックした見本(ダブルクリックで、その素材をモデルに着せる)
        private string wornName;       // 素材の一覧に無い見本の素材を着せているときの、その名前
        [Tooltip("マウスでつかんで動かす小さい球。押せる肌の素材ではモデルに押し当てるとへこみ、糸の素材では押し当ててから離すと糸を引く。")] public Transform finger;
        [Tooltip("球を押し当てた所(糸の端)。プレビューのモデルの子にしておくと、モデルと一緒に回る。")] public Transform fingerAnchor;
        [Tooltip("球と押した所の間の糸(GalaxyStickyStrings)。糸の素材のとき、押し当てたら出す。")] public GameObject fingerStrings;
        [Tooltip("この素材をモデルに着せているときだけ、球で糸を引く(ほかの素材では出さない)。")] public Material stringsMaterial;
        private bool StringsMode => preview != null && stringsMaterial != null && preview.sharedMaterial == stringsMaterial;
        private bool draggingFinger;
        private float fingerDepth; // 球をつかんでいる奥行き(カメラの前向きの距離)
        private float previewZoom = 1f; // マウスホイールで寄る・離れる(プレビューのモデルを見ているとき)

        private int index = -1; // -1 = 全体
        private Vector3 overview, goal;
        private GUIStyle titleStyle, headerStyle, buttonStyle, noteStyle, selectedStyle, sliderStyle, thumbStyle;
        private Texture2D buttonBack, buttonHover, buttonOn, panelBack;

        private void Start()
        {
            overview = goal = transform.position;
            int at = PreviewEntry();
            if (at >= 0) { Show(at); transform.position = goal; } // はじめはプレビューのモデルを大きく見せる
            if (sun != null) SetLightPreset(lightPreset); // 明るさの倍率をはじめから入れる
        }

        private void LateUpdate() => transform.position = Vector3.Lerp(transform.position, goal, 1f - Mathf.Exp(-speed * Time.deltaTime));

        private void OnDestroy()
        {
            foreach (Texture2D t in new[] { buttonBack, buttonHover, buttonOn, panelBack }) if (t != null) Destroy(t);
        }

        private void Show(int i)
        {
            if (entries.Count == 0) return;
            index = i < -1 ? entries.Count - 1 : i >= entries.Count ? -1 : i; // 端まで行ったら全体を挟んで一周
            float zoom = index >= 0 && index == PreviewEntry() ? previewZoom : 1f;
            goal = index < 0 || entries[index].target == null ? overview : entries[index].target.position + new Vector3(0f, -0.15f, -distance * Size(index) * zoom);
        }

        private float Size(int i) => entries[i].size > 0f ? entries[i].size : 1f;

        private int PreviewEntry() => preview == null ? -1 : entries.FindIndex(e => e.target == preview.transform);

        private static Texture2D Solid(Color c)
        {
            var t = new Texture2D(1, 1) { hideFlags = HideFlags.HideAndDontSave };
            t.SetPixel(0, 0, c);
            t.Apply();
            return t;
        }

        // 透けない四角いボタン(ふだんは濃い灰色、マウスを乗せると明るく、選んでいる物は青)
        private void Styles(float scale)
        {
            if (buttonStyle == null)
            {
                buttonBack = Solid(new Color(0.16f, 0.17f, 0.21f));
                buttonHover = Solid(new Color(0.26f, 0.28f, 0.34f));
                buttonOn = Solid(new Color(0.22f, 0.45f, 0.85f));
                panelBack = Solid(new Color(0.08f, 0.08f, 0.1f));
                titleStyle = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold };
                titleStyle.normal.textColor = Color.white;
                headerStyle = new GUIStyle(titleStyle) { alignment = TextAnchor.MiddleLeft, wordWrap = false, clipping = TextClipping.Clip };
                headerStyle.normal.background = panelBack;
                buttonStyle = new GUIStyle { alignment = TextAnchor.MiddleCenter, border = new RectOffset(0, 0, 0, 0), clipping = TextClipping.Clip };
                buttonStyle.normal.background = buttonBack;
                buttonStyle.hover.background = buttonHover;
                buttonStyle.active.background = buttonOn;
                buttonStyle.onNormal.background = buttonOn;
                buttonStyle.onHover.background = buttonOn;
                buttonStyle.onActive.background = buttonOn;
                foreach (GUIStyleState s in new[] { buttonStyle.normal, buttonStyle.hover, buttonStyle.active, buttonStyle.onNormal, buttonStyle.onHover, buttonStyle.onActive })
                    s.textColor = Color.white;
                sliderStyle = new GUIStyle { border = new RectOffset(0, 0, 0, 0) };
                sliderStyle.normal.background = buttonHover;
                thumbStyle = new GUIStyle { border = new RectOffset(0, 0, 0, 0) };
                thumbStyle.normal.background = buttonOn;
                thumbStyle.hover.background = buttonOn;
                thumbStyle.active.background = buttonOn;
                selectedStyle = new GUIStyle(buttonStyle);
                selectedStyle.normal.background = buttonOn;
                selectedStyle.hover.background = buttonOn;
                noteStyle = new GUIStyle(buttonStyle) { wordWrap = true, alignment = TextAnchor.MiddleLeft };
                noteStyle.normal.background = panelBack;
                noteStyle.normal.textColor = new Color(1f, 0.85f, 0.5f);
            }
            titleStyle.fontSize = Mathf.RoundToInt(40 * scale);
            headerStyle.fontSize = Mathf.RoundToInt(28 * scale);
            headerStyle.padding = new RectOffset(Mathf.RoundToInt(10 * scale), 0, 0, 0);
            buttonStyle.fontSize = Mathf.RoundToInt(24 * scale);
            noteStyle.fontSize = Mathf.RoundToInt(18 * scale);
            noteStyle.padding = new RectOffset(Mathf.RoundToInt(8 * scale), Mathf.RoundToInt(8 * scale), 0, 0);
        }

        private void OnGUI()
        {
            Event e = Event.current;
            if (e.type == EventType.KeyDown)
            {
                if (e.keyCode == KeyCode.RightArrow) { Show(index + 1); e.Use(); }
                else if (e.keyCode == KeyCode.LeftArrow) { Show(index - 1); e.Use(); }
                else if (e.keyCode == KeyCode.Home) { Show(-1); e.Use(); }
            }

            float scale = Screen.height / 1080f;
            Styles(scale);

            // プレビューのモデルを見ているときは、着せている素材の番号と名前を出す(素材を替えると変わる)
            string title = index < 0 ? "GalaxyToon  全体 / Overview"
                : index == PreviewEntry() && previewMaterials != null && previewMaterials.Length > 0 ? $"プレビュー / Preview   {previewIndex + 1} / {previewMaterials.Length}   {PreviewName()}"
                : $"{index + 1} / {entries.Count}   {entries[index].title}";
            GUI.Label(new Rect(0f, 20f * scale, Screen.width, 60f * scale), title.Replace("\n", "  "), titleStyle);

            // 画面の下: 前へ・全体・次へ
            float w = 220f * scale, h = 60f * scale, y = Screen.height - h - 24f * scale, cx = Screen.width * 0.5f;
            if (GUI.Button(new Rect(cx - w * 1.5f - 8f * scale, y, w, h), "◀ 前へ / Prev", buttonStyle)) Show(index - 1);
            if (GUI.Button(new Rect(cx - w * 0.5f, y, w, h), "全体 / All", buttonStyle)) Show(-1);
            if (GUI.Button(new Rect(cx + w * 0.5f + 8f * scale, y, w, h), "次へ / Next ▶", buttonStyle)) Show(index + 1);
            if (preview != null && previewMaterials != null && previewMaterials.Length > 0) MaterialButtons(scale, y - h - 12f * scale);

            RightPanel(scale);

            if (e.button == 0 && Pointer(e.type, new Vector2(e.mousePosition.x, Screen.height - e.mousePosition.y), e.delta, e.clickCount)) e.Use();
        }

        /// <summary>
        /// ボタンが使わなかったマウスの操作。screen は画面の位置(左下が原点)。使ったら true。
        /// 押す: 画面上でいちばん近い見本へ寄る(見本の球にはコライダーが無い)。プレビューのモデルの上(またはモデルを見ているとき)は、そのままドラッグで回せる。
        /// ホイール: プレビューのモデルを見ているとき、寄る・離れる。
        /// </summary>
        private bool Pointer(EventType type, Vector2 screen, Vector2 delta, int clickCount = 1)
        {
            switch (type)
            {
                case EventType.MouseDown:
                    if (OnFinger(screen))
                    {
                        draggingFinger = true;
                        fingerDepth = Vector3.Dot(finger.position - transform.position, transform.forward);
                        return true;
                    }
                    int model = PreviewEntry();
                    // 見本をダブルクリック: その見本の素材をプレビューのモデルに着せて、モデルへ寄る
                    // (1 回目のクリックでカメラが動くので、2 回目は位置で探さず、1 回目に押した見本を使う)
                    if (clickCount >= 2 && lastPicked >= 0 && lastPicked != model && model >= 0)
                    {
                        WearSample(lastPicked);
                        return true;
                    }
                    int hit = Pick(screen);
                    lastPicked = hit;
                    dragging = model >= 0 && (hit == model || (hit < 0 && index == model));
                    if (hit >= 0) Show(hit);
                    return hit >= 0 || dragging;
                case EventType.MouseDrag when draggingFinger:
                    MoveFinger(screen);
                    return true;
                case EventType.MouseDrag when dragging:
                    preview.transform.Rotate(Vector3.up, -delta.x * 0.4f, Space.World);
                    preview.transform.Rotate(transform.right, -delta.y * 0.4f, Space.World);
                    return true;
                case EventType.MouseUp:
                    dragging = draggingFinger = false;
                    return false;
                case EventType.ScrollWheel when index >= 0 && index == PreviewEntry():
                    previewZoom = Mathf.Clamp(previewZoom * (1f + delta.y * 0.05f), 0.35f, 2.5f);
                    Show(index);
                    return true;
                default:
                    return false;
            }
        }

        /// <summary>右側の列: Volume(オフ・プリセット)と、ライトのプリセット。Volume の機能が Renderer に無ければ、足し方を出す。</summary>
        private void RightPanel(float scale)
        {
            float w = 230f * scale, h = 40f * scale, gap = 4f * scale, x = Screen.width - w - 20f * scale, y = 100f * scale;
            if (volume != null)
            {
                GUI.Label(new Rect(x, y, w, h), "Volume", headerStyle);
                for (int i = -1; i < VolumePresets.Length; i++)
                {
                    y += h + gap;
                    string label = i < 0 ? "オフ / Off" : VolumePresets[i].name;
                    if (GUI.Toggle(new Rect(x, y, w, h), volumePreset == i, label, buttonStyle) && volumePreset != i) SetVolumePreset(i);
                }
                if (!HasVolumeFeature()) // 足していないと、どのボタンも効かないので、はじめから出す
                {
                    y += h + gap;
                    GUI.Label(new Rect(x, y, w, h * 2f), "Tools > GalaxyToon > Volume を有効にする が必要です", noteStyle);
                    y += h;
                }
                y += h * 0.6f;
            }
            if (sun != null)
            {
                y += h + gap;
                GUI.Label(new Rect(x, y, w, h), "ライト / Light", headerStyle);
                for (int i = 0; i < LightPresets.Length; i++)
                {
                    y += h + gap;
                    if (GUI.Toggle(new Rect(x, y, w, h), lightPreset == i, LightPresets[i].name, buttonStyle) && lightPreset != i) SetLightPreset(i);
                }
                // 明るさ(プリセットの明るさに掛ける倍率)
                y += h + gap;
                GUI.Label(new Rect(x, y, w, h), $"明るさ / Brightness  x{lightScale:0.0}", headerStyle);
                y += h + gap;
                // 太い溝と大きいつまみ(ボタンと同じ高さ。つかみやすく)
                sliderStyle.fixedHeight = h * 0.45f;
                thumbStyle.fixedWidth = h * 0.9f;
                thumbStyle.fixedHeight = h;
                thumbStyle.margin = new RectOffset(0, 0, Mathf.RoundToInt(-h * 0.275f), 0);
                GUI.Box(new Rect(x, y, w, h), GUIContent.none, headerStyle);
                float next = GUI.HorizontalSlider(new Rect(x + h * 0.5f, y + h * 0.275f, w - h, h * 0.45f), lightScale, 0.3f, 3f, sliderStyle, thumbStyle);
                if (!Mathf.Approximately(next, lightScale)) { lightScale = next; SetLightPreset(lightPreset); }
                y += h;
            }

            // 背景の色: 色の四角のボタンを横に並べる(選んでいる物は青い枠)
            y += h * 0.6f + gap;
            GUI.Label(new Rect(x, y, w, h), "背景 / Background", headerStyle);
            y += h + gap;
            float size = (w - gap * (Backgrounds.Length - 1)) / Backgrounds.Length;
            for (int i = 0; i < Backgrounds.Length; i++)
            {
                var cell = new Rect(x + i * (size + gap), y, size, size);
                if (GUI.Button(cell, new GUIContent("", Backgrounds[i].name), background == i ? selectedStyle : buttonStyle)) SetBackground(i);
                Color before = GUI.color;
                GUI.color = Backgrounds[i].color;
                GUI.DrawTexture(new Rect(cell.x + 4f * scale, cell.y + 4f * scale, cell.width - 8f * scale, cell.height - 8f * scale), Texture2D.whiteTexture);
                GUI.color = before;
            }
        }

        /// <summary>背景の色 i にする(カメラの背景の色)。</summary>
        public void SetBackground(int i)
        {
            if (i < 0 || i >= Backgrounds.Length) return;
            background = i;
            var cam = GetComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = Backgrounds[i].color;
        }

        /// <summary>画面の下(前へ・次への上)に、プレビューのモデルの素材を切り替えるボタン。押すとモデルへ寄る。</summary>
        private void MaterialButtons(float scale, float y)
        {
            float bw = 70f * scale, h = 52f * scale, lw = 560f * scale, x = Screen.width * 0.5f - lw * 0.5f - bw - 6f * scale;
            if (GUI.Button(new Rect(x, y, bw, h), "◀", buttonStyle)) SetPreviewMaterial(previewIndex - 1);
            GUI.Label(new Rect(x + bw + 6f * scale, y, lw, h), "素材 / Material:  " + PreviewName(), headerStyle);
            if (GUI.Button(new Rect(x + bw + lw + 12f * scale, y, bw, h), "▶", buttonStyle)) SetPreviewMaterial(previewIndex + 1);
        }

        /// <summary>ライトのプリセット i(向き・色・強さ・環境光)を入れる。</summary>
        public void SetLightPreset(int i)
        {
            if (i < 0 || i >= LightPresets.Length) return;
            lightPreset = i;
            var p = LightPresets[i];
            RenderSettings.ambientLight = p.ambient * Mathf.Sqrt(lightScale); // 環境光も少しついてくる
            if (sun == null) return;
            sun.transform.rotation = Quaternion.Euler(p.euler);
            sun.color = p.color;
            sun.intensity = p.intensity * lightScale;
            if (sun.TryGetComponent(out GalaxyToonLight toon))
            {
                toon.color = p.color;
                toon.intensity = p.intensity * lightScale;
                toon.night = p.night;
            }
        }

        /// <summary>いまプレビューのモデルに着せている素材の名前。</summary>
        private string PreviewName()
        {
            if (wornName != null) return wornName;
            string name = previewNames != null && previewIndex < previewNames.Length ? previewNames[previewIndex] : previewMaterials[previewIndex].name;
            return name.Replace("\n", " ");
        }

        /// <summary>見本 i の素材を、プレビューのモデルに着せて、モデルへ寄る。素材の一覧にある物はその番号に、無い物(瞳など)はそのまま着せる。</summary>
        public void WearSample(int i)
        {
            if (preview == null || i < 0 || i >= entries.Count || entries[i].target == null) return;
            Renderer source = entries[i].target.GetComponentInChildren<Renderer>();
            if (source == null || source.sharedMaterial == null) return;
            int at = previewMaterials != null ? System.Array.IndexOf(previewMaterials, source.sharedMaterial) : -1;
            if (at >= 0) { SetPreviewMaterial(at); return; }
            preview.sharedMaterial = source.sharedMaterial;
            wornName = entries[i].title.Replace("\n", " ");
            ShowExtras(-1);
            if (fingerStrings != null) fingerStrings.SetActive(false);
            int model = PreviewEntry();
            if (model >= 0) Show(model);
        }

        /// <summary>プレビューのモデルを i 番目の素材にする(端は一周)。モデルへ寄る。</summary>
        public void SetPreviewMaterial(int i)
        {
            if (preview == null || previewMaterials == null || previewMaterials.Length == 0) return;
            previewIndex = (i % previewMaterials.Length + previewMaterials.Length) % previewMaterials.Length;
            preview.sharedMaterial = previewMaterials[previewIndex];
            wornName = null;
            ShowExtras(previewIndex);
            if (fingerStrings != null && !StringsMode) fingerStrings.SetActive(false); // 糸は糸の素材のときだけ
            int at = PreviewEntry();
            if (at >= 0 && index != at) Show(at);
        }

        /// <summary>i 番目の素材の部品だけを出す(-1 ですべて隠す)。</summary>
        private void ShowExtras(int i)
        {
            if (previewExtras == null) return;
            for (int j = 0; j < previewExtras.Length; j++)
                if (previewExtras[j] != null) previewExtras[j].SetActive(j == i);
        }

        /// <summary>プリセット i を Volume に入れる。-1 でオフ。</summary>
        public void SetVolumePreset(int i)
        {
            volumePreset = i;
            if (volume == null) return;
            volume.enabled = i >= 0;
            if (i < 0 || volume.sharedProfile == null || !volume.sharedProfile.TryGet(out GalaxyToonVolume v)) return;
            var p = VolumePresets[i];
            v.active = true;
            v.rim.Override(p.rim);
            v.shadowDarken.Override(p.darken);
            v.shadowTint.Override(p.tint);
            v.saturation.Override(p.saturation);
            v.brightness.Override(p.brightness);
            v.outlineWidth.Override(p.outline);
        }

        private static bool HasVolumeFeature()
        {
            if (!(GraphicsSettings.currentRenderPipeline is UniversalRenderPipelineAsset urp)) return false;
            foreach (var data in urp.rendererDataList)
                if (data != null) foreach (var f in data.rendererFeatures) if (f is GalaxyToonVolumeFeature && f.isActive) return true;
            return false;
        }

        /// <summary>画面の位置 screen(左下が原点)が、つかめる小さい球の上か。</summary>
        private bool OnFinger(Vector2 screen)
        {
            if (finger == null) return false;
            var cam = GetComponent<Camera>();
            Vector3 center = cam.WorldToScreenPoint(finger.position);
            if (center.z <= 0f) return false;
            float radius = Vector2.Distance(center, cam.WorldToScreenPoint(finger.position + transform.right * (finger.lossyScale.x * 0.5f)));
            return Vector2.Distance(center, screen) <= Mathf.Max(radius * 1.3f, 12f);
        }

        /// <summary>つかんだ球を、マウスの下へ動かす。プレビューのモデルの上なら、押せる肌では面へ少しめり込ませ(へこむ)、
        /// 糸の素材では面に触れさせて、触れた所を糸の端にする。モデルから外れたら、いまの奥行きのまま動かす(糸が伸びて切れる)。</summary>
        private void MoveFinger(Vector2 screen)
        {
            var cam = GetComponent<Camera>();
            Ray ray = cam.ScreenPointToRay(screen);
            float radius = finger.lossyScale.x * 0.5f;
            var surface = preview != null ? preview.GetComponent<Collider>() : null;
            Physics.SyncTransforms(); // ドラッグで回したモデルの向きを、当たり判定にすぐ入れる
            if (surface != null && surface.Raycast(ray, out RaycastHit hit, 1000f))
            {
                if (StringsMode)
                {
                    finger.position = hit.point + hit.normal * radius; // 面に触れる所まで(へこませない)
                    if (fingerAnchor != null) fingerAnchor.position = hit.point;
                    if (fingerStrings != null && !fingerStrings.activeSelf) fingerStrings.SetActive(true); // 触れた所から糸を引く
                }
                else finger.position = hit.point + hit.normal * (radius * 0.1f); // 押せる肌: 球の 9 割ほど押し込む(深くへこむ)
                fingerDepth = Vector3.Dot(finger.position - transform.position, transform.forward);
                return;
            }
            float along = Mathf.Max(Vector3.Dot(ray.direction, transform.forward), 1e-3f);
            finger.position = ray.origin + ray.direction * (fingerDepth / along);
        }

        /// <summary>画面の位置 screen(左下が原点)に、見本の球(半径 0.5m)が写っていれば、その番号。いちばん近い物。無ければ -1。</summary>
        private int Pick(Vector2 screen)
        {
            var cam = GetComponent<Camera>();
            int best = -1;
            float bestDistance = float.MaxValue;
            for (int i = 0; i < entries.Count; i++)
            {
                Transform t = entries[i].target;
                if (t == null) continue;
                Vector3 center = cam.WorldToScreenPoint(t.position);
                if (center.z <= 0f) continue; // カメラの後ろ
                float radius = Vector2.Distance(center, cam.WorldToScreenPoint(t.position + cam.transform.right * (0.5f * Size(i))));
                float d = Vector2.Distance(center, screen);
                if (d <= radius && d < bestDistance) { best = i; bestDistance = d; }
            }
            return best;
        }
    }
}
