# Wi-Fi positioning prototype · browser edition

A playable port of the Unity diagnostic room. Move the white dot and watch four simulated AP readings produce an independent red position estimate. The world uses meters. The browser performs all sampling and rendering locally.

## Run

Use Node.js 22.12+ and npm. From the repository root:

```sh
cd web
npm ci
npm run dev
```

Open the project URL printed by Vite, normally `http://localhost:5173/wifi-signal-positioning-prototype/`. A browser with WebGL 2 support is required.

```sh
npm run typecheck
npm test
npm run build
npm run preview
```

Preview normally serves `http://localhost:4173/wifi-signal-positioning-prototype/`. The production artifact is `web/dist/`, including Three.js and all styles. It can be served by any static host at the configured project path.

## Play and inspect

- Hold WASD, arrow keys, or one or more direction buttons. The white inertial/trusted marker tracks simulation ground truth. The red ring, AP diagnostics and error update together at each measurement. The error measures separation at sample time.
- The teal outline marks the radio interference creature. At rest in the initial room it intersects the NW path's strong band. Moving changes which paths pass near it.
- Expand **Developer Knobs** to change a parameter. Commit numeric edits with Enter or by leaving the field. Patrol uses the creature origin and the session clock. **Show interference geometry** reveals colored paths, nearest points, path distances, band circles and contaminated AP radii. Settings live in memory. **Reset to Unity defaults** restarts the room, sampling clock and histories.
- Append `?seed=42` to the project URL for repeatable measurement noise. Accepted seeds are unsigned 32-bit decimal integers. Resetting restarts the sequence. Matching input timing and settings are also needed to reproduce a moving trajectory.

## Browser smoke test

Build first. Use an existing Chromium executable or install the Playwright-matched browser locally:

```sh
npm run build
CHROMIUM_BIN=/usr/bin/chromium npm run test:browser
# Alternatively:
npx playwright-core install chromium
npm run test:browser
```

The smoke test serves the built files on a temporary loopback port under the repository prefix, exercises Chromium with software WebGL, reads rendered marker pixels, tests controls and resizes to portrait. The server and browser close after the test. On Windows, set `$env:CHROMIUM_BIN` in PowerShell if using a system browser. The unit tests run without Chromium.

## GitHub Pages

1. Review and merge the Unity baseline PR #1 into `main` first. The web draft is stacked on `codex/wifi-signal-positioning-prototype`. After #1 lands, retarget the web PR to `main` if needed and review its diff. If the baseline was squash-merged, reconcile the branch with the merged baseline before integration.
2. This repository was private at baseline inspection. GitHub Pages for private repositories requires an eligible plan such as GitHub Pro, Team or Enterprise. Published sites are generally public, including sites built from private repositories. Confirm the intended audience and any organization Pages restrictions before enabling deployment. [GitHub Pages eligibility and visibility](https://docs.github.com/en/pages/getting-started-with-github-pages/what-is-github-pages)
3. In **Settings → Pages → Build and deployment → Source**, select **GitHub Actions**. Allow the workflow to deploy from `main` in the `github-pages` environment if repository rules restrict deployments.
4. Merge the reviewed web feature into `main`. Changes to `web/**` or the workflow on `main` trigger **Deploy browser prototype to Pages**. To retry manually, open **Actions → Deploy browser prototype to Pages → Run workflow** and select `main`.
5. Check both the build and deploy jobs and open the URL reported by the deployment environment. The expected standard address after successful deployment is [https://tlrhfgywk2.github.io/wifi-signal-positioning-prototype/](https://tlrhfgywk2.github.io/wifi-signal-positioning-prototype/).

The workflow installs the committed npm lockfile, typechecks, runs the unit suite, builds, configures Pages and publishes the static artifact using official actions. Feature branch pushes only stage source for review.

## Engineering notes

- [Unity baseline, parity checklist and outstanding Editor checks](docs/UNITY_PARITY.md)
- [Runtime architecture and clock semantics](docs/ARCHITECTURE.md)
- [Porting the finalized mechanics back to Unity](docs/PORTING_NOTES.md)
- [Undecided design questions and decision log](docs/DESIGN_ROADMAP.md)
