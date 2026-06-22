<template>
    <div class="action-notice" :class="`action-notice-${tone}`" :role="statusRole" :aria-live="ariaLive">
        <span class="action-notice-icon" aria-hidden="true">
            <AppIcon :name="iconName" />
        </span>
        <div>
            <strong v-if="title">{{ title }}</strong>
            <p>{{ message }}</p>
        </div>
    </div>
</template>

<script>
import AppIcon from '../icons/AppIcon.vue';

export default {
    name: 'ActionNotice',
    components: {
        AppIcon,
    },
    props: {
        tone: {
            type: String,
            default: 'info',
            validator: (value) => ['info', 'success', 'warning', 'error'].includes(value),
        },
        title: {
            type: String,
            default: '',
        },
        message: {
            type: String,
            required: true,
        },
    },
    computed: {
        iconName() {
            return {
                error: 'alertTriangle',
                success: 'check',
                warning: 'alertTriangle',
                info: 'message',
            }[this.tone] || 'message';
        },
        statusRole() {
            return this.tone === 'error' ? 'alert' : 'status';
        },
        ariaLive() {
            return this.tone === 'error' ? 'assertive' : 'polite';
        },
    },
};
</script>
