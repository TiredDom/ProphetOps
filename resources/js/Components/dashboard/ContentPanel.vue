<template>
    <article class="content-panel" :class="panelClass">
        <div class="panel-header">
            <div class="panel-title-group">
                <span v-if="icon" class="panel-icon" aria-hidden="true">
                    <AppIcon :name="icon" />
                </span>
                <div>
                    <p class="eyebrow">{{ eyebrow }}</p>
                    <h3>{{ title }}</h3>
                </div>
            </div>
            <span v-if="badge" class="status-badge">{{ badge }}</span>
        </div>

        <slot />
    </article>
</template>

<script>
import AppIcon from '../icons/AppIcon.vue';

export default {
    name: 'ContentPanel',
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
        badge: {
            type: String,
            default: '',
        },
        panelClass: {
            type: String,
            default: '',
        },
    },
};
</script>
