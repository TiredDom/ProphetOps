<template>
    <AppShell
        active-label="Bookings"
        eyebrow="Records"
        title="Bookings & Transactions"
        description=""
    >
        <section class="dss-page">
            <section class="module-intro">
                <div>
                    <div class="hero-status">
                        <span class="status-dot"></span>
                        Booking Records
                    </div>
                    <h2>Transactions</h2>
                </div>
                <button class="primary-button" type="button" @click="openDrawer(null, 'add')">
                    <AppIcon name="plus" />
                    Add Booking
                </button>
            </section>

            <ActionNotice
                v-if="pageNotice.message"
                :tone="pageNotice.tone"
                :title="pageNotice.title"
                :message="pageNotice.message"
            />

            <ContentPanel icon="database" eyebrow="Booking Records" title="Transactions Table" badge="Records">
                <FilterBar class-name="booking-filterbar" label="Booking filters">
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
                </FilterBar>

                <BulkActionBar
                    :selected-count="selectedBookingCount"
                    description="Update visible booking records without opening each drawer."
                    :actions="bookingBulkActions"
                    @run-action="runBookingBulkAction"
                    @clear="clearBookingSelection"
                />

                <DataTableFrame label="Bookings table">
                    <table class="dss-table">
                        <thead>
                            <tr>
                                <th class="row-select-heading">
                                    <label class="row-select-control">
                                        <input
                                            type="checkbox"
                                            :checked="allVisibleBookingsSelected"
                                            :disabled="!filteredBookings.length"
                                            aria-label="Select all visible bookings"
                                            @change="toggleAllVisibleBookings"
                                        />
                                    </label>
                                </th>
                                <th>Booking ID</th>
                                <th>Client / Partner</th>
                                <th>Destination / Package</th>
                                <th>Passengers</th>
                                <th>Gross Revenue</th>
                                <th>Payment</th>
                                <th>Status</th>
                                <th>Action</th>
                            </tr>
                        </thead>
                        <tbody>
                            <tr v-for="booking in filteredBookings" :key="booking.id">
                                <td class="row-select-cell">
                                    <label class="row-select-control">
                                        <input
                                            type="checkbox"
                                            :checked="isBookingSelected(booking.id)"
                                            :aria-label="`Select booking ${booking.id}`"
                                            @change="toggleBookingSelection(booking.id)"
                                        />
                                    </label>
                                </td>
                                <td><strong>{{ booking.id }}</strong></td>
                                <td>{{ booking.client }}</td>
                                <td><strong>{{ booking.package }}</strong><span>{{ booking.destination }}</span></td>
                                <td>{{ booking.y }}</td>
                                <td>{{ formatCurrency(booking.grossRevenue) }}</td>
                                <td><span class="record-badge" :class="statusClass(booking.paymentStatus)">{{ booking.paymentStatus }}</span></td>
                                <td><span class="record-badge" :class="statusClass(booking.bookingStatus)">{{ booking.bookingStatus }}</span></td>
                                <td>
                                    <button class="secondary-button compact-button" type="button" @click="openDrawer(booking, 'view')">
                                        <AppIcon name="search" />
                                        View
                                    </button>
                                </td>
                            </tr>
                        </tbody>
                    </table>
                </DataTableFrame>

                <EmptyState
                    v-if="!filteredBookings.length"
                    icon="database"
                    :title="emptyBookingsTitle"
                    :message="emptyBookingsMessage"
                    :action-label="emptyBookingsActionLabel"
                    :action-icon="emptyBookingsActionIcon"
                    @action="handleEmptyBookingsAction"
                />
            </ContentPanel>

            <AppDrawer
                v-if="drawerOpen"
                :eyebrow="drawerEyebrow"
                :title="drawerTitle"
                description=""
                @close="closeDrawer"
            >
                    <ActionNotice
                        v-if="formNotice.message"
                        :tone="formNotice.tone"
                        :title="formNotice.title"
                        :message="formNotice.message"
                    />

                    <div v-if="drawerMode === 'add'" class="booking-method-panel">
                        <div>
                            <p class="eyebrow">Booking setup</p>
                            <h4>{{ bookingEntryType === 'preset' ? 'Use a package preset' : 'Create a manual quotation' }}</h4>
                            <p>{{ bookingMethodCopy }}</p>
                        </div>
                        <div class="segment-control">
                            <button
                                type="button"
                                :class="{ active: bookingEntryType === 'preset' }"
                                @click="setBookingEntryType('preset')"
                            >
                                Package preset
                            </button>
                            <button
                                type="button"
                                :class="{ active: bookingEntryType === 'manual' }"
                                @click="setBookingEntryType('manual')"
                            >
                                Manual quotation
                            </button>
                        </div>
                    </div>

                    <div class="form-grid">
                        <template v-if="drawerMode === 'add' && bookingEntryType === 'preset'">
                            <label class="account-field field-wide">
                                <span>Package preset</span>
                                <select v-model="selectedPackageId" @change="applyPackagePreset">
                                    <option v-for="preset in packagePresets" :key="preset.id" :value="preset.id">
                                        {{ preset.packageName }}
                                    </option>
                                </select>
                            </label>
                            <div v-if="selectedPackagePreset" class="preset-summary-card field-wide">
                                <div>
                                    <span>Destination</span>
                                    <strong>{{ selectedPackagePreset.destination }}</strong>
                                </div>
                                <div>
                                    <span>Base price</span>
                                    <strong>{{ formatCurrency(selectedPackagePreset.basePrice) }}</strong>
                                </div>
                                <div>
                                    <span>Available slots</span>
                                    <strong>{{ selectedPackagePreset.availableSlots }}</strong>
                                </div>
                                <div>
                                    <span>Duration</span>
                                    <strong>{{ selectedPackagePreset.duration }}</strong>
                                </div>
                                <p>{{ selectedPackagePreset.inclusions }}</p>
                            </div>
                        </template>

                        <label class="account-field">
                            <span>Booking date</span>
                            <input
                                v-model="form.ds"
                                type="date"
                                required
                                :aria-invalid="Boolean(formErrors.ds)"
                            />
                            <small v-if="formErrors.ds">{{ formErrors.ds }}</small>
                        </label>
                        <label class="account-field">
                            <span>Client / agency partner</span>
                            <input
                                v-model.trim="form.client"
                                maxlength="120"
                                required
                                :aria-invalid="Boolean(formErrors.client)"
                            />
                            <small v-if="formErrors.client">{{ formErrors.client }}</small>
                        </label>
                        <label v-if="drawerMode !== 'add' || bookingEntryType === 'manual'" class="account-field">
                            <span>Destination</span>
                            <input
                                v-model.trim="form.destination"
                                maxlength="120"
                                required
                                :aria-invalid="Boolean(formErrors.destination)"
                            />
                            <small v-if="formErrors.destination">{{ formErrors.destination }}</small>
                        </label>
                        <label v-if="drawerMode !== 'add' || bookingEntryType === 'manual'" class="account-field">
                            <span>Package</span>
                            <input
                                v-model.trim="form.package"
                                maxlength="120"
                                required
                                :aria-invalid="Boolean(formErrors.package)"
                            />
                            <small v-if="formErrors.package">{{ formErrors.package }}</small>
                        </label>
                        <label class="account-field">
                            <span>Passenger count</span>
                            <input
                                v-model.number="form.y"
                                type="number"
                                min="1"
                                required
                                :aria-invalid="Boolean(formErrors.y)"
                                @input="syncPresetRevenue"
                            />
                            <small v-if="formErrors.y">{{ formErrors.y }}</small>
                        </label>
                        <label class="account-field">
                            <span>{{ bookingEntryType === 'preset' && drawerMode === 'add' ? 'Quoted revenue' : 'Gross revenue' }}</span>
                            <input
                                v-model.number="form.grossRevenue"
                                type="number"
                                min="0"
                                required
                                :aria-invalid="Boolean(formErrors.grossRevenue)"
                            />
                            <small v-if="formErrors.grossRevenue">{{ formErrors.grossRevenue }}</small>
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
                            <input v-model.trim="form.staffAssigned" maxlength="80" />
                        </label>
                        <label class="account-field field-wide">
                            <span>Notes</span>
                            <textarea v-model="form.notes" maxlength="320"></textarea>
                        </label>
                    </div>

                <template #footer>
                        <button class="secondary-button" type="button" :disabled="isSaving" @click="closeDrawer">Cancel</button>
                        <button class="primary-button" type="button" :disabled="isSaving" @click="saveBooking">
                            <span v-if="isSaving" class="loading-dot" aria-hidden="true"></span>
                            <AppIcon v-else name="save" />
                            {{ isSaving ? 'Saving...' : 'Save Booking' }}
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
import AppDrawer from '../Components/feedback/AppDrawer.vue';
import BulkActionBar from '../Components/records/BulkActionBar.vue';
import EmptyState from '../Components/feedback/EmptyState.vue';
import FilterBar from '../Components/records/FilterBar.vue';
import AppIcon from '../Components/icons/AppIcon.vue';
import { cloneRecords, formatCurrency } from '../utils/formatters';

export default {
    name: 'Bookings',
    components: {
        ActionNotice,
        AppIcon,
        AppDrawer,
        AppShell,
        BulkActionBar,
        ContentPanel,
        DataTableFrame,
        EmptyState,
        FilterBar,
    },
    props: {
        bookings: {
            type: Array,
            default: () => [],
        },
        packages: {
            type: Array,
            default: () => [],
        },
    },
    data() {
        return {
            records: cloneRecords(this.bookings),
            filters: {
                search: '',
                package: '',
                paymentStatus: '',
                bookingStatus: '',
            },
            drawerOpen: false,
            drawerMode: 'view',
            formErrors: {},
            formNotice: {},
            isSaving: false,
            pageNotice: this.$page.props.flash?.notice || {},
            bookingEntryType: 'preset',
            selectedPackageId: this.packages[0]?.id || '',
            selectedBookingIds: [],
            form: {},
        };
    },
    watch: {
        bookings: {
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
        packagePresets() {
            return this.packages;
        },
        selectedPackagePreset() {
            return this.packagePresets.find((preset) => preset.id === this.selectedPackageId) || null;
        },
        drawerEyebrow() {
            if (this.drawerMode !== 'add') {
                return 'Booking Details';
            }

            return this.bookingEntryType === 'preset' ? 'Package Preset Booking' : 'Manual Quotation';
        },
        drawerTitle() {
            if (this.drawerMode !== 'add') {
                return this.form.id || 'Booking Details';
            }

            return this.bookingEntryType === 'preset' ? 'New Package Booking' : 'New Manual Quotation';
        },
        bookingMethodCopy() {
            if (this.bookingEntryType === 'preset') {
                return 'Select a saved bundle to fill the package, destination, base price, and capacity context.';
            }

            return 'Use this for custom trips, special pricing, or one-off requests not yet saved in the catalog.';
        },
        packageOptions() {
            return [...new Set(this.records.map((booking) => booking.package))].sort();
        },
        hasActiveFilters() {
            return Boolean(
                this.filters.search
                || this.filters.package
                || this.filters.paymentStatus
                || this.filters.bookingStatus,
            );
        },
        emptyBookingsTitle() {
            return this.hasActiveFilters ? 'No bookings match these filters' : 'No bookings recorded yet';
        },
        emptyBookingsMessage() {
            if (this.hasActiveFilters) {
                return 'Clear the filters to return to all bookings, or adjust one filter at a time.';
            }

            return 'Add the first booking to start building the decision-support dataset.';
        },
        emptyBookingsActionLabel() {
            return this.hasActiveFilters ? 'Clear Filters' : 'Add Booking';
        },
        emptyBookingsActionIcon() {
            return this.hasActiveFilters ? 'x' : 'plus';
        },
        visibleBookingIds() {
            return this.filteredBookings.map((booking) => booking.id);
        },
        selectedBookingCount() {
            return this.selectedBookingIds.length;
        },
        allVisibleBookingsSelected() {
            return Boolean(this.visibleBookingIds.length)
                && this.visibleBookingIds.every((id) => this.selectedBookingIds.includes(id));
        },
        bookingBulkActions() {
            return [
                { key: 'confirm', label: 'Mark Confirmed', icon: 'check' },
                { key: 'paid', label: 'Mark Paid', icon: 'wallet' },
            ];
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
        clearFilters() {
            this.filters = {
                search: '',
                package: '',
                paymentStatus: '',
                bookingStatus: '',
            };
        },
        handleEmptyBookingsAction() {
            if (this.hasActiveFilters) {
                this.clearFilters();
                return;
            }

            this.openDrawer(null, 'add');
        },
        isBookingSelected(id) {
            return this.selectedBookingIds.includes(id);
        },
        toggleBookingSelection(id) {
            this.selectedBookingIds = this.isBookingSelected(id)
                ? this.selectedBookingIds.filter((selectedId) => selectedId !== id)
                : [...this.selectedBookingIds, id];
        },
        toggleAllVisibleBookings() {
            if (this.allVisibleBookingsSelected) {
                this.selectedBookingIds = this.selectedBookingIds
                    .filter((id) => !this.visibleBookingIds.includes(id));
                return;
            }

            this.selectedBookingIds = [...new Set([...this.selectedBookingIds, ...this.visibleBookingIds])];
        },
        clearBookingSelection() {
            this.selectedBookingIds = [];
        },
        runBookingBulkAction(action) {
            router.patch('/bookings/bulk', {
                ids: this.selectedBookingIds,
                action,
            }, {
                preserveScroll: true,
                onSuccess: () => {
                    this.clearBookingSelection();
                },
            });
        },
        setBookingEntryType(type) {
            this.bookingEntryType = type;

            if (type === 'preset') {
                this.applyPackagePreset();
                return;
            }

            this.form = {
                ...this.form,
                packageId: '',
                entryType: 'Manual quotation',
                source: 'Manual quotation',
            };
        },
        applyPackagePreset() {
            const preset = this.selectedPackagePreset;

            if (!preset) {
                return;
            }

            this.form = {
                ...this.form,
                packageId: preset.id,
                entryType: 'Package preset',
                source: 'Package preset',
                package: preset.packageName,
                destination: preset.destination,
                grossRevenue: Number(this.form.y || 1) * Number(preset.basePrice || 0),
            };
        },
        syncPresetRevenue() {
            if (this.drawerMode === 'add' && this.bookingEntryType === 'preset') {
                this.applyPackagePreset();
            }
        },
        openDrawer(booking, mode) {
            this.drawerMode = mode;
            this.formErrors = {};
            this.formNotice = {};
            this.bookingEntryType = booking?.entryType === 'Manual quotation' ? 'manual' : 'preset';
            this.selectedPackageId = booking?.packageId
                || this.packagePresets.find((preset) => preset.packageName === booking?.package)?.id
                || this.packagePresets[0]?.id
                || '';

            if (booking) {
                this.form = { ...booking };
            } else {
                this.bookingEntryType = 'preset';
                this.form = {
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
                    source: 'Package preset',
                    packageId: this.selectedPackageId,
                    entryType: 'Package preset',
                    notes: '',
                };
                this.applyPackagePreset();
            }
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
        validateBookingForm() {
            const errors = {};
            const passengerCount = Number(this.form.y);
            const grossRevenue = Number(this.form.grossRevenue);

            if (!this.form.ds) {
                errors.ds = 'Choose the booking date.';
            }

            if (!String(this.form.client || '').trim()) {
                errors.client = 'Enter the client or agency partner.';
            }

            if (!String(this.form.destination || '').trim()) {
                errors.destination = 'Enter the destination.';
            }

            if (!String(this.form.package || '').trim()) {
                errors.package = 'Enter the package name.';
            }

            if (!Number.isFinite(passengerCount) || passengerCount < 1) {
                errors.y = 'Passenger count must be at least 1.';
            }

            if (!Number.isFinite(grossRevenue) || this.form.grossRevenue === '' || grossRevenue < 0) {
                errors.grossRevenue = 'Revenue must be zero or more.';
            }

            this.formErrors = errors;
            this.formNotice = Object.keys(errors).length
                ? {
                    tone: 'error',
                    title: 'Review the booking details',
                    message: 'Fix the highlighted fields before saving this booking.',
                }
                : {};

            return Object.keys(errors).length === 0;
        },
        saveBooking() {
            if (this.isSaving || !this.validateBookingForm()) {
                return;
            }

            this.isSaving = true;
            const existing = this.records.some((booking) => booking.id === this.form.id);
            const method = existing ? 'put' : 'post';
            const url = existing ? `/bookings/${encodeURIComponent(this.form.id)}` : '/bookings';

            router[method](url, this.form, {
                preserveScroll: true,
                onSuccess: () => {
                    this.drawerOpen = false;
                    this.formErrors = {};
                    this.formNotice = {};
                },
                onError: (errors) => {
                    this.formErrors = this.mapServerErrors(errors);
                    this.formNotice = {
                        tone: 'error',
                        title: 'Review the booking details',
                        message: 'Fix the highlighted fields before saving this booking.',
                    };
                },
                onFinish: () => {
                    this.isSaving = false;
                },
            });
        },
        mapServerErrors(errors) {
            return {
                ds: errors.ds,
                client: errors.client,
                destination: errors.destination,
                package: errors.package,
                y: errors.y,
                grossRevenue: errors.grossRevenue,
            };
        },
        statusClass(value) {
            return `status-${String(value).toLowerCase().replace(/[^a-z0-9]+/g, '-')}`;
        },
    },
};
</script>
