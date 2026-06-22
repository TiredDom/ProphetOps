<template>
    <section v-if="selectedCount" class="bulk-action-bar" aria-live="polite">
        <div class="bulk-action-summary">
            <strong>{{ selectedCount }} selected</strong>
            <span>{{ description }}</span>
        </div>
        <div class="bulk-action-controls">
            <button
                v-for="action in actions"
                :key="action.key"
                class="secondary-button compact-button"
                type="button"
                :disabled="action.disabled"
                @click="$emit('run-action', action.key)"
            >
                <AppIcon :name="action.icon || 'check'" />
                {{ action.label }}
            </button>
            <button class="secondary-button compact-button" type="button" @click="$emit('clear')">
                <AppIcon name="x" />
                Clear
            </button>
        </div>
    </section>
</template>

<script>
import AppIcon from '../icons/AppIcon.vue';

export default {
    name: 'BulkActionBar',
    components: {
        AppIcon,
    },
    props: {
        selectedCount: {
            type: Number,
            default: 0,
        },
        description: {
            type: String,
            default: 'Apply an action to the selected records.',
        },
        actions: {
            type: Array,
            default: () => [],
        },
    },
    emits: ['clear', 'run-action'],
};
</script>
