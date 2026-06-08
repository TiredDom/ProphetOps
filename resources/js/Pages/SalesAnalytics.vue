<template>
    <AppShell
        active-label="Analytics"
        eyebrow="Analysis"
        title="Sales Analytics"
        description="Basic business analysis before full forecasting or AI interpretation."
    >
        <section class="dss-page">
            <section class="stat-grid dss-kpi-grid">
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

            <section class="dss-grid two-column">
                <ContentPanel icon="fileBarChart" eyebrow="Monthly Sales" title="Revenue By Booking Date" badge="Mock data">
                    <div class="mini-chart">
                        <div v-for="booking in bookings" :key="booking.id" class="chart-row">
                            <span>{{ booking.ds.slice(5) }}</span>
                            <div><i :style="{ width: chartWidth(booking.grossRevenue, maxRevenue) }"></i></div>
                            <strong>{{ formatCurrency(booking.grossRevenue) }}</strong>
                        </div>
                    </div>
                </ContentPanel>

                <ContentPanel icon="users" eyebrow="Volume" title="Booking Volume">
                    <div class="mini-chart">
                        <div v-for="booking in bookings" :key="`${booking.id}-volume`" class="chart-row">
                            <span>{{ booking.destination }}</span>
                            <div><i :style="{ width: chartWidth(booking.y, maxPassengers) }"></i></div>
                            <strong>{{ booking.y }}</strong>
                        </div>
                    </div>
                </ContentPanel>
            </section>

            <section class="dss-grid two-column">
                <ContentPanel icon="mapPinned" eyebrow="Destination Revenue" title="Revenue By Destination">
                    <div class="ranking-list">
                        <div v-for="item in destinationRevenue" :key="item.destination">
                            <span>{{ item.destination }}</span>
                            <strong>{{ formatCurrency(item.revenue) }}</strong>
                            <small>{{ item.passengers }} passengers</small>
                        </div>
                    </div>
                </ContentPanel>

                <ContentPanel icon="wallet" eyebrow="Cost Comparison" title="Revenue Vs Expenses">
                    <div class="comparison-stack">
                        <div>
                            <span>Total revenue</span>
                            <strong>{{ formatCurrency(totalRevenue) }}</strong>
                            <div class="progress-track"><i style="width: 100%"></i></div>
                        </div>
                        <div>
                            <span>Total expenses</span>
                            <strong>{{ formatCurrency(totalExpenses) }}</strong>
                            <div class="progress-track warning"><i :style="{ width: chartWidth(totalExpenses, totalRevenue) }"></i></div>
                        </div>
                    </div>
                </ContentPanel>
            </section>

            <ContentPanel icon="database" eyebrow="Top Packages" title="Package Performance Table">
                <div class="table-scroll">
                    <table class="dss-table">
                        <thead>
                            <tr>
                                <th>Destination</th>
                                <th>Bookings</th>
                                <th>Passengers</th>
                                <th>Revenue</th>
                                <th>DSS Meaning</th>
                            </tr>
                        </thead>
                        <tbody>
                            <tr v-for="item in destinationRevenue" :key="`${item.destination}-row`">
                                <td><strong>{{ item.destination }}</strong></td>
                                <td>{{ item.bookings }}</td>
                                <td>{{ item.passengers }}</td>
                                <td>{{ formatCurrency(item.revenue) }}</td>
                                <td>{{ item.revenue === destinationRevenue[0].revenue ? 'Top route to review for controlled growth.' : 'Monitor as supporting demand.' }}</td>
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
import { bookings, expenses, expensesByCategory, formatCurrency, groupBookingsByDestination, totalBy } from '../data/mockData';

export default {
    name: 'SalesAnalytics',
    components: { AppShell, ContentPanel, StatCard },
    data() {
        return { bookings, expenses };
    },
    computed: {
        totalRevenue() {
            return totalBy(this.bookings, 'grossRevenue');
        },
        totalExpenses() {
            return totalBy(this.expenses, 'amount');
        },
        destinationRevenue() {
            return groupBookingsByDestination(this.bookings);
        },
        topDestination() {
            return this.destinationRevenue[0];
        },
        costliestCategory() {
            return expensesByCategory(this.expenses)[0];
        },
        maxRevenue() {
            return Math.max(...this.bookings.map((booking) => booking.grossRevenue));
        },
        maxPassengers() {
            return Math.max(...this.bookings.map((booking) => booking.y));
        },
        stats() {
            return [
                { icon: 'mapPinned', label: 'Top Revenue Route', value: this.topDestination.destination, note: formatCurrency(this.topDestination.revenue), status: 'Top route', tone: 'primary' },
                { icon: 'users', label: 'Highest Passenger Volume', value: this.topDestination.destination, note: `${this.topDestination.passengers} passengers`, status: 'Demand', tone: 'success' },
                { icon: 'calendar', label: 'Most Active Month', value: 'June', note: 'Mock data period', status: 'Current', tone: 'data' },
                { icon: 'wallet', label: 'Costliest Category', value: this.costliestCategory.category, note: formatCurrency(this.costliestCategory.amount), status: 'Review', tone: 'warning' },
            ];
        },
    },
    methods: {
        formatCurrency,
        chartWidth(value, max) {
            return `${Math.max(8, Math.round((value / max) * 100))}%`;
        },
    },
};
</script>
