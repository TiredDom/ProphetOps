<template>
    <AppShell
        active-label="Reports"
        eyebrow="Internal Reports"
        title="Reports"
        description="Manage report summaries for owner review and stakeholder updates."
    >
        <section class="dss-page">
            <section class="report-card-grid">
                <article v-for="report in reportCards" :key="report.title" class="report-card">
                    <span class="record-badge" :class="statusClass(report.status)">{{ report.status }}</span>
                    <h2>{{ report.title }}</h2>
                    <p>{{ report.description }}</p>
                    <div class="report-actions">
                        <button class="primary-button compact-button" type="button" @click="openReport(report)">
                            View report
                        </button>
                        <button class="secondary-button compact-button" type="button" @click="openExportModal(report)">
                            Prepare export
                        </button>
                    </div>
                </article>
            </section>

            <AppModal
                v-if="selectedReport"
                eyebrow="Report view"
                :title="selectedReport.title"
                @close="selectedReport = null"
            >
                <div class="report-preview">
                    <p>{{ selectedReport.description }}</p>
                    <div class="report-metric-grid">
                        <div v-for="metric in reportMetrics(selectedReport)" :key="metric.label">
                            <span>{{ metric.label }}</span>
                            <strong>{{ metric.value }}</strong>
                        </div>
                    </div>
                    <div class="modal-section">
                        <p class="modal-section-title">Included sections</p>
                        <div class="modal-chip-list">
                            <span v-for="section in reportSections(selectedReport)" :key="section">{{ section }}</span>
                        </div>
                    </div>
                </div>
                <template #footer>
                    <button class="primary-button" type="button" @click="selectedReport = null">Close report</button>
                </template>
            </AppModal>

            <AppModal
                v-if="exportRequest"
                eyebrow="Export Package"
                :title="`Prepare ${exportRequest.report.title}`"
                @close="closeExportModal"
            >
                <div class="report-preview">
                    <p>{{ exportRequest.report.title }} will include the sections selected below.</p>
                    <div class="modal-section">
                        <p class="modal-section-title">Sections</p>
                        <div class="modal-chip-list">
                            <span v-for="section in reportSections(exportRequest.report)" :key="section">{{ section }}</span>
                        </div>
                    </div>
                    <div class="export-status-card" :class="{ complete: exportPrepared, loading: isPreparingExport }">
                        <AppIcon :name="exportPrepared ? 'check' : 'fileBarChart'" />
                        <div>
                            <strong>{{ exportStatusTitle }}</strong>
                            <p>{{ exportStatusMessage }}</p>
                        </div>
                    </div>
                </div>
                <template #footer>
                    <button class="secondary-button" type="button" @click="closeExportModal">Close</button>
                    <button
                        class="primary-button"
                        type="button"
                        :disabled="exportPrepared || isPreparingExport"
                        @click="prepareExport"
                    >
                        <span v-if="isPreparingExport" class="loading-dot" aria-hidden="true"></span>
                        <AppIcon v-else :name="exportPrepared ? 'check' : 'save'" />
                        {{ exportButtonLabel }}
                    </button>
                </template>
            </AppModal>
        </section>
    </AppShell>
</template>

<script>
import AppShell from '../Components/layout/AppShell.vue';
import AppModal from '../Components/feedback/AppModal.vue';
import AppIcon from '../Components/icons/AppIcon.vue';

export default {
    name: 'Reports',
    components: { AppIcon, AppModal, AppShell },
    props: {
        reportCards: {
            type: Array,
            default: () => [],
        },
    },
    data() {
        return {
            exportPrepared: false,
            exportTimer: null,
            isPreparingExport: false,
            exportRequest: null,
            selectedReport: null,
        };
    },
    computed: {
        exportStatusTitle() {
            if (this.exportPrepared) {
                return 'Export package prepared';
            }

            return this.isPreparingExport ? 'Preparing export package' : 'Ready to prepare';
        },
        exportStatusMessage() {
            if (this.exportPrepared) {
                return 'The report package is ready for presentation review.';
            }

            if (this.isPreparingExport) {
                return 'Packaging the current screen data. Keep this dialog open until it finishes.';
            }

            return 'Prepare a clean report package using the current screen data.';
        },
        exportButtonLabel() {
            if (this.exportPrepared) {
                return 'Prepared';
            }

            return this.isPreparingExport ? 'Preparing...' : 'Prepare Export';
        },
    },
    beforeUnmount() {
        this.clearExportTimer();
    },
    methods: {
        clearExportTimer() {
            if (!this.exportTimer) {
                return;
            }

            window.clearTimeout(this.exportTimer);
            this.exportTimer = null;
        },
        openReport(report) {
            this.selectedReport = report;
        },
        openExportModal(report) {
            this.exportPrepared = false;
            this.isPreparingExport = false;
            this.clearExportTimer();
            this.exportRequest = { report };
        },
        closeExportModal() {
            this.exportPrepared = false;
            this.isPreparingExport = false;
            this.clearExportTimer();
            this.exportRequest = null;
        },
        prepareExport() {
            if (this.exportPrepared || this.isPreparingExport) {
                return;
            }

            this.isPreparingExport = true;
            this.exportTimer = window.setTimeout(() => {
                this.exportPrepared = true;
                this.isPreparingExport = false;
                this.exportTimer = null;
            }, 650);
        },
        reportMetrics(report) {
            if (report.metrics) {
                return report.metrics;
            }

            const metrics = {
                'Sales Summary': [
                    { label: 'Revenue', value: 'PHP 1.13M' },
                    { label: 'Bookings', value: '7' },
                    { label: 'Top route', value: 'Cebu' },
                ],
                'Package Catalog Summary': [
                    { label: 'Packages', value: '6' },
                    { label: 'Low/Critical', value: '3' },
                    { label: 'Watch route', value: 'Boracay' },
                ],
                'Expense Summary': [
                    { label: 'Total costs', value: 'PHP 248.5K' },
                    { label: 'Driver', value: 'Tour ops' },
                    { label: 'Signal', value: 'Rising' },
                ],
                'Package Decision Summary': [
                    { label: 'Criteria', value: '7' },
                    { label: 'Direction', value: 'Guided review' },
                    { label: 'Packages', value: '6' },
                ],
                'Decision Support Summary': [
                    { label: 'Signals', value: '4' },
                    { label: 'High priority', value: '1' },
                    { label: 'Status', value: 'Ready' },
                ],
            };

            return metrics[report.title] || [
                { label: 'Status', value: report.status },
                { label: 'Sections', value: String(this.reportSections(report).length) },
            ];
        },
        reportSections(report) {
            if (report.sections) {
                return report.sections;
            }

            const sections = {
                'Sales Summary': ['Revenue', 'Bookings', 'Passengers', 'Top destinations'],
                'Package Catalog Summary': ['Package presets', 'Available slots', 'Reserved slots', 'Capacity warnings'],
                'Expense Summary': ['Cost categories', 'Payment status', 'Profit watch'],
                'Package Decision Summary': ['Criteria weights', 'Compared options', 'Decision explanation'],
                'Decision Support Summary': ['Action readiness', 'Access coverage', 'Known limits'],
            };

            return sections[report.title] || ['Summary', 'Status', 'Notes'];
        },
        statusClass(value) {
            return `status-${String(value).toLowerCase().replace(/[^a-z0-9]+/g, '-')}`;
        },
    },
};
</script>
