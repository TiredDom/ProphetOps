<template>
    <div class="dashboard-shell" :class="{ 'sidebar-open': sidebarOpen }">
        <Head :title="title" />

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
                :user-role="user?.role || 'User'"
                @toggle-sidebar="toggleSidebar"
                @open-alerts="activeModal = 'alerts'"
                @open-help="activeModal = 'help'"
                @open-profile="activeModal = 'profile'"
                @open-date="activeModal = 'date'"
                @request-logout="activeModal = 'logout'"
            />

            <slot />
        </main>

        <AppModal
            v-if="activeModal === 'alerts'"
            eyebrow="Workspace Alerts"
            title="Priority Notices"
            @close="activeModal = ''"
        >
            <div class="modal-list">
                <DecisionSignal
                    v-for="alert in alerts"
                    :key="alert.title"
                    :badge="alert.type"
                    :tone="signalTone(alert.type)"
                    :meta="[alert.priority]"
                    :title="alert.title"
                    :description="alert.message"
                    :chips="alert.chips"
                    compact
                    embedded
                    heading-tag="h3"
                />
            </div>
        </AppModal>

        <AppModal
            v-if="activeModal === 'profile'"
            eyebrow="Access"
            title="Current Role"
            @close="activeModal = ''"
        >
            <div class="profile-modal-summary">
                <span class="profile-avatar large">{{ userInitials }}</span>
                <div>
                    <strong>{{ user?.name || 'ProphetOps User' }}</strong>
                    <p>{{ user?.email || 'Internal account' }}</p>
                    <span class="record-badge status-active">{{ user?.role || 'User' }}</span>
                </div>
            </div>
            <div class="modal-section">
                <p class="modal-section-title">Visible pages</p>
                <div class="modal-chip-list">
                    <span v-for="page in accessiblePages" :key="page">{{ page }}</span>
                </div>
            </div>
            <p class="placeholder-note">Only pages available to this role are shown.</p>
        </AppModal>

        <AppModal
            v-if="activeModal === 'help'"
            eyebrow="Workspace Guide"
            title="How to read ProphetOps"
            @close="activeModal = ''"
        >
            <div class="modal-section">
                <p class="modal-section-title">Decision signals</p>
                <p class="modal-copy">Signals connect observed records to a business meaning and a review action. They are decision support, not automated approvals.</p>
            </div>
            <div class="modal-section">
                <p class="modal-section-title">Session-scoped actions</p>
                <p class="modal-copy">Record saves write to the system database, exports use prepared report views, and role sessions stay active until logout.</p>
            </div>
            <div class="modal-section">
                <p class="modal-section-title">Fast controls</p>
                <div class="modal-chip-list">
                    <span>Esc closes dialogs</span>
                    <span>Tables scroll horizontally</span>
                    <span>Selected rows unlock batch actions</span>
                    <span>? opens this guide</span>
                </div>
            </div>
            <template #footer>
                <button class="primary-button" type="button" @click="activeModal = ''">Got it</button>
            </template>
        </AppModal>

        <AppModal
            v-if="activeModal === 'date'"
            eyebrow="Workspace Date"
            title="Current Planning Window"
            @close="activeModal = ''"
        >
            <div class="modal-section">
                <p class="modal-section-title">Today</p>
                <div class="profile-modal-summary">
                    <span class="profile-avatar large">
                        <AppIcon name="calendar" />
                    </span>
                    <div>
                        <strong>{{ currentDate }}</strong>
                        <p>Used as the workspace reference date for records, signals, and decision review.</p>
                    </div>
                </div>
            </div>
            <div class="modal-section">
                <p class="modal-section-title">Planning Context</p>
                <div class="modal-chip-list">
                    <span>Current month</span>
                    <span>Package review window</span>
                    <span>Role-based access</span>
                </div>
            </div>
            <template #footer>
                <button class="primary-button" type="button" @click="activeModal = ''">Done</button>
            </template>
        </AppModal>

        <AppModal
            v-if="activeModal === 'logout'"
            eyebrow="Session"
            title="Log out of ProphetOps?"
            @close="activeModal = ''"
        >
            <p class="modal-copy">Your current session will be cleared on this device.</p>
            <template #footer>
                <button class="secondary-button" type="button" @click="activeModal = ''">Stay signed in</button>
                <button class="primary-button" type="button" @click="logout">
                    <AppIcon name="logOut" />
                    Log out
                </button>
            </template>
        </AppModal>
    </div>
</template>

<script>
import { Head, router } from '@inertiajs/vue3';
import AppIcon from '../icons/AppIcon.vue';
import AppModal from '../feedback/AppModal.vue';
import DecisionSignal from '../dss/DecisionSignal.vue';
import Sidebar from './Sidebar.vue';
import TopBar from './TopBar.vue';
import { createNavigationGroups } from '../../data/navigation';

const appName = import.meta.env.VITE_APP_NAME || 'ProphetOps';

export default {
    name: 'AppShell',
    components: {
        AppIcon,
        AppModal,
        DecisionSignal,
        Head,
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
            default: '',
        },
    },
    data() {
        return {
            activeModal: '',
            sidebarOpen: false,
            currentDate: new Intl.DateTimeFormat('en-US', {
                month: 'short',
                day: 'numeric',
                year: 'numeric',
            }).format(new Date()),
        };
    },
    computed: {
        user() {
            return this.$page.props.auth?.user || null;
        },
        navigationGroups() {
            return createNavigationGroups(this.activeLabel, this.user?.role);
        },
        accessiblePages() {
            return this.$page.props.auth?.permissions || [];
        },
        userInitials() {
            return this.user?.name
                ?.split(' ')
                .filter(Boolean)
                .slice(0, 2)
                .map((name) => name[0])
                .join('')
                .toUpperCase() || 'PO';
        },
        alerts() {
            return [
                {
                    type: 'Risk',
                    priority: 'High priority',
                    title: 'Package capacity needs review',
                    message: 'Review Boracay and Palawan capacity before accepting more demand.',
                    chips: ['Capacity watch', 'Boracay', 'Palawan'],
                },
                {
                    type: 'Action',
                    priority: 'Guide review',
                    title: 'Package guide is ready',
                    message: 'Review the package guide before approving new promotions.',
                    chips: ['Decision guide', 'Promo review'],
                },
                {
                    type: 'Warning',
                    priority: 'Cost watch',
                    title: 'Cost monitoring signal',
                    message: 'Marketing and seasonal costs are rising beside revenue.',
                    chips: ['Marketing', 'Seasonal cost'],
                },
            ];
        },
    },
    watch: {
        title() {
            this.syncDocumentTitle();
        },
    },
    mounted() {
        this.syncDocumentTitle();
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
                this.activeModal = '';
                this.closeSidebar();
            }

            if (event.key === '?' && !event.altKey && !event.ctrlKey && !event.metaKey) {
                const target = event.target;
                const isTyping = ['INPUT', 'SELECT', 'TEXTAREA'].includes(target?.tagName);

                if (!isTyping) {
                    this.activeModal = 'help';
                }
            }
        },
        logout() {
            router.post('/logout');
        },
        syncDocumentTitle() {
            document.title = `${this.title} - ${appName}`;
        },
        signalTone(type) {
            return String(type).toLowerCase().replace(/[^a-z0-9]+/g, '-');
        },
    },
};
</script>
