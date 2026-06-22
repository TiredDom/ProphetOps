<template>
    <AppShell
        active-label="Analytics"
        eyebrow="Analysis"
        title="Sales Analytics"
        description="Business analysis for travel demand, revenue movement, and cost pressure."
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
                <ChartPanel icon="fileBarChart" eyebrow="Monthly Sales" title="Revenue By Booking Date" :height="230">
                    <MiniBarChart
                        :items="revenueChartItems"
                        :value-formatter="formatCurrency"
                    />
                </ChartPanel>

                <ContentPanel icon="mapPinned" eyebrow="Destination Revenue" title="Revenue By Destination">
                    <div class="ranking-list">
                        <div v-for="item in destinationRevenue" :key="item.destination">
                            <span>{{ item.destination }}</span>
                            <strong>{{ formatCurrency(item.revenue) }}</strong>
                            <small>{{ item.passengers }} passengers</small>
                        </div>
                    </div>
                </ContentPanel>
            </section>

            <section>
                <ContentPanel
                    icon="wallet"
                    eyebrow="Profitability"
                    title="Revenue, Cost, and Margin"
                    panel-class="analytics-cost-panel"
                >
                    <div class="analytics-cost-summary">
                        <div v-for="metric in costSummaryMetrics" :key="metric.label">
                            <span>{{ metric.label }}</span>
                            <strong>{{ metric.value }}</strong>
                            <small>{{ metric.note }}</small>
                        </div>
                    </div>

                    <div class="analytics-donut-grid">
                        <div class="analytics-donut-section">
                            <div>
                                <p class="section-label">Revenue allocation</p>
                                <strong>Profit vs operating costs</strong>
                            </div>
                            <DonutChart
                                :items="revenueAllocationItems"
                                center-label="Margin"
                                :center-value="formatPercent(profitMargin)"
                                aria-label="Revenue allocation by estimated profit and expenses"
                            />
                        </div>

                        <div class="analytics-donut-section">
                            <div>
                                <p class="section-label">Cost drivers</p>
                                <strong>Expense share by category</strong>
                            </div>
                            <DonutChart
                                :items="expenseCategoryItems"
                                center-label="Largest"
                                :center-value="formatPercent(costDriverShare)"
                                aria-label="Expense category share"
                            />
                        </div>
                    </div>

                    <div class="analytics-cost-breakdown">
                        <div class="analytics-cost-note">
                            <span>Decision cue</span>
                            <strong>Profit is healthy, but cost growth should be checked before promo expansion.</strong>
                            <p>{{ costliestCategory.category }} is the largest recorded cost driver this period.</p>
                        </div>
                    </div>
                </ContentPanel>
            </section>
        </section>
    </AppShell>
</template>

<script>
import AppShell from '../Components/layout/AppShell.vue';
import ChartPanel from '../Components/charts/ChartPanel.vue';
import ContentPanel from '../Components/dashboard/ContentPanel.vue';
import DonutChart from '../Components/charts/DonutChart.vue';
import MiniBarChart from '../Components/charts/MiniBarChart.vue';
import StatCard from '../Components/dashboard/StatCard.vue';
import { expensesByCategory, formatCurrency, groupBookingsByDestination, totalBy } from '../utils/formatters';

export default {
    name: 'SalesAnalytics',
    components: { AppShell, ChartPanel, ContentPanel, DonutChart, MiniBarChart, StatCard },
    props: {
        bookings: {
            type: Array,
            default: () => [],
        },
        expenses: {
            type: Array,
            default: () => [],
        },
    },
    computed: {
        totalRevenue() {
            return totalBy(this.bookings, 'grossRevenue');
        },
        totalExpenses() {
            return totalBy(this.expenses, 'amount');
        },
        estimatedProfit() {
            return this.totalRevenue - this.totalExpenses;
        },
        expenseRatio() {
            return this.totalRevenue ? this.totalExpenses / this.totalRevenue : 0;
        },
        profitMargin() {
            return this.totalRevenue ? this.estimatedProfit / this.totalRevenue : 0;
        },
        destinationRevenue() {
            return groupBookingsByDestination(this.bookings);
        },
        topDestination() {
            return this.destinationRevenue[0] || {
                destination: 'No bookings',
                revenue: 0,
                passengers: 0,
                bookings: 0,
            };
        },
        costliestCategory() {
            return expensesByCategory(this.expenses)[0] || {
                category: 'No expenses',
                amount: 0,
            };
        },
        costDriverShare() {
            return this.totalExpenses ? (this.costliestCategory?.amount || 0) / this.totalExpenses : 0;
        },
        expenseCategoryItems() {
            const colors = ['#4F46E5', '#0F766E', '#B45309', '#15803D'];

            return expensesByCategory(this.expenses).map((item, index) => ({
                id: item.category,
                label: this.shortExpenseLabel(item.category),
                value: item.amount,
                valueLabel: formatCurrency(item.amount),
                color: colors[index % colors.length],
            }));
        },
        revenueChartItems() {
            return this.bookings.map((booking) => ({
                id: booking.id,
                label: booking.ds.slice(5),
                value: booking.grossRevenue,
            }));
        },
        revenueAllocationItems() {
            return [
                {
                    label: 'Estimated profit',
                    value: this.estimatedProfit,
                    valueLabel: formatCurrency(this.estimatedProfit),
                    tone: 'success',
                },
                {
                    label: 'Expenses',
                    value: this.totalExpenses,
                    valueLabel: formatCurrency(this.totalExpenses),
                    tone: 'warning',
                },
            ];
        },
        costSummaryMetrics() {
            return [
                {
                    label: 'Estimated profit',
                    value: formatCurrency(this.estimatedProfit),
                    note: `${this.formatPercent(this.profitMargin)} margin`,
                },
                {
                    label: 'Expense ratio',
                    value: this.formatPercent(this.expenseRatio),
                    note: 'Cost share of revenue',
                },
                {
                    label: 'Top cost driver',
                    value: this.costliestCategory.category,
                    note: formatCurrency(this.costliestCategory.amount),
                },
            ];
        },
        stats() {
            return [
                { icon: 'mapPinned', label: 'Top Revenue Route', value: this.topDestination.destination, note: formatCurrency(this.topDestination.revenue), status: 'Top route', tone: 'primary' },
                { icon: 'users', label: 'Highest Passenger Volume', value: this.topDestination.destination, note: `${this.topDestination.passengers} passengers`, status: 'Demand', tone: 'success' },
                { icon: 'calendar', label: 'Most Active Month', value: 'June', note: 'Current data period', status: 'Current', tone: 'data' },
                { icon: 'wallet', label: 'Costliest Category', value: this.costliestCategory.category, note: formatCurrency(this.costliestCategory.amount), status: 'Review', tone: 'warning' },
            ];
        },
    },
    methods: {
        formatCurrency,
        formatPercent(value) {
            return `${Math.round(value * 100)}%`;
        },
        shortExpenseLabel(value) {
            const labels = {
                'Tour operations': 'Tour ops',
                'Seasonal cost': 'Seasonal',
            };

            return labels[value] || value;
        },
    },
};
</script>
