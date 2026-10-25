using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

namespace GalaxyToon
{
    /// <summary>
    /// SF の転送・服の入れ替え。光る線が体を上下に走り、線の通った所が消える/現れる。線に沿って光の粒が舞う。
    /// Vanish=消える Appear=現れる Swap=古い服(outgoing)が消えながら、同じ線で新しい服(incoming)が現れる。
    /// GalaxyShader/Toon のディゾルブ(位置・ワールドの高さ)を使う。演出中だけマテリアルを複製し、終わったら元に戻す。
    /// shortcut: キーワード _DISSOLVE を実行時に ON にするので、ビルドではディゾルブ ON のマテリアルが最低1つ含まれている必要がある(無いと消えない)。困ったら ShaderVariantCollection に入れる。
    /// </summary>
    [AddComponentMenu("GalaxyToon/Galaxy Teleport")]
    public sealed class GalaxyTeleport : MonoBehaviour
    {
        public enum Kind { Vanish, Appear, Swap }

        [Tooltip("Vanish=消える Appear=現れる Swap=服の入れ替え")] public Kind kind = Kind.Vanish;
        [Tooltip("消える物(Vanish・Swap の古い服)。空なら子の全 Renderer。")] public Renderer[] outgoing = new Renderer[0];
        [Tooltip("現れる物(Appear・Swap の新しい服)。空なら子の全 Renderer(Appear のとき)。")] public Renderer[] incoming = new Renderer[0];
        [Tooltip("かかる秒数。")] public float duration = 1.6f;
        [Tooltip("ON: 上から下へ線が走る。OFF: 下から上へ。")] public bool topDown = true;
        [Tooltip("線(境目)の色。HDR で明るくすると光る。")] [ColorUsage(true, true)] public Color edgeColor = new Color(0.5f, 1.8f, 3.5f, 1f);
        [Tooltip("線の太さ(全体の高さに対する割合)。")] [Range(0.005f, 0.3f)] public float edgeWidth = 0.03f;
        [Tooltip("線のギザギザ。")] [Range(0f, 1f)] public float noise = 0.15f;
        [Tooltip("光の粒のマテリアル(加算のパーティクル用)。空なら粒を出さない。")] public Material particleMaterial;
        [Tooltip("1秒あたりの光の粒の数。")] public float particleRate = 500f;
        [Tooltip("開始時に自動で再生する。")] public bool playOnStart;
        public UnityEvent onFinished = new UnityEvent();

        public bool IsPlaying { get; private set; }

        private static readonly int AmountId = Shader.PropertyToID("_DissolveAmount");
        private static readonly int ModeId = Shader.PropertyToID("_DissolveMode");
        private static readonly int DirId = Shader.PropertyToID("_DissolveDirection");
        private static readonly int StartId = Shader.PropertyToID("_DissolveStart");
        private static readonly int InvertId = Shader.PropertyToID("_DissolveInvert");
        private static readonly int WorldId = Shader.PropertyToID("_DissolveWorld");
        private static readonly int NoiseId = Shader.PropertyToID("_DissolveNoiseStrength");
        private static readonly int EdgeWidthId = Shader.PropertyToID("_DissolveEdgeWidth");
        private static readonly int EdgeColorId = Shader.PropertyToID("_DissolveEdgeColor");

        private sealed class Part
        {
            public Renderer renderer;
            public Material[] original;   // 元の共有マテリアル(終わったら戻す)
            public Material[] instances;  // 演出中だけの複製
            public bool appear;
            public bool switchOnly;       // トゥーンのマテリアルが無い部品: 半分の所で表示を切り替えるだけ
        }

        private readonly List<Part> parts = new List<Part>();

        private void Start() { if (playOnStart) Play(); }

        [ContextMenu("再生")]
        public void Play()
        {
            if (!Application.isPlaying) return;
            StopAllCoroutines();
            Restore();
            StartCoroutine(Run());
        }

        private IEnumerator Run()
        {
            IsPlaying = true;
            Renderer[] outs = kind == Kind.Appear ? new Renderer[0] : (outgoing.Length > 0 ? outgoing : OwnRenderers());
            Renderer[] ins = kind == Kind.Vanish ? new Renderer[0] : (incoming.Length > 0 ? incoming : (kind == Kind.Appear ? OwnRenderers() : new Renderer[0]));

            // 全部品を囲む高さ(線はワールドの高さで走るので、部品どうしで線がそろう)
            Bounds b = default; bool has = false;
            foreach (Renderer r in outs) if (r != null) { if (has) b.Encapsulate(r.bounds); else { b = r.bounds; has = true; } }
            foreach (Renderer r in ins) if (r != null) { if (has) b.Encapsulate(r.bounds); else { b = r.bounds; has = true; } }
            if (!has) { IsPlaying = false; yield break; }
            float range = Mathf.Max(b.size.y, 0.01f);
            Vector4 dir = topDown ? new Vector4(0f, -1f, 0f, range) : new Vector4(0f, 1f, 0f, range);
            float start = topDown ? -b.max.y : b.min.y;   // 向きに沿った開始位置(ワールド)

            foreach (Renderer r in outs) AddPart(r, false, dir, start);
            foreach (Renderer r in ins) AddPart(r, true, dir, start);

            ParticleSystem sparks = particleMaterial != null ? CreateSparks(b) : null;

            for (float t = 0f; t < 1f; t += Time.deltaTime / Mathf.Max(duration, 0.01f))
            {
                SetProgress(t);
                if (sparks != null)
                {
                    // 線の高さ: 値 = 量×(1+幅) の所
                    float line = Mathf.Clamp01(t * (1f + edgeWidth));
                    float y = topDown ? b.max.y - line * range : b.min.y + line * range;
                    sparks.transform.position = new Vector3(b.center.x, y, b.center.z);
                }
                yield return null;
            }
            SetProgress(1f);

            if (sparks != null)
            {
                var em = sparks.emission; em.enabled = false;
                Destroy(sparks.gameObject, 1.5f);
            }
            foreach (Part p in parts) if (!p.appear && p.renderer != null) p.renderer.enabled = false; // 消えた物は非表示のまま
            Restore();
            IsPlaying = false;
            onFinished.Invoke();
        }

        private Renderer[] OwnRenderers()
        {
            var list = new List<Renderer>();
            foreach (Renderer r in GetComponentsInChildren<Renderer>(true))
                if (!(r is ParticleSystemRenderer)) list.Add(r);
            return list.ToArray();
        }

        private void AddPart(Renderer r, bool appear, Vector4 dir, float start)
        {
            if (r == null) return;
            var p = new Part { renderer = r, original = r.sharedMaterials, appear = appear };
            p.instances = new Material[p.original.Length];
            for (int i = 0; i < p.original.Length; i++)
            {
                Material src = p.original[i];
                if (src == null || !src.HasProperty(AmountId)) { p.instances[i] = src; continue; } // トゥーン以外は途中で切り替えるだけ
                var m = new Material(src) { name = src.name + " (Teleport)" };
                m.EnableKeyword("_DISSOLVE");
                m.SetFloat("_UseDissolve", 1f);
                m.SetFloat(ModeId, 1f);
                m.SetFloat(WorldId, 1f);
                m.SetVector(DirId, dir);
                m.SetFloat(StartId, start);
                m.SetFloat(InvertId, appear ? 1f : 0f);
                m.SetFloat(NoiseId, noise);
                m.SetFloat(EdgeWidthId, edgeWidth);
                m.SetColor(EdgeColorId, edgeColor);
                p.instances[i] = m;
            }
            p.switchOnly = System.Array.TrueForAll(p.instances, m => m == null || !m.IsKeywordEnabled("_DISSOLVE"));
            r.sharedMaterials = p.instances;
            if (appear) r.enabled = true;
            parts.Add(p);
        }

        /// <summary>t=0 → 1。消える物は線より手前から消え、現れる物は同じ線の手前から現れる(境目が重なる)。</summary>
        private void SetProgress(float t)
        {
            float a = t * (1f + edgeWidth);                 // 消える物の線(値)
            float outAmount = t;
            float inAmount = Mathf.Clamp01((1f - a) / (1f + edgeWidth)); // 逆向きで同じ線になる量
            foreach (Part p in parts)
            {
                if (p.renderer == null) continue;
                if (p.switchOnly) { p.renderer.enabled = p.appear ? t >= 0.5f : t < 0.5f; continue; }
                for (int i = 0; i < p.instances.Length; i++)
                    if (p.instances[i] != null && p.instances[i] != p.original[i]) p.instances[i].SetFloat(AmountId, p.appear ? inAmount : outAmount);
            }
        }

        private void Restore()
        {
            foreach (Part p in parts)
            {
                if (p.renderer != null) p.renderer.sharedMaterials = p.original;
                for (int i = 0; i < p.instances.Length; i++)
                    if (p.instances[i] != null && p.instances[i] != p.original[i])
                    {
                        if (Application.isPlaying) Destroy(p.instances[i]); else DestroyImmediate(p.instances[i]);
                    }
            }
            parts.Clear();
        }

        private void OnDisable()
        {
            if (IsPlaying) { StopAllCoroutines(); Restore(); IsPlaying = false; }
        }

        private ParticleSystem CreateSparks(Bounds b)
        {
            var go = new GameObject("Teleport Sparks");
            go.transform.SetPositionAndRotation(b.center, Quaternion.identity);
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.loop = true;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.4f, 1f);
            main.startSpeed = 0f;
            main.startSize = new ParticleSystem.MinMaxCurve(0.008f, 0.025f);
            main.startColor = edgeColor;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 2000;
            var em = ps.emission; em.rateOverTime = particleRate;
            var shape = ps.shape; shape.shapeType = ParticleSystemShapeType.Box; shape.scale = new Vector3(b.size.x * 0.7f, 0.005f, b.size.z * 0.7f);
            // ふわっと上へ舞い、横に少し散る
            var vel = ps.velocityOverLifetime; vel.enabled = true; vel.space = ParticleSystemSimulationSpace.World;
            vel.x = new ParticleSystem.MinMaxCurve(-0.08f, 0.08f);
            vel.y = new ParticleSystem.MinMaxCurve(0.05f, 0.35f);
            vel.z = new ParticleSystem.MinMaxCurve(-0.08f, 0.08f);
            var col = ps.colorOverLifetime; col.enabled = true;
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                      new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0f, 1f) });
            col.color = g;
            var rend = go.GetComponent<ParticleSystemRenderer>();
            rend.sharedMaterial = particleMaterial;
            rend.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            rend.receiveShadows = false;
            ps.Play();
            return ps;
        }
    }
}
