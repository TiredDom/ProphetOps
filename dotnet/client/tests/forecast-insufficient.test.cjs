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
  defaultPath: '/dashboard'
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
  lowStockPackages: [],
  pendingPayments: { count: 0, amount: 0 },
  recentBookings: [],
  lastUpdated: 'Oct 2, 2026'
};

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

test('Forecast & Dashboard Insufficient Data & Mobile UX Test Suite', async (t) => {
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

  await t.test('1. Forecast page with ok:false does NOT render stale insight claims, March 2027, or accuracy figures', async () => {
    const page = await browser.newPage({ viewport: { width: 1440, height: 900 } });
    await page.route('**/api/auth/me', route => route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify(mockUser) }));
    await page.route('**/api/forecast', route => route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify(mockInsufficientForecastWithStaleNotes) }));

    await page.goto(`${BASE_URL}/forecast`);
    await page.waitForSelector('.insufficient-full-panel');

    // 1. Must NOT render .forecast-brief or .brief-up/.brief-down
    const briefCount = await page.locator('.forecast-brief').count();
    assert.equal(briefCount, 0, 'Must not render .forecast-brief when data.ok is false');

    // 2. Must NOT render .forecast-chart
    const chartCount = await page.locator('.forecast-chart').count();
    assert.equal(chartCount, 0, 'Must not render .forecast-chart when data.ok is false');

    // 3. Must NOT render .step-panel
    const stepCount = await page.locator('.step-panel').count();
    assert.equal(stepCount, 0, 'Must not render monthly step breakdown when data.ok is false');

    // 4. Must NOT leak stale insight content into the page text
    const fullText = await page.innerText('body');
    assert.ok(!fullText.includes('March 2027'), 'Page must not mention March 2027 from stale insight payload');
    assert.ok(!fullText.includes('trending upward'), 'Page must not mention trend direction from stale insight payload');
    assert.ok(!fullText.includes('5.8%'), 'Page must not mention accuracy or MAPE metrics from stale insight payload');
    assert.ok(!fullText.includes('peak seasonal revenue'), 'Page must not render observation notes from stale insight payload');
    assert.ok(!fullText.includes('heavy seasonal demand'), 'Page must not contain heavy seasonal demand text');

    // 5. Must render clean insufficient panel with accurate month counts and navigation
    assert.ok(fullText.includes('8 of 24 recorded months'), 'Must show actual recorded-month count vs 24-month requirement');
    assert.ok(fullText.includes('Not enough history for a revenue forecast'), 'Must show headline requirement');
    assert.ok(fullText.includes('16 more months needed'), 'Must show remaining months needed');

    const bookingsLink = page.locator('.insufficient-side a[href="/bookings"]');
    assert.equal(await bookingsLink.count(), 1, 'Must render link to bookings on the right side');
    const bookingsLinkText = await bookingsLink.innerText();
    assert.ok(bookingsLinkText.includes('View booking records'), 'Link text must be "View booking records"');
    assert.ok(!bookingsLinkText.includes('record activity'), 'Must not imply one booking will unlock forecasting');
    const reportsLink = page.locator('.insufficient-how-it-works a[href="/reports"]');
    assert.equal(await reportsLink.count(), 1, 'Must render link to reports in technical explanation');

    await page.close();
  });

  await t.test('2. Dashboard revenue outlook with ok:false is simplified and has no nested box or CTAs', async () => {
    const page = await browser.newPage({ viewport: { width: 1440, height: 900 } });
    await page.route('**/api/auth/me', route => route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify(mockUser) }));
    await page.route('**/api/dashboard', route => route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify(mockInsufficientDashboard) }));
    await page.route('**/api/forecast', route => route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify(mockInsufficientForecastWithStaleNotes) }));

    await page.goto(`${BASE_URL}/dashboard`);
    await page.waitForSelector('.revenue-unavailable-panel');

    // 1. Must render flat .revenue-unavailable-panel
    const unavailablePanel = page.locator('.revenue-unavailable-panel');
    assert.equal(await unavailablePanel.count(), 1, 'Must render revenue-unavailable-panel');

    // 2. Must state forecast unavailable and show recorded count vs 24
    const panelText = await unavailablePanel.innerText();
    assert.ok(panelText.includes('Forecast unavailable'), 'Must show short Forecast unavailable heading');
    assert.ok(panelText.includes('8 of 24 required months'), 'Must show actual recorded-month count vs 24');

    // 3. Must have exactly one link to forecast requirements
    const forecastLinks = page.locator('.revenue-panel a[href="/forecast"]');
    assert.equal(await forecastLinks.count(), 1, 'Must have exactly one link to forecast requirements');
    const linkText = await forecastLinks.first().innerText();
    assert.ok(linkText.includes('View forecast requirements'), 'Link text must point to forecast requirements');

    // 4. Must NOT have nested gray box, progress bar, or "Record bookings" CTA
    const nestedBox = await page.locator('.insufficient-data-box').count();
    assert.equal(nestedBox, 0, 'Must remove .insufficient-data-box');
    const progressBar = await page.locator('.progress-bar, progress, .insufficient-progress-box, .insufficient-progress-bar').count();
    assert.equal(progressBar, 0, 'Must remove progress bar from dashboard revenue panel');
    assert.ok(!panelText.includes('Record bookings'), 'Must remove Record bookings CTA');

    await page.close();
  });

  await t.test('3. Mobile dashboard overview leads with recommendations, and forecast page shows breakdown', async () => {
    const page = await browser.newPage({ viewport: { width: 390, height: 844 } });
    await page.route('**/api/auth/me', route => route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify(mockUser) }));
    await page.route('**/api/dashboard', route => route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify(mockInsufficientDashboard) }));
    await page.route('**/api/forecast', route => route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify(mockActiveForecast) }));
    await page.route('**/api/expenses', route => route.fulfill({ status: 200, contentType: 'application/json', body: '[]' }));

    // 1. Dashboard: overview tool is the main highlight and leads on mobile
    await page.goto(`${BASE_URL}/dashboard`);
    await page.waitForSelector('.overview-tool');
    await page.waitForSelector('.forecast-chart');

    const overviewTitle = page.locator('.overview-title');
    assert.ok(await overviewTitle.isVisible(), 'Overview heading must be visible on mobile dashboard');

    const recHeading = page.locator('.rec-action-heading');
    assert.ok(await recHeading.first().isVisible(), 'Action recommendation heading must be visible on mobile dashboard');

    const chartBox = await page.locator('.forecast-chart').boundingBox();
    assert.ok(chartBox !== null, 'Chart bounding box must exist on dashboard');
    assert.ok(chartBox.y < 600, `Chart Y-position (${chartBox.y}px) should appear high up on 844px mobile screen`);

    // Verify link to forecast exists in dashboard overview
    const viewFullLink = page.locator('.overview-header-actions a[href="/forecast"]');
    assert.equal(await viewFullLink.count(), 1, 'Dashboard overview must include link to full forecast');

    // 2. Forecast page: acts as detailed analysis destination with visible breakdown table
    await page.goto(`${BASE_URL}/forecast`);
    await page.waitForSelector('.forecast-chart');
    await page.waitForSelector('.step-panel');

    const stepPanel = page.locator('.step-panel');
    assert.ok(await stepPanel.isVisible(), 'Monthly forecast breakdown table must be directly visible on forecast page');

    const insufficientCount = await page.locator('.insufficient-full-panel').count();
    assert.equal(insufficientCount, 0, 'Must not render insufficient panel when data.ok is true');

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
      await page.goto(`${BASE_URL}/dashboard`);
      await page.waitForSelector('.overview-tool');
      const narrow = viewport.width <= 1024;
      if (narrow) await page.getByRole('button', { name: 'Toggle navigation' }).click();
      if (narrow) await page.waitForFunction(() => document.querySelector('.sidebar-account').getBoundingClientRect().x >= 0);
      const footer = page.locator('.sidebar-account');
      const footerBox = await footer.boundingBox();
      assert.ok(footerBox && footerBox.x >= 0 && footerBox.y >= 0 && footerBox.y + footerBox.height <= viewport.height + 1, `Footer outside ${viewport.width}px viewport: ${JSON.stringify(footerBox)}`);
      assert.equal(await footer.locator('.sidebar-account-name').innerText(), mockUser.name);
      assert.equal(await footer.locator('.sidebar-account-role').innerText(), 'Owner');
      assert.equal(await page.locator('.topbar .profile-button, .topbar .topbar-logout').count(), 0);
      assert.ok(await footer.getByRole('button', { name: 'Log out', exact: true }).isVisible());
      assert.ok(await page.evaluate(() => document.documentElement.scrollWidth <= window.innerWidth), 'Page must not overflow horizontally');
      assert.ok(await page.locator('.dashboard-totals .stat-value').evaluateAll(values => values.every(value => {
        const range = document.createRange();
        range.selectNodeContents(value);
        return range.getBoundingClientRect().height < parseFloat(getComputedStyle(value).lineHeight) * 1.5;
      })), 'Dashboard currency totals must stay on one line');
      if (screenshotDir) await page.screenshot({ path: path.join(screenshotDir, `dashboard-${viewport.width}.png`), fullPage: true });
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
    await page.goto(`${BASE_URL}/forecast`);
    await page.waitForSelector('.step-panel');
    if (screenshotDir) await page.screenshot({ path: path.join(screenshotDir, 'forecast-390.png'), fullPage: true });
    await page.getByRole('button', { name: 'Toggle navigation' }).click();
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
