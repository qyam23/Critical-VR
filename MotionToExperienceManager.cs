using System.Collections;
using UnityEngine;

/// <summary>
/// Samples CenterEyeAnchor motion and converts it into a comfort-safe
/// IntensityScore for Passthrough-driven URP effects on Meta Quest.
/// </summary>
public class MotionToExperienceManager : MonoBehaviour
{
    public enum ExperienceState
    {
        Calm,
        Shift,
        Intense,
        Recovery,
    }

    [Header("References")]
    [Tooltip("CenterEyeAnchor from OVRCameraRig.")]
    [SerializeField] private Transform centerEyeAnchor;
    [Tooltip("Material that drives the URP full-screen or overlay effect.")]
    [SerializeField] private Material effectMaterial;

    [Header("Motion Weights")]
    [SerializeField, Min(0f)] private float linearAccelerationWeight = 0.45f;
    [SerializeField, Min(0f)] private float angularVelocityWeight = 0.65f;
    [SerializeField, Min(0.001f)] private float linearNormalization = 3.5f;
    [SerializeField, Min(0.001f)] private float angularNormalization = 120f;

    [Header("Score Behavior")]
    [SerializeField, Range(0.05f, 3f)] private float sensitivity = 1f;
    [SerializeField, Range(0.05f, 4f)] private float decayRate = 1.25f;
    [SerializeField, Range(4, 32)] private int movingAverageSamples = 12;
    [SerializeField, Range(0.1f, 1f)] private float enterThreshold = 0.18f;
    [SerializeField, Range(0.01f, 0.5f)] private float exitThreshold = 0.08f;

    [Header("Transitions")]
    [SerializeField, Min(1.5f)] private float fadeDuration = 1.5f;
    [SerializeField, Range(0f, 0.05f)] private float maxDistortionUv = 0.05f;
    [SerializeField, Range(0f, 0.03f)] private float maxChromaticAmount = 0.018f;
    [SerializeField, Range(0f, 1f)] private float maxPeripheralBlur = 0.6f;

    [Header("Debug")]
    [SerializeField] private bool useDebugSlider;
    [SerializeField, Range(0f, 1f)] private float debugIntensity;

    private readonly float[] _motionSamples = new float[32];
    private int _sampleCount;
    private int _sampleIndex;
    private float _sampleSum;

    private Vector3 _lastPosition;
    private Quaternion _lastRotation;
    private Vector3 _lastVelocity;
    private float _intensityScore;
    private float _lastIntensityScore;
    private float _smoothedMotion;
    private float _edgeGlow;
    private float _chromaticLayer;
    private float _distortionLayer;
    private ExperienceState _currentState = ExperienceState.Calm;
    private bool _experienceActive;
    private bool _isResetting;

    public float IntensityScore => _intensityScore;
    public ExperienceState CurrentState => _currentState;

    private void Awake()
    {
        if (centerEyeAnchor == null && Camera.main != null)
        {
            centerEyeAnchor = Camera.main.transform;
        }
    }

    private void Start()
    {
        if (centerEyeAnchor == null)
        {
            Debug.LogError("MotionToExperienceManager requires a CenterEyeAnchor reference.");
            enabled = false;
            return;
        }

        _lastPosition = centerEyeAnchor.position;
        _lastRotation = centerEyeAnchor.rotation;
    }

    private void Update()
    {
        HandleResetInput();

        if (_isResetting)
        {
            ApplyMaterialProperties();
            return;
        }

        StepMotion(Time.deltaTime);
        UpdateStateMachine(Time.deltaTime);
        UpdateLayers(Time.deltaTime);
        ApplyMaterialProperties();
    }

    private void StepMotion(float dt)
    {
        if (dt <= 0f)
        {
            return;
        }

        if (useDebugSlider)
        {
            _smoothedMotion = debugIntensity;
            _intensityScore = Mathf.MoveTowards(_intensityScore, debugIntensity, dt / fadeDuration);
            _experienceActive = _intensityScore > exitThreshold;
            return;
        }

        Vector3 currentPosition = centerEyeAnchor.position;
        Quaternion currentRotation = centerEyeAnchor.rotation;

        Vector3 currentVelocity = (currentPosition - _lastPosition) / dt;
        float linearAcceleration = (currentVelocity - _lastVelocity).magnitude / Mathf.Max(dt, 0.0001f);
        float angularVelocity = Quaternion.Angle(currentRotation, _lastRotation) / Mathf.Max(dt, 0.0001f);

        _lastPosition = currentPosition;
        _lastRotation = currentRotation;
        _lastVelocity = currentVelocity;

        float normalizedLinear = Mathf.Clamp01(linearAcceleration / linearNormalization);
        float normalizedAngular = Mathf.Clamp01(angularVelocity / angularNormalization);
        float rawMotion = Mathf.Clamp01((normalizedLinear * linearAccelerationWeight) + (normalizedAngular * angularVelocityWeight));

        AddMotionSample(rawMotion);
        float averageMotion = _sampleCount > 0 ? _sampleSum / _sampleCount : 0f;
        _smoothedMotion = Mathf.Lerp(_smoothedMotion, averageMotion, 1f - Mathf.Exp(-6f * dt));

        if (!_experienceActive && _smoothedMotion >= enterThreshold)
        {
            _experienceActive = true;
        }
        else if (_experienceActive && _smoothedMotion <= exitThreshold && _intensityScore < 0.05f)
        {
            _experienceActive = false;
        }

        if (_experienceActive)
        {
            _intensityScore += _smoothedMotion * sensitivity * dt;
        }

        _intensityScore *= Mathf.Exp(-decayRate * dt);
        _intensityScore = Mathf.Clamp01(_intensityScore);
    }

    private void UpdateStateMachine(float dt)
    {
        float trend = (_intensityScore - _lastIntensityScore) / Mathf.Max(dt, 0.0001f);
        _lastIntensityScore = _intensityScore;

        if (_intensityScore < 0.12f)
        {
            _currentState = ExperienceState.Calm;
        }
        else if (trend < -0.2f && _intensityScore > 0.22f)
        {
            _currentState = ExperienceState.Recovery;
        }
        else if (_intensityScore < 0.45f)
        {
            _currentState = ExperienceState.Shift;
        }
        else
        {
            _currentState = ExperienceState.Intense;
        }
    }

    private void UpdateLayers(float dt)
    {
        float edgeTarget = Mathf.SmoothStep(0f, 1f, _intensityScore);
        float chromaticTarget = Mathf.SmoothStep(0f, 1f, Mathf.Max(0f, (_intensityScore - 0.18f) / 0.82f));
        float distortionTarget = Mathf.SmoothStep(0f, 1f, Mathf.Max(0f, (_intensityScore - 0.38f) / 0.62f));

        _edgeGlow = Mathf.Lerp(_edgeGlow, edgeTarget, 1f - Mathf.Exp(-3.5f * dt));
        _chromaticLayer = Mathf.Lerp(_chromaticLayer, chromaticTarget, 1f - Mathf.Exp(-2.75f * dt));
        _distortionLayer = Mathf.Lerp(_distortionLayer, distortionTarget, 1f - Mathf.Exp(-2.25f * dt));
    }

    private void ApplyMaterialProperties()
    {
        if (effectMaterial == null)
        {
            return;
        }

        effectMaterial.SetFloat("_IntensityScore", _intensityScore);
        effectMaterial.SetFloat("_MotionSignal", _smoothedMotion);
        effectMaterial.SetFloat("_EdgeGlow", _edgeGlow);
        effectMaterial.SetFloat("_ChromaticAmount", _chromaticLayer * maxChromaticAmount);
        effectMaterial.SetFloat("_DistortionAmount", _distortionLayer * maxDistortionUv);
        effectMaterial.SetFloat("_PeripheralBlur", _chromaticLayer * maxPeripheralBlur);

        Color edgeColor = Color.Lerp(
            new Color(0.45f, 0.9f, 1f, 1f),
            new Color(1f, 0.35f, 0.85f, 1f),
            _intensityScore);
        effectMaterial.SetColor("_EdgeColor", edgeColor);
        effectMaterial.SetFloat("_StateIndex", (float)_currentState);
    }

    private void HandleResetInput()
    {
        bool resetPressed = false;

        #if UNITY_EDITOR
        if (Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.B) || Input.GetKeyDown(KeyCode.Y))
        {
            resetPressed = true;
        }
        #endif

        if (OVRInput.GetActiveController() != OVRInput.Controller.None)
        {
            if (OVRInput.GetDown(OVRInput.Button.Two) || OVRInput.GetDown(OVRInput.Button.Four))
            {
                resetPressed = true;
            }
        }

        if (resetPressed && !_isResetting)
        {
            ResetExperience();
        }
    }

    public void ResetExperience()
    {
        StopAllCoroutines();
        StartCoroutine(FadeOutRoutine());
    }

    private IEnumerator FadeOutRoutine()
    {
        _isResetting = true;

        float startScore = _intensityScore;
        float startEdge = _edgeGlow;
        float startChromatic = _chromaticLayer;
        float startDistortion = _distortionLayer;
        float elapsed = 0f;

        while (elapsed < fadeDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / fadeDuration);

            _intensityScore = Mathf.Lerp(startScore, 0f, t);
            _edgeGlow = Mathf.Lerp(startEdge, 0f, t);
            _chromaticLayer = Mathf.Lerp(startChromatic, 0f, t);
            _distortionLayer = Mathf.Lerp(startDistortion, 0f, t);

            ApplyMaterialProperties();
            yield return null;
        }

        _intensityScore = 0f;
        _smoothedMotion = 0f;
        _sampleCount = 0;
        _sampleIndex = 0;
        _sampleSum = 0f;
        _edgeGlow = 0f;
        _chromaticLayer = 0f;
        _distortionLayer = 0f;
        _experienceActive = false;
        _isResetting = false;
        _currentState = ExperienceState.Calm;

        for (int i = 0; i < _motionSamples.Length; i++)
        {
            _motionSamples[i] = 0f;
        }
    }

    private void AddMotionSample(float sample)
    {
        int window = Mathf.Clamp(movingAverageSamples, 1, _motionSamples.Length);

        if (_sampleCount < window)
        {
            _sampleCount++;
        }
        else
        {
            _sampleSum -= _motionSamples[_sampleIndex];
        }

        _motionSamples[_sampleIndex] = sample;
        _sampleSum += sample;
        _sampleIndex = (_sampleIndex + 1) % window;
    }

    [ContextMenu("Trigger Reset")]
    private void DebugReset()
    {
        ResetExperience();
    }
}
