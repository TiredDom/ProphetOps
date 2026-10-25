const test = require('node:test');
const assert = require('node:assert/strict');
const { chromium } = require('playwright');
const { spawn, spawnSync } = require('node:child_process');
const http = require('node:http');

const PORT = 5184;
const BASE_URL = `http://localhost:${PORT}`;

const owner = { name: 'Agency owner', email: 'owner@example.com', role: 'Owner / Management', defaultPath: '/bookings' };

function json(route, body, status = 200) {
  return route.fulfill({ status, contentType: 'application/json', body: JSON.stringify(body) });
}

/** Signed out, every API answer is 401, as the server gives it. */
async function routeSignedOut(page) {
  await page.route('**/api/**', (route) => json(route, { message: 'Sign in first.' }, 401));
}

async function routeSignedIn(page, businessName) {
  await page.route('**/api/**', (route) => json(route, { message: 'There is no API at this address.' }, 404));
  await page.route('**/api/auth/me', (route) => json(route, owner));
  await page.route('**/api/app/config', (route) => json(route, { today: '2026-10-13', timeZone: 'Asia/Manila', businessName }));
  await page.route('**/api/bookings', (route) => json(route, { bookings: [], packages: [] }));
  await page.route('**/api/activity**', (route) => json(route, []));
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

test('What a visitor who has not signed in can see', async (t) => {
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

  await t.test('1. The sign-in page names the product and nothing about whose it is or what it holds', async () => {
    for (const width of [1440, 390]) {
      const page = await browser.newPage({ viewport: { width, height: width < 700 ? 844 : 900 } });
      await routeSignedOut(page);
      await page.goto(`${BASE_URL}/home`);
      await page.waitForURL(/\/login/);
      await page.getByRole('button', { name: 'Sign in' }).waitFor();

      const text = await page.locator('body').innerText();
      assert.match(text, /ProphetOps/);
      assert.match(text, /Authorized/);
      for (const giveaway of [/Renan/i, /Tina/i, /travel/i, /forecast/i, /revenue/i, /Holt/i, /booking/i]) {
        assert.doesNotMatch(text, giveaway, `The signed-out page at ${width}px mentions ${giveaway}`);
      }

      // The example address in the email check gives no domain away either.
      await page.locator('#email').fill('not-an-email');
      await page.getByRole('button', { name: 'Sign in' }).click();
      assert.match(await page.locator('#email-error').innerText(), /name@example\.com/);
      await page.close();
    }
  });

  await t.test('2. Search engines are asked to stay away, and the install file says nothing about the agency', async () => {
    const html = await (await fetch(`${BASE_URL}/login`)).text();
    assert.match(html, /<meta name="robots" content="noindex, nofollow" \/>/);

    const robots = await (await fetch(`${BASE_URL}/robots.txt`)).text();
    assert.match(robots, /User-agent: \*/);
    assert.match(robots, /Disallow: \/\s*$/m);

    const manifest = await (await fetch(`${BASE_URL}/manifest.webmanifest`)).json();
    assert.equal(manifest.name, 'ProphetOps');
    assert.equal(manifest.description, undefined);
    assert.doesNotMatch(JSON.stringify(manifest), /Renan|Tina|forecast|revenue/i);
  });

  await t.test('3. Signed in, the agency name comes from the server and fills the sidebar', async () => {
    const page = await browser.newPage({ viewport: { width: 1440, height: 900 } });
    await routeSignedIn(page, 'Sample Agency Ltd');
    await page.goto(`${BASE_URL}/bookings`);
    await page.locator('.brand-subtitle', { hasText: 'Sample Agency Ltd' }).waitFor();
    await page.close();
  });
});
