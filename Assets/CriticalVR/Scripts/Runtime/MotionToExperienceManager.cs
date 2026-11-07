using System.Collections;
using UnityEngine;
using UnityEngine.XR;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// Converts headset motion into a comfort-aware intensity model for Quest passthrough effects.
/// The effect should bloom gradually, hold after strong peaks, and dissolve without obvious edges.
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

    public enum ExperienceMode
    {
        Psychedelic,
        Empathic,
    }

    private static class ShaderIds
    {
        public static readonly int IntensityScore = Shader.PropertyToID("_IntensityScore");
        public static readonly int MotionSignal = Shader.PropertyToID("_MotionSignal");
        public static readonly int EdgeGlow = Shader.PropertyToID("_EdgeGlow");
        public static readonly int DistortionAmount = Shader.PropertyToID("_DistortionAmount");
        public static readonly int PatternAmount = Shader.PropertyToID("_PatternAmount");
        public static readonly int ChromaticAmount = Shader.PropertyToID("_ChromaticAmount");
        public static readonly int ColorAtmosphere = Shader.PropertyToID("_ColorAtmosphere");
        public static readonly int PeripheralBlur = Shader.PropertyToID("_PeripheralBlur");
        public static readonly int BreathingAmount = Shader.PropertyToID("_BreathingAmount");
        public static readonly int BreathingPhase = Shader.PropertyToID("_BreathingPhase");
        public static readonly int AfterImageAmount = Shader.PropertyToID("_AfterImageAmount");
        public static readonly int OverdriveAmount = Shader.PropertyToID("_OverdriveAmount");
        public static readonly int PeripheralWeight = Shader.PropertyToID("_PeripheralWeight");
        public static readonly int CenterStability = Shader.PropertyToID("_CenterStability");
        public static readonly int IntroFade = Shader.PropertyToID("_IntroFade");
        public static readonly int PeakHold = Shader.PropertyToID("_PeakHold");
        public static readonly int StateIndex = Shader.PropertyToID("_StateIndex");
        public static readonly int ModeIndex = Shader.PropertyToID("_ModeIndex");
        public static readonly int EdgeColor = Shader.PropertyToID("_EdgeColor");
        public static readonly int AtmosphereColor = Shader.PropertyToID("_AtmosphereColor");
    }

    [Header("References")]
    [Tooltip("CenterEyeAnchor from OVRCameraRig. Falls back to Camera.main when left empty.")]
    [SerializeField] private Transform centerEyeAnchor;
    [Tooltip("Material used by the full-screen pass or preview overlay.")]
    [SerializeField] private Material effectMaterial;

    [Header("Experience")]
    [SerializeField] private ExperienceMode mode = ExperienceMode.Psychedelic;
    [SerializeField, Min(5f)] private float sessionIntroDuration = 10f;
    [SerializeField, Range(0.35f, 1f)] private float centerStabilityAtPeak = 0.7f;
    [SerializeField, Range(0.5f, 1.25f)] private float peripheralWeightAtPeak = 0.98f;

    [Header("Motion Analysis")]
    [SerializeField, Min(0f)] private float linearAccelerationWeight = 0.48f;
    [SerializeField, Min(0f)] private float angularVelocityWeight = 0.68f;
    [SerializeField, Min(0.001f)] private float linearNormalization = 3.25f;
    [SerializeField, Min(0.001f)] private float angularNormalization = 135f;
    [SerializeField, Range(6, 48)] private int movingAverageSamples = 18;
    [SerializeField, Min(0.1f)] private float motionHistoryRise = 1.25f;
    [SerializeField, Min(0.05f)] private float motionHistoryFall = 0.22f;
    [SerializeField, Min(0.1f)] private float burstGain = 0.22f;
    [SerializeField, Min(0.2f)] private float burstDecay = 1.8f;

    [Header("Intensity Model")]
    [SerializeField, Range(0.1f, 3f)] private float sensitivity = 1.15f;
    [SerializeField, Range(0.05f, 3f)] private float baseDecayRate = 0.92f;
    [SerializeField, Range(0.05f, 0.6f)] private float enterThreshold = 0.14f;
    [SerializeField, Range(0.01f, 0.3f)] private float exitThreshold = 0.05f;
    [SerializeField, Range(0.1f, 1f)] private float activationFloor = 0.11f;
    [SerializeField, Min(0.5f)] private float intenseHoldDuration = 4.5f;

    [Header("Safety And Layer Caps")]
    [SerializeField, Min(1.5f)] private float safetyFadeDuration = 1.5f;
    [SerializeField, Range(0f, 0.05f)] private float maxDistortionUv = 0.05f;
    [SerializeField, Range(0f, 0.04f)] private float maxChromaticAmount = 0.022f;
    [SerializeField, Range(0f, 1f)] private float maxPatternAmount = 0.85f;
    [SerializeField, Range(0f, 1f)] private float maxColorAtmosphere = 0.95f;
    [SerializeField, Range(0f, 1f)] private float maxPeripheralBlur = 0.68f;
    [SerializeField, Range(0f, 1f)] private float maxBreathingAmount = 0.28f;
    [SerializeField, Range(0f, 1f)] private float maxAfterImageAmount = 0.38f;

    [Header("Debug")]
    [SerializeField] private bool useDebugSlider;
    [SerializeField, Range(0f, 1f)] private float debugIntensity = 0.45f;

    private readonly float[] _motionSamples = new float[48];
    private int _sampleCount;
    private int _sampleIndex;
    private float _sampleSum;

    private Vector3 _lastPosition;
    private Quaternion _lastRotation;
    private Vector3 _lastVelocity;
    private float _smoothedMotion;
    private float _motionHistory;
    private float _burstEnergy;
    private float _activationBlend;
    private float _sessionElapsed;
    private float _intensityScore;
    private float _lastIntensityScore;
    private float _trend;
    private float _peakHoldTimer;
    private float _distortionLayer;
    private float _patternLayer;
    private float _colorLayer;
    private float _breathingLayer;
    private float _edgeGlow;
    private float _afterImageLayer;
    private float _overdriveLayer;
    private float _peripheralWeight;
    private float _centerStability;
    private float _breathingPhase;
    private float _linearAcceleration;
    private float _angularVelocity;
    private float _introFade = 1f;
    private bool _isResetting;
    private bool _experienceActive;
    private bool _hasBeenIntense;
    private bool _secondaryWasPressed;
    private bool _primaryWasPressed;
    public bool EffectsSuspended { get; private set; }
    public event System.Action ResetRequested;
    public event System.Action ResumeRequested;
    private ExperienceState _currentState = ExperienceState.Calm;

    public float IntensityScore => _intensityScore;
    public ExperienceState CurrentState => _currentState;
    public float SessionReveal => 1f - _introFade;

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
        _peripheralWeight = 0.35f;
        _centerStability = 1f;
        ApplyMaterialProperties();
    }

    private void Update()
    {
        HandleResetInput();

        if (_isResetting)
        {
            ApplyMaterialProperties();
            return;
        }

        StepSessionIntro(Time.deltaTime);
        if (EffectsSuspended)
        {
            RebaseMotion();
            ApplyMaterialProperties();
            return;
        }
        StepMotion(Time.deltaTime);
        UpdateStateMachine(Time.deltaTime);
        UpdateVisualLayers(Time.deltaTime);
        ApplyMaterialProperties();
    }

    private void StepSessionIntro(float dt)
    {
        _sessionElapsed += dt;
        float reveal = Mathf.Clamp01(_sessionElapsed / Mathf.Max(0.01f, sessionIntroDuration));
        _introFade = 1f - reveal;
    }

    private void StepMotion(float dt)
    {
        if (dt <= 0f)
        {
            return;
        }

        if (_introFade > 0f)
        {
            RebaseMotion();
            _smoothedMotion = Mathf.Lerp(_smoothedMotion, 0f, 1f - Mathf.Exp(-4f * dt));
            _motionHistory = Mathf.Lerp(_motionHistory, 0f, 1f - Mathf.Exp(-3f * dt));
            _burstEnergy = Mathf.Lerp(_burstEnergy, 0f, 1f - Mathf.Exp(-4f * dt));
            _intensityScore = Mathf.Lerp(_intensityScore, 0f, 1f - Mathf.Exp(-2.4f * dt));
            return;
        }

        if (useDebugSlider)
        {
            _smoothedMotion = ExpLerp(_smoothedMotion, debugIntensity, 4.5f, dt);
            _motionHistory = ExpLerp(_motionHistory, debugIntensity, 3.2f, dt);
            _activationBlend = ExpLerp(_activationBlend, debugIntensity > enterThreshold ? 1f : 0f, 2.6f, dt);
            _experienceActive = debugIntensity > exitThreshold;
            _intensityScore = ExpLerp(_intensityScore, debugIntensity, 2.4f, dt);
            return;
        }

        Vector3 currentPosition = centerEyeAnchor.position;
        Quaternion currentRotation = centerEyeAnchor.rotation;

        Vector3 currentVelocity = (currentPosition - _lastPosition) / dt;
        _linearAcceleration = (currentVelocity - _lastVelocity).magnitude / Mathf.Max(dt, 0.0001f);
        _angularVelocity = Quaternion.Angle(currentRotation, _lastRotation) / Mathf.Max(dt, 0.0001f);

        _lastPosition = currentPosition;
        _lastRotation = currentRotation;
        _lastVelocity = currentVelocity;

        float normalizedLinear = Mathf.Clamp01(_linearAcceleration / linearNormalization);
        float normalizedAngular = Mathf.Clamp01(_angularVelocity / angularNormalization);
        float rawMotion = Mathf.Clamp01((normalizedLinear * linearAccelerationWeight) + (normalizedAngular * angularVelocityWeight));
        float burstContribution = Mathf.Max(0f, rawMotion - _smoothedMotion);

        AddMotionSample(rawMotion);
        float averagedMotion = _sampleCount > 0 ? _sampleSum / _sampleCount : 0f;
        _smoothedMotion = ExpLerp(_smoothedMotion, averagedMotion, 5.5f, dt);

        float historyTarget = Mathf.Clamp01((_smoothedMotion * 0.7f) + (rawMotion * 0.3f));
        float historySpeed = historyTarget > _motionHistory ? motionHistoryRise : motionHistoryFall;
        _motionHistory = ExpLerp(_motionHistory, historyTarget, historySpeed, dt);

        _burstEnergy = Mathf.Max(_burstEnergy * Mathf.Exp(-burstDecay * dt), burstContribution * burstGain);

        float activationTarget = Mathf.Clamp01((_motionHistory - activationFloor) / Mathf.Max(0.001f, 1f - activationFloor));
        _activationBlend = ExpLerp(_activationBlend, activationTarget, activationTarget > _activationBlend ? 1.15f : 0.28f, dt);

        if (!_experienceActive && _activationBlend >= enterThreshold)
        {
            _experienceActive = true;
        }
        else if (_experienceActive && _activationBlend <= exitThreshold && _intensityScore < 0.04f)
        {
            _experienceActive = false;
        }

        if (_experienceActive)
        {
            float gain = sensitivity * Mathf.Lerp(0.4f, 1f, _activationBlend);
            float targetScore = Mathf.Clamp01((_motionHistory * 0.68f) + (_activationBlend * 0.22f) + _burstEnergy);
            _intensityScore = ExpLerp(_intensityScore, targetScore, Mathf.Lerp(0.65f, 1.45f, gain), dt);
        }

        float decayScale = 1f;
        if (_currentState == ExperienceState.Recovery)
        {
            decayScale = 0.42f;
        }
        else if (_currentState == ExperienceState.Intense || _peakHoldTimer > 0f)
        {
            decayScale = 0.28f;
        }
        else if (_currentState == ExperienceState.Shift)
        {
            decayScale = 0.55f;
        }

        _intensityScore *= Mathf.Exp(-(baseDecayRate * decayScale) * dt);

        if (rawMotion >= 0.78f && _intensityScore >= 0.78f)
        {
            _peakHoldTimer = intenseHoldDuration;
            _hasBeenIntense = true;
        }
        else if (_peakHoldTimer > 0f)
        {
            _peakHoldTimer = Mathf.Max(0f, _peakHoldTimer - dt);
            _intensityScore = Mathf.Max(_intensityScore, 0.8f - ((intenseHoldDuration - _peakHoldTimer) * 0.03f));
        }

        _intensityScore = Mathf.Clamp01(_intensityScore);
    }

    private void UpdateStateMachine(float dt)
    {
        _trend = (_intensityScore - _lastIntensityScore) / Mathf.Max(dt, 0.0001f);
        _lastIntensityScore = _intensityScore;

        bool inRecoveryBand = _hasBeenIntense && (_trend < -0.04f || _peakHoldTimer <= 0f) && _intensityScore > 0.18f;

        if (_intensityScore < 0.08f && _motionHistory < 0.09f)
        {
            _currentState = ExperienceState.Calm;
            if (_intensityScore < 0.03f)
            {
                _hasBeenIntense = false;
            }
        }
        else if (inRecoveryBand)
        {
            _currentState = ExperienceState.Recovery;
        }
        else if (_intensityScore < 0.34f)
        {
            _currentState = ExperienceState.Shift;
        }
        else
        {
            _currentState = ExperienceState.Intense;
        }
    }

    private void UpdateVisualLayers(float dt)
    {
        float introReveal = 1f - _introFade;
        float distortionTarget = introReveal * Mathf.Clamp01((_intensityScore - 0.08f) / 0.62f);
        float patternTarget = introReveal * Mathf.Clamp01((_intensityScore - 0.22f) / 0.58f);
        float colorTarget = introReveal * Mathf.Clamp01(Mathf.Pow(_intensityScore, mode == ExperienceMode.Psychedelic ? 0.78f : 0.92f));
        float breathingTarget = introReveal * Mathf.Clamp01((_intensityScore - 0.14f) / 0.66f);
        float edgeTarget = introReveal * Mathf.Clamp01(Mathf.Lerp(_motionHistory, _intensityScore, 0.55f));
        float afterImageTarget = introReveal * Mathf.Clamp01((_patternLayer * 0.55f) + (_colorLayer * 0.3f) + (_burstEnergy * 0.8f));
        float overdriveTarget = (_intensityScore >= 0.78f || _peakHoldTimer > 0f)
            ? Mathf.Clamp01((Mathf.Max(_intensityScore, 0.8f) - 0.78f) / 0.22f)
            : 0f;

        _distortionLayer = SmoothLayer(_distortionLayer, distortionTarget, 1.45f, 0.52f, dt);
        _patternLayer = SmoothLayer(_patternLayer, patternTarget, 0.88f, 0.22f, dt);
        _colorLayer = SmoothLayer(_colorLayer, colorTarget, 0.62f, 0.14f, dt);
        _breathingLayer = SmoothLayer(_breathingLayer, breathingTarget, 0.38f, 0.09f, dt);
        _edgeGlow = SmoothLayer(_edgeGlow, edgeTarget, 1.15f, 0.2f, dt);
        _afterImageLayer = SmoothLayer(_afterImageLayer, afterImageTarget, 0.44f, 0.15f, dt);
        _overdriveLayer = SmoothLayer(_overdriveLayer, overdriveTarget, 0.95f, 0.18f, dt);

        _breathingPhase += dt * Mathf.Lerp(mode == ExperienceMode.Psychedelic ? 0.78f : 0.58f, 1.85f, _breathingLayer);
        _peripheralWeight = Mathf.Lerp(_peripheralWeight, Mathf.Lerp(0.35f, peripheralWeightAtPeak, _colorLayer), 1f - Mathf.Exp(-0.8f * dt));
        _centerStability = Mathf.Lerp(_centerStability, Mathf.Lerp(1f, centerStabilityAtPeak, _colorLayer), 1f - Mathf.Exp(-0.7f * dt));
    }

    private void ApplyMaterialProperties()
    {
        if (effectMaterial == null)
        {
            return;
        }

        effectMaterial.SetFloat(ShaderIds.IntensityScore, _intensityScore);
        effectMaterial.SetFloat(ShaderIds.MotionSignal, _smoothedMotion);
        effectMaterial.SetFloat(ShaderIds.EdgeGlow, _edgeGlow);
        effectMaterial.SetFloat(ShaderIds.DistortionAmount, _distortionLayer * maxDistortionUv);
        effectMaterial.SetFloat(ShaderIds.PatternAmount, _patternLayer * maxPatternAmount);
        effectMaterial.SetFloat(ShaderIds.ChromaticAmount, _edgeGlow * maxChromaticAmount);
        effectMaterial.SetFloat(ShaderIds.ColorAtmosphere, _colorLayer * maxColorAtmosphere);
        effectMaterial.SetFloat(ShaderIds.PeripheralBlur, _colorLayer * maxPeripheralBlur);
        effectMaterial.SetFloat(ShaderIds.BreathingAmount, _breathingLayer * maxBreathingAmount);
        effectMaterial.SetFloat(ShaderIds.BreathingPhase, _breathingPhase);
        effectMaterial.SetFloat(ShaderIds.AfterImageAmount, _afterImageLayer * maxAfterImageAmount);
        effectMaterial.SetFloat(ShaderIds.OverdriveAmount, _overdriveLayer);
        effectMaterial.SetFloat(ShaderIds.PeripheralWeight, _peripheralWeight);
        effectMaterial.SetFloat(ShaderIds.CenterStability, _centerStability);
        effectMaterial.SetFloat(ShaderIds.IntroFade, _introFade);
        effectMaterial.SetFloat(ShaderIds.PeakHold, Mathf.Clamp01(_peakHoldTimer / Mathf.Max(0.01f, intenseHoldDuration)));
        effectMaterial.SetFloat(ShaderIds.StateIndex, (float)_currentState);
        effectMaterial.SetFloat(ShaderIds.ModeIndex, mode == ExperienceMode.Psychedelic ? 0f : 1f);

        Color edgeColor = mode == ExperienceMode.Psychedelic
            ? Color.Lerp(new Color(0.22f, 0.9f, 1f, 1f), new Color(1f, 0.25f, 0.82f, 1f), Mathf.Clamp01(_intensityScore * 1.2f))
            : Color.Lerp(new Color(1f, 0.72f, 0.3f, 1f), new Color(1f, 0.47f, 0.18f, 1f), Mathf.Clamp01(_intensityScore));
        Color atmosphereColor = mode == ExperienceMode.Psychedelic
            ? Color.Lerp(new Color(0.2f, 0.14f, 0.42f, 1f), new Color(0.95f, 0.12f, 0.65f, 1f), Mathf.Clamp01(_colorLayer + _overdriveLayer))
            : Color.Lerp(new Color(0.18f, 0.12f, 0.08f, 1f), new Color(0.96f, 0.52f, 0.2f, 1f), Mathf.Clamp01(_colorLayer));

        effectMaterial.SetColor(ShaderIds.EdgeColor, edgeColor);
        effectMaterial.SetColor(ShaderIds.AtmosphereColor, atmosphereColor);
    }

    private void HandleResetInput()
    {
        bool resetPressed = false;
        bool primaryPressed = PrimaryPressed(XRNode.LeftHand) || PrimaryPressed(XRNode.RightHand);
        bool resumePressed = primaryPressed && !_primaryWasPressed;
        _primaryWasPressed = primaryPressed;
#if UNITY_EDITOR && ENABLE_INPUT_SYSTEM
        resumePressed |= Keyboard.current != null && Keyboard.current.rKey.wasPressedThisFrame;
#endif
        if (resumePressed && EffectsSuspended && !_isResetting) ResumeExperience();

        #if UNITY_EDITOR
        #if ENABLE_INPUT_SYSTEM
        Keyboard keyboard = Keyboard.current;
        if (keyboard != null && (keyboard.spaceKey.wasPressedThisFrame || keyboard.bKey.wasPressedThisFrame || keyboard.yKey.wasPressedThisFrame))
        #else
        if (Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.B) || Input.GetKeyDown(KeyCode.Y))
        #endif
        {
            resetPressed = true;
        }
        #endif

        if (!resetPressed)
        {
            bool secondaryPressed = SecondaryPressed(XRNode.LeftHand) || SecondaryPressed(XRNode.RightHand);
            resetPressed = secondaryPressed && !_secondaryWasPressed;
            _secondaryWasPressed = secondaryPressed;
        }

        if (resetPressed && !_isResetting)
        {
            ResetExperience();
        }
    }

    public void ResetExperience()
    {
        EffectsSuspended = true;
        useDebugSlider = false;
        // Camera imagery is removed immediately; virtual layers finish their smooth release.
        _introFade = 0f;
        _sessionElapsed = sessionIntroDuration;
        ResetRequested?.Invoke();
        StopAllCoroutines();
        StartCoroutine(FadeOutRoutine());
    }

    public void ResumeExperience()
    {
        if (_isResetting) return;
        ClearRuntimeState();
        EffectsSuspended = false;
        ResumeRequested?.Invoke();
    }

    private IEnumerator FadeOutRoutine()
    {
        _isResetting = true;
        float startIntensity = _intensityScore;
        float startDistortion = _distortionLayer;
        float startPattern = _patternLayer;
        float startColor = _colorLayer;
        float startBreathing = _breathingLayer;
        float startEdge = _edgeGlow;
        float startAfterImage = _afterImageLayer;
        float startOverdrive = _overdriveLayer;
        float startMotion = _smoothedMotion;
        float elapsed = 0f;

        while (elapsed < safetyFadeDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / safetyFadeDuration);
            float eased = Mathf.SmoothStep(0f, 1f, t);

            _intensityScore = Mathf.Lerp(startIntensity, 0f, eased);
            _distortionLayer = Mathf.Lerp(startDistortion, 0f, eased);
            _patternLayer = Mathf.Lerp(startPattern, 0f, eased);
            _colorLayer = Mathf.Lerp(startColor, 0f, eased);
            _breathingLayer = Mathf.Lerp(startBreathing, 0f, eased);
            _edgeGlow = Mathf.Lerp(startEdge, 0f, eased);
            _afterImageLayer = Mathf.Lerp(startAfterImage, 0f, eased);
            _overdriveLayer = Mathf.Lerp(startOverdrive, 0f, eased);
            _smoothedMotion = Mathf.Lerp(startMotion, 0f, eased);

            ApplyMaterialProperties();
            yield return null;
        }

        ClearRuntimeState();
        _isResetting = false;
    }

    private void ClearRuntimeState()
    {
        _linearAcceleration = 0f;
        _angularVelocity = 0f;
        _smoothedMotion = 0f;
        _motionHistory = 0f;
        _burstEnergy = 0f;
        _activationBlend = 0f;
        _intensityScore = 0f;
        _lastIntensityScore = 0f;
        _trend = 0f;
        _peakHoldTimer = 0f;
        _distortionLayer = 0f;
        _patternLayer = 0f;
        _colorLayer = 0f;
        _breathingLayer = 0f;
        _edgeGlow = 0f;
        _afterImageLayer = 0f;
        _overdriveLayer = 0f;
        _peripheralWeight = 0.35f;
        _centerStability = 1f;
        _experienceActive = false;
        _hasBeenIntense = false;
        _currentState = ExperienceState.Calm;

        RebaseMotion();
        _sampleCount = 0;
        _sampleIndex = 0;
        _sampleSum = 0f;
        for (int i = 0; i < _motionSamples.Length; i++)
        {
            _motionSamples[i] = 0f;
        }

        ApplyMaterialProperties();
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

    private static float ExpLerp(float current, float target, float speed, float dt)
    {
        return Mathf.Lerp(current, target, 1f - Mathf.Exp(-speed * dt));
    }

    private static float SmoothLayer(float current, float target, float attack, float release, float dt)
    {
        float speed = target > current ? attack : release;
        return ExpLerp(current, target, speed, dt);
    }

    private static bool SecondaryPressed(XRNode hand)
    {
        UnityEngine.XR.InputDevice device = InputDevices.GetDeviceAtXRNode(hand);
        return device.isValid && device.TryGetFeatureValue(UnityEngine.XR.CommonUsages.secondaryButton, out bool pressed) && pressed;
    }

    private static bool PrimaryPressed(XRNode hand)
    {
        UnityEngine.XR.InputDevice device = InputDevices.GetDeviceAtXRNode(hand);
        return device.isValid && device.TryGetFeatureValue(UnityEngine.XR.CommonUsages.primaryButton, out bool pressed) && pressed;
    }

    private void RebaseMotion()
    {
        if (centerEyeAnchor == null) return;
        _lastPosition = centerEyeAnchor.position;
        _lastRotation = centerEyeAnchor.rotation;
        _lastVelocity = Vector3.zero;
    }

    private void OnApplicationFocus(bool focused)
    {
        // Desktop preview deliberately ticks in background; headset focus loss must suspend.
        if (!focused && isActiveAndEnabled && !Application.isEditor) ResetExperience();
        RebaseMotion();
    }

    [ContextMenu("Trigger Reset")]
    private void DebugReset()
    {
        ResetExperience();
    }
}
