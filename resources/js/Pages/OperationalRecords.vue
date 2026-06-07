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
                title="Operational Records"
                description="Add and manage internal records from daily operations and historical sources."
                :current-date="currentDate"
                :sidebar-open="sidebarOpen"
                @toggle-sidebar="toggleSidebar"
            />

            <section class="records-page simple-records-page">
                <section class="module-intro records-intro simple-validation-intro simple-records-intro">
                    <div>
                        <div class="hero-status">
                            <span class="status-dot"></span>
                            Data Intake
                        </div>
                        <h2>Add and manage operational records.</h2>
                        <p>
                            Start with daily records or monthly summaries from Messenger, Google Sheets, Gmail,
                            notebooks, paper records, or manual encoding.
                        </p>
                    </div>

                    <div class="module-actions">
                        <button class="primary-button" type="button" @click="openCreate">
                            <AppIcon name="plus" />
                            Add Operational Record
                        </button>
                    </div>
                </section>

                <section class="stat-grid record-summary-grid simple-summary-grid simple-record-summary-grid" aria-label="Operational records summary">
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

                <section class="records-layout simple-validation-layout simple-records-layout">
                    <ContentPanel
                        icon="database"
                        eyebrow="Records Workspace"
                        title="Operational Records"
                        :badge="recordsBadge"
                        panel-class="records-table-panel simple-queue-panel"
                    >
                        <div class="records-toolbar simple-validation-toolbar simple-records-toolbar" aria-label="Operational records filters">
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
                                    <option value="Draft">Draft</option>
                                    <option value="Needs Checking">Needs Checking</option>
                                    <option value="Ready for Reports">Ready for Reports</option>
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

                        <div v-if="advancedFiltersOpen" class="advanced-filter-card" aria-label="Advanced operational record filters">
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
                                <span>Package / Destination</span>
                                <input v-model.trim="filters.destination" placeholder="Any destination" />
                            </label>

                            <label class="filter-control">
                                <span>Payment Status</span>
                                <select v-model="filters.paymentStatus">
                                    <option value="">Any payment status</option>
                                    <option v-for="status in paymentStatuses" :key="status" :value="status">
                                        {{ status }}
                                    </option>
                                </select>
                            </label>

                            <label class="filter-control">
                                <span>Encoded By</span>
                                <input v-model.trim="filters.encodedBy" placeholder="Any encoder" />
                            </label>

                            <label class="filter-control">
                                <span>Missing Information</span>
                                <select v-model="filters.missingField">
                                    <option value="">Any missing information</option>
                                    <option value="period">Date or month missing</option>
                                    <option value="source">Source missing</option>
                                    <option value="sales">Sales amount missing</option>
                                    <option value="passenger">Passenger count missing</option>
                                </select>
                            </label>

                            <label class="filter-control">
                                <span>Future Use</span>
                                <select v-model="filters.forecastReadiness">
                                    <option value="">Any future use</option>
                                    <option value="ready">Clean record</option>
                                    <option value="needs-info">Needs information</option>
                                    <option value="sales-only">Sales ready only</option>
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

                        <div v-if="filteredRecords.length" class="records-table-wrapper validation-table-wrapper simple-table-wrapper">
                            <table class="records-table validation-table simple-validation-table simple-records-table">
                                <thead>
                                    <tr>
                                        <th>Date / Month</th>
                                        <th>Source</th>
                                        <th>Record Type</th>
                                        <th>Sales Amount</th>
                                        <th>Status</th>
                                        <th>Action</th>
                                    </tr>
                                </thead>
                                <tbody>
                                    <tr
                                        v-for="record in filteredRecords"
                                        :key="record.id"
                                        :class="{ selected: selectedRecord?.id === record.id, archived: record.validationStatus === 'Archived' }"
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
                                        <td>{{ record.frequency }}</td>
                                        <td>{{ formatCurrency(record.netSales) }}</td>
                                        <td>
                                            <span class="record-badge" :class="statusClass(record.validationStatus)">
                                                {{ statusLabel(record.validationStatus) }}
                                            </span>
                                        </td>
                                        <td>
                                            <button class="secondary-button compact-button queue-review-button" type="button" @click="selectRecord(record)">
                                                <AppIcon name="search" />
                                                View
                                            </button>
                                        </td>
                                    </tr>
                                </tbody>
                            </table>
                        </div>

                        <div v-if="filteredRecords.length" class="validation-card-list simple-card-list" aria-label="Mobile operational records list">
                            <article
                                v-for="record in filteredRecords"
                                :key="`${record.id}-card`"
                                class="validation-review-card simple-review-card"
                                :class="{ selected: selectedRecord?.id === record.id, archived: record.validationStatus === 'Archived' }"
                            >
                                <button class="mobile-card-main" type="button" @click="selectRecord(record)">
                                    <span class="source-icon">
                                        <AppIcon :name="sourceIcon(record.sourceChannel)" />
                                    </span>
                                    <span>
                                        <strong>{{ displayPeriod(record) }}</strong>
                                        <small>{{ record.id }} - {{ record.frequency }}</small>
                                    </span>
                                    <span class="record-badge" :class="statusClass(record.validationStatus)">
                                        {{ statusLabel(record.validationStatus) }}
                                    </span>
                                </button>

                                <div class="mobile-card-details">
                                    <div>
                                        <span>Source</span>
                                        <strong>{{ record.sourceChannel || 'Missing source' }}</strong>
                                    </div>
                                    <div>
                                        <span>Sales Amount</span>
                                        <strong>{{ formatCurrency(record.netSales) }}</strong>
                                    </div>
                                </div>

                                <button class="secondary-button compact-button" type="button" @click="selectRecord(record)">
                                    <AppIcon name="search" />
                                    View Details
                                </button>
                            </article>
                        </div>

                        <div v-else class="validation-empty records-empty-state">
                            <EmptyState
                                icon="database"
                                :title="emptyStateTitle"
                                :message="emptyStateMessage"
                                :action-label="emptyStateActionLabel"
                                @action="handleEmptyAction"
                            />
                        </div>
                    </ContentPanel>

                    <ContentPanel
                        icon="fileBarChart"
                        eyebrow="Record Details"
                        :title="selectedRecord ? selectedRecord.id : 'Guided Intake'"
                        :badge="selectedRecord ? statusLabel(selectedRecord.validationStatus) : 'Details'"
                        panel-class="record-review-panel simple-review-panel record-detail-panel"
                    >
                        <div v-if="selectedRecord" class="review-detail simple-review-detail">
                            <div class="review-record-header">
                                <span class="source-icon">
                                    <AppIcon :name="sourceIcon(selectedRecord.sourceChannel)" />
                                </span>
                                <div>
                                    <strong>{{ displayPeriod(selectedRecord) }}</strong>
                                    <p>{{ selectedRecord.sourceChannel || 'Missing source' }} - {{ selectedRecord.frequency }}</p>
                                </div>
                            </div>

                            <div class="record-details-list">
                                <div>
                                    <span>Sales Amount</span>
                                    <strong>{{ formatCurrency(selectedRecord.netSales) }}</strong>
                                </div>
                                <div>
                                    <span>Passenger Count</span>
                                    <strong>{{ displayPassengerCount(selectedRecord.passengerCount) }}</strong>
                                </div>
                                <div>
                                    <span>Package / Destination</span>
                                    <strong>{{ selectedRecord.packageDestination || 'Not specified' }}</strong>
                                </div>
                                <div>
                                    <span>Payment Status</span>
                                    <strong>{{ selectedRecord.paymentStatus || 'Not recorded' }}</strong>
                                </div>
                                <div>
                                    <span>Expense Amount</span>
                                    <strong>{{ formatCurrency(selectedRecord.expenseAmount) }}</strong>
                                </div>
                                <div>
                                    <span>Encoded By</span>
                                    <strong>{{ selectedRecord.encodedBy }}</strong>
                                </div>
                                <div>
                                    <span>Created</span>
                                    <strong>{{ selectedRecord.createdAt }}</strong>
                                </div>
                                <div>
                                    <span>Updated</span>
                                    <strong>{{ selectedRecord.updatedAt }}</strong>
                                </div>
                                <div class="details-wide">
                                    <span>Validation Notes</span>
                                    <strong>{{ validationNote(selectedRecord) }}</strong>
                                </div>
                                <div class="details-wide">
                                    <span>Notes</span>
                                    <strong>{{ selectedRecord.notes || 'No notes recorded' }}</strong>
                                </div>
                            </div>

                            <div class="review-answer-card">
                                <span class="answer-icon data">
                                    <AppIcon name="fileBarChart" />
                                </span>
                                <div>
                                    <p>Report Readiness</p>
                                    <strong>{{ reportReadinessText(selectedRecord) }}</strong>
                                </div>
                            </div>

                            <div class="review-actions simple-review-actions">
                                <button class="primary-button" type="button" @click="editRecord(selectedRecord)">
                                    <AppIcon name="edit" />
                                    Edit Record
                                </button>

                                <button
                                    class="secondary-button"
                                    type="button"
                                    @click="markReadyForReports(selectedRecord.id)"
                                >
                                    <AppIcon name="check" />
                                    Mark Ready for Reports
                                </button>

                                <button class="secondary-button" type="button" @click="setRecordStatus(selectedRecord.id, 'Needs Review')">
                                    <AppIcon name="alertTriangle" />
                                    Send to Needs Checking
                                </button>

                                <button class="secondary-button danger-button" type="button" @click="setRecordStatus(selectedRecord.id, 'Archived')">
                                    <AppIcon name="archive" />
                                    Archive Record
                                </button>
                            </div>
                        </div>

                        <div v-else class="review-placeholder simple-review-placeholder">
                            <span class="empty-state-icon" aria-hidden="true">
                                <AppIcon name="database" />
                            </span>
                            <h4>{{ records.length ? 'Select a record to view details' : 'Start with your first operational record' }}</h4>
                            <p>
                                {{ records.length
                                    ? 'Open a row to see passenger count, destination, notes, payment status, and readiness actions.'
                                    : 'Add a daily record or monthly sales summary to begin the internal data workflow.' }}
                            </p>
                            <button class="primary-button" type="button" @click="openCreate">
                                <AppIcon name="plus" />
                                Add Operational Record
                            </button>
                        </div>
                    </ContentPanel>
                </section>

                <div v-if="toastMessage" class="validation-toast" role="status">
                    <AppIcon :name="toastTone === 'warning' ? 'alertTriangle' : 'check'" />
                    <span>{{ toastMessage }}</span>
                </div>
            </section>
        </main>

        <button
            v-if="drawerOpen"
            class="drawer-backdrop"
            type="button"
            aria-label="Close operational record form"
            @click="closeDrawer"
        ></button>

        <aside class="record-drawer" :class="{ open: drawerOpen }" role="dialog" aria-modal="true" aria-label="Operational record form">
            <form class="record-form simple-record-form" @submit.prevent="saveRecord">
                <div class="drawer-header">
                    <div>
                        <p class="eyebrow">Operational Data Intake</p>
                        <h3>{{ editingRecordId ? 'Edit Operational Record' : 'Add Operational Record' }}</h3>
                        <p>Use this for a daily record or a monthly sales summary.</p>
                    </div>
                    <button class="icon-button" type="button" aria-label="Close form" @click="closeDrawer">
                        <AppIcon name="x" />
                    </button>
                </div>

                <div class="form-section">
                    <h4>Required Information</h4>
                    <p class="form-help">Sales amount, source, and date or month are needed for reliable reports. Enter 0 only when sales were truly zero.</p>

                    <div class="form-grid">
                        <label class="form-field">
                            <span>Record Frequency</span>
                            <select v-model="form.frequency">
                                <option value="Daily Record">Daily Record</option>
                                <option value="Monthly Summary">Monthly Summary</option>
                            </select>
                        </label>

                        <label v-if="form.frequency === 'Daily Record'" class="form-field">
                            <span>Record Date</span>
                            <input v-model="form.recordDate" type="date" />
                            <small v-if="formErrors.recordPeriod">{{ formErrors.recordPeriod }}</small>
                        </label>

                        <label v-else class="form-field">
                            <span>Record Month</span>
                            <input v-model="form.recordMonth" type="month" />
                            <small v-if="formErrors.recordPeriod">{{ formErrors.recordPeriod }}</small>
                        </label>

                        <label class="form-field">
                            <span>Source Channel</span>
                            <select v-model="form.sourceChannel">
                                <option value="">Select source</option>
                                <option v-for="source in sourceChannels" :key="source" :value="source">
                                    {{ source }}
                                </option>
                            </select>
                            <small v-if="formErrors.sourceChannel">{{ formErrors.sourceChannel }}</small>
                        </label>

                        <label class="form-field">
                            <span>Sales Amount</span>
                            <input v-model.trim="form.netSales" inputmode="decimal" placeholder="0.00" />
                            <small v-if="formErrors.netSales">{{ formErrors.netSales }}</small>
                        </label>
                    </div>
                </div>

                <div class="form-section">
                    <h4>Optional Details</h4>
                    <div class="form-grid">
                        <label class="form-field form-field-wide">
                            <span>Package / Destination Reference</span>
                            <input v-model.trim="form.packageDestination" placeholder="Optional destination or package label" />
                        </label>

                        <label class="form-field">
                            <span>Passenger Count</span>
                            <input v-model.trim="form.passengerCount" inputmode="numeric" placeholder="Optional" />
                            <small v-if="formErrors.passengerCount">{{ formErrors.passengerCount }}</small>
                        </label>

                        <label class="form-field">
                            <span>Expense Amount</span>
                            <input v-model.trim="form.expenseAmount" inputmode="decimal" placeholder="Optional" />
                            <small v-if="formErrors.expenseAmount">{{ formErrors.expenseAmount }}</small>
                        </label>

                        <label class="form-field">
                            <span>Payment Status</span>
                            <select v-model="form.paymentStatus">
                                <option value="">Optional</option>
                                <option v-for="status in paymentStatuses" :key="status" :value="status">
                                    {{ status }}
                                </option>
                            </select>
                        </label>

                        <label class="form-field form-field-wide">
                            <span>Notes</span>
                            <textarea v-model.trim="form.notes" rows="4" placeholder="Optional internal notes"></textarea>
                        </label>
                    </div>
                </div>

                <div class="form-readiness">
                    <span class="readiness-icon" :class="formReadiness.tone">
                        <AppIcon :name="formReadiness.icon" />
                    </span>
                    <div>
                        <strong>{{ formReadiness.label }}</strong>
                        <p>{{ formReadiness.description }}</p>
                    </div>
                </div>

                <div class="drawer-actions">
                    <button class="secondary-button" type="button" @click="closeDrawer">Cancel</button>
                    <button class="primary-button" type="submit">
                        <AppIcon name="save" />
                        {{ editingRecordId ? 'Save Changes' : 'Save Operational Record' }}
                    </button>
                </div>
            </form>
        </aside>
    </div>
</template>

<script>
import ContentPanel from '../Components/dashboard/ContentPanel.vue';
import StatCard from '../Components/dashboard/StatCard.vue';
import EmptyState from '../Components/feedback/EmptyState.vue';
import AppIcon from '../Components/icons/AppIcon.vue';
import Sidebar from '../Components/layout/Sidebar.vue';
import TopBar from '../Components/layout/TopBar.vue';
import { createNavigationGroups } from '../data/navigation';
import { requireMockAuth } from '../services/mockAuth';

const sourceChannels = ['Messenger', 'Google Sheets', 'Gmail', 'Notebook', 'Paper Record', 'Manual Encoding'];
const paymentStatuses = ['Unpaid', 'Partial', 'Paid', 'Refunded', 'Voided'];

function createBlankOperationalRecordForm() {
    return {
        frequency: 'Daily Record',
        recordDate: new Date().toISOString().slice(0, 10),
        recordMonth: new Date().toISOString().slice(0, 7),
        sourceChannel: '',
        packageDestination: '',
        netSales: '',
        expenseAmount: '',
        passengerCount: '',
        paymentStatus: '',
        encodedBy: 'Admin',
        notes: '',
    };
}

export default {
    name: 'OperationalRecords',
    components: {
        AppIcon,
        ContentPanel,
        EmptyState,
        Sidebar,
        StatCard,
        TopBar,
    },
    data() {
        return {
            sidebarOpen: false,
            drawerOpen: false,
            advancedFiltersOpen: false,
            editingRecordId: null,
            selectedRecordId: null,
            formErrors: {},
            toastMessage: '',
            toastTone: 'success',
            toastTimer: null,
            currentDate: new Intl.DateTimeFormat('en-US', {
                month: 'short',
                day: 'numeric',
                year: 'numeric',
            }).format(new Date()),
            navigationGroups: createNavigationGroups('Operational Records'),
            records: [],
            filters: {
                search: '',
                status: '',
                source: '',
                frequency: '',
                paymentStatus: '',
                dateFrom: '',
                dateTo: '',
                destination: '',
                encodedBy: '',
                missingField: '',
                forecastReadiness: '',
            },
            form: createBlankOperationalRecordForm(),
            sourceChannels,
            paymentStatuses,
        };
    },
    computed: {
        selectedRecord() {
            return this.records.find((record) => record.id === this.selectedRecordId) || null;
        },
        activeRecords() {
            return this.records.filter((record) => record.validationStatus !== 'Archived');
        },
        needsCheckingRecords() {
            return this.records.filter((record) => ['Draft', 'Needs Review'].includes(record.validationStatus));
        },
        readyRecords() {
            return this.records.filter((record) => record.validationStatus === 'Validated');
        },
        monthlySummaries() {
            return this.records.filter((record) => record.frequency === 'Monthly Summary');
        },
        recordsBadge() {
            return this.records.length ? `${this.activeRecords.length} active` : 'Empty';
        },
        summaryStats() {
            return [
                {
                    icon: 'database',
                    label: 'Total Records',
                    value: String(this.activeRecords.length),
                    note: this.activeRecords.length ? 'Active internal records' : 'No operational records yet',
                    status: this.activeRecords.length ? 'Active' : 'Empty',
                    tone: 'data',
                },
                {
                    icon: 'alertTriangle',
                    label: 'Needs Checking',
                    value: String(this.needsCheckingRecords.length),
                    note: 'Drafts or records still being reviewed',
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
                    icon: 'notebook',
                    label: 'Monthly Summaries',
                    value: String(this.monthlySummaries.length),
                    note: 'Useful for notebook-style owner records',
                    status: 'Monthly',
                    tone: 'primary',
                },
            ];
        },
        filteredRecords() {
            const searchTerm = this.filters.search.toLowerCase();

            return this.records.filter((record) => {
                if (!this.filters.status && record.validationStatus === 'Archived') {
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

                if (this.filters.paymentStatus && record.paymentStatus !== this.filters.paymentStatus) {
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

                if (this.filters.missingField && !this.recordMissingFields(record).includes(this.filters.missingField)) {
                    return false;
                }

                if (this.filters.forecastReadiness && !this.matchesForecastReadiness(record, this.filters.forecastReadiness)) {
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
                    this.statusLabel(record.validationStatus),
                    record.frequency,
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
            return this.records.length ? 'No records match these filters' : 'Start with your first operational record';
        },
        emptyStateMessage() {
            return this.records.length
                ? 'Try clearing filters or searching with a simpler term.'
                : 'Add a daily record or monthly sales summary from Messenger, Google Sheets, Gmail, notebooks, or paper records.';
        },
        emptyStateActionLabel() {
            return this.records.length ? 'Reset Filters' : 'Add Operational Record';
        },
        formReadiness() {
            const hasPeriod = this.form.frequency === 'Daily Record' ? Boolean(this.form.recordDate) : Boolean(this.form.recordMonth);
            const sales = this.parseOptionalNumber(this.form.netSales);

            if (!hasPeriod || !this.form.sourceChannel || sales === null) {
                return {
                    icon: 'alertTriangle',
                    tone: 'warning',
                    label: 'Needs required information',
                    description: 'Date or month, source, and sales amount are needed before this record is ready for reports.',
                };
            }

            if (this.parseOptionalInteger(this.form.passengerCount) === null) {
                return {
                    icon: 'fileBarChart',
                    tone: 'data',
                    label: 'Ready for sales reports',
                    description: 'Passenger count is optional and can be added later. Clean records can support future forecasting.',
                };
            }

            return {
                icon: 'check',
                tone: 'success',
                label: 'Ready for richer reports',
                description: 'This has the key information for sales summaries and passenger-related analysis later.',
            };
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
                this.closeDrawer();
            }
        },
        openCreate() {
            this.editingRecordId = null;
            this.form = createBlankOperationalRecordForm();
            this.formErrors = {};
            this.drawerOpen = true;
        },
        editRecord(record) {
            this.editingRecordId = record.id;
            this.form = {
                frequency: record.frequency,
                recordDate: record.frequency === 'Daily Record' ? record.period : new Date().toISOString().slice(0, 10),
                recordMonth: record.frequency === 'Monthly Summary' ? record.period : new Date().toISOString().slice(0, 7),
                sourceChannel: record.sourceChannel,
                packageDestination: record.packageDestination,
                netSales: this.numberToInput(record.netSales),
                expenseAmount: this.numberToInput(record.expenseAmount),
                passengerCount: record.passengerCount === null ? '' : String(record.passengerCount),
                paymentStatus: record.paymentStatus,
                encodedBy: record.encodedBy || 'Admin',
                notes: record.notes,
            };
            this.formErrors = {};
            this.drawerOpen = true;
        },
        closeDrawer() {
            this.drawerOpen = false;
        },
        saveRecord() {
            if (!this.validateForm()) {
                return;
            }

            const now = this.currentDate;
            const previousRecord = this.records.find((record) => record.id === this.editingRecordId);
            const record = {
                id: this.editingRecordId || this.nextRecordId(),
                frequency: this.form.frequency,
                period: this.form.frequency === 'Daily Record' ? this.form.recordDate : this.form.recordMonth,
                sourceChannel: this.form.sourceChannel,
                packageDestination: this.form.packageDestination,
                netSales: this.parseOptionalNumber(this.form.netSales),
                expenseAmount: this.parseOptionalNumber(this.form.expenseAmount),
                passengerCount: this.parseOptionalInteger(this.form.passengerCount),
                paymentStatus: this.form.paymentStatus,
                validationStatus: previousRecord?.validationStatus || 'Draft',
                encodedBy: this.form.encodedBy || 'Admin',
                notes: this.form.notes,
                createdAt: previousRecord?.createdAt || now,
                updatedAt: now,
            };

            if (this.editingRecordId) {
                this.records = this.records.map((existingRecord) =>
                    existingRecord.id === this.editingRecordId ? record : existingRecord,
                );
                this.showToast(`${record.id} updated.`, 'success');
            } else {
                this.records = [record, ...this.records];
                this.showToast(`${record.id} added.`, 'success');
            }

            this.selectedRecordId = record.id;
            this.closeDrawer();
        },
        validateForm() {
            const errors = {};
            const period = this.form.frequency === 'Daily Record' ? this.form.recordDate : this.form.recordMonth;

            if (!period) {
                errors.recordPeriod = this.form.frequency === 'Daily Record'
                    ? 'Record date is required.'
                    : 'Record month is required.';
            }

            if (!this.form.sourceChannel) {
                errors.sourceChannel = 'Source channel is required.';
            }

            this.requireNumericField(errors, 'netSales', 'Sales amount is required. Use 0 only for true zero sales.');
            this.validateOptionalNumericField(errors, 'expenseAmount');
            this.validateOptionalNumericField(errors, 'passengerCount', true);

            this.formErrors = errors;
            return Object.keys(errors).length === 0;
        },
        requireNumericField(errors, field, message) {
            if (this.form[field] === '') {
                errors[field] = message;
                return;
            }

            if (this.parseOptionalNumber(this.form[field]) === null) {
                errors[field] = 'Use a valid numeric amount.';
            }
        },
        validateOptionalNumericField(errors, field, integerOnly = false) {
            if (this.form[field] === '') {
                return;
            }

            const value = integerOnly
                ? this.parseOptionalInteger(this.form[field])
                : this.parseOptionalNumber(this.form[field]);

            if (value === null) {
                errors[field] = integerOnly ? 'Use a whole number.' : 'Use a valid numeric amount.';
            }
        },
        parseOptionalNumber(value) {
            if (value === '' || value === null || value === undefined) {
                return null;
            }

            const parsed = Number(value);
            return Number.isFinite(parsed) && parsed >= 0 ? parsed : null;
        },
        parseOptionalInteger(value) {
            if (value === '' || value === null || value === undefined) {
                return null;
            }

            const parsed = Number(value);
            return Number.isInteger(parsed) && parsed >= 0 ? parsed : null;
        },
        numberToInput(value) {
            return value === null || value === undefined ? '' : String(value);
        },
        nextRecordId() {
            return `OR-${String(this.records.length + 1).padStart(4, '0')}`;
        },
        selectRecord(record) {
            this.selectedRecordId = record.id;
        },
        markReadyForReports(recordId) {
            const record = this.records.find((item) => item.id === recordId);

            if (!record) {
                return;
            }

            if (!this.canMarkReady(record)) {
                this.showToast('This record still needs date or month, source, and sales amount before it is ready.', 'warning');
                this.setRecordStatus(recordId, 'Needs Review', false);
                return;
            }

            this.setRecordStatus(recordId, 'Validated');
        },
        setRecordStatus(recordId, status, showFeedback = true) {
            this.records = this.records.map((record) =>
                record.id === recordId
                    ? { ...record, validationStatus: status, updatedAt: this.currentDate }
                    : record,
            );

            if (showFeedback) {
                this.showToast(`${recordId} moved to ${this.statusLabel(status)}.`, status === 'Needs Review' ? 'warning' : 'success');
            }
        },
        resetFilters() {
            this.filters = {
                search: '',
                status: '',
                source: '',
                frequency: '',
                paymentStatus: '',
                dateFrom: '',
                dateTo: '',
                destination: '',
                encodedBy: '',
                missingField: '',
                forecastReadiness: '',
            };
        },
        handleEmptyAction() {
            if (this.records.length) {
                this.resetFilters();
                return;
            }

            this.openCreate();
        },
        showToast(message, tone = 'success') {
            this.toastMessage = message;
            this.toastTone = tone;
            window.clearTimeout(this.toastTimer);
            this.toastTimer = window.setTimeout(() => {
                this.toastMessage = '';
            }, 2600);
        },
        filterLabel(key) {
            const labels = {
                search: 'Search',
                status: 'Status',
                source: 'Source',
            };

            return labels[key] || key;
        },
        matchesStatusFilter(record, filter) {
            if (filter === 'Needs Checking') {
                return ['Draft', 'Needs Review'].includes(record.validationStatus);
            }

            return this.statusLabel(record.validationStatus) === filter || record.validationStatus === filter;
        },
        matchesForecastReadiness(record, filter) {
            if (filter === 'ready') {
                return this.canMarkReady(record);
            }

            if (filter === 'needs-info') {
                return !this.canMarkReady(record);
            }

            if (filter === 'sales-only') {
                return this.canMarkReady(record) && record.passengerCount === null;
            }

            return true;
        },
        recordMissingFields(record) {
            const fields = [];

            if (!record.period) {
                fields.push('period');
            }

            if (!record.sourceChannel) {
                fields.push('source');
            }

            if (record.netSales === null || record.netSales === undefined) {
                fields.push('sales');
            }

            if (record.passengerCount === null || record.passengerCount === undefined) {
                fields.push('passenger');
            }

            return fields;
        },
        canMarkReady(record) {
            return Boolean(record.period)
                && Boolean(record.sourceChannel)
                && record.netSales !== null
                && record.netSales !== undefined
                && Number.isFinite(Number(record.netSales));
        },
        validationNote(record) {
            if (!this.canMarkReady(record)) {
                return 'Date or month, source, and sales amount must be clear before reports use this record.';
            }

            if (record.passengerCount === null || record.passengerCount === undefined) {
                return 'Passenger count is optional. This record can still support sales reports.';
            }

            return 'This record has the main information needed for reports.';
        },
        reportReadinessText(record) {
            if (!this.canMarkReady(record)) {
                return 'This record still needs required information before it is ready for reports.';
            }

            return 'This record is ready for basic reports. Clean records can support future forecasting.';
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
        recordComparableDate(record) {
            if (!record.period) {
                return '';
            }

            return record.frequency === 'Monthly Summary' ? `${record.period}-01` : record.period;
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
        displayPeriod(record) {
            if (!record.period) {
                return 'Missing date';
            }

            if (record.frequency === 'Monthly Summary') {
                const date = new Date(`${record.period}-01T00:00:00`);
                return new Intl.DateTimeFormat('en-US', {
                    month: 'long',
                    year: 'numeric',
                }).format(date);
            }

            return new Intl.DateTimeFormat('en-US', {
                month: 'short',
                day: 'numeric',
                year: 'numeric',
            }).format(new Date(`${record.period}T00:00:00`));
        },
        formatCurrency(value) {
            if (value === null || value === undefined) {
                return 'Not recorded';
            }

            return new Intl.NumberFormat('en-PH', {
                style: 'currency',
                currency: 'PHP',
            }).format(value);
        },
        displayPassengerCount(value) {
            return value === null || value === undefined ? 'Not recorded' : String(value);
        },
    },
};
</script>
