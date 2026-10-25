using System;
using System.Collections.Generic;
using System.Text;
using Unity.Profiling;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace GalaxyToon.Editor
{
    /// <summary>再生中のシーンで、機能を1つずつ切って GPU / CPU のフレーム時間を測る(各 1 秒慣らし + 3 秒計測)。
    /// 結果は Result と Console に出る。終わったら全部元に戻す。</summary>
    public static class GalaxyToonPerfSweep
    {
        public static string Result = "";
        public static bool Running;

        private sealed class Step { public string Name; public Action On, Off; }
        private static List<Step> steps;
        private static int index;
        private static double phaseStart;
        private static bool measuring;
        private static ProfilerRecorder cpu, gpu, setPass, tris, draws;
        private static double cpuSum, gpuSum; private static int cpuN, gpuN;
        private static readonly StringBuilder log = new StringBuilder();

        [MenuItem("Tools/GalaxyToon/負荷を測る (再生中のシーン)")]
        public static void Start()
        {
            if (!Application.isPlaying) { Debug.LogWarning("[負荷] 再生中に実行してください"); return; }
            if (Running) return;
            steps = BuildSteps();
            index = 0; measuring = false; phaseStart = EditorApplication.timeSinceStartup;
            log.Clear(); log.AppendLine($"画面 {Screen.width}x{Screen.height}");
            cpu = ProfilerRecorder.StartNew(ProfilerCategory.Internal, "CPU Main Thread Frame Time");
            gpu = ProfilerRecorder.StartNew(ProfilerCategory.Render, "GPU Frame Time");
            setPass = ProfilerRecorder.StartNew(ProfilerCategory.Render, "SetPass Calls Count");
            draws = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Draw Calls Count");
            tris = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Triangles Count");
            Running = true; Result = "running";
            steps[0].On?.Invoke();
            EditorApplication.update += Tick;
        }

        private static void Tick()
        {
            if (!Application.isPlaying) { Finish(); return; }
            double t = EditorApplication.timeSinceStartup - phaseStart;
            if (!measuring)
            {
                if (t < 1.0) return; // 慣らし(変更直後のコンパイル・RT 作り直しを避ける)
                measuring = true; phaseStart = EditorApplication.timeSinceStartup; cpuSum = gpuSum = 0; cpuN = gpuN = 0;
                return;
            }
            if (cpu.Valid && cpu.LastValue > 0) { cpuSum += cpu.LastValue; cpuN++; }
            if (gpu.Valid && gpu.LastValue > 0) { gpuSum += gpu.LastValue; gpuN++; }
            if (t < 3.0) return;
            string g = gpuN > 0 ? (gpuSum / gpuN * 1e-6).ToString("F2") : "n/a";
            string c = cpuN > 0 ? (cpuSum / cpuN * 1e-6).ToString("F2") : "n/a";
            log.AppendLine($"{steps[index].Name,-28} GPU {g} ms  CPU {c} ms  SetPass {setPass.LastValue}  Draw {draws.LastValue}  Tris {tris.LastValue}");
            steps[index].Off?.Invoke();
            index++;
            if (index >= steps.Count) { Finish(); return; }
            steps[index].On?.Invoke();
            measuring = false; phaseStart = EditorApplication.timeSinceStartup;
        }

        private static void Finish()
        {
            EditorApplication.update -= Tick;
            if (index < steps.Count) steps[index].Off?.Invoke();
            cpu.Dispose(); gpu.Dispose(); setPass.Dispose(); draws.Dispose(); tris.Dispose();
            Running = false;
            Result = log.ToString();
            Debug.Log("[負荷]\n" + Result);
        }

        private static List<Step> BuildSteps()
        {
            var list = new List<Step> { new Step { Name = "全部あり(基準)" } };
            var urp = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
            var volume = UnityEngine.Object.FindFirstObjectByType<Volume>();
            VolumeProfile profile = volume != null ? volume.profile : null; // 再生中のインスタンス(アセットは変えない)

            void VolumeStep<T>(string name) where T : VolumeComponent
            {
                if (profile == null || !profile.TryGet<T>(out var comp) || !comp.active) return;
                list.Add(new Step { Name = name, On = () => comp.active = false, Off = () => comp.active = true });
            }
            VolumeStep<DepthOfField>("被写界深度なし");
            VolumeStep<Bloom>("ブルームなし");

            ScriptableRendererData data = null;
            if (urp != null)
            {
                var so = new SerializedObject(urp);
                var arr = so.FindProperty("m_RendererDataList");
                if (arr != null && arr.arraySize > 0) data = arr.GetArrayElementAtIndex(0).objectReferenceValue as ScriptableRendererData;
            }
            if (data != null)
                foreach (var f in data.rendererFeatures)
                {
                    if (f == null || !f.isActive) continue;
                    var feature = f;
                    list.Add(new Step { Name = feature.name + " なし", On = () => feature.SetActive(false), Off = () => feature.SetActive(true) });
                }

            // キャラ(GalaxyToonFace を持つもの)
            var chars = new List<GameObject>();
            foreach (var face in UnityEngine.Object.FindObjectsByType<GalaxyToonFace>(FindObjectsSortMode.None)) chars.Add(face.gameObject);
            var renderers = new List<Renderer>();
            foreach (var c in chars) renderers.AddRange(c.GetComponentsInChildren<Renderer>());
            list.Add(new Step { Name = "輪郭線なし", On = () => SetPass(renderers, "SRPDefaultUnlit", false), Off = () => SetPass(renderers, "SRPDefaultUnlit", true) });
            var lights = UnityEngine.Object.FindObjectsByType<Light>(FindObjectsSortMode.None);
            var shadowed = new List<(Light, LightShadows)>();
            list.Add(new Step
            {
                Name = "ライトの影なし",
                On = () => { shadowed.Clear(); foreach (var l in lights) if (l.shadows != LightShadows.None) { shadowed.Add((l, l.shadows)); l.shadows = LightShadows.None; } },
                Off = () => { foreach (var (l, s) in shadowed) l.shadows = s; }
            });
            list.Add(new Step { Name = "キャラなし", On = () => { foreach (var c in chars) c.SetActive(false); }, Off = () => { foreach (var c in chars) c.SetActive(true); } });
            if (urp != null)
            {
                float scale = urp.renderScale;
                list.Add(new Step { Name = "描画解像度 0.5", On = () => urp.renderScale = 0.5f, Off = () => urp.renderScale = scale });
            }
            return list;
        }

        private static void SetPass(List<Renderer> renderers, string pass, bool on)
        {
            foreach (var r in renderers)
                foreach (var m in r.materials) // 再生中のインスタンス
                    if (m != null) m.SetShaderPassEnabled(pass, on);
        }
    }
}
