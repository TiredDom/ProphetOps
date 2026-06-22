<template>
    <div class="donut-chart" role="img" :aria-label="chartLabel">
        <div class="donut-ring" :style="{ background: gradientStyle }">
            <div class="donut-hole">
                <span>{{ centerLabel }}</span>
                <strong>{{ centerValue }}</strong>
            </div>
        </div>

        <div class="donut-legend">
            <div v-for="item in normalizedItems" :key="item.key" class="donut-legend-item">
                <i :style="{ background: item.color }"></i>
                <span>{{ item.label }}</span>
                <strong>{{ item.percentLabel }}</strong>
                <small v-if="item.valueLabel">{{ item.valueLabel }}</small>
            </div>
        </div>
    </div>
</template>

<script>
const toneColors = {
    primary: '#4F46E5',
    forecast: '#4F46E5',
    warning: '#B45309',
    success: '#15803D',
    data: '#0F766E',
    muted: '#CBD5E1',
};

export default {
    name: 'DonutChart',
    props: {
        ariaLabel: {
            type: String,
            default: '',
        },
        centerLabel: {
            type: String,
            default: 'Total',
        },
        centerValue: {
            type: String,
            default: '',
        },
        items: {
            type: Array,
            required: true,
        },
    },
    computed: {
        total() {
            return this.items.reduce((sum, item) => sum + (Number(item.value) || 0), 0);
        },
        normalizedItems() {
            return this.items.map((item, index) => {
                const value = Number(item.value) || 0;
                const ratio = this.total ? value / this.total : 0;

                return {
                    key: item.id || item.label || index,
                    label: item.label,
                    value,
                    valueLabel: item.valueLabel || '',
                    color: item.color || toneColors[item.tone] || toneColors.primary,
                    percent: ratio * 100,
                    percentLabel: `${Math.round(ratio * 100)}%`,
                };
            });
        },
        gradientStyle() {
            if (!this.total) {
                return '#E5E7EB';
            }

            let start = 0;
            const segments = this.normalizedItems.map((item) => {
                const end = start + item.percent;
                const segment = `${item.color} ${start.toFixed(2)}% ${end.toFixed(2)}%`;
                start = end;
                return segment;
            });

            return `conic-gradient(${segments.join(', ')})`;
        },
        chartLabel() {
            if (this.ariaLabel) {
                return this.ariaLabel;
            }

            return this.normalizedItems
                .map((item) => `${item.label} ${item.percentLabel}`)
                .join(', ');
        },
    },
};
</script>
