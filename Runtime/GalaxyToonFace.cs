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
        private static readonly int HeadForwardId = Shader.PropertyToID("_HeadForward");
        private static readonly int AmbientHeadSplitId = Shader.PropertyToID("_AmbientHeadSplit");
        // 環境光を頭の向きで分けるマテリアルを持つ Renderer(頭の前の向きを入れる)
        private readonly List<Renderer> headForwardRenderers = new List<Renderer>();
        private static readonly int FaceRightId = Shader.PropertyToID("_FaceRight");
        private static readonly int FaceUpId = Shader.PropertyToID("_FaceUp");
        private static readonly int FaceCenterId = Shader.PropertyToID("_FaceCenter");
        private Vector3 rightInHead, upInHead;
        private readonly List<Renderer> bodyRenderers = new List<Renderer>(); // SDF を体の向きで使う Renderer(体など)
        private static readonly int UseFaceSdfId = Shader.PropertyToID("_UseFaceSdf");
        private static readonly int FaceShadingId = Shader.PropertyToID("_FaceShading");
        private static readonly int SdfFollowHeadId = Shader.PropertyToID("_SdfFollowHead"); // SDF 影マップ(顔・髪)も首の向きに合わせる
        private Vector3 forwardInHead; // 頭ボーンの空間での「顔の正面」(キャラの正面を、最初の姿勢で頭の空間へ写したもの)
        private MaterialPropertyBlock block;

        // 高さの毛先(_TipAxis = 2)のマテリアルを持つ Renderer。マテリアルは途中で変わるので、ときどき探し直す
        private static readonly int TipAxisId = Shader.PropertyToID("_TipAxis");
        private static readonly int TipColorId = Shader.PropertyToID("_TipColor");
        private static readonly int TipRestRowId = Shader.PropertyToID("_TipRestRow");
        private readonly List<Renderer> tipRenderers = new List<Renderer>();
        // 遠近の除去(_PerspectiveRemoval > 0)のマテリアルを持つ Renderer。キャラの全部に同じ中心(頭の中心)を入れる(顔と髪がずれない)
        private static readonly int PerspectiveRemovalId = Shader.PropertyToID("_PerspectiveRemoval");
        private static readonly int PerspectiveCenterId = Shader.PropertyToID("_PerspectiveCenter");
        private readonly List<Renderer> perspectiveRenderers = new List<Renderer>();
        private float nextTipScan;
        private MaterialPropertyBlock tipBlock;

        /// <summary>
        /// 高さの毛先に使う、ワールドの位置 → 最初の姿勢(バインドポーズ)でのメッシュの高さ、の行。頭の動き(上下・回転)を戻すので、しゃがむ・寝ても毛先の位置が変わらない。
        /// Renderer のボーンに頭があればそれ、無ければ頭のすぐ下の(いちばん浅い)子孫のボーンで戻す(頭と一緒に動く。揺れるボーンの先は使わない)。どちらも無ければ 0(物体の高さを使う)。
        /// </summary>
        public static Vector4 TipRestRow(Renderer renderer, Transform head)
        {
            if (!(renderer is SkinnedMeshRenderer skinned) || skinned.sharedMesh == null || head == null) return Vector4.zero;
            var bones = skinned.bones;
            var bindposes = skinned.sharedMesh.bindposes;
            var pick = -1;
            var pickDepth = int.MaxValue;
            for (var i = 0; i < bones.Length && i < bindposes.Length; i++)
            {
                var bone = bones[i];
                if (bone == null) continue;
                if (bone == head) { pick = i; break; }
                var depth = 0;
                for (var t = bone; t != null && t != head; t = t.parent) depth++;
                if (!bone.IsChildOf(head) || depth >= pickDepth) continue;
                pick = i;
                pickDepth = depth;
            }
            if (pick < 0) return Vector4.zero;
            Matrix4x4 m = bindposes[pick].inverse * bones[pick].worldToLocalMatrix;
            return m.GetRow(1);
        }

        private void UpdateTipHeights()
        {
            if (Time.unscaledTime >= nextTipScan || !Application.isPlaying)
            {
                nextTipScan = Time.unscaledTime + 1f;
                tipRenderers.Clear();
                perspectiveRenderers.Clear();
                headForwardRenderers.Clear();
                var anyPerspective = false;
                foreach (Renderer r in GetComponentsInChildren<Renderer>(true))
                {
                    foreach (Material m in r.sharedMaterials)
                        if (m != null && m.HasProperty(AmbientHeadSplitId) && m.GetFloat(AmbientHeadSplitId) > 0.001f) { headForwardRenderers.Add(r); break; }
                    foreach (Material m in r.sharedMaterials)
                        if (m != null && m.HasProperty(TipAxisId) && m.GetFloat(TipAxisId) > 1.5f && m.HasProperty(TipColorId) && m.GetColor(TipColorId).a > 0.001f) { tipRenderers.Add(r); break; }
                    foreach (Material m in r.sharedMaterials)
                        if (m != null && m.HasProperty(PerspectiveRemovalId))
                        {
                            perspectiveRenderers.Add(r);
                            anyPerspective |= m.GetFloat(PerspectiveRemovalId) > 0.001f;
                            break;
                        }
                }
                if (!anyPerspective) perspectiveRenderers.Clear();
            }
            if (perspectiveRenderers.Count > 0)
            {
                if (tipBlock == null) tipBlock = new MaterialPropertyBlock();
                Vector3 up = head.TransformDirection(upInHead).normalized;
                Vector3 forward = head.TransformDirection(forwardInHead).normalized;
                Vector3 center = head.position + (up * centerOffset.x + forward * centerOffset.y) * transform.lossyScale.y;
                foreach (Renderer r in perspectiveRenderers)
                {
                    if (r == null) continue;
                    r.GetPropertyBlock(tipBlock);
                    tipBlock.SetVector(PerspectiveCenterId, new Vector4(center.x, center.y, center.z, 1f));
                    r.SetPropertyBlock(tipBlock);
                }
            }
            if (tipRenderers.Count == 0) return;
            if (tipBlock == null) tipBlock = new MaterialPropertyBlock();

            foreach (Renderer r in tipRenderers)
            {
                if (r == null) continue;
                r.GetPropertyBlock(tipBlock);
                tipBlock.SetVector(TipRestRowId, TipRestRow(r, head));
                r.SetPropertyBlock(tipBlock);
            }
        }

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
            if (head == null) { enabled = false; return; }
            // 顔も SDF の体も無いキャラでも、高さの毛先のために動かす(毛先が無ければ LateUpdate は何もしない)
            forwardInHead = head.InverseTransformDirection(transform.forward);
            rightInHead = head.InverseTransformDirection(transform.right);
            upInHead = head.InverseTransformDirection(transform.up);
            block = new MaterialPropertyBlock();
        }

        private void LateUpdate()
        {
            if (block == null) { Setup(); if (!enabled || block == null) return; }
            UpdateTipHeights();
            Vector3 forward = head.TransformDirection(forwardInHead).normalized;
            Vector3 right = head.TransformDirection(rightInHead).normalized;
            Vector3 up = head.TransformDirection(upInHead).normalized;
            float scale = transform.lossyScale.y;
            Vector3 center = head.position + (up * centerOffset.x + forward * centerOffset.y) * scale;
            foreach (Renderer r in headForwardRenderers)
            {
                if (r == null) continue;
                r.GetPropertyBlock(block);
                block.SetVector(HeadForwardId, new Vector4(forward.x, forward.y, forward.z, 1f));
                r.SetPropertyBlock(block);
            }
            foreach (Renderer r in faceRenderers)
            {
                if (r == null) continue;
                r.GetPropertyBlock(block);
                block.SetVector(FaceForwardId, forward);
                block.SetVector(FaceRightId, right);
                block.SetVector(FaceUpId, up);
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
                block.SetVector(FaceUpId, transform.up);
                r.SetPropertyBlock(block);
            }
        }
    }
}
