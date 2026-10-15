const test = require('node:test');
const assert = require('node:assert/strict');
const { chromium } = require('playwright');
const { spawn, spawnSync } = require('node:child_process');
const http = require('node:http');

const PORT = 5179;
const BASE_URL = `http://localhost:${PORT}`;

const owner = { name: 'Renan & Tina', email: 'owner@prophetops.com', role: 'Owner / Management', defaultPath: '/home' };
const staff = { name: 'Casey Staff', email: 'staff@prophetops.com', role: 'Staff', defaultPath: '/bookings' };

function booking(id, ds, client, grossRevenue) {
  return {
    id,
    backendId: Number(id.replace(/\D/g, '')),
    revision: 1,
    ds,
    y: 2,
    client,
    package: 'Alpha Escape',
    packageId: 'PKG-1',
    entryType: 'Package preset',
    destination: 'Palawan',
    grossRevenue,
    paymentStatus: 'Pending',
    bookingStatus: 'Confirmed',
    staffAssigned: 'Staff User',
    source: 'Package preset',
    notes: null,
    voided: false,
    voidReason: null,
  };
}

const bookings = [
  booking('BKG-1002', '2026-10-05', 'Bravo Travel', 50000),
  booking('BKG-1001', '2026-10-02', 'Pearl of the East Tours', 100000),
];

const packages = [
  {
    id: 'PKG-1', backendId: 1, revision: 1, packageName: 'Alpha Escape', destination: 'Palawan', duration: '3D2N',
    basePrice: 10000, inclusions: null, availableSlots: 20, soldCount: 10, reservedCount: 0, status: 'Normal', imageUrl: null,
  },
];

async function route(page, user = owner) {
  const json = (body, status = 200) => ({ status, contentType: 'application/json', body: JSON.stringify(body) });
  await page.route('**/api/auth/me', (r) => r.fulfill(json(user)));
  await page.route('**/api/app/config', (r) => r.fulfill(json({ today: '2026-10-13', timeZone: 'Asia/Manila' })));
  await page.route('**/api/forecast**', (r) => r.fulfill(json({}, 404)));
  await page.route('**/api/activity**', (r) => r.fulfill(json([])));
  await page.route('**/api/bookings', (r) => r.fulfill(json({ bookings, packages })));
  await page.route('**/api/inventory', (r) => r.fulfill(json(packages)));
  await page.route('**/api/expenses', (r) => r.fulfill(json(user.role === 'Staff' ? {} : []), user.role === 'Staff' ? 403 : 200));
  await page.route('**/api/users', (r) => r.fulfill(json([])));
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

test('Search, help and keyboard shortcuts', async (t) => {
  let serverProcess = null;
  let browser = null;

  t.before(async () => {
    serverProcess = spawn('npx', ['vite', '--port', String(PORT), '--strictPort'], {
      shell: true,
      cwd: __dirname + '/..',
      stdio: 'pipe',
    });
    await waitForServer(BASE_URL);
    browser = await chromium.launch({ headless: true });
  });

  t.after(async () => {
    if (browser) await browser.close();
    if (serverProcess) {
      if (process.platform === 'win32') {
        try {
          spawnSync('taskkill', ['/pid', String(serverProcess.pid), '/f', '/t'], { stdio: 'ignore' });
        } catch (_) {}
      }
      try {
        serverProcess.stdout?.destroy();
        serverProcess.stderr?.destroy();
        serverProcess.kill('SIGKILL');
      } catch (_) {}
    }
    setTimeout(() => process.exit(process.exitCode || 0), 100);
  });

  await t.test('1. Ctrl+K finds a booking by its client and opens it', async () => {
    const page = await browser.newPage({ viewport: { width: 1440, height: 900 } });
    await route(page);
    await page.goto(`${BASE_URL}/packages`);
    await page.locator('.records-panel').waitFor();
    await page.keyboard.press('Control+k');
    const search = page.getByRole('dialog', { name: 'Search and go' });
    await search.waitFor();
    const field = search.getByRole('combobox');
    assert.equal(await page.evaluate(() => document.activeElement?.getAttribute('role')), 'combobox', 'The field has focus');
    await field.fill('pearl');
    const option = search.getByRole('option', { name: /Pearl of the East Tours/ });
    await option.waitFor();
    assert.equal(await field.getAttribute('aria-activedescendant'), await option.getAttribute('id'), 'The first match is ready for Enter');
    await page.keyboard.press('Enter');
    await page.getByRole('dialog', { name: 'Booking BKG-1001' }).waitFor();
    assert.ok(page.url().endsWith('/bookings'), 'The address is left clean: ' + page.url());

    // What was opened comes back first next time.
    await page.keyboard.press('Escape');
    await page.keyboard.press('Control+k');
    const recent = page.getByRole('group', { name: 'Recently opened' });
    await recent.waitFor();
    assert.ok((await recent.innerText()).includes('Pearl of the East Tours'));
    await page.keyboard.press('Escape');
    await search.waitFor({ state: 'detached' });
    await page.close();
  });

  await t.test('2. Search and help offer only what the role can open', async () => {
    const page = await browser.newPage({ viewport: { width: 1440, height: 900 } });
    await route(page, staff);
    await page.goto(`${BASE_URL}/bookings`);
    await page.locator('.records-panel').waitFor();
    await page.getByRole('button', { name: /^Search/ }).click();
    const search = page.getByRole('dialog', { name: 'Search and go' });
    await search.waitFor();
    const pages = await search.getByRole('group', { name: 'Pages' }).getByRole('option').allInnerTexts();
    assert.deepEqual(pages.map((text) => text.split('\n')[0]), ['Bookings', 'Packages']);
    await search.getByRole('combobox').fill('forecast');
    await page.waitForTimeout(200);
    assert.equal(await search.getByRole('group', { name: 'Actions' }).count(), 0, 'Staff are offered no forecast to open');
    assert.equal(await search.getByRole('group', { name: 'Pages' }).count(), 0);
    await page.keyboard.press('Escape');

    await page.keyboard.press('?');
    const help = page.getByRole('dialog', { name: 'Help' });
    await help.waitFor();
    await help.getByRole('button', { name: 'All help' }).click();
    const titles = await help.locator('.help-topic-link-title').allInnerTexts();
    assert.ok(titles.includes('Record a booking'));
    assert.ok(!titles.some((title) => /forecast|Reports|access|expenses/i.test(title)), 'No topics for pages Staff cannot open: ' + titles.join(', '));
    await page.close();
  });

  await t.test('3. Help opens on the page in hand, and finds a task by everyday words', async () => {
    const page = await browser.newPage({ viewport: { width: 1440, height: 900 } });
    await route(page);
    await page.goto(`${BASE_URL}/bookings`);
    await page.locator('.records-panel').waitFor();
    await page.getByRole('button', { name: 'Help & shortcuts' }).click();
    const help = page.getByRole('dialog', { name: 'Help' });
    await help.waitFor();
    const here = await help.getByRole('region', { name: 'On this page' }).locator('.help-topic-link-title').allInnerTexts();
    assert.ok(here.length >= 1 && here.length <= 4, 'A short list for this page: ' + here.join(', '));
    assert.equal(here[0], 'Record a booking');

    await help.getByRole('searchbox').or(help.locator('.help-search input')).first().fill('mark paid');
    await help.getByRole('button', { name: /Mark bookings as paid or confirmed/ }).click();
    const heading = help.getByRole('heading', { name: 'Mark bookings as paid or confirmed' });
    await heading.waitFor();
    assert.ok((await help.innerText()).includes('booked value'), 'The topic explains the wording on screen');
    await page.close();
  });

  await t.test('4. Single-key shortcuts act on the page, and wait while typing', async () => {
    const page = await browser.newPage({ viewport: { width: 1440, height: 900 } });
    await route(page);
    await page.goto(`${BASE_URL}/bookings`);
    await page.locator('.records-panel').waitFor();

    await page.keyboard.press('/');
    const search = page.locator('.records-tools .search-field input');
    assert.equal(await page.evaluate(() => document.activeElement?.closest('.search-field') !== null), true, '/ focuses the list search');
    await page.keyboard.type('n');
    assert.equal(await search.inputValue(), 'n', 'N is typed into the field, not taken as a shortcut');
    assert.equal(await page.getByRole('dialog', { name: 'New booking' }).count(), 0);
    await search.fill('');
    await page.locator('h1').click();

    await page.keyboard.press('n');
    await page.getByRole('dialog', { name: 'New booking' }).waitFor();
    await page.keyboard.press('Escape');
    await page.getByRole('dialog', { name: 'New booking' }).waitFor({ state: 'detached' });

    await page.locator('.records-table .row-open').first().click();
    const panel = page.getByRole('dialog', { name: /Booking BKG-/ });
    await panel.waitFor();
    await page.keyboard.press('e');
    await page.getByRole('dialog', { name: 'Edit booking' }).waitFor();
    await page.keyboard.press('Escape');
    await page.getByRole('dialog', { name: /Booking BKG-/ }).waitFor();
    await page.keyboard.press('Escape');
    await page.waitForTimeout(250);

    await page.keyboard.press('g');
    await page.keyboard.press('p');
    await page.waitForURL('**/packages');
    await page.close();
  });

  await t.test('5. Sign-in names each problem beside its field, and returns to the page asked for', async () => {
    const page = await browser.newPage({ viewport: { width: 1440, height: 900 } });
    await route(page);
    let signedIn = false;
    let attempts = 0;
    await page.route('**/api/auth/me', (r) =>
      signedIn
        ? r.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify(owner) })
        : r.fulfill({ status: 401, body: '' }));
    await page.route('**/api/auth/login', (r) => {
      attempts += 1;
      if (attempts === 1) {
        return r.fulfill({ status: 401, contentType: 'application/json', body: JSON.stringify({ message: 'Use an authorized internal account.' }) });
      }
      signedIn = true;
      return r.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify(owner) });
    });

    await page.goto(`${BASE_URL}/packages?stock=low`);
    await page.waitForURL('**/login?next=**');
    await page.getByRole('button', { name: 'Sign in' }).click();
    assert.equal(await page.locator('#email').getAttribute('aria-invalid'), 'true');
    assert.ok((await page.locator('#email-error').innerText()).includes('full email address'));
    assert.equal(await page.evaluate(() => document.activeElement?.id), 'email', 'Focus goes to the field to fix');

    await page.locator('#email').fill('owner@prophetops.com');
    await page.locator('#password').fill('not-the-password');
    await page.getByRole('button', { name: 'Sign in' }).click();
    const alert = page.locator('.login-error');
    await alert.waitFor();
    assert.ok((await alert.innerText()).includes('do not match an active account'), 'Plain words, not the server code');
    assert.equal(await page.locator('#email').inputValue(), 'owner@prophetops.com', 'The email is kept');

    await page.getByRole('button', { name: 'Sign in' }).click();
    await page.waitForURL('**/packages?stock=low');
    await page.close();
  });

  await t.test('6. Reports shows the top five of each breakdown, with the rest a click away', async () => {
    const page = await browser.newPage({ viewport: { width: 1440, height: 900 } });
    await route(page);
    const line = (label, value) => ({ label, value, count: 1 });
    await page.route('**/api/reports**', (r) => r.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify({
        totalsScope: 'period', excludesVoided: true,
        period: { from: '2026-01-01', to: '2026-12-31', through: '2026-10-13' },
        revenue: 700000, costs: 0, profit: 700000,
        counts: { bookings: 7, passengers: 14, packages: 7, expenses: 0, users: 1 },
        previous: null, monthly: [],
        bookingsByStatus: [], bookingsByPayment: [], expensesByCategory: [],
        revenueByPackage: ['A', 'B', 'C', 'D', 'E', 'F', 'G'].map((name, i) => line(`Package ${name}`, 700000 - i * 10000)),
        revenueByDestination: [],
        generatedAt: 'Oct 13, 2026',
      }),
    }));
    await page.goto(`${BASE_URL}/reports`);
    const panel = page.getByRole('region', { name: 'Revenue by package' });
    await panel.waitFor();
    assert.equal(await panel.locator('.report-bar-row:visible').count(), 5);
    await panel.getByRole('button', { name: 'Show all 7' }).click();
    assert.equal(await panel.locator('.report-bar-row:visible').count(), 7);
    await page.close();
  });
});
