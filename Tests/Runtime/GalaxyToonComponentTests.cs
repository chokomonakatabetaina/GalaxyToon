using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace GalaxyToon.Tests
{
    /// <summary>実行時のコンポーネントを、再生中に数フレーム動かして確かめる。</summary>
    public class GalaxyToonComponentTests
    {
        private readonly List<Object> created = new List<Object>();
        private T Keep<T>(T o) where T : Object { created.Add(o); return o; }

        [TearDown]
        public void TearDown()
        {
            foreach (Object o in created) if (o != null) Object.Destroy(o);
            created.Clear();
        }

        private Material Toon(params (string name, float value)[] floats)
        {
            var m = Keep(new Material(Shader.Find("GalaxyShader/Toon")));
            foreach (var (n, v) in floats) m.SetFloat(n, v);
            return m;
        }

        private GameObject Sphere(string name, Material m)
        {
            var go = Keep(GameObject.CreatePrimitive(PrimitiveType.Sphere));
            go.name = name;
            go.GetComponent<Renderer>().sharedMaterial = m;
            return go;
        }

        /// <summary>球を顔(SkinnedMeshRenderer)にする。blendShape を渡すと、その名前で頂点を前へ出す形を足す。</summary>
        private SkinnedMeshRenderer SkinnedSphere(Material m, string blendShape = null)
        {
            var temp = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            Mesh mesh = Keep(Object.Instantiate(temp.GetComponent<MeshFilter>().sharedMesh));
            Object.Destroy(temp);
            if (blendShape != null)
            {
                var deltas = new Vector3[mesh.vertexCount];
                for (int i = 0; i < deltas.Length; i++) deltas[i] = Vector3.back * 0.05f;
                mesh.AddBlendShapeFrame(blendShape, 100f, deltas, null, null);
            }
            var smr = Keep(new GameObject("Face")).AddComponent<SkinnedMeshRenderer>();
            smr.sharedMesh = mesh;
            smr.sharedMaterial = m;
            return smr;
        }

        private static int NearestVertex(Mesh mesh, Vector3 local)
        {
            Vector3[] v = mesh.vertices; int best = 0;
            for (int i = 1; i < v.Length; i++) if ((v[i] - local).sqrMagnitude < (v[best] - local).sqrMagnitude) best = i;
            return best;
        }

        private static MaterialPropertyBlock Block(Renderer r) { var b = new MaterialPropertyBlock(); r.GetPropertyBlock(b); return b; }

        // ================================================================ 音

        [Test]
        public void AudioReact_SplitsTheSpectrumIntoBands()
        {
            var spectrum = new float[512];
            Assert.AreEqual(Vector4.zero, GalaxyAudioReact.Bands(spectrum, 48000f, 8f, 250f, 2000f));
            spectrum[2] = 0.5f; // 約 94Hz: 低音
            Vector4 b = GalaxyAudioReact.Bands(spectrum, 48000f, 8f, 250f, 2000f);
            Assert.Greater(b.x, 0f); Assert.AreEqual(0f, b.y); Assert.AreEqual(0f, b.z); Assert.Greater(b.w, 0f);
            spectrum[100] = 1f; // 約 4.7kHz: 高音
            Assert.Greater(GalaxyAudioReact.Bands(spectrum, 48000f, 8f, 250f, 2000f).z, 0f);
        }

        [UnityTest]
        public IEnumerator AudioReact_ClearsTheGlobalWhenDisabled()
        {
            var react = Keep(new GameObject("Audio")).AddComponent<GalaxyAudioReact>();
            yield return null;
            Assert.IsFalse(float.IsNaN(react.Level.x));
            react.enabled = false;
            Assert.AreEqual(Vector4.zero, Shader.GetGlobalVector("_GalaxyAudio"));
        }

        // ================================================================ 管のメッシュ

        [Test]
        public void TubeMesh_BuildsTubesAndDrops()
        {
            var owner = Keep(new GameObject("Owner")).transform;
            var tube = new GalaxyTubeMesh(owner, "Test");
            Keep(tube.Mesh);
            tube.Begin();
            tube.AddTube(new[] { Vector3.zero, Vector3.up * 0.1f, Vector3.up * 0.2f }, new[] { 0.01f, 0.01f, 0.005f }, 3);
            tube.AddDrop(Vector3.down * 0.1f, 0.02f, Vector3.down, 1.4f);
            tube.End();
            Assert.Greater(tube.Mesh.vertexCount, 20);
            Assert.Greater(tube.Mesh.triangles.Length, 30);
            Assert.AreEqual(HideFlags.DontSave, tube.Mesh.hideFlags & HideFlags.DontSave, "シーンに保存しない");
            tube.Begin(); tube.End();
            Assert.AreEqual(0, tube.Mesh.vertexCount);
        }

        // ================================================================ キャラ専用ライト

        [UnityTest]
        public IEnumerator Light_PerCharacterLightDoesNotClearTheGlobalLight()
        {
            var global = Keep(new GameObject("Global")).AddComponent<GalaxyToonLight>();
            global.transform.rotation = Quaternion.LookRotation(Vector3.down);
            yield return null;
            Assert.AreEqual(1f, Shader.GetGlobalVector("_AdvToonLightDir").y, 1e-4f, "光へ向かう向き");
            GameObject character = Sphere("Character", Toon());
            var local = Keep(new GameObject("Local")).AddComponent<GalaxyToonLight>(); // 対象を入れる前は全キャラ用
            local.transform.rotation = Quaternion.LookRotation(Vector3.left);
            local.target = character;
            yield return null;
            yield return null;
            Assert.AreEqual(1f, Shader.GetGlobalVector("_AdvToonLightDir").y, 1e-4f, "キャラ 1 体だけのライトが、全キャラ用の値を消さない");
            Vector4 dir = Block(character.GetComponent<Renderer>()).GetVector("_GalaxyLocalLightDir");
            Assert.AreEqual(1f, dir.x, 1e-4f, "そのキャラにだけ入る");
            local.enabled = false;
            Assert.AreEqual(Vector4.zero, Block(character.GetComponent<Renderer>()).GetVector("_GalaxyLocalLightDir"));
            global.enabled = false;
            Assert.AreEqual(Vector4.zero, Shader.GetGlobalVector("_AdvToonLightDir"));
        }

        // ================================================================ 顔・髪の揺れ

        [UnityTest]
        public IEnumerator Face_FeedsTheHeadDirectionAndCountsCharacters()
        {
            int before = GalaxyToonFace.ActiveCount;
            GameObject root = Keep(new GameObject("Character"));
            var head = new GameObject("Head").transform;
            head.SetParent(root.transform, false);
            GameObject face = Sphere("Face", Toon(("_FaceShading", 0.8f), ("_TipAxis", 2f)));
            face.GetComponent<Renderer>().sharedMaterial.SetColor("_TipColor", Color.white);
            face.transform.SetParent(root.transform, false);
            var comp = root.AddComponent<GalaxyToonFace>();
            yield return null;
            Assert.AreEqual(before + 1, GalaxyToonFace.ActiveCount);
            Assert.AreEqual(head, comp.head, "頭ボーンは名前で探す");
            root.transform.rotation = Quaternion.Euler(0f, 90f, 0f);
            yield return null;
            Vector4 forward = Block(face.GetComponent<Renderer>()).GetVector("_FaceForward");
            Assert.AreEqual(1f, forward.x, 0.05f, "首を回すと顔の正面もついてくる");
            comp.enabled = false;
            Assert.AreEqual(before, GalaxyToonFace.ActiveCount);
        }

        [UnityTest]
        public IEnumerator HairSway_LagsBehindAMovingHead()
        {
            GameObject root = Keep(new GameObject("Character"));
            var head = new GameObject("Head").transform;
            head.SetParent(root.transform, false);
            head.localPosition = Vector3.up * 1.5f;
            GameObject hair = Sphere("Hair", Toon(("_HairSway", 1f)));
            hair.transform.SetParent(root.transform, false);
            var sway = root.AddComponent<GalaxyToonHairSway>();
            sway.head = head;
            yield return null;
            for (int i = 0; i < 5; i++) { root.transform.position += Vector3.right * 0.2f; yield return null; }
            Assert.Greater(Block(hair.GetComponent<Renderer>()).GetVector("_HairSwayOffset").magnitude, 0f);
        }

        // ================================================================ 濡れ・汗・雨

        [UnityTest]
        public IEnumerator Effects_DriveMaterialsAndKeepOtherBlockValues()
        {
            GameObject root = Keep(new GameObject("Character"));
            Material m = Toon(("_UseFx", 1f));
            m.EnableKeyword("_GALAXY_FX");
            GameObject body = Sphere("Body", m);
            body.transform.SetParent(root.transform, false);
            Renderer r = body.GetComponent<Renderer>();
            var contact = new Vector4[32]; contact[0] = new Vector4(1f, 2f, 3f, 0.1f);
            var b = new MaterialPropertyBlock(); b.SetVectorArray("_SoftContactA", contact); b.SetFloat("_SoftContactCount", 1f); r.SetPropertyBlock(b);
            var fx = root.AddComponent<GalaxyToonEffects>();
            fx.wet = 1f; fx.sweat = 0.5f; fx.blush = 0.3f;
            yield return null;
            Assert.AreEqual(1f, Block(r).GetFloat("_WetAmount"), 1e-4f);
            fx.wet = 0f; fx.sweat = 0f; fx.blush = 0f;
            yield return null;
            Assert.AreEqual(1f, Block(r).GetFloat("_SoftContactCount"), "触れるとへこむの値は残す");
            Assert.AreEqual(contact[0], Block(r).GetVectorArray("_SoftContactA")[0]);
            fx.wet = 1f; fx.wetDryPerSecond = 100f;
            yield return null; yield return null;
            Assert.Less(fx.wet, 1f, "乾く");
            fx.enabled = false;
            Assert.IsFalse(Block(r).HasFloat("_WetAmount"), "外したらマテリアルの値に戻す");
            Assert.AreEqual(1f, Block(r).GetFloat("_SoftContactCount"), "外しても、ほかの部品の値は残す");
        }

        [UnityTest]
        public IEnumerator RainSplashes_EmitOnUpwardSurfaces()
        {
            GameObject root = Keep(new GameObject("Character"));
            Material m = Toon(("_UseFx", 1f), ("_RainSplash", 1f));
            m.EnableKeyword("_GALAXY_FX");
            GameObject body = Sphere("Body", m);
            body.transform.SetParent(root.transform, false);
            SkinnedMeshRenderer skinned = SkinnedSphere(m); // スキンメッシュの面は今の形を焼いて使う
            skinned.transform.SetParent(root.transform, false);
            skinned.transform.localPosition = new Vector3(1.2f, 0f, 0f);
            root.SetActive(false);
            var fx = root.AddComponent<GalaxyToonEffects>();
            fx.rain = 1f;
            fx.rainSplashMaterial = Keep(new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit")));
            root.SetActive(true);
            var cam = Keep(new GameObject("Camera")).AddComponent<Camera>(); // しぶきはカメラに写っている面だけから出す
            cam.transform.position = new Vector3(0f, 1f, -3f);
            cam.transform.LookAt(body.transform);
            var splashes = root.GetComponent<GalaxyRainSurfaceSplashes>();
            Assert.IsNotNull(splashes, "しぶきのマテリアルがあれば自動で付く");
            splashes.impactsPerSquareMeter = 2000f;
            splashes.bottomDripsPerSecond = 200f; // 下からしたたる粒も
            cam.targetTexture = Keep(new RenderTexture(64, 64, 24)); // batchmode では画面が無いので、テクスチャへ毎フレーム描かせて「写っている」にする
            for (int i = 0; i < 20; i++) yield return null;
            Assert.Greater(splashes.EmittedImpacts, 0);
            Assert.Greater(splashes.ImpactsOn("Body"), 0);
            Assert.IsNotNull(splashes.Particles);
            fx.rain = 0f; // 雨がやんだら何もしない
            yield return null;
        }

        // ================================================================ 涙・舌・糸

        [UnityTest]
        public IEnumerator Tears_FlowDownTheFaceAndFall()
        {
            SkinnedMeshRenderer face = SkinnedSphere(Toon());
            var go = Keep(new GameObject("Tears", typeof(MeshFilter), typeof(MeshRenderer)));
            var tears = go.AddComponent<GalaxyTears>();
            tears.face = face;
            tears.startVertices = new[] { NearestVertex(face.sharedMesh, new Vector3(0.15f, 0.1f, -0.47f)) };
            tears.poolTime = 0f; tears.speed = 2f; tears.interval = 0.05f;
            tears.crying = true;
            for (int i = 0; i < 30; i++) yield return null;
            Assert.Greater(go.GetComponent<MeshFilter>().sharedMesh.vertexCount, 0, "涙の筋・粒が描かれる");
            tears.crying = false;
            tears.viscosity = 1f;
            tears.Cry();
            Assert.IsTrue(tears.HasLiquid);
        }

        [UnityTest]
        public IEnumerator Tears_FindStartWithoutEyeBonesWarns()
        {
            SkinnedMeshRenderer face = SkinnedSphere(Toon());
            var tears = Keep(new GameObject("Tears", typeof(MeshFilter), typeof(MeshRenderer))).AddComponent<GalaxyTears>();
            tears.face = face;
            LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex("目のボーン"));
            tears.FindStartVertices();
            yield return null;
            Assert.AreEqual(0, tears.startVertices.Length);
        }

        [UnityTest]
        public IEnumerator TongueDrip_CriesWhenTheTongueComesOut()
        {
            SkinnedMeshRenderer face = SkinnedSphere(Toon(), "TongueOut");
            var go = Keep(new GameObject("Mouth", typeof(MeshFilter), typeof(MeshRenderer)));
            go.SetActive(false);
            var tears = go.AddComponent<GalaxyTears>();
            tears.face = face;
            tears.startVertices = new[] { 0 };
            go.AddComponent<GalaxyTongueDrip>();
            go.SetActive(true);
            yield return null;
            Assert.IsFalse(tears.HasLiquid);
            face.SetBlendShapeWeight(0, 100f);
            yield return null;
            Assert.IsTrue(tears.HasLiquid, "舌を出した瞬間に 1 回");
        }

        [UnityTest]
        public IEnumerator StickyStrings_StretchBreakAndReconnect()
        {
            var a = Keep(new GameObject("A")).transform;
            var b = Keep(new GameObject("B")).transform;
            b.position = Vector3.right * 0.01f;
            var go = Keep(new GameObject("Strings", typeof(MeshFilter), typeof(MeshRenderer)));
            go.GetComponent<MeshRenderer>().sharedMaterial = Toon();
            var s = go.AddComponent<GalaxyStickyStrings>();
            s.anchorA = a; s.anchorB = b;
            yield return null;
            Mesh mesh = go.GetComponent<MeshFilter>().sharedMesh;
            Assert.Greater(mesh.vertexCount, 0);
            b.position = Vector3.right * 1f; // 切れる長さを超える
            for (int i = 0; i < 40; i++) yield return null;
            b.position = Vector3.right * 0.001f; // 近づくとまたつながる
            for (int i = 0; i < 3; i++) yield return null;
            Assert.Greater(go.GetComponent<MeshFilter>().sharedMesh.vertexCount, 0);
            Assert.AreEqual(-1, GalaxyStickyStrings.FindMouthOpenShape(Keep(new Mesh())));
            SkinnedMeshRenderer face = SkinnedSphere(Toon(), "Fcl_MTH_A");
            Assert.AreEqual(0, GalaxyStickyStrings.FindMouthOpenShape(face.sharedMesh));
            s.skin = face;
            s.FindLips();
            yield return null;
        }

        // ================================================================ 転送・毛・前髪

        [UnityTest]
        public IEnumerator Teleport_VanishesAndRestoresMaterials()
        {
            GameObject root = Keep(new GameObject("Character"));
            Material m = Toon();
            GameObject body = Sphere("Body", m);
            body.transform.SetParent(root.transform, false);
            var tp = root.AddComponent<GalaxyTeleport>();
            tp.duration = 0.2f;
            tp.particleMaterial = Keep(new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit")));
            bool finished = false;
            tp.onFinished.AddListener(() => finished = true);
            tp.Play();
            Assert.IsTrue(tp.IsPlaying);
            yield return null;
            Assert.AreNotEqual(m, body.GetComponent<Renderer>().sharedMaterial, "演出中は複製");
            yield return new WaitForSeconds(0.5f);
            Assert.IsFalse(tp.IsPlaying);
            Assert.IsTrue(finished);
            Assert.AreEqual(m, body.GetComponent<Renderer>().sharedMaterial, "終わったら元のマテリアル");
            Assert.IsFalse(body.GetComponent<Renderer>().enabled, "消えた物は非表示");
            tp.kind = GalaxyTeleport.Kind.Appear;
            tp.incoming = new[] { body.GetComponent<Renderer>() };
            tp.Play();
            yield return new WaitForSeconds(0.5f);
            Assert.IsTrue(body.GetComponent<Renderer>().enabled);
        }

        [UnityTest]
        public IEnumerator Fur_DrawsTheShells()
        {
            GameObject body = Sphere("Body", Toon());
            var fur = body.AddComponent<GalaxyToonFur>();
            fur.furMaterial = Keep(new Material(Shader.Find("GalaxyShader/ToonFur")));
            fur.shells = 4;
            int before = GalaxyToonFur.DrawCount;
            yield return null;
            yield return null;
            Assert.Greater(GalaxyToonFur.DrawCount, before);
        }

        [UnityTest]
        public IEnumerator Fur_FollowsASkinnedMesh()
        {
            SkinnedMeshRenderer body = SkinnedSphere(Toon());
            var fur = body.gameObject.AddComponent<GalaxyToonFur>();
            fur.furMaterial = Keep(new Material(Shader.Find("GalaxyShader/ToonFur")));
            fur.shells = 2;
            int before = GalaxyToonFur.DrawCount;
            yield return null;
            yield return null;
            Assert.Greater(GalaxyToonFur.DrawCount, before, "スキンメッシュは今の形を焼いて毛を描く");
            body.sharedMesh = null;
            yield return null; // メッシュが無いときは描かない(エラーにしない)
        }

        // ================================================================ 顔(向き・遠近・毛先・体の SDF)

        [UnityTest]
        public IEnumerator Face_FeedsHeadSplitPerspectiveTipsAndBody()
        {
            GameObject root = Keep(new GameObject("Character"));
            var head = new GameObject("Head").transform;
            head.SetParent(root.transform, false);
            head.localPosition = new Vector3(0f, 1.5f, 0f);
            var hairBone = new GameObject("HairBone").transform;
            hairBone.SetParent(head, false);
            hairBone.localPosition = new Vector3(0f, 0.1f, 0f);
            GameObject split = Sphere("Split", Toon(("_AmbientHeadSplit", 1f)));
            split.transform.SetParent(root.transform, false);
            GameObject persp = Sphere("Perspective", Toon(("_PerspectiveRemoval", 1f)));
            persp.transform.SetParent(root.transform, false);
            GameObject body = Sphere("Body", Toon(("_UseFaceSdf", 1f)));
            body.transform.SetParent(root.transform, false);
            Material tip = Toon(("_TipAxis", 2f));
            tip.SetColor("_TipColor", Color.white);
            SkinnedMeshRenderer hair = HairRenderer(root.transform, hairBone, new[] { tip, tip }, true);
            var face = root.AddComponent<GalaxyToonFace>();
            yield return null;
            yield return null;
            Assert.AreEqual(1f, Block(split.GetComponent<Renderer>()).GetVector("_HeadForward").w, "環境光を頭の向きで分けるマテリアルに、頭の正面");
            Assert.AreEqual(1f, Block(persp.GetComponent<Renderer>()).GetVector("_PerspectiveCenter").w, "遠近の除去の中心");
            Assert.AreNotEqual(Vector4.zero, Block(body.GetComponent<Renderer>()).GetVector("_FaceForward"), "体の SDF にキャラの正面");
            Assert.AreNotEqual(Vector4.zero, Block(hair).GetVector("_TipRestRow"), "毛先の高さは頭の下のボーンで戻す");

            // 毛先の行: 頭そのものがボーン / 頭の外のボーンだけ / ボーンが空
            SkinnedMeshRenderer onHead = HairRenderer(root.transform, head, new[] { tip }, true);
            Assert.AreNotEqual(Vector4.zero, GalaxyToonFace.TipRestRow(onHead, head));
            var outside = Keep(new GameObject("Outside")).transform;
            SkinnedMeshRenderer away = HairRenderer(root.transform, outside, new[] { tip }, true);
            Assert.AreEqual(Vector4.zero, GalaxyToonFace.TipRestRow(away, head));
            away.bones = new Transform[] { null };
            Assert.AreEqual(Vector4.zero, GalaxyToonFace.TipRestRow(away, head));
            Assert.AreEqual(Vector4.zero, GalaxyToonFace.TipRestRow(split.GetComponent<Renderer>(), head), "スキンでない Renderer は 0");
            face.enabled = false;
        }

        // ================================================================ 涙(溜まる・流れる・落ちる・消える、出始めを探す)

        [UnityTest]
        public IEnumerator Tears_PoolFlowFallAndFade()
        {
            SkinnedMeshRenderer face = SkinnedSphere(Toon());
            var go = Keep(new GameObject("Tears", typeof(MeshFilter), typeof(MeshRenderer)));
            var tears = go.AddComponent<GalaxyTears>();
            tears.face = face;
            tears.startVertices = new[] { NearestVertex(face.sharedMesh, new Vector3(0.15f, 0.1f, -0.47f)) };
            tears.poolTime = 0.05f; tears.speed = 3f; tears.interval = 0.05f; tears.fadeTime = 0.05f; tears.stopAndGo = 0.5f;
            tears.crying = true;
            yield return new WaitForSeconds(0.4f); // 溜まる → 流れる → 落ちる(水)
            tears.viscosity = 1f; tears.strandLength = 0.002f; // どろどろ: 先端から糸状に伸びて切れる
            yield return new WaitForSeconds(0.6f);
            tears.crying = false;
            yield return new WaitForSeconds(1.6f); // 落ちきって筋も消える
            Assert.IsFalse(tears.HasLiquid, "全部落ちて消えた");
            tears.Cry(0); // 片目だけ
            Assert.IsTrue(tears.HasLiquid);
        }

        [UnityTest]
        public IEnumerator Tears_FindTheStartFromEyeBonesAndWhites()
        {
            SkinnedMeshRenderer face = SkinnedSphere(Toon());
            Mesh mesh = face.sharedMesh;
            int[] all = mesh.triangles;
            var white = new List<int>();
            Vector3[] v = mesh.vertices;
            for (int i = 0; i < all.Length; i += 3) // 目のあたりの三角形を「白目」のサブメッシュにする
            {
                Vector3 c = (v[all[i]] + v[all[i + 1]] + v[all[i + 2]]) / 3f;
                if (c.z < -0.4f && Mathf.Abs(c.y - 0.1f) < 0.05f && Mathf.Abs(Mathf.Abs(c.x) - 0.15f) < 0.06f) white.AddRange(new[] { all[i], all[i + 1], all[i + 2] });
            }
            mesh.subMeshCount = 2;
            mesh.SetTriangles(all, 0);
            mesh.SetTriangles(white.ToArray(), 1);
            Material eyeWhite = Toon(); eyeWhite.name = "EyeWhite";
            face.sharedMaterials = new[] { face.sharedMaterial, eyeWhite };
            for (int s = -1; s <= 1; s += 2)
            {
                var eye = new GameObject(s < 0 ? "Eye_R_" : "Eye_L_").transform;
                eye.SetParent(face.transform, false);
                eye.localPosition = new Vector3(0.15f * s, 0.1f, -0.3f);
            }
            var tears = Keep(new GameObject("Tears", typeof(MeshFilter), typeof(MeshRenderer))).AddComponent<GalaxyTears>();
            tears.face = face;
            foreach (GalaxyTears.Source source in new[] { GalaxyTears.Source.OuterCorner, GalaxyTears.Source.InnerCorner, GalaxyTears.Source.UnderEye })
            {
                tears.source = source;
                tears.Cry(); // つながりを作って、出始めを探して、流す
                Assert.AreEqual(2, tears.startVertices.Length, source + ": 左右の目の下");
                yield return null;
            }
            // 白目が無いときは、目のボーンの少し下・前
            face.sharedMaterials = new[] { face.sharedMaterial, face.sharedMaterial };
            tears.source = GalaxyTears.Source.OuterCorner;
            tears.Cry();
            Assert.AreEqual(2, tears.startVertices.Length);
        }

        // ================================================================ 糸(スキンの頂点を追う・唇を探す)

        /// <summary>口のメッシュ: 上唇の頂点(動かない)と、下唇の頂点(口を開けるシェイプで下へ動く)。</summary>
        private SkinnedMeshRenderer Mouth(Vector3[] upper, Vector3[] lower)
        {
            var vertices = new List<Vector3>(upper); vertices.AddRange(lower);
            var tris = new List<int>();
            for (int i = 0; i + 2 < vertices.Count; i++) tris.AddRange(new[] { i, i + 1, i + 2 });
            if (tris.Count == 0) tris.AddRange(new[] { 0, 0, 0 });
            var mesh = Keep(new Mesh());
            mesh.SetVertices(vertices);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateNormals();
            var deltas = new Vector3[vertices.Count];
            for (int i = upper.Length; i < deltas.Length; i++) deltas[i] = Vector3.down * 0.02f;
            mesh.AddBlendShapeFrame("MouthOpen", 100f, deltas, null, null);
            var smr = Keep(new GameObject("Mouth")).AddComponent<SkinnedMeshRenderer>();
            smr.sharedMesh = mesh;
            return smr;
        }

        [UnityTest]
        public IEnumerator StickyStrings_FollowSkinVerticesAndFindLips()
        {
            var go = Keep(new GameObject("Strings", typeof(MeshFilter), typeof(MeshRenderer)));
            go.GetComponent<MeshRenderer>().sharedMaterial = Toon();
            var s = go.AddComponent<GalaxyStickyStrings>();
            SkinnedMeshRenderer mouth = Mouth(new[] { new Vector3(-0.01f, 0.01f, 0f), new Vector3(0f, 0.01f, -0.002f), new Vector3(0.01f, 0.01f, 0f) },
                                              new[] { new Vector3(-0.01f, 0f, 0f), new Vector3(0f, 0f, -0.002f), new Vector3(0.01f, 0f, 0f) });
            s.skin = mouth;
            s.FindLips();
            Assert.AreEqual(1, s.vertexA, "上唇は真ん中の前");
            Assert.AreEqual(4, s.vertexB, "下唇は真ん中の前");
            yield return null; // 頂点を追って糸を描く
            Assert.Greater(go.GetComponent<MeshFilter>().sharedMesh.vertexCount, 0);
            SkinnedMeshRenderer other = Mouth(new[] { Vector3.zero }, new[] { new Vector3(0f, -0.01f, 0f) });
            s.skinB = other; s.vertexB = 1; // 端B は別のメッシュ
            yield return null;
            s.vertexB = 99; // 範囲外は描かない
            yield return null;
            s.skinB = null; s.vertexA = 99;
            yield return null;

            // 見つからないとき: 口が開かない / 下唇が真ん中に無い / 上唇が無い
            s.skin = null;
            LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex("SkinnedMeshRenderer"));
            s.FindLips();
            s.skin = Mouth(new[] { Vector3.zero }, new Vector3[0]);
            LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex("口が見つかりません"));
            s.FindLips();
            s.skin = Mouth(new[] { new Vector3(0f, 0.3f, 0f) }, new[] { new Vector3(-0.01f, 0f, 0f), new Vector3(0.01f, 0f, 0f) });
            LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex("下唇が見つかりません"));
            s.FindLips();
            s.skin = Mouth(new[] { new Vector3(0f, 0.3f, 0f) }, new[] { Vector3.zero });
            LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex("上唇が見つかりません"));
            s.FindLips();
        }

        [UnityTest]
        public IEnumerator SeeThroughBangs_StopsWithoutAHead()
        {
            var root = Keep(new GameObject("NoHead"));
            LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex("頭ボーン"));
            var bangs = root.AddComponent<GalaxyToonSeeThroughBangs>();
            for (int i = 0; i < 3; i++) yield return null;
            Assert.IsFalse(bangs.enabled);
        }

        /// <summary>髪の SkinnedMeshRenderer: サブメッシュ 0 = 顔の前の前髪(四角)、1 = 後ろ髪(四角)。頭ボーン 1 本、ブレンドシェイプ 1 つ。</summary>
        private SkinnedMeshRenderer HairRenderer(Transform parent, Transform head, Material[] mats, bool withBangs)
        {
            var go = new GameObject("Hair");
            go.transform.SetParent(parent, false);
            var smr = go.AddComponent<SkinnedMeshRenderer>();
            Vector3 h = head.position - parent.position;
            var v = new List<Vector3>();
            // 前髪: 目(高さ 0.07・前 0.06)より上、生え際より下、顔の前
            if (withBangs) v.AddRange(new[] { h + new Vector3(-0.05f, 0.18f, 0.09f), h + new Vector3(0.05f, 0.18f, 0.09f), h + new Vector3(-0.05f, 0.1f, 0.09f), h + new Vector3(0.05f, 0.1f, 0.09f) });
            else v.AddRange(new[] { h + new Vector3(-0.05f, 0.3f, 0.02f), h + new Vector3(0.05f, 0.3f, 0.02f), h + new Vector3(-0.05f, 0.25f, 0.02f), h + new Vector3(0.05f, 0.25f, 0.02f) });
            v.AddRange(new[] { h + new Vector3(-0.08f, 0.1f, -0.1f), h + new Vector3(0.08f, 0.1f, -0.1f), h + new Vector3(-0.08f, -0.1f, -0.1f), h + new Vector3(0.08f, -0.1f, -0.1f) });
            var mesh = Keep(new Mesh { name = "HairMesh" });
            mesh.SetVertices(v);
            mesh.subMeshCount = 2;
            mesh.SetTriangles(new[] { 0, 1, 2, 2, 1, 3 }, 0);
            mesh.SetTriangles(new[] { 4, 5, 6, 6, 5, 7 }, 1);
            var uv = new List<Vector2>(); foreach (Vector3 p in v) uv.Add(new Vector2(p.x, p.y));
            mesh.SetUVs(0, uv);
            mesh.SetUVs(2, uv);
            var colors = new Color32[v.Count]; for (int i = 0; i < colors.Length; i++) colors[i] = new Color32(255, 128, 0, 255);
            mesh.colors32 = colors;
            mesh.RecalculateNormals();
            mesh.RecalculateTangents();
            var weights = new BoneWeight[v.Count]; for (int i = 0; i < weights.Length; i++) weights[i] = new BoneWeight { boneIndex0 = 0, weight0 = 1f };
            mesh.boneWeights = weights;
            mesh.bindposes = new[] { head.worldToLocalMatrix * go.transform.localToWorldMatrix };
            var deltas = new Vector3[v.Count]; for (int i = 0; i < deltas.Length; i++) deltas[i] = Vector3.forward * 0.01f;
            mesh.AddBlendShapeFrame("Puff", 100f, deltas, deltas, deltas);
            smr.sharedMesh = mesh;
            smr.bones = new[] { head };
            smr.rootBone = head;
            smr.sharedMaterials = mats;
            return smr;
        }

        [UnityTest]
        public IEnumerator SeeThroughBangs_SplitsTheBangsAndFollowsTheHead()
        {
            var root = Keep(new GameObject("Character"));
            var head = new GameObject("Head").transform;
            head.SetParent(root.transform, false);
            head.localPosition = new Vector3(0f, 1.5f, 0f);
            for (int s = -1; s <= 1; s += 2) // 独自の目のボーン(Humanoid の目が無いとき、名前で探す)
            {
                var eye = new GameObject(s < 0 ? "EyeBone L" : "EyeBone R").transform;
                eye.SetParent(head, false);
                eye.localPosition = new Vector3(0.03f * s, 0.07f, 0.06f);
            }
            var brow = new GameObject("EyeBrow").transform; // 眉は目に数えない
            brow.SetParent(head, false);
            Material hair = Toon(); hair.name = "Hair_Mat";
            SkinnedMeshRenderer front = HairRenderer(root.transform, head, new[] { hair, hair }, true);
            SkinnedMeshRenderer back = HairRenderer(root.transform, head, new[] { hair }, false); // 前髪の範囲に何も無い髪
            back.name = "Back Hair";
            Material body = Toon(); body.name = "Body";
            SkinnedMeshRenderer other = HairRenderer(root.transform, head, new[] { body, null }, true); // 髪でないマテリアル
            other.name = "Body";
            var bangs = root.AddComponent<GalaxyToonSeeThroughBangs>();
            for (int i = 0; i < 3; i++) yield return null;

            StringAssert.Contains("(Bangs Solid)", front.sharedMaterials[0].name, "前髪のある髪は、付け根を不透明で描く");
            Transform tips = front.transform.Find("Hair (Bangs Tips)");
            Assert.IsNotNull(tips, "前髪の毛先だけのコピーを作る");
            var copy = tips.GetComponent<SkinnedMeshRenderer>();
            Assert.AreEqual(4, copy.sharedMaterials.Length, "深度 2 + 色 2");
            Assert.AreEqual(4, copy.sharedMesh.subMeshCount);
            Assert.AreEqual(hair, back.sharedMaterials[0], "前髪の無い髪は元のまま");
            Assert.AreEqual(body, other.sharedMaterials[0], "髪でないマテリアルは元のまま");
            Assert.AreEqual(1f, Shader.GetGlobalVector("_GalaxyBangCenter").w);

            front.SetBlendShapeWeight(0, 60f);
            yield return null;
            Assert.AreEqual(60f, copy.GetBlendShapeWeight(0), 1e-3f, "ブレンドシェイプを写す");

            bangs.enabled = false;
            Assert.AreEqual(0f, Shader.GetGlobalVector("_GalaxyBangCenter").w, "外したら透けを止める");
            bangs.enabled = true;
            Object.Destroy(bangs);
            yield return null;
            Assert.IsTrue(copy == null, "外したらコピーも消す");
        }

        // ================================================================ サンプルの部品・Volume

        [UnityTest]
        public IEnumerator SampleParts_MoveViewAndFeedContacts()
        {
            var mover = Keep(new GameObject("Mover")).AddComponent<GalaxyToonSampleMotion>();
            mover.offset = Vector3.right; mover.period = 0.2f;
            yield return null;
            yield return new WaitForSeconds(0.1f);
            Assert.Greater(mover.transform.localPosition.x, 0f);

            var viewer = Keep(new GameObject("Viewer")).AddComponent<GalaxyToonSampleViewer>();
            var target = Keep(new GameObject("Target")).transform;
            target.position = new Vector3(5f, 0f, 0f);
            viewer.entries.Add(new GalaxyToonSampleViewer.Entry { target = target, title = "t" });
            yield return null;
            MethodInfo show = typeof(GalaxyToonSampleViewer).GetMethod("Show", BindingFlags.NonPublic | BindingFlags.Instance);
            show.Invoke(viewer, new object[] { 0 });
            yield return new WaitForSeconds(1.5f);
            Assert.Greater(viewer.transform.position.x, 2f, "見本へ寄る");
            show.Invoke(viewer, new object[] { 1 }); // 端を越えると全体へ
            show.Invoke(viewer, new object[] { -2 });

            // ライトのプリセット: 向き・色を、GalaxyToonLight にも入れる
            var sun = Keep(new GameObject("Sun")).AddComponent<Light>();
            var sunToon = sun.gameObject.AddComponent<GalaxyToonLight>();
            viewer.sun = sun;
            viewer.SetLightPreset(2);
            Assert.AreEqual(GalaxyToonSampleViewer.LightPresets[2].color, sun.color);
            Assert.AreEqual(GalaxyToonSampleViewer.LightPresets[2].color, sunToon.color);
            Assert.AreEqual(1f, sunToon.night, "夜のプリセットは夜のランプ");
            viewer.SetLightPreset(99); // 範囲外は何もしない
            viewer.SetBackground(3);
            Assert.AreEqual(GalaxyToonSampleViewer.Backgrounds[3].color, viewer.GetComponent<Camera>().backgroundColor, "背景の色");
            viewer.SetBackground(-1); // 範囲外は何もしない
            Assert.AreEqual(GalaxyToonSampleViewer.Backgrounds[3].color, viewer.GetComponent<Camera>().backgroundColor);
            viewer.SetLightPreset(0);
            sunToon.enabled = false;

            // プレビューのモデルの素材: 端で一周し、押すとモデルへ寄る
            viewer.SetPreviewMaterial(0); // モデルが無いときは何もしない
            GameObject doll = Sphere("Preview", Toon());
            Material other = Toon(("_Metallic", 1f));
            viewer.entries.Add(new GalaxyToonSampleViewer.Entry { target = doll.transform, title = "preview", size = 2f });
            viewer.preview = doll.GetComponent<Renderer>();
            viewer.previewMaterials = new[] { doll.GetComponent<Renderer>().sharedMaterial, other };
            viewer.SetPreviewMaterial(-1);
            Assert.AreEqual(other, doll.GetComponent<Renderer>().sharedMaterial, "-1 は最後の素材");
            yield return new WaitForSeconds(0.5f);
            Assert.Less(viewer.transform.position.z, -2f, "大きい物は遠めに寄る");

            GameObject skin = Sphere("Skin", Toon(("_SoftTouch", 1f)));
            var touch = Keep(new GameObject("Finger")).AddComponent<GalaxyToonSampleContact>();
            touch.kind = GalaxyToonSampleContact.Kind.SoftTouch;
            touch.target = skin.GetComponent<Renderer>();
            yield return null;
            Assert.AreEqual(1f, Block(skin.GetComponent<Renderer>()).GetFloat("_SoftContactCount"));
            touch.enabled = false;
            Assert.AreEqual(0f, Block(skin.GetComponent<Renderer>()).GetFloat("_SoftContactCount"));

            var leg = Keep(new GameObject("Leg")).AddComponent<GalaxyToonSampleContact>();
            leg.kind = GalaxyToonSampleContact.Kind.LegPush; leg.radius = 0.1f;
            yield return null;
            Assert.AreEqual(0.1f, Shader.GetGlobalVector("_GalaxyLegStart0").w, 1e-4f);
            leg.enabled = false;
            Assert.AreEqual(0f, Shader.GetGlobalVector("_GalaxyLegStart0").w);

            var skirt = Keep(new GameObject("Skirt")).AddComponent<GalaxyToonSampleContact>();
            skirt.kind = GalaxyToonSampleContact.Kind.SkirtShadow;
            yield return null;
            Assert.AreEqual(1f, Shader.GetGlobalVector("_GalaxySkirtShadow").z);
            skirt.enabled = false;
            Assert.AreEqual(0f, Shader.GetGlobalVector("_GalaxySkirtShadow").z);
        }

        [Test]
        public void Volume_DefaultsChangeNothing()
        {
            var v = Keep(ScriptableObject.CreateInstance<GalaxyToonVolume>());
            Assert.AreEqual(1f, v.rim.value);
            Assert.AreEqual(0f, v.shadowDarken.value);
            Assert.AreEqual(Color.white, v.shadowTint.value);
            Assert.AreEqual(1f, v.outlineWidth.value);
        }
    }
}
