<template>
    <article class="stat-card" :class="`stat-card-${tone}`">
        <div class="stat-card-top">
            <span class="stat-icon" aria-hidden="true">
                <AppIcon :name="icon" />
            </span>
            <span class="stat-status">{{ status }}</span>
        </div>
        <p>{{ label }}</p>
        <strong>{{ value }}</strong>
        <span class="stat-note">{{ note }}</span>
    </article>
</template>

<script>
import AppIcon from '../icons/AppIcon.vue';

export default {
    name: 'StatCard',
    components: {
        AppIcon,
    },
    props: {
        icon: {
            type: String,
            required: true,
        },
        label: {
            type: String,
            required: true,
        },
        value: {
            type: String,
            required: true,
        },
        note: {
            type: String,
            required: true,
        },
        status: {
            type: String,
            required: true,
        },
        tone: {
            type: String,
            default: 'primary',
        },
    },
};
</script>
