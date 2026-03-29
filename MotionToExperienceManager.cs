using UnityEngine;
using System.Collections;

/// <summary>
/// Manages the XR experience intensity based on head motion.
/// Samples CenterEyeAnchor movement to drive visual effects.
/// Implements a layered architecture: Distortion, Patterns, and Color.
/// </summary>
public class MotionToExperienceManager : MonoBehaviour
{
    public enum ExperienceState { Calm, Shift, Intense, Recovery }
    public enum ExperienceMode { Psychedelic, Empathic }

    [Header("References")]
    [Tooltip("The CenterEyeAnchor from OVRCameraRig")]
    public Transform centerEyeAnchor;
    [Tooltip("The Full Screen Shader Material or Volume Material")]
    public Material effectMaterial;

    [Header("Mode Selection")]
    public ExperienceMode currentMode = ExperienceMode.Psychedelic;

    [Header("Motion Settings")]
    public float sensitivity = 0.8f;
    public float decayRate = 1.2f;
    public float jitterSensitivity = 2.0f;
    [Range(0.01f, 0.5f)]
    public float smoothingFactor = 0.05f;

    [Header("Experience Phases")]
    public float phaseTransitionSpeed = 0.5f;
    [Tooltip("How fast each layer responds to intensity changes (Attack/Release)")]
    public float distortionAttack = 0.8f;
    public float distortionRelease = 1.5f;
    public float patternAttack = 0.3f;
    public float patternRelease = 0.8f;
    public float colorAttack = 0.4f;
    public float colorRelease = 0.4f;
    public float breathingAttack = 0.2f;
    public float breathingRelease = 0.2f;

    private float _layerADistortion = 0f;
    private float _layerBPatterns = 0f;
    private float _layerCColor = 0f;
    private float _layerDBreathing = 0f;

    [Header("Hysteresis & Inertia")]
    public float enterThreshold = 0.15f;
    public float exitThreshold = 0.08f;
    public int motionHistoryFrames = 60;
    private float[] _motionHistory;
    private int _historyIndex = 0;
    private float _historySum = 0f;
    private float _accumulatedMotion = 0f;

    [Header("Safety & Transitions")]
    public float fadeDuration = 1.5f;
    public float maxDistortion = 0.05f;
    public float maxChromaticAberration = 0.02f;
    public float wobbleSpeed = 0.5f;
    public float breathingAmplitude = 0.02f;

    [Header("Debug")]
    public bool useDebugSlider = false;
    [Range(0, 1)] public float debugIntensity = 0f;

    // Internal State
    private float _intensityScore = 0f;
    private float _lastIntensityScore = 0f;
    private float _intensityTrend = 0f;
    private float _burstFactor = 0f;
    private float _randomFactor = 0f;
    private ExperienceState _currentState = ExperienceState.Calm;
    private Vector3 _lastPosition;
    private Quaternion _lastRotation;
    private Vector3 _velocity;
    private float _smoothedMotion;
    private float _jitterAmount;
    private bool _isResetting = false;
    private bool _isExperienceActive = false;

    private void Start()
    {
        if (centerEyeAnchor == null)
        {
            if (Camera.main != null) centerEyeAnchor = Camera.main.transform;
            else Debug.LogError("CenterEyeAnchor not assigned and Main Camera not found!");
        }

        _lastPosition = centerEyeAnchor.position;
        _lastRotation = centerEyeAnchor.rotation;
        
        _motionHistory = new float[motionHistoryFrames];
    }

    private void Update()
    {
        HandleSafetyInput();
        
        if (!_isResetting)
        {
            CalculateMotion();
            UpdateRandomness();
        }
        
        UpdatePhases();
        ApplyEffects();
    }

    private void UpdateRandomness()
    {
        // Subtle randomness to make the experience feel "alive"
        _randomFactor = Mathf.PerlinNoise(Time.time * 0.5f, 0f) * 0.1f;
    }

    private void CalculateMotion()
    {
        if (useDebugSlider)
        {
            _intensityScore = debugIntensity;
            _isExperienceActive = _intensityScore > enterThreshold;
            return;
        }

        // 1. Linear Velocity & Acceleration
        Vector3 currentVelocity = (centerEyeAnchor.position - _lastPosition) / Time.deltaTime;
        float linearAccel = (currentVelocity - _velocity).magnitude;
        _velocity = currentVelocity;
        _lastPosition = centerEyeAnchor.position;

        // 2. Angular Velocity - Increased weight for head turns (KinoFringe-style response)
        float angularVel = Quaternion.Angle(centerEyeAnchor.rotation, _lastRotation) / Time.deltaTime;
        _lastRotation = centerEyeAnchor.rotation;

        // 3. Detect spikes for "Burst" behavior (Sudden turns/lunges)
        float motionMagnitude = linearAccel + angularVel * 0.08f;
        if (motionMagnitude > 12f) 
        {
            _burstFactor = Mathf.Lerp(_burstFactor, 1f, Time.deltaTime * 10f);
        }
        else
        {
            _burstFactor = Mathf.Lerp(_burstFactor, 0f, Time.deltaTime * 2f);
        }

        // 4. Micro Jitter Detection
        float rawJitter = (linearAccel > 0.1f || angularVel > 0.5f) ? 0f : (linearAccel + angularVel * 0.01f);
        _jitterAmount = Mathf.Lerp(_jitterAmount, rawJitter, 0.1f);

        // 5. Combine and Smooth - Heavier weight on angular motion for perceptual instability
        float rawMotion = (linearAccel * 0.25f) + (angularVel * 0.025f * 0.75f) + (_jitterAmount * jitterSensitivity); 
        
        // 6. Rolling History (Inertia)
        _historySum -= _motionHistory[_historyIndex];
        _motionHistory[_historyIndex] = rawMotion;
        _historySum += rawMotion;
        _historyIndex = (_historyIndex + 1) % motionHistoryFrames;
        float averageMotion = _historySum / motionHistoryFrames;

        _smoothedMotion = Mathf.Lerp(_smoothedMotion, averageMotion, smoothingFactor);

        // 7. Hysteresis & Activation
        if (!_isExperienceActive && _smoothedMotion > enterThreshold)
        {
            _isExperienceActive = true;
        }
        else if (_isExperienceActive && _smoothedMotion < exitThreshold && _intensityScore < 0.05f)
        {
            _isExperienceActive = false;
        }

        // 8. Update Score with Burst and Sensitivity
        if (_isExperienceActive)
        {
            float motionContribution = _smoothedMotion * sensitivity * Time.deltaTime;
            _intensityScore += (motionContribution * (1f + _burstFactor * 3f)) + (_randomFactor * Time.deltaTime * _intensityScore);
        }

        // 9. Adaptive Decay - Slower in mid-range to maintain immersion
        float adaptiveDecay = decayRate * (1f - Mathf.Clamp01(_intensityScore * 0.4f));
        _intensityScore *= Mathf.Exp(-adaptiveDecay * Time.deltaTime);

        _intensityScore = Mathf.Clamp01(_intensityScore);
    }

    private void UpdatePhases()
    {
        // Non-linear response curves based on research-grounded motifs
        // Each layer has independent Attack/Release for organic timing
        
        float targetDistortion = 0f;
        float targetPatterns = 0f;
        float targetColor = 0f;
        float targetBreathing = 0f;

        if (currentMode == ExperienceMode.Psychedelic)
        {
            targetDistortion = Mathf.Pow(_intensityScore, 0.6f);
            targetPatterns = _intensityScore > 0.5f ? Mathf.Pow((_intensityScore - 0.5f) / 0.5f, 2.0f) : 0f;
            targetColor = _intensityScore;
            targetBreathing = Mathf.Pow(_intensityScore, 1.5f);
        }
        else // Empathic Mode
        {
            targetDistortion = _intensityScore * 0.3f;
            targetPatterns = _intensityScore * 0.1f;
            targetColor = Mathf.Pow(_intensityScore, 0.8f);
            targetBreathing = _intensityScore * 0.5f;
        }

        // Apply independent smoothing (Attack vs Release)
        _layerADistortion = SmoothLayer(_layerADistortion, targetDistortion, distortionAttack, distortionRelease);
        _layerBPatterns = SmoothLayer(_layerBPatterns, targetPatterns, patternAttack, patternRelease);
        _layerCColor = SmoothLayer(_layerCColor, targetColor, colorAttack, colorRelease);
        _layerDBreathing = SmoothLayer(_layerDBreathing, targetBreathing, breathingAttack, breathingRelease);

        // Trend calculation for Recovery state
        _intensityTrend = (_intensityScore - _lastIntensityScore) / Time.deltaTime;
        _lastIntensityScore = _intensityScore;

        // State Machine Refinement
        if (_intensityScore < 0.1f) 
        {
            _currentState = ExperienceState.Calm;
        }
        else if (_intensityTrend < -0.3f && _intensityScore > 0.3f) 
        {
            _currentState = ExperienceState.Recovery;
        }
        else if (_intensityScore < 0.55f) 
        {
            _currentState = ExperienceState.Shift;
        }
        else 
        {
            _currentState = ExperienceState.Intense;
        }
    }

    private float SmoothLayer(float current, float target, float attack, float release)
    {
        float speed = (target > current) ? attack : release;
        return Mathf.Lerp(current, target, Time.deltaTime * speed * 5f);
    }

    private void ApplyEffects()
    {
        if (effectMaterial == null) return;

        effectMaterial.SetFloat("_IntensityScore", _intensityScore);
        effectMaterial.SetInt("_ExperienceMode", (int)currentMode);

        // Layer A: Distortion
        float distortion = Mathf.Lerp(0, maxDistortion, _layerADistortion);
        effectMaterial.SetFloat("_DistortionAmount", distortion);
        
        // Layer D: Breathing (Independent timing)
        float breathingFreq = currentMode == ExperienceMode.Psychedelic ? 0.4f + _intensityScore * 1.6f : 0.3f + _intensityScore * 0.7f;
        float breathing = 1.0f + Mathf.Sin(Time.time * breathingFreq) * breathingAmplitude * _layerDBreathing;
        effectMaterial.SetFloat("_BreathingScale", breathing);

        // Layer B: Patterns
        effectMaterial.SetFloat("_PatternOpacity", _layerBPatterns * (currentMode == ExperienceMode.Psychedelic ? 0.5f : 0.15f)); 

        // Layer C: Color & Chromatic
        float chromatic = Mathf.Lerp(0, maxChromaticAberration, _layerCColor);
        effectMaterial.SetFloat("_ChromaticAmount", chromatic);

        float hueShift = Time.time * (currentMode == ExperienceMode.Psychedelic ? 0.2f : 0.05f) * _layerCColor;
        effectMaterial.SetFloat("_HueShift", hueShift);

        // Mode-specific color response
        Color targetColor = currentMode == ExperienceMode.Psychedelic 
            ? new Color(0.85f, 0.15f, 1f, 1f) // Neon Purple/Pink
            : new Color(1f, 0.6f, 0.2f, 1f);   // Warm Amber/Gold
            
        Color edgeColor = Color.Lerp(Color.white, targetColor, Mathf.Pow(_layerCColor, 1.1f));
        effectMaterial.SetColor("_EdgeColor", edgeColor);
    }

    private void HandleSafetyInput()
    {
        // OVRInput for Quest: Button B (Two) or Y (Four)
        bool resetPressed = false;
        
        #if UNITY_EDITOR
        if (Input.GetKeyDown(KeyCode.R)) resetPressed = true;
        #endif

        // Check Meta Quest Controllers
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
        float elapsed = 0f;

        while (elapsed < fadeDuration)
        {
            elapsed += Time.deltaTime;
            _intensityScore = Mathf.Lerp(startScore, 0f, elapsed / fadeDuration);
            yield return null;
        }

        _intensityScore = 0f;
        _isResetting = false;
    }

    // Context Menu for testing in Editor
    [ContextMenu("Trigger Reset")]
    public void DebugReset() => ResetExperience();
}
