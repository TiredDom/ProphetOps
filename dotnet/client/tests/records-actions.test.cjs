const test = require('node:test');
const assert = require('node:assert/strict');
const { chromium } = require('playwright');
const { spawn, spawnSync } = require('node:child_process');
const http = require('node:http');

const PORT = 5178;
const BASE_URL = `http://localhost:${PORT}`;

const owner = { name: 'Renan & Tina', email: 'owner@prophetops.com', role: 'Owner / Management', defaultPath: '/home' };

function booking(id, ds, client, paymentStatus, grossRevenue, y, extra = {}) {
  return {
    id,
    backendId: Number(id.replace(/\D/g, '')),
    revision: 1,
    ds,
    y,
    client,
    package: 'Alpha Escape',
    packageId: 'PKG-1',
    entryType: 'Package preset',
    destination: 'Palawan',
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

/** A fresh, changeable copy of the records for each page, and every write the page sends. */
function world() {
  return {
    bookings: [
      booking('BKG-1002', '2026-10-05', 'Bravo Travel', 'Pending', 50000, 2),
      booking('BKG-1001', '2026-10-02', 'Alpha Tours', 'Paid', 100000, 4),
      booking('BKG-1003', '2026-09-20', 'Charlie Group', 'Partially Paid', 80000, 3, { bookingStatus: 'Reserved' }),
    ],
    packages: [
      pkg('PKG-1', 'Alpha Escape', 'Normal', 20, 10, 0, 10000),
      pkg('PKG-2', 'Beta Retreat', 'Low', 4, 30, 2, 18000),
      pkg('PKG-3', 'Gamma Tour', 'Critical', 1, 40, 1, 12000),
    ],
    expenses: [
      expense('EXP-2', '2026-10-06', 'Marketing', 50000, 'Pending'),
      expense('EXP-1', '2026-10-03', 'Tour operations', 300000, 'Paid'),
    ],
    users: [
      { name: 'Renan & Tina', email: 'owner@prophetops.com', role: 'Owner / Management', status: 'Active', lastLoginAt: '2026-10-13T01:00:00Z' },
      { name: 'Casey Staff', email: 'staff@prophetops.com', role: 'Staff', status: 'Active', lastLoginAt: null },
    ],
    writes: [],
    // Set by a test to answer the next booking save with a conflict.
    staleOnce: null,
  };
}

async function route(page, w) {
  const json = (body, status = 200) => ({ status, contentType: 'application/json', body: JSON.stringify(body) });
  await page.route('**/api/auth/me', (r) => r.fulfill(json(owner)));
  await page.route('**/api/app/config', (r) => r.fulfill(json({ today: '2026-10-13', timeZone: 'Asia/Manila' })));
  await page.route('**/api/forecast**', (r) => r.fulfill(json({}, 404)));
  await page.route('**/api/activity**', (r) => r.fulfill(json([])));
  await page.route('**/api/users/roles', (r) => r.fulfill(json([
    { name: 'Owner / Management', access: 'Full access, including managing accounts.' },
    { name: 'Admin', access: 'Everything except managing accounts.' },
    { name: 'Staff', access: 'Bookings and packages only.' },
  ])));

  await page.route('**/api/bookings**', async (r) => {
    const req = r.request();
    const url = new URL(req.url());
    if (req.method() === 'GET') return r.fulfill(json({ bookings: w.bookings, packages: w.packages }));
    const body = req.postDataJSON();
    w.writes.push({ method: req.method(), path: url.pathname, body });
    if (url.pathname.endsWith('/bulk')) {
      const changed = [];
      for (const id of body.ids) {
        const b = w.bookings.find((x) => x.id === id);
        if (body.action === 'paid') b.paymentStatus = 'Paid';
        else b.bookingStatus = 'Confirmed';
        b.revision += 1;
        changed.push({ ...b });
      }
      return r.fulfill(json({ updated: changed.length, bookings: changed }));
    }
    const code = decodeURIComponent(url.pathname.split('/').pop());
    const b = w.bookings.find((x) => x.id === code);
    if (req.method() === 'PUT') {
      if (w.staleOnce) {
        Object.assign(b, w.staleOnce, { revision: b.revision + 1 });
        w.staleOnce = null;
        return r.fulfill(json({ code: 'stale_revision', message: 'This record has changed. Reload it before saving again.' }, 409));
      }
      if (body.revision !== b.revision) return r.fulfill(json({ code: 'stale_revision', message: 'Stale' }, 409));
      Object.assign(b, body, { revision: b.revision + 1 });
      return r.fulfill(json({ ...b }));
    }
    return r.fulfill(json({}, 400));
  });

  await page.route('**/api/inventory**', (r) => {
    const req = r.request();
    if (req.method() === 'GET') return r.fulfill(json(w.packages));
    w.writes.push({ method: req.method(), path: new URL(req.url()).pathname, body: req.postDataJSON() });
    return r.fulfill(json({}, 400));
  });

  await page.route('**/api/expenses**', (r) => {
    const req = r.request();
    const url = new URL(req.url());
    if (req.method() === 'GET') return r.fulfill(json(w.expenses));
    const body = req.postDataJSON();
    w.writes.push({ method: req.method(), path: url.pathname, body });
    const code = decodeURIComponent(url.pathname.split('/')[3]);
    const e = w.expenses.find((x) => x.id === code);
    if (req.method() === 'PUT') Object.assign(e, body);
    return r.fulfill(json({ ...e }));
  });

  await page.route('**/api/users', (r) => r.fulfill(json(w.users)));
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

async function until(check, timeoutMs = 4000) {
  const start = Date.now();
  while (!check()) {
    if (Date.now() - start > timeoutMs) throw new Error('Timed out waiting');
    await new Promise((resolve) => setTimeout(resolve, 50));
  }
}

test('Record forms, panels, undo and bulk updates', async (t) => {
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

  await t.test('1. A form names each problem beside its field and keeps the draft until told to discard it', async () => {
    const w = world();
    const page = await browser.newPage({ viewport: { width: 1440, height: 900 } });
    await route(page, w);
    await page.goto(`${BASE_URL}/bookings`);
    await page.getByRole('button', { name: 'Add booking' }).click();
    const form = page.getByRole('dialog', { name: 'New booking' });
    await form.waitFor();

    await form.getByRole('button', { name: 'Save booking' }).click();
    const client = form.locator('#booking-client');
    assert.equal(await client.getAttribute('aria-invalid'), 'true');
    assert.equal(await page.evaluate(() => document.activeElement?.id), 'booking-client', 'Focus goes to the field to fix');
    const described = await client.getAttribute('aria-describedby');
    assert.ok((await page.locator(`#${described.split(' ')[0]}`).innerText()).includes('Enter the client'));
    assert.equal(w.writes.length, 0, 'Nothing is sent while a field is wrong');

    await client.fill('Foxtrot Travel');
    await page.waitForTimeout(50);
    assert.equal(await client.getAttribute('aria-invalid'), null, 'The message goes once the field is right');

    await page.keyboard.press('Escape');
    const discard = form.getByRole('alertdialog');
    await discard.waitFor();
    await discard.getByRole('button', { name: 'Keep editing' }).click();
    assert.equal(await client.inputValue(), 'Foxtrot Travel', 'Keep editing keeps the draft');

    await form.getByRole('button', { name: 'Cancel' }).click();
    await form.getByRole('button', { name: 'Discard changes' }).click();
    await form.waitFor({ state: 'detached' });
    await page.close();
  });

  await t.test('2. Taken IDs and full packages are caught before anything is sent', async () => {
    const w = world();
    const page = await browser.newPage({ viewport: { width: 1440, height: 900 } });
    await route(page, w);
    await page.goto(`${BASE_URL}/packages`);
    await page.getByRole('button', { name: 'Add package' }).click();
    const form = page.getByRole('dialog', { name: 'New package' });
    await form.waitFor();
    assert.equal(await form.locator('#package-id').inputValue(), 'PKG-101', 'A new ID never repeats one in use');
    await form.locator('#package-id').fill('PKG-2');
    await form.locator('#package-id').blur();
    assert.ok((await form.locator('#package-id-error').innerText()).includes('already used by Beta Retreat'));
    await page.close();

    const bookingPage = await browser.newPage({ viewport: { width: 1440, height: 900 } });
    await route(bookingPage, w);
    await bookingPage.goto(`${BASE_URL}/bookings?new=1&package=PKG-3`);
    const bookingForm = bookingPage.getByRole('dialog', { name: 'New booking' });
    await bookingForm.waitFor();
    assert.equal(await bookingForm.locator('#booking-packageId').inputValue(), 'PKG-3', 'Book this package arrives chosen');
    assert.ok((await bookingForm.locator('#booking-y-hint').innerText()).includes('1 slot left'));
    await bookingForm.locator('#booking-y').fill('3');
    await bookingForm.locator('#booking-y').blur();
    assert.ok((await bookingForm.locator('#booking-y-error').innerText()).includes('Only 1 slot is left'));
    await bookingPage.close();
  });

  await t.test('3. Every record opens in a panel, and Edit is one step from it', async () => {
    const w = world();
    const page = await browser.newPage({ viewport: { width: 1440, height: 900 } });
    await route(page, w);

    await page.goto(`${BASE_URL}/packages`);
    await page.getByRole('button', { name: 'Beta Retreat' }).click();
    const pkgPanel = page.getByRole('dialog', { name: 'Package PKG-2' });
    await pkgPanel.waitFor();
    assert.ok((await pkgPanel.innerText()).includes('4 open, of 36 in all'));
    await pkgPanel.getByRole('button', { name: 'Edit package' }).click();
    const pkgForm = page.getByRole('dialog', { name: 'Edit package' });
    await pkgForm.waitFor();
    assert.equal(await pkgForm.locator('#package-id').getAttribute('readonly'), '', 'A saved package keeps its ID');
    await pkgForm.getByRole('button', { name: 'Cancel' }).click();
    await page.getByRole('dialog', { name: 'Package PKG-2' }).waitFor();

    await page.goto(`${BASE_URL}/expenses`);
    await page.getByRole('button', { name: 'Marketing', exact: true }).click();
    await page.getByRole('dialog', { name: 'Expense EXP-2' }).waitFor();

    await page.goto(`${BASE_URL}/team?open=staff@prophetops.com`);
    const person = page.getByRole('dialog', { name: 'Casey Staff' });
    await person.waitFor();
    assert.ok(await person.getByRole('button', { name: 'Suspend account' }).isVisible());

    await page.goto(`${BASE_URL}/team?open=owner@prophetops.com`);
    const self = page.getByRole('dialog', { name: 'Renan & Tina' });
    await self.waitFor();
    assert.equal(await self.getByRole('button', { name: 'Suspend account' }).count(), 0, 'Nobody suspends their own account');
    assert.ok((await self.innerText()).includes('This is your own account'));
    await page.close();
  });

  await t.test('4. A quick change says what it did and can be undone, by button or Ctrl+Z', async () => {
    const w = world();
    const page = await browser.newPage({ viewport: { width: 1440, height: 900 } });
    await route(page, w);
    await page.goto(`${BASE_URL}/expenses?open=EXP-2`);
    const panel = page.getByRole('dialog', { name: 'Expense EXP-2' });
    await panel.waitFor();
    await panel.getByRole('button', { name: 'Mark as paid' }).click();
    const toast = page.locator('.toast', { hasText: 'Expense EXP-2 marked as paid' });
    await toast.waitFor();
    assert.equal(w.writes.at(-1).body.paymentStatus, 'Paid');

    await toast.getByRole('button', { name: /Undo/ }).click();
    await page.locator('.toast', { hasText: 'Expense EXP-2 is pending again' }).waitFor();
    assert.equal(w.writes.at(-1).body.paymentStatus, 'Pending', 'Undo puts the expense back');

    // The first notice closes once its undo has finished.
    await page.locator('.toast', { hasText: 'marked as paid' }).waitFor({ state: 'detached' });
    await panel.getByRole('button', { name: 'Mark as paid' }).click();
    await page.locator('.toast', { hasText: 'Expense EXP-2 marked as paid' }).getByRole('button', { name: /Undo/ }).waitFor();
    const before = w.writes.length;
    await page.keyboard.press('Escape');
    await page.keyboard.press('Control+z');
    await until(() => w.writes.length > before);
    assert.equal(w.writes.at(-1).body.paymentStatus, 'Pending', 'Ctrl+Z reaches the same undo');
    await page.close();
  });

  await t.test('5. Several bookings are marked paid at once, and the whole change can be undone', async () => {
    const w = world();
    const page = await browser.newPage({ viewport: { width: 1440, height: 900 } });
    await route(page, w);
    await page.goto(`${BASE_URL}/bookings`);
    await page.getByRole('checkbox', { name: /BKG-1002/ }).check();
    await page.getByRole('checkbox', { name: /BKG-1003/ }).check();
    const bar = page.getByRole('region', { name: '2 selected' });
    await bar.waitFor();
    await bar.getByRole('button', { name: 'Mark 2 as paid' }).click();

    const bulk = w.writes.find((x) => x.path.endsWith('/bulk'));
    assert.deepEqual([...bulk.body.ids].sort(), ['BKG-1002', 'BKG-1003']);
    assert.deepEqual(bulk.body.revisions, { 'BKG-1002': 1, 'BKG-1003': 1 });
    const toast = page.locator('.toast', { hasText: '2 bookings marked as paid' });
    await toast.waitFor();
    await bar.waitFor({ state: 'detached' });

    await toast.getByRole('button', { name: /Undo/ }).click();
    await page.locator('.toast', { hasText: 'put back as they were' }).waitFor();
    const puts = w.writes.filter((x) => x.method === 'PUT');
    assert.deepEqual(puts.map((x) => [x.body.id, x.body.paymentStatus, x.body.revision]).sort(), [
      ['BKG-1002', 'Pending', 2],
      ['BKG-1003', 'Partially Paid', 2],
    ]);
    await page.close();
  });

  await t.test('6. A booking saved elsewhere meanwhile is merged into the draft, not thrown away', async () => {
    const w = world();
    const page = await browser.newPage({ viewport: { width: 1440, height: 900 } });
    await route(page, w);
    await page.goto(`${BASE_URL}/bookings?open=BKG-1002`);
    await page.getByRole('dialog', { name: 'Booking BKG-1002' }).getByRole('button', { name: 'Edit booking' }).click();
    const form = page.getByRole('dialog', { name: 'Edit booking' });
    await form.waitFor();
    await form.locator('#booking-notes').fill('Wants a sea view');

    w.staleOnce = { paymentStatus: 'Paid' };
    await form.getByRole('button', { name: 'Save changes' }).click();
    await form.locator('.drawer-form-merge').waitFor();
    assert.ok((await form.locator('.drawer-form-merge').innerText()).includes('Payment status'));
    assert.equal(await form.locator('#booking-paymentStatus').inputValue(), 'Paid', 'Their change is taken in');
    assert.equal(await form.locator('#booking-notes').inputValue(), 'Wants a sea view', 'The draft is kept');

    await form.getByRole('button', { name: 'Save changes' }).click();
    await form.waitFor({ state: 'detached' });
    const saved = w.writes.filter((x) => x.method === 'PUT').at(-1).body;
    assert.equal(saved.revision, 2, 'The second save goes against the latest version');
    assert.equal(saved.notes, 'Wants a sea view');
    assert.equal(saved.paymentStatus, 'Paid');
    await page.close();
  });

  await t.test('7. A new booking left unfinished comes back after the tab is closed, until started over', async () => {
    const w = world();
    const context = await browser.newContext({ viewport: { width: 1440, height: 900 } });
    const first = await context.newPage();
    await route(first, w);
    await first.goto(`${BASE_URL}/bookings`);
    await first.getByRole('button', { name: 'Add booking' }).click();
    await first.getByRole('dialog', { name: 'New booking' }).waitFor();
    await first.locator('#booking-client').fill('Golf Group');
    await first.locator('#booking-notes').fill('Two rooms, sea view');
    await first.waitForTimeout(700);
    await first.close({ runBeforeUnload: false });

    const again = await context.newPage();
    await route(again, w);
    await again.goto(`${BASE_URL}/bookings`);
    await again.getByRole('button', { name: 'Add booking' }).click();
    const form = again.getByRole('dialog', { name: 'New booking' });
    await form.waitFor();
    assert.ok((await form.locator('.drawer-form-merge').innerText()).includes('Restored the booking you started'));
    assert.equal(await form.locator('#booking-client').inputValue(), 'Golf Group');
    assert.equal(await form.locator('#booking-notes').inputValue(), 'Two rooms, sea view');

    await form.getByRole('button', { name: 'Start over' }).click();
    assert.equal(await form.locator('#booking-client').inputValue(), '', 'Start over clears the draft');
    await again.keyboard.press('Escape');
    await form.waitFor({ state: 'detached' });
    await again.getByRole('button', { name: 'Add booking' }).click();
    await again.getByRole('dialog', { name: 'New booking' }).waitFor();
    assert.equal(await again.locator('.drawer-form-merge').count(), 0, 'Nothing is offered once it was dropped');
    await context.close();
  });

  await t.test('8. A price reached by Tab is replaced by what is typed, not added to', async () => {
    const w = world();
    const page = await browser.newPage({ viewport: { width: 1440, height: 900 } });
    await route(page, w);
    await page.goto(`${BASE_URL}/bookings?new=1&package=PKG-1`);
    const form = page.getByRole('dialog', { name: 'New booking' });
    await form.waitFor();
    const price = form.locator('#booking-grossRevenue');
    assert.equal(await price.inputValue(), '10,000', 'The package price is filled in');

    await form.locator('#booking-packageId').focus();
    await page.keyboard.press('Tab');
    assert.equal(await page.evaluate(() => document.activeElement?.id), 'booking-grossRevenue');
    await page.keyboard.type('25000');
    assert.equal(await price.inputValue(), '25000', 'Typing replaces the price');
    await page.keyboard.press('Tab');
    assert.equal(await price.inputValue(), '25,000', 'The figure reads with commas again once left');
    await page.close();
  });
});
