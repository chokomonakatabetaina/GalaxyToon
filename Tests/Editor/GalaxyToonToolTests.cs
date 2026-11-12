using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using GalaxyToon.Editor;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace GalaxyToon.Tests
{
    /// <summary>
    /// エディタのツール(Inspector の処理・移行・ノーマルマップ・SDF・厚みマップ・なめらか法線・ランプ・瞳と頬)の点検。
    /// 作ったファイルは Assets/GalaxyToonTestTemp に置き、終わったら消す。
    /// プロジェクト全体に手を入れるもの(全マテリアルの整理・Renderer への追加・サンプルシーン・GPU の総当たり)は [Explicit] にしてある。
    /// </summary>
    public class GalaxyToonToolTests
    {
        private const string Temp = "Assets/GalaxyToonTestTemp";
        private static Shader Toon => Shader.Find("GalaxyShader/Toon");
        private readonly List<Object> created = new List<Object>();

        [SetUp]
        public void SetUp()
        {
            if (!AssetDatabase.IsValidFolder(Temp)) AssetDatabase.CreateFolder("Assets", "GalaxyToonTestTemp");
        }

        [TearDown]
        public void TearDown()
        {
            foreach (Object o in created) if (o != null && !EditorUtility.IsPersistent(o)) Object.DestroyImmediate(o);
            created.Clear();
            Selection.objects = new Object[0];
            AssetDatabase.DeleteAsset(Temp);
        }

        private T Keep<T>(T o) where T : Object { created.Add(o); return o; }
        private Material NewToon(string name = "Test_Toon") => Keep(new Material(Toon) { name = name });

        private static Texture2D Solid(int w, int h, Color c)
        {
            var t = new Texture2D(w, h, TextureFormat.RGBA32, false);
            t.SetPixels(Enumerable.Repeat(c, w * h).ToArray());
            t.Apply();
            return t;
        }

        private static Mesh Sphere()
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            Mesh m = Object.Instantiate(go.GetComponent<MeshFilter>().sharedMesh);
            Object.DestroyImmediate(go);
            return m;
        }

        // ================================================================ 表示の言語

        [Test]
        public void Text_SwitchesBetweenJapaneseAndEnglish()
        {
            bool before = GalaxyToonText.English;
            try
            {
                GalaxyToonText.English = false;
                Assert.AreEqual("あ", GalaxyToonText.L("あ", "a"));
                Assert.AreEqual("影", GalaxyToonText.Section("影"));
                Assert.AreEqual("肌", GalaxyToonText.PresetName("肌"));
                Assert.AreEqual("説明", GalaxyToonText.PresetTooltip("肌", "説明"));
                GalaxyToonText.English = true;
                Assert.AreEqual("a", GalaxyToonText.L("あ", "a"));
                Assert.AreEqual("Shadow", GalaxyToonText.Section("影"));
                Assert.AreEqual("見出しに無い名前", GalaxyToonText.Section("見出しに無い名前"));
                Assert.AreNotEqual("肌", GalaxyToonText.PresetName("肌"));
                Assert.AreNotEqual("x", GalaxyToonText.PresetTooltip("肌", "x"));
                Assert.AreEqual("無い", GalaxyToonText.PresetTooltip("無いプリセット", "無い"));
                // 同じ名前の「金属」は、背景側を説明文で見分ける
                Assert.That(GalaxyToonText.PresetTooltip("金属", "むき出しの金属(配管・手すり・機械)。金属 1・つや 0.7、映り込みが地の色に染まる。"), Does.StartWith("Bare metal"));
                Material m = NewToon();
                foreach (MaterialProperty p in MaterialEditor.GetMaterialProperties(new Object[] { m }))
                    if ((p.propertyFlags & ShaderPropertyFlags.HideInInspector) == 0) Assert.IsFalse(string.IsNullOrEmpty(GalaxyToonText.Property(p)), p.name);
            }
            finally { GalaxyToonText.English = before; }
        }

        [Test]
        public void Text_EverySectionHasAnEnglishName()
        {
            bool before = GalaxyToonText.English;
            try
            {
                GalaxyToonText.English = true;
                foreach (var (title, _) in GalaxyToonGUI.Sections)
                    Assert.AreNotEqual(title, GalaxyToonText.Section(title), "英語の見出し名が無い: " + title);
            }
            finally { GalaxyToonText.English = before; }
        }

        // ================================================================ Inspector の処理

        [Test]
        public void Sections_ContainEveryVisibleProperty()
        {
            var inSections = new HashSet<string>(GalaxyToonGUI.Sections.SelectMany(s => s.props));
            var fixedOnTop = new HashSet<string> { "_RenderMode", "_Cull", "_Cutoff", "_QueueOffset" };
            Shader s = Toon;
            var missing = new List<string>();
            for (int i = 0; i < s.GetPropertyCount(); i++)
            {
                string n = s.GetPropertyName(i);
                if ((s.GetPropertyFlags(i) & ShaderPropertyFlags.HideInInspector) != 0 || fixedOnTop.Contains(n)) continue;
                if (!inSections.Contains(n)) missing.Add(n);
            }
            CollectionAssert.IsEmpty(missing, "どの見出しにも無い(「その他」に出る)項目");
        }

        private static IEnumerable<string> PresetNames()
        {
            foreach (FieldInfo f in typeof(GalaxyToonGUI).GetFields(BindingFlags.NonPublic | BindingFlags.Static))
            {
                if (!f.FieldType.IsArray || f.FieldType.GetElementType()?.Name != "Preset") continue;
                foreach (object p in (Array)f.GetValue(null)) yield return (string)p.GetType().GetField("Name").GetValue(p);
            }
        }

        [Test]
        public void Presets_AllApplyAndUseExistingProperties()
        {
            var names = PresetNames().Distinct().ToList();
            Assert.Greater(names.Count, 40);
            foreach (string n in names)
            {
                Material m = NewToon();
                Assert.IsTrue(GalaxyToonGUI.ApplyPreset(m, n), n);
            }
            // プリセットが書く項目は、すべてシェーダーにある(打ち間違い・消した項目が残っていない)
            foreach (FieldInfo f in typeof(GalaxyToonGUI).GetFields(BindingFlags.NonPublic | BindingFlags.Static))
            {
                if (!f.FieldType.IsArray || f.FieldType.GetElementType()?.Name != "Preset") continue;
                foreach (object p in (Array)f.GetValue(null))
                {
                    string name = (string)p.GetType().GetField("Name").GetValue(p);
                    foreach (var (prop, _) in ((string, float)[])p.GetType().GetField("Floats").GetValue(p) ?? new (string, float)[0])
                        Assert.GreaterOrEqual(Toon.FindPropertyIndex(prop), 0, $"{name}: {prop}");
                    foreach (var (prop, _) in ((string, Color)[])p.GetType().GetField("Colors").GetValue(p) ?? new (string, Color)[0])
                        Assert.GreaterOrEqual(Toon.FindPropertyIndex(prop), 0, $"{name}: {prop}");
                }
            }
            Assert.IsFalse(GalaxyToonGUI.ApplyPreset(NewToon(), "無いプリセット"));
        }

        [Test]
        public void Apply_TurnsFeaturesOnlyWhenTheyHaveAnEffect()
        {
            Material m = NewToon();
            GalaxyToonGUI.Apply(m);
            foreach (string k in new[] { "_SPECULAR", "_RIMLIGHT", "_MATCAP", "_GLOSSY", "_EMISSION", "_SSS", "_SHEER", "_NET_DENT", "_EYE", "_DISSOLVE" })
                Assert.IsFalse(m.IsKeywordEnabled(k), "既定で ON: " + k);
            m.SetColor("_SpecularColor", Color.white);
            m.SetColor("_RimOppositeColor", Color.white);
            m.SetFloat("_ClearCoat", 1f);
            m.SetFloat("_UseSss", 1f);
            m.SetFloat("_UseSheer", 1f);
            m.SetFloat("_UseNetDent", 1f);
            m.SetTexture("_ShadeMap", Texture2D.whiteTexture);
            m.SetColor("_Aniso2Color", Color.white);
            GalaxyToonGUI.Apply(m);
            foreach (string k in new[] { "_SPECULAR", "_RIMLIGHT", "_GLOSSY", "_SSS", "_SHEER", "_NET_DENT", "_ANISOTROPY" })
                Assert.IsTrue(m.IsKeywordEnabled(k), "ON にならない: " + k);
            Assert.AreEqual(1f, m.GetFloat("_ShadeMapOn"));
            m.SetFloat("_OutlineWidth", 0f);
            GalaxyToonGUI.Apply(m);
            Assert.IsFalse(m.GetShaderPassEnabled("SRPDefaultUnlit"), "太さ 0 の輪郭線は描かない");
            m.SetFloat("_StencilComp", (float)CompareFunction.NotEqual);
            m.SetFloat("_QueueOffset", 2f);
            GalaxyToonGUI.Apply(m);
            Assert.AreEqual((int)RenderQueue.Geometry + 3, m.renderQueue, "透かす側は 1 つ後ろ + ずらし");
        }

        [Test]
        public void SectionInUse_FollowsTheFeature()
        {
            Material m = NewToon();
            GalaxyToonGUI.Apply(m);
            foreach (var (title, _) in GalaxyToonGUI.Sections) GalaxyToonGUI.SectionInUse(m, title); // どの見出しでも例外を出さない
            Assert.IsFalse(GalaxyToonGUI.SectionInUse(m, "ふくらむ・脚に押される布"));
            Assert.IsFalse(GalaxyToonGUI.SectionInUse(m, "触れるとへこむ"));
            m.SetFloat("_Inflate", 1f);
            m.SetFloat("_SoftTouch", 1f);
            m.SetFloat("_HairSway", 1f);
            m.SetColor("_TipColor", Color.white);
            m.SetFloat("_StencilComp", (float)CompareFunction.Equal);
            m.SetColor("_Shade2Tint", Color.gray);
            m.SetTexture("_BaseMap", Texture2D.whiteTexture);
            GalaxyToonGUI.Apply(m);
            foreach (string t in new[] { "ふくらむ・脚に押される布", "触れるとへこむ", "髪の揺れ", "毛先のグラデーション", "ステンシル (瞳を髪に透かす など)", "影", "基本(色・立体感)", "輪郭線" })
                Assert.IsTrue(GalaxyToonGUI.SectionInUse(m, t), t);
            Assert.IsFalse(GalaxyToonGUI.SectionInUse(m, "無い見出し"));
            Assert.IsTrue(GalaxyToonGUI.SectionInUse(m, "レイヤー1") == m.IsKeywordEnabled("_LAYER1"));
        }

        [Test]
        public void IsInUse_ComparesWithTheShaderDefault()
        {
            Material m = NewToon();
            m.SetFloat("_Shade1Edge", 0.9f);
            m.SetTexture("_BumpMap", Texture2D.normalTexture);
            var props = MaterialEditor.GetMaterialProperties(new Object[] { m });
            MaterialProperty P(string n) => props.First(p => p.name == n);
            Assert.IsTrue(GalaxyToonGUI.IsInUse(Toon, P("_Shade1Edge")));
            Assert.IsTrue(GalaxyToonGUI.IsInUse(Toon, P("_BumpMap")));
            Assert.IsFalse(GalaxyToonGUI.IsInUse(Toon, P("_Shade1Soft")));
            Assert.IsFalse(GalaxyToonGUI.IsInUse(Toon, P("_Shade1Tint")));
            Assert.IsFalse(GalaxyToonGUI.IsInUse(Toon, P("_EyeRect1")));
            Assert.IsFalse(GalaxyToonGUI.IsInUse(Toon, P("_OcclusionMap")));
        }

        [Test]
        public void TextureOff_StashesAndRestoresTheTexture()
        {
            Material m = NewToon();
            Texture2D t = Keep(Solid(2, 2, Color.red));
            m.SetTexture("_BaseMap", t);
            m.SetTexture("_BumpMap", Texture2D.normalTexture);
            GalaxyToonGUI.SetTextureOn(m, "_BaseMap", false);
            Assert.IsNull(m.GetTexture("_BaseMap"));
            Assert.AreEqual(t, m.GetTexture("_BaseMap_Stash"));
            GalaxyToonGUI.SetTextureOn(m, "_BaseMap", true);
            Assert.AreEqual(t, m.GetTexture("_BaseMap"));
            Assert.IsNull(m.GetTexture("_BaseMap_Stash"));
            GalaxyToonGUI.SetAllTextures(m, false);
            Assert.IsNull(m.GetTexture("_BumpMap"));
            Assert.IsFalse(m.IsKeywordEnabled("_NORMALMAP"), "外したノーマルマップの計算はしない");
            GalaxyToonGUI.SetAllTextures(m, true);
            Assert.AreEqual(Texture2D.normalTexture, m.GetTexture("_BumpMap"));
            GalaxyToonGUI.SetTextureOn(m, "_NoSuchTexture", false); // 退避先の無い名前は何もしない
        }

        [Test]
        public void Reset_RestoresShaderDefaults()
        {
            Material m = NewToon();
            Texture2D t = Keep(Solid(2, 2, Color.red));
            m.SetTexture("_BaseMap", t);
            m.SetFloat("_RenderMode", 2f);
            m.SetFloat("_Shade1Edge", 0.9f);
            m.SetColor("_RimColor", Color.red);
            m.SetVector("_EyeRect1", Vector4.one);
            GalaxyToonGUI.ResetProperties(m, new[] { "_Shade1Edge" });
            Assert.AreEqual(Toon.GetPropertyDefaultFloatValue(Toon.FindPropertyIndex("_Shade1Edge")), m.GetFloat("_Shade1Edge"));
            Assert.AreEqual(Color.red, m.GetColor("_RimColor"), "名前に無い項目は変えない");
            GalaxyToonGUI.ResetExceptBase(m);
            Assert.AreEqual(t, m.GetTexture("_BaseMap"), "基本は残す");
            Assert.AreEqual(2f, m.GetFloat("_RenderMode"), "描画モードは残す");
            Assert.AreNotEqual(Color.red, m.GetColor("_RimColor"));
            Assert.AreEqual(Vector4.zero, m.GetVector("_EyeRect1"));
            GalaxyToonGUI.ResetAll(m);
            Assert.IsNull(m.GetTexture("_BaseMap"));
            Assert.AreEqual(0f, m.GetFloat("_RenderMode"));
        }

        // ================================================================ ノーマルマップ

        private static Color[] ReadBack(string path)
        {
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.isReadable = true;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path).GetPixels();
        }

        [Test]
        public void NormalMap_FlatTextureGivesUpNormalsAndKeepsAspect()
        {
            Texture2D src = Keep(Solid(64, 16, Color.gray));
            string path = GalaxyToonNormal.Bake(src, Temp + "/Flat_Normal.png", 1f, 1, 8, false, 32);
            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            Assert.AreEqual(32, tex.width);
            Assert.AreEqual(8, tex.height, "縦横を同じ比率で縮める");
            Assert.AreEqual(TextureImporterType.NormalMap, ((TextureImporter)AssetImporter.GetAtPath(path)).textureType);
        }

        [Test]
        public void NormalMap_TinyTextureWithLargeBlurDoesNotThrow()
        {
            var src = Keep(new Texture2D(3, 2, TextureFormat.RGBA32, false));
            src.SetPixels(new[] { Color.white, Color.black, Color.white, Color.black, Color.white, Color.black });
            src.Apply();
            Assert.DoesNotThrow(() => GalaxyToonNormal.Bake(src, Temp + "/Tiny_Normal.png", 2f, 5, 9, true, 2048));
        }

        [Test]
        public void NormalMap_SlopeTiltsTheNormal()
        {
            var src = Keep(new Texture2D(16, 16, TextureFormat.RGBA32, false));
            for (int y = 0; y < 16; y++) for (int x = 0; x < 16; x++) src.SetPixel(x, y, Color.white * (Mathf.Sin(x / 16f * Mathf.PI * 2f) * 0.5f + 0.5f));
            src.Apply();
            string path = GalaxyToonNormal.Bake(src, Temp + "/Wave_Normal.png", 4f, 0, 0, false, 2048);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Default; // ノーマルマップの圧縮をやめて、色のまま読む
            importer.sRGBTexture = false;
            importer.SaveAndReimport();
            Color[] px = ReadBack(path);
            Assert.Greater(px.Max(c => c.r) - px.Min(c => c.r), 0.2f, "傾きで法線の X が変わる");
        }

        // ================================================================ SDF 影マップ・厚みマップ

        [Test]
        public void Sdf_SphereGivesThresholdsAndCoversTheTexture()
        {
            Mesh mesh = Keep(Sphere());
            foreach (bool sphere in new[] { false, true })
            {
                Color[] px = GalaxyToonSdf.Bake(mesh, 0, 32, Vector3.back, Vector3.left, 0.6f, 0.7f, sphere, sphere ? 0f : 1f);
                Assert.AreEqual(32 * 32, px.Length);
                Assert.That(px.All(c => c.r >= 0f && c.r <= 1f && c.g >= 0f && c.g <= 1f));
                Assert.Greater(px.Max(c => c.r) - px.Min(c => c.r), 0.5f, "光の向きで影に入る角度が変わる");
            }
        }

        [Test]
        public void Sdf_And_Thickness_RejectUnusableMeshesWithAMessage()
        {
            var noUv = Keep(new Mesh { vertices = new[] { Vector3.zero, Vector3.right, Vector3.up }, triangles = new[] { 0, 1, 2 } });
            var ex = Assert.Throws<ArgumentException>(() => GalaxyToonSdf.Bake(noUv, 0, 8, Vector3.forward, Vector3.right, 0.5f, 0f, false));
            StringAssert.Contains("UV", ex.Message);
            Assert.Throws<ArgumentException>(() => GalaxyToonThickness.Bake(noUv, 0, 8, 0.05f, 0.1f, 0.5f));
            Assert.Throws<ArgumentException>(() => GalaxyToonSdf.Bake(noUv, 3, 8, Vector3.forward, Vector3.right, 0.5f, 0f, false), "無いサブメッシュ");
            var empty = Keep(new Mesh { vertices = new[] { Vector3.zero }, uv = new[] { Vector2.zero } });
            empty.subMeshCount = 1;
            Assert.Throws<ArgumentException>(() => GalaxyToonThickness.Bake(empty, 0, 8, 0.05f, 0.1f, 0.5f), "三角形の無いサブメッシュ");
            Assert.Throws<ArgumentException>(() => GalaxyToonSdf.Bake(null, 0, 8, Vector3.forward, Vector3.right, 0.5f, 0f, false));
        }

        [Test]
        public void Thickness_SphereIsThinAtNoPlaceAndKeepsTheMeshUntouched()
        {
            Mesh mesh = Keep(Sphere());
            var noNormals = Keep(new Mesh { vertices = mesh.vertices, uv = mesh.uv, triangles = mesh.triangles });
            Color[] px = GalaxyToonThickness.Bake(noNormals, 0, 16, 0.2f, 0.1f, 0.5f);
            Assert.AreEqual(16 * 16, px.Length);
            Assert.That(px.All(c => c.r >= 0f && c.r <= 1f));
            Assert.AreEqual(0, noNormals.normals.Length, "法線の無い元のメッシュを書き換えない");
            Color[] thin = GalaxyToonThickness.Bake(mesh, 0, 16, 5f, 0.1f, 0f); // 球の直径より長く見ると、どこも「薄い」
            Assert.Greater(thin.Average(c => c.r), px.Average(c => c.r));
        }

        // ================================================================ 輪郭線用のなめらか法線

        [Test]
        public void OutlineNormals_AverageAcrossHardEdges()
        {
            var go = Keep(GameObject.CreatePrimitive(PrimitiveType.Cube));
            Mesh baked = Keep(GalaxyToonOutlineNormalBaker.BakeMesh(go.GetComponent<MeshFilter>().sharedMesh));
            var uv = new List<Vector3>();
            baked.GetUVs(7, uv);
            Assert.AreEqual(baked.vertexCount, uv.Count);
            // 同じ角の頂点(面ごとに分かれている)は、物体の空間で同じ向きのなめらか法線になる
            Vector3[] v = baked.vertices, n = baked.normals; Vector4[] t = baked.tangents;
            Vector3 World(int i) { Vector3 tan = t[i]; Vector3 bit = Vector3.Cross(n[i], tan) * Mathf.Sign(t[i].w); return (tan * uv[i].x + bit * uv[i].y + n[i] * uv[i].z).normalized; }
            for (int i = 0; i < v.Length; i++)
                for (int j = i + 1; j < v.Length; j++)
                    if ((v[i] - v[j]).sqrMagnitude < 1e-8f) Assert.Less(Vector3.Angle(World(i), World(j)), 1f);
            Assert.Less(Vector3.Angle(World(0), v[0].normalized), 1f, "立方体の角では、角の向き");
        }

        [Test]
        public void OutlineNormals_BakeReplacesOnlyToonMeshes()
        {
            var root = Keep(new GameObject("Root"));
            var toon = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            toon.transform.SetParent(root.transform);
            Material toonAsset = new Material(Toon);
            AssetDatabase.CreateAsset(toonAsset, Temp + "/Toon.mat"); // プレハブにも入るよう、保存したマテリアル
            toon.GetComponent<Renderer>().sharedMaterial = toonAsset;
            var other = GameObject.CreatePrimitive(PrimitiveType.Cube);
            other.transform.SetParent(root.transform);
            Mesh otherMesh = other.GetComponent<MeshFilter>().sharedMesh;
            var temp = new GameObject("Temp", typeof(MeshFilter), typeof(MeshRenderer));
            temp.transform.SetParent(root.transform);
            temp.GetComponent<MeshFilter>().sharedMesh = Keep(new Mesh { hideFlags = HideFlags.DontSave });
            temp.GetComponent<Renderer>().sharedMaterial = NewToon();
            Assert.AreEqual(1, GalaxyToonOutlineNormalBaker.Bake(root, Temp + "/OutlineNormals"));
            Assert.That(AssetDatabase.GetAssetPath(toon.GetComponent<MeshFilter>().sharedMesh), Does.StartWith(Temp + "/OutlineNormals/"));
            Assert.AreEqual(otherMesh, other.GetComponent<MeshFilter>().sharedMesh, "GalaxyToon でない物は変えない");
            Assert.AreEqual(1, GalaxyToonOutlineNormalBaker.Bake(root, Temp + "/OutlineNormals"), "焼いた物の焼き直し");
            // プレハブアセットも焼ける
            Object.DestroyImmediate(temp);
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, Temp + "/Root.prefab");
            Assert.AreEqual(1, GalaxyToonOutlineNormalBaker.Bake(prefab));
            // 別の物の、同じ名前のメッシュで上書きし合わない。名前の無いメッシュも焼ける
            var a = new Mesh { name = "Body" }; var b = new Mesh { name = "Body" }; var noName = new Mesh();
            foreach (Mesh src in new[] { a, b, noName })
            {
                src.vertices = new[] { Vector3.zero, Vector3.right, Vector3.up }; src.uv = new Vector2[3]; src.triangles = new[] { 0, 1, 2 };
                src.RecalculateNormals();
                var holder = Keep(new GameObject("Holder", typeof(MeshFilter), typeof(MeshRenderer)));
                holder.GetComponent<MeshFilter>().sharedMesh = Keep(src);
                holder.GetComponent<Renderer>().sharedMaterial = toonAsset;
                Assert.AreEqual(1, GalaxyToonOutlineNormalBaker.Bake(holder, Temp + "/Same"));
            }
            Assert.AreEqual(3, AssetDatabase.FindAssets("t:Mesh", new[] { Temp + "/Same" }).Length);
        }

        // ================================================================ ランプ

        [Test]
        public void Ramps_TemplatesAreCreatedWithFourRows()
        {
            var (shadow, spec) = GalaxyToonRampTemplate.Create(Temp + "/Ramps");
            foreach (string p in new[] { shadow, spec })
            {
                var t = AssetDatabase.LoadAssetAtPath<Texture2D>(p);
                Assert.IsNotNull(t, p);
                Assert.AreEqual(GalaxyToonRampTemplate.Width, t.width);
                Assert.AreEqual(GalaxyToonRampTemplate.Rows * GalaxyToonRampTemplate.RowHeight, t.height);
                Assert.AreEqual(TextureWrapMode.Clamp, t.wrapMode);
            }
            var (again, _) = GalaxyToonRampTemplate.Create(Temp + "/Ramps");
            Assert.AreNotEqual(shadow, again, "上書きしないときは別の名前");
            var (same, _) = GalaxyToonRampTemplate.Create(Temp + "/Ramps", true);
            Assert.AreEqual(shadow, same);
            GalaxyToonRampTemplate.WriteRows(Temp + "/Ramps/Two.png", 2, (row, x) => row == 0 ? Color.red : new Color(x, x, x));
            Color[] px = ReadBack(Temp + "/Ramps/Two.png");
            Assert.AreEqual(Color.red, px[px.Length - 1], "画像のいちばん上が段 0");
        }

        [Test]
        public void Windows_OpenAndClose()
        {
            foreach (Type t in new[] { typeof(GalaxyToonRampEditorWindow), typeof(GalaxyToonSdfBaker), typeof(GalaxyToonThicknessBaker), typeof(GalaxyToonNormalBaker), typeof(GalaxyToonMigrator) })
            {
                var w = EditorWindow.GetWindow(t);
                Assert.IsNotNull(w, t.Name);
                w.Close();
            }
        }

        // ================================================================ 瞳・頬

        /// <summary>顔(肌)の格子と、左右の白目・瞳の小さな四角を持つ SkinnedMeshRenderer。前は +Z。</summary>
        private SkinnedMeshRenderer Face(int extraMaterials = 0)
        {
            var verts = new List<Vector3>(); var uvs = new List<Vector2>(); var normals = new List<Vector3>();
            var skin = new List<int>(); var white = new List<int>();
            void Quad(List<int> tris, Vector3 c, float w, float h, Vector2 uvMin, Vector2 uvMax, int div = 1)
            {
                int start = verts.Count;
                for (int y = 0; y <= div; y++)
                    for (int x = 0; x <= div; x++)
                    {
                        float fx = x / (float)div, fy = y / (float)div;
                        verts.Add(c + new Vector3((fx - 0.5f) * w, (fy - 0.5f) * h, 0f));
                        uvs.Add(Vector2.Lerp(uvMin, uvMax, 0f) + new Vector2((uvMax.x - uvMin.x) * fx, (uvMax.y - uvMin.y) * fy));
                        normals.Add(Vector3.forward);
                    }
                for (int y = 0; y < div; y++)
                    for (int x = 0; x < div; x++)
                    {
                        int a = start + y * (div + 1) + x, b = a + 1, c2 = a + div + 1, d = c2 + 1;
                        tris.AddRange(new[] { a, c2, b, b, c2, d });
                    }
            }
            Quad(skin, new Vector3(0f, 0f, 0f), 0.16f, 0.2f, new Vector2(0f, 0f), new Vector2(0.5f, 0.5f), 16); // 顔の肌
            foreach (float side in new[] { -1f, 1f })
            {
                Quad(white, new Vector3(side * 0.03f, 0.03f, 0.002f), 0.024f, 0.016f, new Vector2(0.6f, 0.6f), new Vector2(0.7f, 0.7f)); // 白目
                Quad(skin, new Vector3(side * 0.03f, 0.03f, 0.004f), 0.012f, 0.012f, side < 0 ? new Vector2(0.80f, 0.1f) : new Vector2(0.90f, 0.1f), side < 0 ? new Vector2(0.85f, 0.15f) : new Vector2(0.95f, 0.15f)); // 瞳
            }
            var mesh = Keep(new Mesh());
            mesh.SetVertices(verts); mesh.SetUVs(0, uvs); mesh.SetNormals(normals);
            mesh.subMeshCount = 2;
            mesh.SetTriangles(skin, 0); mesh.SetTriangles(white, 1);
            var go = Keep(new GameObject("Character"));
            var smr = new GameObject("Face").AddComponent<SkinnedMeshRenderer>();
            smr.transform.SetParent(go.transform, false);
            smr.sharedMesh = mesh;
            var mats = new List<Material> { NewToon("Face_00_SKIN"), NewToon("EyeWhite_00_EYE") };
            for (int i = 0; i < extraMaterials; i++) mats.Add(NewToon("Extra_" + i));
            smr.sharedMaterials = mats.ToArray();
            return smr;
        }

        [Test]
        public void EyeFinder_FindsBothIrisesAndCheeks()
        {
            SkinnedMeshRenderer smr = Face();
            Assert.IsTrue(GalaxyToonEyeFinder.TryFind(smr, out Material face, out Vector4 r1, out Vector4 r2));
            Assert.AreEqual("Face_00_SKIN", face.name);
            var rects = new[] { r1, r2 }.OrderBy(r => r.x).ToArray();
            Assert.AreEqual(0.80f, rects[0].x, 1e-3f); Assert.AreEqual(0.85f, rects[0].z, 1e-3f);
            Assert.AreEqual(0.90f, rects[1].x, 1e-3f); Assert.AreEqual(0.95f, rects[1].z, 1e-3f);
            Assert.IsTrue(GalaxyToonEyeFinder.TryFindCheeks(smr, out Material skin, out Vector4 uv, out float size));
            Assert.AreEqual(face, skin);
            Assert.Less(uv.y, 0.5f * 0.5f + 0.25f, "頬は目より下");
            Assert.Greater(size, 0f);
        }

        [Test]
        public void EyeFinder_ToleratesMoreMaterialsThanSubMeshesAndMissingParts()
        {
            SkinnedMeshRenderer smr = Face(extraMaterials: 2);
            Assert.DoesNotThrow(() => GalaxyToonEyeFinder.TryFind(smr, out _, out _, out _));
            Assert.DoesNotThrow(() => GalaxyToonEyeFinder.TryFindCheeks(smr, out _, out _, out _));
            smr.sharedMaterials = new[] { NewToon("Body"), NewToon("Cloth") }; // 白目が無い
            Assert.IsFalse(GalaxyToonEyeFinder.TryFind(smr, out _, out _, out _));
            Assert.IsFalse(GalaxyToonEyeFinder.TryFindCheeks(smr, out _, out _, out _));
            smr.sharedMesh = null;
            Assert.IsFalse(GalaxyToonEyeFinder.TryFind(smr, out _, out _, out _));
        }

        // ================================================================ 移行

        private Material Source(string shader, string name)
        {
            Shader s = Shader.Find(shader);
            Assert.IsNotNull(s, shader);
            return Keep(new Material(s) { name = name });
        }

        [Test]
        public void Migrator_DetectsShadersAndGuessesParts()
        {
            Assert.AreEqual(GalaxyToonMigrator.SourceKind.MToon, GalaxyToonMigrator.Detect(Source("Hidden/GalaxyToonTests/VRM/MToon", "a")));
            Assert.AreEqual(GalaxyToonMigrator.SourceKind.MToon10, GalaxyToonMigrator.Detect(Source("Hidden/GalaxyToonTests/VRM10/MToon10", "a")));
            Assert.AreEqual(GalaxyToonMigrator.SourceKind.LilToon, GalaxyToonMigrator.Detect(Source("Hidden/GalaxyToonTests/lilToonOutline", "a")));
            Assert.AreEqual(GalaxyToonMigrator.SourceKind.Uts, GalaxyToonMigrator.Detect(Source("Hidden/GalaxyToonTests/UniversalToon", "a")));
            Assert.AreEqual(GalaxyToonMigrator.SourceKind.UrpLit, GalaxyToonMigrator.Detect(Source("Universal Render Pipeline/Lit", "a")));
            Assert.AreEqual(GalaxyToonMigrator.SourceKind.UrpUnlit, GalaxyToonMigrator.Detect(Source("Universal Render Pipeline/Unlit", "a")));
            Assert.AreEqual(GalaxyToonMigrator.SourceKind.GalaxyToon, GalaxyToonMigrator.Detect(NewToon()));
            Assert.AreEqual(GalaxyToonMigrator.SourceKind.Generic, GalaxyToonMigrator.Detect(Source("Sprites/Default", "a")));
            var parts = new Dictionary<string, int>
            {
                { "N00_000_00_Face_00_SKIN (Instance)", 2 }, { "Body_00_SKIN", 1 }, { "HairBack_00_HAIR", 3 }, { "Tops_01_CLOTH", 4 },
                { "Metal_Buckle", 5 }, { "EyeWhite_00_EYE", 6 }, { "FaceBrow_00_FACE", 6 }, { "Something", 0 }, { "髪", 3 },
            };
            foreach (var kv in parts) Assert.AreEqual(kv.Value, GalaxyToonMigrator.GuessPart(kv.Key), kv.Key);
        }

        [Test]
        public void Migrator_ConvertsEachShaderKind()
        {
            var notes = new List<string>();
            // MToon 0.x: 影色は地の色で割った掛け算の色、輪郭線は cm のまま
            Material mt = Source("Hidden/GalaxyToonTests/VRM/MToon", "Hair_00_HAIR");
            mt.SetColor("_Color", new Color(0.8f, 0.8f, 0.8f));
            mt.SetColor("_ShadeColor", new Color(0.4f, 0.4f, 0.4f));
            mt.SetFloat("_OutlineWidthMode", 2f); mt.SetFloat("_OutlineWidth", 0.3f);
            mt.SetFloat("_BlendMode", 1f); mt.SetColor("_RimColor", Color.red);
            mt.SetTexture("_ShadeTexture", Texture2D.blackTexture); mt.SetTexture("_MainTex", Texture2D.whiteTexture);
            Material d = Keep(GalaxyToonMigrator.Convert(mt, GalaxyToonMigrator.SourceKind.MToon, "髪", true, notes));
            Assert.AreEqual(Toon, d.shader);
            Assert.AreEqual(0.5f, d.GetColor("_Shade1Tint").r, 1e-3f);
            Assert.AreEqual(0.3f, d.GetFloat("_OutlineWidth"), 1e-4f);
            Assert.AreEqual(1f, d.GetFloat("_AnimeOutlinePixel"), "画面の太さ");
            Assert.AreEqual(1f, d.GetFloat("_RenderMode"), "切り抜き");
            Assert.AreEqual(1f, d.GetFloat("_HairShadowCast"), "髪は髪の影を落とす");
            Assert.That(notes.Count >= 2, "写せない物のメモ");
            // MToon 1.0: 輪郭線は m → cm、両面、描画順のずらし
            Material m10 = Source("Hidden/GalaxyToonTests/VRM10/MToon10", "Face_00_SKIN (Instance)");
            m10.SetFloat("_OutlineWidthMode", 1f); m10.SetFloat("_OutlineWidth", 0.002f); m10.SetFloat("_AlphaMode", 2f);
            m10.SetFloat("_DoubleSided", 1f); m10.SetFloat("_RenderQueueOffset", -6f);
            m10.SetTexture("_MatcapTex", Keep(Solid(64, 64, Color.white)));
            d = Keep(GalaxyToonMigrator.Convert(m10, GalaxyToonMigrator.SourceKind.MToon10, "顔", true, notes));
            Assert.AreEqual("Face_00_SKIN", d.name, "(Instance) を外す");
            Assert.AreEqual(0.2f, d.GetFloat("_OutlineWidth"), 1e-4f);
            Assert.AreEqual(2f, d.GetFloat("_RenderMode"));
            Assert.AreEqual(0f, d.GetFloat("_Cull"));
            Assert.AreEqual(-6f, d.GetFloat("_QueueOffset"), "描画順のずらしを写す");
            Assert.IsNotNull(d.GetTexture("_MatCapTex"));
            Assert.Greater(d.GetFloat("_HairShadowReceive"), 0f, "顔は髪の影を受ける");
            // 小さなダミーのマットキャップは写さない
            m10.SetTexture("_MatcapTex", Keep(Solid(8, 8, Color.white)));
            Assert.IsNull(Keep(GalaxyToonMigrator.Convert(m10, GalaxyToonMigrator.SourceKind.MToon10, "", false, notes)).GetTexture("_MatCapTex"));
            // lilToon: 同じ意味の項目を GalaxyToon の名前へ
            Material lil = Source("Hidden/GalaxyToonTests/lilToonOutline", "Tops_01_CLOTH");
            lil.SetFloat("_UseShadow", 1f); lil.SetFloat("_ShadowBorder", 0.4f); lil.SetFloat("_UseRim", 1f); lil.SetColor("_RimColor", Color.green);
            lil.SetFloat("_UseEmission", 1f); lil.SetColor("_EmissionColor", Color.blue); lil.SetFloat("_TransparentMode", 4f);
            d = Keep(GalaxyToonMigrator.Convert(lil, GalaxyToonMigrator.SourceKind.LilToon, "服", false, notes));
            Assert.AreEqual(0.4f, d.GetFloat("_Shade1Edge"), 1e-4f);
            Assert.AreEqual(Color.green, d.GetColor("_RimColor"));
            Assert.AreEqual(0.08f, d.GetFloat("_OutlineWidth"), 1e-4f, "Outline のシェーダーは輪郭線を写す");
            Assert.AreEqual(2f, d.GetFloat("_RenderMode"));
            lil.SetFloat("_UseShadow", 0f);
            Assert.AreEqual(0f, Keep(GalaxyToonMigrator.Convert(lil, GalaxyToonMigrator.SourceKind.LilToon, "", false, notes)).GetColor("_Shade1Tint").a, "影を使わない");
            // UTS: 1 影・2 影、輪郭線は 0.001m → cm
            Material uts = Source("Hidden/GalaxyToonTests/UniversalToon", "Body_SKIN");
            uts.SetFloat("_Outline_Width", 2f); uts.SetFloat("_RimLight", 1f); uts.SetFloat("_ClippingMode", 1f);
            uts.SetTexture("_1st_ShadeMap", Texture2D.blackTexture); uts.SetTexture("_MainTex", Texture2D.whiteTexture);
            d = Keep(GalaxyToonMigrator.Convert(uts, GalaxyToonMigrator.SourceKind.Uts, "肌", false, notes));
            Assert.AreEqual(0.2f, d.GetFloat("_OutlineWidth"), 1e-4f);
            Assert.AreEqual(1f, d.GetFloat("_RenderMode"));
            Assert.AreEqual(1f, d.GetColor("_Shade2Tint").a);
            // URP Lit / Unlit / その他
            Material lit = Source("Universal Render Pipeline/Lit", "Crate");
            lit.SetFloat("_Smoothness", 0.7f); lit.SetFloat("_Surface", 1f); lit.EnableKeyword("_EMISSION"); lit.SetColor("_EmissionColor", Color.red);
            d = Keep(GalaxyToonMigrator.Convert(lit, GalaxyToonMigrator.SourceKind.UrpLit, "", false, notes));
            Assert.AreEqual(0.7f, d.GetFloat("_Smoothness"), 1e-4f, "プリセットなしは、つやを写す");
            Assert.AreEqual(2f, d.GetFloat("_RenderMode"));
            Assert.AreEqual(Color.red, d.GetColor("_EmissionColor"));
            d = Keep(GalaxyToonMigrator.Convert(lit, GalaxyToonMigrator.SourceKind.UrpLit, "肌", false, notes));
            Assert.AreEqual(0f, d.GetFloat("_Smoothness"), "キャラのプリセットのときは、URP Lit の既定のつやを写さない");
            d = Keep(GalaxyToonMigrator.Convert(Source("Universal Render Pipeline/Unlit", "Sign"), GalaxyToonMigrator.SourceKind.UrpUnlit, "", false, notes));
            Assert.AreEqual(1f, d.GetFloat("_LightFloor"));
            var standard = Shader.Find("Standard");
            if (standard != null)
            {
                Material st = Keep(new Material(standard) { name = "Std" });
                st.SetFloat("_Mode", 1f);
                Assert.AreEqual(1f, Keep(GalaxyToonMigrator.Convert(st, GalaxyToonMigrator.SourceKind.Standard, "", false, notes)).GetFloat("_RenderMode"));
            }
            notes.Clear();
            Material sprite = Source("Sprites/Default", "Sprite");
            sprite.renderQueue = 3000;
            d = Keep(GalaxyToonMigrator.Convert(sprite, GalaxyToonMigrator.SourceKind.Generic, "", false, notes));
            Assert.AreEqual(2f, d.GetFloat("_RenderMode"));
            Assert.AreEqual(1, notes.Count);
        }

        private static object Field(object o, string name) => o.GetType().GetField(name, BindingFlags.NonPublic | BindingFlags.Instance).GetValue(o);
        private static void SetField(object o, string name, object v) => o.GetType().GetField(name, BindingFlags.NonPublic | BindingFlags.Instance).SetValue(o, v);
        private static void Call(object o, string name) => o.GetType().GetMethod(name, BindingFlags.NonPublic | BindingFlags.Instance).Invoke(o, null);

        [Test]
        public void Migrator_RunMigratesACharacterAndCanBeUndone()
        {
            SkinnedMeshRenderer smr = Face();
            Material faceSrc = Source("Hidden/GalaxyToonTests/VRM10/MToon10", "Face_00_SKIN (Instance)");
            Material whiteSrc = Source("Hidden/GalaxyToonTests/VRM10/MToon10", "EyeWhite_00_EYE (Instance)");
            smr.sharedMaterials = new[] { faceSrc, whiteSrc };
            GameObject root = smr.transform.root.gameObject;
            root.AddComponent<Animator>();
            Selection.activeGameObject = root;
            var w = EditorWindow.GetWindow<GalaxyToonMigrator>();
            try
            {
                Call(w, "Collect");
                var entries = (System.Collections.IList)Field(w, "entries");
                Assert.AreEqual(2, entries.Count);
                Assert.AreEqual(1, ((System.Collections.IList)Field(w, "roots")).Count);
                SetField(w, "folder", Temp + "/Migrated");
                SetField(w, "hairShadow", false);
                Call(w, "Run");
                StringAssert.Contains("2", (string)Field(w, "message"));
                Assert.AreEqual(Toon, smr.sharedMaterials[0].shader);
                Assert.AreEqual(Temp + "/Migrated/Face_00_SKIN.mat", AssetDatabase.GetAssetPath(smr.sharedMaterials[0]));
                Assert.IsNotNull(root.GetComponent<GalaxyToonFace>());
                string guid = AssetDatabase.AssetPathToGUID(Temp + "/Migrated/Face_00_SKIN.mat");
                // もう一度: 同じファイルへ(GUID を保つ)。移行済みは一覧で外れる
                smr.sharedMaterials = new[] { faceSrc, whiteSrc };
                Call(w, "Collect");
                SetField(w, "folder", Temp + "/Migrated");
                SetField(w, "hairShadow", false);
                Call(w, "Run");
                Assert.AreEqual(guid, AssetDatabase.AssetPathToGUID(Temp + "/Migrated/Face_00_SKIN.mat"));
                Undo.PerformUndo();
                Assert.AreEqual(faceSrc, smr.sharedMaterials[0], "Undo で元のマテリアルに戻る");
                // 元のマテリアルを書き換えるモード: .mat のアセットならそのもののシェーダーを変える
                Material asset = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = "Editable" };
                AssetDatabase.CreateAsset(asset, Temp + "/Editable.mat");
                smr.sharedMaterials = new[] { asset, whiteSrc };
                Selection.activeGameObject = root; // Undo で選択も戻るので、選び直す
                Call(w, "Collect");
                SetField(w, "overwrite", true);
                SetField(w, "folder", Temp + "/Migrated");
                SetField(w, "addFace", false); SetField(w, "findEyes", false); SetField(w, "bakeOutline", false); SetField(w, "hairShadow", false);
                Call(w, "Run");
                Assert.AreEqual(Toon, AssetDatabase.LoadAssetAtPath<Material>(Temp + "/Editable.mat").shader);
                // 保存先が Assets の外なら止める
                SetField(w, "overwrite", false);
                SetField(w, "folder", "C:/Outside");
                Call(w, "Run");
                StringAssert.Contains("Assets", (string)Field(w, "message"));
            }
            finally { w.Close(); }
        }

        // ================================================================ そのほかのメニュー

        [Test]
        public void Tools_ReportAndShaderOffToggle()
        {
            LogAssert.Expect(LogType.Log, new System.Text.RegularExpressions.Regex(@"\[GalaxyToon\]"));
            GalaxyToonTools.Report();
            bool before = GalaxyToonTools.ShaderOff;
            GalaxyToonTools.ToggleShaderOff();
            Assert.AreNotEqual(before, GalaxyToonTools.ShaderOff);
            GalaxyToonTools.ToggleShaderOff();
            Assert.AreEqual(before, GalaxyToonTools.ShaderOff);
        }

        [Test]
        public void SeeThroughBangs_NeedsAHumanoidCharacter()
        {
            Selection.activeGameObject = null;
            Assert.IsFalse((bool)typeof(GalaxyToonSeeThroughBangsMenu).GetMethod("Validate", BindingFlags.NonPublic | BindingFlags.Static).Invoke(null, null));
            var go = Keep(new GameObject("NotHuman"));
            Selection.activeGameObject = go;
            LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex("Humanoid"));
            GalaxyToonSeeThroughBangsMenu.Add();
            Assert.IsNull(go.GetComponent<GalaxyToonSeeThroughBangs>());
        }

        [Test]
        public void PerfSweep_OnlyRunsInPlayMode()
        {
            LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex("再生中"));
            GalaxyToonPerfSweep.Start();
            Assert.IsFalse(GalaxyToonPerfSweep.Running);
        }

        // ---- プロジェクト全体に手を入れるもの(カバレッジを測る一時プロジェクトでだけ流す)

        [Test, Explicit("プロジェクトの全 GalaxyToon マテリアルを整理して保存する")]
        public void Tools_ReapplyAllMaterials()
        {
            LogAssert.ignoreFailingMessages = true;
            GalaxyToonTools.ReapplyAll();
        }

        [Test, Explicit("使っている URP Renderer に髪の影の機能を足す")]
        public void Tools_EnableHairShadowIsIdempotent()
        {
            LogAssert.ignoreFailingMessages = true;
            GalaxyToonTools.EnableHairShadow();
            GalaxyToonTools.EnableHairShadow();
        }

        [Test, Explicit("使っている URP Renderer の機能を足して、元に戻す"), Category("ProjectWide")]
        public void Tools_AddingAFeatureCanBeUndone()
        {
            var data = ((UnityEngine.Rendering.Universal.UniversalRenderPipelineAsset)GraphicsSettings.currentRenderPipeline).rendererDataList[0];
            // いま入っていれば外しておく(このテストのため)
            var so = new SerializedObject(data);
            SerializedProperty list = so.FindProperty("m_RendererFeatures"), map = so.FindProperty("m_RendererFeatureMap");
            for (int i = list.arraySize - 1; i >= 0; i--)
                if (list.GetArrayElementAtIndex(i).objectReferenceValue is GalaxyPaintFeature || list.GetArrayElementAtIndex(i).objectReferenceValue == null)
                {
                    list.GetArrayElementAtIndex(i).objectReferenceValue = null; // 物の参照は、null にしてから消す(1 回目の削除は null にするだけ)
                    list.DeleteArrayElementAtIndex(i);
                    if (i < map.arraySize) map.DeleteArrayElementAtIndex(i);
                }
            so.ApplyModifiedPropertiesWithoutUndo();
            int before = data.rendererFeatures.Count;

            GalaxyToonTools.AddRendererFeature<GalaxyPaintFeature>("塗り", "");
            Assert.AreEqual(before + 1, data.rendererFeatures.Count);
            Undo.PerformUndo();
            Assert.AreEqual(before, data.rendererFeatures.Count, "元に戻すと一覧からも消える");
            Assert.IsFalse(data.rendererFeatures.Any(f => f == null), "空の欄を残さない(URP がエラーを出す)");
        }

        [Test, Explicit("パッケージの Samples を作り直す")]
        public void SampleScene_IsCreated()
        {
            var setup = UnityEditor.SceneManagement.EditorSceneManager.GetSceneManagerSetup();
            try
            {
                string path = GalaxyToonSampleSceneCreator.Create();
                Assert.IsTrue(File.Exists(path));
                Assert.Greater(Object.FindFirstObjectByType<GalaxyToonSampleViewer>().entries.Count, 90);
            }
            finally { if (setup.Length > 0) UnityEditor.SceneManagement.EditorSceneManager.RestoreSceneManagerSetup(setup); }
        }

        [Test, Explicit("GPU で全項目を描いて比べる(数分かかる)")]
        public void PropertySweep_FindsNoDeadProperties()
        {
            string report = GalaxyToonPropertySweep.Run(-1);
            Debug.Log(report);
            StringAssert.Contains("描画が変わらなかった項目 0", report);
        }

        [Test, Explicit("GPU で全機能を描いて比べる(数分かかる)")]
        public void ParameterTest_AllPass()
        {
            GalaxyToonParameterTest.RunAllTests(out int passed, out int failed, out string report);
            Assert.AreEqual(0, failed, report);
        }

        [Test, Explicit("メニューから全項目を描く(数分かかる)。結果は Console へ"), Category("ProjectWide")]
        public void DeveloperMenus_RunAndReport()
        {
            LogAssert.ignoreFailingMessages = true; // 結果の一覧は警告で出ることがある
            GalaxyToonPropertySweep.RunMenu();
            GalaxyToonParameterTest.RunFullParameterTestMenu();
        }

        [UnityTest, Explicit("再生して、機能を 1 つずつ切って負荷を測る(1 分ほど)"), Category("ProjectWide")]
        public System.Collections.IEnumerator PerfSweep_MeasuresEveryStepAndRestores()
        {
            yield return new EnterPlayMode();
            var urp = (UnityEngine.Rendering.Universal.UniversalRenderPipelineAsset)GraphicsSettings.currentRenderPipeline;
            float scale = urp.renderScale;
            var volume = new GameObject("Volume").AddComponent<Volume>();
            volume.isGlobal = true;
            volume.profile = ScriptableObject.CreateInstance<VolumeProfile>();
            volume.profile.Add<UnityEngine.Rendering.Universal.Bloom>(true);
            volume.profile.Add<UnityEngine.Rendering.Universal.DepthOfField>(true);
            var sun = new GameObject("Sun").AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.shadows = LightShadows.Soft;
            var character = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            new GameObject("Head").transform.SetParent(character.transform, false);
            character.GetComponent<Renderer>().sharedMaterial = new Material(Toon);
            character.AddComponent<GalaxyToonFace>();
            new GameObject("Camera").AddComponent<Camera>();

            GalaxyToonPerfSweep.Start();
            GalaxyToonPerfSweep.Start(); // 測っている間は、もう一度押しても何もしない
            double end = EditorApplication.timeSinceStartup + 180.0;
            while (GalaxyToonPerfSweep.Running && EditorApplication.timeSinceStartup < end) yield return null;
            Assert.IsFalse(GalaxyToonPerfSweep.Running, "全部の段を測り終える");
            StringAssert.Contains("全部あり", GalaxyToonPerfSweep.Result);
            Assert.AreEqual(scale, urp.renderScale, "描画解像度を戻す");
            Assert.AreEqual(LightShadows.Soft, sun.shadows, "ライトの影を戻す");
            Assert.IsTrue(character.activeSelf, "キャラを戻す");

            // 途中で再生を止めても、元に戻して終わる
            GalaxyToonPerfSweep.Start();
            yield return null;
            yield return new ExitPlayMode();
            yield return null;
            Assert.IsFalse(GalaxyToonPerfSweep.Running);
            Assert.AreEqual(scale, urp.renderScale);
        }

        // ================================================================ メニュー(選択中の物に対して)

        private static void Menu(Type type, string method) => type.GetMethod(method, BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Static).Invoke(null, null);

        [Test]
        public void Menus_WorkOnTheSelection()
        {
            // 瞳・頬: 見つかる顔と、見つからない物
            SkinnedMeshRenderer face = Face();
            Selection.objects = new Object[] { face.transform.parent.gameObject };
            Menu(typeof(GalaxyToonEyeFinder), "FindSelected");
            Assert.AreEqual(1f, face.sharedMaterials[0].GetFloat("_UseEye"));
            Menu(typeof(GalaxyToonEyeFinder), "FindCheeksSelected");
            Assert.AreNotEqual(Vector4.zero, face.sharedMaterials[0].GetVector("_BlushUV"));
            Selection.objects = new Object[] { Keep(new GameObject("Nothing")) };
            LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex("瞳"));
            Menu(typeof(GalaxyToonEyeFinder), "FindSelected");
            LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex("頬"));
            Menu(typeof(GalaxyToonEyeFinder), "FindCheeksSelected");

            // ランプのひな形: 選んだフォルダに作る
            Selection.activeObject = AssetDatabase.LoadAssetAtPath<Object>(Temp);
            GalaxyToonRampTemplate.CreateMenu();
            Assert.IsTrue(File.Exists(Temp + "/GalaxyToon_ShadowRamp_4Rows.png") || AssetDatabase.FindAssets("ShadowRamp", new[] { Temp }).Length > 0);

            // 輪郭線のなめらか法線: 選んだプレハブ(Temp の中)
            var root = Keep(new GameObject("Baked"));
            var sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            sphere.transform.SetParent(root.transform);
            Material mat = new Material(Toon);
            AssetDatabase.CreateAsset(mat, Temp + "/Outline.mat");
            sphere.GetComponent<Renderer>().sharedMaterial = mat;
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, Temp + "/Baked.prefab");
            Selection.objects = new Object[] { prefab };
            Menu(typeof(GalaxyToonOutlineNormalBaker), "BakeSelected");
            Assert.IsTrue(AssetDatabase.IsValidFolder(Temp + "/OutlineNormals"), "プレハブの隣に焼く");
        }

        [Test]
        public void ToolWindows_OpenFromTheMenuAndFollowTheSelection()
        {
            var face = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            Keep(face);
            face.GetComponent<Renderer>().sharedMaterial = NewToon("Selected");
            Selection.activeGameObject = face;
            var windows = new List<EditorWindow>();
            try
            {
                GalaxyToonSdfBaker.Open(); windows.Add(EditorWindow.GetWindow<GalaxyToonSdfBaker>());
                GalaxyToonNormalBaker.Open(); windows.Add(EditorWindow.GetWindow<GalaxyToonNormalBaker>());
                GalaxyToonThicknessBaker.Open(); windows.Add(EditorWindow.GetWindow<GalaxyToonThicknessBaker>());
                GalaxyToonRampEditorWindow.Open(); windows.Add(EditorWindow.GetWindow<GalaxyToonRampEditorWindow>());
                GalaxyToonMigrator.Open(); windows.Add(EditorWindow.GetWindow<GalaxyToonMigrator>());
                foreach (EditorWindow w in windows) Call(w, "OnSelectionChange");
                Assert.AreEqual(face.GetComponent<Renderer>(), Field(windows[0], "target"), "選んだ物を対象にする");
                Assert.AreEqual(face.GetComponent<Renderer>(), Field(windows[2], "_target"));
                // ランプ: マテリアルを選んでも対象になる
                Selection.activeObject = face.GetComponent<Renderer>().sharedMaterial;
                Call(windows[3], "OnSelectionChange");
                Assert.AreEqual(face.GetComponent<Renderer>().sharedMaterial, Field(windows[3], "_targetMaterial"));
                SetField(windows[3], "_targetRenderer", null);
                Call(windows[3], "SyncMaterialFromRenderer"); // 対象の Renderer が無いときは何もしない
                windows[3].GetType().GetMethod("InitDefaultGradients", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(windows[3], new object[] { GalaxyToonRampEditorWindow.TargetSlot.SpecularRamp });
            }
            finally { foreach (EditorWindow w in windows) if (w != null) w.Close(); }
        }

        [Test]
        public void Tools_UpgradeMovesValuesSavedUnderOldNames()
        {
            Material m = NewToon("Old");
            Assert.IsFalse(GalaxyToonTools.UpgradeMaterial(m), "今の名前だけなら何もしない");
            // 前の版のマテリアルを作る: 今の名前の欄を、前の名前(と別の値)にする
            var so = new SerializedObject(m);
            SerializedProperty floats = so.FindProperty("m_SavedProperties.m_Floats");
            for (int i = 0; i < floats.arraySize; i++)
            {
                SerializedProperty e = floats.GetArrayElementAtIndex(i);
                if (e.FindPropertyRelative("first").stringValue != "_Shade1Edge") continue;
                e.FindPropertyRelative("first").stringValue = "_ShadowBorder";
                e.FindPropertyRelative("second").floatValue = 0.33f;
            }
            so.ApplyModifiedPropertiesWithoutUndo();
            Assert.IsTrue(GalaxyToonTools.UpgradeMaterial(m));
            Assert.AreEqual(0.33f, m.GetFloat("_Shade1Edge"), 1e-4f, "前の名前の値を、今の名前へ移す");
            foreach (var (from, to) in GalaxyToonTools.RenamedProperties)
            {
                Assert.IsFalse(m.HasProperty(from), from + " は、もうシェーダーに無い");
                Assert.IsTrue(m.HasProperty(to), to + " がシェーダーにある");
            }
        }

        [Test]
        public void Migrator_NamesEveryKind()
        {
            MethodInfo kindName = typeof(GalaxyToonMigrator).GetMethod("KindName", BindingFlags.NonPublic | BindingFlags.Static);
            foreach (object kind in Enum.GetValues(typeof(GalaxyToonMigrator.SourceKind)))
                Assert.IsFalse(string.IsNullOrEmpty((string)kindName.Invoke(null, new[] { kind })), kind.ToString());
        }

        // ================================================================ 前髪透過のメニュー(Humanoid のキャラ)

        /// <summary>Humanoid のキャラ(最低限のボーンと Avatar)。</summary>
        private GameObject Humanoid(string name)
        {
            var root = Keep(new GameObject(name));
            var spec = new (string bone, string parent, Vector3 at)[]
            {
                ("Hips", null, new Vector3(0f, 1f, 0f)), ("Spine", "Hips", new Vector3(0f, 0.15f, 0f)), ("Chest", "Spine", new Vector3(0f, 0.15f, 0f)),
                ("Neck", "Chest", new Vector3(0f, 0.2f, 0f)), ("Head", "Neck", new Vector3(0f, 0.1f, 0f)),
                ("LeftUpperLeg", "Hips", new Vector3(-0.1f, -0.05f, 0f)), ("LeftLowerLeg", "LeftUpperLeg", new Vector3(0f, -0.45f, 0f)), ("LeftFoot", "LeftLowerLeg", new Vector3(0f, -0.4f, 0f)),
                ("RightUpperLeg", "Hips", new Vector3(0.1f, -0.05f, 0f)), ("RightLowerLeg", "RightUpperLeg", new Vector3(0f, -0.45f, 0f)), ("RightFoot", "RightLowerLeg", new Vector3(0f, -0.4f, 0f)),
                ("LeftUpperArm", "Chest", new Vector3(-0.2f, 0.15f, 0f)), ("LeftLowerArm", "LeftUpperArm", new Vector3(-0.25f, 0f, 0f)), ("LeftHand", "LeftLowerArm", new Vector3(-0.25f, 0f, 0f)),
                ("RightUpperArm", "Chest", new Vector3(0.2f, 0.15f, 0f)), ("RightLowerArm", "RightUpperArm", new Vector3(0.25f, 0f, 0f)), ("RightHand", "RightLowerArm", new Vector3(0.25f, 0f, 0f)),
            };
            var bones = new Dictionary<string, Transform>();
            var human = new List<HumanBone>();
            var skeleton = new List<SkeletonBone> { new SkeletonBone { name = root.name, rotation = Quaternion.identity, scale = Vector3.one } };
            foreach (var (bone, parent, at) in spec)
            {
                var t = new GameObject(bone).transform;
                t.SetParent(parent == null ? root.transform : bones[parent], false);
                t.localPosition = at;
                bones[bone] = t;
                human.Add(new HumanBone { boneName = bone, humanName = bone, limit = new HumanLimit { useDefaultValues = true } });
                skeleton.Add(new SkeletonBone { name = bone, position = at, rotation = Quaternion.identity, scale = Vector3.one });
            }
            Avatar avatar = Keep(AvatarBuilder.BuildHumanAvatar(root, new HumanDescription { human = human.ToArray(), skeleton = skeleton.ToArray() }));
            Assert.IsTrue(avatar.isHuman, "Humanoid の Avatar を作れる");
            root.AddComponent<Animator>().avatar = avatar;
            return root;
        }

        [Test]
        public void SeeThroughBangs_AddsToHumanoidsWithHair()
        {
            GameObject noHair = Humanoid("NoHair");
            Selection.activeGameObject = noHair.transform.Find("Hips/Spine").gameObject; // 子を選んでも、キャラのルートに付ける
            LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex("髪マテリアル"));
            GalaxyToonSeeThroughBangsMenu.Add();
            Assert.IsNull(noHair.GetComponent<GalaxyToonSeeThroughBangs>());

            GameObject withHair = Humanoid("WithHair");
            var hair = new GameObject("Hair").AddComponent<SkinnedMeshRenderer>();
            hair.transform.SetParent(withHair.transform, false);
            hair.sharedMaterial = NewToon("Hair_Mat");
            Selection.activeGameObject = withHair;
            GalaxyToonSeeThroughBangsMenu.Add();
            Assert.IsNotNull(withHair.GetComponent<GalaxyToonSeeThroughBangs>());
            GalaxyToonSeeThroughBangsMenu.Add(); // もう付いている
            Assert.AreEqual(1, withHair.GetComponents<GalaxyToonSeeThroughBangs>().Length);
        }
    }
}
