using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;
using static GalaxyToon.Editor.GalaxyToonText;

namespace GalaxyToon.Editor
{
    /// <summary>ほかのシェーダー(MToon・lilToon・UTS・URP Lit・Standard など)のマテリアルを GalaxyShader/Toon に移す。
    /// 部位(肌・顔・髪・服…)をマテリアル名から推測してプリセットを当て、その上に元の色・テクスチャ・影の境界・輪郭線などを写す。
    /// VRM(VRoid)のキャラは、選んで実行するだけで GalaxyToonFace・瞳と頬・なめらか法線・髪の影までまとめて設定できる。
    /// 既定では元のマテリアルは書き換えず、複製を作って Renderer に差し替える(VRM から取り込んだマテリアルは書き換えられないため)。</summary>
    public sealed class GalaxyToonMigrator : EditorWindow
    {
        public enum SourceKind { MToon, MToon10, LilToon, Uts, UrpLit, UrpUnlit, Standard, Generic, GalaxyToon }

        /// <summary>部位 → 当てるプリセット名(0 は当てない)。</summary>
        private static readonly string[] PartPresets = { "", "肌", "顔", "髪", "服", "金属", "顔パーツ" };
        private static string[] PartNames => new[] { L("(当てない)", "(None)"), L("肌", "Skin"), L("顔", "Face"), L("髪", "Hair"), L("服", "Clothes"), L("金属", "Metal"), L("顔パーツ", "Face Parts") };
        private const int PartNone = 0, PartSkin = 1, PartFace = 2, PartHair = 3, PartCloth = 4, PartMetal = 5, PartFaceParts = 6;

        private sealed class Entry
        {
            public Material material;
            public SourceKind kind;
            public int part;
            public bool include = true;
            public readonly List<(Renderer renderer, int slot)> users = new List<(Renderer, int)>();
        }

        private readonly List<Entry> entries = new List<Entry>();
        private readonly List<GameObject> roots = new List<GameObject>();
        private bool overwrite;
        private string folder = "";
        private bool addFace = true, findEyes = true, bakeOutline = true, hairShadow = true;
        private Vector2 scroll;
        private string message;

        [MenuItem("Tools/GalaxyToon/GalaxyToon に移行 (MToon・lilToon・Lit など)")]
        public static void Open()
        {
            var w = GetWindow<GalaxyToonMigrator>(L("GalaxyToon に移行", "Migrate to GalaxyToon"));
            w.Collect();
        }

        private void OnSelectionChange() { Collect(); Repaint(); }

        // ---------------------------------------------------------------- 画面

        private void OnGUI()
        {
            titleContent.text = L("GalaxyToon に移行", "Migrate to GalaxyToon");
            EditorGUILayout.HelpBox(L(
                "ほかのシェーダー(MToon・lilToon・UTS・URP Lit・Standard など)のマテリアルを GalaxyShader/Toon に移します。\nヒエラルキーでキャラを選ぶか、Project でマテリアルを選んでください。部位はマテリアル名から推測し、プリセットを当ててから元の色・テクスチャ・影・輪郭線を写します。",
                "Migrates materials from other shaders (MToon, lilToon, UTS, URP Lit, Standard, etc.) to GalaxyShader/Toon.\nSelect a character in the Hierarchy or materials in the Project. The body part is guessed from the material name; a preset is applied, then the original colors, textures, shading, and outline are copied."), MessageType.Info);

            if (entries.Count == 0)
            {
                EditorGUILayout.HelpBox(L("移行するマテリアルがありません。キャラかマテリアルを選んでください。", "No materials to migrate. Select a character or materials."), MessageType.None);
                return;
            }

            // ---- マテリアルの一覧(含める・元のシェーダー・部位)
            EditorGUILayout.LabelField(L($"マテリアル ({entries.Count})", $"Materials ({entries.Count})"), EditorStyles.boldLabel);
            using (var sv = new EditorGUILayout.ScrollViewScope(scroll, GUILayout.MaxHeight(260f)))
            {
                scroll = sv.scrollPosition;
                foreach (Entry e in entries)
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        using (new EditorGUI.DisabledScope(e.kind == SourceKind.GalaxyToon))
                            e.include = EditorGUILayout.Toggle(e.include && e.kind != SourceKind.GalaxyToon, GUILayout.Width(16f));
                        using (new EditorGUI.DisabledScope(true))
                            EditorGUILayout.ObjectField(e.material, typeof(Material), false, GUILayout.MinWidth(120f));
                        GUILayout.Label(KindName(e.kind), EditorStyles.miniLabel, GUILayout.Width(90f));
                        using (new EditorGUI.DisabledScope(e.kind == SourceKind.GalaxyToon))
                            e.part = EditorGUILayout.Popup(e.part, PartNames, GUILayout.Width(90f));
                    }
            }

            // ---- 保存のしかた
            EditorGUILayout.Space(6f);
            EditorGUILayout.LabelField(L("保存", "Output"), EditorStyles.boldLabel);
            overwrite = EditorGUILayout.Toggle(new GUIContent(L("元のマテリアルを書き換える", "Overwrite Original Materials"), L(
                "OFF(おすすめ): 複製を作って Renderer に差し替えます。元のマテリアルは残ります。\nON: 元のマテリアルのシェーダーを変えます。VRM などから取り込んだ、書き換えられないマテリアルは複製になります。",
                "OFF (recommended): creates copies and assigns them to the renderers. The originals are kept.\nON: changes the shader of the original materials. Read-only materials (imported from VRM, etc.) are copied instead.")), overwrite);
            using (new EditorGUI.DisabledScope(overwrite))
            using (new EditorGUILayout.HorizontalScope())
            {
                folder = EditorGUILayout.TextField(new GUIContent(L("保存先フォルダ", "Output Folder"), L("複製したマテリアルの保存先(Assets からのパス)。同じ名前があれば上書きします(GUID は保ちます)。", "Where copied materials are saved (path under Assets). Existing files with the same name are updated in place (GUIDs are kept).")), folder);
                if (GUILayout.Button(L("選ぶ", "Browse"), GUILayout.Width(52f)))
                {
                    string picked = EditorUtility.OpenFolderPanel(L("保存先フォルダ", "Output Folder"), "Assets", "");
                    if (!string.IsNullOrEmpty(picked) && picked.StartsWith(Application.dataPath)) folder = "Assets" + picked.Substring(Application.dataPath.Length);
                }
            }

            // ---- キャラのセットアップ
            using (new EditorGUI.DisabledScope(roots.Count == 0))
            {
                EditorGUILayout.Space(6f);
                EditorGUILayout.LabelField(L($"キャラのセットアップ ({roots.Count} 体)", $"Character Setup ({roots.Count})"), EditorStyles.boldLabel);
                addFace = EditorGUILayout.Toggle(new GUIContent(L("Galaxy Toon Face を付ける", "Add Galaxy Toon Face"), L("顔の陰影・SDF 顔影・髪の影が首の向きについてくるようにします。", "Makes face shading, SDF face shadows, and hair shadows follow the head.")), addFace);
                findEyes = EditorGUILayout.Toggle(new GUIContent(L("瞳と頬の位置を探す", "Find Eyes and Cheeks"), L("「瞳の範囲を探す」「頬の位置を探す」と同じ処理です。", "Same as \"Find Eye Range\" and \"Find Cheek Position\".")), findEyes);
                bakeOutline = EditorGUILayout.Toggle(new GUIContent(L("輪郭線のなめらか法線を焼く", "Bake Smooth Outline Normals"), L("角で輪郭線が割れないようにします。焼いたメッシュは OutlineNormals フォルダに保存され、元の FBX / VRM は変わりません。", "Prevents the outline from splitting at hard edges. Baked meshes are saved to an OutlineNormals folder; the original FBX / VRM is not changed.")), bakeOutline);
                hairShadow = EditorGUILayout.Toggle(new GUIContent(L("髪の影を設定する", "Set Up Hair Shadow"), L("髪に「髪の影を落とす」、顔と瞳に「髪の影を受ける」を当て、URP Renderer に髪の影の機能を追加します。", "Applies \"Cast Hair Shadow\" to hair and \"Receive Hair Shadow\" to the face and irises, and adds the hair shadow feature to the URP Renderer.")), hairShadow);
            }

            EditorGUILayout.Space(8f);
            int count = 0;
            foreach (Entry e in entries) if (e.include && e.kind != SourceKind.GalaxyToon) count++;
            using (new EditorGUI.DisabledScope(count == 0))
                if (GUILayout.Button(L($"移行する ({count} 個)", $"Migrate ({count})"), GUILayout.Height(30f))) Run();
            if (!string.IsNullOrEmpty(message)) EditorGUILayout.HelpBox(message, MessageType.None);
        }

        private static string KindName(SourceKind k)
        {
            switch (k)
            {
                case SourceKind.MToon: return "MToon (VRM 0.x)";
                case SourceKind.MToon10: return "MToon (VRM 1.0)";
                case SourceKind.LilToon: return "lilToon";
                case SourceKind.Uts: return "UTS";
                case SourceKind.UrpLit: return "URP Lit";
                case SourceKind.UrpUnlit: return "URP Unlit";
                case SourceKind.Standard: return "Standard";
                case SourceKind.GalaxyToon: return L("移行済み", "Done");
                default: return L("その他", "Other");
            }
        }

        // ---------------------------------------------------------------- 選択からマテリアルを集める

        private void Collect()
        {
            var old = new Dictionary<Material, Entry>();
            foreach (Entry e in entries) old[e.material] = e;
            entries.Clear();
            roots.Clear();
            var byMaterial = new Dictionary<Material, Entry>();

            Entry Add(Material m, string rendererName)
            {
                if (m == null) return null;
                if (!byMaterial.TryGetValue(m, out Entry e))
                {
                    e = new Entry { material = m, kind = Detect(m) };
                    e.part = GuessPart(m.name);
                    if (e.part == PartNone && rendererName != null) e.part = GuessPart(rendererName);
                    if (old.TryGetValue(m, out Entry prev)) { e.include = prev.include; e.part = prev.part; } // 手で変えた部位は保つ
                    byMaterial[m] = e;
                    entries.Add(e);
                }
                return e;
            }

            foreach (GameObject go in Selection.gameObjects)
            {
                if (EditorUtility.IsPersistent(go)) continue; // Project のプレハブは対象外(シーンに置いて実行)
                GameObject root = CharacterRoot(go);
                if (!roots.Contains(root)) roots.Add(root);
                foreach (Renderer r in go.GetComponentsInChildren<Renderer>(true))
                {
                    if (r is ParticleSystemRenderer || r is TrailRenderer || r is LineRenderer) continue;
                    Material[] mats = r.sharedMaterials;
                    for (int i = 0; i < mats.Length; i++) Add(mats[i], r.name)?.users.Add((r, i));
                }
            }
            foreach (Object o in Selection.objects) if (o is Material m) Add(m, null);

            if (string.IsNullOrEmpty(folder)) folder = DefaultFolder();
            message = null;
        }

        private static GameObject CharacterRoot(GameObject go)
        {
            Animator a = go.GetComponentInParent<Animator>(true);
            return a != null ? a.gameObject : go.transform.root.gameObject;
        }

        private string DefaultFolder()
        {
            foreach (GameObject root in roots)
            {
                string prefab = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(root);
                if (!string.IsNullOrEmpty(prefab) && prefab.StartsWith("Assets/")) return Path.GetDirectoryName(prefab).Replace('\\', '/') + "/GalaxyToon";
            }
            foreach (Entry e in entries)
            {
                string path = AssetDatabase.GetAssetPath(e.material);
                if (path.StartsWith("Assets/")) return Path.GetDirectoryName(path).Replace('\\', '/') + "/GalaxyToon";
            }
            return "Assets/GalaxyToonMigrated";
        }

        public static SourceKind Detect(Material m)
        {
            string s = m.shader != null ? m.shader.name : "";
            string n = s.ToLowerInvariant();
            if (s.StartsWith("GalaxyShader/")) return SourceKind.GalaxyToon;
            if (n.Contains("mtoon10")) return SourceKind.MToon10;
            if (n.Contains("mtoon")) return SourceKind.MToon;
            if (n.Contains("liltoon") || n.Contains("ltspass") || n.StartsWith("_lil/")) return SourceKind.LilToon;
            if (n.Contains("toon") && (m.HasProperty("_1st_ShadeColor") || n.Contains("unitychan"))) return SourceKind.Uts;
            if (n.StartsWith("universal render pipeline/unlit")) return SourceKind.UrpUnlit;
            if (n.StartsWith("universal render pipeline/")) return SourceKind.UrpLit;
            if (s == "Standard" || s == "Standard (Specular setup)") return SourceKind.Standard;
            return SourceKind.Generic;
        }

        /// <summary>名前から部位を推測する。VRoid の名前(Face_00_SKIN・HairBack_00_HAIR・EyeWhite_00_EYE・Tops_01_CLOTH など)にも合わせてある。</summary>
        public static int GuessPart(string name)
        {
            string n = name.ToLowerInvariant();
            bool Any(params string[] words) { foreach (string w in words) if (n.Contains(w)) return true; return false; }
            if (Any("eye", "iris", "pupil", "sclera", "lash", "brow", "mouth", "teeth", "tooth", "tongue", "highlight", "白目", "瞳", "まつ", "眉", "歯", "舌")) return PartFaceParts;
            if (Any("face", "顔")) return PartFace;
            if (Any("hair", "bang", "髪")) return PartHair;
            if (Any("metal", "金属")) return PartMetal;
            if (Any("skin", "body", "肌", "素体", "体")) return PartSkin;
            if (Any("cloth", "tops", "bottoms", "shoe", "shirt", "skirt", "pants", "dress", "jacket", "coat", "sock", "glove", "boot", "accessory", "服", "衣装")) return PartCloth;
            return PartNone;
        }

        // ---------------------------------------------------------------- 移行

        private void Run()
        {
            Shader toon = Shader.Find("GalaxyShader/Toon");
            if (toon == null) { message = L("GalaxyShader/Toon が見つかりません。", "GalaxyShader/Toon was not found."); return; }
            if (!folder.StartsWith("Assets")) { message = L("保存先フォルダは Assets の中にしてください。", "The output folder must be inside Assets."); return; }

            Undo.SetCurrentGroupName(L("GalaxyToon に移行", "Migrate to GalaxyToon"));
            int group = Undo.GetCurrentGroup();
            var log = new StringBuilder();
            var usedPaths = new HashSet<string>();
            int migrated = 0;
            bool anyHair = false;

            try
            {
                for (int i = 0; i < entries.Count; i++)
                {
                    Entry e = entries[i];
                    if (!e.include || e.kind == SourceKind.GalaxyToon) continue;
                    EditorUtility.DisplayProgressBar(L("GalaxyToon に移行", "Migrate to GalaxyToon"), e.material.name, (float)i / entries.Count);

                    var notes = new List<string>();
                    Material fresh = Convert(e.material, e.kind, PartPresets[e.part], hairShadow, notes);
                    anyHair |= e.part == PartHair;

                    Material target;
                    if (overwrite && CanEdit(e.material))
                    {
                        target = e.material;
                        Undo.RecordObject(target, "GalaxyToon に移行");
                        fresh.name = target.name;
                        EditorUtility.CopySerialized(fresh, target);
                    }
                    else
                    {
                        target = SaveCopy(fresh, folder, usedPaths);
                        foreach (var (r, slot) in e.users)
                        {
                            if (r == null) continue;
                            Undo.RecordObject(r, "GalaxyToon に移行");
                            Material[] mats = r.sharedMaterials;
                            if (slot < mats.Length) { mats[slot] = target; r.sharedMaterials = mats; }
                            PrefabUtility.RecordPrefabInstancePropertyModifications(r);
                        }
                    }
                    Object.DestroyImmediate(fresh);
                    EditorUtility.SetDirty(target);
                    migrated++;
                    log.AppendLine($"  {e.material.name} ({KindName(e.kind)}{(e.part > 0 ? " / " + PartNames[e.part] : "")}) → {AssetDatabase.GetAssetPath(target)}{(notes.Count > 0 ? "\n      " + string.Join("\n      ", notes) : "")}");
                }
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }
            AssetDatabase.SaveAssets();

            // ---- キャラのセットアップ(差し替え後のマテリアルに対して)
            foreach (GameObject root in roots)
            {
                if (root == null) continue;
                if (addFace && root.GetComponent<GalaxyToonFace>() == null) Undo.AddComponent<GalaxyToonFace>(root);
                if (findEyes) FindEyesAndCheeks(root, log);
                if (bakeOutline)
                {
                    int baked = GalaxyToonOutlineNormalBaker.Bake(root, folder + "/OutlineNormals");
                    log.AppendLine(L($"  輪郭線: {root.name} の {baked} 個のメッシュになめらか法線を焼きました", $"  Outline: baked smooth normals into {baked} mesh(es) of {root.name}"));
                }
            }
            if (hairShadow && anyHair) GalaxyToonTools.EnableHairShadow();
            AssetDatabase.SaveAssets();
            Undo.CollapseUndoOperations(group);

            message = L($"{migrated} 個のマテリアルを移行しました。詳しくは Console を見てください。", $"Migrated {migrated} material(s). See the Console for details.");
            Debug.Log(L("[GalaxyToon 移行]\n", "[GalaxyToon Migration]\n") + log);
            string done = message;
            Collect(); // 一覧を移行後の状態にする(message が消えるので戻す)
            message = done;
        }

        private static bool CanEdit(Material m)
        {
            string path = AssetDatabase.GetAssetPath(m);
            return path.StartsWith("Assets/") && path.EndsWith(".mat") && AssetDatabase.IsMainAsset(m);
        }

        /// <summary>保存先に .mat として保存する。同じ名前があれば中身を入れ替える(GUID を保つので、ほかからの参照が切れない)。</summary>
        private static Material SaveCopy(Material fresh, string folder, HashSet<string> usedPaths)
        {
            if (!AssetDatabase.IsValidFolder(folder))
            {
                string parent = "Assets";
                foreach (string part in folder.Substring("Assets".Length).Split('/'))
                {
                    if (string.IsNullOrEmpty(part)) continue;
                    if (!AssetDatabase.IsValidFolder(parent + "/" + part)) AssetDatabase.CreateFolder(parent, part);
                    parent += "/" + part;
                }
            }
            string name = fresh.name;
            foreach (char c in Path.GetInvalidFileNameChars()) name = name.Replace(c, '_');
            string path = $"{folder}/{name}.mat";
            for (int i = 2; usedPaths.Contains(path); i++) path = $"{folder}/{name}_{i}.mat"; // 同じ名前の別マテリアル
            usedPaths.Add(path);

            var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing == null) { var copy = new Material(fresh) { name = fresh.name }; AssetDatabase.CreateAsset(copy, path); return copy; }
            Undo.RecordObject(existing, "GalaxyToon に移行");
            fresh.name = existing.name;
            EditorUtility.CopySerialized(fresh, existing);
            return existing;
        }

        private static void FindEyesAndCheeks(GameObject root, StringBuilder log)
        {
            foreach (SkinnedMeshRenderer smr in root.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                if (GalaxyToonEyeFinder.TryFind(smr, out Material face, out Vector4 r1, out Vector4 r2) && face.shader.name == "GalaxyShader/Toon")
                {
                    Undo.RecordObject(face, "瞳の範囲");
                    face.SetVector("_EyeRect1", r1); face.SetVector("_EyeRect2", r2);
                    face.SetFloat("_UseEye", 1f);
                    GalaxyToonGUI.Apply(face);
                    log.AppendLine(L($"  瞳: {face.name} に範囲を入れました", $"  Eyes: set the iris range on {face.name}"));
                }
                if (GalaxyToonEyeFinder.TryFindCheeks(smr, out Material skin, out Vector4 uv, out float size) && skin.shader.name == "GalaxyShader/Toon")
                {
                    Undo.RecordObject(skin, "頬の位置");
                    skin.SetVector("_BlushUV", uv); skin.SetFloat("_BlushSize", size);
                    EditorUtility.SetDirty(skin);
                    log.AppendLine(L($"  頬: {skin.name} に位置を入れました", $"  Cheeks: set the cheek position on {skin.name}"));
                }
            }
        }

        // ---------------------------------------------------------------- 値の写し方

        /// <summary>src の見た目を写した GalaxyShader/Toon のマテリアル(保存前)を作る。プリセット → 元の値 の順に入れるので、元の色・影・輪郭線が優先される。</summary>
        public static Material Convert(Material src, SourceKind kind, string preset, bool hairShadow, List<string> notes)
        {
            var dst = new Material(Shader.Find("GalaxyShader/Toon")) { name = src.name.Replace(" (Instance)", "") }; // VRM から取り込んだマテリアルは名前に (Instance) が付く
            if (!string.IsNullOrEmpty(preset)) GalaxyToonGUI.ApplyPreset(dst, preset);

            switch (kind)
            {
                case SourceKind.MToon:
                case SourceKind.MToon10: FromMToon(src, dst, kind == SourceKind.MToon10, notes); break;
                case SourceKind.LilToon: FromLilToon(src, dst, notes); break;
                case SourceKind.Uts: FromUts(src, dst, notes); break;
                case SourceKind.UrpLit:
                case SourceKind.UrpUnlit:
                case SourceKind.Standard: FromLit(src, dst, kind, string.IsNullOrEmpty(preset)); break;
                default: FromGeneric(src, dst); notes.Add(L("知らないシェーダーなので、色・テクスチャ・ノーマル・発光だけを写しました", "Unknown shader: only color, texture, normal map, and emission were copied")); break;
            }

            if (hairShadow)
            {
                string n = src.name.ToLowerInvariant();
                if (preset == "髪") GalaxyToonGUI.ApplyPreset(dst, "髪の影を落とす");
                else if (preset == "顔" || (preset == "顔パーツ" && (n.Contains("iris") || n.Contains("pupil") || n.Contains("瞳")))) GalaxyToonGUI.ApplyPreset(dst, "髪の影を受ける");
            }
            GalaxyToonGUI.Apply(dst);
            return dst;
        }

        // ---- MToon(VRM 0.x の VRM/MToon、VRM 1.0 の MToon10)
        private static void FromMToon(Material src, Material dst, bool v10, List<string> notes)
        {
            Color baseColor = Col(src, Color.white, "_Color");
            Base(src, dst, "_MainTex", baseColor);
            Normal(src, dst, "_BumpMap", "_BumpScale");

            // 影色: MToon の影色は「地の色に掛ける色」ではなく影の色そのものなので、地の色で割って掛け算の色に直す
            Color shade = Col(src, new Color(0.97f, 0.81f, 0.86f), "_ShadeColor");
            dst.SetColor("_ShadowColor", ShadeRatio(baseColor, shade));
            dst.SetColor("_Shadow2ndColor", new Color(1f, 1f, 1f, 0f));
            Texture main = Tex(src, "_MainTex"), shadeTex = Tex(src, "_ShadeTex", "_ShadeTexture");
            if (shadeTex != null && shadeTex != main) notes.Add(L("影テクスチャ(Shade Texture)はメインテクスチャと違うため写せません。影色は影テクスチャの平均ではなく「影色」から作りました", "The shade texture differs from the main texture and cannot be copied; the shadow color was made from the shade color"));

            // 影の境界: MToon は N・L(-1〜1)が shift から lerp(1, shift, toony) の間で影から光へ移る。GalaxyToon は (N・L)*0.5+0.5 の border を中心に blur の幅
            float shift = Flt(src, v10 ? -0.05f : 0f, "_ShadingShiftFactor", "_ShadeShift");
            float toony = Flt(src, v10 ? 0.95f : 0.9f, "_ShadingToonyFactor", "_ShadeToony");
            float upper = Mathf.Lerp(1f, shift, toony);
            dst.SetFloat("_ShadowBorder", Mathf.Clamp01((shift + upper) * 0.25f + 0.5f));
            dst.SetFloat("_ShadowBlur", Mathf.Clamp01((upper - shift) * 0.5f));

            Emission(src, dst, "_EmissionMap", "_EmissionColor");

            // マットキャップ(加算)
            Texture matcap = Tex(src, "_MatcapTex", "_SphereAdd");
            if (!IsDummy(matcap))
            {
                Color mc = v10 ? Col(src, Color.white, "_MatcapColor") : Color.white;
                if (mc.maxColorComponent > 0f) { dst.SetTexture("_MatCapTex", matcap); dst.SetColor("_MatCapColor", new Color(mc.r, mc.g, mc.b, 1f)); }
            }

            // リム(色が黒なら使っていないので、プリセットのリムを残す)
            Color rim = Col(src, Color.black, "_RimColor");
            if (rim.maxColorComponent > 0.001f)
            {
                dst.SetColor("_RimColor", new Color(rim.r, rim.g, rim.b, 1f));
                dst.SetFloat("_RimFresnelPower", Flt(src, 5f, "_RimFresnelPower"));
            }

            // 輪郭線: 0=なし 1=ワールド 2=画面。VRM 0.x のワールドは cm、VRM 1.0 は m
            int mode = Mathf.RoundToInt(Flt(src, 0f, "_OutlineWidthMode"));
            float width = Flt(src, 0f, "_OutlineWidth");
            if (mode == 0 || width <= 0f) dst.SetFloat("_OutlineWidth", 0f);
            else
            {
                dst.SetFloat("_OutlineWidth", Mathf.Clamp(v10 ? width * 100f : width, 0f, 1f));
                dst.SetFloat("_AnimeOutlinePixel", mode == 2 ? 1f : 0f);
                if (mode == 2) notes.Add(L("輪郭線の「画面の太さ」は近い見た目に換算しました。太さを確かめてください", "Screen-space outline width was approximated; check the thickness"));
                dst.SetColor("_OutlineColor", Col(src, Color.black, "_OutlineColor"));
                dst.SetFloat("_OutlineTexBlend", 0f);
                dst.SetFloat("_OutlineLit", Flt(src, 1f, "_OutlineLightingMix"));
                Texture widthTex = Tex(src, "_OutlineWidthTex", "_OutlineWidthTexture");
                if (widthTex != null) dst.SetTexture("_OutlineWidthMask", widthTex);
            }

            // 描画モード・面
            int blend = v10 ? Mathf.RoundToInt(Flt(src, 0f, "_AlphaMode")) : Mathf.RoundToInt(Flt(src, 0f, "_BlendMode"));
            dst.SetFloat("_RenderMode", blend == 0 ? 0f : blend == 1 ? 1f : 2f);
            dst.SetFloat("_Cutoff", Flt(src, 0.5f, "_Cutoff"));
            if (v10) dst.SetFloat("_Cull", Flt(src, 0f, "_DoubleSided") > 0.5f ? 0f : 2f);
            else dst.SetFloat("_Cull", Flt(src, 2f, "_CullMode"));
            if (v10 && src.HasProperty("_RenderQueueOffset")) dst.SetFloat("_QueueOffset", Mathf.Clamp(src.GetFloat("_RenderQueueOffset"), -5f, 5f));
        }

        // ---- lilToon(GalaxyToon と同じ名前・同じ意味の項目が多いので、そのまま写せる)
        private static void FromLilToon(Material src, Material dst, List<string> notes)
        {
            Color baseColor = Col(src, Color.white, "_Color");
            Base(src, dst, "_MainTex", baseColor);
            if (Flt(src, 1f, "_UseBumpMap") > 0.5f) Normal(src, dst, "_BumpMap", "_BumpScale");

            if (Flt(src, 0f, "_UseShadow") > 0.5f)
            {
                float strength = Flt(src, 1f, "_ShadowStrength");
                Color s1 = Col(src, dst.GetColor("_ShadowColor"), "_ShadowColor"); s1.a *= strength;
                dst.SetColor("_ShadowColor", s1);
                Copy(src, dst, "_ShadowBorder", "_ShadowBlur", "_Shadow2ndColor", "_Shadow2ndBorder", "_Shadow2ndBlur", "_Shadow3rdColor", "_Shadow3rdBorder", "_Shadow3rdBlur", "_ShadowStrengthMask");
            }
            else { dst.SetColor("_ShadowColor", new Color(1f, 1f, 1f, 0f)); dst.SetColor("_Shadow2ndColor", new Color(1f, 1f, 1f, 0f)); }

            if (Flt(src, 0f, "_UseRim") > 0.5f) Copy(src, dst, "_RimColor", "_RimBorder", "_RimBlur", "_RimFresnelPower");
            if (Flt(src, 0f, "_UseMatCap") > 0.5f && !IsDummy(Tex(src, "_MatCapTex"))) Copy(src, dst, "_MatCapTex", "_MatCapColor");
            if (Flt(src, 0f, "_UseEmission") > 0.5f) Emission(src, dst, "_EmissionMap", "_EmissionColor");
            else dst.SetColor("_EmissionColor", Color.black);

            string shader = src.shader.name.ToLowerInvariant();
            if (shader.Contains("outline")) Copy(src, dst, "_OutlineWidth", "_OutlineColor", "_OutlineWidthMask");
            else dst.SetFloat("_OutlineWidth", 0f);

            // 描画モード: _TransparentMode(0=不透明 1=カットアウト 2=半透明…)。無ければシェーダー名
            float tm = src.HasProperty("_TransparentMode") ? src.GetFloat("_TransparentMode") : shader.Contains("cutout") ? 1f : shader.Contains("trans") ? 2f : 0f;
            dst.SetFloat("_RenderMode", tm < 0.5f ? 0f : tm < 1.5f ? 1f : 2f);
            if (tm > 2.5f) notes.Add(L("lilToon の屈折・ファーなどの特殊な描画モードは、半透明にしました", "lilToon special render modes (refraction, fur, etc.) were converted to Transparent"));
            Copy(src, dst, "_Cutoff", "_Cull");
        }

        // ---- UTS(Unity-Chan Toon Shader)
        private static void FromUts(Material src, Material dst, List<string> notes)
        {
            Color baseColor = Col(src, Color.white, "_BaseColor", "_Color");
            Base(src, dst, "_MainTex", baseColor);
            Normal(src, dst, "_NormalMap", "_BumpScale");

            dst.SetColor("_ShadowColor", ShadeRatio(baseColor, Col(src, baseColor, "_1st_ShadeColor")));
            dst.SetFloat("_ShadowBorder", Flt(src, 0.5f, "_BaseColor_Step"));
            dst.SetFloat("_ShadowBlur", Flt(src, 0.0001f, "_BaseShade_Feather"));
            if (src.HasProperty("_2nd_ShadeColor"))
            {
                Color s2 = ShadeRatio(baseColor, src.GetColor("_2nd_ShadeColor"));
                dst.SetColor("_Shadow2ndColor", s2);
                dst.SetFloat("_Shadow2ndBorder", Flt(src, 0f, "_ShadeColor_Step"));
                dst.SetFloat("_Shadow2ndBlur", Flt(src, 0.0001f, "_1st2nd_Shades_Feather"));
            }
            if (Tex(src, "_1st_ShadeMap") is Texture t1 && t1 != Tex(src, "_MainTex")) notes.Add(L("1影のテクスチャはメインテクスチャと違うため写せません", "The 1st shade map differs from the main texture and cannot be copied"));

            if (Flt(src, 0f, "_RimLight") > 0.5f) { Color rim = Col(src, Color.white, "_RimLightColor"); dst.SetColor("_RimColor", new Color(rim.r, rim.g, rim.b, 1f)); }
            if (Flt(src, 0f, "_MatCap") > 0.5f && Tex(src, "_MatCap_Sampler") is Texture mc && !IsDummy(mc)) { dst.SetTexture("_MatCapTex", mc); dst.SetColor("_MatCapColor", Col(src, Color.white, "_MatCapColor")); }
            Emission(src, dst, "_Emissive_Tex", "_Emissive_Color");

            // 輪郭線: UTS の太さは 0.001m 単位 → cm
            if (src.HasProperty("_Outline_Width"))
            {
                dst.SetFloat("_OutlineWidth", Mathf.Clamp(src.GetFloat("_Outline_Width") * 0.1f, 0f, 1f));
                dst.SetColor("_OutlineColor", Col(src, Color.black, "_Outline_Color"));
            }
            string shader = src.shader.name.ToLowerInvariant();
            bool clip = Flt(src, 0f, "_ClippingMode") > 0.5f || shader.Contains("clipping");
            dst.SetFloat("_RenderMode", shader.Contains("trans") ? 2f : clip ? 1f : 0f);
            dst.SetFloat("_Cutoff", Flt(src, 0.5f, "_Clipping_Level", "_Cutoff"));
            dst.SetFloat("_Cull", Flt(src, 2f, "_CullMode"));
        }

        // ---- URP Lit / Simple Lit / Unlit、Built-in の Standard
        private static void FromLit(Material src, Material dst, SourceKind kind, bool copyGloss)
        {
            bool standard = kind == SourceKind.Standard;
            Color baseColor = Col(src, Color.white, standard ? "_Color" : "_BaseColor");
            Base(src, dst, standard ? "_MainTex" : "_BaseMap", baseColor);
            Normal(src, dst, "_BumpMap", "_BumpScale");
            if (Tex(src, "_OcclusionMap") is Texture ao) { dst.SetTexture("_OcclusionMap", ao); dst.SetFloat("_OcclusionStrength", Flt(src, 1f, "_OcclusionStrength")); }
            // 金属・つや: キャラのプリセットを当てたときは写さない(URP Lit の既定のつや 0.5 で、トゥーンの肌・服に映り込みが出てしまうため)
            Texture gloss = Tex(src, "_MetallicGlossMap");
            if (copyGloss || gloss != null)
            {
                dst.SetFloat("_Metallic", Flt(src, 0f, "_Metallic"));
                dst.SetFloat("_Smoothness", Flt(src, 0f, "_Smoothness", "_Glossiness"));
                if (gloss != null) dst.SetTexture("_MetallicGlossMap", gloss);
            }
            if (kind == SourceKind.UrpUnlit) { dst.SetFloat("_LightMinLimit", 1f); dst.SetColor("_ShadowColor", new Color(1f, 1f, 1f, 0f)); }
            if (src.IsKeywordEnabled("_EMISSION")) Emission(src, dst, "_EmissionMap", "_EmissionColor");
            else dst.SetColor("_EmissionColor", Color.black);

            float mode;
            if (standard) { float m = Flt(src, 0f, "_Mode"); mode = m < 0.5f ? 0f : m < 1.5f ? 1f : 2f; }
            else mode = Flt(src, 0f, "_Surface") > 0.5f ? 2f : Flt(src, 0f, "_AlphaClip") > 0.5f ? 1f : 0f;
            dst.SetFloat("_RenderMode", mode);
            Copy(src, dst, "_Cutoff", "_Cull");
        }

        // ---- そのほか: 名前のよくある項目だけ
        private static void FromGeneric(Material src, Material dst)
        {
            Color baseColor = Col(src, Color.white, "_BaseColor", "_Color");
            Base(src, dst, src.HasProperty("_BaseMap") ? "_BaseMap" : "_MainTex", baseColor);
            Normal(src, dst, "_BumpMap", "_BumpScale");
            Emission(src, dst, "_EmissionMap", "_EmissionColor");
            int queue = src.renderQueue;
            dst.SetFloat("_RenderMode", queue >= 2900 ? 2f : queue >= 2450 ? 1f : 0f);
            dst.SetFloat("_Cutoff", Flt(src, 0.5f, "_Cutoff"));
            dst.SetFloat("_Cull", Flt(src, 2f, "_Cull", "_CullMode"));
        }

        // ---------------------------------------------------------------- 小さな道具

        private static void Base(Material src, Material dst, string texName, Color color)
        {
            dst.SetColor("_BaseColor", color);
            if (!src.HasProperty(texName)) return;
            dst.SetTexture("_BaseMap", src.GetTexture(texName));
            dst.SetTextureScale("_BaseMap", src.GetTextureScale(texName));
            dst.SetTextureOffset("_BaseMap", src.GetTextureOffset(texName));
        }

        private static void Normal(Material src, Material dst, string texName, string scaleName)
        {
            Texture n = Tex(src, texName);
            float scale = Mathf.Clamp(Flt(src, 1f, scaleName), 0f, 2f);
            if (IsDummy(n) || scale <= 0f) return; // 強さ 0 や平らなダミーを入れると、効かないノーマルマップの計算だけが増える
            dst.SetTexture("_BumpMap", n);
            dst.SetFloat("_BumpScale", scale);
        }

        private static void Emission(Material src, Material dst, string texName, string colorName)
        {
            Color c = Col(src, Color.black, colorName);
            dst.SetColor("_EmissionColor", c);
            if (c.maxColorComponent > 0f && Tex(src, texName) is Texture t) dst.SetTexture("_EmissionMap", t);
        }

        /// <summary>中身の無い小さなテクスチャか(VRoid は使わない項目にも 8x8 のマットキャップ・平らなノーマルを入れる)。写すと、効かない機能の計算が増える。</summary>
        private static bool IsDummy(Texture t) => t == null || (t.width <= 16 && t.height <= 16);

        /// <summary>影の色そのもの(shade)を、地の色(base)に掛ける色に直す。</summary>
        private static Color ShadeRatio(Color baseColor, Color shade)
        {
            float R(float s, float b) => Mathf.Clamp01(b > 0.01f ? s / b : s);
            return new Color(R(shade.r, baseColor.r), R(shade.g, baseColor.g), R(shade.b, baseColor.b), 1f);
        }

        /// <summary>同じ名前・同じ種類の項目をそのまま写す。</summary>
        private static void Copy(Material src, Material dst, params string[] names)
        {
            Shader s = dst.shader;
            foreach (string n in names)
            {
                if (!src.HasProperty(n) || !dst.HasProperty(n)) continue;
                int i = s.FindPropertyIndex(n);
                switch (s.GetPropertyType(i))
                {
                    case UnityEngine.Rendering.ShaderPropertyType.Texture: dst.SetTexture(n, src.GetTexture(n)); break;
                    case UnityEngine.Rendering.ShaderPropertyType.Color: dst.SetColor(n, src.GetColor(n)); break;
                    case UnityEngine.Rendering.ShaderPropertyType.Vector: dst.SetVector(n, src.GetVector(n)); break;
                    default: dst.SetFloat(n, src.GetFloat(n)); break;
                }
            }
        }

        private static Texture Tex(Material m, params string[] names)
        {
            foreach (string n in names) if (m.HasProperty(n) && m.GetTexture(n) != null) return m.GetTexture(n);
            return null;
        }

        private static Color Col(Material m, Color fallback, params string[] names)
        {
            foreach (string n in names) if (m.HasProperty(n)) return m.GetColor(n);
            return fallback;
        }

        private static float Flt(Material m, float fallback, params string[] names)
        {
            foreach (string n in names) if (m.HasProperty(n)) return m.GetFloat(n);
            return fallback;
        }
    }
}
