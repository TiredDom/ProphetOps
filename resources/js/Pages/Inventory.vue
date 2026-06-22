<template>
    <AppShell
        active-label="Package Catalog"
        eyebrow="Operations"
        title="Package Catalog"
        description=""
    >
        <section class="dss-page">
            <section class="module-intro">
                <div>
                    <div class="hero-status">
                        <span class="status-dot"></span>
                        Booking Bundle Storage
                    </div>
                    <h2>Package presets</h2>
                </div>
                <div class="module-actions">
                    <button class="primary-button" type="button" @click="openDrawer(null, 'add')">
                        <AppIcon name="plus" />
                        Add Preset
                    </button>
                    <button class="secondary-button" type="button" :disabled="!selectedItem" @click="openDrawer(selectedItem, 'adjust')">
                        <AppIcon name="edit" />
                        Adjust Capacity
                    </button>
                </div>
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
                v-if="lowItems.length"
                badge="Decision Signal"
                tone="warning"
                icon="alertTriangle"
                :meta="['Capacity watch']"
                :title="lowSignalTitle"
                description="Review package slots before approving more promos."
                :chips="lowSignalChips"
                action-label="Show Limited Capacity"
                @action="filters.lowOnly = true"
            />

            <ContentPanel icon="boxes" eyebrow="Package Catalog" title="Booking Bundles" badge="Records">
                <FilterBar label="Package catalog filters">
                    <label class="search-control">
                        <AppIcon name="search" />
                        <input v-model.trim="filters.search" type="search" placeholder="Search preset or destination" />
                    </label>
                    <label class="filter-control">
                        <span>Status</span>
                        <select v-model="filters.status">
                            <option value="">All statuses</option>
                            <option>Normal</option>
                            <option>Low</option>
                            <option>Critical</option>
                        </select>
                    </label>
                    <label class="remember-control low-filter">
                        <input v-model="filters.lowOnly" type="checkbox" />
                        <span>Limited capacity only</span>
                    </label>
                </FilterBar>

                <BulkActionBar
                    :selected-count="selectedInventoryCount"
                    description="Batch-update capacity status for selected package presets."
                    :actions="inventoryBulkActions"
                    @run-action="runInventoryBulkAction"
                    @clear="clearInventorySelection"
                />

                <DataTableFrame label="Package catalog table">
                    <table class="dss-table">
                        <thead>
                            <tr>
                                <th class="row-select-heading">
                                    <label class="row-select-control">
                                        <input
                                            type="checkbox"
                                            :checked="allVisibleInventorySelected"
                                            :disabled="!filteredInventory.length"
                                            aria-label="Select all visible package presets"
                                            @change="toggleAllVisibleInventory"
                                        />
                                    </label>
                                </th>
                                <th>Package</th>
                                <th>Destination</th>
                                <th>Base Price</th>
                                <th>Duration</th>
                                <th>Type</th>
                                <th>Slots</th>
                                <th>Status</th>
                                <th>Action</th>
                            </tr>
                        </thead>
                        <tbody>
                            <tr v-for="item in filteredInventory" :key="item.id" :class="{ selected: selectedItem?.id === item.id }">
                                <td class="row-select-cell">
                                    <label class="row-select-control">
                                        <input
                                            type="checkbox"
                                            :checked="isInventorySelected(item.id)"
                                            :aria-label="`Select package preset ${item.packageName}`"
                                            @change="toggleInventorySelection(item.id)"
                                        />
                                    </label>
                                </td>
                                <td><strong>{{ item.packageName }}</strong><span>{{ item.id }}</span></td>
                                <td>{{ item.destination }}</td>
                                <td>{{ formatCurrency(item.basePrice) }}</td>
                                <td>{{ item.duration }}</td>
                                <td>{{ item.travelType }}</td>
                                <td><strong>{{ item.availableSlots }} open</strong><span>{{ item.soldCount }} booked / {{ item.reservedCount }} reserved</span></td>
                                <td><span class="record-badge" :class="statusClass(item.status)">{{ item.status }}</span></td>
                                <td>
                                    <button class="secondary-button compact-button" type="button" @click="openDrawer(item, 'view')">
                                        <AppIcon name="search" />
                                        View
                                    </button>
                                </td>
                            </tr>
                        </tbody>
                    </table>
                </DataTableFrame>

                <EmptyState
                    v-if="!filteredInventory.length"
                    icon="boxes"
                    :title="emptyInventoryTitle"
                    :message="emptyInventoryMessage"
                    :action-label="emptyInventoryActionLabel"
                    :action-icon="emptyInventoryActionIcon"
                    @action="handleEmptyInventoryAction"
                />
            </ContentPanel>

            <AppDrawer
                v-if="drawerOpen"
                :eyebrow="drawerMode === 'adjust' ? 'Adjust Capacity' : 'Package Preset'"
                :title="form.packageName || 'New Package'"
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
                            <span>Package name</span>
                            <input
                                v-model.trim="form.packageName"
                                maxlength="120"
                                required
                                :aria-invalid="Boolean(formErrors.packageName)"
                            />
                            <small v-if="formErrors.packageName">{{ formErrors.packageName }}</small>
                        </label>
                        <label class="account-field">
                            <span>Destination</span>
                            <input
                                v-model.trim="form.destination"
                                maxlength="120"
                                required
                                :aria-invalid="Boolean(formErrors.destination)"
                            />
                            <small v-if="formErrors.destination">{{ formErrors.destination }}</small>
                        </label>
                        <label class="account-field">
                            <span>Duration</span>
                            <input v-model.trim="form.duration" maxlength="80" />
                        </label>
                        <label class="account-field">
                            <span>Base price per passenger</span>
                            <input
                                v-model.number="form.basePrice"
                                type="number"
                                min="0"
                                required
                                :aria-invalid="Boolean(formErrors.basePrice)"
                            />
                            <small v-if="formErrors.basePrice">{{ formErrors.basePrice }}</small>
                        </label>
                        <label class="account-field">
                            <span>Travel type</span>
                            <select v-model="form.travelType">
                                <option>Leisure</option>
                                <option>Educational</option>
                                <option>Cultural</option>
                                <option>Adventure</option>
                                <option>Corporate</option>
                            </select>
                        </label>
                        <label class="account-field">
                            <span>Available slots</span>
                            <input
                                v-model.number="form.availableSlots"
                                type="number"
                                min="0"
                                required
                                :aria-invalid="Boolean(formErrors.availableSlots)"
                            />
                            <small v-if="formErrors.availableSlots">{{ formErrors.availableSlots }}</small>
                        </label>
                        <label class="account-field">
                            <span>Booked slots</span>
                            <input v-model.number="form.soldCount" type="number" min="0" />
                        </label>
                        <label class="account-field">
                            <span>Reserved slots</span>
                            <input v-model.number="form.reservedCount" type="number" min="0" />
                        </label>
                        <label class="account-field">
                            <span>Status</span>
                            <select v-model="form.status">
                                <option>Normal</option>
                                <option>Low</option>
                                <option>Critical</option>
                            </select>
                        </label>
                        <label class="account-field">
                            <span>Supplier reliability</span>
                            <input
                                v-model.number="form.supplierReliabilityScore"
                                type="number"
                                min="0"
                                max="100"
                                required
                                :aria-invalid="Boolean(formErrors.supplierReliabilityScore)"
                            />
                            <small v-if="formErrors.supplierReliabilityScore">{{ formErrors.supplierReliabilityScore }}</small>
                        </label>
                        <label class="account-field">
                            <span>Business value</span>
                            <input
                                v-model.number="form.businessValueScore"
                                type="number"
                                min="0"
                                max="100"
                                required
                                :aria-invalid="Boolean(formErrors.businessValueScore)"
                            />
                            <small v-if="formErrors.businessValueScore">{{ formErrors.businessValueScore }}</small>
                        </label>
                        <label class="account-field">
                            <span>Risk score</span>
                            <input
                                v-model.number="form.riskScore"
                                type="number"
                                min="0"
                                max="100"
                                required
                                :aria-invalid="Boolean(formErrors.riskScore)"
                            />
                            <small v-if="formErrors.riskScore">{{ formErrors.riskScore }}</small>
                        </label>
                        <label class="account-field field-wide">
                            <span>Included in preset</span>
                            <textarea v-model="form.inclusions" maxlength="420"></textarea>
                        </label>
                    </div>

                <template #footer>
                        <button class="secondary-button" type="button" :disabled="isSaving" @click="closeDrawer">Cancel</button>
                        <button class="primary-button" type="button" :disabled="isSaving" @click="savePackage">
                            <span v-if="isSaving" class="loading-dot" aria-hidden="true"></span>
                            <AppIcon v-else name="save" />
                            {{ isSaving ? 'Saving...' : 'Save Preset' }}
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
import { cloneRecords, formatCurrency, formatNumber } from '../utils/formatters';

export default {
    name: 'Inventory',
    components: {
        ActionNotice,
        AppIcon,
        AppDrawer,
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
        packages: {
            type: Array,
            default: () => [],
        },
    },
    data() {
        return {
            inventoryItems: cloneRecords(this.packages),
            filters: {
                search: '',
                status: '',
                lowOnly: false,
            },
            selectedId: this.packages[0]?.id || null,
            selectedInventoryIds: [],
            drawerOpen: false,
            drawerMode: 'view',
            formErrors: {},
            formNotice: {},
            isSaving: false,
            pageNotice: this.$page.props.flash?.notice || {},
            form: {},
        };
    },
    watch: {
        packages: {
            handler(records) {
                this.inventoryItems = cloneRecords(records);
                if (!this.selectedId && records.length) {
                    this.selectedId = records[0].id;
                }
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
        selectedItem() {
            return this.inventoryItems.find((item) => item.id === this.selectedId) || null;
        },
        lowItems() {
            return this.inventoryItems.filter((item) => ['Low', 'Critical'].includes(item.status));
        },
        lowSignalTitle() {
            return `${this.lowItems.length} ${this.lowItems.length === 1 ? 'package needs' : 'packages need'} review`;
        },
        lowSignalChips() {
            return [
                ...this.lowItems.slice(0, 2).map((item) => `${item.packageName}: ${item.availableSlots} slots`),
                `${this.lowItems.length} watch item${this.lowItems.length === 1 ? '' : 's'}`,
            ];
        },
        hasActiveFilters() {
            return Boolean(this.filters.search || this.filters.status || this.filters.lowOnly);
        },
        emptyInventoryTitle() {
            return this.hasActiveFilters ? 'No presets match these filters' : 'No package presets yet';
        },
        emptyInventoryMessage() {
            if (this.hasActiveFilters) {
                return 'Clear the filters to return to all package presets, or adjust one filter at a time.';
            }

            return 'Add the first reusable package preset so staff can quote and track capacity consistently.';
        },
        emptyInventoryActionLabel() {
            return this.hasActiveFilters ? 'Clear Filters' : 'Add Preset';
        },
        emptyInventoryActionIcon() {
            return this.hasActiveFilters ? 'x' : 'plus';
        },
        visibleInventoryIds() {
            return this.filteredInventory.map((item) => item.id);
        },
        selectedInventoryCount() {
            return this.selectedInventoryIds.length;
        },
        allVisibleInventorySelected() {
            return Boolean(this.visibleInventoryIds.length)
                && this.visibleInventoryIds.every((id) => this.selectedInventoryIds.includes(id));
        },
        inventoryBulkActions() {
            return [
                { key: 'normal', label: 'Mark Normal', icon: 'check' },
                { key: 'low', label: 'Mark Low', icon: 'alertTriangle' },
            ];
        },
        stats() {
            return [
                { icon: 'boxes', label: 'Total Presets', value: formatNumber(this.inventoryItems.length), note: 'Reusable booking bundles', status: 'Active', tone: 'primary' },
                { icon: 'alertTriangle', label: 'Limited Capacity', value: formatNumber(this.lowItems.length), note: 'Needs review before promos', status: 'Review', tone: 'warning' },
                { icon: 'users', label: 'Booked Slots', value: formatNumber(this.inventoryItems.reduce((sum, item) => sum + item.soldCount, 0)), note: 'Booked count across packages', status: 'Demand', tone: 'data' },
                { icon: 'calendar', label: 'Reserved Slots', value: formatNumber(this.inventoryItems.reduce((sum, item) => sum + item.reservedCount, 0)), note: 'Potential upcoming bookings', status: 'Watch', tone: 'success' },
            ];
        },
        filteredInventory() {
            const term = this.filters.search.toLowerCase();

            return this.inventoryItems.filter((item) => {
                if (this.filters.status && item.status !== this.filters.status) {
                    return false;
                }

                if (this.filters.lowOnly && !['Low', 'Critical'].includes(item.status)) {
                    return false;
                }

                if (!term) {
                    return true;
                }

                return [item.packageName, item.destination, item.id]
                    .some((value) => String(value).toLowerCase().includes(term));
            });
        },
    },
    methods: {
        formatCurrency,
        clearFilters() {
            this.filters = {
                search: '',
                status: '',
                lowOnly: false,
            };
        },
        handleEmptyInventoryAction() {
            if (this.hasActiveFilters) {
                this.clearFilters();
                return;
            }

            this.openDrawer(null, 'add');
        },
        isInventorySelected(id) {
            return this.selectedInventoryIds.includes(id);
        },
        toggleInventorySelection(id) {
            this.selectedInventoryIds = this.isInventorySelected(id)
                ? this.selectedInventoryIds.filter((selectedId) => selectedId !== id)
                : [...this.selectedInventoryIds, id];
        },
        toggleAllVisibleInventory() {
            if (this.allVisibleInventorySelected) {
                this.selectedInventoryIds = this.selectedInventoryIds
                    .filter((id) => !this.visibleInventoryIds.includes(id));
                return;
            }

            this.selectedInventoryIds = [...new Set([...this.selectedInventoryIds, ...this.visibleInventoryIds])];
        },
        clearInventorySelection() {
            this.selectedInventoryIds = [];
        },
        runInventoryBulkAction(action) {
            router.patch('/inventory/bulk', {
                ids: this.selectedInventoryIds,
                action,
            }, {
                preserveScroll: true,
                onSuccess: () => {
                    this.clearInventorySelection();
                },
            });
        },
        openDrawer(item, mode) {
            this.drawerMode = mode;
            this.selectedId = item?.id || this.selectedId;
            this.formErrors = {};
            this.formNotice = {};
            this.form = item
                ? { ...item }
                : {
                    id: `PKG-${100 + this.inventoryItems.length + 1}`,
                    packageName: '',
                    destination: '',
                    duration: '',
                    basePrice: 0,
                    inclusions: '',
                    travelType: 'Leisure',
                    supplierReliabilityScore: 80,
                    businessValueScore: 75,
                    riskScore: 25,
                    availableSlots: 0,
                    soldCount: 0,
                    reservedCount: 0,
                    status: 'Normal',
                    lastUpdated: new Date().toISOString().slice(0, 10),
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
        validatePackageForm() {
            const errors = {};
            const basePrice = Number(this.form.basePrice);
            const availableSlots = Number(this.form.availableSlots);
            const supplierReliabilityScore = Number(this.form.supplierReliabilityScore);
            const businessValueScore = Number(this.form.businessValueScore);
            const riskScore = Number(this.form.riskScore);

            if (!String(this.form.packageName || '').trim()) {
                errors.packageName = 'Enter the package name.';
            }

            if (!String(this.form.destination || '').trim()) {
                errors.destination = 'Enter the destination.';
            }

            if (!Number.isFinite(basePrice) || this.form.basePrice === '' || basePrice < 0) {
                errors.basePrice = 'Base price must be zero or more.';
            }

            if (!Number.isFinite(availableSlots) || this.form.availableSlots === '' || availableSlots < 0) {
                errors.availableSlots = 'Available slots must be zero or more.';
            }

            if (!String(this.form.travelType || '').trim()) {
                errors.travelType = 'Select the travel type.';
            }

            if (!Number.isFinite(supplierReliabilityScore) || supplierReliabilityScore < 0 || supplierReliabilityScore > 100) {
                errors.supplierReliabilityScore = 'Use a value from 0 to 100.';
            }

            if (!Number.isFinite(businessValueScore) || businessValueScore < 0 || businessValueScore > 100) {
                errors.businessValueScore = 'Use a value from 0 to 100.';
            }

            if (!Number.isFinite(riskScore) || riskScore < 0 || riskScore > 100) {
                errors.riskScore = 'Use a value from 0 to 100.';
            }

            this.formErrors = errors;
            this.formNotice = Object.keys(errors).length
                ? {
                    tone: 'error',
                    title: 'Review the package preset',
                    message: 'Fix the highlighted fields before saving this package.',
                }
                : {};

            return Object.keys(errors).length === 0;
        },
        savePackage() {
            if (this.isSaving || !this.validatePackageForm()) {
                return;
            }

            this.isSaving = true;
            const existing = this.inventoryItems.some((item) => item.id === this.form.id);
            const method = existing ? 'put' : 'post';
            const url = existing ? `/inventory/${encodeURIComponent(this.form.id)}` : '/inventory';

            router[method](url, this.form, {
                preserveScroll: true,
                onSuccess: () => {
                    this.selectedId = this.form.id;
                    this.drawerOpen = false;
                    this.formErrors = {};
                    this.formNotice = {};
                },
                onError: (errors) => {
                    this.formErrors = {
                        packageName: errors.packageName,
                        destination: errors.destination,
                        basePrice: errors.basePrice,
                        availableSlots: errors.availableSlots,
                        travelType: errors.travelType,
                        supplierReliabilityScore: errors.supplierReliabilityScore,
                        businessValueScore: errors.businessValueScore,
                        riskScore: errors.riskScore,
                    };
                    this.formNotice = {
                        tone: 'error',
                        title: 'Review the package preset',
                        message: 'Fix the highlighted fields before saving this package.',
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
