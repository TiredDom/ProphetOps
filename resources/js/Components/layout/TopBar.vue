<template>
    <header class="topbar">
        <div class="topbar-title-row">
            <button
                class="menu-button"
                type="button"
                :aria-expanded="sidebarOpen ? 'true' : 'false'"
                aria-controls="app-sidebar"
                aria-label="Open navigation"
                @click="$emit('toggle-sidebar')"
            >
                <span></span>
                <span></span>
                <span></span>
            </button>
            <div>
                <p class="eyebrow">{{ eyebrow }}</p>
                <h1>{{ title }}</h1>
                <p class="topbar-description">{{ description }}</p>
            </div>
        </div>

        <div class="topbar-actions">
            <button class="date-pill" type="button">
                <span class="button-icon" aria-hidden="true">
                    <AppIcon name="calendar" />
                </span>
                {{ currentDate }}
            </button>
            <button class="icon-button" type="button" aria-label="View alerts">
                <AppIcon name="bell" />
            </button>
            <button class="profile-button" type="button">
                <span class="profile-avatar">A</span>
                <span>Admin</span>
            </button>
        </div>
    </header>
</template>

<script>
import AppIcon from '../icons/AppIcon.vue';

export default {
    name: 'TopBar',
    components: {
        AppIcon,
    },
    props: {
        eyebrow: {
            type: String,
            required: true,
        },
        title: {
            type: String,
            required: true,
        },
        description: {
            type: String,
            required: true,
        },
        currentDate: {
            type: String,
            required: true,
        },
        sidebarOpen: {
            type: Boolean,
            default: false,
        },
    },
    emits: ['toggle-sidebar'],
};
</script>
