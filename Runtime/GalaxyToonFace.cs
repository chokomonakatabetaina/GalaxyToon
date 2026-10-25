using System.Collections.Generic;
using UnityEngine;

namespace GalaxyToon
{
    /// <summary>GalaxyShader/Toon の「顔の陰影を平らに」用。頭ボーンの向きを顔のマテリアルへ毎フレーム渡す(首を振っても顔の陰影がついてくる)。
    /// キャラのルートに付けるだけでよい: 頭ボーンは Animator(Humanoid)か名前(Head)から、顔は「顔の陰影を平らに」が 0 より大きいか、SDF 影マップを首の向きに合わせるマテリアルを持つ Renderer から探す。</summary>
    [AddComponentMenu("GalaxyToon/Galaxy Toon Face")]
    [ExecuteAlways] // エディタ上でも顔の陰影を正しく見せる
    public sealed class GalaxyToonFace : MonoBehaviour
    {
        [Tooltip("頭ボーン。空なら自動で探す。")]
        public Transform head;
        [Tooltip("顔の Renderer。空なら自動で探す。")]
        public List<Renderer> faceRenderers = new List<Renderer>();
        [Tooltip("頭ボーンから頭の中心までのずれ(m、キャラの向きで。上・前)。「顔を球として塗る」の球の中心。")]
        public Vector2 centerOffset = new Vector2(0.09f, 0.01f);

        private static readonly int FaceForwardId = Shader.PropertyToID("_FaceForward");
        private static readonly int FaceRightId = Shader.PropertyToID("_FaceRight");
        private static readonly int FaceCenterId = Shader.PropertyToID("_FaceCenter");
        private Vector3 rightInHead, upInHead;
        private readonly List<Renderer> bodyRenderers = new List<Renderer>(); // SDF を体の向きで使う Renderer(体など)
        private static readonly int UseFaceSdfId = Shader.PropertyToID("_UseFaceSdf");
        private static readonly int FaceShadingId = Shader.PropertyToID("_FaceShading");
        private static readonly int SdfFollowHeadId = Shader.PropertyToID("_SdfFollowHead"); // SDF 影マップ(顔・髪)も首の向きに合わせる
        private Vector3 forwardInHead; // 頭ボーンの空間での「顔の正面」(キャラの正面を、最初の姿勢で頭の空間へ写したもの)
        private MaterialPropertyBlock block;

        private void Start() => Setup();

        /// <summary>有効な顔(キャラ)。空なら髪の影の描画(GalaxyHairShadow)を丸ごと省く(キャラがいない場面の負荷を 0 に)。</summary>
        public static readonly List<GalaxyToonFace> Instances = new List<GalaxyToonFace>();
        public static int ActiveCount => Instances.Count;
        private void OnEnable() { if (!Instances.Contains(this)) Instances.Add(this); }
        private void OnDisable() => Instances.Remove(this);

        /// <summary>頭ボーン・対象の Renderer を探し、向きの基準を取る。スクリプトの再読み込みで中身が消えたとき(エディタ)は LateUpdate から呼び直す。</summary>
        private void Setup()
        {
            if (head == null)
            {
                var animator = GetComponentInChildren<Animator>();
                if (animator != null && animator.isHuman) head = animator.GetBoneTransform(HumanBodyBones.Head);
                if (head == null)
                    foreach (Transform t in GetComponentsInChildren<Transform>(true))
                        if (t.name.EndsWith("Head") && t.GetComponent<Renderer>() == null) { head = t; break; } // Swap_Head などのメッシュは除く
            }
            if (faceRenderers.Count == 0)
                foreach (Renderer r in GetComponentsInChildren<Renderer>(true))
                    foreach (Material m in r.sharedMaterials)
                        if (m != null && m.HasProperty(FaceShadingId) && (m.GetFloat(FaceShadingId) > 0f || m.GetFloat(SdfFollowHeadId) > 0f)) { faceRenderers.Add(r); break; }
            bodyRenderers.Clear();
            foreach (Renderer r in GetComponentsInChildren<Renderer>(true))
            {
                if (faceRenderers.Contains(r)) continue;
                foreach (Material m in r.sharedMaterials)
                    if (m != null && m.HasProperty(UseFaceSdfId) && m.GetFloat(UseFaceSdfId) > 0f) { bodyRenderers.Add(r); break; }
            }
            if (head == null || faceRenderers.Count + bodyRenderers.Count == 0) { enabled = false; return; }
            forwardInHead = head.InverseTransformDirection(transform.forward);
            rightInHead = head.InverseTransformDirection(transform.right);
            upInHead = head.InverseTransformDirection(transform.up);
            block = new MaterialPropertyBlock();
        }

        private void LateUpdate()
        {
            if (block == null) { Setup(); if (!enabled || block == null) return; }
            Vector3 forward = head.TransformDirection(forwardInHead).normalized;
            Vector3 right = head.TransformDirection(rightInHead).normalized;
            Vector3 up = head.TransformDirection(upInHead).normalized;
            float scale = transform.lossyScale.y;
            Vector3 center = head.position + (up * centerOffset.x + forward * centerOffset.y) * scale;
            foreach (Renderer r in faceRenderers)
            {
                if (r == null) continue;
                r.GetPropertyBlock(block);
                block.SetVector(FaceForwardId, forward);
                block.SetVector(FaceRightId, right);
                block.SetVector(FaceCenterId, new Vector4(center.x, center.y, center.z, 1f));
                r.SetPropertyBlock(block);
            }
            // 体の SDF はキャラの正面基準(FBX の軸の違いで、モデル行列の +Z が正面とは限らないので渡す)
            foreach (Renderer r in bodyRenderers)
            {
                if (r == null) continue;
                r.GetPropertyBlock(block);
                block.SetVector(FaceForwardId, transform.forward);
                block.SetVector(FaceRightId, transform.right);
                r.SetPropertyBlock(block);
            }
        }
    }
}
