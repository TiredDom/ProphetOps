<template>
    <div class="mini-bar-chart" :class="{ 'mini-bar-chart-compact': compact }">
        <div v-for="item in normalizedItems" :key="item.key" class="mini-bar-row">
            <span>{{ item.label }}</span>
            <div class="mini-bar-track">
                <i :style="{ width: item.width }"></i>
            </div>
            <strong>{{ item.displayValue }}</strong>
        </div>
    </div>
</template>

<script>
export default {
    name: 'MiniBarChart',
    props: {
        items: {
            type: Array,
            required: true,
        },
        labelKey: {
            type: String,
            default: 'label',
        },
        valueKey: {
            type: String,
            default: 'value',
        },
        max: {
            type: Number,
            default: 0,
        },
        compact: {
            type: Boolean,
            default: false,
        },
        valueFormatter: {
            type: Function,
            default: (value) => value,
        },
    },
    computed: {
        maxValue() {
            return this.max || Math.max(...this.items.map((item) => Number(item[this.valueKey]) || 0), 1);
        },
        normalizedItems() {
            return this.items.map((item, index) => {
                const value = Number(item[this.valueKey]) || 0;
                const width = `${Math.max(5, Math.round((value / this.maxValue) * 100))}%`;

                return {
                    key: item.id || item[this.labelKey] || index,
                    label: item[this.labelKey],
                    width,
                    displayValue: this.valueFormatter(value, item),
                };
            });
        },
    },
};
</script>
