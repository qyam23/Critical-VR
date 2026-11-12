using System;
using System.Collections;
using Meta.XR;
using UnityEngine;
using UnityEngine.Android;
using UnityEngine.Rendering;

namespace CriticalVR
{
    /// <summary>
    /// GPU-only, two-camera experimental overlay. The compositor remains the baseline.
    /// Calibrated projection uses a reference distance, NOT a reconstructed room mesh.
    /// Near objects and disocclusions therefore still require headset evaluation.
    /// </summary>
    [DefaultExecutionOrder(100)]
    public sealed class QuestPassthroughCameraBridge : MonoBehaviour
    {
        private const string CameraPermission = OVRPermissionsRequester.PassthroughCameraAccessPermission;
        [SerializeField] private Material effectMaterial;
        [SerializeField] private MotionToExperienceManager experience;
        [SerializeField] private Camera eyeCamera;
        [SerializeField] private UnityEngine.UI.Text statusText;
        [SerializeField, Range(1f, 5f)] private float referenceDistance = 2f;
        [SerializeField, Range(0f, 0.6f)] private float maximumOpacity = 0.45f;
        [SerializeField, Range(0.1f, 0.5f)] private float staleFrameTimeout = 0.25f;

        private readonly PassthroughCameraAccess[] _feeds = new PassthroughCameraAccess[2];
        private readonly float[] _lastFrames = new float[2];
        private PermissionCallbacks _permissionCallbacks;
        private bool _focused = true;
        private bool _permissionPending;
        private bool _starting;
        private float _startedAt;
        private string _status;
        public string Status => _status;
        public bool StereoFeedReady { get; private set; }

        private void OnEnable()
        {
            if (effectMaterial != null)
            {
                effectMaterial.SetFloat("_PassthroughMode", 1f);
                effectMaterial.SetFloat("_CameraFeedReady", 0f);
            }
            if (experience != null)
            {
                experience.ResetRequested += StopFeeds;
                experience.ResumeRequested += TryStart;
            }
            RenderPipelineManager.beginCameraRendering += BeforeCameraRendering;
        }

        private IEnumerator Start()
        {
#if UNITY_ANDROID && DEVELOPMENT_BUILD && !UNITY_EDITOR
            gameObject.AddComponent<QuestDesktopLink>().Initialize(experience, eyeCamera, this, effectMaterial);
#endif
            // Reveal the compositor underlay before showing a camera permission prompt.
            while (experience != null && experience.SessionReveal < 1f) yield return null;
            TryStart();
        }

        public void TryStart()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            if (!_focused || _permissionPending || effectMaterial == null || experience == null ||
                experience.EffectsSuspended || experience.SessionReveal < 1f || eyeCamera == null) return;
            try
            {
                if (!PassthroughCameraAccess.IsSupported)
                {
                    experience.ResetExperience();
                    SetStatus("Unsupported device/OS. Native passthrough only; Quest 3/3S and compatible Horizon OS required.");
                    return;
                }
                if (!Permission.HasUserAuthorizedPermission(CameraPermission))
                {
                    _permissionPending = true;
                    SetStatus("Camera permission requested. Native passthrough remains visible.");
                    _permissionCallbacks = new PermissionCallbacks();
                    _permissionCallbacks.PermissionGranted += PermissionGranted;
                    _permissionCallbacks.PermissionDenied += PermissionDenied;
                    Permission.RequestUserPermission(CameraPermission, _permissionCallbacks);
                    return;
                }
                if (_feeds[0] == null) CreateFeeds();
                for (int i = 0; i < 2; i++)
                {
                    _lastFrames[i] = float.NegativeInfinity;
                    _feeds[i].enabled = true;
                }
                _starting = true;
                _startedAt = Time.realtimeSinceStartup;
                SetStatus("Starting independent left/right cameras.");
            }
            catch (Exception exception)
            {
                FailClosed("Camera initialization failed. Native passthrough only; A/X retries: " + exception.Message);
            }
#else
            SetStatus("Device cameras are disabled in Editor. Use the synthetic room for preview.");
#endif
        }

        private void CreateFeeds()
        {
            for (int i = 0; i < 2; i++)
            {
                var host = new GameObject(i == 0 ? "LeftPassthroughCamera" : "RightPassthroughCamera");
                host.SetActive(false);
                host.transform.SetParent(transform, false);
                var feed = host.AddComponent<PassthroughCameraAccess>();
                feed.enabled = false;
                feed.CameraPosition = i == 0 ? PassthroughCameraAccess.CameraPositionType.Left : PassthroughCameraAccess.CameraPositionType.Right;
                feed.RequestedResolution = new Vector2Int(1280, 960);
                feed.MaxFramerate = 60;
                host.SetActive(true);
                _feeds[i] = feed;
            }
        }

        private void PermissionGranted(string permission)
        {
            _permissionPending = false;
            // Permission dialogs can change focus. Never resume effects implicitly.
            if (_focused && !experience.EffectsSuspended) TryStart();
            else SetStatus("Permission granted. Hold right pinch or press A/X to resume.");
        }

        private void PermissionDenied(string permission)
        {
            _permissionPending = false;
            StopFeeds();
            experience.ResetExperience();
            SetStatus("Camera permission denied. Native passthrough only. A/X retries explicitly.");
        }

        private void Update()
        {
            if (statusText != null)
            {
                statusText.gameObject.SetActive(experience.SessionReveal >= 1f &&
                    (!StereoFeedReady || experience.EffectsSuspended || Time.realtimeSinceStartup - _startedAt < 15f));
                statusText.text = (experience.EffectsSuspended ? "Effects stopped. Your room is visible." :
                    StereoFeedReady ? "Critical-VR | Experimental camera effects" : "Your room is visible. Waiting for camera access.") +
                    "\nLeft pinch: stop | Hold right pinch: resume" +
                    "\nControllers: B/Y stop | A/X resume";
            }
            if (!_starting) return;
            if (!OVRManager.hasVrFocus || !OVRManager.hasInputFocus || !OVRManager.isHmdPresent)
            {
                FailClosed("Headset tracking/input focus lost. Press A/X to resume.");
                return;
            }
            if (!_focused || experience.EffectsSuspended || !Permission.HasUserAuthorizedPermission(CameraPermission))
            {
                FailClosed("Camera permission/focus lost. Press A/X to resume.");
                return;
            }
            bool ready = true;
            for (int i = 0; i < 2; i++)
            {
                var feed = _feeds[i];
                if (feed != null && feed.IsUpdatedThisFrame) _lastFrames[i] = Time.realtimeSinceStartup;
                ready &= feed != null && feed.IsPlaying && Time.realtimeSinceStartup - _lastFrames[i] <= staleFrameTimeout;
            }
            if (!ready)
            {
                effectMaterial.SetFloat("_CameraFeedReady", 0f);
                if (StereoFeedReady || Time.realtimeSinceStartup - _startedAt > 10f)
                    FailClosed("Camera stream missing/stale. Native passthrough only. Press A/X to retry.");
                return;
            }
            // Never show one eye alone or pair frames from substantially different times.
            if (Math.Abs((_feeds[0].Timestamp - _feeds[1].Timestamp).TotalSeconds) > 0.05)
            {
                // The two streams can deliver their first frames at different times.
                // Keep native passthrough visible until a synchronized pair arrives.
                if (StereoFeedReady || Time.realtimeSinceStartup - _startedAt > 10f)
                    FailClosed("Left/right camera timestamps diverged. Press A/X to retry.");
                return;
            }
            if (!StereoFeedReady)
            {
                SetStatus($"Stereo camera textures ready: L {_feeds[0].CurrentResolution}, R {_feeds[1].CurrentResolution}. Reference-distance projection; depth alignment unverified.");
                StereoFeedReady = true;
            }
        }

        private void BeforeCameraRendering(ScriptableRenderContext context, Camera camera)
        {
            if (camera != eyeCamera || !StereoFeedReady || !_starting) return;
            for (int i = 0; i < 2; i++)
            {
                var feed = _feeds[i];
                if (!feed.IsPlaying || Time.realtimeSinceStartup - _lastFrames[i] > staleFrameTimeout)
                {
                    effectMaterial.SetFloat("_CameraFeedReady", 0f);
                    return;
                }
                var texture = feed.GetTexture();
                if (texture == null) { FailClosed("Camera texture lost. Native passthrough only."); return; }
                string suffix = i == 0 ? "Left" : "Right";
                effectMaterial.SetTexture("_Camera" + suffix, texture);
                // Use the SDK's own crop and principal-point calculation, rather than assuming centered optics.
                Pose pose = feed.GetCameraPose();
                Vector2 origin = feed.WorldToViewportPoint(pose.position + pose.rotation * Vector3.forward, pose);
                Vector2 horizontal = feed.WorldToViewportPoint(pose.position + pose.rotation * new Vector3(1f, 0f, 1f), pose);
                Vector2 vertical = feed.WorldToViewportPoint(pose.position + pose.rotation * new Vector3(0f, 1f, 1f), pose);
                effectMaterial.SetVector("_CameraCalibration" + suffix,
                    new Vector4(horizontal.x - origin.x, vertical.y - origin.y, origin.x, origin.y));
                effectMaterial.SetMatrix("_CameraWorldToLocal" + suffix,
                    Matrix4x4.TRS(pose.position, pose.rotation, Vector3.one).inverse);
            }
            effectMaterial.SetFloat("_CameraReferenceDistance", referenceDistance);
            effectMaterial.SetFloat("_CameraMaximumOpacity", maximumOpacity);
            effectMaterial.SetFloat("_CameraFeedReady", 1f);
        }

        private void FailClosed(string message)
        {
            StopFeeds();
            experience.ResetExperience();
            SetStatus(message);
        }

        private void StopFeeds()
        {
            _starting = false;
            StereoFeedReady = false;
            if (effectMaterial != null)
            {
                effectMaterial.SetFloat("_CameraFeedReady", 0f);
                effectMaterial.SetTexture("_CameraLeft", null);
                effectMaterial.SetTexture("_CameraRight", null);
            }
            foreach (var feed in _feeds) if (feed != null) feed.enabled = false;
        }

        private void OnApplicationFocus(bool focused)
        {
            _focused = focused;
            if (!focused) StopFeeds();
        }

        private void OnApplicationPause(bool paused)
        {
            if (paused)
            {
                StopFeeds();
                if (experience != null && experience.SessionReveal >= 1f) experience.ResetExperience();
            }
        }

        private void OnDisable()
        {
            StopFeeds();
            RenderPipelineManager.beginCameraRendering -= BeforeCameraRendering;
            if (experience != null)
            {
                experience.ResetRequested -= StopFeeds;
                experience.ResumeRequested -= TryStart;
            }
            if (_permissionCallbacks != null)
            {
                _permissionCallbacks.PermissionGranted -= PermissionGranted;
                _permissionCallbacks.PermissionDenied -= PermissionDenied;
            }
        }

        private void SetStatus(string status)
        {
            if (_status == status) return;
            _status = status;
            Debug.Log("[CriticalVR.Camera] " + status, this);
        }
    }
}
