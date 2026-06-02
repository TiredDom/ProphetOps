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
                eyebrow="Admin Workspace"
                title="Welcome back, Administrator"
                description="Centralized operational data foundation for Renan-Tina Travels and Tours."
                :current-date="currentDate"
                :sidebar-open="sidebarOpen"
                @toggle-sidebar="toggleSidebar"
            />

            <section class="welcome-hero">
                <div class="hero-content">
                    <div class="hero-main">
                        <div class="hero-kicker">
                            <div class="hero-status">
                                <span class="status-dot"></span>
                                System Ready
                            </div>
                        </div>
                        <h2>Operational Data Workspace</h2>
                        <p>
                            Centralize internal records, validate data quality, and prepare clean inputs for reports and
                            future forecasting.
                        </p>
                        <div class="hero-actions">
                            <button class="primary-button" type="button">
                                <AppIcon name="plus" />
                                Add Record
                            </button>
                            <button class="secondary-button" type="button">
                                <AppIcon name="shieldCheck" />
                                Review Data
                            </button>
                        </div>
                    </div>

                    <div class="hero-priority" aria-label="Next dashboard priority">
                        <span class="priority-label">Next Priority</span>
                        <strong>Add the first operational record</strong>
                        <p>Start with one clean entry so validation, summaries, and future forecasts have a reliable base.</p>
                    </div>
                </div>

                <div class="hero-summary" aria-label="Data foundation summary">
                    <div class="summary-header">
                        <span>System Readiness</span>
                        <strong>Internal DSS</strong>
                    </div>
                    <div class="summary-row">
                        <span>Data Intake</span>
                        <strong>Ready</strong>
                    </div>
                    <div class="summary-row">
                        <span>Validation Flow</span>
                        <strong>Structured</strong>
                    </div>
                    <div class="summary-row">
                        <span>Forecasting</span>
                        <strong>Planned</strong>
                    </div>
                </div>
            </section>

            <section class="stat-grid" aria-label="Operational summary">
                <StatCard
                    v-for="stat in stats"
                    :key="stat.label"
                    :icon="stat.icon"
                    :label="stat.label"
                    :value="stat.value"
                    :note="stat.note"
                    :status="stat.status"
                    :tone="stat.tone"
                />
            </section>

            <section class="workspace-grid">
                <ContentPanel
                    icon="database"
                    eyebrow="Data Workspace"
                    title="Operational Records Intake"
                    badge="Data Intake"
                    panel-class="records-panel"
                >
                    <div class="intake-list">
                        <button v-for="source in sourceChannels" :key="source.label" class="intake-item" type="button">
                            <div class="source-icon">
                                <AppIcon :name="source.icon" />
                            </div>
                            <div>
                                <strong>{{ source.label }}</strong>
                                <p>{{ source.description }}</p>
                                <span class="source-status">{{ source.status }}</span>
                            </div>
                            <AppIcon class-name="source-arrow" name="arrowRight" />
                        </button>
                    </div>

                    <EmptyState
                        icon="database"
                        title="No records encoded yet"
                    message="Start by adding an operational record from Messenger, Sheets, Gmail, or paper notes."
                        action-label="Add Record"
                    />
                </ContentPanel>

                <ContentPanel
                    icon="shieldCheck"
                    eyebrow="Validation Flow"
                    title="Data Quality Status"
                    panel-class="quality-panel"
                >
                    <p class="panel-note">
                        Records move through these statuses after intake. Validated records become reliable inputs for reports and future forecasting.
                    </p>
                    <div class="quality-stack">
                        <div v-for="status in qualityStatuses" :key="status.label" class="quality-row">
                            <div>
                                <span>{{ status.label }}</span>
                                <p>{{ status.description }}</p>
                            </div>
                            <strong>{{ status.value }}</strong>
                        </div>
                    </div>
                </ContentPanel>

                <ContentPanel
                    icon="check"
                    eyebrow="Setup Progress"
                    title="Data Foundation Checklist"
                    panel-class="checklist-panel"
                >
                    <div class="checklist">
                        <div
                            v-for="step in setupSteps"
                            :key="step.label"
                            class="checklist-item"
                            :class="{ complete: step.complete }"
                        >
                            <span class="checklist-marker" aria-hidden="true">
                                <AppIcon :name="step.complete ? 'check' : 'arrowRight'" />
                            </span>
                            <div>
                                <strong>{{ step.label }}</strong>
                                <p>{{ step.description }}</p>
                            </div>
                        </div>
                    </div>
                </ContentPanel>

                <ContentPanel icon="fileBarChart" eyebrow="Reports" title="Basic Summaries" panel-class="report-panel">
                    <div class="report-list">
                        <span>Sales Summary</span>
                        <span>Expense Summary</span>
                        <span>Inventory Summary</span>
                        <span>Data Quality Summary</span>
                    </div>
                </ContentPanel>
            </section>
        </main>
    </div>
</template>

<script>
import ContentPanel from '../Components/dashboard/ContentPanel.vue';
import StatCard from '../Components/dashboard/StatCard.vue';
import EmptyState from '../Components/feedback/EmptyState.vue';
import AppIcon from '../Components/icons/AppIcon.vue';
import Sidebar from '../Components/layout/Sidebar.vue';
import TopBar from '../Components/layout/TopBar.vue';

export default {
    name: 'Welcome',
    components: {
        AppIcon,
        ContentPanel,
        EmptyState,
        Sidebar,
        StatCard,
        TopBar,
    },
    data() {
        return {
            sidebarOpen: false,
            currentDate: new Intl.DateTimeFormat('en-US', {
                month: 'short',
                day: 'numeric',
                year: 'numeric',
            }).format(new Date()),
            navigationGroups: [
                {
                    label: 'Overview',
                    items: [{ label: 'Dashboard', icon: 'dashboard', active: true }],
                },
                {
                    label: 'Data Workspace',
                    items: [
                        { label: 'Operational Records', icon: 'database' },
                        { label: 'Data Validation', icon: 'shieldCheck' },
                    ],
                },
                {
                    label: 'Business Records',
                    items: [
                        { label: 'Package / Destination References', icon: 'mapPinned' },
                        { label: 'Expenses', icon: 'wallet' },
                    ],
                },
                {
                    label: 'Operations',
                    items: [{ label: 'Inventory', icon: 'boxes' }],
                },
                {
                    label: 'Reports',
                    items: [{ label: 'Reports', icon: 'fileBarChart' }],
                },
                {
                    label: 'Analytics',
                    items: [{ label: 'Forecasting', icon: 'sparkles', locked: true }],
                },
                {
                    label: 'Administration',
                    items: [
                        { label: 'Users', icon: 'users' },
                        { label: 'Settings', icon: 'settings' },
                    ],
                },
            ],
            stats: [
                {
                    icon: 'wallet',
                    label: 'Total Sales',
                    value: '₱0.00',
                    note: 'No validated records yet',
                    status: 'Empty',
                    tone: 'primary',
                },
                {
                    icon: 'database',
                    label: 'Operational Records',
                    value: '0',
                    note: 'Start by adding an operational record',
                    status: 'Ready',
                    tone: 'data',
                },
                {
                    icon: 'fileBarChart',
                    label: 'Expenses',
                    value: '₱0.00',
                    note: 'No cost records encoded yet',
                    status: 'Empty',
                    tone: 'warning',
                },
                {
                    icon: 'boxes',
                    label: 'Low Stock',
                    value: '0',
                    note: 'Inventory alerts will appear here',
                    status: 'Clear',
                    tone: 'success',
                },
            ],
            sourceChannels: [
                {
                    icon: 'message',
                    label: 'Messenger',
                    description: 'Daily inquiries and historical notes',
                    status: 'Ready for manual intake',
                },
                {
                    icon: 'sheet',
                    label: 'Google Sheets',
                    description: 'Existing sales and expense records',
                    status: 'No records imported yet',
                },
                {
                    icon: 'mail',
                    label: 'Gmail',
                    description: 'Email-based confirmations and references',
                    status: 'Ready for manual intake',
                },
                {
                    icon: 'notebook',
                    label: 'Notebooks',
                    description: 'Paper-based operational entries',
                    status: 'No records encoded yet',
                },
            ],
            qualityStatuses: [
                { label: 'Draft', value: '0', description: 'New or incomplete entries' },
                { label: 'Needs Review', value: '0', description: 'Records waiting for checking' },
                { label: 'Validated', value: '0', description: 'Clean records for reports' },
                { label: 'Archived', value: '0', description: 'Historical records kept for reference' },
            ],
            setupSteps: [
                {
                    label: 'Add operational records',
                    description: 'Create the first clean internal data entries.',
                    complete: false,
                },
                {
                    label: 'Validate data quality',
                    description: 'Move records from draft to validated status.',
                    complete: false,
                },
                {
                    label: 'Set package / destination references',
                    description: 'Keep destinations consistent across records.',
                    complete: false,
                },
                {
                    label: 'Encode expenses',
                    description: 'Track operational and marketing costs.',
                    complete: false,
                },
                {
                    label: 'Review basic reports',
                    description: 'Check summaries once records are available.',
                    complete: false,
                },
            ],
        };
    },
    mounted() {
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
    },
};
</script>
