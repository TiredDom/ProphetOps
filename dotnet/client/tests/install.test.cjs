const test = require('node:test');
const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const vm = require('node:vm');
const { chromium } = require('playwright');
const { spawn, spawnSync } = require('node:child_process');
const http = require('node:http');

const PORT = 5181;
const BASE_URL = `http://localhost:${PORT}`;

const owner = { name: 'Renan & Tina', email: 'owner@prophetops.com', role: 'Owner / Management', defaultPath: '/home' };

function json(route, body) {
  return route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify(body) });
}

async function routeHome(page) {
  await page.route('**/api/auth/me', (route) => json(route, owner));
  await page.route('**/api/app/config', (route) => json(route, { today: '2026-10-13', timeZone: 'Asia/Manila' }));
  await page.route('**/api/dashboard', (route) => json(route, {
    totalsScope: 'lifetime', excludesVoided: true, revenue: 0, costs: 0, estimatedProfit: 0, bookings: 0, packages: 0, expenses: 0,
    lowStockPackages: [], pendingPayments: { count: 0, amount: 0 }, recentBookings: [], lastUpdated: 'Oct 13, 2026',
  }));
  await page.route('**/api/forecast**', (route) => json(route, {
    method: 'Holt-Winters', horizon: 6, ok: false, accuracy: 0, history: [], steps: [],
    dataSource: { status: 'insufficient-history', usingLiveRecords: false, usingSample: false, label: 'Insufficient booking history', liveMonthsAvailable: 8, recordedMonths: 8, minimumMonths: 24, filledMonths: 0, lastRecordedMonth: 'September 2026' },
    insight: { direction: 'flat', changePercent: 0, peakMonth: '', peakValue: 0, notes: [] },
  }));
  await page.route('**/api/activity**', (route) => json(route, []));
  await page.route('**/api/expenses', (route) => json(route, []));
}

/** Width and height from a PNG's header. */
function pngSize(buffer) {
  assert.equal(buffer.subarray(1, 4).toString('ascii'), 'PNG');
  return [buffer.readUInt32BE(16), buffer.readUInt32BE(20)];
}

function waitForServer(url, timeoutMs = 20000) {
  const start = Date.now();
  return new Promise((resolve, reject) => {
    function ping() {
      http.get(url, () => resolve()).on('error', () => {
        if (Date.now() - start > timeoutMs) reject(new Error(`Server at ${url} did not start within ${timeoutMs}ms`));
        else setTimeout(ping, 300);
      });
    }
    ping();
  });
}

test('Installing ProphetOps', async (t) => {
  let server = null;
  let browser = null;

  t.before(async () => {
    server = spawn('npx', ['vite', '--port', String(PORT), '--strictPort'], { shell: true, cwd: __dirname + '/..', stdio: 'pipe' });
    await waitForServer(BASE_URL);
    browser = await chromium.launch({ headless: true });
  });

  t.after(async () => {
    if (browser) await browser.close();
    if (server) {
      if (process.platform === 'win32') {
        try { spawnSync('taskkill', ['/pid', String(server.pid), '/f', '/t'], { stdio: 'ignore' }); } catch (_) {}
      }
      try { server.stdout?.destroy(); server.stderr?.destroy(); server.kill('SIGKILL'); } catch (_) {}
    }
    setTimeout(() => process.exit(process.exitCode || 0), 100);
  });

  await t.test('1. The page links a manifest whose icons exist at the sizes it states', async () => {
    const html = await (await fetch(`${BASE_URL}/`)).text();
    assert.match(html, /<link rel="manifest" href="\/manifest\.webmanifest"/);
    assert.match(html, /<link rel="apple-touch-icon" href="\/icons\/apple-touch-icon\.png"/);
    assert.match(html, /<link rel="icon" href="\/icons\/icon\.svg" type="image\/svg\+xml"/);

    const manifest = await (await fetch(`${BASE_URL}/manifest.webmanifest`)).json();
    assert.equal(manifest.name, 'ProphetOps');
    assert.equal(manifest.start_url, '/');
    assert.equal(manifest.scope, '/');
    assert.equal(manifest.display, 'standalone');
    assert.deepEqual(manifest.icons.map((i) => `${i.sizes} ${i.purpose}`), ['192x192 any', '512x512 any', '512x512 maskable']);

    for (const icon of manifest.icons) {
      const [width, height] = pngSize(Buffer.from(await (await fetch(BASE_URL + icon.src)).arrayBuffer()));
      assert.equal(`${width}x${height}`, icon.sizes, icon.src);
    }
    const [appleWidth] = pngSize(Buffer.from(await (await fetch(`${BASE_URL}/icons/apple-touch-icon.png`)).arrayBuffer()));
    assert.equal(appleWidth, 180);
  });

  await t.test('2. The account menu offers Install only when the browser does, and hands the choice to it', async () => {
    const page = await browser.newPage({ viewport: { width: 1440, height: 900 } });
    await routeHome(page);
    await page.goto(`${BASE_URL}/home`);
    await page.locator('.sidebar-account').waitFor();
    const install = page.getByRole('button', { name: 'Install ProphetOps' });
    assert.equal(await install.count(), 0, 'Nothing to install before the browser offers it');

    await page.evaluate(() => {
      const offer = new Event('beforeinstallprompt', { cancelable: true });
      offer.prompt = async () => { window.__prompted = (window.__prompted ?? 0) + 1; };
      offer.userChoice = Promise.resolve({ outcome: 'accepted' });
      window.dispatchEvent(offer);
      window.__offerHeld = offer.defaultPrevented;
    });
    assert.equal(await page.evaluate(() => window.__offerHeld), true, 'The browser pop-up is held back for the menu');
    await install.waitFor();

    await install.click();
    await page.waitForFunction(() => window.__prompted === 1);
    await page.waitForFunction(() => !Array.from(document.querySelectorAll('button')).some((b) => b.textContent.includes('Install ProphetOps')));
    await page.close();
  });

  await t.test('3. Offline, the worker answers a page load with its own page and leaves data requests alone', async () => {
    const source = fs.readFileSync(path.join(__dirname, '..', 'public', 'sw.js'), 'utf8');
    const handlers = {};
    const sandbox = {
      self: { addEventListener: (type, fn) => { handlers[type] = fn; }, skipWaiting: () => {}, clients: { claim: async () => {} } },
      fetch: async () => { throw new TypeError('Failed to fetch'); },
      Response,
    };
    vm.runInNewContext(source, sandbox);

    let answered = null;
    handlers.fetch({ request: { mode: 'navigate' }, respondWith: (promise) => { answered = promise; } });
    const response = await answered;
    assert.equal(response.status, 503);
    assert.match(response.headers.get('content-type'), /text\/html/);
    const text = await response.text();
    assert.match(text, /<h1>ProphetOps needs an internet connection<\/h1>/);
    assert.match(text, /<a href="\/">Try again<\/a>/);
    assert.doesNotMatch(text, /<script/, 'The offline page runs no script');

    let touched = false;
    handlers.fetch({ request: { mode: 'cors' }, respondWith: () => { touched = true; } });
    assert.equal(touched, false, 'Data requests go to the server untouched');
  });
});
