const test = require('node:test');
const assert = require('node:assert/strict');
const { chromium } = require('playwright');
const { spawn, spawnSync } = require('node:child_process');
const http = require('node:http');

const PORT = 5175;
const BASE_URL = `http://localhost:${PORT}`;

const owner = { name: 'Renan & Tina', email: 'owner@prophetops.com', role: 'Owner / Management', defaultPath: '/home' };
const staff = { name: 'Casey Staff', email: 'staff@prophetops.com', role: 'Staff', defaultPath: '/bookings' };

function booking(id, ds, client, paymentStatus, grossRevenue, y, extra = {}) {
  return {
    id,
    backendId: Number(id.replace(/\D/g, '')),
    revision: 1,
    ds,
    y,
    client,
    package: 'Boracay Group Package',
    packageId: 'PKG-1',
    entryType: 'Package preset',
    destination: 'Boracay',
    grossRevenue,
    paymentStatus,
    bookingStatus: 'Confirmed',
    staffAssigned: 'Staff User',
    source: 'Package preset',
    notes: null,
    voided: false,
    voidReason: null,
    ...extra,
  };
}

// Business date is 13 October 2026. One voided booking carries a large figure that must never count.
const bookings = [
  booking('BKG-1005', '2026-10-08', 'Echo Voided', 'Pending', 999000, 5, { voided: true, voidReason: 'Duplicate' }),
  booking('BKG-1002', '2026-10-05', 'Bravo Travel', 'Pending', 50000, 2),
  booking('BKG-1001', '2026-10-02', 'Alpha Tours', 'Paid', 100000, 4),
  booking('BKG-1003', '2026-09-20', 'Charlie Group', 'Partially Paid', 80000, 3),
  booking('BKG-1004', '2026-09-10', 'Delta Agency', 'Paid', 250000, 10),
];

function pkg(id, packageName, status, availableSlots, soldCount, reservedCount, basePrice) {
  return {
    id,
    backendId: Number(id.replace(/\D/g, '')),
    revision: 1,
    packageName,
    destination: 'Palawan',
    duration: '3D2N',
    basePrice,
    inclusions: null,
    availableSlots,
    soldCount,
    reservedCount,
    status,
    imageUrl: null,
  };
}

const packages = [
  pkg('PKG-1', 'Alpha Escape', 'Normal', 20, 10, 0, 10000),
  pkg('PKG-2', 'Beta Retreat', 'Low', 4, 30, 2, 18000),
  pkg('PKG-3', 'Gamma Tour', 'Critical', 1, 40, 1, 12000),
  pkg('PKG-4', 'Delta Cruise', 'Normal', 50, 5, 0, 30000),
];

function expense(id, date, category, amount, paymentStatus, extra = {}) {
  return {
    id,
    backendId: Number(id.replace(/\D/g, '')),
    date,
    category,
    amount,
    relatedPackage: 'Office operations',
    paymentStatus,
    notes: null,
    voided: false,
    voidReason: null,
    ...extra,
  };
}

const expenses = [
  expense('EXP-4', '2026-10-07', 'Marketing', 10000, 'Paid', { voided: true, voidReason: 'Entered twice' }),
  expense('EXP-2', '2026-10-06', 'Marketing', 50000, 'Pending'),
  expense('EXP-1', '2026-10-03', 'Tour operations', 300000, 'Paid'),
  expense('EXP-3', '2026-09-15', 'Overhead', 40000, 'Paid'),
];

const bookingHistory = [
  { at: '2026-10-12T07:40:00Z', actor: 'owner@prophetops.com', actorName: 'Renan & Tina', action: 'Updated', entityType: 'Booking', entityCode: 'BKG-1002', summary: 'Payment Partially Paid → Pending; Revenue 45000 → 50000' },
  { at: '2026-10-05T02:15:00Z', actor: 'staff@prophetops.com', actorName: 'Casey Staff', action: 'Created', entityType: 'Booking', entityCode: 'BKG-1002', summary: 'Bravo Travel, 2 pax, P45,000' },
];

async function routeRecords(page, user = owner) {
  const activityRequests = [];
  const json = (body) => ({ status: 200, contentType: 'application/json', body: JSON.stringify(body) });
  await page.route('**/api/auth/me', (route) => route.fulfill(json(user)));
  await page.route('**/api/app/config', (route) => route.fulfill(json({ today: '2026-10-13', timeZone: 'Asia/Manila' })));
  // These tests are about the lists; the forecast bands have their own suite.
  await page.route('**/api/forecast**', (route) => route.fulfill({ status: 404, contentType: 'application/json', body: '{}' }));
  await page.route('**/api/bookings', (route) => route.fulfill(json({ bookings, packages: packages.map((p) => ({ ...p })) })));
  await page.route('**/api/inventory', (route) => route.fulfill(json(packages)));
  await page.route('**/api/expenses', (route) => route.fulfill(json(expenses)));
  await page.route('**/api/activity**', (route) => {
    activityRequests.push(route.request().url());
    route.fulfill(json(user.role === 'Staff' ? [] : bookingHistory));
  });
  return activityRequests;
}

async function summary(page) {
  return (await page.locator('.records-summary').innerText()).replace(/\s+/g, ' ');
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

test('Bookings, Packages and Expenses lists', async (t) => {
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

  await t.test('1. Voided bookings stay out of the list and the totals until asked for', async () => {
    const page = await browser.newPage({ viewport: { width: 1440, height: 900 } });
    await routeRecords(page);
    await page.goto(`${BASE_URL}/bookings`);
    await page.waitForSelector('.records-table');

    assert.equal(await page.locator('.records-table .record-row').count(), 4, 'Four active bookings listed');
    const before = await summary(page);
    assert.ok(before.includes('4 bookings'), before);
    assert.ok(before.includes('₱480,000 revenue'), 'Revenue is gross revenue without the voided booking: ' + before);

    await page.getByRole('button', { name: /Show voided/ }).click();
    await page.waitForURL(/voided=1/);
    assert.equal(await page.locator('.records-table .record-row').count(), 5, 'The voided booking is listed on request');
    const after = await summary(page);
    assert.ok(after.includes('₱480,000 revenue'), 'Listing a voided booking does not count it: ' + after);
    assert.ok(after.includes('1 voided listed, not counted'), after);
    await page.close();
  });

  await t.test('2. Filters come from the address and survive a reload', async () => {
    const page = await browser.newPage({ viewport: { width: 1440, height: 900 } });
    await routeRecords(page);
    await page.goto(`${BASE_URL}/bookings?payment=unpaid&period=month`);
    await page.waitForSelector('.records-table');

    assert.deepEqual(await page.locator('.records-table .row-open').allInnerTexts(), ['Bravo Travel']);
    assert.equal(await page.getByRole('button', { name: /^Unpaid/ }).getAttribute('aria-pressed'), 'true');
    assert.equal(await page.locator('.period-select select').inputValue(), 'month');
    assert.ok((await summary(page)).includes('October 2026'), 'The summary names the period');

    await page.locator('.search-field input').fill('bravo');
    await page.waitForURL(/q=bravo/);
    await page.reload();
    await page.waitForSelector('.records-table');
    assert.equal(await page.locator('.search-field input').inputValue(), 'bravo');
    assert.deepEqual(await page.locator('.records-table .row-open').allInnerTexts(), ['Bravo Travel']);

    await page.getByRole('button', { name: 'Clear filters' }).click();
    await page.waitForURL((url) => url.search === '');
    assert.equal(await page.locator('.records-table .record-row').count(), 4);
    await page.close();
  });

  await t.test('3. Sorting by a column orders the rows and says so', async () => {
    const page = await browser.newPage({ viewport: { width: 1440, height: 900 } });
    await routeRecords(page);
    await page.goto(`${BASE_URL}/bookings`);
    await page.waitForSelector('.records-table');

    assert.equal(await page.locator('.month-row').count(), 2, 'Date order groups bookings by month');
    await page.getByRole('button', { name: 'Revenue', exact: true }).click();
    await page.waitForURL(/sort=revenue-desc/);
    assert.equal(await page.locator('th[aria-sort]').getAttribute('aria-sort'), 'descending');
    assert.equal(await page.locator('.records-table .row-open').first().innerText(), 'Delta Agency');
    assert.equal(await page.locator('.month-row').count(), 0, 'Month headings only appear in date order');

    await page.getByRole('button', { name: 'Revenue', exact: true }).click();
    await page.waitForURL(/sort=revenue-asc/);
    assert.equal(await page.locator('.records-table .row-open').first().innerText(), 'Bravo Travel');
    await page.close();
  });

  await t.test('4. A booking opens in a panel with its history for the owner', async () => {
    const page = await browser.newPage({ viewport: { width: 1440, height: 900 } });
    const requests = await routeRecords(page);
    await page.goto(`${BASE_URL}/bookings`);
    await page.waitForSelector('.records-table');

    await page.getByRole('button', { name: 'Bravo Travel' }).click();
    const panel = page.getByRole('dialog', { name: 'Booking BKG-1002' });
    await panel.waitFor();
    await panel.locator('.history-item').first().waitFor();

    const states = (await panel.locator('.detail-states').innerText()).replace(/\s+/g, ' ');
    assert.ok(states.includes('Payment Pending') && states.includes('Booking Confirmed'), states);
    assert.ok(await panel.getByRole('button', { name: 'Mark as paid' }).isVisible());
    assert.equal(await panel.getByRole('button', { name: 'Confirm booking' }).count(), 0, 'A confirmed booking offers no confirm');

    const changes = (await panel.locator('.history-changes').first().innerText()).replace(/\s+/g, ' ');
    assert.ok(changes.includes('Revenue ₱45,000 to ₱50,000'), 'Money in the history reads as pesos: ' + changes);
    assert.ok((await panel.locator('.history-note').last().innerText()).includes('₱45,000'));
    assert.ok(requests.some((url) => url.includes('entityType=Booking') && url.includes('entityCode=BKG-1002')));
    await page.close();
  });

  await t.test('5. Staff see the booking panel without the history, and nothing asks for it', async () => {
    const page = await browser.newPage({ viewport: { width: 390, height: 844 } });
    const requests = await routeRecords(page, staff);
    await page.goto(`${BASE_URL}/bookings`);
    await page.waitForSelector('.record-item');

    await page.locator('.record-item').first().click();
    await page.getByRole('dialog', { name: /Booking BKG-/ }).waitFor();
    await page.waitForTimeout(300);
    assert.equal(await page.locator('.record-history').count(), 0);
    assert.equal(requests.length, 0, 'No request to the activity log, which Staff cannot read');
    await page.close();
  });

  await t.test('6. Phones get a stacked list, the actions stay reachable and nothing scrolls sideways', async () => {
    for (const width of [390, 320]) {
      const page = await browser.newPage({ viewport: { width, height: 760 } });
      await routeRecords(page);
      for (const path of ['/bookings', '/packages', '/expenses']) {
        await page.goto(`${BASE_URL}${path}`);
        await page.waitForSelector('.record-item');
        assert.equal(await page.locator('.records-table').count(), 0, `${path} at ${width}px uses the stacked list`);
        const overflow = await page.evaluate(() => document.documentElement.scrollWidth - window.innerWidth);
        assert.ok(overflow <= 0, `${path} at ${width}px scrolls sideways by ${overflow}px`);
        // The title, search and account button share the first row; the page's actions come after.
        const rows = await page.evaluate(() => {
          const top = (selector) => Math.round(document.querySelector(selector).getBoundingClientRect().top);
          return { title: top('.page-title'), search: top('.header-search'), account: top('.account-button'), actions: top('.page-actions') };
        });
        assert.ok(Math.abs(rows.account - rows.title) < 24 && Math.abs(rows.search - rows.title) < 24, `${path} at ${width}px keeps the header on one row: ${JSON.stringify(rows)}`);
        assert.ok(rows.actions > rows.title + 24, `${path} at ${width}px puts the actions on their own row`);
      }

      await page.goto(`${BASE_URL}/bookings`);
      await page.waitForSelector('.record-item');
      await page.getByRole('button', { name: 'More', exact: true }).click();
      const menu = page.getByRole('menu');
      await menu.waitFor();
      assert.deepEqual(
        (await menu.getByRole('menuitem').allInnerTexts()).map((text) => text.split('\n')[0]),
        ['Export bookings', 'Import from a file'],
      );
      // The More button starts the row at the left edge, so the menu has to open towards the right.
      const box = await menu.boundingBox();
      assert.ok(box.x >= 0 && box.x + box.width <= width, `The More menu at ${width}px runs from ${Math.round(box.x)} to ${Math.round(box.x + box.width)}`);
      await page.keyboard.press('Escape');
      await page.getByRole('menu').waitFor({ state: 'detached' });
      assert.equal(await page.evaluate(() => document.activeElement?.getAttribute('aria-haspopup')), 'menu', 'Focus returns to the More button');
      await page.close();
    }
  });

  await t.test('7. Expenses split by category, filter by it, and export the chosen period', async () => {
    const page = await browser.newPage({ viewport: { width: 1440, height: 900 } });
    await routeRecords(page);
    await page.goto(`${BASE_URL}/expenses`);
    await page.waitForSelector('.records-table');

    assert.ok((await summary(page)).includes('3 expenses'), 'The voided expense is not counted');
    assert.ok((await summary(page)).includes('₱390,000 total'));
    const marketing = page.locator('.category-chip', { hasText: 'Marketing' });
    assert.ok((await marketing.innerText()).includes('13%'), 'Marketing is 50,000 of 390,000');

    await marketing.click();
    await page.waitForURL(/category=Marketing/);
    assert.deepEqual(await page.locator('.records-table .row-open').allInnerTexts(), ['Marketing']);
    assert.ok((await summary(page)).includes('₱50,000 still owed on 1 pending'));

    await page.goto(`${BASE_URL}/expenses?period=month`);
    await page.waitForSelector('.records-table');
    await page.getByRole('button', { name: 'More', exact: true }).click();
    const href = await page.getByRole('menuitem', { name: /Export expenses/ }).getAttribute('href');
    assert.equal(href, '/api/export/expenses.csv?from=2026-10-01&to=2026-10-31');
    assert.ok((await summary(page)).includes('₱350,000 total'));
    await page.close();
  });

  await t.test('8. A list that fails to load says so and offers to try again', async () => {
    const page = await browser.newPage({ viewport: { width: 1440, height: 900 } });
    await routeRecords(page);
    let failing = true;
    await page.route('**/api/bookings', (route) =>
      failing
        ? route.fulfill({ status: 500, contentType: 'application/json', body: '{}' })
        : route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify({ bookings, packages: [] }) }),
    );
    await page.goto(`${BASE_URL}/bookings`);
    const alert = page.locator('.load-error');
    await alert.waitFor();
    assert.ok((await alert.innerText()).includes('The bookings could not be loaded'));
    assert.equal(await page.getByText('No bookings yet').count(), 0, 'A failed load is not an empty list');

    failing = false;
    await alert.getByRole('button', { name: 'Try again' }).click();
    await page.waitForSelector('.records-table');
    assert.equal(await page.locator('.records-table .record-row').count(), 4);
    await page.close();
  });

  await t.test('9. Packages list compactly, filter to low slots from the address, and sort by slots left', async () => {
    const page = await browser.newPage({ viewport: { width: 1440, height: 900 } });
    await routeRecords(page);
    await page.goto(`${BASE_URL}/packages?stock=low`);
    await page.waitForSelector('.records-table');

    assert.deepEqual(await page.locator('.records-table .row-open').allInnerTexts(), ['Beta Retreat', 'Gamma Tour']);
    assert.equal(await page.locator('.package-figure, .package-thumb-space').count(), 0, 'No empty photo boxes');

    await page.getByRole('button', { name: 'Slots', exact: true }).click();
    await page.waitForURL(/sort=slots-asc/);
    assert.equal(await page.locator('.records-table .row-open').first().innerText(), 'Gamma Tour');
    assert.ok((await page.locator('.records-table .record-row').first().innerText()).includes('1 left'));
    await page.close();
  });
});
