# Unity-to-web parity checklist

## Baseline inspected

The remote default branch `main` was the Unity skeleton at `0435936f9d7a27d4bbe85e63f29960812ebcaf4e`. Draft PR #1 was open from `codex/wifi-signal-positioning-prototype` at `d76b5ff4e7951ec2d4085eff3da03cd32e5b5cad`, following the interference implementation at `c7c11b5`. The private repository had no web app or Pages workflow. The new branch `codex/threejs-parity-v0.1` started at the clean Unity feature tip.

The reference is Unity 6000.3.19f1, scene `Assets/Scenes/WifiSignalPositioningPrototype.unity`. Its serialized root references the serialized creature. The bootstrap creates the other runtime objects. Scene YAML, all twelve runtime scripts, the Editor generator and its core/play-mode validations were reviewed alongside the root README and CODEX_HANDOFF.

## Implementation and verification

“Unit” means executable TypeScript checks. “Browser” means Chromium exercised the production build. “Source” means the constants or behavior were mapped by code inspection. Visual matching in Unity Play Mode is tracked separately below.

| Unity source and behavior | Browser equivalent under web/ | Evidence |
|---|---|---|
| Scene + PrototypeBootstrap.BuildPrototype, room and fallback creature settings | simulation/config.ts and main.ts create one session with the serialized defaults. One creature begins at (−1.2, 1.1). | Source, unit |
| BuildCamera, black clear, ortho half-height 6.2, view left of diagnostics | rendering/scene.ts and styles/main.css maintain square meters with responsive panel placement. | Browser, source |
| RoomBounds, BuildRoom | Centered 10 × 10 m white outline, width 0.035 m. Center clamps to ±4.86 on each axis. | Unit, source |
| BuildPlayer, PlayerMovement | White disk radius 0.14 m, 28 segments. Start at origin, 3 m/s. Bootstrap's 0.14 m inset overrides the component's 0.12 m initializer. | Unit, browser |
| DirectionalInputButton, read keyboard/UI, EventSystem | input/controls.ts ORs WASD/arrow aliases, adds separate UI axes, then move normalizes combined magnitude above one. Four held pointer controls release on up/exit/cancel. | Unit, browser |
| BuildAccessPoints / TextMesh | NW, NE, SW, SE in Unity order at (±4.55, ±4.55), inset 0.45, diamond radius 0.22. Label offsets 0.35 horizontally and 0.42 vertically toward center. | Unit, browser, source |
| AccessPointSignal / LinearSignalModel | simulation/radioModel.ts computes ideal −20 − 5 × distance, subtracts per-AP static attenuation (default 0), adds independent uniform ±3 dB and signed creature attenuation. Invert actual measured dBm, clamp distance at zero and reject invalid loss/readings. | Unit |
| SignalMeasurementHistory / recentCreatureSamples | simulation/history.ts stores twelve finite measurements per AP, wraps, computes max−min. A non-none band refreshes the counter to 12. Each clear-path sample decrements it to a minimum of zero. | Unit |
| RadioInterferenceMath / InterferenceGeometry | simulation/interference.ts projects onto the finite AP→player segment. Both endpoints and zero length handled. Inclusive radii 0.25/0.65/1.4 m select 12/6/3 dB, otherwise zero. Radius 0.9 m flags AP contamination, raising amplitude to at least medium. Radii and amplitudes are ordered as in Unity. | Unit |
| RadioInterferenceCreature.SampleAttenuation / StableChannel | Same channel hash/offset, seed 17.31 and time × 0.7 into coherent gradient noise. Loss varies from −amplitude to −2/3 amplitude. | Unit, source |
| Creature icon and Update | Teal outlined circle, 0.24 m center radius, width 0.07, 48 segments. Default stationary. Patrol uses normalized (1,0) × 1.5 × sin(time × 0.5) from origin. | Unit, source |
| Bootstrap.LateUpdate / estimator marker | Startup AP warm-up, immediate solve, then 1 s intervals, minimum 0.1 s. Red ring radius 0.22, width 0.04, 40 segments. Ring holds its last measurement without interpolation, may leave the room, hides on invalid solution. | Unit, browser |
| SignalPositionEstimator.TryEstimate | simulation/estimator.ts requires the first AP, ≥3 inputs, ≥2 additional valid rows. Circle subtraction, 2×2 normal equations, absolute determinant > 1e−6, finite result. A missing first reading fails even when the other three are usable. | Unit |
| SignalStatusClassifier | AP CONTAMINATED first, then strong or recent range ≥9 dB → SEVERE INTERFERENCE, medium or recent range ≥5 → UNSTABLE, weak → WEAK INTERFERENCE, else STABLE. Movement or static-only loss cannot set the recency flag. | Unit |
| SignalPanelController / SignalRowView | ui/diagnostics.ts displays identifier, horizontal bar clamped from −90 to −20 dBm, rounded measured dBm, signed delta from ideal, status, X/Y to 0.01 m and sampled Euclidean error to 0.001 m. NO VALID SOLUTION hides coordinates and error. Unity's unused −45/−60/−75 strength fields do not participate in status. | Unit, browser, source |
| BuildCanvas / BuildDirectionalPad / legend | HTML and CSS reproduce the world/diagnostics/controls hierarchy and marker meanings. Four lower controls, movement instructions, native collapsible development settings. | Browser, source |
| RadioInterferenceDebugVisualizer / selected gizmos | Opt-in paths colored by level, closest-point markers of radius 0.09, creature connectors, active contamination circles. Browser adds all three band outlines and numeric path distances for inspection. | Browser toggle, source |
| WifiPrototypeSceneGenerator.GenerateScene, ValidateCoreModel, RunPlayModeSmokeTest | Existing Unity scene remains the authoring reference. tests/radio.test.ts and simulation.test.ts cover model/geometry/inputs/bounds. tests/browser-smoke.mjs exercises actual rendering, initial hierarchy and diagnostics. No scene generation step is needed on the web. | Unit, browser |

Implementation files in the table are relative to `web/src/` except where a test or document is named.

## Browser adaptations

- The simulation uses doubles, browser uniform randomness and a local Perlin lattice. Equations, channel separation, output ranges, temporal coherence and classification are preserved. Individual noisy readings can differ between engines.
- Unity checks a deadline in LateUpdate and schedules from that frame. The browser uses exact logical deadlines during active frames and discards background elapsed time. Both show one held estimate per configured interval.
- The browser replaces fixed uGUI anchors, LegacyRuntime text and Game View gizmos with responsive CSS, browser fonts and development overlays. The sample-rate label follows the configured interval. Minor color and antialiasing differences arise from the renderer.
- “Inertial / trusted” is simulation ground truth. Position estimation uses RSSI-derived-distance multilateration. Fingerprint-based positioning research was inspiration for the prototype.
- Editor smoke checks involving random error tolerance are covered by reproducible forward/inverse, noise range, independent solve and sampled-marker assertions. Reset, seed selection, interruption handling and the WebGL error message are browser conveniences.

## Verification record

| Gate | Result |
|---|---|
| Strict TypeScript, production build, npm lockfile | Passed locally |
| Vitest math, state, movement, history, cadence and failure conditions | 35 tests passed |
| Production path in Chromium with WebGL 2 | Passed. Project-prefix-only server resolved HTML, bundled JS and CSS. Captured the rendered white and red markers while moving between samples. |
| Input, settings and failure display | Passed. Keyboard, pointer hold/exit/release/cancel, touch input, blur/visibility release, debug toggle, invalid solve, reset and WebGL failure message. |
| Responsive world | Passed at 1280×720, 390×844 and 320×844. Verified equal scale per meter from projected labels in portrait. |
| Live Pages deployment | Pending repository setup and reviewed merges |

## Pending Unity Editor visual comparison

Open the committed scene in Unity 6000.3.19f1 and enter Play Mode. Compare at 1280×720 first. Check the white disk and hollow red ring when they overlap, corner label readability, ring thickness, room framing and lower controls. Hold a diagonal across a sample boundary, then stop and compare stationary jitter and the apparent 1 Hz updates. Toggle the Unity debug visualizer and compare NW nearest-point placement, contaminated-AP circles and band transitions while relocating the creature. Confirm the adapted panel preserves the readability of the hand-adjusted Unity layout. Unity Editor and a physical touchscreen were not run for this port.
