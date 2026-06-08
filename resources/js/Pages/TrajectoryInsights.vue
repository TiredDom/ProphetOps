<template>
    <AppShell
        active-label="Trajectory Insights"
        eyebrow="Simulated DSS Insight"
        title="Trajectory Insights"
        description="AI trajectory module placeholder using simulated business interpretation cards."
    >
        <section class="dss-page">
            <section class="business-gist">
                <div>
                    <span class="insight-label">AI trajectory module placeholder</span>
                    <h2>These insights are simulated for Sprint 1.</h2>
                    <p>Each card connects observed data to business meaning and a suggested action.</p>
                </div>
            </section>

            <section class="insight-card-grid">
                <article v-for="insight in trajectoryInsights" :key="insight.finding" class="decision-card insight-card">
                    <div class="decision-card-top">
                        <span class="record-badge" :class="statusClass(insight.type)">{{ insight.type }}</span>
                        <small>Simulated DSS Insight</small>
                    </div>
                    <h3>{{ insight.category }}</h3>
                    <p><strong>Finding:</strong> {{ insight.finding }}</p>
                    <p><strong>Reason:</strong> {{ insight.reason }}</p>
                    <p><strong>Suggested Action:</strong> {{ insight.action }}</p>
                </article>
            </section>
        </section>
    </AppShell>
</template>

<script>
import AppShell from '../Components/layout/AppShell.vue';
import { trajectoryInsights } from '../data/mockData';

export default {
    name: 'TrajectoryInsights',
    components: { AppShell },
    data() {
        return { trajectoryInsights };
    },
    methods: {
        statusClass(value) {
            return `status-${String(value).toLowerCase().replace(/[^a-z0-9]+/g, '-')}`;
        },
    },
};
</script>
