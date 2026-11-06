import React, {
  startTransition,
  useDeferredValue,
  useEffect,
  useEffectEvent,
  useRef,
  useState,
} from 'react';
import {
  Activity,
  AlertCircle,
  Camera as CameraIcon,
  Info,
  RefreshCw,
  Settings,
  Shield,
  SlidersHorizontal,
  Waves,
} from 'lucide-react';
import { AnimatePresence, motion } from 'motion/react';

type ExperienceState = 'Calm' | 'Shift' | 'Intense' | 'Recovery';
type ExperienceMode = 'Psychedelic' | 'Empathic';

type LayerState = {
  edge: number;
  chromatic: number;
  blur: number;
  distortion: number;
  overdrive: number;
};

type UiSnapshot = {
  intensityScore: number;
  state: ExperienceState;
  isActive: boolean;
  linearAcceleration: number;
  angularVelocity: number;
  smoothedMotion: number;
  trend: number;
  breathingScale: number;
  trailIntensity: number;
  layers: LayerState;
};

type SimulationState = {
  intensityScore: number;
  state: ExperienceState;
  isActive: boolean;
  isResetting: boolean;
  resetElapsed: number;
  resetFrom: UiSnapshot;
  lastMoveTs: number;
  lastPointerX: number;
  lastPointerY: number;
  lastVelocity: number;
  lastHeading: number | null;
  linearAcceleration: number;
  angularVelocity: number;
  pendingMotion: number;
  smoothedMotion: number;
  breathingPhase: number;
  breathingScale: number;
  trailIntensity: number;
  trend: number;
  overdriveHold: number;
  layers: LayerState;
  motionSamples: number[];
};

declare global {
  interface Window {
    render_game_to_text?: () => string;
    advanceTime?: (ms: number) => void;
  }
}

const ENTER_THRESHOLD = 0.15;
const EXIT_THRESHOLD = 0.06;
const RESET_DURATION = 1.5;
const DISTORTION_CAP = 0.05;
const initialLayers: LayerState = {
  edge: 0,
  chromatic: 0,
  blur: 0,
  distortion: 0,
  overdrive: 0,
};

const initialUiSnapshot: UiSnapshot = {
  intensityScore: 0,
  state: 'Calm',
  isActive: false,
  linearAcceleration: 0,
  angularVelocity: 0,
  smoothedMotion: 0,
  trend: 0,
  breathingScale: 1,
  trailIntensity: 0,
  layers: initialLayers,
};

function clamp01(value: number) {
  return Math.min(Math.max(value, 0), 1);
}

function expLerp(current: number, target: number, speed: number, dt: number) {
  return current + (target - current) * (1 - Math.exp(-speed * dt));
}

function approachLayer(current: number, target: number, attack: number, release: number, dt: number) {
  return expLerp(current, target, target > current ? attack : release, dt);
}

function getState(score: number, trend: number): ExperienceState {
  if (score < 0.08) {
    return 'Calm';
  }
  if (trend < -0.2 && score > 0.2) {
    return 'Recovery';
  }
  if (score < 0.3) {
    return 'Shift';
  }
  return 'Intense';
}

export default function App() {
  const [mode, setMode] = useState<ExperienceMode>('Psychedelic');
  const [showGuide, setShowGuide] = useState(false);
  const [useCamera, setUseCamera] = useState(false);
  const [introProgress, setIntroProgress] = useState(0);
  const [useDebugSlider, setUseDebugSlider] = useState(true);
  const [debugIntensity, setDebugIntensity] = useState(0.48);
  const [sensitivity, setSensitivity] = useState(1);
  const [decayRate, setDecayRate] = useState(1.25);
  const [ui, setUi] = useState<UiSnapshot>(initialUiSnapshot);
  const deferredScore = useDeferredValue(ui.intensityScore);

  const videoRef = useRef<HTMLVideoElement>(null);
  const mediaStreamRef = useRef<MediaStream | null>(null);
  const paramsRef = useRef({
    mode,
    useDebugSlider,
    debugIntensity,
    sensitivity,
    decayRate,
  });
  paramsRef.current = {
    mode,
    useDebugSlider,
    debugIntensity,
    sensitivity,
    decayRate,
  };

  const simRef = useRef<SimulationState>({
    intensityScore: 0,
    state: 'Calm',
    isActive: false,
    isResetting: false,
    resetElapsed: 0,
    resetFrom: initialUiSnapshot,
    lastMoveTs: performance.now(),
    lastPointerX: 0,
    lastPointerY: 0,
    lastVelocity: 0,
    lastHeading: null,
    linearAcceleration: 0,
    angularVelocity: 0,
    pendingMotion: 0,
    smoothedMotion: 0,
    breathingPhase: 0,
    breathingScale: 1,
    trailIntensity: 0,
    trend: 0,
    overdriveHold: 0,
    layers: { ...initialLayers },
    motionSamples: [],
  });

  const syncUi = useEffectEvent(() => {
    const sim = simRef.current;
    startTransition(() => {
      setUi({
        intensityScore: sim.intensityScore,
        state: sim.state,
        isActive: sim.isActive,
        linearAcceleration: sim.linearAcceleration,
        angularVelocity: sim.angularVelocity,
        smoothedMotion: sim.smoothedMotion,
        trend: sim.trend,
        breathingScale: sim.breathingScale,
        trailIntensity: sim.trailIntensity,
        layers: { ...sim.layers },
      });
    });
  });

  const resetExperience = useEffectEvent(() => {
    const sim = simRef.current;
    setUseDebugSlider(false);
    paramsRef.current.useDebugSlider = false;
    sim.isResetting = true;
    sim.resetElapsed = 0;
    sim.resetFrom = {
      intensityScore: sim.intensityScore,
      state: sim.state,
      isActive: sim.isActive,
      linearAcceleration: sim.linearAcceleration,
      angularVelocity: sim.angularVelocity,
      smoothedMotion: sim.smoothedMotion,
      trend: sim.trend,
      breathingScale: sim.breathingScale,
      trailIntensity: sim.trailIntensity,
      layers: { ...sim.layers },
    };
  });

  const pushMotionSample = (sample: number) => {
    const samples = simRef.current.motionSamples;
    samples.push(sample);
    if (samples.length > 12) {
      samples.shift();
    }
  };

  const updatePointerMotion = useEffectEvent((event: MouseEvent) => {
    if (paramsRef.current.useDebugSlider) {
      return;
    }

    const sim = simRef.current;
    const now = event.timeStamp || performance.now();
    const dt = Math.max((now - sim.lastMoveTs) / 1000, 1 / 240);
    const dx = event.clientX - sim.lastPointerX;
    const dy = event.clientY - sim.lastPointerY;
    const distance = Math.hypot(dx, dy);
    const velocity = distance / dt;
    const linearAcceleration = Math.abs(velocity - sim.lastVelocity) / dt;

    let angularVelocity = 0;
    if (distance > 0.001) {
      const heading = Math.atan2(dy, dx);
      if (sim.lastHeading !== null) {
        const delta = Math.atan2(Math.sin(heading - sim.lastHeading), Math.cos(heading - sim.lastHeading));
        angularVelocity = Math.abs(delta) / dt * (180 / Math.PI);
      }
      sim.lastHeading = heading;
    }

    sim.lastMoveTs = now;
    sim.lastPointerX = event.clientX;
    sim.lastPointerY = event.clientY;
    sim.lastVelocity = velocity;
    sim.linearAcceleration = linearAcceleration;
    sim.angularVelocity = angularVelocity;

    const normalizedMotion = clamp01((linearAcceleration / 2400) * 0.55 + (angularVelocity / 720) * 0.75);
    sim.pendingMotion = Math.max(sim.pendingMotion * 0.72, normalizedMotion);
    sim.trailIntensity = Math.max(sim.trailIntensity, normalizedMotion > 0.65 ? 0.9 : normalizedMotion * 0.45);
    pushMotionSample(normalizedMotion);
  });

  const stepSimulation = useEffectEvent((dt: number) => {
    if (introProgress < 1) {
      return;
    }

    const sim = simRef.current;
    const params = paramsRef.current;
    const previousScore = sim.intensityScore;

    if (sim.isResetting) {
      sim.resetElapsed += dt;
      const t = clamp01(sim.resetElapsed / RESET_DURATION);
      sim.intensityScore = sim.resetFrom.intensityScore * (1 - t);
      sim.smoothedMotion = sim.resetFrom.smoothedMotion * (1 - t);
      sim.linearAcceleration = sim.resetFrom.linearAcceleration * (1 - t);
      sim.angularVelocity = sim.resetFrom.angularVelocity * (1 - t);
      sim.trailIntensity = sim.resetFrom.trailIntensity * (1 - t);
      sim.layers.edge = sim.resetFrom.layers.edge * (1 - t);
      sim.layers.chromatic = sim.resetFrom.layers.chromatic * (1 - t);
      sim.layers.blur = sim.resetFrom.layers.blur * (1 - t);
      sim.layers.distortion = sim.resetFrom.layers.distortion * (1 - t);
      sim.layers.overdrive = sim.resetFrom.layers.overdrive * (1 - t);
      sim.trend = (sim.intensityScore - previousScore) / Math.max(dt, 1 / 240);
      sim.state = 'Recovery';
      sim.isActive = sim.intensityScore > EXIT_THRESHOLD;
      sim.breathingScale = 1 + Math.sin(sim.breathingPhase) * 0.008 * sim.layers.blur;

      if (t >= 1) {
        sim.intensityScore = 0;
        sim.state = 'Calm';
        sim.isActive = false;
        sim.isResetting = false;
        sim.linearAcceleration = 0;
        sim.angularVelocity = 0;
        sim.pendingMotion = 0;
        sim.smoothedMotion = 0;
        sim.trailIntensity = 0;
        sim.layers = { ...initialLayers };
        sim.motionSamples = [];
        sim.overdriveHold = 0;
      }

      syncUi();
      return;
    }

    let motionSignal = sim.pendingMotion;
    if (params.useDebugSlider) {
      motionSignal = params.debugIntensity;
      sim.pendingMotion = params.debugIntensity;
    }

    pushMotionSample(motionSignal);
    const historyAverage =
      sim.motionSamples.reduce((sum, sample) => sum + sample, 0) / Math.max(sim.motionSamples.length, 1);

    sim.smoothedMotion = expLerp(sim.smoothedMotion, historyAverage, 6, dt);

    if (!sim.isActive && sim.smoothedMotion > ENTER_THRESHOLD) {
      sim.isActive = true;
    } else if (sim.isActive && sim.smoothedMotion < EXIT_THRESHOLD && sim.intensityScore < 0.05) {
      sim.isActive = false;
    }

    if (params.useDebugSlider) {
      sim.intensityScore = expLerp(sim.intensityScore, params.debugIntensity, 3.5, dt);
    } else {
      if (sim.isActive) {
        const gain = sim.intensityScore > 0.72 ? 0.5 : 0.95;
        sim.intensityScore += sim.smoothedMotion * params.sensitivity * gain * dt;
      }
      const releaseMultiplier = sim.intensityScore > 0.7 || sim.overdriveHold > 0 ? 0.28 : 0.65;
      sim.intensityScore *= Math.exp(-(params.decayRate * releaseMultiplier) * dt);
    }

    // Only new motion can renew the hold; held intensity must not renew itself.
    if (motionSignal >= 0.78 && sim.intensityScore >= 0.78) {
      sim.overdriveHold = 4.5;
    } else if (sim.overdriveHold > 0) {
      sim.overdriveHold = Math.max(0, sim.overdriveHold - dt);
      sim.intensityScore = Math.max(sim.intensityScore, 0.8 - (4.5 - sim.overdriveHold) * 0.03);
    }

    sim.intensityScore = clamp01(sim.intensityScore);
    sim.trend = (sim.intensityScore - previousScore) / Math.max(dt, 1 / 240);
    sim.state = getState(sim.intensityScore, sim.trend);

    const chromaticTarget = clamp01((sim.intensityScore - 0.08) / 0.92);
    const blurTarget = clamp01((sim.intensityScore - 0.04) / 0.96);
    const distortionTarget = clamp01((sim.intensityScore - 0.22) / 0.78);
    const edgeTarget = clamp01(Math.pow(sim.intensityScore, 0.78));
    const overdriveTarget = sim.intensityScore >= 0.75 || sim.overdriveHold > 0
      ? clamp01((Math.max(sim.intensityScore, 0.8) - 0.75) / 0.25)
      : 0;

    sim.layers.edge = approachLayer(sim.layers.edge, edgeTarget, 2.2, 0.42, dt);
    sim.layers.chromatic = approachLayer(sim.layers.chromatic, chromaticTarget, 1.8, 0.34, dt);
    sim.layers.blur = approachLayer(sim.layers.blur, blurTarget, 1.5, 0.3, dt);
    sim.layers.distortion = approachLayer(sim.layers.distortion, distortionTarget, 1.2, 0.28, dt);
    sim.layers.overdrive = approachLayer(sim.layers.overdrive, overdriveTarget, 1.1, 0.16, dt);

    sim.breathingPhase += dt * (params.mode === 'Psychedelic' ? 1.4 + sim.intensityScore * 1.8 : 0.9 + sim.intensityScore);
    sim.breathingScale = 1 + Math.sin(sim.breathingPhase) * (0.015 + sim.layers.overdrive * 0.02) * (0.2 + sim.layers.blur);

    sim.trailIntensity = Math.max(sim.trailIntensity * Math.exp(-2.2 * dt), motionSignal > 0.65 ? 0.95 : sim.layers.overdrive * 0.55);
    sim.pendingMotion *= Math.exp(-7 * dt);
    sim.linearAcceleration *= Math.exp(-7.5 * dt);
    sim.angularVelocity *= Math.exp(-7.5 * dt);

    syncUi();
  });

  useEffect(() => {
    const onMouseMove = (event: MouseEvent) => updatePointerMotion(event);
    const onKeyDown = (event: KeyboardEvent) => {
      if (event.code === 'Space' || event.key.toLowerCase() === 'b' || event.key.toLowerCase() === 'y') {
        event.preventDefault();
        resetExperience();
      }
    };

    window.addEventListener('mousemove', onMouseMove);
    window.addEventListener('keydown', onKeyDown);
    return () => {
      window.removeEventListener('mousemove', onMouseMove);
      window.removeEventListener('keydown', onKeyDown);
    };
  }, [resetExperience, updatePointerMotion]);

  useEffect(() => {
    const start = performance.now();
    const interval = window.setInterval(() => {
      const progress = clamp01((performance.now() - start) / 10000);
      setIntroProgress(progress);
      if (progress >= 1) {
        window.clearInterval(interval);
      }
    }, 1000 / 60);

    return () => window.clearInterval(interval);
  }, []);

  useEffect(() => {
    const interval = window.setInterval(() => stepSimulation(1 / 60), 1000 / 60);
    return () => window.clearInterval(interval);
  }, [stepSimulation]);

  useEffect(() => {
    window.render_game_to_text = () =>
      JSON.stringify({
        mode,
        state: simRef.current.state,
        intensityScore: Number(simRef.current.intensityScore.toFixed(3)),
        smoothedMotion: Number(simRef.current.smoothedMotion.toFixed(3)),
        linearAcceleration: Number(simRef.current.linearAcceleration.toFixed(1)),
        angularVelocity: Number(simRef.current.angularVelocity.toFixed(1)),
        layers: {
          edge: Number(simRef.current.layers.edge.toFixed(3)),
          chromatic: Number(simRef.current.layers.chromatic.toFixed(3)),
          blur: Number(simRef.current.layers.blur.toFixed(3)),
          distortion: Number(simRef.current.layers.distortion.toFixed(3)),
          overdrive: Number(simRef.current.layers.overdrive.toFixed(3)),
        },
        controls: {
          reset: ['Space', 'B', 'Y'],
          debugSlider: useDebugSlider,
        },
        coordinateSystem: 'screen pointer motion, origin top-left, X right, Y down',
      });

    window.advanceTime = (ms: number) => {
      const steps = Math.max(1, Math.round(ms / (1000 / 60)));
      for (let index = 0; index < steps; index += 1) {
        stepSimulation(1 / 60);
      }
    };

    return () => {
      delete window.render_game_to_text;
      delete window.advanceTime;
    };
  }, [mode, stepSimulation, useDebugSlider]);

  useEffect(() => {
    if (!useCamera) {
      if (mediaStreamRef.current) {
        mediaStreamRef.current.getTracks().forEach(track => track.stop());
        mediaStreamRef.current = null;
      }
      if (videoRef.current) {
        videoRef.current.srcObject = null;
      }
      return;
    }

    let cancelled = false;
    navigator.mediaDevices
      .getUserMedia({ video: { facingMode: 'environment' } })
      .then(stream => {
        if (cancelled) {
          stream.getTracks().forEach(track => track.stop());
          return;
        }
        mediaStreamRef.current = stream;
        if (videoRef.current) {
          videoRef.current.srcObject = stream;
        }
      })
      .catch(() => {
        setUseCamera(false);
      });

    return () => {
      cancelled = true;
    };
  }, [useCamera]);

  const filterStyles = {
    filter: `
      saturate(${100 + ui.layers.edge * (mode === 'Psychedelic' ? 145 : 55)}%)
      contrast(${100 + ui.layers.edge * (mode === 'Psychedelic' ? 78 : 34)}%)
      brightness(${100 + ui.layers.blur * (mode === 'Psychedelic' ? 18 : 10) + ui.layers.overdrive * 20}%)
      hue-rotate(${ui.layers.chromatic * (mode === 'Psychedelic' ? 145 : 28) + ui.layers.overdrive * 120}deg)
      blur(${ui.layers.blur * 2.1 + ui.layers.overdrive * 1.8}px)
      url(#tripDistortion)
    `,
    transform: `scale(${ui.breathingScale + ui.layers.distortion * 0.04 + ui.layers.overdrive * 0.02})`,
  };

  const backgroundTint =
    mode === 'Psychedelic'
      ? 'from-[#14061f] via-[#030712] to-[#02040a]'
      : 'from-[#17130a] via-[#06070a] to-[#02040a]';

  const edgeTint =
    mode === 'Psychedelic'
      ? `rgba(232, 121, 249, ${0.25 + ui.layers.edge * 0.75})`
      : `rgba(251, 191, 36, ${0.18 + ui.layers.edge * 0.55})`;
  const introOpacity = 1 - introProgress;
  const introReveal = 8 + introProgress * 160;

  return (
    <div className="min-h-screen overflow-hidden bg-black text-white selection:bg-fuchsia-500/20">
      <svg className="hidden">
        <filter id="tripDistortion">
          <feTurbulence type="fractalNoise" baseFrequency="0.008 0.015" numOctaves="2" seed="7">
            <animate attributeName="baseFrequency" values="0.008 0.015;0.012 0.02;0.008 0.015" dur="12s" repeatCount="indefinite" />
          </feTurbulence>
          <feDisplacementMap
            in="SourceGraphic"
            scale={ui.layers.distortion * DISTORTION_CAP * 1000}
            xChannelSelector="R"
            yChannelSelector="G"
          />
        </filter>
      </svg>

      <div className="pointer-events-none fixed inset-0">
        <div className={`absolute inset-0 bg-[radial-gradient(circle_at_top,rgba(255,255,255,0.12),transparent_38%)] bg-gradient-to-br ${backgroundTint}`} />
        <div className="absolute inset-0 opacity-80" style={filterStyles}>
          {useCamera ? (
            <video ref={videoRef} autoPlay playsInline className="h-full w-full object-cover opacity-55" />
          ) : (
            <div className="flex h-full w-full items-center justify-center">
              <div className="grid grid-cols-8 gap-5 opacity-25">
                {Array.from({ length: 48 }).map((_, index) => (
                  <div
                    key={index}
                    className="h-14 w-14 rounded-full border border-white/15"
                    style={{
                      transform: `scale(${0.85 + ((index % 5) * 0.04)})`,
                      boxShadow: `0 0 ${10 + ui.layers.edge * 28}px rgba(255,255,255,${0.04 + ui.layers.edge * 0.08})`,
                    }}
                  />
                ))}
              </div>
            </div>
          )}
        </div>

        {ui.trailIntensity > 0.05 && (
          <div
            className="absolute inset-0 mix-blend-screen opacity-30"
            style={{
              transform: `translate(${ui.trailIntensity * 22}px, ${ui.trailIntensity * 12}px) scale(${1 + ui.trailIntensity * 0.02})`,
              background:
                mode === 'Psychedelic'
                  ? 'radial-gradient(circle at 40% 30%, rgba(236,72,153,0.34), transparent 35%), radial-gradient(circle at 70% 60%, rgba(59,130,246,0.26), transparent 35%)'
                  : 'radial-gradient(circle at 40% 30%, rgba(251,191,36,0.24), transparent 35%), radial-gradient(circle at 70% 60%, rgba(45,212,191,0.18), transparent 35%)',
            }}
          />
        )}

        <div
          className="absolute inset-0 mix-blend-screen transition-opacity duration-500"
          style={{
            opacity: mode === 'Psychedelic' ? ui.layers.edge * 0.22 + ui.layers.overdrive * 0.18 : ui.layers.edge * 0.12 + ui.layers.overdrive * 0.1,
            background:
              mode === 'Psychedelic'
                ? 'radial-gradient(circle at 20% 20%, rgba(217,70,239,0.55), transparent 30%), radial-gradient(circle at 80% 70%, rgba(56,189,248,0.45), transparent 32%)'
                : 'radial-gradient(circle at 20% 20%, rgba(251,191,36,0.35), transparent 30%), radial-gradient(circle at 80% 70%, rgba(45,212,191,0.22), transparent 32%)',
          }}
        />

        <div
          className="absolute inset-0 mix-blend-screen transition-opacity duration-700"
          style={{
            opacity: ui.layers.overdrive * 0.58,
            background:
              'radial-gradient(circle at 18% 22%, rgba(255,0,128,0.9), transparent 22%), radial-gradient(circle at 78% 28%, rgba(0,255,255,0.72), transparent 24%), radial-gradient(circle at 52% 78%, rgba(255,255,0,0.62), transparent 28%)',
          }}
        />

        <div
          className="absolute inset-0 transition-opacity duration-700"
          style={{
            opacity: ui.layers.overdrive * 0.36,
            background:
              'linear-gradient(135deg, rgba(255,0,106,0.45), rgba(102,0,255,0.18) 35%, rgba(0,255,224,0.28) 70%, rgba(255,153,0,0.35))',
            mixBlendMode: 'color-dodge',
          }}
        />

        <div
          className="absolute inset-0 transition-[box-shadow,border] duration-500"
          style={{
            boxShadow: `inset 0 0 ${40 + ui.layers.edge * 140 + ui.layers.overdrive * 180}px ${edgeTint}`,
            border: `${ui.layers.edge * 14 + ui.layers.overdrive * 10}px solid ${edgeTint}`,
          }}
        />

        {ui.layers.chromatic > 0.08 && (
          <div className="absolute inset-0 overflow-hidden mix-blend-screen">
            <div
              className="absolute inset-0 bg-red-500/20"
              style={{ transform: `translateX(${ui.layers.chromatic * 18 + ui.layers.overdrive * 24}px) scale(${1.01 + ui.layers.overdrive * 0.02})` }}
            />
            <div
              className="absolute inset-0 bg-cyan-400/20"
              style={{ transform: `translateX(-${ui.layers.chromatic * 18 + ui.layers.overdrive * 24}px) scale(${1.01 + ui.layers.overdrive * 0.02})` }}
            />
          </div>
        )}

        {introProgress < 1 && (
          <div
            className="absolute inset-0 z-40 transition-opacity"
            style={{
              opacity: introOpacity,
              background: `radial-gradient(circle at 50% 50%, rgba(0,0,0,0) 0%, rgba(0,0,0,0) ${introReveal * 0.2}%, rgba(0,0,0,0.94) ${introReveal * 0.45}%, rgba(0,0,0,1) ${introReveal}%)`,
            }}
          />
        )}
      </div>

      <div className="relative z-10 flex min-h-screen flex-col">
        <header className="flex items-start justify-between p-6 md:p-8">
          <div>
            <div className="mb-2 flex items-center gap-3 text-[11px] uppercase tracking-[0.32em] text-zinc-400">
              <Waves className="h-4 w-4" />
              <span>Critical VR Motion Trip</span>
            </div>
            <div className="flex items-end gap-4">
              <h1 className="text-4xl font-light tracking-[-0.04em] md:text-6xl">{ui.state}</h1>
              <div className="pb-2 font-mono text-sm text-zinc-400">{(deferredScore * 100).toFixed(1)}%</div>
            </div>
          </div>

          <div className="flex items-center gap-3">
            <button
              onClick={() => setShowGuide(value => !value)}
              className="rounded-full border border-white/10 bg-white/5 p-3 text-zinc-300 transition hover:bg-white/10"
            >
              <Info className="h-5 w-5" />
            </button>
            <button
              onClick={() => setUseCamera(value => !value)}
              className={`rounded-full border p-3 transition ${
                useCamera
                  ? 'border-emerald-400/50 bg-emerald-400/15 text-emerald-300'
                  : 'border-white/10 bg-white/5 text-zinc-300 hover:bg-white/10'
              }`}
            >
              <CameraIcon className="h-5 w-5" />
            </button>
          </div>
        </header>

        <main className="mt-auto grid gap-5 px-6 pb-8 md:grid-cols-12 md:px-8">
          <section className="pointer-events-auto rounded-[2rem] border border-white/10 bg-black/40 p-6 backdrop-blur-xl md:col-span-4">
            <div className="mb-5 flex items-center gap-2 text-[11px] uppercase tracking-[0.28em] text-zinc-400">
              <Settings className="h-4 w-4" />
              <span>Mode And Safety</span>
            </div>

            <div className="mb-5 grid grid-cols-2 gap-2">
              <button
                onClick={() => setMode('Psychedelic')}
                className={`rounded-2xl border px-4 py-3 text-xs font-medium uppercase tracking-[0.2em] transition ${
                  mode === 'Psychedelic'
                    ? 'border-fuchsia-400/60 bg-fuchsia-500/15 text-fuchsia-200'
                    : 'border-white/10 bg-white/5 text-zinc-400 hover:bg-white/10'
                }`}
              >
                Psychedelic
              </button>
              <button
                onClick={() => setMode('Empathic')}
                className={`rounded-2xl border px-4 py-3 text-xs font-medium uppercase tracking-[0.2em] transition ${
                  mode === 'Empathic'
                    ? 'border-amber-400/60 bg-amber-500/15 text-amber-200'
                    : 'border-white/10 bg-white/5 text-zinc-400 hover:bg-white/10'
                }`}
              >
                Empathic
              </button>
            </div>

            <div className="mb-5 space-y-3 text-sm text-zinc-300">
              <label className="flex items-center justify-between rounded-2xl border border-white/10 bg-white/5 px-4 py-3">
                <span className="flex items-center gap-2">
                  <SlidersHorizontal className="h-4 w-4 text-zinc-400" />
                  Debug slider
                </span>
                <input
                  type="checkbox"
                  checked={useDebugSlider}
                  onChange={event => setUseDebugSlider(event.target.checked)}
                  className="h-4 w-4 accent-fuchsia-500"
                />
              </label>
              <div className="rounded-2xl border border-white/10 bg-white/5 px-4 py-4">
                <div className="mb-2 flex justify-between text-[11px] uppercase tracking-[0.2em] text-zinc-500">
                  <span>Target Intensity</span>
                  <span>{debugIntensity.toFixed(2)}</span>
                </div>
                <input
                  type="range"
                  min="0"
                  max="1"
                  step="0.01"
                  value={debugIntensity}
                  onChange={event => setDebugIntensity(Number(event.target.value))}
                  className="w-full"
                />
                <div className="mt-2 text-[10px] uppercase tracking-[0.16em] text-zinc-500">
                  Debug mode overrides live sensitivity and drives the score directly.
                </div>
              </div>
            </div>

            <div className="space-y-4">
              <div>
                <div className="mb-2 flex justify-between text-[11px] uppercase tracking-[0.2em] text-zinc-500">
                  <span>Sensitivity</span>
                  <span>{sensitivity.toFixed(2)}</span>
                </div>
                <input
                  type="range"
                  min="0.25"
                  max="2"
                  step="0.05"
                  value={sensitivity}
                  onChange={event => setSensitivity(Number(event.target.value))}
                  className="w-full"
                />
              </div>
              <div>
                <div className="mb-2 flex justify-between text-[11px] uppercase tracking-[0.2em] text-zinc-500">
                  <span>Decay</span>
                  <span>{decayRate.toFixed(2)}</span>
                </div>
                <input
                  type="range"
                  min="0.2"
                  max="3"
                  step="0.05"
                  value={decayRate}
                  onChange={event => setDecayRate(Number(event.target.value))}
                  className="w-full"
                />
              </div>
            </div>
          </section>

          <section className="pointer-events-auto rounded-[2rem] border border-white/10 bg-black/40 p-6 backdrop-blur-xl md:col-span-5">
            <div className="mb-5 flex items-center justify-between">
              <div className="flex items-center gap-2 text-[11px] uppercase tracking-[0.28em] text-zinc-400">
                <Activity className="h-4 w-4" />
                <span>Motion Analyzer</span>
              </div>
              <div className={`rounded-full px-3 py-1 text-[10px] uppercase tracking-[0.2em] ${ui.isActive ? 'bg-emerald-400/15 text-emerald-300' : 'bg-white/5 text-zinc-500'}`}>
                {ui.isActive ? 'Active' : 'Idle'}
              </div>
            </div>

            <div className="mb-5 h-2 overflow-hidden rounded-full bg-white/10">
              <motion.div
                className="h-full bg-gradient-to-r from-sky-400 via-fuchsia-400 to-amber-300"
                animate={{ width: `${deferredScore * 100}%` }}
                transition={{ type: 'spring', bounce: 0, duration: 0.3 }}
              />
            </div>

            <div className="grid grid-cols-2 gap-3 text-sm md:grid-cols-3">
              <div className="rounded-2xl border border-white/10 bg-white/5 p-4">
                <div className="mb-1 text-[11px] uppercase tracking-[0.18em] text-zinc-500">Linear Accel</div>
                <div className="text-lg text-white">{ui.linearAcceleration.toFixed(1)}</div>
              </div>
              <div className="rounded-2xl border border-white/10 bg-white/5 p-4">
                <div className="mb-1 text-[11px] uppercase tracking-[0.18em] text-zinc-500">Angular Vel</div>
                <div className="text-lg text-white">{ui.angularVelocity.toFixed(1)}</div>
              </div>
              <div className="rounded-2xl border border-white/10 bg-white/5 p-4">
                <div className="mb-1 text-[11px] uppercase tracking-[0.18em] text-zinc-500">Smoothed</div>
                <div className="text-lg text-white">{ui.smoothedMotion.toFixed(3)}</div>
              </div>
              <div className="rounded-2xl border border-white/10 bg-white/5 p-4">
                <div className="mb-1 text-[11px] uppercase tracking-[0.18em] text-zinc-500">Edge Glow</div>
                <div className="text-lg text-white">{(ui.layers.edge * 100).toFixed(0)}%</div>
              </div>
              <div className="rounded-2xl border border-white/10 bg-white/5 p-4">
                <div className="mb-1 text-[11px] uppercase tracking-[0.18em] text-zinc-500">Chromatic</div>
                <div className="text-lg text-white">{(ui.layers.chromatic * 100).toFixed(0)}%</div>
              </div>
              <div className="rounded-2xl border border-white/10 bg-white/5 p-4">
                <div className="mb-1 text-[11px] uppercase tracking-[0.18em] text-zinc-500">Distortion</div>
                <div className="text-lg text-white">{(ui.layers.distortion * DISTORTION_CAP).toFixed(3)}</div>
              </div>
              <div className="rounded-2xl border border-white/10 bg-white/5 p-4">
                <div className="mb-1 text-[11px] uppercase tracking-[0.18em] text-zinc-500">Overdrive</div>
                <div className="text-lg text-white">{(ui.layers.overdrive * 100).toFixed(0)}%</div>
              </div>
            </div>
          </section>

          <section className="pointer-events-auto flex flex-col justify-between rounded-[2rem] border border-white/10 bg-black/40 p-6 backdrop-blur-xl md:col-span-3">
            <div>
              <div className="mb-4 flex items-center gap-2 text-[11px] uppercase tracking-[0.28em] text-zinc-400">
                <Shield className="h-4 w-4" />
                <span>Comfort Layer</span>
              </div>
              <div className="space-y-3 text-sm text-zinc-300">
                <div className="rounded-2xl border border-white/10 bg-white/5 p-4">
                  Distortion stays clamped to <span className="font-mono text-white">0.05</span> UV offset.
                </div>
                <div className="rounded-2xl border border-white/10 bg-white/5 p-4">
                  Reset fades all layers to zero over <span className="font-mono text-white">1.5s</span>.
                </div>
                <div className="rounded-2xl border border-white/10 bg-white/5 p-4">
                  Use <span className="font-mono text-white">Space</span>, <span className="font-mono text-white">B</span>, or <span className="font-mono text-white">Y</span> for emergency stop.
                </div>
              </div>
            </div>

            <button
              onClick={() => resetExperience()}
              className="mt-6 flex items-center justify-center gap-3 rounded-[1.5rem] border border-red-400/20 bg-red-500/10 px-4 py-4 text-sm uppercase tracking-[0.24em] text-red-200 transition hover:bg-red-500/15"
            >
              <RefreshCw className="h-5 w-5" />
              Reset Trip
            </button>
          </section>
        </main>
      </div>

      <AnimatePresence>
        {showGuide && (
          <motion.div
            initial={{ opacity: 0 }}
            animate={{ opacity: 1 }}
            exit={{ opacity: 0 }}
            className="fixed inset-0 z-50 flex items-center justify-center bg-black/80 p-6 backdrop-blur-md"
          >
            <motion.div
              initial={{ y: 20, opacity: 0 }}
              animate={{ y: 0, opacity: 1 }}
              exit={{ y: 20, opacity: 0 }}
              className="w-full max-w-2xl rounded-[2rem] border border-white/10 bg-zinc-950/95 p-8"
            >
              <div className="mb-5 flex items-center justify-between">
                <h2 className="text-2xl font-light tracking-[-0.03em]">Quest XR Simulator Notes</h2>
                <button onClick={() => setShowGuide(false)} className="text-sm text-zinc-400 hover:text-white">
                  Close
                </button>
              </div>
              <div className="space-y-4 text-sm leading-relaxed text-zinc-300">
                <p>
                  This local app mirrors the intended <strong>MotionToExperienceManager</strong> flow: smoothed motion analysis,
                  temporal inertia, four-state machine, layered attack and release timing, and comfort-safe visual caps for
                  passthrough-oriented effects.
                </p>
                <p>
                  Turn off the debug slider to drive the score with mouse motion. Leave it on to tune values before wearing a headset,
                  just like the Unity-side editor slider requested for the Quest build.
                </p>
                <div className="rounded-2xl border border-sky-400/20 bg-sky-400/10 p-4 text-sky-100">
                  <div className="mb-1 flex items-center gap-2 font-medium">
                    <AlertCircle className="h-4 w-4" />
                    <span>Implementation path</span>
                  </div>
                  <div>
                    Unity runtime logic now lives under <code>Assets/CriticalVR/Scripts/Runtime</code>, while Quest deployment
                    guidance lives under <code>Docs/Quest</code>.
                  </div>
                </div>
              </div>
            </motion.div>
          </motion.div>
        )}
      </AnimatePresence>

      <div className="pointer-events-none fixed bottom-5 left-1/2 z-20 -translate-x-1/2 rounded-full border border-white/10 bg-black/45 px-4 py-2 text-[11px] uppercase tracking-[0.24em] text-zinc-400 backdrop-blur-md">
        Move mouse for head motion simulation. Space, B, or Y resets.
      </div>
    </div>
  );
}
