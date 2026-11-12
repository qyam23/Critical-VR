#if UNITY_ANDROID && DEVELOPMENT_BUILD && !UNITY_EDITOR
using System;
using System.IO;
using UnityEngine;

namespace CriticalVR
{
    // ADB run-as can access the development app's private files. No device network listener is needed.
    internal sealed class QuestDesktopLink : MonoBehaviour
    {
        [Serializable]
        private sealed class Command
        {
            public string id;
            public string action;
            public float strength = 1f;
            public float depth = 1f;
        }

        [Serializable]
        private sealed class Sample
        {
            public string session;
            public string command;
            public double seconds;
            public float angularDegPerSec;
            public float linearMetersPerSec;
            public float intensity;
            public float strength = 1f;
            public float depth = 1f;
            public float reveal;
            public bool suspended;
            public bool camerasReady;
        }

        private readonly Sample _sample = new();
        private MotionToExperienceManager _experience;
        private QuestPassthroughCameraBridge _bridge;
        private Transform _eye;
        private Material _material;
        private string _commandPath;
        private string _samplePath;
        private float _nextPoll;
        private float _observeUntil;
        private bool _hasPose;
        private Vector3 _lastPosition;
        private Quaternion _lastRotation;

        internal void Initialize(MotionToExperienceManager experience, Camera eye,
            QuestPassthroughCameraBridge bridge, Material material)
        {
            _experience = experience;
            _eye = eye != null ? eye.transform : null;
            _bridge = bridge;
            _material = material;
            try
            {
                using var player = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
                using var activity = player.GetStatic<AndroidJavaObject>("currentActivity");
                using var files = activity.Call<AndroidJavaObject>("getFilesDir");
                string directory = files.Call<string>("getAbsolutePath");
                _commandPath = Path.Combine(directory, "criticalvr-command.json");
                _samplePath = Path.Combine(directory, "criticalvr-sample.json");
                // Ignore commands left from a previous app session.
                if (File.Exists(_commandPath)) _sample.command = ReadCommand()?.id;
                if (File.Exists(_samplePath)) File.Delete(_samplePath);
                _sample.session = Guid.NewGuid().ToString("N");
            }
            catch (Exception error)
            {
                Debug.LogWarning("[CriticalVR.Desktop] USB console unavailable: " + error.Message);
                enabled = false;
            }
        }

        private Command ReadCommand()
        {
            var info = new FileInfo(_commandPath);
            if (!info.Exists || info.Length > 1024) return null;
            return JsonUtility.FromJson<Command>(File.ReadAllText(_commandPath));
        }

        private void Update()
        {
            if (_commandPath == null || _eye == null || _experience == null) return;
            float now = Time.realtimeSinceStartup;
            if (now < _observeUntil) MeasurePose();
            else _hasPose = false;
            if (now < _nextPoll) return;
            _nextPoll = now + 0.25f;
            try
            {
                Command command = ReadCommand();
                if (command != null && !string.IsNullOrEmpty(command.id) && command.id != _sample.command)
                {
                    _sample.command = command.id;
                    _observeUntil = now + 6f;
                    ApplyCommand(command);
                }

                if (now >= _observeUntil) return;
                _sample.seconds = Time.realtimeSinceStartupAsDouble;
                _sample.reveal = _experience.SessionReveal;
                _sample.suspended = _experience.EffectsSuspended;
                _sample.camerasReady = _bridge.StereoFeedReady;
                _sample.intensity = _sample.camerasReady && !_sample.suspended
                    ? Mathf.Clamp01(_experience.IntensityScore * _sample.strength) : 0f;
                File.WriteAllText(_samplePath, JsonUtility.ToJson(_sample));
            }
            catch (IOException) { }
            catch (ArgumentException) { }
        }

        private void ApplyCommand(Command command)
        {
            switch (command.action)
            {
                case "stop": _experience.ResetExperience(); break;
                case "resume":
                    if (_experience.EffectsSuspended && OVRManager.hasInputFocus && OVRManager.hasVrFocus)
                        _experience.ResumeExperience();
                    break;
                case "set":
                    _sample.strength = FiniteGain(command.strength);
                    _sample.depth = FiniteGain(command.depth);
                    _material.SetFloat("_DesktopStrength", _sample.strength);
                    _material.SetFloat("_DesktopDepth", _sample.depth);
                    break;
            }
        }

        private static float FiniteGain(float value) => float.IsNaN(value) || float.IsInfinity(value) ? 1f : Mathf.Clamp(value, 0f, 1.3f);

        private void MeasurePose()
        {
            Vector3 position = _eye.position;
            Quaternion rotation = _eye.rotation;
            float dt = Time.unscaledDeltaTime;
            if (_hasPose && dt > 0f && dt < 0.25f && OVRManager.hasVrFocus)
            {
                float weight = 1f - Mathf.Exp(-10f * dt);
                _sample.angularDegPerSec = Mathf.Lerp(_sample.angularDegPerSec, Quaternion.Angle(_lastRotation, rotation) / dt, weight);
                _sample.linearMetersPerSec = Mathf.Lerp(_sample.linearMetersPerSec, Vector3.Distance(_lastPosition, position) / dt, weight);
            }
            _lastPosition = position;
            _lastRotation = rotation;
            _hasPose = true;
        }

        private void OnApplicationPause(bool paused)
        {
            _hasPose = false;
            _sample.angularDegPerSec = 0f;
            _sample.linearMetersPerSec = 0f;
        }
    }
}
#endif
