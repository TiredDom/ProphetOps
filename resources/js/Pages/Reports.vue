<template>
    <AppShell
        active-label="Reports"
        eyebrow="Internal Reports"
        title="Reports"
        description="Internal report cards for owners and stakeholders. Export actions are Sprint 1 placeholders."
    >
        <section class="dss-page">
            <section class="report-card-grid">
                <article v-for="report in reportCards" :key="report.title" class="report-card">
                    <span class="record-badge" :class="statusClass(report.status)">{{ report.status }}</span>
                    <h3>{{ report.title }}</h3>
                    <p>{{ report.description }}</p>
                    <div class="report-actions">
                        <button class="primary-button compact-button" type="button">View report</button>
                        <button class="secondary-button compact-button" type="button" disabled>Export PDF</button>
                        <button class="secondary-button compact-button" type="button" disabled>Export Excel</button>
                    </div>
                </article>
            </section>
        </section>
    </AppShell>
</template>

<script>
import AppShell from '../Components/layout/AppShell.vue';
import { reportCards } from '../data/mockData';

export default {
    name: 'Reports',
    components: { AppShell },
    data() {
        return { reportCards };
    },
    methods: {
        statusClass(value) {
            return `status-${String(value).toLowerCase().replace(/[^a-z0-9]+/g, '-')}`;
        },
    },
};
</script>
