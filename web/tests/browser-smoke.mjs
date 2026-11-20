import assert from 'node:assert/strict';
import { readFile } from 'node:fs/promises';
import { createServer } from 'node:http';
import { test } from 'node:test';
import { chromium } from 'playwright-core';

const prefix = '/wifi-signal-positioning-prototype/';
const dist = new URL('../dist/', import.meta.url);
const mime = { html: 'text/html', js: 'text/javascript', css: 'text/css' };

test('built app at the project path, rendered movement, diagnostics and responsive controls', async t => {
  // mount ONLY the project path, with neither a root fallback nor Vite transformations.
  const server = createServer(async (req, res) => {
    const path = new URL(req.url, 'http://localhost').pathname;
    try {
      assert.ok(path.startsWith(prefix));
      const file = new URL(path.slice(prefix.length) || 'index.html', dist);
      assert.ok(file.href.startsWith(dist.href));
      const bytes = await readFile(file);
      res.writeHead(200, { 'Content-Type': mime[file.pathname.split('.').pop()] });
      res.end(bytes);
    } catch {
      res.writeHead(404).end();
    }
  });
  await new Promise(resolve => server.listen(0, '127.0.0.1', resolve));
  t.after(() => new Promise(resolve => server.close(resolve)));
  const url = 'http://127.0.0.1:' + server.address().port + prefix;
  const launch = {
    executablePath: process.env.CHROMIUM_BIN || undefined,
    args: ['--use-angle=swiftshader', '--enable-unsafe-swiftshader'],
  };
  const browser = await chromium.launch(launch);
  t.after(() => browser.close());
  const page = await browser.newPage({ viewport: { width: 1280, height: 720 }, hasTouch: true });
  const errors = [], assets = [];
  page.on('pageerror', e => errors.push(e.message));
  page.on('requestfailed', r => errors.push(r.url()));
  page.on('response', r => { assets.push(r.url()); if (!r.ok()) errors.push(r.url()); });
  await page.clock.install({ time: new Date('2025-01-01T00:00:00Z') });
  await page.clock.pauseAt(new Date('2025-01-01T00:00:01Z'));
  // SwiftShader may discard the framebuffer between Playwright clock callbacks.
  await page.addInitScript(() => {
    const getContext = HTMLCanvasElement.prototype.getContext;
    HTMLCanvasElement.prototype.getContext = function(kind, options) {
      return getContext.call(this, kind, kind === 'webgl2' ? { ...options, preserveDrawingBuffer: true } : options);
    };
  });
  await page.goto(url + '?seed=42');
  await page.clock.runFor(32);
  assert.equal(await page.locator('.ap-row').count(), 4);
  assert.equal(await page.locator('#rate').innerText(), '1 Hz');
  assert.match(await page.locator('#error').innerText(), /^\d+\.\d{3} m$/);
  assert.equal(await page.locator('#knobs').getAttribute('open'), null);
  assert.ok(assets.some(s => s.endsWith('.js')) && assets.some(s => s.endsWith('.css')));
  assert.ok(assets.every(s => s.startsWith(url)));

  // Read the presented Three.js framebuffer just after the game's RAF render.
  async function markers() {
    await page.evaluate(() => {
      requestAnimationFrame(() => {
        const canvas = document.querySelector('canvas');
        const gl = canvas.getContext('webgl2');
        const { width: w, height: h } = canvas;
        const pixels = new Uint8Array(w * h * 4);
        gl.readPixels(0, 0, w, h, gl.RGBA, gl.UNSIGNED_BYTE, pixels);
        const white = [], red = [];
        // Exclude the room outline and corner diamonds.
        for (let y = Math.floor(h * .3); y < h * .7; y++) {
          for (let x = Math.floor(w * .3); x < w * .7; x++) {
            const i = 4 * (y * w + x);
            if (pixels[i] > 220 && pixels[i + 1] > 220 && pixels[i + 2] > 220) white.push([x, y]);
            if (pixels[i] > 220 && pixels[i + 1] < 100 && pixels[i + 2] < 100) red.push([x, y]);
          }
        }
        function center(points) {
          if (!points.length) return null;
          return points.reduce((s, [x, y]) => [s[0] + x / points.length, s[1] + y / points.length], [0, 0]);
        }
        window.renderedMarkers = { white: center(white), red: center(red) };
      });
    });
    await page.clock.runFor(16);
    return page.evaluate(() => window.renderedMarkers);
  }

  assert.ok(await page.locator('#pad').evaluate(el => el.getBoundingClientRect().bottom <= innerHeight));
  const start = await markers();
  assert.ok(start.white && start.red);
  const error = await page.locator('#error').innerText();
  await page.keyboard.down('d');
  await page.clock.runFor(160);
  await page.keyboard.up('d');
  const moved = await markers();
  assert.ok(moved.white[0] > start.white[0] + 8);
  assert.deepEqual(moved.red, start.red);
  assert.equal(await page.locator('#error').innerText(), error);

  const up = page.getByRole('button', { name: 'Move up' });
  const box = await up.boundingBox();
  await page.mouse.move(box.x + box.width / 2, box.y + box.height / 2);
  await page.mouse.down();
  await page.clock.runFor(160);
  await page.mouse.move(0, 0);
  const leftButton = await markers();
  assert.ok(leftButton.white[1] > moved.white[1] + 8);
  await page.clock.runFor(120);
  await page.mouse.up();
  assert.deepEqual((await markers()).white, leftButton.white);
  const right = page.getByRole('button', { name: 'Move right' });
  for (const release of ['pointerup', 'pointercancel']) {
    await right.dispatchEvent('pointerdown', { pointerType: 'touch', pointerId: 11, button: 0 });
    assert.equal(await right.evaluate(b => b.classList.contains('held')), true);
    await right.dispatchEvent(release, { pointerType: 'touch', pointerId: 11 });
    assert.equal(await right.evaluate(b => b.classList.contains('held')), false);
  }
  const touch = await page.context().newCDPSession(page);
  const at = { x: Math.round(box.x + box.width / 2), y: Math.round(box.y + box.height / 2) };
  await touch.send('Input.dispatchTouchEvent', { type: 'touchStart', touchPoints: [at] });
  assert.equal(await up.evaluate(b => b.classList.contains('held')), true);
  await touch.send('Input.dispatchTouchEvent', { type: 'touchMove', touchPoints: [{ x: at.x - 110, y: at.y }] });
  await touch.send('Input.dispatchTouchEvent', { type: 'touchEnd', touchPoints: [] });
  assert.equal(await up.evaluate(b => b.classList.contains('held')), false);
  for (const event of ['blur', 'visibilitychange']) {
    await page.keyboard.down('ArrowLeft');
    await page.evaluate(name => (name === 'blur' ? window : document).dispatchEvent(new Event(name)), event);
    assert.equal(await page.locator('#pad .held').count(), 0);
    await page.keyboard.up('ArrowLeft');
  }
  await page.clock.runFor(640);
  assert.notDeepEqual((await markers()).red, start.red);
  await page.locator('summary').evaluate(el => el.click());
  const debug = page.getByLabel('Show interference geometry');
  await debug.evaluate(el => el.click());
  await page.clock.runFor(16);
  assert.equal(await page.locator('.path:visible').count(), 4);
  const loss = page.getByLabel('Loss per meter');
  await loss.fill('1e308');
  await loss.press('Tab');
  await page.clock.runFor(1040);
  assert.equal(await page.locator('#invalid').isVisible(), true);
  assert.equal((await markers()).red, null);
  await page.getByRole('button', { name: 'Reset to Unity defaults' }).evaluate(el => el.click());
  await page.clock.runFor(16);
  assert.equal(await loss.inputValue(), '5');
  assert.equal(await debug.isChecked(), false);
  assert.equal(await page.locator('#invalid').isVisible(), false);
  await page.locator('summary').evaluate(el => el.click());

  for (const width of [390, 320]) {
    await page.setViewportSize({ width, height: 844 });
    await page.evaluate(() => new Promise(resolve => {
      const observer = new ResizeObserver(() => { observer.disconnect(); resolve(); });
      observer.observe(document.querySelector('#viewport'));
    }));
    await page.clock.runFor(32);
    const layout = await page.evaluate(() => {
      const box = s => document.querySelector(s).getBoundingClientRect();
      const [nw, ne, sw] = [...document.querySelectorAll('.map-label')].map(el => el.getBoundingClientRect());
      return { overflow: document.documentElement.scrollWidth > innerWidth, stacked: box('aside').top >= box('.world').bottom,
        dx: ne.x - nw.x, dy: sw.y - nw.y, button: box('#pad button').width };
    });
    assert.ok(!layout.overflow && layout.stacked && layout.button >= 44);
    assert.ok(Math.abs(layout.dx / 8.4 - layout.dy / 8.26) < 0.25, 'pixels per meter: ' + JSON.stringify(layout));
  }
  assert.deepEqual(errors, []);
  if (process.env.SMOKE_SCREENSHOT) {
    await page.setViewportSize({ width: 1280, height: 720 });
    await page.clock.runFor(32);
    await page.evaluate(() => scrollTo(0, 0));
    await page.screenshot({ path: process.env.SMOKE_SCREENSHOT });
  }
  const unsupported = await browser.newPage();
  await unsupported.addInitScript(() => {
    const getContext = HTMLCanvasElement.prototype.getContext;
    HTMLCanvasElement.prototype.getContext = function(kind, ...args) {
      return kind.startsWith('webgl') ? null : getContext.call(this, kind, ...args);
    };
  });
  await unsupported.goto(url);
  assert.match(await unsupported.getByRole('alert').innerText(), /WebGL 2/);
});
