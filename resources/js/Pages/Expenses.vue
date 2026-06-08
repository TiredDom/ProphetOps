<template>
    <AppShell
        active-label="Expenses"
        eyebrow="Costs"
        title="Expenses & Operational Costs"
        description="Track tour operations, marketing, seasonal, and overhead costs for DSS financial analysis."
    >
        <section class="dss-page">
            <section class="module-intro">
                <div>
                    <div class="hero-status warning-status">
                        <span class="status-dot"></span>
                        Cost Monitoring
                    </div>
                    <h2>Keep operating costs visible beside bookings.</h2>
                    <p>Marketing cost increased this period. Compare with booking conversion before approving new campaigns.</p>
                </div>
                <button class="primary-button" type="button" @click="openDrawer(null)">
                    <AppIcon name="plus" />
                    Add Expense
                </button>
            </section>

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

            <section class="warning-band">
                <AppIcon name="alertTriangle" />
                <div>
                    <strong>Cost insight</strong>
                    <p>Observed data -> marketing and seasonal costs increased. Business meaning -> profit may narrow. Suggested action -> compare cost with confirmed bookings.</p>
                </div>
            </section>

            <ContentPanel icon="wallet" eyebrow="Expense Records" title="Operational Costs" badge="Mock data">
                <div class="dss-filterbar">
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
                </div>

                <div class="table-scroll">
                    <table class="dss-table">
                        <thead>
                            <tr>
                                <th>Date</th>
                                <th>Category</th>
                                <th>Amount</th>
                                <th>Related Package</th>
                                <th>Payment</th>
                                <th>Notes</th>
                                <th>Action</th>
                            </tr>
                        </thead>
                        <tbody>
                            <tr v-for="expense in filteredExpenses" :key="expense.id">
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
                </div>
            </ContentPanel>

            <div v-if="drawerOpen" class="drawer-backdrop" @click.self="closeDrawer">
                <aside class="record-drawer sprint-drawer">
                    <div class="drawer-header">
                        <div>
                            <p class="eyebrow">Expense Details</p>
                            <h3>{{ form.id || 'New Expense' }}</h3>
                        </div>
                        <button class="icon-button" type="button" aria-label="Close" @click="closeDrawer">
                            <AppIcon name="x" />
                        </button>
                    </div>
                    <div class="form-grid">
                        <label class="account-field"><span>Date</span><input v-model="form.date" type="date" /></label>
                        <label class="account-field"><span>Category</span><select v-model="form.category"><option>Tour operations</option><option>Marketing</option><option>Seasonal cost</option><option>Overhead</option></select></label>
                        <label class="account-field"><span>Amount</span><input v-model.number="form.amount" type="number" min="0" /></label>
                        <label class="account-field"><span>Related package</span><input v-model="form.package" /></label>
                        <label class="account-field"><span>Payment status</span><select v-model="form.paymentStatus"><option>Paid</option><option>Pending</option></select></label>
                        <label class="account-field field-wide"><span>Notes</span><textarea v-model="form.notes"></textarea></label>
                    </div>
                    <div class="drawer-actions">
                        <button class="secondary-button" type="button" @click="closeDrawer">Cancel</button>
                        <button class="primary-button" type="button" @click="saveExpense"><AppIcon name="save" />Save</button>
                    </div>
                </aside>
            </div>
        </section>
    </AppShell>
</template>

<script>
import AppShell from '../Components/layout/AppShell.vue';
import ContentPanel from '../Components/dashboard/ContentPanel.vue';
import StatCard from '../Components/dashboard/StatCard.vue';
import AppIcon from '../Components/icons/AppIcon.vue';
import { cloneRecords, expenses, expensesByCategory, formatCurrency } from '../data/mockData';

export default {
    name: 'Expenses',
    components: { AppIcon, AppShell, ContentPanel, StatCard },
    data() {
        return {
            records: cloneRecords(expenses),
            filters: { search: '', category: '', paymentStatus: '' },
            drawerOpen: false,
            form: {},
        };
    },
    computed: {
        categories() {
            return [...new Set(this.records.map((expense) => expense.category))].sort();
        },
        totalExpenses() {
            return this.records.reduce((sum, expense) => sum + expense.amount, 0);
        },
        costliestCategory() {
            return expensesByCategory(this.records)[0];
        },
        stats() {
            return [
                { icon: 'wallet', label: 'Total Costs', value: formatCurrency(this.totalExpenses), note: 'Recorded mock operating costs', status: 'Watch', tone: 'warning' },
                { icon: 'fileBarChart', label: 'Costliest Category', value: this.costliestCategory?.category || 'None', note: formatCurrency(this.costliestCategory?.amount || 0), status: 'Review', tone: 'primary' },
                { icon: 'calendar', label: 'Pending Payments', value: String(this.records.filter((expense) => expense.paymentStatus === 'Pending').length), note: 'Need payment follow-up', status: 'Pending', tone: 'warning' },
                { icon: 'sparkles', label: 'DSS Signal', value: 'Cost rising', note: 'Compare with booking conversion', status: 'Insight', tone: 'data' },
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
        openDrawer(expense) {
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
            this.drawerOpen = false;
        },
        saveExpense() {
            const existing = this.records.some((expense) => expense.id === this.form.id);
            this.records = existing
                ? this.records.map((expense) => expense.id === this.form.id ? { ...this.form } : expense)
                : [{ ...this.form }, ...this.records];
            this.closeDrawer();
        },
        statusClass(value) {
            return `status-${String(value).toLowerCase().replace(/[^a-z0-9]+/g, '-')}`;
        },
    },
};
</script>
