using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using GalaxyToon;
using GalaxyToon.Editor;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace GalaxyToon.Tests
{
    /// <summary>
    /// GalaxyToon（シェーダーのパッケージ）の点検：コンパイル、マテリアルの値の宣言（SRP Batcher）、パスごとのキーワード、
    /// 描画モードの切り替え、いくつかの組み合わせで描けるか、止めた描画機能の値が残らないか。
    /// </summary>
    public class GalaxyToonShaderTests
    {
        // パッケージの場所はシェーダーから決める(Packages/ でも Assets/ の下でも動く)
        private static string PackageRoot => Path.GetDirectoryName(Path.GetDirectoryName(AssetDatabase.GetAssetPath(Shader.Find("GalaxyShader/Toon"))));
        private const string ToonShaderName = "GalaxyShader/Toon";

        private static string ShaderFile(string name) => Path.GetFullPath(Path.Combine(PackageRoot, "Shaders", name));

        private static string Read(string name) => File.ReadAllText(ShaderFile(name));

        private static readonly string[] TextureTypes = { "2D", "3D", "Cube", "2DArray", "CubeArray" };

        // ---- コンパイル

        [TestCase("GalaxyShader/Toon")]
        [TestCase("GalaxyShader/ToonFur")]
        [TestCase("Hidden/GalaxyToon/Screen")]
        public void Shader_CompilesWithoutErrors(string name)
        {
            var shader = Shader.Find(name);
            Assert.IsNotNull(shader, name);
            var errors = ShaderUtil.GetShaderMessages(shader).Where(m => m.severity == UnityEditor.Rendering.ShaderCompilerMessageSeverity.Error).Select(m => m.message + " @" + m.file + ":" + m.line).ToList();
            Assert.IsEmpty(errors, string.Join("\n", errors));
            Assert.IsFalse(ShaderUtil.ShaderHasError(shader), name);
        }

        // ---- マテリアルの値の宣言

        /// <summary>Properties の値（テクスチャ以外）は UnityPerMaterial に、UnityPerMaterial の値は Properties にある（SRP Batcher が効き、値が正しく入る）。</summary>
        [Test]
        public void Properties_MatchUnityPerMaterialBuffer()
        {
            var properties = PropertyTypes(Read("GalaxyToon.shader"));
            var buffer = UnityPerMaterial(Read("GalaxyToonInput.hlsl"));
            var shader = Read("GalaxyToon.shader");
            // 描画の状態（Cull・Blend・Stencil など）とキーワードの切り替えは、シェーダーの中で値として読まないので要らない
            var stateOnly = new HashSet<string>(Regex.Matches(shader, @"\[\s*(_\w+)\s*\]").Cast<Match>().Select(m => m.Groups[1].Value));
            var toggles = new HashSet<string>(Regex.Matches(shader, @"\[(?:Toggle|ToggleOff|KeywordEnum)\(\w+[^\]]*\]\s*(_\w+)").Cast<Match>().Select(m => m.Groups[1].Value));
            var hidden = new HashSet<string> { "_RenderMode", "_AlphaClip", "_QueueOffset", "_SdfFollowHead" };
            var missing = properties.Where(p => !TextureTypes.Contains(p.Value) && !buffer.Contains(p.Key) && !stateOnly.Contains(p.Key) && !toggles.Contains(p.Key) && !hidden.Contains(p.Key)).Select(p => p.Key).ToList();
            Assert.IsEmpty(missing, "Properties にあって UnityPerMaterial に無い値: " + string.Join(", ", missing));

            var extra = buffer.Where(b => !properties.ContainsKey(b) && !b.EndsWith("_ST") && !b.EndsWith("_TexelSize")).ToList();
            Assert.IsEmpty(extra, "UnityPerMaterial にあって Properties に無い値: " + string.Join(", ", extra));
        }

        /// <summary>値はシェーダーのコード（hlsl）で、同じ名前が UnityPerMaterial の外に二重に宣言されていない（グローバルと混ざらない）。</summary>
        [Test]
        public void UnityPerMaterial_NamesAreNotRedeclaredOutside()
        {
            var input = Read("GalaxyToonInput.hlsl");
            var start = input.IndexOf("CBUFFER_START(UnityPerMaterial)");
            var end = input.IndexOf("CBUFFER_END", start);
            var outside = input.Substring(0, start) + input.Substring(end);
            var buffer = UnityPerMaterial(input);
            var redeclared = buffer.Where(name => Regex.IsMatch(outside, @"^\s*(?:half|float|int|uint)[234]?\s+" + Regex.Escape(name) + @"\s*;", RegexOptions.Multiline)).ToList();
            Assert.IsEmpty(redeclared, string.Join(", ", redeclared));
        }

        // ---- キーワード

        [Test]
        public void ToggleKeywords_AreDeclaredAndUsed()
        {
            var shader = Read("GalaxyToon.shader");
            var toggles = Regex.Matches(shader, @"\[(?:Toggle|ToggleOff)\((\w+)\)\]").Cast<Match>().Select(m => m.Groups[1].Value).ToList();
            var declared = new HashSet<string>(Passes(shader).SelectMany(p => p.keywords));
            var used = KeywordsTested(Directory.GetFiles(Path.GetDirectoryName(ShaderFile("GalaxyToon.shader")), "*.hlsl").Select(File.ReadAllText).Append(shader));
            Assert.IsEmpty(toggles.Where(k => !declared.Contains(k)).ToList(), "どのパスでも宣言していないキーワード");
            Assert.IsEmpty(toggles.Where(k => !used.Contains(k)).ToList(), "コードで使っていないキーワード");
        }

        /// <summary>
        /// ForwardLit 以外のパスで、そのパスのコード（GalaxyToonPasses・GalaxyToonInput）が調べるキーワードは、そのパスで宣言している
        /// （宣言していないと、ディゾルブ・切り抜きなどがそのパスだけ効かない：消えた所に影・線が残る）。
        /// </summary>
        [Test]
        public void NonForwardPasses_DeclareTheKeywordsTheirCodeTests()
        {
            var shader = Read("GalaxyToon.shader");
            var toggles = new HashSet<string>(Regex.Matches(shader, @"\[(?:Toggle|ToggleOff|KeywordEnum)\((\w+)").Cast<Match>().Select(m => m.Groups[1].Value));
            var tested = KeywordsTested(new[] { Read("GalaxyToonPasses.hlsl"), Read("GalaxyToonInput.hlsl") });
            var problems = new List<string>();
            foreach (var (name, keywords) in Passes(shader).Where(p => p.name != "ForwardLit"))
            {
                var missing = tested.Where(k => toggles.Contains(k) && !keywords.Contains(k)).ToList();
                if (missing.Count > 0) problems.Add(name + ": " + string.Join(", ", missing));
            }

            Assert.IsEmpty(problems, string.Join("\n", problems));
        }

        // ---- 描画モード

        [TestCase(GalaxyToonGUI.RenderMode.Opaque)]
        [TestCase(GalaxyToonGUI.RenderMode.Cutout)]
        [TestCase(GalaxyToonGUI.RenderMode.Transparent)]
        public void Apply_SetsRenderStatesForMode(GalaxyToonGUI.RenderMode mode)
        {
            var material = new Material(Shader.Find(ToonShaderName));
            try
            {
                material.SetFloat("_RenderMode", (float)mode);
                GalaxyToonGUI.Apply(material);
                var cutout = mode == GalaxyToonGUI.RenderMode.Cutout;
                var transparent = mode == GalaxyToonGUI.RenderMode.Transparent;
                Assert.AreEqual(cutout, material.IsKeywordEnabled("_ALPHATEST_ON"), "_ALPHATEST_ON");
                Assert.AreEqual(transparent ? 0f : 1f, material.GetFloat("_ZWrite"), "_ZWrite");
                Assert.AreEqual(cutout ? 1f : 0f, material.GetFloat("_AlphaToMask"), "_AlphaToMask（半透明で入れるとアルファが二重に効く）");
                Assert.AreEqual((float)(transparent ? BlendMode.SrcAlpha : BlendMode.One), material.GetFloat("_SrcBlend"), "_SrcBlend");
                Assert.AreEqual((float)(transparent ? BlendMode.OneMinusSrcAlpha : BlendMode.Zero), material.GetFloat("_DstBlend"), "_DstBlend");
                var queue = transparent ? (int)RenderQueue.Transparent : cutout ? (int)RenderQueue.AlphaTest : (int)RenderQueue.Geometry;
                Assert.GreaterOrEqual(material.renderQueue, queue, "renderQueue");
                Assert.Less(material.renderQueue, queue + 50, "renderQueue");
            }
            finally
            {
                Object.DestroyImmediate(material);
            }
        }

        // ---- 描けるか

        /// <summary>いくつかのキーワードの組み合わせで球を描き、エラーが出ず、エラーのシェーダー（マゼンタ）にならず、色が NaN にならない。</summary>
        [TestCase(0, "")]
        [TestCase(1, "")]
        [TestCase(2, "")]
        [TestCase(0, "_ANIME")]
        [TestCase(1, "_ANIME _RIMLIGHT _SPECULAR _ANISOTROPY")]
        [TestCase(0, "_DISSOLVE _NEAR_FADE")]
        [TestCase(0, "_FACE _HAIR_SHADOW _NORMALMAP")]
        [TestCase(0, "_CONTACT_SHADOW _CREVICE_AO _DEPTH_RIM")]
        public void Render_SphereProducesValidPixels(int renderMode, string keywords)
        {
            var material = new Material(Shader.Find(ToonShaderName));
            var sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            var cameraObject = new GameObject("GalaxyToonTestCamera");
            var light = new GameObject("GalaxyToonTestLight").AddComponent<Light>();
            var target = new RenderTexture(64, 64, 24);
            var readback = new Texture2D(64, 64, TextureFormat.RGBA32, false);
            bool async = ShaderUtil.allowAsyncCompilation;
            ShaderUtil.allowAsyncCompilation = false; // 初めての組み合わせでも、仮の表示(何も描かない)ではなく、その場でコンパイルして描く
            try
            {
                material.SetFloat("_RenderMode", renderMode);
                GalaxyToonGUI.Apply(material);
                foreach (var keyword in keywords.Split(new[] { ' ' }, System.StringSplitOptions.RemoveEmptyEntries)) material.EnableKeyword(keyword);
                sphere.GetComponent<Renderer>().sharedMaterial = material;
                sphere.transform.position = new Vector3(0f, -1000f, 0f);
                light.type = LightType.Directional;
                light.transform.rotation = Quaternion.Euler(40f, -30f, 0f);
                var camera = cameraObject.AddComponent<Camera>();
                camera.transform.position = sphere.transform.position + new Vector3(0f, 0f, -3f);
                camera.transform.LookAt(sphere.transform);
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = Color.black;
                camera.targetTexture = target;
                camera.Render();
                camera.Render(); // 2 回描く: DX12 は新しいキーワードの組み合わせの初回の描画が間に合わないことがある
                RenderTexture.active = target;
                readback.ReadPixels(new Rect(0, 0, 64, 64), 0, 0);
                readback.Apply();
                RenderTexture.active = null;
                var center = readback.GetPixel(32, 32);
                Assert.IsFalse(float.IsNaN(center.r) || float.IsNaN(center.g) || float.IsNaN(center.b), "NaN");
                Assert.IsFalse(center.r > 0.9f && center.g < 0.1f && center.b > 0.9f, "エラーのシェーダー（マゼンタ）: " + center);
                Assert.Greater(center.r + center.g + center.b, 0.02f, "何も描かれていない: " + center);
            }
            finally
            {
                ShaderUtil.allowAsyncCompilation = async;
                RenderTexture.active = null;
                Object.DestroyImmediate(cameraObject);
                Object.DestroyImmediate(readback);
                Object.DestroyImmediate(target);
                Object.DestroyImmediate(light.gameObject);
                Object.DestroyImmediate(sphere);
                Object.DestroyImmediate(material);
            }
        }

        // ---- 止めた描画機能

        /// <summary>
        /// 描画機能（キャラ専用の影・髪の影・Volume）を作ったあと、描かないフレームがあっても、シェーダーへの値は「使わない」に戻る
        /// （止めた機能は値を入れ直さないので、前の値が残ると顔が暗いまま・前の影の画像を読み続ける）。
        /// </summary>
        [Test]
        public void RendererFeatures_ResetGlobalsAtFrameStart()
        {
            var shadow = ScriptableObject.CreateInstance<GalaxyCharacterShadowFeature>();
            var hair = ScriptableObject.CreateInstance<GalaxyHairShadowFeature>();
            var volume = ScriptableObject.CreateInstance<GalaxyToonVolumeFeature>();
            var cameraObject = new GameObject("GalaxyToonTestCamera");
            var target = new RenderTexture(16, 16, 24);
            var shadowId = Shader.PropertyToID("_GalaxyCharShadowParams");
            var hairId = Shader.PropertyToID("_GalaxyHairShadowOn");
            var volumeId = Shader.PropertyToID("_GalaxyVolumeA");
            var before = (Shader.GetGlobalVector(shadowId), Shader.GetGlobalFloat(hairId), Shader.GetGlobalVector(volumeId));
            try
            {
                shadow.Create();
                hair.Create();
                volume.Create();
                Shader.SetGlobalVector(shadowId, new Vector4(1f, 0.001f, 1.5f, 1f));
                Shader.SetGlobalFloat(hairId, 1f);
                Shader.SetGlobalVector(volumeId, Vector4.one);
                // この 3 つはレンダラーに入っていない（＝止めた機能と同じ）。フレームを描くと、始めに戻る
                var camera = cameraObject.AddComponent<Camera>();
                camera.targetTexture = target;
                camera.Render();
                Assert.AreEqual(0f, Shader.GetGlobalVector(shadowId).w, "キャラ専用の影");
                Assert.AreEqual(0f, Shader.GetGlobalFloat(hairId), "髪の影");
                Assert.AreEqual(Vector4.zero, Shader.GetGlobalVector(volumeId), "Volume");
            }
            finally
            {
                Object.DestroyImmediate(cameraObject);
                Object.DestroyImmediate(target);
                Object.DestroyImmediate(shadow);
                Object.DestroyImmediate(hair);
                Object.DestroyImmediate(volume);
                Shader.SetGlobalVector(shadowId, before.Item1);
                Shader.SetGlobalFloat(hairId, before.Item2);
                Shader.SetGlobalVector(volumeId, before.Item3);
            }
        }

        // ---- 読み取り

        private static Dictionary<string, string> PropertyTypes(string shader)
        {
            var block = shader.Substring(shader.IndexOf("Properties"));
            block = block.Substring(0, block.IndexOf("SubShader"));
            var result = new Dictionary<string, string>();
            foreach (Match m in Regex.Matches(block, @"^\s*(?:\[[^\]]*\]\s*)*(_\w+)\s*\(\s*""[^""]*""\s*,\s*(\w+)", RegexOptions.Multiline)) result[m.Groups[1].Value] = m.Groups[2].Value;
            return result;
        }

        private static HashSet<string> UnityPerMaterial(string input)
        {
            var start = input.IndexOf("CBUFFER_START(UnityPerMaterial)");
            var end = input.IndexOf("CBUFFER_END", start);
            var block = Regex.Replace(input.Substring(start, end - start), @"//.*", "");
            var names = new HashSet<string>();
            foreach (Match m in Regex.Matches(block, @"\b(?:half|float|int|uint)[234]?(?:x[234])?\s+([^;]+);"))
            {
                foreach (var part in m.Groups[1].Value.Split(',')) names.Add(part.Trim().Split('[')[0]);
            }

            return names;
        }

        private static List<(string name, HashSet<string> keywords)> Passes(string shader)
        {
            var result = new List<(string, HashSet<string>)>();
            foreach (var pass in Regex.Split(shader, @"\n\s*Pass\s*\n").Skip(1))
            {
                var name = Regex.Match(pass, @"Name\s+""(\w+)""").Groups[1].Value;
                var keywords = new HashSet<string>();
                foreach (Match m in Regex.Matches(pass, @"#pragma\s+(?:shader_feature|multi_compile)\w*\s+([^\n]+)"))
                {
                    foreach (var word in m.Groups[1].Value.Split(new[] { ' ', '\t', '\r' }, System.StringSplitOptions.RemoveEmptyEntries)) keywords.Add(word);
                }

                result.Add((name, keywords));
            }

            return result;
        }

        private static HashSet<string> KeywordsTested(IEnumerable<string> sources)
        {
            var result = new HashSet<string>();
            foreach (var source in sources)
            {
                foreach (Match m in Regex.Matches(source, @"defined\((_[A-Z0-9_]+)\)")) result.Add(m.Groups[1].Value);
                foreach (Match m in Regex.Matches(source, @"#ifn?def\s+(_[A-Z0-9_]+)")) result.Add(m.Groups[1].Value);
            }

            return result;
        }
    }
}
