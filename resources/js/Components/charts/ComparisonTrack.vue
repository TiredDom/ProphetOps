<template>
    <div class="comparison-track-list" :class="{ 'comparison-track-list-compact': compact }">
        <div v-for="item in normalizedItems" :key="item.key" class="comparison-track-item">
            <div class="comparison-track-header">
                <span>{{ item.label }}</span>
                <strong>{{ item.displayValue }}</strong>
            </div>
            <div class="comparison-track">
                <i :class="`comparison-track-fill comparison-track-fill-${item.tone}`" :style="{ width: item.width }"></i>
            </div>
        </div>
    </div>
</template>

<script>
export default {
    name: 'ComparisonTrack',
    props: {
        items: {
            type: Array,
            required: true,
        },
        compact: {
            type: Boolean,
            default: false,
        },
    },
    computed: {
        normalizedItems() {
            return this.items.map((item, index) => {
                const max = Number(item.max) || Math.max(Number(item.value) || 0, 1);
                const value = Number(item.value) || 0;

                return {
                    key: item.key || item.label || index,
                    label: item.label,
                    displayValue: item.valueLabel || value,
                    tone: item.tone || 'primary',
                    width: `${Math.max(5, Math.round((value / max) * 100))}%`,
                };
            });
        },
    },
};
</script>
