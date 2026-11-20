# Runtime architecture

`main.ts` composes one simulation, input adapter, orthographic Three.js world and diagnostics view. The simulation modules import neither Three.js nor browser APIs. The public flow for each visible animation frame is:

```text
keyboard state + held pointers → direction axes → Simulation.step(elapsed seconds, axes)
  ├─ continuous state: bounded player and optional creature patrol
  └─ measurement deadline
       AP positions + truth + creature → path geometry → noisy measured dBm
       measured dBm → distances → least-squares position → sampled error
       per-AP history → range + recent creature samples → status
SimulationState → world renderer
new Sample → estimate ring + diagnostics
```

## State and units

`config.ts` defines the room, ordered AP coordinates and default parameters. A session owns one editable config. `SimulationState` contains simulation time in seconds, continuous player and creature positions in meters, and the latest `Sample`. Each observation includes full-precision ideal/measured dBm, noise and losses in dB, inverted distance, path geometry, history range and recency. Each AP owns a fixed twelve-slot buffer. A sample contains a nullable estimate and its ground-truth error at that instant. The estimator accepts only coordinates and recovered distances. All display rounding is downstream.

## Sampling and time

Initialization primes AP histories, then samples and solves at time zero. This models Unity's bootstrap refresh followed by its first LateUpdate. Every frame advances movement and absolute-time sinusoidal patrol. When a step crosses a sample deadline it advances to the deadline, measures there, then integrates the remainder. Holding a direction for the same simulation duration yields the same noiseless samples across frame rates. An edited shorter interval that is already due takes one reading at the current time and schedules forward.

The animation loop suspends while the document is hidden and discards the previous timestamp on visibility changes. An unexplained frame gap over one second advances 0.25 s. This bounds catch-up and avoids teleporting after suspension. The UI clears held controls on blur and visibility changes. Disabling an active patrol freezes the current creature position, as Unity's early return does. Editing its origin repositions it. Reset creates fresh state and histories.

## Randomness

Each AP takes a separate uniform draw per observation when amplitude is positive. Normal play uses `Math.random`. The diagnostic `?seed` option selects an unsigned 32-bit LCG. Tests can inject any deterministic uniform source. Draw order is NW, NE, SW, SE.

Creature modulation is a separate, continuous 2D gradient-noise function with quintic interpolation. The stable C# identifier hash starts at 17, multiplies by 31 and adds each UTF-16 code unit with 32-bit wraparound. Its nonnegative result supplies the X coordinate `(hash % 997) * 0.173 + 17.31`. Y is sample time times frequency (default 0.7, minimum 0.01). Noise in [0,1] interpolates between two-thirds and all of the band amplitude, applied as negative dB. Browser doubles, lattice gradients and uniform random streams differ from Unity floats and engine RNGs. The seed controls the browser experiment only.

## Rendering and cost

The world is an XY plane with +Y north. A ResizeObserver maintains equal pixels per meter on both axes and places the DOM AP labels against the same orthographic extents. The room has a margin for estimates near its boundary. A distant estimate can leave the viewport. Geometry is allocated at startup. The renderer moves the two continuous markers every frame and places or hides the ring when the Sample identity changes. Debug segments reuse position buffers. Like Unity, line endpoints follow live transforms while nearest points and band colors come from the latest measurement. Debug path readouts share that measurement.

DOM rows, markers and knob fields are reused. A measurement scans four paths and at most twelve history values per AP. Rendering follows requestAnimationFrame and caps pixel ratio at two. There are no runtime requests for fonts, art, services or data beyond the static application files.
