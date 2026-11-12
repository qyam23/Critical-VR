#if UNITY_EDITOR
using System;
using System.IO;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.LowLevel;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace CriticalVR.EditorTools
{
    /// <summary>Runs actual MonoBehaviour updates and rendered frames; no simulated motion model.</summary>
    [InitializeOnLoad]
    public static class CriticalVRRoomCheck
    {
        private const string Key = "CriticalVR.RoomCheck.";
        private static double _start;
        private static double _wallStart;
        private static int _phase;
        private static MotionToExperienceManager _manager;
        private static PlayerLoopSystem _originalLoop;
        private static bool _motionDriverInstalled;
        private static Transform _eye;
        private static int _drivenFrames;
        private static double _nextGameFrameTime;

        static CriticalVRRoomCheck()
        {
            EditorApplication.playModeStateChanged += state =>
            {
                if (state == PlayModeStateChange.EnteredEditMode && SessionState.GetBool(Key + "BatchDone", false))
                {
                    SessionState.SetBool(Key + "BatchDone", false);
                    EditorApplication.delayCall += () => EditorApplication.Exit(SessionState.GetBool(Key + "Passed", false) ? 0 : 1);
                }
                if (state == PlayModeStateChange.EnteredPlayMode && SessionState.GetBool(Key + "Run", false))
                {
                    _phase = 0;
                    _drivenFrames = 0;
                    _nextGameFrameTime = Time.realtimeSinceStartupAsDouble;
                    _start = Time.time;
                    _wallStart = EditorApplication.timeSinceStartup;
                    _manager = UnityEngine.Object.FindFirstObjectByType<MotionToExperienceManager>();
                    if (_manager != null)
                        _eye = (Transform)new SerializedObject(_manager).FindProperty("centerEyeAnchor").objectReferenceValue;
                    InstallMotionDriver();
                    EditorApplication.update += Tick;
                }
                if (state == PlayModeStateChange.ExitingPlayMode)
                {
                    EditorApplication.update -= Tick;
                    if (SessionState.GetBool(Key + "Run", false)) Finish(false, "Play Mode stopped before check completed.");
                }
            };
        }

        [MenuItem("CriticalVR/Preview/Run Automated Room Check")]
        public static void Run()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Stop Play Mode before the room check.");
            if (!UnityEditor.SceneManagement.EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            Shader shader = Shader.Find(CriticalVRProjectPaths.ShaderPath);
            if (shader == null || ShaderUtil.ShaderHasError(shader)) throw new InvalidOperationException("Fix shader compilation before room check.");
            CriticalVRDesktopPreview.CreateScene();
            SessionState.SetBool(Key + "Run", true);
            EditorApplication.isPlaying = true;
        }

        public static void RunBatch()
        {
            SessionState.SetBool(Key + "Batch", true);
            Run();
        }

        private static void Tick()
        {
            try
            {
                if (_manager == null) throw new InvalidOperationException("Motion manager missing.");
                if (EditorApplication.timeSinceStartup - _wallStart > 300)
                    throw new TimeoutException("Play Mode did not complete within five minutes; no passing result is assumed.");
                double elapsed = Time.time - _start;
                var serialized = new SerializedObject(_manager);
                if (_phase == 0 && elapsed > 12)
                {
                    serialized.FindProperty("useDebugSlider").boolValue = true;
                    serialized.FindProperty("debugIntensity").floatValue = 0.9f;
                    serialized.ApplyModifiedPropertiesWithoutUndo();
                    _phase = 1;
                }
                if (_phase == 1 && elapsed > 22)
                {
                    if (_manager.IntensityScore < 0.7f) throw new InvalidOperationException("Debug activation failed.");
                    Directory.CreateDirectory("Builds/Quest/Logs");
                    CaptureCamera("Builds/Quest/Logs/synthetic-active.png");
                    serialized.FindProperty("useDebugSlider").boolValue = false;
                    serialized.ApplyModifiedPropertiesWithoutUndo();
                    _phase = 2;
                }
                // Drive the real tracked-transform input through a repeatable head-turn sequence.
                if (_phase == 2)
                {
                    if (elapsed > 34)
                    {
                        if (_manager.IntensityScore < 0.1f) throw new InvalidOperationException($"Head-motion activation failed: intensity={_manager.IntensityScore}, angularVelocity={ReadRuntimeFloat("_angularVelocity")}, motion={ReadRuntimeFloat("_smoothedMotion")}, drivenFrames={_drivenFrames}, eye={_eye.localEulerAngles}, suspended={_manager.EffectsSuspended}.");
                        _phase = 3;
                    }
                }
                if (_phase == 3 && elapsed > 90)
                {
                    if (_manager.IntensityScore > 0.08f) throw new InvalidOperationException("Motion-free decay failed.");
                    serialized.FindProperty("useDebugSlider").boolValue = true;
                    serialized.FindProperty("debugIntensity").floatValue = 0.9f;
                    serialized.ApplyModifiedPropertiesWithoutUndo();
                    _phase = 4;
                }
                if (_phase == 4 && elapsed > 98) { _manager.ResetExperience(); _phase = 5; }
                if (_phase == 5 && elapsed > 102)
                {
                    if (!_manager.EffectsSuspended || _manager.IntensityScore > 0.001f || serialized.FindProperty("useDebugSlider").boolValue)
                        throw new InvalidOperationException("Reset did not clear and suspend effects/debug input.");
                    if (elapsed > 110)
                    {
                        _eye.localRotation = Quaternion.identity;
                        CaptureCamera("Builds/Quest/Logs/synthetic-reset.png");
                        _phase = 6;
                    }
                }
                if (_phase == 6 && elapsed > 112)
                {
                    if (_manager.IntensityScore > 0.001f) throw new InvalidOperationException("Motion reactivated a suspended experience.");
                    _manager.ResumeExperience();
                    if (_manager.EffectsSuspended) throw new InvalidOperationException("Explicit resume failed.");
                    Finish(true, "Debug activation, real transform motion, 56s decay, reset, debug disable, idle/motion stability and explicit resume passed.");
                }
            }
            catch (Exception exception) { Finish(false, exception.ToString()); }
        }

        private static void Finish(bool passed, string detail)
        {
            if (SessionState.GetBool(Key + "Batch", false))
            {
                SessionState.SetBool(Key + "Batch", false);
                SessionState.SetBool(Key + "Passed", passed);
                SessionState.SetBool(Key + "BatchDone", true);
            }
            SessionState.SetBool(Key + "Run", false);
            EditorApplication.update -= Tick;
            if (_motionDriverInstalled) PlayerLoop.SetPlayerLoop(_originalLoop);
            _motionDriverInstalled = false;
            Directory.CreateDirectory("Builds/Quest/Logs");
            File.WriteAllText("Builds/Quest/Logs/synthetic-check.md", $"# Synthetic Unity Play Mode check\n\n{DateTime.UtcNow:O}\n\n{(passed ? "PASS" : "FAIL")}: {detail}\n\nThis does not validate passthrough cameras or headset stereo/performance.\n");
            if (passed) Debug.Log(detail); else Debug.LogError(detail);
            EditorApplication.isPlaying = false;
        }

        private static void InstallMotionDriver()
        {
            // EditorApplication.update is not synchronized with MonoBehaviour.Update.
            // Inject tracked-transform motion once per game frame, before its consumer.
            _originalLoop = PlayerLoop.GetCurrentPlayerLoop();
            var loop = _originalLoop;
            loop.subSystemList = (PlayerLoopSystem[])_originalLoop.subSystemList.Clone();
            for (int i = 0; i < loop.subSystemList.Length; i++)
            {
                var group = loop.subSystemList[i];
                if (group.type != typeof(UnityEngine.PlayerLoop.Update)) continue;
                var systems = new List<PlayerLoopSystem>();
                foreach (var system in group.subSystemList)
                {
                    if (system.type == typeof(UnityEngine.PlayerLoop.Update.ScriptRunBehaviourUpdate))
                        systems.Add(new PlayerLoopSystem { type = typeof(CriticalVRRoomCheck), updateDelegate = DriveMotion });
                    systems.Add(system);
                }
                group.subSystemList = systems.ToArray();
                loop.subSystemList[i] = group;
                PlayerLoop.SetPlayerLoop(loop);
                _motionDriverInstalled = true;
                return;
            }
            throw new InvalidOperationException("Could not install per-frame motion driver.");
        }

        private static void DriveMotion()
        {
            if (_eye == null) return;
            // Batch editors can run >4000 FPS. At that rate Quaternion.Angle's
            // epsilon swallows each tiny rotation. Exercise the real consumer at
            // a Quest-like 72 FPS, without changing the runtime or its thresholds.
            int waitMilliseconds = (int)((_nextGameFrameTime - Time.realtimeSinceStartupAsDouble) * 1000);
            if (waitMilliseconds > 0) System.Threading.Thread.Sleep(waitMilliseconds);
            _nextGameFrameTime = Math.Max(_nextGameFrameTime + 1.0 / 72.0, Time.realtimeSinceStartupAsDouble);
            _drivenFrames++;
            float elapsed = (float)(Time.time - _start);
            if (_phase == 2)
                _eye.localRotation = Quaternion.Euler(0f, 35f * Mathf.Sin(elapsed * 5f), 0f);
            else if (_phase == 5 && elapsed > 102f && elapsed < 110f)
                _eye.localRotation = Quaternion.Euler(0f, 80f * Mathf.Sin(elapsed * 5f), 0f);
        }

        private static float ReadRuntimeFloat(string name) => (float)typeof(MotionToExperienceManager)
            .GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(_manager);

        private static void CaptureCamera(string path)
        {
            var camera = Camera.main;
            var target = new RenderTexture(1280, 720, 24, RenderTextureFormat.ARGB32);
            var image = new Texture2D(1280, 720, TextureFormat.RGB24, false);
            RenderTexture previous = RenderTexture.active;
            try
            {
                target.Create();
                var request = new UniversalRenderPipeline.SingleCameraRequest { destination = target };
                if (!RenderPipeline.SupportsRenderRequest(camera, request))
                    throw new InvalidOperationException("URP camera capture request is not supported.");
                RenderPipeline.SubmitRenderRequest(camera, request);
                RenderTexture.active = target;
                image.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0);
                image.Apply();
                File.WriteAllBytes(path, image.EncodeToPNG());
            }
            finally
            {
                RenderTexture.active = previous;
                target.Release();
                UnityEngine.Object.DestroyImmediate(target);
                UnityEngine.Object.DestroyImmediate(image);
            }
        }
    }
}
#endif
