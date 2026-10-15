const test = require('node:test');
const assert = require('node:assert/strict');
const { chromium } = require('playwright');
const { spawn, spawnSync } = require('node:child_process');
const http = require('node:http');

const PORT = 5183;
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

const months = ['October 2026', 'November 2026', 'December 2026', 'January 2027', 'February 2027', 'March 2027'];

function forecastFor(measure, scale) {
  const values = [148500, 153000, 168000, 159000, 156000, 182000];
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
    history: [{ label: 'M0', month: 'Sep', value: 145000 / scale }],
    steps: months.map((monthLabel, i) => ({
      step: i + 1,
      month: monthLabel.slice(0, 3),
      monthLabel,
      value: values[i] / scale,
      lower: (values[i] * 0.9) / scale,
      upper: (values[i] * 1.1) / scale,
    })),
    insight: { direction: 'up', changePercent: 8.4, peakMonth: 'March 2027', peakValue: 182000 / scale, notes: [] },
  };
}

const notTested = {
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
  dataSource: live,
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

const ownWording = {
  summary: 'Booking revenue is forecast to rise about 8.4% from October 2026 to March 2027, against the last six recorded months, with the highest month March 2027 at about ₱182,000.',
  points: [
    'Each month comes with an 80% range: for March 2027, ₱163,800 to ₱200,200. In about 8 months out of 10 the recorded figure should land inside it.',
    'Across the six months that is about ₱966,500 in booking revenue, before any costs.',
    'There are not yet enough recorded months to test it on months it had not seen.',
    'It learns from the bookings recorded up to September 2026.',
  ],
};

function explanation(measure, extra = {}) {
  return {
    measure,
    ok: true,
    reason: null,
    source: 'rules',
    model: null,
    fallbackReason: null,
    summary: measure === 'revenue' ? ownWording.summary : 'Passenger numbers are forecast to rise about 8.4% from October 2026 to March 2027.',
    points: ownWording.points,
    aiAvailable: false,
    aiModel: null,
    facts: { lastRecordedMonth: 'September 2026' },
    ...extra,
  };
}

const geminiWording = {
  source: 'gemini',
  model: 'gemini-3.8-flash',
  summary: 'Bookings should pick up towards March 2027, the busiest forecast month at about ₱182,000. <b>Plan for it.</b>',
  points: ['Each month has an 80% range around its forecast.', 'These figures come from the agency’s recorded bookings up to September 2026.'],
};

function json(route, body, status = 200) {
  return route.fulfill({ status, contentType: 'application/json', body: JSON.stringify(body) });
}

/** Home with the wording answers given by `world`; returns the explanation requests made. */
async function routeHome(page, world) {
  const asked = [];
  await page.route('**/api/**', (route) => json(route, { message: 'There is no API at this address.' }, 404));
  await page.route('**/api/auth/me', (route) => json(route, owner));
  await page.route('**/api/app/config', (route) => json(route, { today: '2026-10-13', timeZone: 'Asia/Manila' }));
  await page.route('**/api/dashboard', (route) => json(route, dashboard));
  await page.route('**/api/forecast', (route) => json(route, forecastFor('revenue', 1)));
  await page.route('**/api/forecast?measure=passengers', (route) => json(route, forecastFor('passengers', 1000)));
  await page.route('**/api/forecast/evaluation**', (route) => json(route, notTested));
  await page.route('**/api/forecast/explanation**', async (route) => {
    const request = route.request();
    const measure = new URL(request.url()).searchParams.get('measure');
    asked.push(`${request.method()} ${measure}`);
    const answer = await (request.method() === 'POST' ? world.gemini(measure, asked) : world.own(measure, asked));
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

async function text(locator) {
  return (await locator.innerText()).replace(/\s+/g, ' ').trim();
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

test('The forecast in plain words', async (t) => {
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

  await t.test('1. Without Gemini, ProphetOps words the forecast once the details are open', async () => {
    const page = await browser.newPage({ viewport: { width: 1440, height: 900 } });
    const asked = await routeHome(page, { own: (measure) => explanation(measure), gemini: () => ({ status: 500, body: {} }) });

    await page.goto(`${BASE_URL}/home`);
    await page.locator('.forecast-chart').waitFor();
    assert.deepEqual(asked, [], 'Nothing is worded while the details are closed');

    await openDetails(page);
    const block = page.locator('.explain-block');
    await block.locator('.explain-summary').waitFor();
    assert.deepEqual(asked, ['GET revenue']);
    assert.equal(await text(block.locator('.explain-summary')), ownWording.summary);
    assert.deepEqual(await block.locator('.explain-points li').allInnerTexts(), ownWording.points);
    assert.equal(await text(block.locator('.explain-source')), 'Worded by ProphetOps from the forecast\'s own figures.');
    assert.equal(await block.getByRole('button', { name: 'Word it with Gemini' }).count(), 0, 'No offer when Gemini is not set up');
    assert.equal(await block.locator('.explain-privacy').count(), 0);

    // The block sits first in the details, before the monthly breakdown.
    const first = await page.locator('#forecast-details .forecast-details-body > *').first().getAttribute('class');
    assert.match(first, /explain-block/);
    await page.close();
  });

  await t.test('2. With Gemini set up, its wording replaces the rules on request and can be switched back', async () => {
    const page = await browser.newPage({ viewport: { width: 1440, height: 900 } });
    const asked = await routeHome(page, {
      own: (measure) => explanation(measure, { aiAvailable: true, aiModel: 'gemini-3.8-flash' }),
      gemini: (measure) => explanation(measure, { aiAvailable: true, aiModel: 'gemini-3.8-flash', ...geminiWording }),
    });

    await page.goto(`${BASE_URL}/home#forecast-details`);
    const block = page.locator('.explain-block');
    await block.locator('.explain-summary').waitFor();
    assert.match(await text(block.locator('.explain-privacy')), /never a client, a booking or a member of staff/);
    assert.match(await text(block.locator('.explain-privacy')), /Holt-Winters makes every figure/);

    await block.getByRole('button', { name: 'Word it with Gemini' }).click();
    await block.locator('.explain-by').waitFor();
    assert.deepEqual(asked, ['GET revenue', 'POST revenue']);
    assert.equal(await text(block.locator('.explain-by')), 'Written by Gemini');
    assert.equal(await text(block.locator('.explain-summary')), geminiWording.summary, 'Shown as plain text, markup and all');
    assert.equal(await block.locator('.explain-summary b').count(), 0, 'Nothing in the wording is read as markup');
    assert.equal(await text(block.locator('.explain-source')), 'Worded by Gemini (gemini-3.8-flash) and checked: every figure in it is one of the forecast\'s own.');
    assert.equal(await block.locator('.explain-privacy').count(), 0);

    await block.getByRole('button', { name: 'Show ProphetOps\' wording' }).click();
    assert.equal(await text(block.locator('.explain-summary')), ownWording.summary);
    await block.getByRole('button', { name: 'Show Gemini\'s wording' }).click();
    assert.equal(await text(block.locator('.explain-summary')), geminiWording.summary);
    assert.deepEqual(asked, ['GET revenue', 'POST revenue'], 'Switching back and forth asks nothing more');
    await page.close();
  });

  await t.test('3. When Gemini’s wording is set aside, the rules stay and the reason is given', async () => {
    const page = await browser.newPage({ viewport: { width: 1440, height: 900 } });
    await routeHome(page, {
      own: (measure) => explanation(measure, { aiAvailable: true, aiModel: 'gemini-3.8-flash' }),
      gemini: (measure) => explanation(measure, { aiAvailable: true, aiModel: 'gemini-3.8-flash', fallbackReason: 'unknown-figure' }),
    });

    await page.goto(`${BASE_URL}/home#forecast-details`);
    const block = page.locator('.explain-block');
    await block.getByRole('button', { name: 'Word it with Gemini' }).click();
    const notice = block.getByRole('status');
    await notice.waitFor();
    assert.equal(await text(notice), 'Gemini’s wording held a figure that is not in the forecast, so it was set aside. This is ProphetOps’ own wording.');
    assert.equal(await text(block.locator('.explain-summary')), ownWording.summary);
    assert.equal(await block.locator('.explain-by').count(), 0);
    assert.equal(await block.getByRole('button', { name: 'Word it with Gemini' }).count(), 1, 'It can be asked again');
    await page.close();
  });

  await t.test('4. Each measure is worded on its own, and a failed load can be tried again', async () => {
    const page = await browser.newPage({ viewport: { width: 1440, height: 900 } });
    const asked = await routeHome(page, {
      own: (measure, seen) => (seen.length === 1 ? { status: 500, body: {} } : explanation(measure)),
      gemini: () => ({ status: 500, body: {} }),
    });

    await page.goto(`${BASE_URL}/home#forecast-details`);
    const block = page.locator('.explain-block');
    const problem = block.getByRole('alert');
    await problem.waitFor();
    assert.match(await problem.innerText(), /The server ran into a problem/);
    await block.getByRole('button', { name: 'Try again' }).click();
    await block.locator('.explain-summary').waitFor();

    await page.getByRole('button', { name: 'Passengers' }).click();
    await block.locator('.explain-summary', { hasText: 'Passenger numbers are forecast' }).waitFor();
    assert.deepEqual(asked, ['GET revenue', 'GET revenue', 'GET passengers']);
    await page.close();
  });

  await t.test('5. On a phone the block keeps to the screen', async () => {
    const page = await browser.newPage({ viewport: { width: 390, height: 844 } });
    await routeHome(page, {
      own: (measure) => explanation(measure, { aiAvailable: true, aiModel: 'gemini-3.8-flash' }),
      gemini: () => ({ status: 500, body: {} }),
    });

    await page.goto(`${BASE_URL}/home#forecast-details`);
    const block = page.locator('.explain-block');
    await block.locator('.explain-summary').waitFor();
    const overflow = await page.evaluate(() => document.documentElement.scrollWidth - window.innerWidth);
    assert.ok(overflow <= 0, `The page scrolls sideways by ${overflow}px`);
    const button = await block.getByRole('button', { name: 'Word it with Gemini' }).boundingBox();
    const box = await block.boundingBox();
    assert.ok(button.x >= box.x - 1 && button.x + button.width <= box.x + box.width + 1, 'The button stays inside the block');
    await page.close();
  });
});
