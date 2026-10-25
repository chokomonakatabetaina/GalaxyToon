using System.Collections.Generic;
using UnityEngine;

namespace GalaxyToon
{
    /// <summary>髪の揺れ(ボーン無しの髪用)。頭の下にある「毛先の点」をバネで追いかけさせ、その遅れを GalaxyShader/Toon に渡す。
    /// シェーダが毛先ほど大きくずらすので、首を振る・歩く・止まるで髪がふわっと遅れてついてくる。
    /// キャラのルートに付けるだけ。対象は「髪の揺れ」が 0 より大きいマテリアル。</summary>
    [AddComponentMenu("GalaxyToon/Galaxy Toon Hair Sway")]
    public sealed class GalaxyToonHairSway : MonoBehaviour
    {
        [Tooltip("頭ボーン。空なら自動で探す。")]
        public Transform head;
        [Tooltip("揺れの支点(頭ボーンからの高さ m)。これより下ほど揺れる。")]
        public float pivotHeight = 0.15f;
        [Tooltip("支点から毛先までの長さ(m)。")]
        public float hairLength = 0.3f;
        [Tooltip("元に戻る強さ。大きいほど硬く、すぐ戻る。")]
        public float stiffness = 60f;
        [Range(0f, 1f), Tooltip("揺れの減衰。大きいほどすぐ止まる。")]
        public float damping = 0.35f;
        [Tooltip("遅れの最大(m)。暴れ防止。")]
        public float maxOffset = 0.06f;
        [Tooltip("止まっているときのそよぎ(m)。0 で無し。")]
        public float idleSway = 0.004f;

        private static readonly int StrengthId = Shader.PropertyToID("_HairSway");
        private static readonly int OffsetId = Shader.PropertyToID("_HairSwayOffset");
        private static readonly int PivotId = Shader.PropertyToID("_HairSwayPivot");
        private static readonly int UpId = Shader.PropertyToID("_HairSwayUp");

        private readonly List<Renderer> targets = new List<Renderer>();
        private MaterialPropertyBlock block;
        private Vector3 tip, velocity;
        private bool started;

        private void OnEnable()
        {
            if (head == null)
            {
                var animator = GetComponentInChildren<Animator>();
                if (animator != null && animator.isHuman) head = animator.GetBoneTransform(HumanBodyBones.Head);
                if (head == null)
                    foreach (Transform t in GetComponentsInChildren<Transform>(true))
                        if (t.name.EndsWith("Head") && t.GetComponent<Renderer>() == null) { head = t; break; }
            }
            targets.Clear();
            foreach (Renderer r in GetComponentsInChildren<Renderer>(true))
                foreach (Material m in r.sharedMaterials)
                    if (m != null && m.HasProperty(StrengthId) && m.GetFloat(StrengthId) > 0f) { targets.Add(r); break; }
            block ??= new MaterialPropertyBlock();
            started = false;
        }

        private Vector3 Up => transform.up; // 頭の上向き = キャラの上(頭ボーンの軸は FBX によって違うので)

        private Vector3 Anchor => head.position + Up * (pivotHeight - hairLength);

        private void LateUpdate()
        {
            if (head == null || targets.Count == 0) return;
            float dt = Mathf.Min(Time.deltaTime, 1f / 30f);
            Vector3 anchor = Anchor;
            if (!started || (tip - anchor).sqrMagnitude > 1f) { tip = anchor; velocity = Vector3.zero; started = true; } // ワープしたら追いつかせる
            if (dt > 0f)
            {
                velocity += (anchor - tip) * (stiffness * dt);
                velocity *= Mathf.Pow(1f - damping, dt * 10f);
                tip += velocity * dt;
            }
            Vector3 offset = Vector3.ClampMagnitude(tip - anchor, maxOffset);
            if (idleSway > 0f) offset += transform.right * (Mathf.Sin(Time.time * 1.3f) * idleSway) + transform.forward * (Mathf.Sin(Time.time * 0.9f + 1f) * idleSway * 0.5f);

            Vector3 pivot = head.position + Up * pivotHeight;
            foreach (Renderer r in targets)
            {
                if (r == null) continue;
                r.GetPropertyBlock(block);
                block.SetVector(OffsetId, offset);
                block.SetVector(PivotId, new Vector4(pivot.x, pivot.y, pivot.z, hairLength));
                block.SetVector(UpId, Up);
                r.SetPropertyBlock(block);
            }
        }
    }
}
