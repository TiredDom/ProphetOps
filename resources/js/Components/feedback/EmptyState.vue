<template>
    <div class="empty-state">
        <span class="empty-state-icon" aria-hidden="true">
            <AppIcon :name="icon" />
        </span>
        <div>
            <h4>{{ title }}</h4>
            <p>{{ message }}</p>
        </div>
        <button v-if="actionLabel" class="empty-state-action" type="button" @click="$emit('action')">
            <AppIcon name="plus" />
            {{ actionLabel }}
        </button>
    </div>
</template>

<script>
import AppIcon from '../icons/AppIcon.vue';

export default {
    name: 'EmptyState',
    components: {
        AppIcon,
    },
    props: {
        icon: {
            type: String,
            default: 'database',
        },
        title: {
            type: String,
            required: true,
        },
        message: {
            type: String,
            required: true,
        },
        actionLabel: {
            type: String,
            default: '',
        },
    },
    emits: ['action'],
};
</script>
