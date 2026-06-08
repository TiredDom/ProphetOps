<template>
    <div class="dashboard-shell" :class="{ 'sidebar-open': sidebarOpen }">
        <button class="sidebar-overlay" type="button" aria-label="Close navigation" @click="closeSidebar"></button>

        <Sidebar
            :navigation-groups="navigationGroups"
            :is-open="sidebarOpen"
            @item-selected="closeSidebar"
        />

        <main class="main-panel">
            <TopBar
                :eyebrow="eyebrow"
                :title="title"
                :description="description"
                :current-date="currentDate"
                :sidebar-open="sidebarOpen"
                :user-name="user?.name || 'ProphetOps User'"
                :user-role="user?.role || 'Prototype'"
                @toggle-sidebar="toggleSidebar"
                @logout="logout"
            />

            <slot />
        </main>
    </div>
</template>

<script>
import Sidebar from './Sidebar.vue';
import TopBar from './TopBar.vue';
import { createNavigationGroups } from '../../data/navigation';
import {
    clearMockAuthSession,
    getMockUser,
    requirePageAccess,
} from '../../services/mockAuth';

export default {
    name: 'AppShell',
    components: {
        Sidebar,
        TopBar,
    },
    props: {
        activeLabel: {
            type: String,
            required: true,
        },
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
    },
    data() {
        return {
            sidebarOpen: false,
            user: null,
            currentDate: new Intl.DateTimeFormat('en-US', {
                month: 'short',
                day: 'numeric',
                year: 'numeric',
            }).format(new Date()),
        };
    },
    computed: {
        navigationGroups() {
            return createNavigationGroups(this.activeLabel, this.user?.role);
        },
    },
    mounted() {
        if (!requirePageAccess(this.activeLabel)) {
            return;
        }

        this.user = getMockUser();
        window.addEventListener('keydown', this.handleKeydown);
    },
    beforeUnmount() {
        window.removeEventListener('keydown', this.handleKeydown);
    },
    methods: {
        toggleSidebar() {
            this.sidebarOpen = !this.sidebarOpen;
        },
        closeSidebar() {
            this.sidebarOpen = false;
        },
        handleKeydown(event) {
            if (event.key === 'Escape') {
                this.closeSidebar();
            }
        },
        logout() {
            clearMockAuthSession();
            window.location.href = '/login';
        },
    },
};
</script>
