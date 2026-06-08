<template>
    <AppShell
        active-label="Dashboard"
        eyebrow="Owner DSS"
        title="Decision Support Overview"
        description="Business status, decision priorities, and Sprint 1 forecast readiness from mock travel operations data."
    >
        <section class="dss-page">
            <div class="dss-toolbar">
                <div class="segment-control" aria-label="Date range">
                    <button
                        v-for="range in ranges"
                        :key="range"
                        type="button"
                        :class="{ active: selectedRange === range }"
                        @click="selectedRange = range"
                    >
                        {{ range }}
                    </button>
                </div>
                <div class="system-status">
                    <span class="status-dot"></span>
                    Local Intranet Active
                    <span>Last updated {{ lastUpdated }}</span>
                </div>
            </div>

            <section class="business-gist">
                <div>
                    <span class="insight-label">DSS Insight Summary</span>
                    <h2>Sales are above last month's pace, but operating costs are increasing.</h2>
                    <p>
                        Recommended action: review high-cost destinations before approving new promos.
                    </p>
                </div>
                <a class="primary-button" href="/trajectory-insights">
                    <AppIcon name="sparkles" />
                    Review Insights
                </a>
            </section>

            <section class="stat-grid dss-kpi-grid dashboard-kpi-grid" aria-label="Dashboard KPIs">
                <StatCard
                    v-for="stat in kpiCards"
                    :key="stat.label"
                    :icon="stat.icon"
                    :label="stat.label"
                    :value="stat.value"
                    :note="stat.note"
                    :status="stat.status"
                    :tone="stat.tone"
                />
            </section>

            <section class="decision-grid">
                <article v-for="decision in priorityDecisions" :key="decision.title" class="decision-card">
                    <span class="record-badge" :class="statusClass(decision.type)">{{ decision.type }}</span>
                    <h3>{{ decision.title }}</h3>
                    <p><strong>Finding:</strong> {{ decision.finding }}</p>
                    <p><strong>Meaning:</strong> {{ decision.meaning }}</p>
                    <a class="secondary-button compact-button" :href="decision.href">
                        <AppIcon name="arrowRight" />
                        {{ decision.action }}
                    </a>
                </article>
            </section>

            <section class="dss-grid two-column">
                <ContentPanel icon="fileBarChart" eyebrow="Sales" title="Sales Trend" badge="Mock data">
                    <div class="mini-chart">
                        <div v-for="booking in recentBookings" :key="booking.id" class="chart-row">
                            <span>{{ booking.ds.slice(5) }}</span>
                            <div><i :style="{ width: chartWidth(booking.grossRevenue, maxRevenue) }"></i></div>
                            <strong>{{ formatCurrency(booking.grossRevenue) }}</strong>
                        </div>
                    </div>
                </ContentPanel>

                <ContentPanel icon="wallet" eyebrow="Profit Watch" title="Revenue Vs Expenses" badge="DSS view">
                    <div class="comparison-stack">
                        <div>
                            <span>Revenue</span>
                            <strong>{{ formatCurrency(totalRevenue) }}</strong>
                            <div class="progress-track"><i :style="{ width: '100%' }"></i></div>
                        </div>
                        <div>
                            <span>Expenses</span>
                            <strong>{{ formatCurrency(totalExpenses) }}</strong>
                            <div class="progress-track warning"><i :style="{ width: chartWidth(totalExpenses, totalRevenue) }"></i></div>
                        </div>
                    </div>
                    <p class="panel-note no-indent">
                        Observed data -> costs are rising beside revenue. Business meaning -> profit needs monitoring before promo approvals.
                    </p>
                </ContentPanel>
            </section>

            <section class="dss-grid three-column">
                <ContentPanel icon="mapPinned" eyebrow="Packages" title="Top Performing Packages">
                    <div class="ranking-list">
                        <div v-for="item in topDestinations.slice(0, 4)" :key="item.destination">
                            <span>{{ item.destination }}</span>
                            <strong>{{ formatCurrency(item.revenue) }}</strong>
                            <small>{{ item.passengers }} passengers</small>
                        </div>
                    </div>
                </ContentPanel>

                <ContentPanel icon="sparkles" eyebrow="Forecasting" title="Forecast Preview" badge="Sample Forecast Preview">
                    <p class="placeholder-note">Forecast engine integration pending.</p>
                    <div class="forecast-mini">
                        <div v-for="point in forecastProjection.slice(0, 4)" :key="point.date">
                            <span>{{ point.date }}</span>
                            <strong>{{ point.bookings }}</strong>
                            <small>projected bookings</small>
                        </div>
                    </div>
                </ContentPanel>

                <ContentPanel icon="boxes" eyebrow="Inventory" title="Recent Inventory Changes">
                    <div class="compact-list">
                        <a v-for="item in inventory.slice(0, 5)" :key="item.id" href="/inventory">
                            <span>{{ item.packageName }}</span>
                            <strong>{{ item.availableSlots }} slots</strong>
                            <small class="record-badge compact-status" :class="statusClass(item.status)">{{ item.status }}</small>
                        </a>
                    </div>
                </ContentPanel>
            </section>

            <ContentPanel icon="database" eyebrow="Transactions" title="Recent Transactions" badge="5 rows">
                <div class="table-scroll">
                    <table class="dss-table">
                        <thead>
                            <tr>
                                <th>Booking</th>
                                <th>Client</th>
                                <th>Package</th>
                                <th>Passengers</th>
                                <th>Revenue</th>
                                <th>Status</th>
                            </tr>
                        </thead>
                        <tbody>
                            <tr v-for="booking in recentBookings.slice(0, 5)" :key="booking.id">
                                <td><strong>{{ booking.id }}</strong><span>{{ booking.ds }}</span></td>
                                <td>{{ booking.client }}</td>
                                <td>{{ booking.package }}</td>
                                <td>{{ booking.y }}</td>
                                <td>{{ formatCurrency(booking.grossRevenue) }}</td>
                                <td><span class="record-badge" :class="statusClass(booking.bookingStatus)">{{ booking.bookingStatus }}</span></td>
                            </tr>
                        </tbody>
                    </table>
                </div>
            </ContentPanel>
        </section>
    </AppShell>
</template>

<script>
import AppShell from '../Components/layout/AppShell.vue';
import ContentPanel from '../Components/dashboard/ContentPanel.vue';
import StatCard from '../Components/dashboard/StatCard.vue';
import AppIcon from '../Components/icons/AppIcon.vue';
import {
    bookings,
    expenses,
    forecastProjection,
    formatCurrency,
    formatNumber,
    groupBookingsByDestination,
    inventory,
    totalBy,
} from '../data/mockData';

export default {
    name: 'Welcome',
    components: {
        AppIcon,
        AppShell,
        ContentPanel,
        StatCard,
    },
    data() {
        return {
            bookings,
            expenses,
            forecastProjection,
            inventory,
            ranges: ['Today', 'This Month', 'This Quarter'],
            selectedRange: 'This Month',
            lastUpdated: '8:30 AM',
        };
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
        kpiCards() {
            return [
                { icon: 'wallet', label: 'Total Revenue', value: formatCurrency(this.totalRevenue), note: 'Gross revenue from mock bookings', status: 'Above pace', tone: 'primary' },
                { icon: 'database', label: 'Total Bookings', value: formatNumber(this.totalBookings), note: 'Confirmed, reserved, and pending records', status: 'Active', tone: 'data' },
                { icon: 'users', label: 'Passenger Count', value: formatNumber(this.passengerCount), note: 'Demand value for future forecasting', status: 'Forecast input', tone: 'success' },
                { icon: 'wallet', label: 'Operating Costs', value: formatCurrency(this.totalExpenses), note: 'Tour, marketing, seasonal, overhead', status: 'Watch', tone: 'warning' },
                { icon: 'fileBarChart', label: 'Estimated Profit', value: formatCurrency(this.estimatedProfit), note: 'Revenue minus recorded expenses', status: 'Estimate', tone: 'primary' },
                { icon: 'boxes', label: 'Low Inventory Packages', value: String(this.lowInventoryCount), note: 'Low or Critical package capacity', status: 'Review', tone: 'warning' },
            ];
        },
        priorityDecisions() {
            return [
                {
                    type: 'Risk',
                    title: 'Low Inventory Risk',
                    finding: 'Boracay Package has only 4 slots left.',
                    meaning: 'This may affect expected demand next week.',
                    action: 'Review Inventory',
                    href: '/inventory',
                },
                {
                    type: 'Warning',
                    title: 'Cost Increase Watch',
                    finding: 'Marketing and seasonal costs are rising this period.',
                    meaning: 'Spending may reduce estimated profit if bookings slow.',
                    action: 'Review Expenses',
                    href: '/expenses',
                },
                {
                    type: 'Opportunity',
                    title: 'Cebu Demand Opportunity',
                    finding: 'Cebu Heritage Tour has strong passenger volume.',
                    meaning: 'This package may support a controlled promo push.',
                    action: 'View Analytics',
                    href: '/analytics',
                },
            ];
        },
        recentBookings() {
            return [...this.bookings].sort((a, b) => b.ds.localeCompare(a.ds));
        },
        maxRevenue() {
            return Math.max(...this.bookings.map((booking) => booking.grossRevenue));
        },
        topDestinations() {
            return groupBookingsByDestination(this.bookings);
        },
    },
    methods: {
        formatCurrency,
        chartWidth(value, max) {
            return `${Math.max(8, Math.round((value / max) * 100))}%`;
        },
        statusClass(value) {
            return `status-${String(value).toLowerCase().replace(/[^a-z0-9]+/g, '-')}`;
        },
    },
};
</script>
