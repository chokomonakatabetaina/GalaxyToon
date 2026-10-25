using System.Collections.Generic;
using UnityEngine;

namespace GalaxyToon
{
    /// <summary>サンプルシーン用: 「前へ / 次へ」で見本を1つずつ寄って見せる(←→ キー、Home で全体)。カメラに付ける。</summary>
    [AddComponentMenu("GalaxyToon/Samples/Galaxy Toon Sample Viewer")]
    [RequireComponent(typeof(Camera))]
    public sealed class GalaxyToonSampleViewer : MonoBehaviour
    {
        [System.Serializable]
        public struct Entry { public Transform target; public string title; }

        public List<Entry> entries = new List<Entry>();
        [Tooltip("寄ったときの見本からの距離(m)。")] public float distance = 2.4f;
        [Tooltip("カメラが動く速さ。")] public float speed = 6f;

        private int index = -1; // -1 = 全体
        private Vector3 overview, goal;
        private GUIStyle titleStyle, buttonStyle;

        private void Start() => overview = goal = transform.position;

        private void LateUpdate() => transform.position = Vector3.Lerp(transform.position, goal, 1f - Mathf.Exp(-speed * Time.deltaTime));

        private void Show(int i)
        {
            if (entries.Count == 0) return;
            index = i < -1 ? entries.Count - 1 : i >= entries.Count ? -1 : i; // 端まで行ったら全体を挟んで一周
            goal = index < 0 || entries[index].target == null ? overview : entries[index].target.position + new Vector3(0f, -0.15f, -distance);
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
            titleStyle ??= new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold };
            buttonStyle ??= new GUIStyle(GUI.skin.button);
            titleStyle.fontSize = Mathf.RoundToInt(40 * scale);
            buttonStyle.fontSize = Mathf.RoundToInt(26 * scale);

            string title = index < 0 ? "GalaxyToon  全体 / Overview" : $"{index + 1} / {entries.Count}   {entries[index].title}";
            GUI.Label(new Rect(0f, 20f * scale, Screen.width, 60f * scale), title.Replace("\n", "  "), titleStyle);

            float w = 220f * scale, h = 64f * scale, y = Screen.height - h - 24f * scale, cx = Screen.width * 0.5f;
            if (GUI.Button(new Rect(cx - w * 1.5f - 16f * scale, y, w, h), "◀ 前へ / Prev", buttonStyle)) Show(index - 1);
            if (GUI.Button(new Rect(cx - w * 0.5f, y, w, h), "全体 / All", buttonStyle)) Show(-1);
            if (GUI.Button(new Rect(cx + w * 0.5f + 16f * scale, y, w, h), "次へ / Next ▶", buttonStyle)) Show(index + 1);
        }
    }
}
