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

const sampleHistory = [
  { label: 'M-11', month: 'Aug', value: 1204000 },
  { label: 'M-10', month: 'Sep', value: 1167900 },
  { label: 'M-9', month: 'Oct', value: 1308200 },
  { label: 'M-8', month: 'Nov', value: 1601200 },
  { label: 'M-7', month: 'Dec', value: 2284600 },
  { label: 'M-6', month: 'Jan', value: 1557100 },
  { label: 'M-5', month: 'Feb', value: 1563200 },
  { label: 'M-4', month: 'Mar', value: 1961400 },
  { label: 'M-3', month: 'Apr', value: 2523400 },
  { label: 'M-2', month: 'May', value: 2095400 },
  { label: 'M-1', month: 'Jun', value: 747000 },
  { label: 'M0', month: 'Jul', value: 451500 },
];

const sampleSteps = [
  { step: 1, month: 'Aug', monthLabel: 'August 2026', value: 1365247.03, lower: 928270.62, upper: 1802223.44 },
  { step: 2, month: 'Sep', monthLabel: 'September 2026', value: 1135408.48, lower: 517430.52, upper: 1753386.44 },
  { step: 3, month: 'Oct', monthLabel: 'October 2026', value: 1313418.04, lower: 556552.70, upper: 2070283.38 },
  { step: 4, month: 'Nov', monthLabel: 'November 2026', value: 1609254.73, lower: 735301.91, upper: 2483207.54 },
  { step: 5, month: 'Dec', monthLabel: 'December 2026', value: 2096918.21, lower: 1119809.26, upper: 3074027.16 },
  { step: 6, month: 'Jan', monthLabel: 'January 2027', value: 1344452.36, lower: 274083.13, upper: 2414821.59 },
];

const dataSource = { lastRecordedMonth: 'July 2026' };

test('1. History display changes (6 vs 12 months) do not alter fitted forecast steps or values', () => {
  const chart6 = buildOverviewChartModel({
    width: 800,
    history: sampleHistory,
    steps: sampleSteps,
    dataSource,
    historyMonths: 6,
  });

  const chart12 = buildOverviewChartModel({
    width: 800,
    history: sampleHistory,
    steps: sampleSteps,
    dataSource,
    historyMonths: 12,
  });

  assert.ok(chart6 && chart12);
  assert.equal(chart6.entries.filter((e) => e.isForecast).length, 6);
  assert.equal(chart12.entries.filter((e) => e.isForecast).length, 6);

  const forecast6 = chart6.entries.filter((e) => e.isForecast);
  const forecast12 = chart12.entries.filter((e) => e.isForecast);

  for (let i = 0; i < 6; i++) {
    assert.equal(forecast6[i].value, forecast12[i].value);
    assert.equal(forecast6[i].lower, forecast12[i].lower);
    assert.equal(forecast6[i].upper, forecast12[i].upper);
    assert.equal(forecast6[i].label, forecast12[i].label);
  }

  // Decisions computed from full steps are completely unaffected by history toggle
  const decisions = buildForecastDecisions({ steps: sampleSteps, history: sampleHistory });
  assert.equal(decisions.length, 3);
});

test('2. Real live forecast steps generate data-derived actions and matching indicators', () => {
  const decisions = buildForecastDecisions({ steps: sampleSteps, history: sampleHistory });

  assert.equal(decisions.length, 3);

  // Peak: Dec 2026 vs Nov 2026 (+30.3%)
  const peak = decisions.find((d) => d.id === 'peak');
  assert.ok(peak);
  assert.equal(peak.kind, 'increase');
  assert.equal(peak.action, 'Review expenses before approving new spending');
  assert.equal(peak.finding, 'December peak / +30.3% versus November');
  assert.equal(JSON.stringify(peak.stepNumbers), JSON.stringify([4, 5]));
  assert.equal(peak.evidence.type, 'comparison');
  assert.equal(peak.evidence.changePercent, 30.3);
  assert.equal(formatConcisePeso(peak.evidence.prior.value), '₱1.61M');
  assert.equal(formatConcisePeso(peak.evidence.current.value), '₱2.10M');

  // Range: Dec 2026 uncertainty (₱1.12M - ₱3.07M)
  const range = decisions.find((d) => d.id === 'range');
  assert.ok(range);
  assert.equal(range.kind, 'uncertainty');
  assert.equal(range.action, 'Keep December commitments flexible');
  assert.equal(range.finding, 'December uncertainty / approx. 80% range');
  assert.equal(JSON.stringify(range.stepNumbers), JSON.stringify([5]));
  assert.equal(range.evidence.type, 'range');
  assert.equal(formatConcisePeso(range.evidence.lower), '₱1.12M');
  assert.equal(formatConcisePeso(range.evidence.value), '₱2.10M');
  assert.equal(formatConcisePeso(range.evidence.upper), '₱3.07M');

  // Drop: Jan 2027 vs Dec 2026 (-35.9%)
  const drop = decisions.find((d) => d.id === 'drop');
  assert.ok(drop);
  assert.equal(drop.kind, 'decrease');
  assert.equal(drop.action, 'Review expense categories before January');
  assert.equal(drop.finding, 'January decrease / -35.9% versus December');
  assert.equal(JSON.stringify(drop.stepNumbers), JSON.stringify([5, 6]));
  assert.equal(drop.evidence.type, 'comparison');
  assert.equal(drop.evidence.changePercent, -35.9);
  assert.equal(formatConcisePeso(drop.evidence.prior.value), '₱2.10M');
  assert.equal(formatConcisePeso(drop.evidence.current.value), '₱1.34M');
});

test('3. Small and large revenue values format dynamically without clipping or NaN', () => {
  assert.equal(formatCompactPeso(0), '₱0');
  assert.equal(formatCompactPeso(500), '₱500');
  assert.equal(formatCompactPeso(50000), '₱50k');
  assert.equal(formatCompactPeso(500000), '₱500k');
  assert.equal(formatCompactPeso(2000000), '₱2M');
  assert.equal(formatCompactPeso(150000000), '₱150M');

  assert.equal(formatConcisePeso(0), '₱0');
  assert.equal(formatConcisePeso(500), '₱500');
  assert.equal(formatConcisePeso(75000), '₱75K');
  assert.equal(formatConcisePeso(2096918.21), '₱2.10M');
  assert.equal(formatConcisePeso(150000000), '₱150.00M');

  const smallSteps = [
    { step: 1, month: 'Aug', monthLabel: 'August 2026', value: 500, lower: 300, upper: 700 },
    { step: 2, month: 'Sep', monthLabel: 'September 2026', value: 800, lower: 600, upper: 1000 },
  ];
  const chartSmall = buildOverviewChartModel({
    width: 390,
    history: [{ label: 'M0', month: 'Jul', value: 400 }],
    steps: smallSteps,
    dataSource,
  });
  assert.ok(chartSmall);
  assert.ok(chartSmall.yTicks.length > 0);
  for (const t of chartSmall.yTicks) {
    assert.ok(Number.isFinite(t.y));
    assert.ok(!t.label.includes('NaN'));
  }
});

test('4. Empty, short, and flat series handle edge cases gracefully', () => {
  // Empty steps
  assert.equal(buildForecastDecisions({ steps: [] }).length, 0);
  assert.equal(buildOverviewChartModel({ width: 400, history: [], steps: [], dataSource }), null);

  // Flat steps (e.g. constant 100,000)
  const flatSteps = [
    { step: 1, month: 'Aug', monthLabel: 'August 2026', value: 100000, lower: 80000, upper: 120000 },
    { step: 2, month: 'Sep', monthLabel: 'September 2026', value: 100000, lower: 80000, upper: 120000 },
    { step: 3, month: 'Oct', monthLabel: 'October 2026', value: 100000, lower: 80000, upper: 120000 },
  ];
  const flatDecisions = buildForecastDecisions({ steps: flatSteps });
  assert.ok(flatDecisions.length >= 1 && flatDecisions.length <= 3);
  // Must NOT produce false peak or false decrease claims on flat data
  assert.ok(!flatDecisions.some((d) => d.id === 'peak'));
  assert.ok(!flatDecisions.some((d) => d.id === 'drop'));
  const range = flatDecisions.find((d) => d.id === 'range' || d.id === 'steady');
  assert.ok(range);
});

test('5. Invalid numeric values and zero denominators are suppressed safely', () => {
  const badSteps = [
    { step: 1, month: 'Aug', monthLabel: 'August 2026', value: 0, lower: NaN, upper: Infinity },
    { step: 2, month: 'Sep', monthLabel: 'September 2026', value: 50000, lower: 40000, upper: 60000 },
  ];
  // Prior is 0: should not produce Infinity% or NaN%
  const decisions = buildForecastDecisions({ steps: badSteps });
  for (const d of decisions) {
    assert.ok(!d.finding.includes('NaN'));
    assert.ok(!d.finding.includes('Infinity'));
  }

  // Model handles NaN without crash
  const chart = buildOverviewChartModel({
    width: 500,
    history: [{ label: 'M0', month: 'Jul', value: 10000 }],
    steps: badSteps,
    dataSource,
  });
  assert.ok(chart);
  assert.ok(!chart.historyPath.includes('NaN'));
  assert.ok(!chart.forecastPath.includes('NaN'));
});

test('6. Tied peaks select first occurrence deterministically', () => {
  const tiedSteps = [
    { step: 1, month: 'Aug', monthLabel: 'August 2026', value: 100000, lower: 80000, upper: 120000 },
    { step: 2, month: 'Sep', monthLabel: 'September 2026', value: 200000, lower: 180000, upper: 220000 },
    { step: 3, month: 'Oct', monthLabel: 'October 2026', value: 150000, lower: 130000, upper: 170000 },
    { step: 4, month: 'Nov', monthLabel: 'November 2026', value: 200000, lower: 180000, upper: 220000 },
  ];
  const decisions = buildForecastDecisions({ steps: tiedSteps });
  const peak = decisions.find((d) => d.id === 'peak');
  assert.ok(peak);
  // Must pick step 2 (September), not step 4
  assert.ok(peak.finding.includes('September peak'));
  assert.equal(JSON.stringify(peak.stepNumbers), JSON.stringify([1, 2]));
});

test('7. Missing or reversed bounds are normalized safely', () => {
  const reversedSteps = [
    { step: 1, month: 'Aug', monthLabel: 'August 2026', value: 150000, lower: 200000, upper: 100000 }, // lower > upper
    { step: 2, month: 'Sep', monthLabel: 'September 2026', value: 160000, lower: undefined, upper: null },
  ];
  const decisions = buildForecastDecisions({ steps: reversedSteps });
  const range = decisions.find((d) => d.id === 'range');
  assert.ok(range);
  assert.equal(range.evidence.type, 'range');
  // Bounds should be normalized so lower <= upper
  assert.ok(range.evidence.lower <= range.evidence.upper);
  assert.equal(range.evidence.lower, 100000);
  assert.equal(range.evidence.upper, 200000);

  const chart = buildOverviewChartModel({
    width: 600,
    history: [{ label: 'M0', month: 'Jul', value: 140000 }],
    steps: reversedSteps,
    dataSource,
  });
  assert.ok(chart);
  assert.ok(!chart.bandPath.includes('NaN'));
});

test('8. Monotonically increasing data does not invent a decline after horizon', () => {
  const risingSteps = [
    { step: 1, month: 'Aug', monthLabel: 'August 2026', value: 100000, lower: 80000, upper: 120000 },
    { step: 2, month: 'Sep', monthLabel: 'September 2026', value: 120000, lower: 100000, upper: 140000 },
    { step: 3, month: 'Oct', monthLabel: 'October 2026', value: 150000, lower: 130000, upper: 170000 },
    { step: 4, month: 'Nov', monthLabel: 'November 2026', value: 180000, lower: 160000, upper: 200000 },
  ];
  const decisions = buildForecastDecisions({ steps: risingSteps });
  const drop = decisions.find((d) => d.id === 'drop');
  // Must NOT invent a decline!
  assert.equal(drop, undefined);
  assert.ok(decisions.some((d) => d.id === 'peak'));
});

test('9. Recommendation selection highlights exact related forecast step points', () => {
  const chart = buildOverviewChartModel({
    width: 800,
    history: sampleHistory,
    steps: sampleSteps,
    dataSource,
    selectedRecStepNumbers: [4, 5], // Peak recommendation covers Nov (step 4) and Dec (step 5)
    recKind: 'increase',
  });

  assert.ok(chart);
  assert.ok(chart.highlightRegion);
  assert.equal(chart.highlightRegion.color, '#126c5f');
  assert.equal(chart.highlightRegion.points.length, 2);
  assert.ok(chart.highlightRegion.width > 0);
  assert.ok(chart.highlightRegion.x >= chart.plotLeft);
});
