// Runs the actual simulation callbacks without React rendering or browser timers.
const fs = require('node:fs');
const vm = require('node:vm');
const assert = require('node:assert/strict');
const ts = require('typescript');
const source = fs.readFileSync(require('node:path').join(__dirname, '../src/App.tsx'), 'utf8');
const take = (start, end) => source.slice(source.indexOf(start), source.indexOf(end, source.indexOf(start)));
const code = [
 take('const ENTER_THRESHOLD', 'export default function App'),
 'const useRef = value => ({current:value}); const useEffectEvent = fn => fn;',
 'const paramsRef = {current:{mode:"Psychedelic",useDebugSlider:true,debugIntensity:0.9,sensitivity:1,decayRate:1.25}};',
 'let introProgress = 1; const syncUi = () => {}; const setUseDebugSlider = value => {paramsRef.current.useDebugSlider=value};',
 take('  const simRef = useRef<SimulationState>', '  const syncUi'),
 take('  const resetExperience =', '  const updatePointerMotion'),
 take('  const stepSimulation =', '  useEffect(() => {'),
 'globalThis.api = { step:stepSimulation, reset:resetExperience, sim:simRef.current, params:paramsRef.current };'
].join('\n');
const context = { performance: {now:()=>0} };
vm.createContext(context);
vm.runInContext(ts.transpileModule(code, {compilerOptions:{target:ts.ScriptTarget.ES2022}}).outputText, context);
const {step,reset,sim,params} = context.api;
const advance = seconds => {for(let i=0;i<seconds*60;i++) step(1/60)};
advance(8);
assert(sim.intensityScore > 0.78, 'high debug input activates peak');
params.useDebugSlider=false; sim.pendingMotion=0;
advance(40);
assert(sim.intensityScore < 0.01, 'peak hold expires and motion-free input decays');
params.useDebugSlider=true; params.debugIntensity=0.9;
advance(8); reset(); advance(2);
assert.equal(sim.intensityScore,0,'reset fades to zero');
assert.equal(params.useDebugSlider,false,'reset disables automatic debug drive');
advance(20);
assert.equal(sim.intensityScore,0,'idle stays at zero after reset');
for (const value of Object.values(sim.layers)) assert.equal(value,0);
console.log('PASS: peak activation, hold expiry, decay, reset and idle stability (actual simulator callbacks).');
