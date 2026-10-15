const test = require('node:test');
const assert = require('node:assert/strict');
const { chromium } = require('playwright');
const { spawn, spawnSync } = require('node:child_process');
const http = require('node:http');

const PORT = 5180;
const BASE_URL = `http://localhost:${PORT}`;

const owner = { name: 'Renan & Tina', email: 'owner@prophetops.com', role: 'Owner / Management', defaultPath: '/home' };

const live = {
  status: 'live',
  usingLiveRecords: true,
  usingSample: false,
  label: 'Live booking history',
  liveMonthsAvailable: 37,
  recordedMonths: 37,
  minimumMonths: 24,
  filledMonths: 0,
  lastRecordedMonth: 'September 2026',
};

function forecastFor(measure, scale) {
  return {
    method: 'Holt-Winters',
    measure,
    unit: measure === 'revenue' ? 'PHP' : measure,
    seasonLength: 12,
    horizon: 6,
    ok: true,
    accuracy: 94,
    params: { alpha: 0.3, beta: 0.05, gamma: 0.2 },
    metrics: { mae: 8500 / scale, rmse: 11200 / scale, mape: 5.8, sampleSize: 25 },
    baselines: { seasonalNaiveMae: 14000 / scale, naiveMae: 19000 / scale },
    dataSource: live,
    history: [
      { label: 'M-1', month: 'Aug', value: 141000 / scale },
      { label: 'M0', month: 'Sep', value: 145000 / scale },
    ],
    steps: [
      { step: 1, month: 'Oct', monthLabel: 'October 2026', value: 148500 / scale, lower: 136000 / scale, upper: 161000 / scale },
      { step: 2, month: 'Nov', monthLabel: 'November 2026', value: 153000 / scale, lower: 139000 / scale, upper: 167000 / scale },
      { step: 3, month: 'Dec', monthLabel: 'December 2026', value: 168000 / scale, lower: 151000 / scale, upper: 185000 / scale },
      { step: 4, month: 'Jan', monthLabel: 'January 2027', value: 159000 / scale, lower: 140000 / scale, upper: 178000 / scale },
      { step: 5, month: 'Feb', monthLabel: 'February 2027', value: 156000 / scale, lower: 135000 / scale, upper: 177000 / scale },
      { step: 6, month: 'Mar', monthLabel: 'March 2027', value: 182000 / scale, lower: 158000 / scale, upper: 206000 / scale },
    ],
    insight: { direction: 'up', changePercent: 8.4, peakMonth: 'March 2027', peakValue: 182000 / scale, notes: [] },
  };
}

function evaluationFor(measure, maeScale) {
  const rows = [
    [1, 13, 6.2, 10.9, 11],
    [2, 12, 7.0, 11.2, 10],
    [3, 11, 7.5, 11.6, 9],
    [4, 10, 7.9, 11.9, 8],
    [5, 9, 8.1, 12.3, 8],
    [6, 8, 8.6, 12.8, 6],
  ];
  return {
    method: 'Rolling-origin test',
    measure,
    unit: measure === 'revenue' ? 'PHP' : measure,
    ok: true,
    reason: null,
    seasonLength: 12,
    horizon: 6,
    monthsAvailable: 37,
    monthsRequired: 25,
    origins: 13,
    firstTestedMonth: 'September 2025',
    lastTestedMonth: 'September 2026',
    dataSource: live,
    byHorizon: rows.map(([step, forecasts, mape, naive, inside]) => ({
      step,
      forecasts,
      mae: 10000 / maeScale,
      mape,
      seasonalNaiveMae: 16000 / maeScale,
      seasonalNaiveMape: naive,
      insideRange: inside,
      coverage: inside / forecasts,
    })),
    overall: {
      forecasts: 63,
      mae: 10650 / maeScale,
      mape: 7.4,
      seasonalNaiveMae: 16640 / maeScale,
      seasonalNaiveMape: 11.6,
      insideRange: 52,
      coverage: 0.8254,
      skillVsSeasonalNaive: 0.36,
    },
    points: [],
  };
}

const tooShort = {
  method: 'Rolling-origin test',
  measure: 'revenue',
  unit: 'PHP',
  ok: false,
  reason: 'The test needs two full seasonal cycles to learn from and at least one later month to check.',
  seasonLength: 12,
  horizon: 6,
  monthsAvailable: 24,
  monthsRequired: 25,
  origins: 0,
  dataSource: { ...live, liveMonthsAvailable: 24, recordedMonths: 24 },
  byHorizon: [],
  overall: null,
  points: [],
};

const dashboard = {
  totalsScope: 'lifetime',
  excludesVoided: true,
  revenue: 1248500,
  costs: 812000,
  estimatedProfit: 436500,
  bookings: 142,
  packages: 12,
  expenses: 48,
  forecast: { method: 'Holt-Winters', horizon: 6, ok: true, status: 'live', accuracy: 94, mape: 5.8, nextValue: 148500, direction: 'up', changePercent: 8.4, peakMonth: 'March 2027', peakValue: 182000, dataSource: live },
  thisMonth: {
    current: { from: '2026-10-01', to: '2026-10-13', revenue: 1248500, bookings: 12, expenses: 812000 },
    previous: { from: '2026-09-01', to: '2026-09-13', revenue: 1102000, bookings: 14, expenses: 790000 },
  },
  lowStockPackages: [],
  pendingPayments: { count: 0, amount: 0 },
  recentBookings: [],
  lastUpdated: 'Oct 13, 2026',
};

function json(route, body, status = 200) {
  return route.fulfill({ status, contentType: 'application/json', body: JSON.stringify(body) });
}

/** Home's answers, with the accuracy test supplied by `evaluation(measure)`. Returns the test requests seen. */
async function routeHome(page, evaluation) {
  const asked = [];
  await page.route('**/api/auth/me', (route) => json(route, owner));
  await page.route('**/api/app/config', (route) => json(route, { today: '2026-10-13', timeZone: 'Asia/Manila' }));
  await page.route('**/api/dashboard', (route) => json(route, dashboard));
  await page.route('**/api/forecast', (route) => json(route, forecastFor('revenue', 1)));
  await page.route('**/api/forecast?measure=passengers', (route) => json(route, forecastFor('passengers', 1000)));
  await page.route('**/api/forecast/evaluation**', async (route) => {
    const measure = new URL(route.request().url()).searchParams.get('measure');
    asked.push(measure);
    const answer = await evaluation(measure, asked.length);
    return answer.status ? json(route, answer.body, answer.status) : json(route, answer);
  });
  await page.route('**/api/activity**', (route) => json(route, []));
  await page.route('**/api/expenses', (route) => json(route, []));
  return asked;
}

async function openDetails(page) {
  await page.getByText('Monthly figures and model details').click();
  await page.locator('#forecast-details[open]').waitFor();
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

test('Forecast accuracy on months it had not seen', async (t) => {
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

  await t.test('1. The test waits for the details, then says in plain words how the forecast did', async () => {
    const page = await browser.newPage({ viewport: { width: 1440, height: 900 } });
    const asked = await routeHome(page, (measure) => evaluationFor(measure, 1));

    await page.goto(`${BASE_URL}/home`);
    await page.locator('.forecast-chart').waitFor();
    assert.deepEqual(asked, [], 'Nothing is tested while the details are closed');

    await openDetails(page);
    const section = page.locator('.accuracy-block');
    await section.getByRole('heading', { name: 'Accuracy on months it had not seen' }).waitFor();
    await section.locator('.accuracy-table').waitFor();
    assert.deepEqual(asked, ['revenue']);

    const lead = (await section.locator('.accuracy-lead').innerText()).replace(/\s+/g, ' ');
    assert.match(lead, /rerun from each of 13 past months, using only the months before it/);
    assert.match(lead, /from September 2025 to September 2026: 63 forecasts in all/);
    assert.match(lead, /missed by 7\.4% on average \(about ₱10,650 a month\), against 11\.6% for repeating the same month last year/);
    assert.match(lead, /That is a 36% smaller miss\./);
    assert.match(lead, /inside the 80% range in 52 of 63 forecasts \(83%\)/);

    const rows = await section.locator('.accuracy-table tbody tr').allInnerTexts();
    assert.equal(rows.length, 6);
    assert.deepEqual(rows[0].split('\t').map((c) => c.trim()), ['1 month', '6.2%', '10.9%', '11 of 13']);
    assert.deepEqual(rows[5].split('\t').map((c) => c.trim()), ['6 months', '8.6%', '12.8%', '6 of 8']);
    assert.equal(await section.locator('tbody tr').first().locator('td.smaller').count(), 1, 'The smaller miss is marked');

    const download = section.getByRole('link', { name: 'Download the test as CSV' });
    assert.equal(await download.getAttribute('href'), '/api/export/forecast-test.csv?measure=revenue');

    // Closing and opening again reuses the result.
    await page.getByText('Monthly figures and model details').click();
    await openDetails(page);
    assert.deepEqual(asked, ['revenue']);
    await page.close();
  });

  await t.test('2. Too few months: the section says what the test needs and shows no score', async () => {
    const page = await browser.newPage({ viewport: { width: 1440, height: 900 } });
    await routeHome(page, () => tooShort);

    await page.goto(`${BASE_URL}/home#forecast-details`);
    const section = page.locator('.accuracy-block');
    await section.getByText('The test needs 25 recorded months').waitFor();
    const text = (await section.innerText()).replace(/\s+/g, ' ');
    assert.match(text, /two years to learn from and at least one more to check\. 24 are recorded so far\./);
    assert.equal(await section.locator('table').count(), 0);
    assert.equal(await section.getByRole('link', { name: 'Download the test as CSV' }).count(), 0);
    await page.close();
  });

  await t.test('3. A test that fails to load says so and loads on Try again', async () => {
    const page = await browser.newPage({ viewport: { width: 1440, height: 900 } });
    await routeHome(page, (measure, attempt) =>
      attempt === 1 ? { status: 500, body: {} } : evaluationFor(measure, 1));

    await page.goto(`${BASE_URL}/home#forecast-details`);
    const section = page.locator('.accuracy-block');
    const problem = section.getByRole('alert');
    await problem.waitFor();
    assert.match(await problem.innerText(), /The server ran into a problem/);

    await section.getByRole('button', { name: 'Try again' }).click();
    await section.locator('.accuracy-table').waitFor();
    assert.equal(await section.getByRole('alert').count(), 0);
    await page.close();
  });

  await t.test('4. Switching the measure tests that measure, in its own unit', async () => {
    const page = await browser.newPage({ viewport: { width: 1440, height: 900 } });
    const asked = await routeHome(page, (measure) => evaluationFor(measure, measure === 'revenue' ? 1 : 10000));

    await page.goto(`${BASE_URL}/home#forecast-details`);
    const section = page.locator('.accuracy-block');
    await section.locator('.accuracy-table').waitFor();

    await page.getByRole('button', { name: 'Passengers' }).click();
    await page.waitForFunction(() => document.querySelector('.accuracy-download')?.getAttribute('href')?.endsWith('measure=passengers'));
    assert.deepEqual(asked, ['revenue', 'passengers']);
    const lead = (await section.locator('.accuracy-lead').innerText()).replace(/\s+/g, ' ');
    assert.match(lead, /about 1\.1 passengers a month/);
    await page.close();
  });

  await t.test('5. On a phone the table keeps to the screen with short headings', async () => {
    const page = await browser.newPage({ viewport: { width: 390, height: 844 } });
    await routeHome(page, (measure) => evaluationFor(measure, 1));

    await page.goto(`${BASE_URL}/home#forecast-details`);
    const section = page.locator('.accuracy-block');
    await section.locator('.accuracy-table').waitFor();

    const headings = (await section.locator('thead th').allInnerTexts()).map((h) => h.trim());
    assert.deepEqual(headings, ['Months ahead', 'Forecast', 'Last year', 'In range']);
    const overflow = await page.evaluate(() => document.documentElement.scrollWidth - window.innerWidth);
    assert.ok(overflow <= 0, `The page scrolls sideways by ${overflow}px`);
    const table = await section.locator('.accuracy-table').boundingBox();
    const box = await section.boundingBox();
    assert.ok(table.width <= box.width + 1, 'The table fits inside its section');
    await page.close();
  });
});
