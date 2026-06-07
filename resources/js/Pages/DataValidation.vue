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
                eyebrow="Data Workspace"
                title="Review Operational Records"
                description="Check internal records before they are used in summaries and reports."
                :current-date="currentDate"
                :sidebar-open="sidebarOpen"
                @toggle-sidebar="toggleSidebar"
            />

            <section class="validation-page simple-validation-page">
                <section class="module-intro validation-intro simple-validation-intro">
                    <div>
                        <div class="hero-status review-status">
                            <span class="status-dot"></span>
                            Review Queue
                        </div>
                        <h2>Review records before reports.</h2>
                        <p>
                            Check missing information, confirm clear records, and keep operational data easy to trust.
                        </p>
                    </div>

                    <div class="module-actions">
                        <button class="primary-button" type="button" @click="reviewNextRecord">
                            <AppIcon name="shieldCheck" />
                            Review Next Record
                        </button>
                        <Link class="secondary-button" href="/data/operational-records">
                            <AppIcon name="database" />
                            View Operational Records
                        </Link>
                    </div>
                </section>

                <section class="stat-grid validation-summary-grid simple-summary-grid" aria-label="Record review summary">
                    <StatCard
                        v-for="stat in summaryStats"
                        :key="stat.label"
                        :icon="stat.icon"
                        :label="stat.label"
                        :value="stat.value"
                        :note="stat.note"
                        :status="stat.status"
                        :tone="stat.tone"
                    />
                </section>

                <section class="validation-layout simple-validation-layout">
                    <ContentPanel
                        icon="shieldCheck"
                        eyebrow="Review Queue"
                        title="Records To Check"
                        :badge="queueBadge"
                        panel-class="validation-queue-panel simple-queue-panel"
                    >
                        <div class="validation-toolbar simple-validation-toolbar" aria-label="Record review filters">
                            <label class="search-control">
                                <AppIcon name="search" />
                                <input
                                    v-model.trim="filters.search"
                                    type="search"
                                    placeholder="Search date, source, destination, or notes"
                                />
                            </label>

                            <label class="filter-control">
                                <span>Status</span>
                                <select v-model="filters.status">
                                    <option value="">All active records</option>
                                    <option value="Needs Checking">Needs Checking</option>
                                    <option value="Ready for Reports">Ready for Reports</option>
                                    <option value="Draft">Draft</option>
                                    <option value="Archived">Archived</option>
                                </select>
                            </label>

                            <label class="filter-control">
                                <span>Source</span>
                                <select v-model="filters.source">
                                    <option value="">All sources</option>
                                    <option v-for="source in sourceChannels" :key="source" :value="source">
                                        {{ source }}
                                    </option>
                                </select>
                            </label>

                            <button class="secondary-button compact-button" type="button" @click="advancedFiltersOpen = !advancedFiltersOpen">
                                <AppIcon name="filter" />
                                {{ advancedFiltersOpen ? 'Hide Advanced' : 'Advanced Filters' }}
                            </button>
                        </div>

                        <div v-if="advancedFiltersOpen" class="advanced-filter-card" aria-label="Advanced record review filters">
                            <label class="filter-control">
                                <span>Record Type</span>
                                <select v-model="filters.frequency">
                                    <option value="">Any type</option>
                                    <option value="Daily Record">Daily Record</option>
                                    <option value="Monthly Summary">Monthly Summary</option>
                                </select>
                            </label>

                            <label class="filter-control">
                                <span>From Date</span>
                                <input v-model="filters.dateFrom" type="date" />
                            </label>

                            <label class="filter-control">
                                <span>To Date</span>
                                <input v-model="filters.dateTo" type="date" />
                            </label>

                            <label class="filter-control">
                                <span>Destination</span>
                                <input v-model.trim="filters.destination" placeholder="Any destination" />
                            </label>

                            <label class="filter-control">
                                <span>Encoded By</span>
                                <input v-model.trim="filters.encodedBy" placeholder="Any encoder" />
                            </label>

                            <label class="filter-control">
                                <span>Missing Information</span>
                                <select v-model="filters.issue">
                                    <option value="">Any missing information</option>
                                    <option v-for="issue in issueFilters" :key="issue" :value="issue">
                                        {{ friendlyIssueLabel(issue) }}
                                    </option>
                                </select>
                            </label>

                            <label class="filter-control">
                                <span>Future Use</span>
                                <select v-model="filters.forecastUse">
                                    <option value="">Any future use</option>
                                    <option value="sales-ready">Sales amount ready</option>
                                    <option value="missing-sales">Missing sales amount</option>
                                    <option value="passenger-missing">Passenger count missing</option>
                                </select>
                            </label>

                            <button class="secondary-button compact-button" type="button" @click="resetFilters">
                                <AppIcon name="filter" />
                                Reset Filters
                            </button>
                        </div>

                        <div v-if="activeFilterText" class="filter-feedback simple-filter-feedback">
                            <AppIcon name="filter" />
                            <span>{{ activeFilterText }}</span>
                        </div>

                        <div v-if="filteredRecords.length" ref="queuePanel" class="validation-table-wrapper simple-table-wrapper">
                            <table class="validation-table simple-validation-table">
                                <thead>
                                    <tr>
                                        <th>Date / Month</th>
                                        <th>Source</th>
                                        <th>Sales Amount</th>
                                        <th>Missing Information</th>
                                        <th>Status</th>
                                        <th>Action</th>
                                    </tr>
                                </thead>
                                <tbody>
                                    <tr
                                        v-for="record in filteredRecords"
                                        :key="record.id"
                                        :class="{ selected: selectedRecord?.id === record.id, archived: record.status === 'Archived' }"
                                    >
                                        <td>
                                            <strong>{{ displayPeriod(record) }}</strong>
                                            <span>{{ record.id }}</span>
                                        </td>
                                        <td>
                                            <span class="source-chip">
                                                <AppIcon :name="sourceIcon(record.sourceChannel)" />
                                                {{ record.sourceChannel || 'Missing source' }}
                                            </span>
                                        </td>
                                        <td>{{ displaySalesAmount(record) }}</td>
                                        <td>
                                            <span class="simple-issue-label" :class="issueTone(record)">
                                                {{ missingInformationLabel(record) }}
                                            </span>
                                        </td>
                                        <td>
                                            <span class="record-badge" :class="statusClass(record.status)">
                                                {{ statusLabel(record.status) }}
                                            </span>
                                        </td>
                                        <td>
                                            <button class="secondary-button compact-button queue-review-button" type="button" @click="selectRecord(record)">
                                                <AppIcon name="search" />
                                                Review
                                            </button>
                                        </td>
                                    </tr>
                                </tbody>
                            </table>
                        </div>

                        <div v-if="filteredRecords.length" class="validation-card-list simple-card-list" aria-label="Mobile record review queue">
                            <article
                                v-for="record in filteredRecords"
                                :key="`${record.id}-card`"
                                class="validation-review-card simple-review-card"
                                :class="{ selected: selectedRecord?.id === record.id, archived: record.status === 'Archived' }"
                            >
                                <button class="mobile-card-main" type="button" @click="selectRecord(record)">
                                    <span class="source-icon">
                                        <AppIcon :name="sourceIcon(record.sourceChannel)" />
                                    </span>
                                    <span>
                                        <strong>{{ displayPeriod(record) }}</strong>
                                        <small>{{ record.id }} - {{ record.sourceChannel || 'Missing source' }}</small>
                                    </span>
                                    <span class="record-badge" :class="statusClass(record.status)">
                                        {{ statusLabel(record.status) }}
                                    </span>
                                </button>

                                <div class="mobile-card-details">
                                    <div>
                                        <span>Sales Amount</span>
                                        <strong>{{ displaySalesAmount(record) }}</strong>
                                    </div>
                                    <div>
                                        <span>Missing Information</span>
                                        <strong>{{ missingInformationLabel(record) }}</strong>
                                    </div>
                                </div>

                                <button class="secondary-button compact-button" type="button" @click="selectRecord(record)">
                                    <AppIcon name="search" />
                                    Review Record
                                </button>
                            </article>
                        </div>

                        <div v-else class="validation-empty">
                            <EmptyState
                                icon="shieldCheck"
                                :title="emptyStateTitle"
                                :message="emptyStateMessage"
                                :action-label="emptyStateActionLabel"
                                @action="handleEmptyAction"
                            />
                        </div>
                    </ContentPanel>

                    <ContentPanel
                        icon="fileBarChart"
                        eyebrow="Record Review"
                        :title="selectedRecord ? statusLabel(selectedRecord.status) : 'Guided Review'"
                        :badge="selectedRecord ? selectedRecord.id : 'Next Step'"
                        panel-class="record-review-panel simple-review-panel"
                    >
                        <div v-if="selectedRecord" class="review-detail simple-review-detail">
                            <div class="review-record-header">
                                <span class="source-icon">
                                    <AppIcon :name="sourceIcon(selectedRecord.sourceChannel)" />
                                </span>
                                <div>
                                    <strong>{{ displayPeriod(selectedRecord) }}</strong>
                                    <p>{{ selectedRecord.sourceChannel || 'Missing source' }} record</p>
                                </div>
                            </div>

                            <div class="review-answer-card">
                                <span class="answer-icon warning">
                                    <AppIcon name="alertTriangle" />
                                </span>
                                <div>
                                    <p>What is missing?</p>
                                    <strong>{{ missingInformationSentence(selectedRecord) }}</strong>
                                </div>
                            </div>

                            <div class="review-answer-card">
                                <span class="answer-icon data">
                                    <AppIcon name="fileBarChart" />
                                </span>
                                <div>
                                    <p>Why does it matter?</p>
                                    <strong>{{ whyItMatters(selectedRecord) }}</strong>
                                </div>
                            </div>

                            <div class="review-answer-card">
                                <span class="answer-icon success">
                                    <AppIcon name="check" />
                                </span>
                                <div>
                                    <p>What should the user do next?</p>
                                    <strong>{{ nextStepText(selectedRecord) }}</strong>
                                </div>
                            </div>

                            <div class="review-actions simple-review-actions">
                                <button
                                    v-if="canMarkReady(selectedRecord)"
                                    class="primary-button"
                                    type="button"
                                    @click="markReadyForReports(selectedRecord.id)"
                                >
                                    <AppIcon name="check" />
                                    Mark Ready for Reports
                                </button>

                                <Link
                                    v-else
                                    class="primary-button"
                                    href="/data/operational-records"
                                >
                                    <AppIcon name="edit" />
                                    Fix Information
                                </Link>

                                <button class="secondary-button" type="button" @click="setRecordStatus(selectedRecord.id, 'Needs Review')">
                                    <AppIcon name="alertTriangle" />
                                    Keep as Needs Checking
                                </button>

                                <button class="secondary-button danger-button" type="button" @click="setRecordStatus(selectedRecord.id, 'Archived')">
                                    <AppIcon name="archive" />
                                    Archive Record
                                </button>
                            </div>

                            <button class="details-toggle" type="button" @click="detailsOpen = !detailsOpen">
                                <AppIcon name="filter" />
                                {{ detailsOpen ? 'Hide details' : 'Show record details' }}
                            </button>

                            <div v-if="detailsOpen" class="record-details-list">
                                <div>
                                    <span>Record Type</span>
                                    <strong>{{ selectedRecord.frequency }}</strong>
                                </div>
                                <div>
                                    <span>Destination</span>
                                    <strong>{{ selectedRecord.packageDestination || 'Not specified' }}</strong>
                                </div>
                                <div>
                                    <span>Passenger Count</span>
                                    <strong>{{ displayPassengerCount(selectedRecord.passengerCount) }}</strong>
                                </div>
                                <div>
                                    <span>Future Use</span>
                                    <strong>{{ forecastUseText(selectedRecord) }}</strong>
                                </div>
                                <div>
                                    <span>Encoded By</span>
                                    <strong>{{ selectedRecord.encodedBy }}</strong>
                                </div>
                                <div>
                                    <span>Last Updated</span>
                                    <strong>{{ selectedRecord.updatedAt }}</strong>
                                </div>
                                <div class="details-wide">
                                    <span>Notes</span>
                                    <strong>{{ selectedRecord.notes || 'No notes recorded' }}</strong>
                                </div>
                            </div>
                        </div>

                        <div v-else class="review-placeholder simple-review-placeholder">
                            <span class="empty-state-icon" aria-hidden="true">
                                <AppIcon name="shieldCheck" />
                            </span>
                            <h4>{{ needsCheckingRecords.length ? 'Start with the next record' : 'All records are ready for reports.' }}</h4>
                            <p>
                                {{ needsCheckingRecords.length
                                    ? 'Use Review Next Record to open the first item that needs checking.'
                                    : 'There are no records waiting for checking right now.' }}
                            </p>
                            <button v-if="needsCheckingRecords.length" class="primary-button" type="button" @click="reviewNextRecord">
                                <AppIcon name="shieldCheck" />
                                Review Next Record
                            </button>
                            <Link v-else class="secondary-button" href="/data/operational-records">
                                <AppIcon name="database" />
                                View Operational Records
                            </Link>
                        </div>
                    </ContentPanel>
                </section>

                <div v-if="toastMessage" class="validation-toast" role="status">
                    <AppIcon :name="toastTone === 'warning' ? 'alertTriangle' : 'check'" />
                    <span>{{ toastMessage }}</span>
                </div>
            </section>
        </main>
    </div>
</template>

<script>
import { Link } from '@inertiajs/vue3';
import ContentPanel from '../Components/dashboard/ContentPanel.vue';
import StatCard from '../Components/dashboard/StatCard.vue';
import EmptyState from '../Components/feedback/EmptyState.vue';
import AppIcon from '../Components/icons/AppIcon.vue';
import Sidebar from '../Components/layout/Sidebar.vue';
import TopBar from '../Components/layout/TopBar.vue';
import { createNavigationGroups } from '../data/navigation';
import { requireMockAuth } from '../services/mockAuth';

const sourceChannels = ['Messenger', 'Google Sheets', 'Gmail', 'Notebook', 'Paper Record', 'Manual Encoding'];
const issueFilters = [
    'Missing Date / Month',
    'Missing Source',
    'Missing Sales Amount',
    'Passenger Count Missing',
    'Possible Duplicate',
    'Notes Only',
];

function createInitialValidationRecords() {
    return [
        {
            id: 'OR-0007',
            frequency: 'Monthly Summary',
            period: '2026-05',
            sourceChannel: 'Notebook',
            packageDestination: 'Baguio Tour',
            salesAmount: 48200,
            passengerCount: null,
            status: 'Needs Review',
            encodedBy: 'Admin',
            updatedAt: 'Jun 3, 2026',
            notes: 'Owner notebook monthly sales. Passenger count was not recorded.',
            issues: ['Passenger Count Missing'],
        },
        {
            id: 'OR-0006',
            frequency: 'Daily Record',
            period: '2026-05-28',
            sourceChannel: 'Messenger',
            packageDestination: 'Cebu Package',
            salesAmount: null,
            passengerCount: 4,
            status: 'Needs Review',
            encodedBy: 'Staff',
            updatedAt: 'Jun 2, 2026',
            notes: 'Messenger thread has passenger count but sales amount is unclear.',
            issues: ['Missing Sales Amount'],
        },
        {
            id: 'OR-0005',
            frequency: 'Daily Record',
            period: '2026-05-26',
            sourceChannel: 'Google Sheets',
            packageDestination: 'Palawan Package',
            salesAmount: 0,
            passengerCount: 0,
            status: 'Validated',
            encodedBy: 'Admin',
            updatedAt: 'Jun 2, 2026',
            notes: 'Confirmed true zero-sales day, not a missing entry.',
            issues: [],
        },
        {
            id: 'OR-0004',
            frequency: 'Daily Record',
            period: '',
            sourceChannel: 'Paper Record',
            packageDestination: '',
            salesAmount: 12500,
            passengerCount: 2,
            status: 'Draft',
            encodedBy: 'Staff',
            updatedAt: 'Jun 1, 2026',
            notes: 'Paper record has amount but no readable date.',
            issues: ['Missing Date / Month'],
        },
        {
            id: 'OR-0003',
            frequency: 'Daily Record',
            period: '2026-05-20',
            sourceChannel: '',
            packageDestination: 'Bohol Tour',
            salesAmount: 18600,
            passengerCount: 3,
            status: 'Needs Review',
            encodedBy: 'Staff',
            updatedAt: 'May 31, 2026',
            notes: 'Looks similar to another encoded line. Source channel not confirmed.',
            issues: ['Missing Source', 'Possible Duplicate'],
        },
        {
            id: 'OR-0002',
            frequency: 'Monthly Summary',
            period: '2026-04',
            sourceChannel: 'Manual Encoding',
            packageDestination: '',
            salesAmount: null,
            passengerCount: null,
            status: 'Archived',
            encodedBy: 'Admin',
            updatedAt: 'May 29, 2026',
            notes: 'Notes-only historical placeholder. Replaced by cleaner owner notebook entry.',
            issues: ['Notes Only', 'Missing Sales Amount'],
        },
    ];
}

export default {
    name: 'DataValidation',
    components: {
        AppIcon,
        ContentPanel,
        EmptyState,
        Link,
        Sidebar,
        StatCard,
        TopBar,
    },
    data() {
        return {
            sidebarOpen: false,
            advancedFiltersOpen: false,
            detailsOpen: false,
            currentDate: new Intl.DateTimeFormat('en-US', {
                month: 'short',
                day: 'numeric',
                year: 'numeric',
            }).format(new Date()),
            navigationGroups: createNavigationGroups('Data Validation'),
            records: createInitialValidationRecords(),
            selectedRecordId: null,
            filters: {
                search: '',
                status: '',
                source: '',
                frequency: '',
                dateFrom: '',
                dateTo: '',
                destination: '',
                encodedBy: '',
                issue: '',
                forecastUse: '',
            },
            sourceChannels,
            issueFilters,
            toastMessage: '',
            toastTone: 'success',
            toastTimer: null,
        };
    },
    computed: {
        selectedRecord() {
            return this.records.find((record) => record.id === this.selectedRecordId) || null;
        },
        activeRecords() {
            return this.records.filter((record) => record.status !== 'Archived');
        },
        needsCheckingRecords() {
            return this.records.filter((record) => ['Needs Review', 'Draft'].includes(record.status));
        },
        readyRecords() {
            return this.records.filter((record) => record.status === 'Validated');
        },
        archivedRecords() {
            return this.records.filter((record) => record.status === 'Archived');
        },
        queueBadge() {
            return this.needsCheckingRecords.length ? `${this.needsCheckingRecords.length} to check` : 'Clear';
        },
        summaryStats() {
            return [
                {
                    icon: 'alertTriangle',
                    label: 'Needs Checking',
                    value: String(this.needsCheckingRecords.length),
                    note: this.needsCheckingRecords.length ? 'Start with the next record' : 'Nothing waiting',
                    status: 'Check',
                    tone: 'warning',
                },
                {
                    icon: 'shieldCheck',
                    label: 'Ready for Reports',
                    value: String(this.readyRecords.length),
                    note: 'Clean enough for basic summaries',
                    status: 'Ready',
                    tone: 'success',
                },
                {
                    icon: 'archive',
                    label: 'Archived',
                    value: String(this.archivedRecords.length),
                    note: 'Kept for history',
                    status: 'Stored',
                    tone: 'primary',
                },
            ];
        },
        filteredRecords() {
            const searchTerm = this.filters.search.toLowerCase();

            return this.records.filter((record) => {
                if (!this.filters.status && record.status === 'Archived') {
                    return false;
                }

                if (this.filters.status && !this.matchesStatusFilter(record, this.filters.status)) {
                    return false;
                }

                if (this.filters.source && record.sourceChannel !== this.filters.source) {
                    return false;
                }

                if (this.filters.frequency && record.frequency !== this.filters.frequency) {
                    return false;
                }

                if (this.filters.dateFrom && this.recordComparableDate(record) < this.filters.dateFrom) {
                    return false;
                }

                if (this.filters.dateTo && this.recordComparableDate(record) > this.filters.dateTo) {
                    return false;
                }

                if (
                    this.filters.destination
                    && !String(record.packageDestination || '').toLowerCase().includes(this.filters.destination.toLowerCase())
                ) {
                    return false;
                }

                if (
                    this.filters.encodedBy
                    && !String(record.encodedBy || '').toLowerCase().includes(this.filters.encodedBy.toLowerCase())
                ) {
                    return false;
                }

                if (this.filters.issue && !record.issues.includes(this.filters.issue)) {
                    return false;
                }

                if (this.filters.forecastUse && !this.matchesForecastUse(record, this.filters.forecastUse)) {
                    return false;
                }

                if (!searchTerm) {
                    return true;
                }

                return [
                    record.id,
                    record.sourceChannel,
                    record.packageDestination,
                    record.notes,
                    record.encodedBy,
                    this.statusLabel(record.status),
                    this.missingInformationLabel(record),
                ].some((value) => String(value || '').toLowerCase().includes(searchTerm));
            });
        },
        activeFilterText() {
            const visibleFilters = ['search', 'status', 'source'];
            const activeFilters = visibleFilters
                .filter((key) => this.filters[key])
                .map((key) => `${this.filterLabel(key)}: ${this.filters[key]}`);

            const advancedCount = Object.entries(this.filters)
                .filter(([key, value]) => !visibleFilters.includes(key) && value)
                .length;

            if (advancedCount) {
                activeFilters.push(`${advancedCount} advanced`);
            }

            return activeFilters.length ? `${this.filteredRecords.length} record result(s) with ${activeFilters.join(', ')}` : '';
        },
        emptyStateTitle() {
            if (!this.records.length) {
                return 'No records to check yet.';
            }

            if (!this.needsCheckingRecords.length && !this.hasActiveFilters()) {
                return 'All records are ready for reports.';
            }

            return 'No records match these filters.';
        },
        emptyStateMessage() {
            if (!this.records.length) {
                return 'Add operational records first so this review page has something to check.';
            }

            if (!this.needsCheckingRecords.length && !this.hasActiveFilters()) {
                return 'There are no records waiting for checking right now.';
            }

            return 'Try clearing filters or searching with a simpler term.';
        },
        emptyStateActionLabel() {
            if (!this.records.length || !this.needsCheckingRecords.length) {
                return 'View Operational Records';
            }

            return 'Reset Filters';
        },
    },
    watch: {
        selectedRecordId() {
            this.detailsOpen = false;
        },
    },
    mounted() {
        if (!requireMockAuth()) {
            return;
        }

        window.addEventListener('keydown', this.handleKeydown);
    },
    beforeUnmount() {
        window.removeEventListener('keydown', this.handleKeydown);
        window.clearTimeout(this.toastTimer);
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
        reviewNextRecord() {
            const nextRecord = this.needsCheckingRecords[0];

            if (!nextRecord) {
                this.selectedRecordId = null;
                this.showToast('All records are ready for reports.', 'success');
                return;
            }

            this.selectedRecordId = nextRecord.id;
            this.filters.status = '';
            this.$nextTick(() => {
                this.$refs.queuePanel?.scrollIntoView({ behavior: 'smooth', block: 'center' });
            });
        },
        selectRecord(record) {
            this.selectedRecordId = record.id;
        },
        resetFilters() {
            this.filters = {
                search: '',
                status: '',
                source: '',
                frequency: '',
                dateFrom: '',
                dateTo: '',
                destination: '',
                encodedBy: '',
                issue: '',
                forecastUse: '',
            };
        },
        handleEmptyAction() {
            if (!this.records.length || !this.needsCheckingRecords.length) {
                window.location.href = '/data/operational-records';
                return;
            }

            this.resetFilters();
        },
        markReadyForReports(recordId) {
            const record = this.records.find((item) => item.id === recordId);

            if (!record) {
                return;
            }

            if (!this.canMarkReady(record)) {
                this.showToast('This record still needs missing information before it can be marked ready.', 'warning');
                return;
            }

            this.records = this.records.map((item) =>
                item.id === recordId
                    ? { ...item, status: 'Validated', updatedAt: this.currentDate, issues: this.keepHelpfulGaps(item) }
                    : item,
            );
            this.showToast(`${recordId} is now ready for reports.`, 'success');
        },
        setRecordStatus(recordId, status) {
            this.records = this.records.map((record) =>
                record.id === recordId
                    ? { ...record, status, updatedAt: this.currentDate }
                    : record,
            );
            this.showToast(`${recordId} moved to ${this.statusLabel(status)}.`, status === 'Needs Review' ? 'warning' : 'success');
        },
        showToast(message, tone = 'success') {
            this.toastMessage = message;
            this.toastTone = tone;
            window.clearTimeout(this.toastTimer);
            this.toastTimer = window.setTimeout(() => {
                this.toastMessage = '';
            }, 2600);
        },
        hasActiveFilters() {
            return Object.values(this.filters).some((value) => Boolean(value));
        },
        canMarkReady(record) {
            return Boolean(record.period)
                && Boolean(record.sourceChannel)
                && record.salesAmount !== null
                && record.salesAmount !== undefined
                && Number.isFinite(Number(record.salesAmount));
        },
        keepHelpfulGaps(record) {
            return record.issues.filter((issue) => issue === 'Passenger Count Missing');
        },
        matchesForecastUse(record, filter) {
            if (filter === 'sales-ready') {
                return Boolean(record.period)
                    && Boolean(record.sourceChannel)
                    && record.salesAmount !== null
                    && record.salesAmount !== undefined
                    && Number.isFinite(Number(record.salesAmount));
            }

            if (filter === 'missing-sales') {
                return record.salesAmount === null || record.salesAmount === undefined;
            }

            if (filter === 'passenger-missing') {
                return record.passengerCount === null || record.passengerCount === undefined;
            }

            return true;
        },
        matchesStatusFilter(record, filter) {
            if (filter === 'Needs Checking') {
                return ['Needs Review', 'Draft'].includes(record.status);
            }

            return this.statusLabel(record.status) === filter || record.status === filter;
        },
        missingInformationLabel(record) {
            if (!record.issues.length) {
                return 'None';
            }

            if (record.issues.includes('Missing Sales Amount')) {
                return 'Sales amount missing';
            }

            if (record.issues.includes('Missing Date / Month')) {
                return 'Date or month missing';
            }

            if (record.issues.includes('Missing Source')) {
                return 'Source missing';
            }

            if (record.issues.includes('Passenger Count Missing')) {
                return 'Passenger count missing';
            }

            if (record.issues.includes('Possible Duplicate')) {
                return 'May be duplicate';
            }

            return 'Needs checking';
        },
        missingInformationSentence(record) {
            const label = this.missingInformationLabel(record);

            if (label === 'None') {
                return 'No required information is missing.';
            }

            return `${label}.`;
        },
        whyItMatters(record) {
            if (record.issues.includes('Missing Sales Amount')) {
                return 'Sales amount is missing, so this record cannot be used for sales reports yet.';
            }

            if (record.issues.includes('Missing Date / Month')) {
                return 'The report needs a date or month so the record appears in the correct period.';
            }

            if (record.issues.includes('Missing Source')) {
                return 'The source helps staff know where the record came from and whether it can be trusted.';
            }

            if (record.issues.includes('Passenger Count Missing')) {
                return 'Passenger count is missing, but this record can still be used for sales reports if the sales amount is valid.';
            }

            if (record.issues.includes('Possible Duplicate')) {
                return 'This may already be recorded, so it should be checked before reports use it.';
            }

            return 'This record has the main information needed for basic reports.';
        },
        nextStepText(record) {
            if (this.canMarkReady(record)) {
                if (record.issues.includes('Passenger Count Missing')) {
                    return 'Confirm the sales amount, then mark it ready for reports. Passenger details can be added later.';
                }

                if (record.issues.includes('Possible Duplicate')) {
                    return 'Check if this is repeated, then archive one copy or mark this record ready.';
                }

                return 'Mark this record ready for reports.';
            }

            return 'Open Operational Records and fill in the missing information.';
        },
        forecastUseText(record) {
            if (!this.canMarkReady(record)) {
                return 'Not ready for future forecasting yet';
            }

            if (record.passengerCount === null || record.passengerCount === undefined) {
                return 'Ready for sales use; passenger count can be added later';
            }

            return 'Ready for sales and passenger use';
        },
        issueTone(record) {
            if (!record.issues.length) {
                return 'issue-ok';
            }

            if (this.canMarkReady(record) && record.issues.includes('Passenger Count Missing')) {
                return 'issue-muted';
            }

            return 'issue-warning';
        },
        friendlyIssueLabel(issue) {
            const labels = {
                'Missing Date / Month': 'Date or month missing',
                'Missing Source': 'Source missing',
                'Missing Sales Amount': 'Sales amount missing',
                'Passenger Count Missing': 'Passenger count missing',
                'Possible Duplicate': 'May be duplicate',
                'Notes Only': 'Notes only',
            };

            return labels[issue] || issue;
        },
        statusLabel(status) {
            const labels = {
                Draft: 'Draft',
                'Needs Review': 'Needs Checking',
                Validated: 'Ready for Reports',
                Archived: 'Archived',
            };

            return labels[status] || status;
        },
        statusClass(status) {
            return `status-${status.toLowerCase().replace(/\s+/g, '-')}`;
        },
        filterLabel(key) {
            const labels = {
                search: 'Search',
                status: 'Status',
                source: 'Source',
            };

            return labels[key] || key;
        },
        sourceIcon(source) {
            const icons = {
                Messenger: 'message',
                'Google Sheets': 'sheet',
                Gmail: 'mail',
                Notebook: 'notebook',
                'Paper Record': 'paperRecord',
                'Manual Encoding': 'edit',
            };

            return icons[source] || 'database';
        },
        recordComparableDate(record) {
            if (!record.period) {
                return '';
            }

            return record.frequency === 'Monthly Summary' ? `${record.period}-01` : record.period;
        },
        displayPeriod(record) {
            if (!record.period) {
                return 'Missing date';
            }

            if (record.frequency === 'Monthly Summary') {
                return new Intl.DateTimeFormat('en-US', {
                    month: 'long',
                    year: 'numeric',
                }).format(new Date(`${record.period}-01T00:00:00`));
            }

            return new Intl.DateTimeFormat('en-US', {
                month: 'short',
                day: 'numeric',
                year: 'numeric',
            }).format(new Date(`${record.period}T00:00:00`));
        },
        displaySalesAmount(record) {
            if (record.salesAmount === null || record.salesAmount === undefined) {
                return 'Missing';
            }

            return new Intl.NumberFormat('en-PH', {
                style: 'currency',
                currency: 'PHP',
            }).format(record.salesAmount);
        },
        displayPassengerCount(value) {
            return value === null || value === undefined ? 'Not recorded' : String(value);
        },
    },
};
</script>
