<template>
    <div
        class="line-trend-chart"
        :class="{
            'line-trend-chart-compact': compact,
            'line-trend-chart-no-axis': !showAxis,
            'line-trend-chart-with-y-axis': showYAxis,
        }"
    >
        <svg class="line-chart-svg" viewBox="0 0 1000 320" preserveAspectRatio="none" role="img" :aria-label="ariaLabel">
            <defs>
                <linearGradient id="forecastConfidenceFill" x1="0" x2="0" y1="0" y2="1">
                    <stop offset="0%" stop-color="rgba(79, 70, 229, 0.22)" />
                    <stop offset="100%" stop-color="rgba(79, 70, 229, 0.04)" />
                </linearGradient>
            </defs>

            <g v-if="showAxis" class="line-chart-grid">
                <line
                    v-for="line in gridLines"
                    :key="line.y"
                    :x1="plotPadding.left"
                    :x2="width - plotPadding.right"
                    :y1="line.y"
                    :y2="line.y"
                />
            </g>

            <path v-if="confidencePath" class="line-chart-confidence" :d="confidencePath" />

            <line
                v-if="showForecastMarker && forecastStartX"
                class="line-chart-marker"
                :x1="forecastStartX"
                :x2="forecastStartX"
                :y1="plotPadding.top"
                :y2="height - plotPadding.bottom"
            />

            <path v-if="actualPath" class="line-chart-path line-chart-path-actual" :d="actualPath" />
            <path v-if="forecastPath" class="line-chart-path line-chart-path-forecast" :d="forecastPath" />

            <circle
                v-for="point in actualPlotPoints"
                :key="`actual-${point.index}`"
                class="line-chart-dot line-chart-dot-actual"
                :cx="point.x"
                :cy="point.y"
                r="5"
            />
            <circle
                v-for="point in forecastPlotPoints"
                :key="`forecast-${point.index}`"
                class="line-chart-dot line-chart-dot-forecast"
                :cx="point.x"
                :cy="point.y"
                r="5"
            />
        </svg>

        <div v-if="showYAxis" class="line-chart-y-axis" aria-hidden="true">
            <span
                v-for="line in yAxisLines"
                :key="line.label"
                :style="{ top: `${(line.y / height) * 100}%` }"
            >
                {{ line.label }}
            </span>
        </div>

        <div v-if="showAxis" class="line-chart-axis" :style="axisStyle">
            <span
                v-for="(point, index) in points"
                :key="`${point[labelKey]}-${index}`"
                :class="{ visible: visibleAxisIndexes.includes(index) }"
            >
                {{ point[labelKey] }}
            </span>
        </div>
    </div>
</template>

<script>
export default {
    name: 'LineTrendChart',
    props: {
        points: {
            type: Array,
            required: true,
        },
        actualKey: {
            type: String,
            default: 'actual',
        },
        forecastKey: {
            type: String,
            default: 'forecast',
        },
        lowerKey: {
            type: String,
            default: 'lower',
        },
        upperKey: {
            type: String,
            default: 'upper',
        },
        labelKey: {
            type: String,
            default: 'label',
        },
        phaseKey: {
            type: String,
            default: 'phase',
        },
        forecastPhase: {
            type: String,
            default: 'forecast',
        },
        ariaLabel: {
            type: String,
            default: 'Line trend chart',
        },
        compact: {
            type: Boolean,
            default: false,
        },
        showAxis: {
            type: Boolean,
            default: true,
        },
        showConfidence: {
            type: Boolean,
            default: true,
        },
        showForecastMarker: {
            type: Boolean,
            default: true,
        },
        showYAxis: {
            type: Boolean,
            default: false,
        },
        yAxisFormatter: {
            type: Function,
            default: (value) => value,
        },
    },
    data() {
        return {
            width: 1000,
            height: 320,
            padding: {
                top: 18,
                right: 30,
                bottom: 40,
                left: 34,
            },
        };
    },
    computed: {
        axisStyle() {
            return {
                gridTemplateColumns: `repeat(${Math.max(this.points.length, 1)}, minmax(0, 1fr))`,
            };
        },
        allValues() {
            return this.points.flatMap((point) => [
                point[this.actualKey],
                point[this.forecastKey],
                point[this.lowerKey],
                point[this.upperKey],
            ]).filter((value) => this.isPlottableValue(value)).map(Number);
        },
        yMax() {
            return Math.max(...this.allValues, 1);
        },
        yMin() {
            return Math.min(0, ...this.allValues);
        },
        yRange() {
            return Math.max(this.yMax - this.yMin, 1);
        },
        plotPadding() {
            return {
                ...this.padding,
                left: this.showYAxis ? 118 : this.padding.left,
            };
        },
        gridLines() {
            const plotHeight = this.height - this.plotPadding.top - this.plotPadding.bottom;

            return [0, 0.25, 0.5, 0.75, 1].map((step) => ({
                step,
                y: this.plotPadding.top + plotHeight * step,
            }));
        },
        yAxisLines() {
            return this.gridLines
                .filter((line) => [0, 0.5, 1].includes(line.step))
                .map((line) => {
                    const value = this.yMax - this.yRange * line.step;

                    return {
                        ...line,
                        label: this.yAxisFormatter(Math.round(value)),
                    };
                });
        },
        actualPlotPoints() {
            return this.plotPointsForKey(this.actualKey);
        },
        forecastPlotPoints() {
            return this.plotPointsForKey(this.forecastKey);
        },
        actualPath() {
            return this.pathFromPoints(this.actualPlotPoints);
        },
        forecastPath() {
            return this.pathFromPoints(this.forecastPlotPoints);
        },
        forecastStartIndex() {
            const byPhase = this.points.findIndex((point) => point[this.phaseKey] === this.forecastPhase);

            if (byPhase >= 0) {
                return byPhase;
            }

            return this.points.findIndex((point) => point[this.actualKey] === null && point[this.forecastKey] !== null);
        },
        forecastStartX() {
            return this.forecastStartIndex >= 0 ? this.xForIndex(this.forecastStartIndex) : null;
        },
        confidencePath() {
            if (!this.showConfidence) {
                return '';
            }

            const startIndex = this.forecastStartIndex >= 0 ? this.forecastStartIndex : 0;
            const confidencePoints = this.points
                .map((point, index) => ({ point, index }))
                .filter(({ point, index }) =>
                    index >= startIndex &&
                    this.isPlottableValue(point[this.lowerKey]) &&
                    this.isPlottableValue(point[this.upperKey]),
                );

            if (confidencePoints.length < 2) {
                return '';
            }

            const upperPath = confidencePoints.map(({ point, index }) => ({
                x: this.xForIndex(index),
                y: this.yForValue(point[this.upperKey]),
            }));
            const lowerPath = confidencePoints.slice().reverse().map(({ point, index }) => ({
                x: this.xForIndex(index),
                y: this.yForValue(point[this.lowerKey]),
            }));
            const areaPoints = [...upperPath, ...lowerPath];

            return `${this.pathFromPoints(areaPoints)} Z`;
        },
        visibleAxisIndexes() {
            const lastIndex = this.points.length - 1;

            if (lastIndex <= 5) {
                return this.points.map((_, index) => index);
            }

            return this.points
                .map((_, index) => index)
                .filter((index) => index === 0 || index === lastIndex || index % 2 === 0);
        },
    },
    methods: {
        xForIndex(index) {
            const plotWidth = this.width - this.plotPadding.left - this.plotPadding.right;
            const denominator = Math.max(this.points.length - 1, 1);

            return this.plotPadding.left + (plotWidth * index) / denominator;
        },
        yForValue(value) {
            const plotHeight = this.height - this.plotPadding.top - this.plotPadding.bottom;
            const normalized = (Number(value) - this.yMin) / this.yRange;

            return this.height - this.plotPadding.bottom - normalized * plotHeight;
        },
        isPlottableValue(value) {
            return value !== null && value !== undefined && value !== '' && Number.isFinite(Number(value));
        },
        plotPointsForKey(key) {
            return this.points
                .map((point, index) => {
                    const value = point[key];

                    if (!this.isPlottableValue(value)) {
                        return null;
                    }

                    return {
                        index,
                        x: this.xForIndex(index),
                        y: this.yForValue(value),
                    };
                })
                .filter(Boolean);
        },
        pathFromPoints(points) {
            return points
                .map((point, index) => `${index === 0 ? 'M' : 'L'} ${point.x.toFixed(2)} ${point.y.toFixed(2)}`)
                .join(' ');
        },
    },
};
</script>
