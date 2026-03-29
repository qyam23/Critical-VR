import React, { useState, useEffect, useRef, useCallback } from 'react';
import { 
  Activity, 
  Zap, 
  Shield, 
  RefreshCw, 
  Camera as CameraIcon,
  Settings,
  Info,
  AlertCircle
} from 'lucide-react';
import { motion, AnimatePresence } from 'motion/react';

// --- Types ---
type ExperienceState = 'Calm' | 'Shift' | 'Intense' | 'Recovery';

export default function App() {
  // --- State ---
  const [intensityScore, setIntensityScore] = useState(0);
  const [state, setState] = useState<ExperienceState>('Calm');
  const [mode, setMode] = useState<'Psychedelic' | 'Empathic'>('Psychedelic');
  const [isExperienceActive, setIsExperienceActive] = useState(false);
  const [isResetting, setIsResetting] = useState(false);
  const [useCamera, setUseCamera] = useState(false);
  const [sensitivity, setSensitivity] = useState(0.8);
  const [decayRate, setDecayRate] = useState(1.2);
  const [showGuide, setShowGuide] = useState(false);

  // --- Refs for Motion Calculation ---
  const lastMousePos = useRef({ x: 0, y: 0 });
  const lastTime = useRef(Date.now());
  const velocity = useRef(0);
  const videoRef = useRef<HTMLVideoElement>(null);

  // --- Reset Logic ---
  const resetExperience = useCallback(() => {
    setIsResetting(true);
    const startScore = intensityScore;
    const duration = 1500;
    const startTime = Date.now();

    const fade = () => {
      const elapsed = Date.now() - startTime;
      const progress = Math.min(elapsed / duration, 1);
      const current = startScore * (1 - progress);
      
      setIntensityScore(current);

      if (progress < 1) {
        requestAnimationFrame(fade);
      } else {
        setIsResetting(false);
        setIntensityScore(0);
      }
    };
    requestAnimationFrame(fade);
  }, [intensityScore]);

  // --- Mouse Motion Simulation ---
  useEffect(() => {
    const handleMouseMove = (e: MouseEvent) => {
      if (isResetting) return;

      const now = Date.now();
      const dt = (now - lastTime.current) / 1000;
      if (dt === 0) return;

      const dx = e.clientX - lastMousePos.current.x;
      const dy = e.clientY - lastMousePos.current.y;
      const dist = Math.sqrt(dx * dx + dy * dy);
      
      const currentVel = dist / dt;
      const accel = Math.abs(currentVel - velocity.current) / dt;
      
      velocity.current = currentVel;
      lastMousePos.current = { x: e.clientX, y: e.clientY };
      lastTime.current = now;

      // Update Intensity Score
      setIntensityScore(prev => {
        const motionContribution = (accel * 0.00005) * sensitivity;
        return Math.min(Math.max(prev + motionContribution, 0), 1);
      });
    };

    window.addEventListener('mousemove', handleMouseMove);
    return () => window.removeEventListener('mousemove', handleMouseMove);
  }, [isResetting, sensitivity]);

  // --- Decay & Layer Logic ---
  const [layers, setLayers] = useState({
    distortion: 0,
    patterns: 0,
    color: 0,
    breathing: 0
  });
  const [breathingScale, setBreathingScale] = useState(1);
  const [trailIntensity, setTrailIntensity] = useState(0);

  const [intensityTrend, setIntensityTrend] = useState(0);
  const lastScore = useRef(0);
  const motionHistory = useRef<number[]>([]);

  // Simulation Constants
  const ENTER_THRESHOLD = 0.15;
  const EXIT_THRESHOLD = 0.08;
  const ATTACK = { distortion: 0.1, patterns: 0.05, color: 0.08, breathing: 0.03 };
  const RELEASE = { distortion: 0.15, patterns: 0.08, color: 0.04, breathing: 0.02 };

  useEffect(() => {
    const interval = setInterval(() => {
      if (isResetting) return;
      
      // 1. Simulate Motion Input (Rolling History for Inertia)
      const time = Date.now() / 1000;
      const baseMotion = (Math.sin(time * 0.5) * 0.5 + 0.5) * 0.3;
      const mouseInfluence = Math.min(velocity.current * 0.0005, 0.7);
      const rawMotion = baseMotion + mouseInfluence;

      motionHistory.current.push(rawMotion);
      if (motionHistory.current.length > 60) motionHistory.current.shift();
      const avgMotion = motionHistory.current.reduce((a, b) => a + b, 0) / motionHistory.current.length;

      // 2. Hysteresis Logic
      setIsExperienceActive(prev => {
        if (!prev && avgMotion > ENTER_THRESHOLD) return true;
        if (prev && avgMotion < EXIT_THRESHOLD && intensityScore < 0.05) return false;
        return prev;
      });

      // 3. Update Intensity Score
      setIntensityScore(prev => {
        let next = prev;
        if (isExperienceActive) {
          next += (avgMotion * 0.05);
        }
        const decay = Math.exp(-decayRate * 0.05);
        next = Math.max(next * decay - 0.001, 0);
        
        // Trend calculation
        const trend = (next - lastScore.current) / 0.05;
        setIntensityTrend(trend);
        lastScore.current = next;
        return Math.min(next, 1);
      });

      // 4. Layered Attack/Release
      setLayers(prev => {
        const targetDistortion = mode === 'Psychedelic' ? Math.pow(intensityScore, 0.6) : intensityScore * 0.3;
        const targetPatterns = mode === 'Psychedelic' ? (intensityScore > 0.5 ? Math.pow((intensityScore - 0.5) / 0.5, 2) : 0) : intensityScore * 0.1;
        const targetColor = mode === 'Psychedelic' ? intensityScore : Math.pow(intensityScore, 0.8);
        const targetBreathing = mode === 'Psychedelic' ? Math.pow(intensityScore, 1.5) : intensityScore * 0.5;

        const smooth = (curr: number, target: number, key: keyof typeof ATTACK) => {
          const speed = target > curr ? ATTACK[key] : RELEASE[key];
          return curr + (target - curr) * speed;
        };

        return {
          distortion: smooth(prev.distortion, targetDistortion, 'distortion'),
          patterns: smooth(prev.patterns, targetPatterns, 'patterns'),
          color: smooth(prev.color, targetColor, 'color'),
          breathing: smooth(prev.breathing, targetBreathing, 'breathing')
        };
      });

      // 5. Breathing effect
      const breathingFreq = mode === 'Psychedelic' ? 0.4 + intensityScore * 1.6 : 0.3 + intensityScore * 0.7;
      const breathing = 1 + Math.sin(Date.now() * 0.002 * breathingFreq) * 0.02 * layers.breathing;
      setBreathingScale(breathing);

      // 6. Trail intensity
      setTrailIntensity(prevTrail => Math.max(prevTrail * 0.9, velocity.current > 500 ? 1 : 0));

    }, 50);
    return () => clearInterval(interval);
  }, [isResetting, decayRate, isExperienceActive, intensityScore, mode, layers.breathing]);

  // --- State Machine ---
  useEffect(() => {
    if (intensityScore < 0.12) setState('Calm');
    else if (intensityTrend < -0.25 && intensityScore > 0.35) setState('Recovery');
    else if (intensityScore < 0.5) setState('Shift');
    else setState('Intense');
  }, [intensityScore, intensityTrend]);

  // --- Camera Setup ---
  useEffect(() => {
    if (useCamera && videoRef.current) {
      navigator.mediaDevices.getUserMedia({ video: true })
        .then(stream => {
          if (videoRef.current) videoRef.current.srcObject = stream;
        })
        .catch(err => {
          console.error("Camera error:", err);
          setUseCamera(false);
        });
    }
  }, [useCamera]);

  // --- Visual Styles ---
  const filterStyles = {
    filter: `
      contrast(${100 + layers.color * (mode === 'Psychedelic' ? 60 : 30)}%)
      saturate(${100 + layers.color * (mode === 'Psychedelic' ? 120 : 60)}%)
      hue-rotate(${layers.color * (mode === 'Psychedelic' ? 120 : 20)}deg)
      blur(${layers.distortion * 1.5}px)
      ${state === 'Recovery' ? 'sepia(0.3) brightness(1.1)' : ''}
      ${mode === 'Empathic' ? 'sepia(0.2) brightness(1.05)' : ''}
      url(#radialDistortion)
    `,
    transform: `scale(${breathingScale + layers.distortion * 0.04}) rotate(${layers.distortion * (mode === 'Psychedelic' ? 1.5 : 0.5)}deg)`,
  };

  const peripheralMaskStyle = {
    maskImage: `radial-gradient(circle, transparent ${40 - layers.distortion * 20}%, black ${80 - layers.distortion * 10}%)`,
    WebkitMaskImage: `radial-gradient(circle, transparent ${40 - layers.distortion * 20}%, black ${80 - layers.distortion * 10}%)`,
  };

  const edgeGlowStyle = {
    boxShadow: `inset 0 0 ${layers.color * 120}px ${mode === 'Psychedelic' ? 'rgba(168, 85, 247, 0.8)' : 'rgba(251, 191, 36, 0.6)'}`,
    border: `${layers.color * 12}px solid ${mode === 'Psychedelic' ? 'rgba(168, 85, 247, 0.6)' : 'rgba(251, 191, 36, 0.4)'}`,
    filter: `drop-shadow(0 0 ${layers.color * 10}px rgba(255, 255, 255, ${layers.color * 0.5}))`,
    ...peripheralMaskStyle
  };

  return (
    <div className="min-h-screen bg-black text-white font-sans selection:bg-purple-500/30 overflow-hidden flex flex-col">
      {/* --- Background / Passthrough Simulator --- */}
      <svg className="hidden">
        <filter id="radialDistortion">
          <feTurbulence 
            type="fractalNoise" 
            baseFrequency="0.015" 
            numOctaves="3" 
            seed="2"
          >
            <animate 
              attributeName="baseFrequency" 
              values="0.015;0.02;0.015" 
              dur="8s" 
              repeatCount="indefinite" 
            />
          </feTurbulence>
          <feDisplacementMap 
            in="SourceGraphic" 
            scale={layers.distortion * 80} 
            xChannelSelector="R" 
            yChannelSelector="G" 
          />
        </filter>
      </svg>

      <div className="fixed inset-0 z-0 overflow-hidden pointer-events-none">
        {/* Trail Layer (Ghosting) */}
        {trailIntensity > 0.1 && (
          <div 
            className="absolute inset-0 opacity-30 mix-blend-screen transition-transform duration-200"
            style={{ 
              ...filterStyles, 
              transform: `${filterStyles.transform} translate(10px, 5px)`,
              filter: `${filterStyles.filter} brightness(1.5)`
            }}
          >
            {useCamera ? (
              <video 
                ref={videoRef} 
                autoPlay 
                playsInline 
                className="w-full h-full object-cover"
              />
            ) : (
              <div className="w-full h-full bg-zinc-900/50" />
            )}
          </div>
        )}

        <div 
          className="absolute inset-0 transition-all duration-300 ease-out"
          style={filterStyles}
        >
          {useCamera ? (
            <video 
              ref={videoRef} 
              autoPlay 
              playsInline 
              className="w-full h-full object-cover opacity-60"
            />
          ) : (
            <div className="w-full h-full bg-gradient-to-br from-zinc-900 to-black flex items-center justify-center">
              <div className="grid grid-cols-8 gap-4 opacity-20">
                {Array.from({ length: 64 }).map((_, i) => (
                  <div key={i} className="w-12 h-12 border border-white/20 rounded-full animate-pulse" style={{ animationDelay: `${i * 0.1}s` }} />
                ))}
              </div>
            </div>
          )}
        </div>
        
        {/* Edge Glow Overlay */}
        <div 
          className="absolute inset-0 transition-all duration-300 pointer-events-none"
          style={edgeGlowStyle}
        />

        {/* Chromatic Aberration Simulation (Simplified) */}
        {layers.color > 0.1 && (
          <div className="absolute inset-0 mix-blend-screen pointer-events-none overflow-hidden">
            <div 
              className="absolute inset-0 bg-red-500/20 transition-transform duration-75" 
              style={{ transform: `translateX(${layers.color * 10}px) scale(${1 + layers.color * 0.02})` }}
            />
            <div 
              className="absolute inset-0 bg-blue-500/20 transition-transform duration-75" 
              style={{ transform: `translateX(-${layers.color * 10}px) scale(${1 + layers.color * 0.02})` }}
            />
          </div>
        )}

        {/* Layer B: Pattern Overlay Simulation */}
        <AnimatePresence>
          {layers.patterns > 0.1 && (
            <motion.div
              initial={{ opacity: 0 }}
              animate={{ opacity: layers.patterns * 0.3 }}
              exit={{ opacity: 0 }}
              className="absolute inset-0 z-10 pointer-events-none mix-blend-overlay"
              style={{
                backgroundImage: `radial-gradient(circle at 50% 50%, transparent 0%, rgba(139, 92, 246, 0.2) 100%), 
                                 repeating-conic-gradient(from 0deg, transparent 0deg 30deg, rgba(255,255,255,0.05) 30deg 60deg)`
              }}
            />
          )}
        </AnimatePresence>
      </div>

      {/* --- UI Layer --- */}
      <header className="relative z-10 p-6 flex justify-between items-start pointer-events-none">
        <div className="pointer-events-auto">
          <div className="flex items-center gap-3 mb-1">
            <div className="w-3 h-3 rounded-full bg-purple-500 animate-ping" />
            <h1 className="text-xs font-mono uppercase tracking-[0.2em] text-zinc-400">Quest Motion Trip</h1>
          </div>
          <div className="text-4xl font-light tracking-tighter flex items-baseline gap-4">
            <span>{state}</span>
            <span className="text-sm font-mono text-zinc-500">{(intensityScore * 100).toFixed(1)}%</span>
          </div>
        </div>

        <div className="flex flex-col gap-4 pointer-events-auto items-end">
          <button 
            onClick={() => setShowGuide(!showGuide)}
            className="p-3 rounded-full bg-white/5 border border-white/10 hover:bg-white/10 transition-colors"
          >
            <Info className="w-5 h-5 text-zinc-400" />
          </button>
          
          <button 
            onClick={() => setUseCamera(!useCamera)}
            className={`p-3 rounded-full border transition-all ${useCamera ? 'bg-purple-500/20 border-purple-500/50 text-purple-400' : 'bg-white/5 border-white/10 text-zinc-400'}`}
          >
            <CameraIcon className="w-5 h-5" />
          </button>
        </div>
      </header>

      <main className="relative z-10 flex-1 flex flex-col justify-end p-8 gap-8 pointer-events-none">
        {/* Visual Feedback Bar */}
        <div className="w-full max-w-2xl mx-auto space-y-4 pointer-events-auto">
          <div className="h-1 w-full bg-white/10 rounded-full overflow-hidden">
            <motion.div 
              className="h-full bg-gradient-to-r from-blue-500 via-purple-500 to-pink-500"
              initial={{ width: 0 }}
              animate={{ width: `${intensityScore * 100}%` }}
              transition={{ type: 'spring', bounce: 0, duration: 0.2 }}
            />
          </div>
          
          <div className="flex justify-between items-center text-[10px] font-mono uppercase tracking-widest text-zinc-500">
            <div className="flex items-center gap-2">
              <Activity className="w-3 h-3" />
              <span>Motion Analyzer Active</span>
            </div>
            <div className="flex items-center gap-2">
              <Shield className="w-3 h-3" />
              <span>Safety Limits: Clamped 0.05 UV</span>
            </div>
          </div>
        </div>

        {/* Controls Panel */}
        <div className="grid grid-cols-1 md:grid-cols-3 gap-6 pointer-events-auto">
          <div className="bg-black/40 backdrop-blur-xl border border-white/10 p-6 rounded-3xl space-y-4">
            <div className="flex items-center gap-2 text-xs font-mono text-zinc-400 uppercase">
              <Settings className="w-3 h-3" />
              <span>Experience Mode</span>
            </div>
            <div className="flex gap-2">
              <button 
                onClick={() => setMode('Psychedelic')}
                className={`flex-1 py-2 px-3 rounded-xl text-[10px] font-mono transition-all border ${
                  mode === 'Psychedelic' 
                    ? 'bg-purple-500/20 border-purple-500 text-purple-300 shadow-[0_0_10px_rgba(168,85,247,0.3)]' 
                    : 'bg-zinc-800/50 border-zinc-700 text-zinc-500 hover:border-zinc-500'
                }`}
              >
                PSYCHEDELIC
              </button>
              <button 
                onClick={() => setMode('Empathic')}
                className={`flex-1 py-2 px-3 rounded-xl text-[10px] font-mono transition-all border ${
                  mode === 'Empathic' 
                    ? 'bg-amber-500/20 border-amber-500 text-amber-300 shadow-[0_0_10px_rgba(245,158,11,0.3)]' 
                    : 'bg-zinc-800/50 border-zinc-700 text-zinc-500 hover:border-zinc-500'
                }`}
              >
                EMPATHIC
              </button>
            </div>
          </div>

          <div className="bg-black/40 backdrop-blur-xl border border-white/10 p-6 rounded-3xl space-y-4">
            <div className="flex items-center gap-2 text-xs font-mono text-zinc-400 uppercase">
              <Settings className="w-3 h-3" />
              <span>C# Parameters</span>
            </div>
            <div className="space-y-6">
              <div className="space-y-2">
                <div className="flex justify-between text-[11px]">
                  <span className="text-zinc-500">Sensitivity</span>
                  <span>{sensitivity.toFixed(1)}</span>
                </div>
                <input 
                  type="range" min="0.1" max="2" step="0.1" 
                  value={sensitivity} onChange={e => setSensitivity(parseFloat(e.target.value))}
                  className="w-full accent-purple-500"
                />
              </div>
              <div className="space-y-2">
                <div className="flex justify-between text-[11px]">
                  <span className="text-zinc-500">Decay Rate</span>
                  <span>{decayRate.toFixed(1)}</span>
                </div>
                <input 
                  type="range" min="0.1" max="3" step="0.1" 
                  value={decayRate} onChange={e => setDecayRate(parseFloat(e.target.value))}
                  className="w-full accent-purple-500"
                />
              </div>
            </div>
          </div>

          <div className="bg-black/40 backdrop-blur-xl border border-white/10 p-6 rounded-3xl flex flex-col justify-between items-center text-center gap-4">
            <div className="space-y-1">
              <h3 className="text-sm font-medium">Emergency Reset</h3>
              <p className="text-[10px] text-zinc-500 leading-relaxed uppercase tracking-tighter">
                Simulates Controller Button B/Y
              </p>
            </div>
            <button 
              onClick={resetExperience}
              disabled={isResetting || intensityScore === 0}
              className="group relative w-20 h-20 rounded-full bg-zinc-900 border border-white/10 flex items-center justify-center hover:bg-red-500/20 hover:border-red-500/50 transition-all disabled:opacity-30 disabled:cursor-not-allowed"
            >
              <RefreshCw className={`w-8 h-8 text-zinc-400 group-hover:text-red-400 transition-colors ${isResetting ? 'animate-spin' : ''}`} />
            </button>
          </div>

          <div className="bg-black/40 backdrop-blur-xl border border-white/10 p-6 rounded-3xl space-y-4">
            <div className="flex items-center gap-2 text-xs font-mono text-zinc-400 uppercase">
              <Zap className="w-3 h-3" />
              <span>Live Telemetry</span>
            </div>
            <div className="grid grid-cols-2 gap-2 text-[10px] font-mono">
              <div className="bg-white/5 p-3 rounded-xl border border-white/5">
                <div className="text-zinc-500 mb-1 uppercase">Motion History</div>
                <div className="text-lg">
                  {(motionHistory.current.reduce((a, b) => a + b, 0) / (motionHistory.current.length || 1)).toFixed(3)}
                </div>
              </div>
              <div className="bg-white/5 p-3 rounded-xl border border-white/5">
                <div className="text-zinc-500 mb-1 uppercase">Active State</div>
                <div className={`text-lg ${isExperienceActive ? 'text-green-400' : 'text-zinc-600'}`}>
                  {isExperienceActive ? 'ACTIVE' : 'IDLE'}
                </div>
              </div>
              <div className="bg-white/5 p-3 rounded-xl border border-white/5">
                <div className="text-zinc-500 mb-1 uppercase">Distortion (L-A)</div>
                <div className="text-lg">{(layers.distortion * 100).toFixed(1)}%</div>
              </div>
              <div className="bg-white/5 p-3 rounded-xl border border-white/5">
                <div className="text-zinc-500 mb-1 uppercase">Patterns (L-B)</div>
                <div className="text-lg">{(layers.patterns * 100).toFixed(1)}%</div>
              </div>
              <div className="bg-white/5 p-3 rounded-xl border border-white/5">
                <div className="text-zinc-500 mb-1 uppercase">Color (L-C)</div>
                <div className="text-lg">{(layers.color * 100).toFixed(1)}%</div>
              </div>
              <div className="bg-white/5 p-3 rounded-xl border border-white/5">
                <div className="text-zinc-500 mb-1 uppercase">Breathing</div>
                <div className="text-lg">{(breathingScale).toFixed(3)}x</div>
              </div>
              <div className="col-span-2 bg-white/5 p-3 rounded-xl border border-white/5 flex justify-between items-center">
                <span className="text-zinc-500 uppercase">Safety Status</span>
                <span className={intensityScore > 0.8 ? 'text-red-400' : 'text-green-400'}>
                  {intensityScore > 0.8 ? 'LIMIT REACHED' : 'NOMINAL'}
                </span>
              </div>
            </div>
          </div>
        </div>
      </main>

      {/* --- Overlays --- */}
      <AnimatePresence>
        {showGuide && (
          <motion.div 
            initial={{ opacity: 0, y: 20 }}
            animate={{ opacity: 1, y: 0 }}
            exit={{ opacity: 0, y: 20 }}
            className="fixed inset-0 z-50 flex items-center justify-center p-6 bg-black/80 backdrop-blur-md"
          >
            <div className="max-w-xl w-full bg-zinc-900 border border-white/10 rounded-[2rem] p-8 space-y-6">
              <div className="flex justify-between items-center">
                <h2 className="text-2xl font-light">Project Directive Guide</h2>
                <button onClick={() => setShowGuide(false)} className="text-zinc-500 hover:text-white">Close</button>
              </div>
              
              <div className="space-y-4 text-sm text-zinc-400 leading-relaxed">
                <p>
                  This simulator demonstrates the <strong>MotionToExperienceManager</strong> logic. 
                  Move your mouse rapidly to simulate head motion and increase the <strong>Intensity Score</strong>.
                </p>
                <div className="grid grid-cols-1 gap-3">
                  <div className="flex gap-3 items-start">
                    <div className="w-6 h-6 rounded bg-purple-500/20 text-purple-400 flex items-center justify-center shrink-0">1</div>
                    <p><strong>Edge Glow:</strong> Sobel filter simulation increases with score.</p>
                  </div>
                  <div className="flex gap-3 items-start">
                    <div className="w-6 h-6 rounded bg-purple-500/20 text-purple-400 flex items-center justify-center shrink-0">2</div>
                    <p><strong>Distortion:</strong> UV offset is clamped to 0.05 to prevent motion sickness.</p>
                  </div>
                  <div className="flex gap-3 items-start">
                    <div className="w-6 h-6 rounded bg-purple-500/20 text-purple-400 flex items-center justify-center shrink-0">3</div>
                    <p><strong>Decay:</strong> Score drops exponentially when motion stops.</p>
                  </div>
                </div>
                <div className="p-4 bg-blue-500/10 border border-blue-500/20 rounded-2xl flex gap-3 text-blue-400">
                  <AlertCircle className="w-5 h-5 shrink-0" />
                  <p className="text-xs italic">
                    Note: The Unity C# script and Shader Graph guides are available in the project files for your Quest implementation.
                  </p>
                </div>
              </div>
            </div>
          </motion.div>
        )}
      </AnimatePresence>

      {/* Instructions Toast */}
      <div className="fixed bottom-4 left-1/2 -translate-x-1/2 z-20 pointer-events-none">
        <div className="bg-white/5 backdrop-blur-md border border-white/10 px-4 py-2 rounded-full text-[10px] uppercase tracking-widest text-zinc-500">
          Move Mouse to Simulate Motion • Press Space for Reset
        </div>
      </div>
    </div>
  );
}
