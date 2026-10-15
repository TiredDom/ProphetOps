const test = require('node:test');
const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const vm = require('node:vm');
const ts = require('typescript');

function loadHelper() {
  const sourcePath = path.join(__dirname, '..', 'src', 'forecastChart.ts');
  const source = fs.readFileSync(sourcePath, 'utf8');
  const compiled = ts.transpileModule(source, {
    compilerOptions: {
      module: ts.ModuleKind.CommonJS,
      target: ts.ScriptTarget.ES2020,
      strict: true,
    },
  }).outputText;
  const context = {
    exports: {},
    module: { exports: {} },
    require,
  };
  context.exports = context.module.exports;
  vm.runInNewContext(compiled, context, { filename: sourcePath });
  return context.module.exports;
}

const {
  buildForecastTimeline,
  buildOverviewChartModel,
  buildForecastDecisions,
  formatCompactPeso,
  formatConcisePeso,
} = loadHelper();

// -----------------------------------------------------------------------------
// Data Series Logic (Isolated In-Memory Implementation Matching DemandSeriesBuilder)
// -----------------------------------------------------------------------------
function buildDemandSeries(bookings, today, allowSampleFallback = false) {
  const lastComplete = new Date(Date.UTC(today.getUTCFullYear(), today.getUTCMonth(), 1));
  lastComplete.setUTCMonth(lastComplete.getUTCMonth() - 1);

  // Filter bookings: not voided and booking date <= end of last complete month
  const lastCompleteEnd = new Date(Date.UTC(lastComplete.getUTCFullYear(), lastComplete.getUTCMonth() + 1, 0, 23, 59, 59, 999));

  const valid = bookings.filter((b) => {
    if (b.voidedAt) return false;
    const d = new Date(b.bookingDate);
    return d <= lastCompleteEnd;
  });

  const monthly = new Map();
  for (const b of valid) {
    const d = new Date(b.bookingDate);
    const key = `${d.getUTCFullYear()}-${String(d.getUTCMonth() + 1).padStart(2, '0')}`;
    monthly.set(key, (monthly.get(key) || 0) + b.grossRevenue);
  }

  const keys = Array.from(monthly.keys()).sort();
  let liveMonths = 0;
  let filled = 0;
  const live = [];

  if (keys.length > 0) {
    const [firstY, firstM] = keys[0].split('-').map(Number);
    const [lastRecY, lastRecM] = keys[keys.length - 1].split('-').map(Number);
    const lastRecDate = new Date(Date.UTC(lastRecY, lastRecM - 1, 1));
    const endDate = lastRecDate < lastComplete ? lastRecDate : lastComplete;

    const curr = new Date(Date.UTC(firstY, firstM - 1, 1));
    while (curr <= endDate) {
      const key = `${curr.getUTCFullYear()}-${String(curr.getUTCMonth() + 1).padStart(2, '0')}`;
      if (monthly.has(key)) {
        live.push(monthly.get(key));
      } else {
        live.push(0);
        filled++;
      }
      curr.setUTCMonth(curr.getUTCMonth() + 1);
    }
    liveMonths = live.Count !== undefined ? live.Count : live.length;
  }

  const minMonths = 24;
  const recordedCount = liveMonths - filled;
  if (recordedCount >= minMonths) {
    return {
      values: live,
      source: 'LiveRecords',
      usingLiveRecords: true,
      liveMonthsAvailable: liveMonths,
      filledMonths: filled,
      recordedMonths: recordedCount,
    };
  }

  return {
    values: allowSampleFallback ? new Array(36).fill(100000) : [],
    source: 'InsufficientLiveRecords',
    usingLiveRecords: false,
    liveMonthsAvailable: liveMonths,
    filledMonths: filled,
    recordedMonths: recordedCount,
  };
}

// =============================================================================
// TEST SUITE: Dynamic Date Ranges, Fallback Year Bug, and Recommendations
// =============================================================================

test('1. Multi-year handling & Dec-to-Jan rollover across non-2026 years (2024-2025, 2027-2028)', () => {
  // History: 12 months from Jan 2024 to Dec 2024
  const history2024 = Array.from({ length: 12 }, (_, i) => ({
    label: `M-${11 - i}`,
    month: ['Jan', 'Feb', 'Mar', 'Apr', 'May', 'Jun', 'Jul', 'Aug', 'Sep', 'Oct', 'Nov', 'Dec'][i],
    value: 500000 + i * 15000,
  }));

  // Forecast: Dec 2024 rollover to Jan 2025 -> Jun 2025
  const steps2025 = [
    { step: 1, month: 'Jan', monthLabel: 'January 2025', value: 720000, lower: 650000, upper: 790000 },
    { step: 2, month: 'Feb', monthLabel: 'February 2025', value: 740000, lower: 660000, upper: 820000 },
    { step: 3, month: 'Mar', monthLabel: 'March 2025', value: 790000, lower: 700000, upper: 880000 },
    { step: 4, month: 'Apr', monthLabel: 'April 2025', value: 850000, lower: 750000, upper: 950000 },
    { step: 5, month: 'May', monthLabel: 'May 2025', value: 920000, lower: 810000, upper: 1030000 },
    { step: 6, month: 'Jun', monthLabel: 'June 2025', value: 980000, lower: 860000, upper: 1100000 },
  ];

  const timeline = buildForecastTimeline(history2024, steps2025, { lastRecordedMonth: 'December 2024' });
  assert.equal(timeline.length, 18);
  assert.equal(timeline[0].label, 'January 2024');
  assert.equal(timeline[11].label, 'December 2024');
  assert.equal(timeline[12].label, 'January 2025');
  assert.equal(timeline[17].label, 'June 2025');

  // Verify chart model year extraction for 2024 and 2025
  const chart = buildOverviewChartModel({
    width: 800,
    history: history2024,
    steps: steps2025,
    dataSource: { lastRecordedMonth: 'December 2024' },
    historyMonths: 12,
  });

  assert.ok(chart);
  // Ensure xTicks carry truthful non-2026 years (2024 and 2025)
  const years = chart.xTicks.map((t) => t.year).filter((y) => y !== null);
  assert.ok(years.includes(2024), 'Must include year 2024');
  assert.ok(years.includes(2025), 'Must include rollover year 2025');
  assert.ok(!years.includes(2026), 'Must NOT contain 2026 for a 2024-2025 timeline');

  // Next: Test 2027 to 2028 rollover
  const history2027 = [
    { label: 'M-1', month: 'Nov', value: 600000 },
    { label: 'M0', month: 'Dec', value: 650000 },
  ];
  const steps2028 = [
    { step: 1, month: 'Jan', monthLabel: 'January 2028', value: 700000, lower: 600000, upper: 800000 },
    { step: 2, month: 'Feb', monthLabel: 'February 2028', value: 720000, lower: 610000, upper: 830000 },
  ];
  const timeline2028 = buildForecastTimeline(history2027, steps2028, { lastRecordedMonth: 'December 2027' });
  assert.equal(timeline2028[0].label, 'November 2027');
  assert.equal(timeline2028[1].label, 'December 2027');
  assert.equal(timeline2028[2].label, 'January 2028');
  assert.equal(timeline2028[3].label, 'February 2028');
});

test('2. Leap-year months handling (Feb 2024 & Feb 2028)', () => {
  // February in a leap year (2024, 2028)
  const leapHistory = [
    { label: 'M-2', month: 'Dec', value: 400000 },
    { label: 'M-1', month: 'Jan', value: 420000 },
    { label: 'M0', month: 'Feb', value: 450000 },
  ];
  const leapSteps = [
    { step: 1, month: 'Mar', monthLabel: 'March 2024', value: 480000, lower: 420000, upper: 540000 },
    { step: 2, month: 'Apr', monthLabel: 'April 2024', value: 500000, lower: 440000, upper: 560000 },
  ];

  const timeline = buildForecastTimeline(leapHistory, leapSteps, { lastRecordedMonth: 'February 2024' });
  assert.equal(timeline[2].label, 'February 2024');
  assert.equal(timeline[2].shortLabel, 'Feb 24');
  assert.equal(timeline[3].label, 'March 2024');

  // Verify leap year 2028
  const leap2028Steps = [
    { step: 1, month: 'Feb', monthLabel: 'February 2028', value: 510000, lower: 450000, upper: 570000 },
  ];
  const timeline2028 = buildForecastTimeline(leapHistory, leap2028Steps, { lastRecordedMonth: 'January 2028' });
  assert.equal(timeline2028[3].label, 'February 2028');
  assert.equal(timeline2028[3].shortLabel, 'Feb 28');
});

test('3. 6-month vs 12-month display windows preserve identical underlying forecast steps', () => {
  const fullHistory = Array.from({ length: 18 }, (_, i) => ({
    label: `M-${17 - i}`,
    month: ['Jan', 'Feb', 'Mar', 'Apr', 'May', 'Jun', 'Jul', 'Aug', 'Sep', 'Oct', 'Nov', 'Dec'][i % 12],
    value: 300000 + i * 20000,
  }));

  const steps = [
    { step: 1, month: 'Jul', monthLabel: 'July 2025', value: 750000, lower: 680000, upper: 820000 },
    { step: 2, month: 'Aug', monthLabel: 'August 2025', value: 800000, lower: 720000, upper: 880000 },
    { step: 3, month: 'Sep', monthLabel: 'September 2025', value: 710000, lower: 630000, upper: 790000 },
    { step: 4, month: 'Oct', monthLabel: 'October 2025', value: 890000, lower: 790000, upper: 990000 },
    { step: 5, month: 'Nov', monthLabel: 'November 2025', value: 940000, lower: 830000, upper: 1050000 },
    { step: 6, month: 'Dec', monthLabel: 'December 2025', value: 1100000, lower: 960000, upper: 1240000 },
  ];

  const chart6 = buildOverviewChartModel({
    width: 800,
    history: fullHistory,
    steps,
    dataSource: { lastRecordedMonth: 'June 2025' },
    historyMonths: 6,
  });

  const chart12 = buildOverviewChartModel({
    width: 800,
    history: fullHistory,
    steps,
    dataSource: { lastRecordedMonth: 'June 2025' },
    historyMonths: 12,
  });

  assert.ok(chart6);
  assert.ok(chart12);

  // History lengths reflect requested windows
  assert.equal(chart6.historyLength, 6);
  assert.equal(chart12.historyLength, 12);

  // Forecast points count is 6 in both models
  const forecastEntries6 = chart6.entries.filter((e) => e.isForecast);
  const forecastEntries12 = chart12.entries.filter((e) => e.isForecast);
  assert.equal(forecastEntries6.length, 6);
  assert.equal(forecastEntries12.length, 6);

  // Crucial check: underlying forecast step values, bounds, and step numbers must remain 100% IDENTICAL
  for (let i = 0; i < 6; i++) {
    assert.equal(forecastEntries6[i].value, forecastEntries12[i].value, `Step ${i + 1} value must match`);
    assert.equal(forecastEntries6[i].lower, forecastEntries12[i].lower, `Step ${i + 1} lower bound must match`);
    assert.equal(forecastEntries6[i].upper, forecastEntries12[i].upper, `Step ${i + 1} upper bound must match`);
    assert.equal(forecastEntries6[i].forecastStepNumber, forecastEntries12[i].forecastStepNumber, `Step number must match`);
    assert.equal(forecastEntries6[i].label, forecastEntries12[i].label, `Month label must match`);
  }
});

test('4. Data series history duration: 24+ recorded months vs insufficient history', () => {
  const today = new Date(Date.UTC(2026, 7, 15)); // August 15, 2026 (last complete month is July 2026)

  // Case A: Exactly 23 months of bookings (Jan 2024 to Nov 2025) -> insufficient
  const bookings23 = [];
  for (let m = 0; m < 23; m++) {
    const d = new Date(Date.UTC(2024, m, 5));
    bookings23.push({
      bookingDate: d.toISOString(),
      grossRevenue: 100000,
      voidedAt: null,
    });
  }
  const series23 = buildDemandSeries(bookings23, today, false);
  assert.equal(series23.usingLiveRecords, false);
  assert.equal(series23.recordedMonths, 23);
  assert.equal(series23.values.length, 0);

  // Case B: Exactly 24 complete recorded months (Jan 2024 to Dec 2025) -> qualifies
  const bookings24 = [...bookings23];
  bookings24.push({
    bookingDate: new Date(Date.UTC(2025, 11, 5)).toISOString(),
    grossRevenue: 150000,
    voidedAt: null,
  });
  const series24 = buildDemandSeries(bookings24, today, false);
  assert.equal(series24.usingLiveRecords, true);
  assert.equal(series24.recordedMonths, 24);
  assert.equal(series24.values.length, 24);
  assert.equal(series24.values[23], 150000);
});

test('5. Interior missing months are zero-filled without breaking seasonal alignment', () => {
  const today = new Date(Date.UTC(2026, 4, 15)); // May 15, 2026 (last complete is April 2026)
  const bookings = [];

  // Jan 2024 to Dec 2025 (24 months)
  for (let m = 0; m < 24; m++) {
    bookings.push({
      bookingDate: new Date(Date.UTC(2024, m, 5)).toISOString(),
      grossRevenue: 100000,
      voidedAt: null,
    });
  }
  // March 2026 has a booking, but Jan 2026 and Feb 2026 have none (interior gap!)
  bookings.push({
    bookingDate: new Date(Date.UTC(2026, 2, 10)).toISOString(),
    grossRevenue: 250000,
    voidedAt: null,
  });

  const series = buildDemandSeries(bookings, today, false);
  assert.equal(series.usingLiveRecords, true);
  // Total span from Jan 2024 to Mar 2026 = 27 months
  assert.equal(series.values.length, 27);
  assert.equal(series.filledMonths, 2, 'Must count exactly 2 interior filled months');
  assert.equal(series.values[24], 0, 'Jan 2026 must be padded with 0');
  assert.equal(series.values[25], 0, 'Feb 2026 must be padded with 0');
  assert.equal(series.values[26], 250000, 'Mar 2026 must preserve actual booking total');
});

test('6. Exclusion of running/current month and future months from recorded history', () => {
  // Standing on Oct 15, 2026
  const standingDate = new Date(Date.UTC(2026, 9, 15));
  const bookings = [];

  // Jan 2024 to Sep 2026 (33 completed months)
  for (let m = 0; m < 33; m++) {
    bookings.push({
      bookingDate: new Date(Date.UTC(2024, m, 5)).toISOString(),
      grossRevenue: 100000,
      voidedAt: null,
    });
  }

  // Bookings in the running month (Oct 4 and Oct 10, 2026)
  bookings.push({
    bookingDate: new Date(Date.UTC(2026, 9, 4)).toISOString(),
    grossRevenue: 50000,
    voidedAt: null,
  });
  bookings.push({
    bookingDate: new Date(Date.UTC(2026, 9, 10)).toISOString(),
    grossRevenue: 40000,
    voidedAt: null,
  });

  // Future booking booked far ahead for Dec 2026
  bookings.push({
    bookingDate: new Date(Date.UTC(2026, 11, 25)).toISOString(),
    grossRevenue: 300000,
    voidedAt: null,
  });

  const series = buildDemandSeries(bookings, standingDate, false);
  // Must stop at September 2026 (index 32 = 33 months)
  assert.equal(series.values.length, 33);
  assert.equal(series.values[32], 100000, 'September 2026 is the last complete month');
  // October and December bookings must be completely excluded from history series
  assert.ok(!series.values.includes(90000));
  assert.ok(!series.values.includes(300000));
});

test('7. Exclusion of voided bookings from revenue aggregation', () => {
  const today = new Date(Date.UTC(2026, 1, 10)); // Feb 10, 2026 (last complete is Jan 2026)
  const bookings = [];

  for (let m = 0; m < 24; m++) {
    bookings.push({
      bookingDate: new Date(Date.UTC(2024, m, 5)).toISOString(),
      grossRevenue: 100000,
      voidedAt: null,
    });
  }

  // Add a voided booking in December 2025 (month index 23)
  bookings.push({
    bookingDate: new Date(Date.UTC(2025, 11, 20)).toISOString(),
    grossRevenue: 85000,
    voidedAt: new Date(Date.UTC(2025, 11, 21)).toISOString(), // VOIDED!
  });

  const series = buildDemandSeries(bookings, today, false);
  // December 2025 revenue must remain 100000, not 185000
  assert.equal(series.values[23], 100000);
});

test('8. Recomputation when booking dates or amounts change', () => {
  const today = new Date(Date.UTC(2026, 3, 10)); // Standing in April 2026
  const bookings = [];
  // 26 months of data: Jan 2024 to Feb 2026
  for (let m = 0; m < 26; m++) {
    bookings.push({
      id: `bkg-${m}`,
      bookingDate: new Date(Date.UTC(2024, m, 5)).toISOString(),
      grossRevenue: 100000,
      voidedAt: null,
    });
  }

  // Initial series: Month 10 (Nov 2024) is 100,000, Month 11 (Dec 2024) is 100,000
  const seriesBefore = buildDemandSeries(bookings, today, false);
  assert.equal(seriesBefore.usingLiveRecords, true);
  assert.equal(seriesBefore.values[10], 100000);
  assert.equal(seriesBefore.values[11], 100000);

  // User edits a booking: moves date from Nov 5 2024 to Dec 5 2024, and increases revenue to 150,000
  bookings[10].bookingDate = new Date(Date.UTC(2024, 11, 5)).toISOString();
  bookings[10].grossRevenue = 150000;

  const seriesAfter = buildDemandSeries(bookings, today, false);
  assert.equal(seriesAfter.usingLiveRecords, true);
  // Month 10 (Nov 2024) now has 0 (interior gap filled with 0)
  assert.equal(seriesAfter.values[10], 0);
  // Month 11 (Dec 2024) now has 100,000 + 150,000 = 250,000
  assert.equal(seriesAfter.values[11], 250000);
});

test('9. Recommendation selection & evidence: rising, falling, flat, tied peaks, first-step peak, zero baseline', () => {
  const historyBaseline = [{ label: 'Jul 26', month: 'Jul', value: 1000000 }];

  // 9A: Rising peak (+10%)
  const risingSteps = [
    { step: 1, month: 'Aug', monthLabel: 'August 2026', value: 1000000, lower: 900000, upper: 1100000 },
    { step: 2, month: 'Sep', monthLabel: 'September 2026', value: 1100000, lower: 980000, upper: 1220000 },
    { step: 3, month: 'Oct', monthLabel: 'October 2026', value: 1050000, lower: 920000, upper: 1180000 },
  ];
  const recsRising = buildForecastDecisions({ steps: risingSteps, history: historyBaseline });
  const peakRec = recsRising.find((r) => r.id === 'peak');
  assert.ok(peakRec, 'Must produce peak recommendation');
  assert.equal(peakRec.kind, 'increase');
  assert.equal(peakRec.evidence.type, 'comparison');
  assert.equal(peakRec.evidence.current.label, 'Sep');
  assert.equal(peakRec.evidence.changePercent, 10);
  assert.ok(peakRec.finding.includes('+10.0%'));

  // 9B: Falling drop (-4.5%)
  const dropRec = recsRising.find((r) => r.id === 'drop');
  assert.ok(dropRec, 'Must produce drop recommendation');
  assert.equal(dropRec.kind, 'decrease');
  assert.equal(dropRec.evidence.type, 'comparison');
  assert.equal(dropRec.evidence.current.label, 'Oct');
  assert.equal(dropRec.evidence.changePercent, -4.5);

  // 9C: Flat horizon with bounds (must NOT produce false peak or drop)
  const flatStepsWithBounds = [
    { step: 1, month: 'Aug', monthLabel: 'August 2026', value: 1000000, lower: 900000, upper: 1100000 },
    { step: 2, month: 'Sep', monthLabel: 'September 2026', value: 1000000, lower: 900000, upper: 1100000 },
    { step: 3, month: 'Oct', monthLabel: 'October 2026', value: 1000000, lower: 900000, upper: 1100000 },
  ];
  const recsFlatBounds = buildForecastDecisions({ steps: flatStepsWithBounds, history: historyBaseline });
  assert.ok(!recsFlatBounds.some((r) => r.id === 'peak'), 'Must NOT invent a peak on flat data');
  assert.ok(!recsFlatBounds.some((r) => r.id === 'drop'), 'Must NOT invent a drop on flat data');
  const rangeRecFlat = recsFlatBounds.find((r) => r.id === 'range');
  assert.ok(rangeRecFlat, 'Produces range uncertainty for bounded flat data');

  // 9C-2: Completely flat horizon with no bounds triggers steady recommendation
  const flatStepsNoBounds = [
    { step: 1, month: 'Aug', monthLabel: 'August 2026', value: 1000000 },
    { step: 2, month: 'Sep', monthLabel: 'September 2026', value: 1000000 },
  ];
  const recsSteady = buildForecastDecisions({ steps: flatStepsNoBounds, history: historyBaseline });
  assert.equal(recsSteady.length, 1);
  assert.equal(recsSteady[0].id, 'steady');
  assert.equal(recsSteady[0].kind, 'uncertainty');
  assert.ok(recsSteady[0].finding.includes('Steady horizon'));

  // 9D: Tied peaks (Step 2 and Step 4 both 1,500,000) -> Selects first occurrence deterministically
  const tiedSteps = [
    { step: 1, month: 'Aug', monthLabel: 'August 2026', value: 1200000, lower: 1100000, upper: 1300000 },
    { step: 2, month: 'Sep', monthLabel: 'September 2026', value: 1500000, lower: 1350000, upper: 1650000 },
    { step: 3, month: 'Oct', monthLabel: 'October 2026', value: 1400000, lower: 1250000, upper: 1550000 },
    { step: 4, month: 'Nov', monthLabel: 'November 2026', value: 1500000, lower: 1350000, upper: 1650000 },
  ];
  const recsTied = buildForecastDecisions({ steps: tiedSteps, history: historyBaseline });
  const tiedPeak = recsTied.find((r) => r.id === 'peak');
  assert.ok(tiedPeak);
  assert.equal(tiedPeak.evidence.current.label, 'Sep', 'Must pick the first peak step (Sep, step 2)');
  assert.equal(tiedPeak.stepNumbers.join(','), '1,2');

  // 9E: First-step peak (Step 1 is highest, compared against history baseline)
  const firstStepPeakSteps = [
    { step: 1, month: 'Aug', monthLabel: 'August 2026', value: 1400000, lower: 1250000, upper: 1550000 },
    { step: 2, month: 'Sep', monthLabel: 'September 2026', value: 1200000, lower: 1050000, upper: 1350000 },
    { step: 3, month: 'Oct', monthLabel: 'October 2026', value: 1100000, lower: 950000, upper: 1250000 },
  ];
  const recsFirstPeak = buildForecastDecisions({ steps: firstStepPeakSteps, history: historyBaseline });
  const firstPeakRec = recsFirstPeak.find((r) => r.id === 'peak');
  assert.ok(firstPeakRec);
  assert.equal(firstPeakRec.evidence.current.label, 'Aug');
  // Diff: (1400000 - 1000000) / 1000000 * 100 = +40%
  assert.equal(firstPeakRec.evidence.changePercent, 40);

  // 9F: Zero baseline (prior value is 0) -> suppressed safely without division by zero, NaN, or Infinity
  const zeroBaselineHistory = [{ label: 'Jul 26', month: 'Jul', value: 0 }];
  const recsZeroBaseline = buildForecastDecisions({ steps: firstStepPeakSteps, history: zeroBaselineHistory });
  for (const r of recsZeroBaseline) {
    if (r.evidence.type === 'comparison') {
      assert.ok(Number.isFinite(r.evidence.changePercent), 'Percentage must be finite');
      assert.ok(!isNaN(r.evidence.changePercent), 'Percentage must not be NaN');
    }
  }
});

test('10. Prediction bounds edge cases: missing, reversed, wide, and narrow bounds', () => {
  // Reversed bounds: lower is higher than upper (e.g. lower: 1500000, upper: 1200000)
  const reversedBoundsSteps = [
    { step: 1, month: 'Aug', monthLabel: 'August 2026', value: 1350000, lower: 1500000, upper: 1200000 },
    { step: 2, month: 'Sep', monthLabel: 'September 2026', value: 1360000, lower: 1600000, upper: 1100000 },
  ];
  const recsReversed = buildForecastDecisions({ steps: reversedBoundsSteps });
  const rangeRec = recsReversed.find((r) => r.id === 'range');
  assert.ok(rangeRec);
  assert.ok(rangeRec.evidence.lower <= rangeRec.evidence.upper, 'Lower bound must be <= upper bound after normalization');

  // Wide bounds vs narrow bounds
  const varyingSpreadSteps = [
    { step: 1, month: 'Aug', monthLabel: 'August 2026', value: 1000000, lower: 980000, upper: 1020000 }, // spread 40k
    { step: 2, month: 'Sep', monthLabel: 'September 2026', value: 1000000, lower: 700000, upper: 1300000 }, // spread 600k (Wider!)
  ];
  const recsWide = buildForecastDecisions({ steps: varyingSpreadSteps });
  const uncertaintyRec = recsWide.find((r) => r.id === 'range');
  assert.ok(uncertaintyRec);
  assert.equal(uncertaintyRec.evidence.label, 'Sep', 'Must choose the widest spread step as candidate');
});

test('11. Empty and invalid data handling in recommendation and chart builders', () => {
  // Empty steps array
  assert.equal(buildForecastDecisions({ steps: [] }).length, 0);

  // Steps with NaN and invalid values
  const invalidSteps = [
    { step: NaN, month: 'Aug', monthLabel: 'August', value: NaN, lower: 0, upper: 100 },
    null,
    undefined,
    { step: 1, month: 'Aug', monthLabel: 'August 2026', value: 1000000, lower: 900000, upper: 1100000 },
  ];
  const recs = buildForecastDecisions({ steps: invalidSteps });
  assert.ok(recs.length > 0);
  assert.ok(Number.isFinite(recs[0].evidence.value));

  // Chart model with null history
  assert.equal(buildOverviewChartModel({ width: 600, history: [], steps: [], dataSource: { lastRecordedMonth: null } }), null);
});

test('12. Guardrail semantic verification: never infer travel demand, cash/profit, staffing, or projected expenses from revenue alone', () => {
  const stepsCollection = [
    // Rising scenario
    [
      { step: 1, month: 'Aug', monthLabel: 'August 2026', value: 1000000, lower: 900000, upper: 1100000 },
      { step: 2, month: 'Sep', monthLabel: 'September 2026', value: 1250000, lower: 1100000, upper: 1400000 },
      { step: 3, month: 'Oct', monthLabel: 'October 2026', value: 1100000, lower: 950000, upper: 1250000 },
    ],
    // Falling scenario
    [
      { step: 1, month: 'Nov', monthLabel: 'November 2026', value: 1500000, lower: 1300000, upper: 1700000 },
      { step: 2, month: 'Dec', monthLabel: 'December 2026', value: 1200000, lower: 1000000, upper: 1400000 },
    ],
    // Flat scenario
    [
      { step: 1, month: 'Jan', monthLabel: 'January 2027', value: 800000, lower: 750000, upper: 850000 },
      { step: 2, month: 'Feb', monthLabel: 'February 2027', value: 800000, lower: 750000, upper: 850000 },
    ],
  ];

  const prohibitedPhrases = [
    'travel demand',
    'demand',
    'cash',
    'profit',
    'staffing requirements',
    'staffing',
    'projected expenses',
    'projected expense',
    'cashflow',
    'cash flow',
  ];

  for (const steps of stepsCollection) {
    const recs = buildForecastDecisions({ steps });
    for (const rec of recs) {
      const combinedText = `${rec.action} ${rec.finding}`.toLowerCase();

      for (const phrase of prohibitedPhrases) {
        assert.ok(
          !combinedText.includes(phrase),
          `Prohibited phrase "${phrase}" found in recommendation text: "${rec.action}" / "${rec.finding}"`
        );
      }

      // Check dynamic content follows input data
      if (rec.evidence.type === 'comparison') {
        assert.ok(Number.isFinite(rec.evidence.changePercent));
        assert.ok(rec.finding.includes('%'));
      }
    }
  }
});

test('13. forecastChart.ts fallback year truthful behavior: missing dates do NOT invent 2026', () => {
  // Label with NO year (e.g. synthetic label or unparsed label)
  const syntheticHistory = [
    { label: 'Step 1', month: 'Custom', value: 200000 },
    { label: 'M-1', month: 'Custom', value: 210000 },
  ];
  const syntheticSteps = [
    { step: 1, month: 'Custom', monthLabel: 'Step 2', value: 220000, lower: 200000, upper: 240000 },
  ];

  const chart = buildOverviewChartModel({
    width: 600,
    history: syntheticHistory,
    steps: syntheticSteps,
    dataSource: { lastRecordedMonth: null },
  });

  assert.ok(chart);
  // Entries with no discernible date must have year === null, NEVER 2026!
  for (const entry of chart.entries) {
    assert.equal(
      entry.year,
      null,
      `Entry with label "${entry.label}" must have year null, not invented 2026 (got: ${entry.year})`
    );
  }

  // Ticks must also have year === null
  for (const tick of chart.xTicks) {
    assert.equal(
      tick.year,
      null,
      `xTick for index ${tick.index} must have year null, not invented 2026 (got: ${tick.year})`
    );
  }

  // Contrast with truthful explicit years: 2024 and 2028
  const explicitHistory = [
    { label: 'October 2024', month: 'Oct', value: 300000 },
  ];
  const explicitSteps = [
    { step: 1, month: 'Nov', monthLabel: 'November 2028', value: 320000, lower: 290000, upper: 350000 },
  ];
  const explicitChart = buildOverviewChartModel({
    width: 600,
    history: explicitHistory,
    steps: explicitSteps,
    dataSource: { lastRecordedMonth: 'October 2024' },
  });

  assert.ok(explicitChart);
  assert.equal(explicitChart.entries[0].year, 2024);
  assert.equal(explicitChart.entries[1].year, 2028);
});
