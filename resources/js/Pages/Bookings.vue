<template>
    <AppShell
        active-label="Bookings"
        eyebrow="Records"
        title="Bookings & Transactions"
        description="Centralize booking records from Messenger, Sheets, Gmail, and notebooks for future forecasting."
    >
        <section class="dss-page">
            <section class="module-intro">
                <div>
                    <div class="hero-status">
                        <span class="status-dot"></span>
                        Historical Data Source
                    </div>
                    <h2>Build the sales history ProphetOps will analyze later.</h2>
                    <p>Each booking record contributes booking date, passenger demand, revenue, package, and status data.</p>
                </div>
                <button class="primary-button" type="button" @click="openDrawer(null, 'add')">
                    <AppIcon name="plus" />
                    Add Booking
                </button>
            </section>

            <ContentPanel icon="database" eyebrow="Booking Records" title="Transactions Table" badge="Mock data">
                <div class="dss-filterbar booking-filterbar">
                    <label class="search-control">
                        <AppIcon name="search" />
                        <input v-model.trim="filters.search" type="search" placeholder="Search client, package, booking ID" />
                    </label>
                    <label class="filter-control">
                        <span>Package</span>
                        <select v-model="filters.package">
                            <option value="">All packages</option>
                            <option v-for="packageName in packageOptions" :key="packageName">{{ packageName }}</option>
                        </select>
                    </label>
                    <label class="filter-control">
                        <span>Payment</span>
                        <select v-model="filters.paymentStatus">
                            <option value="">All payments</option>
                            <option>Paid</option>
                            <option>Partially Paid</option>
                            <option>Pending</option>
                        </select>
                    </label>
                    <label class="filter-control">
                        <span>Status</span>
                        <select v-model="filters.bookingStatus">
                            <option value="">All statuses</option>
                            <option>Confirmed</option>
                            <option>Reserved</option>
                            <option>Pending</option>
                        </select>
                    </label>
                </div>

                <div class="table-scroll">
                    <table class="dss-table">
                        <thead>
                            <tr>
                                <th>Booking ID</th>
                                <th>Date</th>
                                <th>Client / Partner</th>
                                <th>Destination / Package</th>
                                <th>Passengers</th>
                                <th>Gross Revenue</th>
                                <th>Payment</th>
                                <th>Status</th>
                                <th>Staff</th>
                                <th>Action</th>
                            </tr>
                        </thead>
                        <tbody>
                            <tr v-for="booking in filteredBookings" :key="booking.id">
                                <td><strong>{{ booking.id }}</strong><span>{{ booking.source }}</span></td>
                                <td>{{ booking.ds }}</td>
                                <td>{{ booking.client }}</td>
                                <td><strong>{{ booking.destination }}</strong><span>{{ booking.package }}</span></td>
                                <td>{{ booking.y }}</td>
                                <td>{{ formatCurrency(booking.grossRevenue) }}</td>
                                <td><span class="record-badge" :class="statusClass(booking.paymentStatus)">{{ booking.paymentStatus }}</span></td>
                                <td><span class="record-badge" :class="statusClass(booking.bookingStatus)">{{ booking.bookingStatus }}</span></td>
                                <td>{{ booking.staffAssigned }}</td>
                                <td>
                                    <button class="secondary-button compact-button" type="button" @click="openDrawer(booking, 'view')">
                                        <AppIcon name="search" />
                                        View
                                    </button>
                                </td>
                            </tr>
                        </tbody>
                    </table>
                </div>

                <EmptyState
                    v-if="!filteredBookings.length"
                    icon="database"
                    title="No bookings match these filters"
                    message="Add your first booking to start building the forecasting dataset."
                    action-label="Add Booking"
                    @action="openDrawer(null, 'add')"
                />
            </ContentPanel>

            <div v-if="drawerOpen" class="drawer-backdrop" @click.self="closeDrawer">
                <aside class="record-drawer sprint-drawer">
                    <div class="drawer-header">
                        <div>
                            <p class="eyebrow">{{ drawerMode === 'add' ? 'Add Booking' : 'Booking Details' }}</p>
                            <h3>{{ form.id || 'New Booking' }}</h3>
                        </div>
                        <button class="icon-button" type="button" aria-label="Close" @click="closeDrawer">
                            <AppIcon name="x" />
                        </button>
                    </div>

                    <div class="form-grid">
                        <label class="account-field">
                            <span>Booking date</span>
                            <input v-model="form.ds" type="date" />
                        </label>
                        <label class="account-field">
                            <span>Client / agency partner</span>
                            <input v-model="form.client" />
                        </label>
                        <label class="account-field">
                            <span>Destination</span>
                            <input v-model="form.destination" />
                        </label>
                        <label class="account-field">
                            <span>Package</span>
                            <input v-model="form.package" />
                        </label>
                        <label class="account-field">
                            <span>Passenger count</span>
                            <input v-model.number="form.y" type="number" min="1" />
                        </label>
                        <label class="account-field">
                            <span>Gross revenue</span>
                            <input v-model.number="form.grossRevenue" type="number" min="0" />
                        </label>
                        <label class="account-field">
                            <span>Payment status</span>
                            <select v-model="form.paymentStatus">
                                <option>Paid</option>
                                <option>Partially Paid</option>
                                <option>Pending</option>
                            </select>
                        </label>
                        <label class="account-field">
                            <span>Booking status</span>
                            <select v-model="form.bookingStatus">
                                <option>Confirmed</option>
                                <option>Reserved</option>
                                <option>Pending</option>
                            </select>
                        </label>
                        <label class="account-field">
                            <span>Staff assigned</span>
                            <input v-model="form.staffAssigned" />
                        </label>
                        <label class="account-field field-wide">
                            <span>Notes</span>
                            <textarea v-model="form.notes"></textarea>
                        </label>
                    </div>

                    <div class="drawer-actions">
                        <button class="secondary-button" type="button" @click="closeDrawer">Cancel</button>
                        <button class="primary-button" type="button" @click="saveBooking">
                            <AppIcon name="save" />
                            Save
                        </button>
                    </div>
                </aside>
            </div>
        </section>
    </AppShell>
</template>

<script>
import AppShell from '../Components/layout/AppShell.vue';
import ContentPanel from '../Components/dashboard/ContentPanel.vue';
import EmptyState from '../Components/feedback/EmptyState.vue';
import AppIcon from '../Components/icons/AppIcon.vue';
import { bookings, cloneRecords, formatCurrency } from '../data/mockData';

export default {
    name: 'Bookings',
    components: {
        AppIcon,
        AppShell,
        ContentPanel,
        EmptyState,
    },
    data() {
        return {
            records: cloneRecords(bookings),
            filters: {
                search: '',
                package: '',
                paymentStatus: '',
                bookingStatus: '',
            },
            drawerOpen: false,
            drawerMode: 'view',
            form: {},
        };
    },
    computed: {
        packageOptions() {
            return [...new Set(this.records.map((booking) => booking.package))].sort();
        },
        filteredBookings() {
            const term = this.filters.search.toLowerCase();

            return this.records.filter((booking) => {
                if (this.filters.package && booking.package !== this.filters.package) {
                    return false;
                }

                if (this.filters.paymentStatus && booking.paymentStatus !== this.filters.paymentStatus) {
                    return false;
                }

                if (this.filters.bookingStatus && booking.bookingStatus !== this.filters.bookingStatus) {
                    return false;
                }

                if (!term) {
                    return true;
                }

                return [booking.id, booking.client, booking.package, booking.destination, booking.staffAssigned]
                    .some((value) => String(value).toLowerCase().includes(term));
            });
        },
    },
    methods: {
        formatCurrency,
        openDrawer(booking, mode) {
            this.drawerMode = mode;
            this.form = booking
                ? { ...booking }
                : {
                    id: `BKG-${2400 + this.records.length + 1}`,
                    ds: new Date().toISOString().slice(0, 10),
                    y: 1,
                    client: '',
                    package: '',
                    destination: '',
                    grossRevenue: 0,
                    paymentStatus: 'Pending',
                    bookingStatus: 'Pending',
                    staffAssigned: 'Staff User',
                    source: 'Manual',
                    notes: '',
                };
            this.drawerOpen = true;
        },
        closeDrawer() {
            this.drawerOpen = false;
        },
        saveBooking() {
            const existing = this.records.some((booking) => booking.id === this.form.id);
            this.records = existing
                ? this.records.map((booking) => booking.id === this.form.id ? { ...this.form } : booking)
                : [{ ...this.form }, ...this.records];
            this.closeDrawer();
        },
        statusClass(value) {
            return `status-${String(value).toLowerCase().replace(/[^a-z0-9]+/g, '-')}`;
        },
    },
};
</script>
