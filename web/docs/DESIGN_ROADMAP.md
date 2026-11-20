# Design roadmap

Phase 1 is the playable Unity parity baseline. Later phases are questions for experiments. Their order and inclusion remain undecided.

| Candidate area | Status | Question for a future decision |
|---|---|---|
| Estimation experiments | UNDECIDED | Would fingerprint collection and KNN, or another estimator, support the intended experience? Which inputs and error criteria would make the comparison fair? |
| Objectives and UX flow | UNDECIDED | What should players infer from the two markers, and what action would demonstrate that understanding? |
| Progression | UNDECIDED | Would introducing changes to room conditions over time improve learning? |
| Creature interactions | UNDECIDED | Does moving the existing interference source produce interpretable changes in the readings? |
| Gameplay knobs and balance | UNDECIDED | Which source parameters should become design variables? Compare one parameter at a time along identical paths and seeds. |
| Telemetry | UNDECIDED | Which measurements would answer an agreed playtest question? Define consent, collection scope and retention before choosing instrumentation. |
| Return to Unity | UNDECIDED | Which future mechanics merit freezing, which tests define acceptance, and when should an art/audio pass begin? |

## Decision log

| Phase | Decision | Evidence |
|---|---|---|
| 1 · baseline | Reproduce the current distance-based least-squares solve, independent AP noise, creature attenuation and sample-held estimate. | Unity feature commit `d76b5ff` and the serialized playable scene |

For each future accepted or rejected experiment, add its date, question, tested configurations/seeds, observed result, decision and affected fixtures.
