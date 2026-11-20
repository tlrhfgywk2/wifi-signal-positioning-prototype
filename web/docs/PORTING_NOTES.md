# Returning finalized mechanics to Unity

Use the committed Unity baseline and the browser parity checklist as the starting pair. Keep the equations in engine-independent C# and adapt transforms, Time, Input System, material placement and uGUI at their boundaries. Porting proceeds through behavioral fixtures.

## Inputs and outputs to preserve

| Boundary | Inputs | Observable result |
|---|---|---|
| Movement | Meters, seconds, keyboard axes and independent held-button axes | Combined vector normalized above unit magnitude, continuous bounded true position |
| Creature | Origin, normalized direction, patrol enable/distance/speed, absolute simulation seconds | Current XY position |
| Synthesis | Ordered AP IDs/coordinates/static losses, player and creature XY, radio config, sample time, uniform draw per AP | Full-precision expected/measured dBm, signed loss, delta, path geometry, recovered distance |
| Per-AP memory | Finite measured value and current interference level | Last twelve values, range, remaining creature recency and classified status |
| Estimator | First reference AP followed by additional position/distance pairs | Valid XY or failure |
| Sample publication | One completed set of observations, estimate and true XY at the same instant | Coordinates, marker visibility, error and rows from one sample |

1. Take a reviewed design decision from the roadmap log and record which constants, equations, clock semantics or failure cases changed. Version fixtures with that decision.
2. Translate the corresponding pure functions to C#. Keep uniform noise injectable and time explicit. Copy the port's hash/gradients and seed algorithm only if exact repeatable diagnostic streams are required. Otherwise compare closed-form outputs and noise properties.
3. Feed identical noiseless observations to both estimators. The target (1.25, −2.1) must recover within 1e−4 m for C# floats. Repeat with invalid AP zero, two collinear rows, infinite distance and an intentionally outside-room solution.
4. Replay a known input trajectory with deterministic time. Check the 0.14 m inset, normalized mixed input, sample/error hold, startup history priming and hidden ring on failure. Run the boundary fixtures for the three bands and AP radius, then record one creature hit followed by twelve clear samples. Compare status and expiry at every step.
5. Bind visuals last. Keep the true transform usable only in synthesis, movement and error calculation. Check z-order, AP labels, diagnostics and control usability in Play Mode. If a design decision changes a baseline behavior, update both the fixture and the decision log.

The existing Editor entry points are `WifiPositioningPrototypeEditor.WifiPrototypeSceneGenerator.ValidateCoreModel` and `RunPlayModeSmokeTest`. The latter exits the Editor itself when run in batch mode. The repository's root handoff describes the Unity command lines.
