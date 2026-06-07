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
                description="Decision support workspace for operational records, reports, and future forecasting readiness."
                :current-date="currentDate"
                :sidebar-open="sidebarOpen"
                @toggle-sidebar="toggleSidebar"
            />

            <section class="welcome-hero dss-hero">
                <div class="hero-content dss-overview">
                    <div class="hero-main dss-hero-main">
                        <div class="hero-kicker">
                            <div class="hero-status">
                                <span class="status-dot"></span>
                                Decision Support System
                            </div>
                        </div>
                        <h2>ProphetOps Decision Support Workspace</h2>
                        <p>
                            Transform scattered operational records into reports, forecasting inputs, and
                            decision-ready insights.
                        </p>
                        <div class="hero-actions">
                            <Link class="primary-button" href="/data/operational-records">
                                <AppIcon name="plus" />
                                Add Operational Record
                            </Link>
                            <Link class="secondary-button" href="/data/validation">
                                <AppIcon name="shieldCheck" />
                                Review Data
                            </Link>
                        </div>
                    </div>

                    <div class="hero-priority dss-next-action" aria-label="Next dashboard priority">
                        <span class="priority-label">Next Best Action</span>
                        <strong>Add the first operational record</strong>
                        <p>Start with one clean entry so reports and future forecasts have a reliable base.</p>
                    </div>

                    <div class="dss-pipeline" aria-label="Decision support system flow">
                        <article
                            v-for="step in dssPipeline"
                            :key="step.label"
                            class="pipeline-card"
                            :class="{ planned: step.planned }"
                        >
                            <span class="pipeline-icon" aria-hidden="true">
                                <AppIcon :name="step.icon" />
                            </span>
                            <div>
                                <strong>{{ step.label }}</strong>
                                <p>{{ step.description }}</p>
                            </div>
                            <span class="record-badge" :class="step.badgeClass">{{ step.status }}</span>
                        </article>
                    </div>
                </div>

                <div class="hero-summary" aria-label="Data foundation status">
                    <div class="summary-header">
                        <span>DSS Readiness</span>
                        <strong>Foundation View</strong>
                    </div>
                    <div v-for="item in systemReadiness" :key="item.label" class="summary-row">
                        <span>{{ item.label }}</span>
                        <strong>{{ item.status }}</strong>
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
                    icon="sparkles"
                    eyebrow="Management Decision Support"
                    title="Decision Support Preview"
                    badge="Planned Insight"
                    panel-class="records-panel decision-panel"
                >
                    <p class="panel-note no-indent">
                        The dashboard turns clean internal data into practical signals for management review.
                    </p>
                    <div class="decision-list">
                        <div v-for="item in decisionPreview" :key="item.label" class="decision-item">
                            <span class="decision-icon" :class="item.tone" aria-hidden="true">
                                <AppIcon :name="item.icon" />
                            </span>
                            <div>
                                <strong>{{ item.label }}</strong>
                                <p>{{ item.description }}</p>
                            </div>
                        </div>
                    </div>
                </ContentPanel>

                <ContentPanel
                    icon="shieldCheck"
                    eyebrow="Validation Flow"
                    title="Data Quality Status"
                    panel-class="quality-panel"
                >
                    <p class="panel-note">
                        Records move through these statuses after intake. Clean records become reliable inputs for reports and later forecasting.
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
                    eyebrow="Current Focus"
                    title="Data Foundation Progress"
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

                <ContentPanel icon="fileBarChart" eyebrow="Reports" title="Report And Forecast Readiness" panel-class="report-panel">
                    <div class="report-list">
                        <span v-for="item in reportReadiness" :key="item.label">
                            {{ item.label }}
                            <strong>{{ item.status }}</strong>
                        </span>
                    </div>
                </ContentPanel>
            </section>
        </main>
    </div>
</template>

<script>
import { Link } from '@inertiajs/vue3';
import ContentPanel from '../Components/dashboard/ContentPanel.vue';
import StatCard from '../Components/dashboard/StatCard.vue';
import AppIcon from '../Components/icons/AppIcon.vue';
import Sidebar from '../Components/layout/Sidebar.vue';
import TopBar from '../Components/layout/TopBar.vue';
import { createNavigationGroups } from '../data/navigation';
import { requireMockAuth } from '../services/mockAuth';

export default {
    name: 'Welcome',
    components: {
        AppIcon,
        ContentPanel,
        Link,
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
            navigationGroups: createNavigationGroups('Dashboard'),
            stats: [
                {
                    icon: 'wallet',
                    label: 'Total Sales',
                    value: 'PHP 0.00',
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
                    value: 'PHP 0.00',
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
            dssPipeline: [
                {
                    icon: 'database',
                    label: 'Data Intake',
                    description: 'Messenger, Sheets, Gmail, notebooks, and paper records',
                    status: 'Ready',
                    badgeClass: 'status-validated',
                },
                {
                    icon: 'shieldCheck',
                    label: 'Data Validation',
                    description: 'Check missing or unclear information',
                    status: 'Structured',
                    badgeClass: 'status-validated',
                },
                {
                    icon: 'wallet',
                    label: 'Business Monitoring',
                    description: 'Sales, expenses, and inventory summaries',
                    status: 'Basic',
                    badgeClass: 'status-basic',
                },
                {
                    icon: 'fileBarChart',
                    label: 'Forecasting Preparation',
                    description: 'Clean records for Meta Prophet',
                    status: 'Planned',
                    badgeClass: 'status-planned',
                    planned: true,
                },
                {
                    icon: 'sparkles',
                    label: 'Trajectory Insights',
                    description: 'Future decision support from forecast trends',
                    status: 'Locked',
                    badgeClass: 'status-locked',
                    planned: true,
                },
            ],
            systemReadiness: [
                { label: 'Data Intake', status: 'Ready' },
                { label: 'Validation Flow', status: 'Structured' },
                { label: 'Reports', status: 'Basic' },
                { label: 'Forecasting', status: 'Planned' },
                { label: 'Trajectory Insights', status: 'Planned' },
            ],
            qualityStatuses: [
                { label: 'Draft', value: '0', description: 'New or incomplete entries' },
                { label: 'Needs Checking', value: '0', description: 'Records waiting for review' },
                { label: 'Ready for Reports', value: '0', description: 'Clean records for summaries' },
                { label: 'Archived', value: '0', description: 'Historical records kept for reference' },
            ],
            decisionPreview: [
                {
                    icon: 'fileBarChart',
                    label: 'Sales trend visibility',
                    description: 'See whether internal sales records are ready for review.',
                    tone: 'data',
                },
                {
                    icon: 'wallet',
                    label: 'Expense monitoring',
                    description: 'Keep operating costs visible beside sales summaries.',
                    tone: 'warning',
                },
                {
                    icon: 'boxes',
                    label: 'Inventory awareness',
                    description: 'Spot low stock and resource gaps before daily work is affected.',
                    tone: 'success',
                },
                {
                    icon: 'shieldCheck',
                    label: 'Forecasting readiness',
                    description: 'Know which records are clean enough for later forecasting.',
                    tone: 'data',
                },
                {
                    icon: 'sparkles',
                    label: 'Management decision support',
                    description: 'Prepare future insight views without showing fake forecasts.',
                    tone: 'primary',
                },
            ],
            setupSteps: [
                {
                    label: 'Add operational records',
                    description: 'Create the first clean internal data entries.',
                    complete: false,
                },
                {
                    label: 'Validate records',
                    description: 'Move records from draft to ready for reports.',
                    complete: false,
                },
                {
                    label: 'Encode expenses',
                    description: 'Track operational and marketing costs.',
                    complete: false,
                },
                {
                    label: 'Track inventory',
                    description: 'Monitor items, stock movements, and low stock alerts.',
                    complete: false,
                },
                {
                    label: 'Review basic reports',
                    description: 'Check summaries once records are available.',
                    complete: false,
                },
            ],
            reportReadiness: [
                { label: 'Sales Summary', status: 'Basic' },
                { label: 'Expense Summary', status: 'Basic' },
                { label: 'Inventory Summary', status: 'Basic' },
                { label: 'Forecasting Inputs', status: 'Planned' },
                { label: 'Trajectory Insights', status: 'Planned' },
            ],
        };
    },
    mounted() {
        if (!requireMockAuth()) {
            return;
        }

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
