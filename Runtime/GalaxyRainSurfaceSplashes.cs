using System.Collections.Generic;
using UnityEngine;

namespace GalaxyToon
{
    [DisallowMultipleComponent, DefaultExecutionOrder(10000)]
    public sealed class GalaxyRainSurfaceSplashes : MonoBehaviour
    {
        [Min(0f), Tooltip("雨1のとき、上から見た表面1平方mに当たる雨の数/秒。")]
        public float impactsPerSquareMeter = 70f;
        [Range(1, 6)] public int dropsPerImpact = 4;
        [Min(0.02f)] public float surfaceUpdateInterval = 0.035f;
        [Min(0f), Tooltip("球などの底から滴り落ちる水滴の数/秒。0なら滴下しない。")]
        public float bottomDripsPerSecond;

        private sealed class Surface
        {
            public Renderer renderer;
            public SkinnedMeshRenderer skinned;
            public Mesh mesh;
            public readonly List<Vector3> vertices = new List<Vector3>();
            public readonly List<int> triangles = new List<int>();
            public readonly List<float> areas = new List<float>();
            public readonly List<float> receive = new List<float>();
            public float projectedArea, remainder;
            public int impactCount;
        }

        private readonly List<Surface> surfaces = new List<Surface>();
        private GalaxyToonEffects effects;
        private ParticleSystem splashes;
        private float nextUpdate, lastUpdate;
        private float dripRemainder;
        public int EmittedImpacts { get; private set; }
        public ParticleSystem Particles => splashes;
        public int ImpactsOn(string rendererName)
        {
            int count = 0;
            foreach (var surface in surfaces)
                if (surface.renderer != null && surface.renderer.name == rendererName) count += surface.impactCount;
            return count;
        }

        private void OnEnable()
        {
            effects = GetComponent<GalaxyToonEffects>();
            lastUpdate = Time.time;
        }

        private void CreateSplashes()
        {
            var child = new GameObject("Rain Surface Splashes");
            child.transform.SetParent(transform, false);
            splashes = child.AddComponent<ParticleSystem>();
            splashes.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = splashes.main;
            main.playOnAwake = false;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 1000;
            main.gravityModifier = 1f;
            main.startSpeed = 0f;
            main.startLifetime = 0.16f;
            var size = splashes.sizeOverLifetime; size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(0.6f, 0.8f), new Keyframe(1f, 0f)));
            var emission = splashes.emission; emission.enabled = false;
            var shape = splashes.shape; shape.enabled = false;
            if (bottomDripsPerSecond > 0f)
            {
                var collision = splashes.collision;
                collision.enabled = true;
                collision.type = ParticleSystemCollisionType.World;
                collision.mode = ParticleSystemCollisionMode.Collision3D;
                collision.quality = ParticleSystemCollisionQuality.Medium;
                collision.lifetimeLoss = 1f;
                collision.radiusScale = 0.5f;
            }
            var color = splashes.colorOverLifetime;
            color.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0.8f, 0.45f), new GradientAlphaKey(0f, 1f) });
            color.color = gradient;
            var renderer = child.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = effects.rainSplashMaterial;
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.minParticleSize = 0f;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            splashes.Play();

            foreach (var source in GetComponentsInChildren<Renderer>())
            {
                var skinned = source as SkinnedMeshRenderer;
                var filter = source.GetComponent<MeshFilter>();
                if (skinned == null && filter == null) continue;
                var materials = source.sharedMaterials;
                bool receives = false;
                foreach (var m in materials)
                    receives |= m != null && m.HasProperty("_RainSplash") && m.GetFloat("_RainSplash") > 0f;
                var sourceMesh = skinned != null ? skinned.sharedMesh : filter.sharedMesh;
                if (!receives || sourceMesh == null) continue;
                var surface = new Surface { renderer = source, skinned = skinned, mesh = skinned != null ? new Mesh { name = "Rain surface snapshot" } : Instantiate(sourceMesh) };
                if (skinned != null) skinned.BakeMesh(surface.mesh);
                surface.mesh.GetVertices(surface.vertices);
                // BakeMesh also works for imported meshes with Read/Write disabled.
                for (int sub = 0; sub < surface.mesh.subMeshCount && sub < materials.Length; sub++)
                {
                    var m = materials[sub];
                    if (m == null || !m.HasProperty("_RainSplash")) continue;
                    float receive = m.GetFloat("_RainSplash") * m.GetFloat("_RainReceive");
                    if (receive <= 0f) continue;
                    var indices = surface.mesh.GetTriangles(sub);
                    for (int i = 0; i < indices.Length; i += 3)
                    {
                        Vector3 a = source.transform.TransformPoint(surface.vertices[indices[i]]);
                        Vector3 b = source.transform.TransformPoint(surface.vertices[indices[i + 1]]);
                        Vector3 c = source.transform.TransformPoint(surface.vertices[indices[i + 2]]);
                        float area = Mathf.Max(0f, Vector3.Cross(b - a, c - a).y) * 0.5f * receive;
                        surface.triangles.Add(indices[i]); surface.triangles.Add(indices[i + 1]); surface.triangles.Add(indices[i + 2]);
                        surface.receive.Add(receive);
                        surface.projectedArea += area;
                        surface.areas.Add(surface.projectedArea);
                    }
                }
                if (surface.areas.Count == 0) Destroy(surface.mesh);
                else surfaces.Add(surface);
            }
        }

        private void LateUpdate()
        {
            if (effects == null || !effects.isActiveAndEnabled || effects.rain <= 0f || effects.rainSplashMaterial == null)
            {
                lastUpdate = Time.time;
                dripRemainder = 0f;
                foreach (var surface in surfaces) surface.remainder = 0f;
                return;
            }
            if (Time.time < nextUpdate) return;
            float dt = Mathf.Min(Time.time - lastUpdate, 0.2f);
            lastUpdate = Time.time;
            nextUpdate = Time.time + surfaceUpdateInterval;
            if (splashes == null) CreateSplashes();
            foreach (var surface in surfaces)
            {
                var source = surface.renderer;
                if (source == null || !source.enabled || !source.gameObject.activeInHierarchy || !source.isVisible) continue;
                if (surface.skinned != null)
                {
                    surface.skinned.BakeMesh(surface.mesh);
                    surface.mesh.GetVertices(surface.vertices);
                }
                // アニメーション後の実際の面積・向きで雨を配分する。初期姿勢の面だけに集中させない。
                surface.projectedArea = 0f;
                for (int tri = 0; tri < surface.areas.Count; tri++)
                {
                    int j = tri * 3;
                    Vector3 va = source.transform.TransformPoint(surface.vertices[surface.triangles[j]]);
                    Vector3 vb = source.transform.TransformPoint(surface.vertices[surface.triangles[j + 1]]);
                    Vector3 vc = source.transform.TransformPoint(surface.vertices[surface.triangles[j + 2]]);
                    surface.projectedArea += Mathf.Max(0f, Vector3.Cross(vb - va, vc - va).y) * 0.5f * surface.receive[tri];
                    surface.areas[tri] = surface.projectedArea;
                }
                if (surface.projectedArea < 0.000001f) continue;
                surface.remainder += surface.projectedArea * impactsPerSquareMeter * effects.rain * dt;
                int count = Mathf.FloorToInt(surface.remainder);
                surface.remainder -= count;
                for (int hit = 0; hit < count; hit++)
                {
                    float sample = Random.value * surface.projectedArea;
                    int index = surface.areas.BinarySearch(sample);
                    if (index < 0) index = ~index;
                    index = Mathf.Min(index, surface.areas.Count - 1) * 3;
                    Vector3 a = source.transform.TransformPoint(surface.vertices[surface.triangles[index]]);
                    Vector3 b = source.transform.TransformPoint(surface.vertices[surface.triangles[index + 1]]);
                    Vector3 c = source.transform.TransformPoint(surface.vertices[surface.triangles[index + 2]]);
                    Vector3 normal = Vector3.Cross(b - a, c - a).normalized;
                    if (normal.y <= 0.05f) continue; // 雨の当たらない裏面からは出さない。
                    float u = Mathf.Sqrt(Random.value), v = Random.value;
                    Vector3 point = a * (1f - u) + b * (u * (1f - v)) + c * (u * v) + normal * 0.003f;
                    surface.impactCount++;
                    EmittedImpacts++;
                    for (int drop = 0; drop < dropsPerImpact; drop++)
                    {
                        Vector3 tangent = Vector3.ProjectOnPlane(Random.onUnitSphere, normal).normalized;
                        var particle = new ParticleSystem.EmitParams
                        {
                            position = point,
                            // 見える大きさ・滞空(2mm/0.1秒では画面でほぼ消える)。少しアニメ寄りに弾けさせる
                            velocity = normal * Random.Range(0.35f, 0.75f) + tangent * Random.Range(0.08f, 0.22f),
                            startSize = Random.Range(0.008f, 0.014f),
                            startLifetime = Random.Range(0.2f, 0.35f),
                            startColor = new Color(0.85f, 0.94f, 1f, 0.8f)
                        };
                        splashes.Emit(particle, 1);
                    }
                }
            }
            if (bottomDripsPerSecond > 0f && surfaces.Count > 0)
            {
                dripRemainder += bottomDripsPerSecond * effects.rain * dt;
                int count = Mathf.FloorToInt(dripRemainder);
                dripRemainder -= count;
                var bounds = surfaces[0].renderer.bounds;
                for (int i = 0; i < count; i++)
                    splashes.Emit(new ParticleSystem.EmitParams
                    {
                        position = new Vector3(bounds.center.x, bounds.min.y - 0.008f, bounds.center.z),
                        velocity = Vector3.down * 0.04f,
                        startSize = 0.0035f,
                        startLifetime = 0.65f,
                        startColor = new Color(0.85f, 0.94f, 1f, 0.8f)
                    }, 1);
            }
        }

        private void OnDisable()
        {
            if (splashes != null) Destroy(splashes.gameObject);
            foreach (var surface in surfaces) if (surface.mesh != null) Destroy(surface.mesh);
            surfaces.Clear();
            splashes = null;
        }
    }
}
