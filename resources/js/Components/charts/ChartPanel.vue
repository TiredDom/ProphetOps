<template>
    <article class="content-panel chart-panel" :class="{ 'chart-panel-compact': compact }">
        <div class="panel-header">
            <div class="panel-title-group">
                <span v-if="icon" class="panel-icon" aria-hidden="true">
                    <AppIcon :name="icon" />
                </span>
                <div>
                    <p class="eyebrow">{{ eyebrow }}</p>
                    <component :is="headingTag">{{ title }}</component>
                </div>
            </div>
            <div v-if="$slots.action" class="chart-panel-action">
                <slot name="action" />
            </div>
        </div>

        <slot name="before" />

        <div class="chart-frame" :style="frameStyle">
            <slot />
        </div>

        <div v-if="legend.length" class="chart-panel-legend">
            <span v-for="item in legend" :key="item.label">
                <i :class="`chart-legend-dot chart-legend-dot-${item.tone || 'primary'}`"></i>
                {{ item.label }}
            </span>
        </div>

        <p v-if="caption" class="chart-caption">{{ caption }}</p>

        <slot name="after" />
    </article>
</template>

<script>
import AppIcon from '../icons/AppIcon.vue';

export default {
    name: 'ChartPanel',
    components: {
        AppIcon,
    },
    props: {
        icon: {
            type: String,
            default: '',
        },
        eyebrow: {
            type: String,
            required: true,
        },
        title: {
            type: String,
            required: true,
        },
        caption: {
            type: String,
            default: '',
        },
        compact: {
            type: Boolean,
            default: false,
        },
        height: {
            type: [Number, String],
            default: 300,
        },
        legend: {
            type: Array,
            default: () => [],
        },
        headingTag: {
            type: String,
            default: 'h2',
        },
    },
    computed: {
        frameStyle() {
            const height = Number.isFinite(Number(this.height)) ? `${Number(this.height)}px` : this.height;

            return {
                '--chart-frame-height': height,
            };
        },
    },
};
</script>
