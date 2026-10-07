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

const { buildForecastTimeline, buildForecastChartModel } = loadHelper();

const history = Array.from({ length: 12 }, (_, index) => ({
  label: `M-${11 - index}`,
  month: ['Oct', 'Nov', 'Dec', 'Jan', 'Feb', 'Mar', 'Apr', 'May', 'Jun', 'Jul', 'Aug', 'Sep'][index],
  value: 120000 + index * 2500,
}));

const steps = [
  { step: 1, month: 'Oct', monthLabel: 'October 2026', value: 148500, lower: 136000, upper: 161000 },
  { step: 2, month: 'Nov', monthLabel: 'November 2026', value: 153000, lower: 139000, upper: 167000 },
  { step: 3, month: 'Dec', monthLabel: 'December 2026', value: 168000, lower: 151000, upper: 185000 },
  { step: 4, month: 'Jan', monthLabel: 'January 2027', value: 159000, lower: 140000, upper: 178000 },
  { step: 5, month: 'Feb', monthLabel: 'February 2027', value: 156000, lower: 135000, upper: 177000 },
  { step: 6, month: 'Mar', monthLabel: 'March 2027', value: 182000, lower: 158000, upper: 206000 },
];

test('buildForecastTimeline derives reliable calendar labels across year boundary', () => {
  const timeline = buildForecastTimeline(history, steps, { lastRecordedMonth: 'September 2026' });

  assert.equal(timeline[0].label, 'October 2025');
  assert.equal(timeline[11].label, 'September 2026');
  assert.equal(timeline[12].label, 'October 2026');
  assert.equal(timeline[15].label, 'January 2027');
  assert.equal(timeline[17].label, 'March 2027');
});

test('buildForecastTimeline offsets history from first forecast step when live source omits last recorded month', () => {
  const deferredSteps = [
    { step: 3, month: 'Dec', monthLabel: 'December 2026', value: 168000, lower: 151000, upper: 185000 },
    { step: 4, month: 'Jan', monthLabel: 'January 2027', value: 159000, lower: 140000, upper: 178000 },
  ];
  const timeline = buildForecastTimeline(history, deferredSteps, { lastRecordedMonth: null });

  assert.equal(timeline[0].label, 'October 2025');
  assert.equal(timeline[11].label, 'September 2026');
  assert.equal(timeline[12].label, 'December 2026');
});

test('buildForecastTimeline preserves individual valid forecast month labels', () => {
  const nonContiguousSteps = [
    { step: 1, month: 'Oct', monthLabel: 'October 2026', value: 148500, lower: 136000, upper: 161000 },
    { step: 2, month: 'Dec', monthLabel: 'December 2026', value: 153000, lower: 139000, upper: 167000 },
    { step: 3, month: 'Bad', monthLabel: 'Not a month', value: 168000, lower: 151000, upper: 185000 },
  ];
  const timeline = buildForecastTimeline(history, nonContiguousSteps, { lastRecordedMonth: 'September 2026' });

  assert.equal(timeline[12].label, 'October 2026');
  assert.equal(timeline[13].label, 'December 2026');
  assert.equal(timeline[14].label, 'Not a month');
});

test('buildForecastTimeline gracefully falls back when labels are malformed', () => {
  const timeline = buildForecastTimeline(
    [{ label: 'M0', month: 'Sep', value: 145000 }],
    [{ step: 1, month: 'Oct', monthLabel: 'Not a month', value: 148500, lower: 136000, upper: 161000 }],
    { lastRecordedMonth: 'Bad Label' },
  );

  assert.equal(timeline[0].label, 'Sep');
  assert.equal(timeline[1].label, 'Not a month');
  assert.ok(!timeline.some((point) => point.label.includes('Invalid')));
});

test('buildForecastChartModel preserves exact forecast values and bounds', () => {
  const chart = buildForecastChartModel({ width: 820, history, steps, dataSource: { lastRecordedMonth: 'September 2026' } });

  assert.ok(chart);
  const november = chart.timeline.find((point) => point.label === 'November 2026');
  assert.equal(november.value, 153000);
  assert.equal(november.lower, 139000);
  assert.equal(november.upper, 167000);
  assert.equal(chart.forecastMarkers.length, 6);
});

test('buildForecastChartModel places divider between history and first forecast', () => {
  const chart = buildForecastChartModel({ width: 820, history, steps, dataSource: { lastRecordedMonth: 'September 2026' } });

  assert.ok(chart);
  const lastHistory = chart.historyMarkers.at(-1);
  const firstForecast = chart.forecastMarkers[0];
  assert.ok(chart.divider > lastHistory.x);
  assert.ok(chart.divider < firstForecast.x);
});

function estimatedBounds(label) {
  const estimatedWidth = label.text.length * 6.4;
  if (label.anchor === 'start') return { left: label.x, right: label.x + estimatedWidth };
  if (label.anchor === 'end') return { left: label.x - estimatedWidth, right: label.x };
  return { left: label.x - estimatedWidth / 2, right: label.x + estimatedWidth / 2 };
}

test('buildForecastChartModel keeps narrow chart ticks and labels contained', () => {
  for (const width of [220, 240, 290, 320, 390]) {
    const chart = buildForecastChartModel({ width, history, steps, dataSource: { lastRecordedMonth: 'September 2026' } });

    assert.ok(chart);
    assert.equal(chart.width, width);
    assert.equal(chart.height, 240);
    assert.ok(chart.xLabels.some((label) => label.text === 'Oct 25'));
    assert.ok(chart.xLabels.some((label) => label.text === 'Mar 27'));
    assert.equal(chart.transitionLabel, 'Recorded through September 2026; forecast starts October 2026.');
    for (const label of chart.xLabels) {
      assert.ok(label.x >= chart.plotLeft);
      assert.ok(label.x <= chart.plotRight);
    }
    for (let i = 1; i < chart.xLabels.length; i += 1) {
      const previous = estimatedBounds(chart.xLabels[i - 1]);
      const current = estimatedBounds(chart.xLabels[i]);
      assert.ok(previous.right + 8 <= current.left, `${width}px labels overlap: ${chart.xLabels[i - 1].text} / ${chart.xLabels[i].text}`);
    }
  }
});

test('buildForecastChartModel avoids invalid geometry for flat or tiny ranges', () => {
  const flatHistory = history.map((point) => ({ ...point, value: 100000 }));
  const flatSteps = steps.map((step) => ({ ...step, value: 100000, lower: 100000, upper: 100000 }));
  const chart = buildForecastChartModel({ width: 390, history: flatHistory, steps: flatSteps, dataSource: { lastRecordedMonth: 'September 2026' } });

  assert.ok(chart);
  const numericValues = [
    ...chart.historyMarkers.flatMap((point) => [point.x, point.y]),
    ...chart.forecastMarkers.flatMap((point) => [point.x, point.y]),
    chart.divider,
  ];
  for (const value of numericValues) {
    assert.ok(Number.isFinite(value));
  }
  assert.ok(!chart.historyPoints.includes('NaN'));
  assert.ok(!chart.forecastPoints.includes('NaN'));
  assert.ok(!chart.band.includes('NaN'));
});
