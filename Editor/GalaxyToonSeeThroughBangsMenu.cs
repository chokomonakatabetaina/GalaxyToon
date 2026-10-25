using UnityEditor;
using UnityEngine;

namespace GalaxyToon.Editor
{
    /// <summary>前髪透過(GalaxyToonSeeThroughBangs)をキャラに付ける。マテリアルのプリセットではキャラにコンポーネントを付けられないので、メニューから付ける。</summary>
    public static class GalaxyToonSeeThroughBangsMenu
    {
        private const string Menu = "Tools/GalaxyToon/前髪を透過する (選択中のキャラ)";

        [MenuItem(Menu)]
        public static void Add()
        {
            int added = 0;
            foreach (var go in Selection.gameObjects)
            {
                // 選んだ子からでも、Humanoid の Animator があるキャラのルートに付ける
                var animator = go.GetComponentInParent<Animator>();
                if (animator == null || !animator.isHuman) { Debug.LogWarning($"[前髪透過] {go.name}: Humanoid の Animator がありません", go); continue; }
                var root = animator.gameObject;
                if (root.GetComponent<GalaxyToonSeeThroughBangs>() != null) { Debug.Log($"[前髪透過] {root.name}: もう付いています", root); continue; }
                bool hasHair = false;
                foreach (var r in root.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                    foreach (var m in r.sharedMaterials)
                        hasHair |= m != null && m.name.ToLowerInvariant().Contains("hair") && m.HasProperty("_BangFadeMode");
                if (!hasHair) { Debug.LogWarning($"[前髪透過] {root.name}: GalaxyShader/Toon の髪マテリアル(名前に hair)がありません", root); continue; }
                Undo.AddComponent<GalaxyToonSeeThroughBangs>(root);
                if (PrefabUtility.IsPartOfPrefabAsset(root)) PrefabUtility.SavePrefabAsset(root.transform.root.gameObject);
                added++;
            }
            Debug.Log($"[前髪透過] {added} 体に付けました。再生中に前髪の毛先が透けます(透ける範囲はコンポーネントで調整)。");
        }

        [MenuItem(Menu, true)]
        private static bool Validate() => Selection.gameObjects.Length > 0;
    }
}
