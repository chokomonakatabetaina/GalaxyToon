using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using GalaxyToon.Editor;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace GalaxyToon.Tests
{
    /// <summary>テスト用の窓: 渡した処理を OnGUI の中で呼ぶ(Inspector の描画を、本物の GUI の中で通す)。</summary>
    public sealed class GalaxyToonTestHostWindow : EditorWindow
    {
        public static Action Draw;
        /// <summary>次の OnGUI で使うイベント(クリック・キー)。SendEvent はあとで処理されるので、描くときに差し替える。</summary>
        public static readonly Queue<Event> Pending = new Queue<Event>();
        private void OnGUI()
        {
            if (Pending.Count > 0) Event.current = Pending.Dequeue();
            Draw?.Invoke();
        }
    }

    /// <summary>ツールの窓(描画と「作成」)・マテリアルの Inspector・画面の効果を、実際に動かして確かめる。</summary>
    public class GalaxyToonWindowTests
    {
        private const string Temp = "Assets/GalaxyToonTestTemp";
        private const BindingFlags Any = BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance;
        private readonly List<Object> created = new List<Object>();
        private T Keep<T>(T o) where T : Object { created.Add(o); return o; }

        [SetUp]
        public void SetUp() { if (!AssetDatabase.IsValidFolder(Temp)) AssetDatabase.CreateFolder("Assets", "GalaxyToonTestTemp"); }

        [TearDown]
        public void TearDown()
        {
            foreach (Object o in created) if (o != null && !EditorUtility.IsPersistent(o)) Object.DestroyImmediate(o);
            created.Clear();
            Selection.objects = new Object[0];
            AssetDatabase.DeleteAsset(Temp);
        }

        private static void Set(object o, string name, object v) => o.GetType().GetField(name, Any).SetValue(o, v);
        private static object Get(object o, string name) => o.GetType().GetField(name, Any).GetValue(o);
        private static void Call(object o, string name) => o.GetType().GetMethod(name, Any, null, Type.EmptyTypes, null).Invoke(o, null);

        /// <summary>窓の OnGUI を、いまここで(Layout と Repaint の両方)描かせる。</summary>
        private static void Repaint(EditorWindow w)
        {
            MethodInfo now = typeof(EditorWindow).GetMethod("RepaintImmediately", Any);
            Assert.IsNotNull(now, "EditorWindow.RepaintImmediately が無い");
            now.Invoke(w, null);
            now.Invoke(w, null);
        }

        private Material ToonAsset(string name)
        {
            var m = new Material(Shader.Find("GalaxyShader/Toon")) { name = name };
            AssetDatabase.CreateAsset(m, $"{Temp}/{name}.mat");
            return m;
        }

        private MeshRenderer SphereWith(Material m)
        {
            var go = Keep(GameObject.CreatePrimitive(PrimitiveType.Sphere));
            go.GetComponent<MeshRenderer>().sharedMaterial = m;
            return go.GetComponent<MeshRenderer>();
        }

        // ================================================================ マテリアルの Inspector

        [Test]
        public void Inspector_DrawsInBothLanguagesAndWhileSearching()
        {
            bool english = GalaxyToonText.English;
            Material m = ToonAsset("Inspector");
            m.SetTexture("_BaseMap", Texture2D.whiteTexture);
            m.SetFloat("_RenderMode", 1f);
            m.SetFloat("_StencilComp", (float)CompareFunction.NotEqual);
            GalaxyToonGUI.Apply(m);
            var editor = (MaterialEditor)UnityEditor.Editor.CreateEditor(m);
            var host = EditorWindow.GetWindow<GalaxyToonTestHostWindow>();
            try
            {
                // MaterialEditor.OnInspectorGUI は Inspector で開いていないと中身を描かないので、GalaxyToon の GUI を直接呼ぶ
                var shaderGui = (ShaderGUI)typeof(MaterialEditor).GetProperty("customShaderGUI", Any).GetValue(editor);
                Assert.IsInstanceOf<GalaxyToonGUI>(shaderGui);
                GalaxyToonTestHostWindow.Draw = () => shaderGui.OnGUI(editor, MaterialEditor.GetMaterialProperties(new Object[] { m }));
                foreach (bool en in new[] { false, true })
                {
                    GalaxyToonText.English = en;
                    GalaxyToonGUI.SetTextureOn(m, "_BaseMap", en); // テクスチャを「オフ」にしている行も描く
                    foreach (bool accordion in new[] { true, false })
                    {
                        EditorPrefs.SetBool("GalaxyToonGUI.Accordion", accordion);
                        foreach (var (title, _) in GalaxyToonGUI.Sections) EditorPrefs.SetBool("GalaxyToonGUI.Fold." + title, true); // 全部開いて描く
                        EditorPrefs.SetBool("GalaxyToonGUI.Fold.表示の設定", true);
                        Repaint(host);
                    }
                }
                // 検索中は、名前に合う項目だけを描く(合う物が無いときの表示も)
                foreach (string query in new[] { "影", "rim color", "見つからない語" })
                {
                    Set(shaderGui, "search", query);
                    Repaint(host);
                }
                Set(shaderGui, "search", null);
                // 閉じた見出しと、プリセット・表示の設定を閉じた状態も描く
                foreach (var (title, _) in GalaxyToonGUI.Sections) EditorPrefs.SetBool("GalaxyToonGUI.Fold." + title, false);
                EditorPrefs.SetBool("GalaxyToonGUI.Fold.Presets", false);
                EditorPrefs.SetBool("GalaxyToonGUI.Fold.表示の設定", false);
                Repaint(host);
                EditorPrefs.SetBool("GalaxyToonGUI.Fold.Presets", true);
                Assert.AreEqual(1f, m.GetFloat("_RenderMode"), "描くだけで値は変えない");
            }
            finally
            {
                GalaxyToonTestHostWindow.Draw = null;
                host.Close();
                Object.DestroyImmediate(editor);
                GalaxyToonText.English = english;
                foreach (var (title, _) in GalaxyToonGUI.Sections) EditorPrefs.DeleteKey("GalaxyToonGUI.Fold." + title);
                EditorPrefs.DeleteKey("GalaxyToonGUI.Fold.表示の設定");
                EditorPrefs.DeleteKey("GalaxyToonGUI.Accordion");
            }
        }

        [Test]
        public void Inspector_ChangingTheShaderAppliesTheRenderMode()
        {
            var m = Keep(new Material(Shader.Find("Universal Render Pipeline/Lit")));
            m.shader = Shader.Find("GalaxyShader/Toon");
            new GalaxyToonGUI().AssignNewShaderToMaterial(m, Shader.Find("Universal Render Pipeline/Lit"), m.shader);
            Assert.AreEqual("Opaque", m.GetTag("RenderType", false));
        }

        // ================================================================ ツールの窓

        [Test]
        public void SdfWindow_DrawsAndBakesIntoTheMaterial()
        {
            Material m = ToonAsset("Face_SDF");
            MeshRenderer r = SphereWith(m);
            var w = EditorWindow.GetWindow<GalaxyToonSdfBaker>();
            try
            {
                Repaint(w); // 対象なし
                Set(w, "target", r);
                Set(w, "resolution", 64);
                foreach (object kind in Enum.GetValues(typeof(GalaxyToonSdfBaker.Kind))) { Set(w, "kind", kind); Repaint(w); }
                foreach (object axis in Enum.GetValues(typeof(GalaxyToonSdfBaker.Axis)))
                {
                    Set(w, "forwardAxis", axis);
                    Set(w, "savePath", $"{Temp}/Sdf_{axis}.png");
                    Call(w, "Bake");
                }
                Repaint(w); // 下見の画像も描く
                Assert.IsNotNull(m.GetTexture("_FaceSdfTex"));
                Assert.IsTrue(m.IsKeywordEnabled("_FACE_SDF"));
                Set(w, "materialIndex", 5);
                Call(w, "Bake");
                StringAssert.Contains("サブメッシュ", (string)Get(w, "message"));
                r.GetComponent<MeshFilter>().sharedMesh = Keep(new Mesh { vertices = new[] { Vector3.zero, Vector3.right, Vector3.up }, triangles = new[] { 0, 1, 2 } });
                Set(w, "materialIndex", 0);
                Call(w, "Bake");
                StringAssert.Contains("UV", (string)Get(w, "message"), "焼けないメッシュは言葉で知らせる");
                r.GetComponent<MeshFilter>().sharedMesh = null;
                Call(w, "Bake");
                StringAssert.Contains("メッシュ", (string)Get(w, "message"));
            }
            finally { w.Close(); }
        }

        [Test]
        public void ThicknessWindow_DrawsAndBakesIntoTheMaterial()
        {
            Material m = ToonAsset("Skin_Thickness");
            MeshRenderer r = SphereWith(m);
            var w = EditorWindow.GetWindow<GalaxyToonThicknessBaker>();
            try
            {
                Repaint(w);
                Set(w, "_target", r);
                Set(w, "_resolution", 32);
                Set(w, "_savePath", $"{Temp}/Thickness.png");
                Repaint(w);
                Call(w, "Bake");
                Repaint(w);
                Assert.IsNotNull(m.GetTexture("_ThicknessMap"));
                Assert.AreEqual(1f, m.GetFloat("_UseSss"));
                Set(w, "_applyToMaterial", false);
                Call(w, "Bake");
                Set(w, "_materialIndex", 4);
                Call(w, "Bake");
                StringAssert.Contains("サブメッシュ", (string)Get(w, "_message"));
                r.GetComponent<MeshFilter>().sharedMesh = null;
                Call(w, "Bake");
                StringAssert.Contains("メッシュ", (string)Get(w, "_message"));
            }
            finally { w.Close(); }
        }

        [Test]
        public void NormalWindow_DrawsAndBakesIntoTheMaterial()
        {
            Material m = ToonAsset("Cloth_Normal");
            var source = new Texture2D(16, 16);
            AssetDatabase.CreateAsset(source, $"{Temp}/Source.asset");
            m.SetTexture("_BaseMap", source);
            MeshRenderer r = SphereWith(m);
            var w = EditorWindow.GetWindow<GalaxyToonNormalBaker>();
            try
            {
                Repaint(w);
                Set(w, "target", r);
                Repaint(w); // マテリアルからテクスチャを拾う
                Assert.AreEqual(source, Get(w, "source"));
                Set(w, "savePath", $"{Temp}/Normal.png");
                Call(w, "Bake");
                Repaint(w);
                Assert.IsNotNull(m.GetTexture("_BumpMap"));
                Assert.IsTrue(m.IsKeywordEnabled("_NORMALMAP"));
                Set(w, "applyToMaterial", false);
                Call(w, "Bake");
            }
            finally { w.Close(); }
        }

        [Test]
        public void RampEditor_SavesAndRestoresTheMaterialWhenClosedWithoutSaving()
        {
            Material m = ToonAsset("Ramp_Target");
            var w = EditorWindow.GetWindow<GalaxyToonRampEditorWindow>();
            Set(w, "_targetMaterial", m);
            Repaint(w);
            Call(w, "ApplyLivePreview");
            Assert.IsNotNull(m.GetTexture("_ShadowRampTex"), "編集中はマテリアルに映す");
            w.Close(); // 保存せずに閉じる
            Assert.IsNull(m.GetTexture("_ShadowRampTex"), "保存せずに閉じたら、元(空)に戻す(消えた一時テクスチャを指したままにしない)");

            w = EditorWindow.GetWindow<GalaxyToonRampEditorWindow>();
            try
            {
                Set(w, "_targetMaterial", m);
                Set(w, "_slot", GalaxyToonRampEditorWindow.TargetSlot.SpecularRamp);
                Set(w, "_savePath", $"{Temp}/Ramp_Saved.png");
                Repaint(w);
                Call(w, "ApplyLivePreview");
                Call(w, "SaveAndAssign");
                Assert.AreEqual(AssetDatabase.LoadAssetAtPath<Texture2D>($"{Temp}/Ramp_Saved.png"), m.GetTexture("_SpecularRampTex"));
                var target = SphereWith(m);
                Selection.activeGameObject = target.gameObject;
                Repaint(w);
            }
            finally { w.Close(); }
            Assert.AreEqual(AssetDatabase.LoadAssetAtPath<Texture2D>($"{Temp}/Ramp_Saved.png"), m.GetTexture("_SpecularRampTex"), "保存した物は閉じても残る");
        }

        [Test]
        public void MigratorWindow_Draws()
        {
            var face = SphereWith(Keep(new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = "Face_SKIN" }));
            Selection.activeGameObject = face.gameObject;
            var w = EditorWindow.GetWindow<GalaxyToonMigrator>();
            try
            {
                Call(w, "Collect");
                Repaint(w);
                Selection.activeGameObject = null;
                Call(w, "Collect");
                Repaint(w); // 何も選んでいないとき
            }
            finally { w.Close(); }
        }

        // ================================================================ サンプルのビューア(画面のボタン・キー・マウス)

        /// <summary>次の OnGUI をこのイベントで描く(キー)。マウスのイベントは、実際のマウスが窓の上に無いと Ignore にされるので使わない。</summary>
        private static void Send(EditorWindow w, Event e)
        {
            GalaxyToonTestHostWindow.Pending.Enqueue(e);
            typeof(EditorWindow).GetMethod("RepaintImmediately", Any).Invoke(w, null);
            GalaxyToonTestHostWindow.Pending.Clear();
        }

        private static bool Pointer(GalaxyToonSampleViewer v, EventType type, Vector2 screen, Vector2 delta = default, int clicks = 1) =>
            (bool)typeof(GalaxyToonSampleViewer).GetMethod("Pointer", Any).Invoke(v, new object[] { type, screen, delta, clicks });

        private static void Show(GalaxyToonSampleViewer v, int i) => typeof(GalaxyToonSampleViewer).GetMethod("Show", Any).Invoke(v, new object[] { i });

        [Test]
        public void SampleViewer_DrawsKeysPicksDragsAndZooms()
        {
            var host = EditorWindow.GetWindow<GalaxyToonTestHostWindow>();
            host.position = new Rect(100f, 100f, 1600f, 900f);
            Color ambient = RenderSettings.ambientLight; // ライトのプリセットが環境光を変えるので、あとで戻す
            var camGo = Keep(new GameObject("Viewer Camera"));
            var cam = camGo.AddComponent<Camera>();
            var viewer = camGo.AddComponent<GalaxyToonSampleViewer>();
            MethodInfo onGui = typeof(GalaxyToonSampleViewer).GetMethod("OnGUI", Any);
            GalaxyToonTestHostWindow.Draw = () => onGui.Invoke(viewer, null);
            try
            {
                Repaint(host); // 見本も Volume もライトも無いとき
                cam.targetTexture = Keep(new RenderTexture(800, 600, 16));
                var front = Keep(GameObject.CreatePrimitive(PrimitiveType.Sphere));
                front.transform.position = new Vector3(0f, 0f, 5f);
                var behind = Keep(new GameObject("Behind")).transform;
                behind.position = new Vector3(0f, 0f, -5f);
                viewer.entries.Add(new GalaxyToonSampleViewer.Entry { target = front.transform, title = "前\nFront" });
                viewer.entries.Add(new GalaxyToonSampleViewer.Entry { target = behind, title = "後ろ" });
                viewer.entries.Add(new GalaxyToonSampleViewer.Entry { target = null, title = "空" });
                var sun = Keep(new GameObject("Sun")).AddComponent<Light>();
                sun.gameObject.AddComponent<GalaxyToonLight>();
                viewer.sun = sun;
                var volume = Keep(new GameObject("Volume")).AddComponent<Volume>();
                volume.sharedProfile = Keep(ScriptableObject.CreateInstance<VolumeProfile>());
                volume.sharedProfile.Add<GalaxyToonVolume>(true);
                viewer.volume = volume;
                viewer.preview = front.GetComponent<Renderer>();
                viewer.previewMaterials = new[] { front.GetComponent<Renderer>().sharedMaterial, Keep(new Material(Shader.Find("GalaxyShader/Toon"))) };
                Repaint(host); // 全部ある画面を描く(Volume・ライト・明るさ・素材の行)
                viewer.SetVolumePreset(1);
                Repaint(host); // プリセットを選んだ状態も

                // キー: → で次、← で前(全体を挟んで一周)、Home で全体。ほかのキーは使わない
                Send(host, new Event { type = EventType.KeyDown, keyCode = KeyCode.RightArrow });
                Assert.AreEqual(0, (int)Get(viewer, "index"));
                Send(host, new Event { type = EventType.KeyDown, keyCode = KeyCode.LeftArrow });
                Send(host, new Event { type = EventType.KeyDown, keyCode = KeyCode.LeftArrow });
                Assert.AreEqual(2, (int)Get(viewer, "index"), "全体から ← で最後へ");
                Send(host, new Event { type = EventType.KeyDown, keyCode = KeyCode.Home });
                Assert.AreEqual(-1, (int)Get(viewer, "index"));
                Send(host, new Event { type = EventType.KeyDown, keyCode = KeyCode.A });

                // マウス: 見本の球(画面の真ん中)を押すと寄る。何も無い所は何もしない
                Vector2 center = new Vector2(400f, 300f);
                Assert.IsTrue(Pointer(viewer, EventType.MouseDown, center));
                Assert.AreEqual(0, (int)Get(viewer, "index"), "押した見本へ寄る");
                // そのままドラッグで回す
                Quaternion before = front.transform.rotation;
                Assert.IsTrue(Pointer(viewer, EventType.MouseDrag, center, new Vector2(40f, 10f)));
                Assert.AreNotEqual(before, front.transform.rotation, "ドラッグで回る");
                Assert.IsFalse(Pointer(viewer, EventType.MouseUp, center));
                Assert.IsFalse(Pointer(viewer, EventType.MouseDrag, center, new Vector2(40f, 0f)), "離したら回さない");
                // ホイールで寄る・離れる(範囲の中で)
                Assert.IsTrue(Pointer(viewer, EventType.ScrollWheel, center, new Vector2(0f, -40f)));
                Assert.AreEqual(0.35f, (float)Get(viewer, "previewZoom"), 1e-4f, "寄りすぎない");
                Assert.IsTrue(Pointer(viewer, EventType.ScrollWheel, center, new Vector2(0f, 400f)));
                Assert.AreEqual(2.5f, (float)Get(viewer, "previewZoom"), 1e-4f, "離れすぎない");
                // 見本の外を押す・モデルを見ていないときのホイールは使わない
                Show(viewer, -1);
                Assert.IsFalse(Pointer(viewer, EventType.MouseDown, new Vector2(2f, 2f)));
                Assert.IsFalse(Pointer(viewer, EventType.ScrollWheel, center, new Vector2(0f, 1f)));
                Assert.IsFalse(Pointer(viewer, EventType.KeyUp, center));

                // つかめる小さい球: モデルに押し当てるとめり込み、押した所に糸の端。外へ引くと同じ奥行きのまま動く
                var finger = Keep(GameObject.CreatePrimitive(PrimitiveType.Sphere)).transform;
                finger.position = new Vector3(1.2f, 0f, 4f);
                finger.localScale = Vector3.one * 0.24f;
                var anchor = Keep(new GameObject("Anchor")).transform;
                var strings = Keep(new GameObject("Strings"));
                strings.SetActive(false);
                viewer.finger = finger; viewer.fingerAnchor = anchor; viewer.fingerStrings = strings;
                // 押せる肌(糸の素材でない): めり込むが、糸は出さない
                viewer.stringsMaterial = viewer.previewMaterials[1];
                viewer.SetPreviewMaterial(0);
                Assert.IsTrue(Pointer(viewer, EventType.MouseDown, (Vector2)cam.WorldToScreenPoint(finger.position)), "球をつかむ");
                Assert.IsTrue(Pointer(viewer, EventType.MouseDrag, center));
                Assert.Less(Vector3.Distance(finger.position, front.transform.position), 0.6f, "球は面に押し込む(触れるだけなら 0.62)");
                Assert.IsFalse(strings.activeSelf, "押せる肌では糸を出さない");
                // 糸: 触れた所が糸の端。めり込ませない
                viewer.SetPreviewMaterial(1);
                Assert.IsTrue(Pointer(viewer, EventType.MouseDrag, center));
                Assert.Less(Vector3.Distance(anchor.position, front.transform.position), 0.6f, "触れた所はモデルの面");
                Assert.AreEqual(0.12f, Vector3.Distance(finger.position, anchor.position), 0.01f, "球は面に触れるだけ");
                Assert.IsTrue(strings.activeSelf, "糸の素材では糸を出す");
                Assert.IsTrue(Pointer(viewer, EventType.MouseDrag, new Vector2(760f, 300f)));
                Assert.Greater(Vector3.Distance(finger.position, anchor.position), 1f, "離すと糸の端から遠ざかる");
                Pointer(viewer, EventType.MouseUp, center);
                Assert.IsFalse(Pointer(viewer, EventType.MouseDrag, center), "離したら動かさない");
                viewer.SetPreviewMaterial(0);
                Assert.IsFalse(strings.activeSelf, "ほかの素材にしたら糸を消す");

                // 見本をダブルクリック: その見本の素材をモデルに着せて、モデルへ寄る(一覧にある素材は番号で、無い素材はそのまま)
                var sample = Keep(GameObject.CreatePrimitive(PrimitiveType.Sphere));
                sample.transform.position = new Vector3(3f, 0f, 8f);
                sample.GetComponent<Renderer>().sharedMaterial = viewer.previewMaterials[1];
                viewer.entries.Add(new GalaxyToonSampleViewer.Entry { target = sample.transform, title = "見本" });
                viewer.SetPreviewMaterial(0);
                Show(viewer, -1);
                Vector2 at = cam.WorldToScreenPoint(sample.transform.position);
                Assert.IsTrue(Pointer(viewer, EventType.MouseDown, at));
                Assert.AreEqual(3, (int)Get(viewer, "index"), "1 回目のクリックは見本へ寄る");
                Assert.IsTrue(Pointer(viewer, EventType.MouseDown, at, default, 2));
                Assert.AreEqual(viewer.previewMaterials[1], front.GetComponent<Renderer>().sharedMaterial, "ダブルクリックで、その素材をモデルに着せる");
                Assert.AreEqual(0, (int)Get(viewer, "index"), "モデルへ寄る");
                Material extra = Keep(new Material(Shader.Find("GalaxyShader/Toon")));
                sample.GetComponent<Renderer>().sharedMaterial = extra;
                viewer.WearSample(3);
                Assert.AreEqual(extra, front.GetComponent<Renderer>().sharedMaterial, "一覧に無い素材も着せられる");
                Repaint(host); // 題に、着せた見本の名前が出る
                viewer.WearSample(99);
                viewer.SetPreviewMaterial(0);

                // 明るさ: プリセットの明るさに掛かる
                viewer.lightScale = 2f;
                viewer.SetLightPreset(2);
                Assert.AreEqual(GalaxyToonSampleViewer.LightPresets[2].intensity * 2f, sun.intensity, 1e-4f);
                Assert.AreEqual(GalaxyToonSampleViewer.LightPresets[2].intensity * 2f, sun.GetComponent<GalaxyToonLight>().intensity, 1e-4f);
            }
            finally
            {
                GalaxyToonTestHostWindow.Draw = null;
                host.Close();
                RenderSettings.ambientLight = ambient;
                cam.targetTexture = null;
            }
        }


        // ================================================================ 窓の中を実際に押す(ボタン・スライダー・トグル・折りたたみ)

        /// <summary>窓の中の位置 (x, y) を押して離す。SendEvent の位置は窓の左上から(タブの高さを含む)なので、その分を足す。</summary>
        private static void Press(EditorWindow w, float x, float y, float tab)
        {
            try
            {
                w.SendEvent(new Event { type = EventType.MouseDown, mousePosition = new Vector2(x, y + tab), button = 0, clickCount = 1 });
                w.SendEvent(new Event { type = EventType.MouseUp, mousePosition = new Vector2(x, y + tab), button = 0, clickCount = 1 });
            }
            catch (Exception) { /* IMGUI の中断(ExitGUI)など: 次を押す */ }
        }

        /// <summary>窓の中を、上から下へ格子状に押していく(押すと形が変わる所も、その後の形で押す)。</summary>
        private static void PressEverywhere(EditorWindow w, float height, float tab)
        {
            for (float y = 3f; y < height; y += 10f)
                foreach (float x in new[] { 40f, 240f, 450f, 650f })
                    if (w != null) Press(w, x, y, tab);
        }

        /// <summary>タブの高さ: 窓の y=200 へ送ったイベントが、OnGUI では何 px に見えるか。</summary>
        private static float TabHeight(GalaxyToonTestHostWindow host)
        {
            float seen = -1f;
            GalaxyToonTestHostWindow.Draw = () => { if (Event.current.rawType == EventType.MouseDown) seen = Event.current.mousePosition.y; };
            host.SendEvent(new Event { type = EventType.MouseDown, mousePosition = new Vector2(50f, 200f), button = 0 });
            host.SendEvent(new Event { type = EventType.MouseUp, mousePosition = new Vector2(50f, 200f), button = 0 });
            GalaxyToonTestHostWindow.Draw = null;
            return seen >= 0f ? 200f - seen : 26f;
        }

        [Test, Explicit("Inspector のボタン・スライダーを全部押す(数分かかる。Temp のマテリアルだけを対象にする)"), Category("ProjectWide"), Timeout(1200000)]
        public void Inspector_EveryControlCanBePressed()
        {
            LogAssert.ignoreFailingMessages = true; // 押した結果の警告は気にしない(落ちない・固まらないことを見る)
            bool english = GalaxyToonText.English;
            Material m = ToonAsset("Pressed");
            var source = new Texture2D(16, 16);
            AssetDatabase.CreateAsset(source, $"{Temp}/PressedSource.asset");
            m.SetTexture("_BaseMap", source);
            m.SetTexture("_BumpMap", source);
            m.SetFloat("_RenderMode", 1f);
            GalaxyToonGUI.Apply(m);
            MeshRenderer target = SphereWith(m);
            target.name = "Face_Pressed";
            Selection.activeGameObject = target.gameObject;
            var editor = (MaterialEditor)UnityEditor.Editor.CreateEditor(m);
            var host = EditorWindow.GetWindow<GalaxyToonTestHostWindow>();
            host.position = new Rect(60f, 60f, 700f, 900f);
            var windows = new List<EditorWindow>();
            try
            {
                float tab = TabHeight(host);

                // マテリアルの Inspector(日本語・英語): 縦に長いので、ずらしながら押す
                var shaderGui = (ShaderGUI)typeof(MaterialEditor).GetProperty("customShaderGUI", Any).GetValue(editor);
                float scroll = 0f;
                GalaxyToonTestHostWindow.Draw = () =>
                {
                    GUILayout.BeginArea(new Rect(0f, -scroll, 700f, 6000f));
                    shaderGui.OnGUI(editor, MaterialEditor.GetMaterialProperties(new Object[] { m }));
                    GUILayout.EndArea();
                };
                // 見出しを 1 つずつ開いて、その見出しの中を押す(全部開くと縦に長すぎる)。最後に、全部閉じた状態で見出しの行を押す
                GalaxyToonText.English = false;
                EditorPrefs.SetBool("GalaxyToonGUI.Accordion", true);
                var titles = GalaxyToonGUI.Sections.Select(x => x.Item1).ToList();
                titles.Add(null);
                foreach (string open in titles)
                {
                    foreach (var (title, _) in GalaxyToonGUI.Sections) EditorPrefs.SetBool("GalaxyToonGUI.Fold." + title, title == open);
                    EditorPrefs.SetBool("GalaxyToonGUI.Fold.表示の設定", open == null);
                    EditorPrefs.SetBool("GalaxyToonGUI.Fold.Presets", open == null);
                    for (scroll = 0f; scroll < (open == null ? 1600f : 2400f); scroll += 800f) PressEverywhere(host, 800f, tab);
                }
            }
            finally
            {
                GalaxyToonTestHostWindow.Draw = null;
                host.Close();
                foreach (EditorWindow w in Resources.FindObjectsOfTypeAll<EditorWindow>()) // 押して開いた色・グラデーションの窓を閉じる
                    if (w != null && (w.GetType().Name == "ColorPicker" || w.GetType().Name == "GradientPicker" || w.GetType().Name == "ObjectSelector")) w.Close();
                Object.DestroyImmediate(editor);
                GalaxyToonText.English = english;
                if (GalaxyToonTools.ShaderOff) GalaxyToonTools.ToggleShaderOff(); // 押されて OFF のままにしない
                foreach (var (title, _) in GalaxyToonGUI.Sections) EditorPrefs.DeleteKey("GalaxyToonGUI.Fold." + title);
                foreach (string key in new[] { "GalaxyToonGUI.Fold.表示の設定", "GalaxyToonGUI.Fold.Presets", "GalaxyToonGUI.Fold.その他", "GalaxyToonGUI.Accordion", "GalaxyToonGUI.HeaderInUseColor", "GalaxyToonGUI.ItemInUseColor" })
                    EditorPrefs.DeleteKey(key);
            }
        }

        [Test, Explicit("ツールの窓のボタン・スライダーを全部押す(数分かかる。Temp のマテリアルと球だけを対象にする)"), Category("ProjectWide"), Timeout(1200000)]
        public void ToolWindows_EveryControlCanBePressed()
        {
            LogAssert.ignoreFailingMessages = true;
            Material m = ToonAsset("PressedTool");
            var source = new Texture2D(16, 16);
            AssetDatabase.CreateAsset(source, $"{Temp}/PressedToolSource.asset");
            m.SetTexture("_BaseMap", source);
            GalaxyToonGUI.Apply(m);
            MeshRenderer target = SphereWith(m);
            target.name = "Face_Pressed";
            var host = EditorWindow.GetWindow<GalaxyToonTestHostWindow>();
            host.position = new Rect(60f, 60f, 700f, 900f);
            var windows = new List<EditorWindow>();
            try
            {
                float tab = TabHeight(host);
                host.Close();
                // 対象は Temp のマテリアルの球(保存先も Temp)。焼く解像度は小さくしておく
                foreach (Type t in new[] { typeof(GalaxyToonSdfBaker), typeof(GalaxyToonNormalBaker), typeof(GalaxyToonThicknessBaker), typeof(GalaxyToonRampEditorWindow), typeof(GalaxyToonMigrator) })
                {
                    EditorWindow w = EditorWindow.GetWindow(t);
                    windows.Add(w);
                    w.position = new Rect(60f, 60f, 700f, 720f);
                    Selection.activeGameObject = target.gameObject;
                    t.GetMethod("OnSelectionChange", Any)?.Invoke(w, null);
                    foreach (string field in new[] { "resolution", "_resolution" })
                        t.GetField(field, Any)?.SetValue(w, 256);
                    Repaint(w);
                    PressEverywhere(w, 700f, tab);
                }
            }
            finally
            {
                foreach (EditorWindow w in windows) if (w != null) w.Close();
                foreach (EditorWindow w in Resources.FindObjectsOfTypeAll<EditorWindow>())
                    if (w != null && (w.GetType().Name == "ColorPicker" || w.GetType().Name == "GradientPicker" || w.GetType().Name == "ObjectSelector")) w.Close();
            }
        }

        // ================================================================ 画面の効果(Renderer に足すので、一時プロジェクトだけで流す)

        [Test]
        public void RendererFeatures_DisposeCleanly()
        {
            foreach (Type t in new[] { typeof(GalaxyScreenLineFeature), typeof(GalaxyGlowFeature), typeof(GalaxyPaintFeature), typeof(GalaxyToonVolumeFeature), typeof(GalaxyCharacterShadowFeature), typeof(GalaxyHairShadowFeature) })
            {
                var f = (ScriptableRendererFeature)ScriptableObject.CreateInstance(t);
                f.Create();
                f.Dispose();
                Object.DestroyImmediate(f);
            }
            Assert.AreEqual(Vector4.zero, Shader.GetGlobalVector("_GalaxyVolumeA"), "Volume の値を 0 に戻す");
        }

        [Test, Explicit("使っている URP Renderer に画面の効果を足して描く"), Category("ProjectWide")]
        public void ScreenEffects_RenderWithEveryFeature()
        {
            GalaxyToonTools.AddRendererFeature<GalaxyScreenLineFeature>("画面の線", "");
            GalaxyToonTools.AddRendererFeature<GalaxyGlowFeature>("グロー", "");
            GalaxyToonTools.AddRendererFeature<GalaxyPaintFeature>("塗り", "");
            GalaxyToonTools.AddRendererFeature<GalaxyCharacterShadowFeature>("キャラの影", "");
            GalaxyToonTools.EnableVolume();
            GalaxyToonTools.EnableHairShadow();
            var urp = (UniversalRenderPipelineAsset)GraphicsSettings.currentRenderPipeline;
            foreach (var f in urp.rendererDataList[0].rendererFeatures)
            {
                if (f is GalaxyScreenLineFeature line) line.objectLines = true;
                if (f is GalaxyGlowFeature glow) glow.characterDepth = 50f;
            }

            Material m = ToonAsset("Character");
            m.SetFloat("_ObjectId", 3f);
            m.SetFloat("_HairShadowReceive", 1f);
            m.SetColor("_EmissionColor", Color.white * 2f);
            GalaxyToonGUI.Apply(m);
            var root = Keep(new GameObject("Character"));
            var head = new GameObject("Head").transform;
            head.SetParent(root.transform, false);
            MeshRenderer body = SphereWith(m);
            body.transform.SetParent(root.transform, false);
            root.AddComponent<GalaxyToonFace>();
            var light = Keep(new GameObject("Sun")).AddComponent<Light>();
            light.type = LightType.Directional;
            light.transform.rotation = Quaternion.Euler(40f, -30f, 0f);
            var toonLight = light.gameObject.AddComponent<GalaxyToonLight>();
            var volumeGo = Keep(new GameObject("Volume"));
            var volume = volumeGo.AddComponent<Volume>();
            volume.isGlobal = true;
            volume.sharedProfile = Keep(ScriptableObject.CreateInstance<VolumeProfile>());
            volume.sharedProfile.Add<GalaxyToonVolume>(true).rim.Override(2f);
            var cam = Keep(new GameObject("Camera")).AddComponent<Camera>();
            cam.GetUniversalAdditionalCameraData().requiresDepthTexture = true;
            cam.transform.position = new Vector3(0f, 0f, -3f);
            var rt = Keep(new RenderTexture(128, 128, 24));
            cam.targetTexture = rt;
            bool async = ShaderUtil.allowAsyncCompilation;
            ShaderUtil.allowAsyncCompilation = false;
            try
            {
                for (int i = 0; i < 3; i++) cam.Render();
                Assert.AreNotEqual(Vector4.zero, Shader.GetGlobalVector("_GalaxyVolumeA"), "Volume の値が入る");
                volumeGo.SetActive(false);
                cam.Render();
                Assert.AreEqual(Vector4.zero, Shader.GetGlobalVector("_GalaxyVolumeA"), "Volume が無ければ 0");
                // キャラの影: キャラ専用ライトが無ければシーンのメインライト、それも無ければ使わない
                toonLight.enabled = false;
                cam.Render();
                light.enabled = false;
                cam.Render();
                light.enabled = true;
                // プレビューのカメラでは、キャラの影を使わない
                cam.cameraType = CameraType.Preview;
                cam.Render();
                Assert.AreEqual(Vector4.zero, Shader.GetGlobalVector("_GalaxyCharShadowParams"));
                cam.cameraType = CameraType.Game;
                foreach (var f in urp.rendererDataList[0].rendererFeatures) if (f != null) f.SetActive(false);
                cam.Render();
                Assert.AreEqual(Vector4.zero, Shader.GetGlobalVector("_GalaxyVolumeA"), "止めたら戻る");
                GalaxyCharacterShadowFeature.ClearGlobals();
            }
            finally
            {
                ShaderUtil.allowAsyncCompilation = async;
                foreach (var f in urp.rendererDataList[0].rendererFeatures) if (f != null) f.SetActive(true);
                cam.targetTexture = null;
                toonLight.enabled = false;
            }
        }
    }
}
