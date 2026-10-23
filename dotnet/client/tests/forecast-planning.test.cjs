const test = require('node:test');
const assert = require('node:assert/strict');
const { chromium } = require('playwright');
const { spawn, spawnSync } = require('node:child_process');
const http = require('node:http');

const PORT = 5182;
const BASE_URL = `http://localhost:${PORT}`;

const owner = { name: 'Renan & Tina', email: 'owner@prophetops.com', role: 'Owner / Management', defaultPath: '/home' };
const staff = { name: 'Casey Staff', email: 'staff@prophetops.com', role: 'Staff', defaultPath: '/bookings' };

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

// The revenue forecast the scenario starts from, October 2026 to March 2027.
const months = ['October 2026', 'November 2026', 'December 2026', 'January 2027', 'February 2027', 'March 2027'];
const revenue = [148500, 153000, 168000, 159000, 156000, 182000];
const revenueLower = [136000, 139000, 151000, 140000, 135000, 158000];
const revenueUpper = [161000, 167000, 185000, 178000, 177000, 206000];
const spentLastYear = [90000, 95000, 160000, 70000, 60000, 85000];
const sum = (values) => values.reduce((total, value) => total + value, 0);

/** The server's arithmetic: spending as a share of the forecast, or last year's spending month by month. */
function scenario(basis, share, extra = {}) {
  const rows = months.map((month, i) => {
    const costs = basis === 'share' ? Math.round(revenue[i] * share) : spentLastYear[i];
    const keep = (value) => (basis === 'share' ? Math.round(value * (1 - share)) : value - costs);
    return {
      month,
      revenue: revenue[i],
      revenueLower: revenueLower[i],
      revenueUpper: revenueUpper[i],
      costs,
      afterCosts: keep(revenue[i]),
      afterCostsLower: keep(revenueLower[i]),
      afterCostsUpper: keep(revenueUpper[i]),
    };
  });
  return {
    ok: true,
    reason: null,
    basis,
    share: basis === 'share' ? share : null,
    recordedShare: 0.4188,
    basisPeriod: { from: 'October 2025', to: 'September 2026', revenue: 1800000, costs: 753840 },
    passedMonths: [],
    months: rows,
    total: {
      revenue: sum(rows.map((r) => r.revenue)),
      costs: sum(rows.map((r) => r.costs)),
      afterCosts: sum(rows.map((r) => r.afterCosts)),
      afterCostsLower: sum(rows.map((r) => r.afterCostsLower)),
      afterCostsUpper: sum(rows.map((r) => r.afterCostsUpper)),
    },
    dataSource: live,
    ...extra,
  };
}

function pkg(id, packageName, status, availableSlots, soldCount, basePrice) {
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
    reservedCount: 0,
    status,
    imageUrl: null,
  };
}

function freshPackages() {
  return [
    pkg('PKG-1', 'Alpha Escape', 'Normal', 20, 10, 10000),
    pkg('PKG-2', 'Beta Retreat', 'Low', 4, 30, 18000),
    pkg('PKG-3', 'Gamma Tour', 'Critical', 1, 40, 12000),
    pkg('PKG-4', 'Delta Cruise', 'Normal', 50, 5, 30000),
  ];
}

function demand(code, name, openSlots, passengersInBasis, share, expected, status, price = null) {
  return { code, name, destination: 'Palawan', openSlots, passengersInBasis, share, expected, status, price };
}

// Over the next three months: Alpha's low end is over its 20 open slots, Beta's high end is over
// its 4, Gamma had no passengers in the twelve months, and Delta's high end fills under half of 50.
const outlook = {
  ok: true,
  reason: null,
  monthsAhead: 3,
  months: ['October 2026', 'November 2026', 'December 2026'],
  passedMonths: [],
  basisPeriod: { from: 'October 2025', to: 'September 2026', passengers: 400 },
  agency: { value: 474, lower: 426, upper: 513 },
  packages: [
    demand('PKG-1', 'Alpha Escape', 20, 20, 0.05, { value: 23.7, lower: 21.3, upper: 25.7 }, 'likely-short', {
      kind: 'raise',
      currentPrice: 10000,
      suggestedPrice: 10500,
      changePercent: 5,
      reason: 'About 21 to 26 passengers are expected from October 2026 to December 2026, more than its 20 open slots even at the low end.',
    }),
    demand('PKG-2', 'Beta Retreat', 4, 4, 0.009, { value: 4.3, lower: 3.8, upper: 4.6 }, 'could-short'),
    demand('PKG-4', 'Delta Cruise', 50, 16, 0.04, { value: 19, lower: 17, upper: 20.5 }, 'enough', {
      kind: 'offer',
      currentPrice: 30000,
      suggestedPrice: 28500,
      changePercent: -5,
      reason: 'At most about 21 passengers are expected from October 2026 to December 2026, under half of its 50 open slots.',
    }),
    demand('PKG-3', 'Gamma Tour', 1, 0, 0, { value: 0, lower: 0, upper: 0 }, 'no-history'),
  ],
  dataSource: live,
};

const passengerForecast = {
  method: 'Holt-Winters',
  measure: 'passengers',
  unit: 'passengers',
  seasonLength: 12,
  horizon: 6,
  ok: true,
  accuracy: 92,
  params: { alpha: 0.3, beta: 0.05, gamma: 0.2 },
  metrics: { mae: 9, rmse: 12, mape: 6.1, sampleSize: 25 },
  baselines: { seasonalNaiveMae: 14, naiveMae: 19 },
  dataSource: live,
  history: [{ label: 'M0', month: 'Sep', value: 150 }],
  steps: months.map((monthLabel, i) => ({
    step: i + 1,
    month: monthLabel.slice(0, 3),
    monthLabel,
    value: [150, 155, 169, 160, 158, 184][i],
    lower: [136, 140, 152, 141, 136, 160][i],
    upper: [164, 170, 186, 179, 180, 208][i],
  })),
  insight: { direction: 'up', changePercent: 8, peakMonth: 'March 2027', peakValue: 184, notes: [] },
};

const expenses = [
  { id: 'EXP-1', backendId: 1, date: '2026-10-03', category: 'Tour operations', amount: 300000, relatedPackage: 'Office operations', paymentStatus: 'Paid', notes: null, voided: false, voidReason: null },
  { id: 'EXP-2', backendId: 2, date: '2026-10-06', category: 'Marketing', amount: 50000, relatedPackage: 'Office operations', paymentStatus: 'Pending', notes: null, voided: false, voidReason: null },
];

function json(route, body, status = 200) {
  return route.fulfill({ status, contentType: 'application/json', body: JSON.stringify(body) });
}

/**
 * The answers both pages need. `world.scenario(url)` and `world.outlook()` give the planning answers;
 * every request and write is recorded so a test can say what was asked.
 */
async function routePages(page, user, world) {
  const seen = { scenario: [], outlook: 0, writes: [] };
  // Anything not listed is a missing API, as the server answers it.
  await page.route('**/api/**', (route) => json(route, { message: 'There is no API at this address.' }, 404));
  await page.route('**/api/auth/me', (route) => json(route, user));
  await page.route('**/api/app/config', (route) => json(route, { today: '2026-10-13', timeZone: 'Asia/Manila' }));
  await page.route('**/api/activity**', (route) => json(route, []));
  await page.route('**/api/expenses', (route) => json(route, expenses));
  await page.route('**/api/forecast?measure=passengers', (route) => json(route, passengerForecast));
  await page.route('**/api/forecast/scenario**', async (route) => {
    const url = new URL(route.request().url());
    seen.scenario.push(url.search);
    const answer = await world.scenario(url.searchParams);
    return answer.status ? json(route, answer.body, answer.status) : json(route, answer);
  });
  await page.route('**/api/forecast/packages**', async (route) => {
    seen.outlook += 1;
    const answer = await world.outlook(seen.outlook);
    return answer.status ? json(route, answer.body, answer.status) : json(route, answer);
  });
  await page.route('**/api/inventory**', (route) => {
    const request = route.request();
    if (request.method() === 'GET') return json(route, world.packages);
    const body = request.postDataJSON();
    seen.writes.push({ method: request.method(), path: new URL(request.url()).pathname, body });
    const saved = world.packages.find((p) => p.id === body.id);
    Object.assign(saved, body, { revision: saved.revision + 1 });
    return json(route, { ...saved });
  });
  return seen;
}

function scenarioWorld() {
  return {
    packages: freshPackages(),
    scenario: (params) => {
      const basis = params.get('basis') ?? 'share';
      const share = params.has('share') ? Number(params.get('share')) : 0.4188;
      return scenario(basis, share);
    },
    outlook: () => outlook,
  };
}

async function openScenario(page) {
  const panel = page.locator('details.scenario');
  await panel.locator('.summary-chip').waitFor();
  await panel.locator('summary').click();
  await panel.locator('.scenario-table').waitFor();
  return panel;
}

async function text(locator) {
  return (await locator.innerText()).replace(/\s+/g, ' ').trim();
}

async function until(check, timeoutMs = 4000) {
  const start = Date.now();
  while (!check()) {
    if (Date.now() - start > timeoutMs) throw new Error('Timed out waiting');
    await new Promise((resolve) => setTimeout(resolve, 50));
  }
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

test('Planning with the forecast: the cost scenario and package demand', async (t) => {
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

  await t.test('1. The scenario starts from the recorded share and states its assumption and range', async () => {
    const page = await browser.newPage({ viewport: { width: 1440, height: 900 } });
    const seen = await routePages(page, owner, scenarioWorld());

    await page.goto(`${BASE_URL}/expenses`);
    const panel = page.locator('details.scenario');
    await panel.locator('.summary-chip').waitFor();
    const expected = scenario('share', 0.4188);
    assert.equal(await text(panel.locator('.summary-chip')), `After costs about ₱${(expected.total.afterCosts / 1000).toFixed(1)}k, Oct 2026 to Mar 2027`);
    assert.deepEqual(seen.scenario, ['?basis=share'], 'The first answer uses the server’s recorded share');

    await panel.locator('summary').click();
    await panel.locator('.scenario-table').waitFor();
    assert.equal(await page.locator('#scenario-share').inputValue(), '41.9');
    assert.equal(await text(panel.locator('.share-note')), 'Spending took 41.9% of revenue from October 2025 to September 2026.');
    assert.equal(await panel.getByRole('button', { name: /Use the recorded/ }).count(), 0, 'No reset while the recorded share is in use');

    const lead = await text(panel.locator('.scenario-lead'));
    const peso = (value) => `₱${Math.round(value).toLocaleString('en-PH')}`;
    assert.match(lead, /^If spending keeps taking the 41\.9% of revenue it took from October 2025 to September 2026, the 6 months from October 2026 to March 2027 would leave about/);
    assert.ok(lead.includes(`${peso(expected.total.afterCosts)} after costs.`), lead);
    assert.ok(lead.includes(`${peso(expected.total.afterCostsLower)} to ${peso(expected.total.afterCostsUpper)}.`), lead);

    const rows = await panel.locator('.scenario-table tbody tr').allInnerTexts();
    assert.equal(rows.length, 6);
    const first = rows[0].split('\t').map((cell) => cell.trim().replace(/\s+/g, ' '));
    assert.equal(first[0], 'October 2026');
    assert.equal(first[1], '₱148,500');
    assert.equal(first[2], peso(expected.months[0].costs));
    assert.match(first[3], new RegExp(`^${peso(expected.months[0].afterCosts)}`));
    assert.equal(await panel.locator('td.model-figure').count(), 6, 'Forecast revenue is in the model’s teal');
    assert.match(await text(panel.locator('.scenario-note').last()), /A scenario, not a forecast/);
    await page.close();
  });

  await t.test('2. Typing a share asks again once typing stops, and a loss reads as one', async () => {
    const page = await browser.newPage({ viewport: { width: 1440, height: 900 } });
    const seen = await routePages(page, owner, scenarioWorld());
    await page.goto(`${BASE_URL}/expenses`);
    const panel = await openScenario(page);

    const share = page.locator('#scenario-share');
    await share.fill('');
    await share.pressSequentially('120', { delay: 40 });
    await until(() => seen.scenario.includes('?basis=share&share=1.2'));
    assert.deepEqual(seen.scenario, ['?basis=share', '?basis=share&share=1.2'], 'One request for the finished number, none per key');

    await panel.locator('.scenario-lead').filter({ hasText: 'If spending takes 120% of revenue' }).waitFor();
    const loss = panel.locator('.scenario-lead strong.loss');
    assert.match(await loss.innerText(), /^−₱/, 'A loss reads with the sign before the currency');
    assert.equal(await panel.locator('td.after.loss').count(), 6);

    await panel.getByRole('button', { name: 'Use the recorded 41.9%' }).click();
    await until(() => seen.scenario.filter((s) => s === '?basis=share').length === 2);
    await panel.locator('.scenario-lead').filter({ hasText: 'keeps taking the 41.9%' }).waitFor();
    await page.close();
  });

  await t.test('3. A share out of range is named beside the box and not sent', async () => {
    const page = await browser.newPage({ viewport: { width: 1440, height: 900 } });
    const seen = await routePages(page, owner, scenarioWorld());
    await page.goto(`${BASE_URL}/expenses`);
    const panel = await openScenario(page);

    await page.locator('#scenario-share').fill('250');
    await panel.locator('.share-note.problem').waitFor();
    assert.equal(await text(panel.locator('.share-note')), 'Use a share from 0 to 200%.');
    assert.equal(await page.locator('#scenario-share').getAttribute('aria-invalid'), 'true');
    await page.waitForTimeout(600);
    assert.deepEqual(seen.scenario, ['?basis=share'], 'Nothing is asked for a share out of range');
    await page.close();
  });

  await t.test('4. Repeating last year’s spending drops the share and says so', async () => {
    const page = await browser.newPage({ viewport: { width: 1440, height: 900 } });
    const seen = await routePages(page, owner, scenarioWorld());
    await page.goto(`${BASE_URL}/expenses`);
    const panel = await openScenario(page);

    await panel.getByLabel('repeats the same month last year').check();
    await until(() => seen.scenario.includes('?basis=last-year'));
    await panel.locator('.scenario-lead').filter({ hasText: 'If spending repeats what was spent in the same month a year earlier' }).waitFor();
    assert.equal(await page.locator('#scenario-share').count(), 0);
    const first = (await panel.locator('.scenario-table tbody tr').first().innerText()).split('\t').map((c) => c.trim());
    assert.equal(first[2], '₱90,000');
    await page.close();
  });

  await t.test('5. Months already passed are left out and named; a scenario that cannot be made says why', async () => {
    const page = await browser.newPage({ viewport: { width: 1440, height: 900 } });
    const world = scenarioWorld();
    world.scenario = () => scenario('share', 0.4188, { passedMonths: ['September 2026'] });
    await routePages(page, owner, world);
    await page.goto(`${BASE_URL}/expenses`);
    const panel = await openScenario(page);
    assert.match(await text(panel.locator('.scenario-note').first()), /The forecast also covers September 2026, which has already passed and is left out\./);
    await page.close();

    const short = await browser.newPage({ viewport: { width: 1440, height: 900 } });
    const shortWorld = scenarioWorld();
    shortWorld.scenario = () => ({
      ...scenario('share', 0.4188),
      ok: false,
      reason: 'The forecast needs two full years of recorded bookings before it can be planned with.',
      months: [],
      total: null,
    });
    await routePages(short, owner, shortWorld);
    await short.goto(`${BASE_URL}/expenses`);
    const shortPanel = short.locator('details.scenario');
    await shortPanel.locator('summary').click();
    await shortPanel.getByText('The forecast needs two full years of recorded bookings').waitFor();
    assert.equal(await shortPanel.locator('table').count(), 0);
    assert.equal(await shortPanel.locator('.summary-chip').count(), 0);
    await short.close();
  });

  await t.test('6. Packages that may run short are counted, flagged and listed on request', async () => {
    const page = await browser.newPage({ viewport: { width: 1440, height: 900 } });
    await routePages(page, owner, scenarioWorld());
    await page.goto(`${BASE_URL}/packages`);

    const band = page.locator('.outlook-text');
    await band.getByText('run short of slots by December 2026').waitFor();
    assert.match(await text(band), /1 package is likely to run short of slots by December 2026, and 1 more could\./);
    assert.match(await text(band), /A package's part of it is estimated from its share of the passengers booked over the last 12 months\./);

    const chip = page.getByRole('button', { name: /May run short/ });
    assert.equal(await text(chip), 'May run short 2');
    assert.equal(await chip.getAttribute('aria-pressed'), 'false');

    const flags = await page.locator('.packages-table .demand-flag').allInnerTexts();
    assert.deepEqual(flags.map((f) => f.trim()), ['Likely to run short', 'Could run short']);

    // The low-on-slots line has its own Show them; this is the one on the demand line.
    const demandLine = band.locator('.outlook-line', { hasText: 'run short of slots' });
    await demandLine.getByRole('button', { name: 'Show them' }).click();
    await page.waitForURL(/demand=short/);
    const names = await page.locator('.packages-table .row-open').allInnerTexts();
    assert.deepEqual(names, ['Alpha Escape', 'Beta Retreat']);
    assert.equal(await chip.getAttribute('aria-pressed'), 'true');
    assert.match(await text(page.locator('.records-summary')), /May run short/);
    assert.equal(await demandLine.getByRole('button').count(), 0, 'The band’s button goes once the list is filtered');

    await page.getByRole('button', { name: 'Clear filters' }).click();
    assert.equal((await page.locator('.packages-table .row-open').allInnerTexts()).length, 4);
    await page.close();
  });

  await t.test('7. The address keeps the filter, and a package panel shows its expected demand', async () => {
    const page = await browser.newPage({ viewport: { width: 1440, height: 900 } });
    await routePages(page, owner, scenarioWorld());
    await page.goto(`${BASE_URL}/packages?demand=short`);
    await page.locator('.packages-table .demand-flag').first().waitFor();
    assert.deepEqual(await page.locator('.packages-table .row-open').allInnerTexts(), ['Alpha Escape', 'Beta Retreat']);

    await page.getByRole('button', { name: 'Beta Retreat' }).click();
    const panel = page.locator('.demand');
    await panel.waitFor();
    assert.match(await text(panel), /About 4 passengers are expected from October 2026 to December 2026 \(4 to 5\), going by its 0\.9% share of the passengers booked from October 2025 to September 2026\./);
    assert.equal(await text(panel.locator('.demand-verdict')), 'The high end is more than its 4 open slots: it could run short.');
    assert.equal(await panel.locator('.price-idea').count(), 0, 'No price is suggested when it could go either way');
    await page.close();
  });

  await t.test('8. A suggested price only fills in the form; saving it is the person’s step', async () => {
    const page = await browser.newPage({ viewport: { width: 1440, height: 900 } });
    const world = scenarioWorld();
    const seen = await routePages(page, owner, world);
    await page.goto(`${BASE_URL}/packages?open=PKG-1`);

    const panel = page.locator('.demand');
    await panel.locator('.price-idea').waitFor();
    assert.equal(await text(panel.locator('.demand-verdict')), 'That is more than its 20 open slots even at the low end: likely to run short.');
    assert.equal(await text(panel.locator('.price-line')), 'Price to consider: ₱10,500 +5% instead of ₱10,000.');
    assert.equal(seen.writes.length, 0);

    await panel.getByRole('button', { name: 'Use this price' }).click();
    const form = page.getByRole('dialog', { name: 'Edit package' });
    await form.waitFor();
    assert.equal(await form.getByLabel('Price per passenger').inputValue(), '10,500');
    assert.match(await text(form.locator('.drawer-form-merge')), /The suggested price is filled in\. Review it, then save; bookings already made keep their prices\./);
    assert.equal(seen.writes.length, 0, 'Nothing is saved by choosing the price');

    await form.getByRole('button', { name: 'Save changes' }).click();
    await until(() => seen.writes.length === 1);
    assert.equal(seen.writes[0].method, 'PUT');
    assert.equal(seen.writes[0].path, '/api/inventory/PKG-1');
    assert.equal(seen.writes[0].body.basePrice, 10500);
    assert.equal(seen.writes[0].body.availableSlots, 20, 'Only the price changes');
    await until(() => seen.outlook >= 2);
    await page.close();
  });

  await t.test('9. Choosing the price and then closing the form leaves the price as it was', async () => {
    const page = await browser.newPage({ viewport: { width: 1440, height: 900 } });
    const world = scenarioWorld();
    const seen = await routePages(page, owner, world);
    await page.goto(`${BASE_URL}/packages?open=PKG-4`);

    const panel = page.locator('.demand');
    await panel.locator('.price-idea').waitFor();
    assert.equal(await text(panel.locator('.demand-verdict')), 'Even the high end would fill no more than half of its 50 open slots.');
    assert.equal(await text(panel.locator('.price-line')), 'A limited offer to consider: ₱28,500 −5% instead of ₱30,000.');
    await panel.getByRole('button', { name: 'Use this price' }).click();

    const form = page.getByRole('dialog', { name: 'Edit package' });
    await form.waitFor();
    await form.getByRole('button', { name: 'Close panel' }).click();
    await form.getByRole('alertdialog').waitFor();
    await form.getByRole('button', { name: 'Discard changes' }).click();
    await page.locator('.demand .price-idea').waitFor();
    assert.equal(seen.writes.length, 0);
    assert.equal(world.packages.find((p) => p.id === 'PKG-4').basePrice, 30000);
    await page.close();
  });

  await t.test('10. Staff see no demand view, and nothing is asked for them', async () => {
    const page = await browser.newPage({ viewport: { width: 1440, height: 900 } });
    const seen = await routePages(page, staff, scenarioWorld());
    await page.goto(`${BASE_URL}/packages?demand=short`);
    await page.locator('.packages-table .row-open').first().waitFor();
    assert.equal((await page.locator('.packages-table .row-open').allInnerTexts()).length, 4, 'The filter does not hide packages from Staff');
    assert.equal(await page.getByRole('button', { name: /May run short/ }).count(), 0);
    assert.equal(await page.locator('.demand-flag').count(), 0);

    await page.getByRole('button', { name: 'Alpha Escape' }).click();
    await page.getByRole('dialog', { name: 'Package PKG-1' }).waitFor();
    assert.equal(await page.locator('.demand').count(), 0);
    assert.equal(seen.outlook, 0);
    await page.close();
  });

  await t.test('11. If the demand view fails, the packages still work without it', async () => {
    const page = await browser.newPage({ viewport: { width: 1440, height: 900 } });
    const world = scenarioWorld();
    world.outlook = () => ({ status: 500, body: {} });
    const seen = await routePages(page, owner, world);
    await page.goto(`${BASE_URL}/packages`);
    await page.locator('.packages-table .row-open').first().waitFor();
    await until(() => seen.outlook >= 1);
    await page.waitForTimeout(200);
    assert.equal(await page.getByRole('button', { name: /May run short/ }).count(), 0);
    assert.equal(await page.locator('.demand-flag').count(), 0);
    assert.equal(await page.locator('.outlook-text').getByText('run short of slots').count(), 0);
    assert.doesNotMatch(await text(page.locator('.outlook-note')), /share of the passengers/, 'The share is not mentioned without the demand view');
    await page.close();
  });

  await t.test('12. On a phone both fit the screen with short labels', async () => {
    const page = await browser.newPage({ viewport: { width: 390, height: 844 } });
    await routePages(page, owner, scenarioWorld());

    await page.goto(`${BASE_URL}/expenses`);
    const panel = await openScenario(page);
    const headings = (await panel.locator('thead th').allInnerTexts()).map((h) => h.trim());
    assert.deepEqual(headings, ['Month', 'Revenue', 'Spending', 'After costs']);
    assert.equal(await panel.locator('tbody th').first().innerText(), 'Oct 2026');
    const table = await panel.locator('.scenario-table').boundingBox();
    const frame = await panel.locator('.table-scroll').boundingBox();
    assert.ok(table.width <= frame.width + 1, `The table is ${table.width}px in a ${frame.width}px space`);
    let overflow = await page.evaluate(() => document.documentElement.scrollWidth - window.innerWidth);
    assert.ok(overflow <= 0, `Expenses scrolls sideways by ${overflow}px`);

    await page.goto(`${BASE_URL}/packages`);
    await page.locator('.record-list .demand-flag').first().waitFor();
    assert.equal(await page.locator('.record-list .demand-flag').count(), 2);
    overflow = await page.evaluate(() => document.documentElement.scrollWidth - window.innerWidth);
    assert.ok(overflow <= 0, `Packages scrolls sideways by ${overflow}px`);
    await page.close();
  });

  await t.test('13. On the narrowest phones the assumed spending folds under the revenue', async () => {
    const page = await browser.newPage({ viewport: { width: 320, height: 568 } });
    await routePages(page, owner, scenarioWorld());

    await page.goto(`${BASE_URL}/expenses`);
    const panel = await openScenario(page);
    const headings = (await panel.locator('thead th:visible').allInnerTexts()).map((h) => h.trim());
    assert.deepEqual(headings, ['Month', 'Revenue', 'After costs']);
    assert.equal(await text(panel.locator('tbody td.model-figure').first()), '₱148.5k spending ₱62.2k');
    const table = await panel.locator('.scenario-table').boundingBox();
    const frame = await panel.locator('.table-scroll').boundingBox();
    assert.ok(table.width <= frame.width + 1, `The table is ${table.width}px in a ${frame.width}px space`);
    const overflow = await page.evaluate(() => document.documentElement.scrollWidth - window.innerWidth);
    assert.ok(overflow <= 0, `Expenses scrolls sideways by ${overflow}px`);
    await page.close();
  });
});
