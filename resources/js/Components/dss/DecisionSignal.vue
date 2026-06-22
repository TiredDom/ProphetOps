<template>
    <article class="decision-signal" :class="signalClasses">
        <div class="decision-signal-icon" v-if="icon">
            <AppIcon :name="icon" />
        </div>

        <div class="decision-signal-body">
            <div class="decision-signal-header">
                <span class="decision-signal-badge">{{ badge }}</span>
                <span v-if="metaText" class="decision-signal-meta">{{ metaText }}</span>
            </div>

            <component :is="headingTag" class="decision-signal-title">{{ title }}</component>

            <p v-if="description" class="decision-signal-description">{{ description }}</p>

            <div v-if="chips.length" class="decision-signal-chips">
                <span v-for="chip in chips" :key="chip">{{ chip }}</span>
            </div>

            <slot name="details" />
        </div>

        <div v-if="hasActions" class="decision-signal-actions">
            <slot name="actions">
                <a v-if="actionHref" class="primary-button compact-button" :href="actionHref">
                    <AppIcon :name="actionIcon" />
                    {{ actionLabel }}
                </a>
                <button v-else class="primary-button compact-button" type="button" @click="$emit('action')">
                    <AppIcon :name="actionIcon" />
                    {{ actionLabel }}
                </button>

                <a v-if="secondaryActionHref && secondaryActionLabel" class="secondary-button compact-button" :href="secondaryActionHref">
                    {{ secondaryActionLabel }}
                </a>
            </slot>
        </div>
    </article>
</template>

<script>
import AppIcon from '../icons/AppIcon.vue';

export default {
    name: 'DecisionSignal',
    components: { AppIcon },
    emits: ['action'],
    props: {
        actionHref: {
            type: String,
            default: '',
        },
        actionIcon: {
            type: String,
            default: 'arrowRight',
        },
        actionLabel: {
            type: String,
            default: '',
        },
        badge: {
            type: String,
            default: 'Decision Signal',
        },
        chips: {
            type: Array,
            default: () => [],
        },
        compact: {
            type: Boolean,
            default: false,
        },
        description: {
            type: String,
            default: '',
        },
        embedded: {
            type: Boolean,
            default: false,
        },
        headingTag: {
            type: String,
            default: 'h2',
        },
        icon: {
            type: String,
            default: 'sparkles',
        },
        meta: {
            type: Array,
            default: () => [],
        },
        secondaryActionHref: {
            type: String,
            default: '',
        },
        secondaryActionLabel: {
            type: String,
            default: '',
        },
        title: {
            type: String,
            required: true,
        },
        tone: {
            type: String,
            default: 'primary',
        },
    },
    computed: {
        hasActions() {
            return Boolean(this.actionLabel || this.$slots.actions);
        },
        metaText() {
            return this.meta.filter(Boolean).join(' / ');
        },
        signalClasses() {
            return [
                `decision-signal-${this.tone}`,
                {
                    'decision-signal-compact': this.compact,
                    'decision-signal-embedded': this.embedded,
                },
            ];
        },
    },
};
</script>
