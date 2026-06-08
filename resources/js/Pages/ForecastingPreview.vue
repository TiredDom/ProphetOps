<template>
    <AppShell
        active-label="Forecasting"
        eyebrow="Forecasting"
        title="Forecasting Preview"
        description="Sample interface for future Meta Prophet integration. Sprint 1 uses mock data only."
    >
        <section class="dss-page">
            <section class="business-gist">
                <div>
                    <span class="insight-label">Sample Forecast Preview</span>
                    <h2>Forecast engine integration pending.</h2>
                    <p>This page shows how the forecasting interface will look once clean booking data is connected to a real engine.</p>
                </div>
                <a class="primary-button" href="/bookings">Review Data Requirements</a>
            </section>

            <section class="dss-grid two-column">
                <ContentPanel icon="sparkles" eyebrow="30-Day Projection" title="Booking Projection" badge="Sample only">
                    <div class="mini-chart">
                        <div v-for="point in forecastProjection" :key="point.date" class="chart-row">
                            <span>{{ point.date }}</span>
                            <div><i :style="{ width: chartWidth(point.bookings, maxBookings) }"></i></div>
                            <strong>{{ point.bookings }}</strong>
                        </div>
                    </div>
                </ContentPanel>

                <ContentPanel icon="wallet" eyebrow="30-Day Projection" title="Revenue Projection" badge="Sample only">
                    <div class="mini-chart">
                        <div v-for="point in forecastProjection" :key="`${point.date}-revenue`" class="chart-row">
                            <span>{{ point.date }}</span>
                            <div><i :style="{ width: chartWidth(point.revenue, maxRevenue) }"></i></div>
                            <strong>{{ formatCurrency(point.revenue) }}</strong>
                        </div>
                    </div>
                </ContentPanel>
            </section>

            <section class="dss-grid three-column">
                <ContentPanel icon="fileBarChart" eyebrow="Demand Trend" title="Seasonality Notes">
                    <p class="placeholder-note">Observed sample trend: bookings rise near month-end. Business meaning: capacity planning should happen before promo approval.</p>
                </ContentPanel>

                <ContentPanel icon="database" eyebrow="Data Requirements" title="Prophet Input Fields">
                    <div class="compact-list static-list">
                        <span>ds: booking date</span>
                        <span>y: passenger count or demand value</span>
                        <span>gross revenue</span>
                        <span>operational cost</span>
                        <span>marketing cost</span>
                        <span>destination/package</span>
                    </div>
                </ContentPanel>

                <ContentPanel icon="shieldCheck" eyebrow="Forecast Status" title="Integration Status" badge="Pending">
                    <p class="placeholder-note">Meta Prophet is not running in Sprint 1. This is a UI placeholder for academic/prototype review.</p>
                </ContentPanel>
            </section>
        </section>
    </AppShell>
</template>

<script>
import AppShell from '../Components/layout/AppShell.vue';
import ContentPanel from '../Components/dashboard/ContentPanel.vue';
import { forecastProjection, formatCurrency } from '../data/mockData';

export default {
    name: 'ForecastingPreview',
    components: { AppShell, ContentPanel },
    data() {
        return { forecastProjection };
    },
    computed: {
        maxBookings() {
            return Math.max(...this.forecastProjection.map((point) => point.bookings));
        },
        maxRevenue() {
            return Math.max(...this.forecastProjection.map((point) => point.revenue));
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
