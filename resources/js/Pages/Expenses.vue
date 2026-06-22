<template>
    <AppShell
        active-label="Expenses"
        eyebrow="Costs"
        title="Expenses & Operational Costs"
        description=""
    >
        <section class="dss-page">
            <section class="module-intro">
                <div>
                    <div class="hero-status warning-status">
                        <span class="status-dot"></span>
                        Cost Records
                    </div>
                    <h2>Operational costs</h2>
                </div>
                <button class="primary-button" type="button" @click="openDrawer(null)">
                    <AppIcon name="plus" />
                    Add Expense
                </button>
            </section>

            <ActionNotice
                v-if="pageNotice.message"
                :tone="pageNotice.tone"
                :title="pageNotice.title"
                :message="pageNotice.message"
            />

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

            <DecisionSignal
                badge="Decision Signal"
                tone="warning"
                icon="alertTriangle"
                :meta="['Cost watch']"
                title="Marketing and seasonal costs need review."
                description="Check spend against confirmed bookings."
                :chips="costSignalChips"
                action-label="Filter Cost Driver"
                @action="filters.category = costliestCategory?.category || ''"
            />

            <ContentPanel icon="wallet" eyebrow="Expense Records" title="Operational Costs" badge="Cost log">
                <FilterBar label="Expense filters">
                    <label class="search-control">
                        <AppIcon name="search" />
                        <input v-model.trim="filters.search" type="search" placeholder="Search expense, package, notes" />
                    </label>
                    <label class="filter-control">
                        <span>Category</span>
                        <select v-model="filters.category">
                            <option value="">All categories</option>
                            <option v-for="category in categories" :key="category">{{ category }}</option>
                        </select>
                    </label>
                    <label class="filter-control">
                        <span>Payment</span>
                        <select v-model="filters.paymentStatus">
                            <option value="">All payments</option>
                            <option>Paid</option>
                            <option>Pending</option>
                        </select>
                    </label>
                </FilterBar>

                <BulkActionBar
                    :selected-count="selectedExpenseCount"
                    description="Batch-update payment status for selected expense records."
                    :actions="expenseBulkActions"
                    @run-action="runExpenseBulkAction"
                    @clear="clearExpenseSelection"
                />

                <DataTableFrame label="Expenses table">
                    <table class="dss-table">
                        <thead>
                            <tr>
                                <th class="row-select-heading">
                                    <label class="row-select-control">
                                        <input
                                            type="checkbox"
                                            :checked="allVisibleExpensesSelected"
                                            :disabled="!filteredExpenses.length"
                                            aria-label="Select all visible expenses"
                                            @change="toggleAllVisibleExpenses"
                                        />
                                    </label>
                                </th>
                                <th>Date</th>
                                <th>Category</th>
                                <th>Amount</th>
                                <th>Related Package / Operation</th>
                                <th>Payment</th>
                                <th>Notes</th>
                                <th>Action</th>
                            </tr>
                        </thead>
                        <tbody>
                            <tr v-for="expense in filteredExpenses" :key="expense.id">
                                <td class="row-select-cell">
                                    <label class="row-select-control">
                                        <input
                                            type="checkbox"
                                            :checked="isExpenseSelected(expense.id)"
                                            :aria-label="`Select expense ${expense.id}`"
                                            @change="toggleExpenseSelection(expense.id)"
                                        />
                                    </label>
                                </td>
                                <td><strong>{{ expense.id }}</strong><span>{{ expense.date }}</span></td>
                                <td>{{ expense.category }}</td>
                                <td>{{ formatCurrency(expense.amount) }}</td>
                                <td>{{ expense.package }}</td>
                                <td><span class="record-badge" :class="statusClass(expense.paymentStatus)">{{ expense.paymentStatus }}</span></td>
                                <td>{{ expense.notes }}</td>
                                <td>
                                    <button class="secondary-button compact-button" type="button" @click="openDrawer(expense)">
                                        <AppIcon name="edit" />
                                        Edit
                                    </button>
                                </td>
                            </tr>
                        </tbody>
                    </table>
                </DataTableFrame>

                <EmptyState
                    v-if="!filteredExpenses.length"
                    icon="wallet"
                    :title="emptyExpensesTitle"
                    :message="emptyExpensesMessage"
                    :action-label="emptyExpensesActionLabel"
                    :action-icon="emptyExpensesActionIcon"
                    @action="handleEmptyExpensesAction"
                />
            </ContentPanel>

            <AppDrawer
                v-if="drawerOpen"
                eyebrow="Expense Details"
                :title="form.id || 'New Expense'"
                description=""
                @close="closeDrawer"
            >
                    <ActionNotice
                        v-if="formNotice.message"
                        :tone="formNotice.tone"
                        :title="formNotice.title"
                        :message="formNotice.message"
                    />

                    <div class="form-grid">
                        <label class="account-field">
                            <span>Date</span>
                            <input v-model="form.date" type="date" required :aria-invalid="Boolean(formErrors.date)" />
                            <small v-if="formErrors.date">{{ formErrors.date }}</small>
                        </label>
                        <label class="account-field"><span>Category</span><select v-model="form.category"><option>Tour operations</option><option>Marketing</option><option>Seasonal cost</option><option>Overhead</option></select></label>
                        <label class="account-field">
                            <span>Amount</span>
                            <input v-model.number="form.amount" type="number" min="0" required :aria-invalid="Boolean(formErrors.amount)" />
                            <small v-if="formErrors.amount">{{ formErrors.amount }}</small>
                        </label>
                        <label class="account-field">
                            <span>Related package or operation</span>
                            <input v-model.trim="form.package" maxlength="120" required :aria-invalid="Boolean(formErrors.package)" />
                            <small v-if="formErrors.package">{{ formErrors.package }}</small>
                        </label>
                        <label class="account-field"><span>Payment status</span><select v-model="form.paymentStatus"><option>Paid</option><option>Pending</option></select></label>
                        <label class="account-field field-wide"><span>Notes</span><textarea v-model="form.notes" maxlength="320"></textarea></label>
                    </div>

                <template #footer>
                        <button class="secondary-button" type="button" :disabled="isSaving" @click="closeDrawer">Cancel</button>
                        <button class="primary-button" type="button" :disabled="isSaving" @click="saveExpense">
                            <span v-if="isSaving" class="loading-dot" aria-hidden="true"></span>
                            <AppIcon v-else name="save" />
                            {{ isSaving ? 'Saving...' : 'Save Expense' }}
                        </button>
                </template>
            </AppDrawer>
        </section>
    </AppShell>
</template>

<script>
import { router } from '@inertiajs/vue3';
import AppShell from '../Components/layout/AppShell.vue';
import ActionNotice from '../Components/feedback/ActionNotice.vue';
import ContentPanel from '../Components/dashboard/ContentPanel.vue';
import DataTableFrame from '../Components/records/DataTableFrame.vue';
import DecisionSignal from '../Components/dss/DecisionSignal.vue';
import AppDrawer from '../Components/feedback/AppDrawer.vue';
import BulkActionBar from '../Components/records/BulkActionBar.vue';
import EmptyState from '../Components/feedback/EmptyState.vue';
import FilterBar from '../Components/records/FilterBar.vue';
import StatCard from '../Components/dashboard/StatCard.vue';
import AppIcon from '../Components/icons/AppIcon.vue';
import { cloneRecords, expensesByCategory, formatCurrency } from '../utils/formatters';

export default {
    name: 'Expenses',
    components: {
        ActionNotice,
        AppDrawer,
        AppIcon,
        AppShell,
        BulkActionBar,
        ContentPanel,
        DataTableFrame,
        DecisionSignal,
        EmptyState,
        FilterBar,
        StatCard,
    },
    props: {
        expenses: {
            type: Array,
            default: () => [],
        },
    },
    data() {
        return {
            records: cloneRecords(this.expenses),
            filters: { search: '', category: '', paymentStatus: '' },
            selectedExpenseIds: [],
            drawerOpen: false,
            formErrors: {},
            formNotice: {},
            isSaving: false,
            pageNotice: this.$page.props.flash?.notice || {},
            form: {},
        };
    },
    watch: {
        expenses: {
            handler(records) {
                this.records = cloneRecords(records);
            },
            deep: true,
        },
        '$page.props.flash.notice': {
            handler(notice) {
                if (notice) {
                    this.pageNotice = notice;
                }
            },
        },
    },
    computed: {
        categories() {
            return [...new Set(this.records.map((expense) => expense.category))].sort();
        },
        hasActiveFilters() {
            return Boolean(this.filters.search || this.filters.category || this.filters.paymentStatus);
        },
        emptyExpensesTitle() {
            return this.hasActiveFilters ? 'No expenses match these filters' : 'No expenses recorded yet';
        },
        emptyExpensesMessage() {
            if (this.hasActiveFilters) {
                return 'Clear the filters to return to all expense records, or adjust one filter at a time.';
            }

            return 'Add the first cost record so the DSS can compare spending against confirmed bookings.';
        },
        emptyExpensesActionLabel() {
            return this.hasActiveFilters ? 'Clear Filters' : 'Add Expense';
        },
        emptyExpensesActionIcon() {
            return this.hasActiveFilters ? 'x' : 'plus';
        },
        visibleExpenseIds() {
            return this.filteredExpenses.map((expense) => expense.id);
        },
        selectedExpenseCount() {
            return this.selectedExpenseIds.length;
        },
        allVisibleExpensesSelected() {
            return Boolean(this.visibleExpenseIds.length)
                && this.visibleExpenseIds.every((id) => this.selectedExpenseIds.includes(id));
        },
        expenseBulkActions() {
            return [
                { key: 'paid', label: 'Mark Paid', icon: 'check' },
                { key: 'pending', label: 'Mark Pending', icon: 'alertTriangle' },
            ];
        },
        totalExpenses() {
            return this.records.reduce((sum, expense) => sum + expense.amount, 0);
        },
        costliestCategory() {
            return expensesByCategory(this.records)[0];
        },
        costSignalChips() {
            return [
                this.costliestCategory?.category || 'Cost driver',
                formatCurrency(this.costliestCategory?.amount || 0),
                'Conversion check',
            ];
        },
        stats() {
            return [
                { icon: 'wallet', label: 'Total Costs', value: formatCurrency(this.totalExpenses), note: 'Recorded operating costs', status: 'Watch', tone: 'warning' },
                { icon: 'fileBarChart', label: 'Costliest Category', value: this.costliestCategory?.category || 'None', note: formatCurrency(this.costliestCategory?.amount || 0), status: 'Review', tone: 'primary' },
                { icon: 'calendar', label: 'Pending Payments', value: String(this.records.filter((expense) => expense.paymentStatus === 'Pending').length), note: 'Need payment follow-up', status: 'Pending', tone: 'warning' },
            ];
        },
        filteredExpenses() {
            const term = this.filters.search.toLowerCase();
            return this.records.filter((expense) => {
                if (this.filters.category && expense.category !== this.filters.category) return false;
                if (this.filters.paymentStatus && expense.paymentStatus !== this.filters.paymentStatus) return false;
                if (!term) return true;
                return [expense.id, expense.category, expense.package, expense.notes].some((value) => String(value).toLowerCase().includes(term));
            });
        },
    },
    methods: {
        formatCurrency,
        clearFilters() {
            this.filters = {
                search: '',
                category: '',
                paymentStatus: '',
            };
        },
        handleEmptyExpensesAction() {
            if (this.hasActiveFilters) {
                this.clearFilters();
                return;
            }

            this.openDrawer(null);
        },
        isExpenseSelected(id) {
            return this.selectedExpenseIds.includes(id);
        },
        toggleExpenseSelection(id) {
            this.selectedExpenseIds = this.isExpenseSelected(id)
                ? this.selectedExpenseIds.filter((selectedId) => selectedId !== id)
                : [...this.selectedExpenseIds, id];
        },
        toggleAllVisibleExpenses() {
            if (this.allVisibleExpensesSelected) {
                this.selectedExpenseIds = this.selectedExpenseIds
                    .filter((id) => !this.visibleExpenseIds.includes(id));
                return;
            }

            this.selectedExpenseIds = [...new Set([...this.selectedExpenseIds, ...this.visibleExpenseIds])];
        },
        clearExpenseSelection() {
            this.selectedExpenseIds = [];
        },
        runExpenseBulkAction(action) {
            router.patch('/expenses/bulk', {
                ids: this.selectedExpenseIds,
                action,
            }, {
                preserveScroll: true,
                onSuccess: () => {
                    this.clearExpenseSelection();
                },
            });
        },
        openDrawer(expense) {
            this.formErrors = {};
            this.formNotice = {};
            this.form = expense ? { ...expense } : {
                id: `EXP-${3100 + this.records.length + 1}`,
                date: new Date().toISOString().slice(0, 10),
                category: 'Tour operations',
                amount: 0,
                package: '',
                paymentStatus: 'Pending',
                notes: '',
            };
            this.drawerOpen = true;
        },
        closeDrawer() {
            if (this.isSaving) {
                return;
            }

            this.drawerOpen = false;
            this.formErrors = {};
            this.formNotice = {};
        },
        validateExpenseForm() {
            const errors = {};
            const amount = Number(this.form.amount);

            if (!this.form.date) {
                errors.date = 'Choose the expense date.';
            }

            if (!Number.isFinite(amount) || this.form.amount === '' || amount < 0) {
                errors.amount = 'Expense amount must be zero or more.';
            }

            if (!String(this.form.package || '').trim()) {
                errors.package = 'Enter the related package or operation.';
            }

            this.formErrors = errors;
            this.formNotice = Object.keys(errors).length
                ? {
                    tone: 'error',
                    title: 'Review the expense details',
                    message: 'Fix the highlighted fields before saving this expense.',
                }
                : {};

            return Object.keys(errors).length === 0;
        },
        saveExpense() {
            if (this.isSaving || !this.validateExpenseForm()) {
                return;
            }

            this.isSaving = true;
            const existing = this.records.some((expense) => expense.id === this.form.id);
            const method = existing ? 'put' : 'post';
            const url = existing ? `/expenses/${encodeURIComponent(this.form.id)}` : '/expenses';

            router[method](url, this.form, {
                preserveScroll: true,
                onSuccess: () => {
                    this.drawerOpen = false;
                    this.formErrors = {};
                    this.formNotice = {};
                },
                onError: (errors) => {
                    this.formErrors = {
                        date: errors.date,
                        amount: errors.amount,
                        package: errors.package,
                    };
                    this.formNotice = {
                        tone: 'error',
                        title: 'Review the expense details',
                        message: 'Fix the highlighted fields before saving this expense.',
                    };
                },
                onFinish: () => {
                    this.isSaving = false;
                },
            });
        },
        statusClass(value) {
            return `status-${String(value).toLowerCase().replace(/[^a-z0-9]+/g, '-')}`;
        },
    },
};
</script>
