<template>
    <AppShell
        active-label="Dashboard"
        eyebrow="Decision Support"
        title="Decision Support Overview"
        description="Business status, package priorities, and owner-ready decision guidance."
    >
        <section class="dss-page">
            <div class="dss-toolbar">
                <div class="system-status">
                    <span class="status-dot"></span>
                    Local Intranet Active
                    <span>Last updated {{ lastUpdated }}</span>
                </div>
            </div>

            <DecisionSignal
                class="dashboard-prescription-signal"
                badge="Owner Review"
                tone="primary"
                icon="fileBarChart"
                :meta="['High priority', `Updated ${lastUpdated}`]"
                title="Use the package guide before approving the next offer push."
                description="Capacity, cost, budget fit, and business value should be reviewed before promoting more package offers."
                :chips="dashboardSignalChips"
                action-label="Open Guide"
                action-href="/decision-guide"
            />

            <section class="dashboard-summary-strip" aria-label="Essential dashboard measures">
                <a
                    v-for="metric in essentialMetrics"
                    :key="metric.label"
                    class="dashboard-summary-item"
                    :class="`dashboard-summary-item-${metric.tone}`"
                    :href="metric.href"
                >
                    <span class="dashboard-summary-label">
                        <AppIcon :name="metric.icon" />
                        {{ metric.label }}
                    </span>
                    <strong>{{ metric.value }}</strong>
                    <small>{{ metric.note }}</small>
                </a>
            </section>

            <ContentPanel icon="fileBarChart" eyebrow="Review Queue" title="Next Reviews" badge="3 priorities">
                <div class="dashboard-review-list">
                    <a
                        v-for="decision in priorityDecisions"
                        :key="decision.finding"
                        class="dashboard-review-row"
                        :href="insightHref(decision)"
                    >
                        <span class="record-badge" :class="statusClass(decision.type)">{{ decision.type }}</span>
                        <strong>{{ decision.category }}</strong>
                        <p>{{ decisionSummary(decision) }}</p>
                        <small>{{ decision.horizon }}</small>
                        <AppIcon name="arrowRight" />
                    </a>
                </div>
            </ContentPanel>

            <ContentPanel icon="boxes" eyebrow="Package Catalog" title="Recent Capacity Changes" :badge="`${lowInventoryCount} watch`">
                <div class="compact-list">
                    <a v-for="item in inventory.slice(0, 5)" :key="item.id" href="/inventory">
                        <span>{{ item.packageName }}</span>
                        <strong>{{ item.availableSlots }} slots</strong>
                        <small class="record-badge compact-status" :class="statusClass(item.status)">{{ item.status }}</small>
                    </a>
                </div>
            </ContentPanel>
        </section>
    </AppShell>
</template>

<script>
import AppShell from '../Components/layout/AppShell.vue';
import DecisionSignal from '../Components/dss/DecisionSignal.vue';
import ContentPanel from '../Components/dashboard/ContentPanel.vue';
import AppIcon from '../Components/icons/AppIcon.vue';
import { formatCurrency, formatNumber, totalBy } from '../utils/formatters';

export default {
    name: 'Welcome',
    components: {
        AppIcon,
        AppShell,
        DecisionSignal,
        ContentPanel,
    },
    props: {
        bookings: {
            type: Array,
            default: () => [],
        },
        expenses: {
            type: Array,
            default: () => [],
        },
        inventory: {
            type: Array,
            default: () => [],
        },
        decisionReviewSignals: {
            type: Array,
            default: () => [],
        },
        lastUpdated: {
            type: String,
            default: '',
        },
    },
    computed: {
        totalRevenue() {
            return totalBy(this.bookings, 'grossRevenue');
        },
        totalBookings() {
            return this.bookings.length;
        },
        passengerCount() {
            return totalBy(this.bookings, 'y');
        },
        totalExpenses() {
            return totalBy(this.expenses, 'amount');
        },
        estimatedProfit() {
            return this.totalRevenue - this.totalExpenses;
        },
        lowInventoryCount() {
            return this.inventory.filter((item) => ['Low', 'Critical'].includes(item.status)).length;
        },
        dashboardSignalChips() {
            return [
                'Decision-ready records',
                `${formatCurrency(this.estimatedProfit)} estimated profit`,
                `${this.lowInventoryCount} capacity watch`,
            ];
        },
        essentialMetrics() {
            return [
                { icon: 'wallet', label: 'Revenue', value: formatCurrency(this.totalRevenue), note: 'Booked revenue', tone: 'primary', href: '/bookings' },
                { icon: 'fileBarChart', label: 'Profit', value: formatCurrency(this.estimatedProfit), note: 'After recorded costs', tone: 'primary', href: '/expenses' },
                { icon: 'boxes', label: 'Capacity watch', value: String(this.lowInventoryCount), note: 'Low or critical packages', tone: 'warning', href: '/inventory' },
                { icon: 'database', label: 'Bookings', value: formatNumber(this.totalBookings), note: `${formatNumber(this.passengerCount)} passengers`, tone: 'data', href: '/bookings' },
            ];
        },
        priorityDecisions() {
            return this.decisionReviewSignals.slice(0, 3);
        },
    },
    methods: {
        formatCurrency,
        insightHref(decision) {
            if (decision.category === 'Capacity risk') {
                return '/inventory';
            }

            if (decision.category === 'Cost risk') {
                return '/expenses';
            }

            return '/decision-guide';
        },
        decisionSummary(decision) {
            const summaries = {
                'Capacity risk': 'Boracay capacity is low while demand is rising.',
                'Cost risk': 'Check rising costs against booking conversion.',
                'Demand increase': 'Cebu demand is strong and may support a controlled promo.',
                'Sales trend': 'Revenue is improving, but costs and capacity still need monitoring.',
            };

            return summaries[decision.category] || decision.finding;
        },
        statusClass(value) {
            return `status-${String(value).toLowerCase().replace(/[^a-z0-9]+/g, '-')}`;
        },
    },
};
</script>
