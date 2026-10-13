const test = require('node:test');
const assert = require('node:assert/strict');
const { chromium } = require('playwright');
const { spawn, spawnSync } = require('node:child_process');
const http = require('node:http');
const fs = require('node:fs/promises');
const path = require('node:path');

const PORT = 5174;
const BASE_URL = `http://localhost:${PORT}`;

const mockUser = {
  name: 'Renan & Tina',
  email: 'owner@prophetops.com',
  role: 'Owner / Management',
  defaultPath: '/home'
};

const mockInsufficientForecastWithStaleNotes = {
  method: 'Holt-Winters',
  horizon: 6,
  ok: false,
  accuracy: 0,
  dataSource: {
    status: 'insufficient-history',
    usingLiveRecords: false,
    usingSample: false,
    label: 'Insufficient booking history',
    liveMonthsAvailable: 8,
    recordedMonths: 8,
    minimumMonths: 24,
    filledMonths: 0,
    lastRecordedMonth: 'September 2026'
  },
  history: [],
  steps: [],
  // Intentionally stale insight payload that MUST NOT leak into the UI when ok is false
  insight: {
    direction: 'up',
    changePercent: 8.4,
    peakMonth: 'March 2027',
    peakValue: 182000,
    notes: [
      { kind: 'direction', text: 'Booking revenue is trending upward, projected +8.4% over the next 6 months, peaking in March 2027 at ₱182,000.' },
      { kind: 'capacity', text: 'March 2027 runs 18.2% above the 6-month average, pointing to peak seasonal revenue.' },
      { kind: 'reliability', text: 'Typical monthly error is 5.8%, so figures have stayed close on recorded months.' }
    ]
  }
};

const mockInsufficientDashboard = {
  totalsScope: 'lifetime',
  excludesVoided: true,
  revenue: 1248500,
  costs: 812000,
  estimatedProfit: 436500,
  bookings: 142,
  packages: 12,
  expenses: 48,
  forecast: {
    method: 'Holt-Winters',
    horizon: 6,
    ok: false,
    status: 'insufficient-history',
    accuracy: 0,
    mape: 0,
    nextValue: 0,
    dataSource: {
      status: 'insufficient-history',
      usingLiveRecords: false,
      usingSample: false,
      label: 'Insufficient booking history',
      liveMonthsAvailable: 8,
      recordedMonths: 8,
      minimumMonths: 24,
      filledMonths: 0,
      lastRecordedMonth: 'September 2026'
    }
  },
  thisMonth: {
    current: { from: '2026-10-01', to: '2026-10-13', revenue: 1248500, bookings: 12, expenses: 812000 },
    previous: { from: '2026-09-01', to: '2026-09-13', revenue: 1102000, bookings: 14, expenses: 790000 }
  },
  lowStockPackages: [],
  pendingPayments: { count: 0, amount: 0 },
  recentBookings: [],
  lastUpdated: 'Oct 2, 2026'
};

const mockActivity = [
  { at: '2026-10-12T07:40:00Z', actor: 'owner@prophetops.com', actorName: 'Renan & Tina', action: 'Voided', entityType: 'Booking', entityCode: 'BKG-2597', summary: 'Client cancelled after the airline moved the flight.' },
  { at: '2026-10-11T02:15:00Z', actor: 'owner@prophetops.com', actorName: 'Renan & Tina', action: 'Created', entityType: 'Expense', entityCode: 'EXP-3190', summary: 'Overhead - PHP 45,000' }
];

// Every page that renders Home needs these answered; individual tests override the forecast.
async function routeHome(page, forecast, dashboard = mockInsufficientDashboard) {
  await page.route('**/api/auth/me', route => route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify(mockUser) }));
  await page.route('**/api/dashboard', route => route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify(dashboard) }));
  await page.route('**/api/forecast', route => route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify(forecast) }));
  await page.route('**/api/activity**', route => route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify(mockActivity) }));
  await page.route('**/api/expenses', route => route.fulfill({ status: 200, contentType: 'application/json', body: '[]' }));
}

const mockActiveForecast = {
  method: 'Holt-Winters',
  horizon: 6,
  ok: true,
  status: 'live',
  accuracy: 94,
  metrics: { mae: 8500, rmse: 11200, mape: 5.8, sampleSize: 28 },
  dataSource: {
    status: 'live',
    usingLiveRecords: true,
    usingSample: false,
    label: 'Live booking history',
    liveMonthsAvailable: 28,
    recordedMonths: 28,
    minimumMonths: 24,
    filledMonths: 0,
    lastRecordedMonth: 'September 2026'
  },
  history: [
    { label: 'M-1', month: 'Aug 26', value: 141000 },
    { label: 'M0', month: 'Sep 26', value: 145000 }
  ],
  steps: [
    { step: 1, month: 'Oct 26', monthLabel: 'October 2026', value: 148500, lower: 136000, upper: 161000 },
    { step: 2, month: 'Nov 26', monthLabel: 'November 2026', value: 153000, lower: 139000, upper: 167000 },
    { step: 3, month: 'Dec 26', monthLabel: 'December 2026', value: 168000, lower: 151000, upper: 185000 },
    { step: 4, month: 'Jan 27', monthLabel: 'January 2027', value: 159000, lower: 140000, upper: 178000 },
    { step: 5, month: 'Feb 27', monthLabel: 'February 2027', value: 156000, lower: 135000, upper: 177000 },
    { step: 6, month: 'Mar 27', monthLabel: 'March 2027', value: 182000, lower: 158000, upper: 206000 }
  ],
  insight: {
    direction: 'up',
    changePercent: 8.4,
    peakMonth: 'March 2027',
    peakValue: 182000,
    notes: [
      { kind: 'direction', text: 'Booking revenue is trending upward, projected +8.4% over the next 6 months, peaking in March 2027 at ₱182,000.' },
      { kind: 'capacity', text: 'March 2027 runs 18.2% above the 6-month average, pointing to peak seasonal revenue.' },
      { kind: 'reliability', text: 'Typical monthly error is 5.8%, so figures have stayed close on recorded months.' }
    ]
  }
};

function waitForServer(url, timeoutMs = 20000) {
  const start = Date.now();
  return new Promise((resolve, reject) => {
    function ping() {
      http.get(url, (res) => {
        resolve();
      }).on('error', () => {
        if (Date.now() - start > timeoutMs) {
          reject(new Error(`Server at ${url} did not start within ${timeoutMs}ms`));
        } else {
          setTimeout(ping, 300);
        }
      });
    }
    ping();
  });
}

test('Home forecast, insufficient history and mobile UX', async (t) => {
  let serverProcess = null;
  let browser = null;

  t.before(async () => {
    // Start vite dev server on test port
    serverProcess = spawn('npx', ['vite', '--port', String(PORT), '--strictPort'], {
      shell: true,
      cwd: __dirname + '/..',
      stdio: 'pipe'
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

  await t.test('1. Home with ok:false shows one truthful status panel and no stale forecast claims', async () => {
    const page = await browser.newPage({ viewport: { width: 1440, height: 900 } });
    await routeHome(page, mockInsufficientForecastWithStaleNotes);

    await page.goto(`${BASE_URL}/home`);
    await page.waitForSelector('.forecast-unavailable');

    assert.equal(await page.locator('.forecast-unavailable').count(), 1, 'Exactly one forecast status panel');
    assert.equal(await page.locator('.forecast-chart').count(), 0, 'No chart when data.ok is false');
    assert.equal(await page.locator('.step-panel, #forecast-details').count(), 0, 'No monthly breakdown when data.ok is false');

    const fullText = await page.innerText('body');
    assert.ok(!fullText.includes('March 2027'), 'Must not mention March 2027 from the stale insight payload');
    assert.ok(!fullText.includes('trending upward'), 'Must not mention a trend from the stale insight payload');
    assert.ok(!fullText.includes('5.8%'), 'Must not mention accuracy or MAPE figures from the stale payload');
    assert.ok(!fullText.includes('peak seasonal revenue'), 'Must not render observation notes from the stale payload');

    const panelText = await page.locator('.forecast-unavailable').innerText();
    assert.ok(panelText.includes('Forecast unavailable'), 'States that the forecast is unavailable');
    assert.ok(panelText.includes('8 of 24 months recorded'), 'Shows recorded months against the 24-month requirement');
    assert.ok(panelText.includes('16 more months are needed'), 'Shows how many months are still needed');
    assert.ok(!panelText.includes('Record bookings'), 'No call to action implying one booking unlocks forecasting');
    assert.equal(await page.locator('.forecast-unavailable progress, .forecast-unavailable .progress-bar').count(), 0, 'No progress bar');

    const bookingsLink = page.locator('.forecast-unavailable a[href="/bookings"]');
    assert.equal(await bookingsLink.count(), 1, 'Links to the booking records');
    assert.ok((await bookingsLink.innerText()).includes('View booking records'));
    assert.equal(await page.locator('a[href="/forecast"]').count(), 0, 'No links to the retired forecast page');

    // The rest of Home still works without a forecast.
    assert.ok(await page.locator('#month-title').isVisible(), 'This month so far is still shown');
    assert.ok(await page.locator('#attention-title').isVisible(), 'Needs attention is still shown');
    assert.equal(await page.locator('.feed-item').count(), mockActivity.length, 'Recent activity lists the log entries');
    assert.ok((await page.locator('.feed-item').first().innerText()).includes('voided booking BKG-2597'));
    assert.ok(await page.locator('.live-status').isVisible(), 'Recent activity shows its live status');

    await page.close();
  });

  await t.test('2. The old /forecast address opens Home with the monthly figures expanded', async () => {
    const page = await browser.newPage({ viewport: { width: 1440, height: 900 } });
    await routeHome(page, mockActiveForecast);

    await page.goto(`${BASE_URL}/forecast`);
    await page.waitForURL('**/home#forecast-details');
    await page.waitForSelector('#forecast-details[open] .step-panel');

    assert.ok(await page.locator('.step-panel').isVisible(), 'Monthly breakdown is visible');
    const detailsText = await page.locator('#forecast-details').innerText();
    assert.ok(detailsText.includes('Fit on recorded months'), 'Model figures are labelled as fit on recorded months');
    assert.ok(!detailsText.includes('Model accuracy'), 'No "Model accuracy" claim');
    assert.ok(detailsText.includes('not a test on months it has not seen'), 'States the limit of the fit figures');

    await page.close();
  });

  await t.test('3. Mobile Home leads with the chart and recommendations, and monthly figures open in place', async () => {
    const page = await browser.newPage({ viewport: { width: 390, height: 844 } });
    await routeHome(page, mockActiveForecast);

    await page.goto(`${BASE_URL}/home`);
    await page.waitForSelector('.overview-tool');
    await page.waitForSelector('.forecast-chart');

    assert.ok(await page.locator('.overview-title').isVisible(), 'Forecast heading is visible on mobile Home');
    assert.ok(await page.locator('.rec-action-heading').first().isVisible(), 'Recommendation heading is visible on mobile Home');

    const chartBox = await page.locator('.forecast-chart').boundingBox();
    assert.ok(chartBox !== null && chartBox.y < 600, `Chart should start high on an 844px screen (y=${chartBox && chartBox.y})`);

    const detailsLink = page.locator('.overview-header-actions a[href="#forecast-details"]');
    assert.equal(await detailsLink.count(), 1, 'Forecast card links to the monthly figures');
    assert.equal(await page.locator('#forecast-details[open]').count(), 0, 'Monthly figures start closed');
    await detailsLink.click();
    await page.waitForSelector('#forecast-details[open] .step-panel');
    assert.ok(await page.locator('.step-panel').isVisible(), 'Monthly breakdown opens in place');
    assert.ok(await page.evaluate(() => document.documentElement.scrollWidth <= window.innerWidth), 'Opening the details must not cause sideways scrolling');

    await page.close();
  });

  await t.test('4. Account footer remains reachable on desktop and mobile, and logout recovers from errors', async () => {
    const page = await browser.newPage();
    let signedOut = false;
    let failLogout = true;
    await page.route('**/api/auth/me', route => route.fulfill({
      status: signedOut ? 401 : 200,
      contentType: 'application/json',
      body: JSON.stringify(signedOut ? { message: 'Unauthorized' } : mockUser)
    }));
    await page.route('**/api/dashboard', route => route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify(mockInsufficientDashboard) }));
    await page.route('**/api/forecast', route => route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify(mockActiveForecast) }));
    await page.route('**/api/activity**', route => route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify(mockActivity) }));
    await page.route('**/api/expenses', route => route.fulfill({ status: 200, contentType: 'application/json', body: '[]' }));
    await page.route('**/api/auth/logout', route => {
      if (failLogout) return route.fulfill({ status: 503, contentType: 'application/json', body: '{"message":"Unavailable"}' });
      signedOut = true;
      return route.fulfill({ status: 204 });
    });

    const screenshotDir = process.env.PROPHETOPS_SCREENSHOT_DIR;
    if (screenshotDir) await fs.mkdir(screenshotDir, { recursive: true });
    for (const viewport of [{ width: 1440, height: 900 }, { width: 390, height: 844 }, { width: 320, height: 568 }]) {
      await page.setViewportSize(viewport);
      await page.goto(`${BASE_URL}/home`);
      await page.waitForSelector('.overview-tool');
      const narrow = viewport.width <= 1024;
      // Narrow screens move navigation to the tab bar and keep the account in a sheet opened from the header.
      assert.equal(await page.locator('.tab-bar').isVisible(), narrow, `Tab bar visibility at ${viewport.width}px`);
      if (narrow) await page.getByRole('button', { name: 'Account' }).click();
      if (narrow) await page.waitForFunction(() => {
        const box = document.querySelector('.sidebar-account').getBoundingClientRect();
        return box.top >= 0 && box.bottom <= window.innerHeight + 1;
      });
      const footer = page.locator('.sidebar-account');
      const footerBox = await footer.boundingBox();
      assert.ok(footerBox && footerBox.x >= 0 && footerBox.y >= 0 && footerBox.y + footerBox.height <= viewport.height + 1, `Footer outside ${viewport.width}px viewport: ${JSON.stringify(footerBox)}`);
      assert.equal(await footer.locator('.sidebar-account-name').innerText(), mockUser.name);
      assert.equal(await footer.locator('.sidebar-account-role').innerText(), 'Owner');
      assert.equal(await page.locator('.page-header').getByRole('button', { name: 'Log out' }).count(), 0);
      assert.ok(await footer.getByRole('button', { name: 'Log out', exact: true }).isVisible());
      assert.ok(await footer.getByRole('link', { name: 'Team & access' }).isVisible(), 'Owner must reach Team & access from the account area');
      assert.ok(await page.evaluate(() => document.documentElement.scrollWidth <= window.innerWidth), 'Page must not overflow horizontally');
      const figures = page.locator('.score-grid .score-value');
      assert.equal(await figures.count(), 3, 'This month so far shows three figures');
      assert.ok(await figures.evaluateAll(values => values.every(value => {
        const range = document.createRange();
        range.selectNodeContents(value);
        return range.getBoundingClientRect().height < parseFloat(getComputedStyle(value).lineHeight) * 1.5;
      })), 'Currency figures must stay on one line');
      if (screenshotDir) await page.screenshot({ path: path.join(screenshotDir, `home-${viewport.width}.png`), fullPage: true });
      if (narrow) await page.keyboard.press('Escape');
    }

    const months = page.locator('.month-target');
    await months.first().focus();
    await page.keyboard.press('ArrowRight');
    await page.waitForFunction(() => document.activeElement === document.querySelectorAll('.month-target')[1]);
    await page.keyboard.press('ArrowRight');
    await page.waitForFunction(() => document.activeElement === document.querySelectorAll('.month-target')[2]);
    const tooltip = page.locator('.chart-tooltip-group');
    const tooltipBox = await tooltip.boundingBox();
    const chartBox = await page.locator('.chart-tooltip-group').locator('..').boundingBox();
    assert.ok(tooltipBox && chartBox && tooltipBox.x >= chartBox.x && tooltipBox.x + tooltipBox.width <= chartBox.x + chartBox.width + 1, 'Monthly preview must fit inside the chart on mobile');

    await page.setViewportSize({ width: 390, height: 844 });
    await page.goto(`${BASE_URL}/home#forecast-details`);
    await page.waitForSelector('#forecast-details[open] .step-panel');
    if (screenshotDir) await page.screenshot({ path: path.join(screenshotDir, 'home-details-390.png'), fullPage: true });
    await page.getByRole('button', { name: 'Account' }).click();
    await page.locator('.sidebar-logout').click();
    await page.waitForSelector('.sidebar-account-error');
    assert.equal(await page.locator('.sidebar-account-error').innerText(), 'Unable to log out. Please try again.');
    assert.ok(await page.locator('.sidebar-logout').isEnabled());
    failLogout = false;
    await page.locator('.sidebar-logout').click();
    await page.waitForURL('**/login');
    assert.equal(signedOut, true);
    assert.equal(await page.locator('.sidebar-account').count(), 0);
    await page.close();
  });
});
