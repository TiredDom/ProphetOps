const test = require('node:test');
const assert = require('node:assert/strict');
const { chromium } = require('playwright');
const { spawn, spawnSync } = require('node:child_process');
const http = require('node:http');

const PORT = 5176;
const BASE_URL = `http://localhost:${PORT}`;

const owner = { name: 'Renan & Tina', email: 'owner@prophetops.com', role: 'Owner / Management', defaultPath: '/home' };
const staff = { name: 'Casey Staff', email: 'staff@prophetops.com', role: 'Staff', defaultPath: '/bookings' };

// Business date 13 October 2026. Bookings stop at July 2026, so August and September are
// unrecorded and the forecast runs August 2026 to January 2027, with October inside it.
const forecast = {
  method: 'Holt-Winters',
  seasonLength: 12,
  horizon: 6,
  ok: true,
  accuracy: 76,
  params: { alpha: 0.4, beta: 0.1, gamma: 0.3 },
  metrics: { mae: 250000, rmse: 310000, mape: 24.2, sampleSize: 25 },
  baselines: { seasonalNaiveMae: 300000, naiveMae: 320000 },
  insight: { direction: 'up', changePercent: 3, peakMonth: 'December 2026', peakValue: 2100000, notes: [] },
  dataSource: {
    status: 'live',
    usingLiveRecords: true,
    usingSample: false,
    label: 'Live booking history',
    liveMonthsAvailable: 37,
    recordedMonths: 37,
    minimumMonths: 24,
    filledMonths: 0,
    lastRecordedMonth: 'July 2026',
  },
  history: [
    { label: 'M-2', month: 'May', value: 2100000 },
    { label: 'M-1', month: 'Jun', value: 1200000 },
    { label: 'M0', month: 'Jul', value: 451500 },
  ],
  steps: [
    { step: 1, month: 'Aug', monthLabel: 'August 2026', value: 1370000, lower: 900000, upper: 1840000 },
    { step: 2, month: 'Sep', monthLabel: 'September 2026', value: 1140000, lower: 600000, upper: 1680000 },
    { step: 3, month: 'Oct', monthLabel: 'October 2026', value: 1310000, lower: 557000, upper: 2070000 },
    { step: 4, month: 'Nov', monthLabel: 'November 2026', value: 1610000, lower: 800000, upper: 2420000 },
    { step: 5, month: 'Dec', monthLabel: 'December 2026', value: 2100000, lower: 1120000, upper: 3070000 },
    { step: 6, month: 'Jan', monthLabel: 'January 2027', value: 1340000, lower: 300000, upper: 2380000 },
  ],
};

// The same months as counts. Passengers dip in September and November, so the January peak is a
// peak, not a steady climb; bookings fall after a December peak.
function countForecast(measure, values) {
  const counting = measure === 'bookings';
  return {
    ...forecast,
    measure,
    unit: measure,
    insight: { ...forecast.insight, notes: [] },
    metrics: { mae: counting ? 1.33 : 20.2, rmse: counting ? 1.8 : 26.4, mape: counting ? 31.8 : 35.2, sampleSize: 25 },
    baselines: { seasonalNaiveMae: counting ? 1.6 : 24.1, naiveMae: 2 },
    history: [
      { label: 'M-2', month: 'May', value: counting ? 9 : 139 },
      { label: 'M-1', month: 'Jun', value: counting ? 4 : 38 },
      { label: 'M0', month: 'Jul', value: counting ? 2 : 21 },
    ],
    steps: forecast.steps.map((s, i) => ({
      ...s,
      value: values[i],
      lower: Math.max(0, values[i] * 0.45),
      upper: values[i] * 1.6,
    })),
  };
}

const passengerForecast = countForecast('passengers', [77, 67, 100, 82, 97, 105]);
const bookingForecast = countForecast('bookings', [4.4, 3.9, 5.4, 4.2, 7.6, 6.1]);

const dashboard = {
  totalsScope: 'lifetime',
  excludesVoided: true,
  revenue: 54000000,
  costs: 36000000,
  estimatedProfit: 18000000,
  bookings: 197,
  packages: 4,
  expenses: 3,
  forecast: { method: 'Holt-Winters', horizon: 6, ok: true, status: 'live', accuracy: 76, mape: 24.2, nextValue: 1370000, direction: 'up', changePercent: 3, peakMonth: 'December 2026', peakValue: 2100000, dataSource: forecast.dataSource },
  thisMonth: {
    current: { from: '2026-10-01', to: '2026-10-13', revenue: 12000, bookings: 1, expenses: 0 },
    previous: { from: '2026-09-01', to: '2026-09-13', revenue: 0, bookings: 0, expenses: 0 },
  },
  lowStockPackages: [],
  pendingPayments: { count: 0, amount: 0 },
  recentBookings: [],
  lastUpdated: 'Oct 13, 2026',
};

const bookings = [
  { id: 'BKG-1', backendId: 1, revision: 1, ds: '2026-10-05', y: 2, client: 'Bravo Travel', package: 'Boracay', packageId: 'PKG-1', entryType: 'Package preset', destination: 'Boracay', grossRevenue: 12000, paymentStatus: 'Paid', bookingStatus: 'Confirmed', staffAssigned: null, source: 'Package preset', notes: null, voided: false },
  { id: 'BKG-2', backendId: 2, revision: 1, ds: '2026-10-06', y: 2, client: 'Voided Client', package: 'Boracay', packageId: 'PKG-1', entryType: 'Package preset', destination: 'Boracay', grossRevenue: 500000, paymentStatus: 'Paid', bookingStatus: 'Confirmed', staffAssigned: null, source: 'Package preset', notes: null, voided: true },
  { id: 'BKG-3', backendId: 3, revision: 1, ds: '2026-07-20', y: 3, client: 'Charlie Group', package: 'Boracay', packageId: 'PKG-1', entryType: 'Package preset', destination: 'Boracay', grossRevenue: 90000, paymentStatus: 'Paid', bookingStatus: 'Confirmed', staffAssigned: null, source: 'Package preset', notes: null, voided: false },
];

const packages = [
  { id: 'PKG-1', backendId: 1, revision: 1, packageName: 'Alpha Escape', destination: 'Palawan', duration: '3D2N', basePrice: 10000, inclusions: null, availableSlots: 2, soldCount: 40, reservedCount: 0, status: 'Critical', imageUrl: null },
  { id: 'PKG-2', backendId: 2, revision: 1, packageName: 'Beta Retreat', destination: 'Cebu', duration: '4D3N', basePrice: 18000, inclusions: null, availableSlots: 30, soldCount: 5, reservedCount: 0, status: 'Normal', imageUrl: null },
];

const expenses = [
  { id: 'EXP-1', backendId: 1, date: '2025-12-04', category: 'Tour operations', amount: 300000, relatedPackage: 'Peak season', paymentStatus: 'Paid', notes: null, voided: false },
  { id: 'EXP-2', backendId: 2, date: '2026-01-06', category: 'Marketing', amount: 50000, relatedPackage: 'Flyers', paymentStatus: 'Paid', notes: null, voided: false },
  { id: 'EXP-3', backendId: 3, date: '2026-06-15', category: 'Overhead', amount: 40000, relatedPackage: 'Office', paymentStatus: 'Paid', notes: null, voided: false },
];

async function route(page, user = owner) {
  const forecastRequests = [];
  const json = (body) => ({ status: 200, contentType: 'application/json', body: JSON.stringify(body) });
  await page.route('**/api/auth/me', (r) => r.fulfill(json(user)));
  await page.route('**/api/app/config', (r) => r.fulfill(json({ today: '2026-10-13', timeZone: 'Asia/Manila' })));
  await page.route('**/api/dashboard', (r) => r.fulfill(json(dashboard)));
  await page.route('**/api/forecast**', (r) => {
    const url = r.request().url();
    forecastRequests.push(url);
    if (user.role === 'Staff') return r.fulfill({ status: 403, body: '' });
    const measure = new URL(url).searchParams.get('measure') ?? 'revenue';
    return r.fulfill(json(measure === 'passengers' ? passengerForecast : measure === 'bookings' ? bookingForecast : forecast));
  });
  await page.route('**/api/activity**', (r) => r.fulfill(json([])));
  await page.route('**/api/bookings', (r) => r.fulfill(json({ bookings, packages })));
  await page.route('**/api/inventory', (r) => r.fulfill(json(packages)));
  await page.route('**/api/expenses', (r) => r.fulfill(json(expenses)));
  return forecastRequests;
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

const squash = (text) => text.replace(/\s+/g, ' ').trim();

test('The forecast leads Home and follows into the record pages', async (t) => {
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

  await t.test('1. Home says what the forecast expects, how it was made, and what is missing from it', async () => {
    const page = await browser.newPage({ viewport: { width: 1440, height: 900 } });
    await route(page);
    await page.goto(`${BASE_URL}/home`);
    await page.waitForSelector('.forecast-headline');

    assert.equal(
      squash(await page.locator('.forecast-headline').innerText()),
      'Revenue is forecast to climb to a December peak of ₱2.10M, then fall 36.2% in January 2027.',
    );
    assert.ok((await page.locator('.overview-subtitle').innerText()).includes('Holt-Winters'), 'The method is named');
    assert.ok((await page.locator('.overview-subtitle').innerText()).includes('37 months of bookings to July 2026'));
    const gap = squash(await page.locator('.forecast-gap').innerText());
    assert.ok(gap.includes('No bookings are recorded for August–September 2026'), gap);
    assert.ok(gap.includes('works from July 2026'), gap);
    await page.close();
  });

  await t.test('2. Each decision links to the spending in the same month a year earlier', async () => {
    const page = await browser.newPage({ viewport: { width: 1440, height: 900 } });
    await route(page);
    await page.goto(`${BASE_URL}/home`);
    await page.waitForSelector('.rec-next');

    assert.ok((await page.locator('.rec-section-title').innerText()).includes('Decisions to review'));
    const links = page.locator('.rec-next');
    assert.equal(await links.count(), 3);
    const peakLink = links.first();
    assert.equal(squash(await peakLink.innerText()), 'See spending in December 2025');
    assert.equal(await peakLink.getAttribute('href'), '/expenses?period=custom&from=2025-12-01&to=2025-12-31');

    // The decision heading is a real button, so the chart highlight works from the keyboard.
    const toggle = page.locator('.rec-toggle').first();
    await toggle.focus();
    await page.keyboard.press('Enter');
    assert.equal(await toggle.getAttribute('aria-pressed'), 'true');

    await peakLink.click();
    await page.waitForURL('**/expenses?period=custom&from=2025-12-01&to=2025-12-31');
    await page.waitForSelector('.records-table');
    assert.deepEqual(await page.locator('.records-table .row-open').allInnerTexts(), ['Tour operations']);
    await page.close();
  });

  await t.test('3. The sidebar keeps this month\'s forecast in view for the owner', async () => {
    const page = await browser.newPage({ viewport: { width: 1440, height: 900 } });
    await route(page);
    await page.goto(`${BASE_URL}/packages`);
    const pulse = page.locator('.forecast-pulse');
    await pulse.waitFor();
    const text = squash(await pulse.innerText());
    assert.ok(text.includes('This month') && text.includes('₱1.31M'), text);
    assert.ok(text.includes('Peak December 2026'), text);
    await pulse.click();
    await page.waitForURL('**/home');
    await page.close();
  });

  await t.test('4. Bookings compares this month with its forecast, without counting voided bookings', async () => {
    const page = await browser.newPage({ viewport: { width: 1440, height: 900 } });
    await route(page);
    await page.goto(`${BASE_URL}/bookings`);
    const band = page.locator('.forecast-band');
    await band.waitFor();
    const text = squash(await band.innerText());
    assert.ok(text.includes('October 2026 against the forecast'), text);
    assert.ok(text.includes('₱12,000 recorded so far'), 'Only the active October booking counts: ' + text);
    assert.ok(text.includes('₱1.31M forecast for October'), text);
    assert.ok(text.includes('1 booking so far'), "Counts this month's active bookings: " + text);
    assert.ok(text.includes('about 5 forecast, likely 2 to 9'), text);
    assert.ok(text.includes('No bookings are recorded for August–September 2026'), text);
    await page.close();
  });

  await t.test('5. Staff see no forecast anywhere, and nothing asks for one', async () => {
    const page = await browser.newPage({ viewport: { width: 1440, height: 900 } });
    const requests = await route(page, staff);
    for (const path of ['/bookings', '/packages']) {
      await page.goto(`${BASE_URL}${path}`);
      await page.waitForSelector('.records-table');
      await page.waitForTimeout(300);
      assert.equal(await page.locator('.forecast-band, .forecast-pulse').count(), 0, `${path} shows no forecast to Staff`);
    }
    assert.equal(requests.length, 0, 'Staff never request the forecast');
    await page.close();
  });

  await t.test('6. Packages shows the outlook beside the slots, and its button filters to low slots', async () => {
    const page = await browser.newPage({ viewport: { width: 1440, height: 900 } });
    await route(page);
    await page.goto(`${BASE_URL}/packages`);
    const band = page.locator('.forecast-band');
    await band.waitFor();
    const text = squash(await band.innerText());
    assert.ok(text.includes('Demand outlook'), text);
    assert.ok(text.includes('Passenger numbers are forecast to peak in January 2027 at about 105'), text);
    assert.ok(text.includes('32 slots are open across the catalog now'), 'Open slots across every package: ' + text);
    assert.ok(text.includes("fewer than the peak month's forecast"), 'Says when the open slots fall short of the peak: ' + text);
    assert.ok(text.includes('not each package'), 'Says the forecast is not per package');
    assert.equal(await band.locator('.outlook-month').count(), 6);
    assert.equal(squash(await band.locator('.outlook-month.peak .outlook-label').innerText()), 'Jan 27');
    assert.equal(squash(await band.locator('.outlook-month.current .outlook-label').innerText()), 'Oct');

    await band.getByRole('button', { name: 'Show it' }).click();
    await page.waitForURL(/stock=low/);
    assert.deepEqual(await page.locator('.records-table .row-open').allInnerTexts(), ['Alpha Escape']);
    await page.close();
  });

  await t.test('7. Expenses lists the spending decisions and opens last year\'s month in place', async () => {
    const page = await browser.newPage({ viewport: { width: 1440, height: 900 } });
    await route(page);
    await page.goto(`${BASE_URL}/expenses`);
    const band = page.locator('.forecast-band');
    await band.waitFor();
    assert.ok(squash(await band.innerText()).includes('expenses are not forecast'));

    const button = band.locator('.guide-button', { hasText: 'January 2026' });
    assert.equal(squash(await button.innerText()), 'Show January 2026');
    await button.click();
    await page.waitForURL(/from=2026-01-01&to=2026-01-31/);
    assert.equal(await button.getAttribute('aria-pressed'), 'true');
    assert.deepEqual(await page.locator('.records-table .row-open').allInnerTexts(), ['Marketing']);
    await page.close();
  });

  await t.test('9. Home switches between revenue, bookings and passengers, and words each one', async () => {
    const page = await browser.newPage({ viewport: { width: 1440, height: 900 } });
    await route(page);
    await page.goto(`${BASE_URL}/home`);
    await page.waitForSelector('.forecast-headline');

    await page.getByRole('button', { name: 'Passengers', exact: true }).click();
    await page.waitForURL(/measure=passengers/);
    await page.waitForSelector('.overview-title:has-text("Passenger forecast")');
    assert.equal(
      squash(await page.locator('.forecast-headline').innerText()),
      'Passenger numbers are forecast to peak in January 2027 at about 105.',
      'A dip on the way means a peak, not a steady climb',
    );
    assert.ok((await page.locator('.overview-subtitle').innerText()).includes('passengers by booking date'));
    assert.ok(squash(await page.locator('.forecast-subline').innerText()).includes('About 528 passengers'));
    assert.equal(await page.getByRole('button', { name: 'Passengers', exact: true }).getAttribute('aria-pressed'), 'true');

    // Demand decisions open the bookings of the same month last year, never spending.
    const links = await page.locator('.rec-next').evaluateAll((els) => els.map((el) => [el.textContent.trim(), el.getAttribute('href')]));
    assert.ok(links.length > 0, 'Decisions have next steps');
    for (const [label, href] of links) {
      assert.ok(!href.startsWith('/expenses'), `A passenger decision should not open spending: ${label}`);
    }
    assert.ok(
      links.some(([label, href]) => label.startsWith('See bookings in') && href.startsWith('/bookings?period=custom')),
      JSON.stringify(links),
    );

    await page.getByRole('button', { name: 'Bookings', exact: true }).click();
    await page.waitForURL(/measure=bookings/);
    await page.waitForSelector('.overview-title:has-text("Bookings forecast")');
    assert.equal(
      squash(await page.locator('.forecast-headline').innerText()),
      'Bookings are forecast to climb to a December peak of about 8, then fall 19.7% in January 2027.',
    );
    const peakLink = page.locator('.rec-next').first();
    assert.equal(squash(await peakLink.innerText()), 'See packages low on slots');
    assert.equal(await peakLink.getAttribute('href'), '/packages?stock=low');

    await page.reload();
    await page.waitForSelector('.overview-title:has-text("Bookings forecast")');
    await page.close();
  });

  await t.test('10. The monthly figures and model details speak in the chosen unit', async () => {
    const page = await browser.newPage({ viewport: { width: 1440, height: 900 } });
    await route(page);
    await page.goto(`${BASE_URL}/home?measure=bookings#forecast-details`);
    await page.waitForSelector('#forecast-details[open] .step-panel');
    const details = squash(await page.locator('#forecast-details').innerText());
    assert.ok(details.includes('Forecast bookings'), 'The table names its unit');
    assert.ok(details.includes('Bookings per month, by booking date'), 'The measure is described');
    assert.ok(details.includes('Average miss (MAE) 1.3 bookings'), 'Misses are counted, not in pesos: ' + details);
    assert.ok(!details.includes('Average miss in pesos'), 'No peso wording for counts');
    assert.ok(details.includes('not by travel date'), 'Says what a booking-date count is');
    await page.close();
  });

  await t.test('11. On phones the bands fold to one line and open on request', async () => {
    const page = await browser.newPage({ viewport: { width: 390, height: 844 } });
    await route(page);
    await page.goto(`${BASE_URL}/expenses`);
    const band = page.locator('.forecast-band');
    await band.waitFor();
    assert.ok(squash(await band.locator('.band-summary').innerText()).includes('3 decisions on spending'));
    assert.equal(await band.locator('.guide').isVisible(), false, 'Folded by default');
    await band.getByRole('button', { name: 'Show details' }).click();
    assert.equal(await band.locator('.guide').isVisible(), true);
    assert.equal(await page.locator('.forecast-pulse').isVisible(), false, 'No sidebar forecast at phone width');
    const overflow = await page.evaluate(() => document.documentElement.scrollWidth - window.innerWidth);
    assert.ok(overflow <= 0, `Scrolls sideways by ${overflow}px`);
    await page.close();
  });

  await t.test('12. On wide screens a band folds on request, and every band stays folded after', async () => {
    const context = await browser.newContext({ viewport: { width: 1440, height: 900 } });
    const page = await context.newPage();
    await route(page);
    await page.goto(`${BASE_URL}/expenses`);
    const band = page.locator('.forecast-band');
    await band.waitFor();
    assert.equal(await band.locator('.guide').isVisible(), true, 'Open by default on wide screens');
    await band.getByRole('button', { name: 'Hide details' }).click();
    assert.equal(await band.locator('.guide').isVisible(), false);
    assert.ok(squash(await band.locator('.band-summary').innerText()).includes('3 decisions on spending'));

    await page.goto(`${BASE_URL}/bookings`);
    const bookingsBand = page.locator('.forecast-band');
    await bookingsBand.waitFor();
    assert.ok(await bookingsBand.getByRole('button', { name: 'Show details' }).isVisible(), 'The choice holds on the next page');
    await bookingsBand.getByRole('button', { name: 'Show details' }).click();
    assert.ok(await bookingsBand.getByRole('button', { name: 'Hide details' }).isVisible());
    await context.close();
  });
});
