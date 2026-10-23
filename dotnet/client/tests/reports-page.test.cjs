const test = require('node:test');
const assert = require('node:assert/strict');
const { chromium } = require('playwright');
const { spawn, spawnSync } = require('node:child_process');
const http = require('node:http');

const PORT = 5177;
const BASE_URL = `http://localhost:${PORT}`;

const owner = { name: 'Renan & Tina', email: 'owner@prophetops.com', role: 'Owner / Management', defaultPath: '/home' };

function report(from, to, through) {
  return {
    totalsScope: from || to ? 'period' : 'lifetime',
    excludesVoided: true,
    period: { from, to, through },
    revenue: 5100,
    costs: 700,
    profit: 4400,
    counts: { bookings: 4, passengers: 8, packages: 3, expenses: 2, users: 4 },
    previous: from ? { from: '2025-01-01', to: '2025-10-13', revenue: 3500, costs: 400, profit: 3100, bookings: 3, passengers: 6 } : null,
    monthly: [
      { month: '2026-01', label: 'Jan 2026', revenue: 1500, costs: 0, afterCosts: 1500, bookings: 1, passengers: 2 },
      { month: '2026-02', label: 'Feb 2026', revenue: 0, costs: 600, afterCosts: -600, bookings: 0, passengers: 0 },
      { month: '2026-03', label: 'Mar 2026', revenue: 3600, costs: 100, afterCosts: 3500, bookings: 3, passengers: 6 },
    ],
    bookingsByStatus: [{ label: 'Confirmed', value: 4 }],
    bookingsByPayment: [{ label: 'Paid', value: 3, revenue: 4300 }, { label: 'Pending', value: 1, revenue: 800 }],
    expensesByCategory: [{ label: 'Transport', value: 700, count: 2 }],
    revenueByPackage: [{ label: 'Boracay Group Package', value: 5100, count: 4 }],
    revenueByDestination: [{ label: 'Boracay', value: 5100, count: 4 }],
    generatedAt: 'Oct 13, 2026',
  };
}

async function route(page) {
  const requests = [];
  const json = (body) => ({ status: 200, contentType: 'application/json', body: JSON.stringify(body) });
  await page.route('**/api/auth/me', (r) => r.fulfill(json(owner)));
  await page.route('**/api/app/config', (r) => r.fulfill(json({ today: '2026-10-13', timeZone: 'Asia/Manila' })));
  await page.route('**/api/forecast**', (r) => r.fulfill({ status: 404, contentType: 'application/json', body: '{}' }));
  await page.route('**/api/reports**', (r) => {
    const url = new URL(r.request().url());
    requests.push(url.search);
    const from = url.searchParams.get('from');
    const to = url.searchParams.get('to');
    r.fulfill(json(report(from, to, to && to > '2026-10-13' ? '2026-10-13' : to)));
  });
  return requests;
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

test('Reports', async (t) => {
  let serverProcess = null;
  let browser = null;

  t.before(async () => {
    serverProcess = spawn('npx', ['vite', '--port', String(PORT), '--strictPort'], { shell: true, cwd: __dirname + '/..', stdio: 'pipe' });
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

  await t.test('1. Reports opens on this year, compared with the same days last year', async () => {
    const page = await browser.newPage({ viewport: { width: 1440, height: 900 } });
    const requests = await route(page);
    await page.goto(`${BASE_URL}/reports`);
    await page.waitForSelector('.statement-table');

    assert.ok(requests.includes('?from=2026-01-01&to=2026-12-31'), JSON.stringify(requests));
    const period = squash(await page.locator('.report-period-text').innerText());
    assert.equal(period, 'Jan 1 – Oct 13, 2026, compared with Jan 1 – Oct 13, 2025');

    const revenueRow = squash(await page.locator('.statement-table tbody tr').first().innerText());
    assert.ok(revenueRow.includes('₱5,100') && revenueRow.includes('₱3,500') && revenueRow.includes('+45.7%'), revenueRow);
    assert.ok((await page.locator('.report-headline').innerText()).includes('Revenue is up 45.7%'));

    // A month that lost money reads with the sign before the peso.
    assert.ok((await page.locator('.month-table').innerText()).includes('−₱600'));
    assert.equal(
      await page.locator('a[href*="/api/export/report.csv"]').getAttribute('href'),
      '/api/export/report.csv?from=2026-01-01&to=2026-12-31',
    );
    await page.close();
  });

  await t.test('2. A different period is asked for, kept in the address, and exported', async () => {
    const page = await browser.newPage({ viewport: { width: 1440, height: 900 } });
    const requests = await route(page);
    await page.goto(`${BASE_URL}/reports`);
    await page.waitForSelector('.statement-table');

    await page.locator('.period-select select').selectOption('last-month');
    await page.waitForURL(/period=last-month/);
    await page.waitForFunction(() => document.querySelector('a[href*="report.csv"]')?.getAttribute('href')?.includes('2026-09-01'));
    assert.ok(requests.includes('?from=2026-09-01&to=2026-09-30'), JSON.stringify(requests));
    assert.equal(
      await page.locator('a[href*="/api/export/report.csv"]').getAttribute('href'),
      '/api/export/report.csv?from=2026-09-01&to=2026-09-30',
    );

    await page.reload();
    await page.waitForSelector('.statement-table');
    assert.equal(await page.locator('.period-select select').inputValue(), 'last-month');
    await page.close();
  });

  await t.test('3. The old Analytics address opens Reports', async () => {
    const page = await browser.newPage({ viewport: { width: 1440, height: 900 } });
    await route(page);
    await page.goto(`${BASE_URL}/analytics`);
    await page.waitForURL('**/reports');
    await page.waitForSelector('.statement-table');
    await page.close();
  });

  await t.test('4. Print hands the page to the browser, and phones see a narrow summary', async () => {
    const page = await browser.newPage({ viewport: { width: 390, height: 844 } });
    await route(page);
    await page.addInitScript(() => {
      window.__printed = 0;
      window.print = () => { window.__printed += 1; };
    });
    await page.goto(`${BASE_URL}/reports`);
    await page.waitForSelector('.statement-table');

    await page.getByRole('button', { name: 'Print' }).click();
    assert.equal(await page.evaluate(() => window.__printed), 1);

    const header = squash(await page.locator('.statement-table thead').innerText());
    assert.ok(header.includes('This period') && !header.includes('Change'), header);
    // The change and the earlier figure sit under this period's figure instead of in columns of their own.
    const revenueRow = squash(await page.locator('.statement-table tbody tr').first().innerText());
    assert.ok(revenueRow.includes('₱5,100') && revenueRow.includes('+45.7%') && revenueRow.includes('from ₱3.5k'), revenueRow);
    const overflow = await page.evaluate(() => document.documentElement.scrollWidth - window.innerWidth);
    assert.ok(overflow <= 0, `Scrolls sideways by ${overflow}px`);
    const tableOverflow = await page.evaluate(() => {
      const box = document.querySelector('.statement-table').parentElement;
      return box.scrollWidth - box.clientWidth;
    });
    assert.ok(tableOverflow <= 1, `The summary table is wider than its panel by ${tableOverflow}px`);
    await page.close();
  });

  await t.test('5. On the narrowest phones costs fold under revenue, and no table scrolls sideways', async () => {
    const page = await browser.newPage({ viewport: { width: 320, height: 568 } });
    await route(page);
    await page.goto(`${BASE_URL}/reports`);
    await page.waitForSelector('.month-table');

    const headings = (await page.locator('.month-table thead th:visible').allInnerTexts()).map((h) => h.trim());
    assert.deepEqual(headings, ['Month', 'Revenue', 'After costs']);
    const rows = await page.locator('.month-table tbody tr').allInnerTexts();
    assert.equal(squash(rows[1]), 'Feb 2026 ₱0 costs ₱600 −₱600');
    const total = squash(await page.locator('.month-table tfoot').innerText());
    assert.ok(total.includes('₱5.1k costs ₱700'), total);

    for (const table of ['.statement-table', '.month-table']) {
      const extra = await page.evaluate((selector) => {
        const box = document.querySelector(selector).parentElement;
        return box.scrollWidth - box.clientWidth;
      }, table);
      assert.ok(extra <= 1, `${table} is wider than its panel by ${extra}px`);
    }
    const overflow = await page.evaluate(() => document.documentElement.scrollWidth - window.innerWidth);
    assert.ok(overflow <= 0, `Scrolls sideways by ${overflow}px`);
    await page.close();
  });
});
