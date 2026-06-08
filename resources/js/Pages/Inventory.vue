<template>
    <AppShell
        active-label="Inventory"
        eyebrow="Operations"
        title="Inventory"
        description="Monitor travel package availability, reserved slots, and low-capacity warnings."
    >
        <section class="dss-page">
            <section class="module-intro">
                <div>
                    <div class="hero-status">
                        <span class="status-dot"></span>
                        Package Availability
                    </div>
                    <h2>Review slots before demand is affected.</h2>
                    <p>Low availability may affect future demand. Review package capacity before confirming more promos.</p>
                </div>
                <div class="module-actions">
                    <button class="primary-button" type="button" @click="openDrawer(null, 'add')">
                        <AppIcon name="plus" />
                        Add Package
                    </button>
                    <button class="secondary-button" type="button" :disabled="!selectedItem" @click="openDrawer(selectedItem, 'adjust')">
                        <AppIcon name="edit" />
                        Adjust Stock
                    </button>
                </div>
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

            <section class="warning-band" v-if="lowItems.length">
                <AppIcon name="alertTriangle" />
                <div>
                    <strong>{{ lowItems.length }} package(s) need review</strong>
                    <p>Observed data -> low available slots. Business meaning -> demand may be blocked. Suggested action -> review capacity.</p>
                </div>
            </section>

            <ContentPanel icon="boxes" eyebrow="Inventory Records" title="Package Availability" badge="Mock data">
                <div class="dss-filterbar">
                    <label class="search-control">
                        <AppIcon name="search" />
                        <input v-model.trim="filters.search" type="search" placeholder="Search package or destination" />
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
                        <span>Low stock only</span>
                    </label>
                </div>

                <div class="table-scroll">
                    <table class="dss-table">
                        <thead>
                            <tr>
                                <th>Package</th>
                                <th>Destination</th>
                                <th>Available</th>
                                <th>Sold</th>
                                <th>Reserved</th>
                                <th>Status</th>
                                <th>Last Updated</th>
                                <th>Action</th>
                            </tr>
                        </thead>
                        <tbody>
                            <tr v-for="item in filteredInventory" :key="item.id" :class="{ selected: selectedItem?.id === item.id }">
                                <td><strong>{{ item.packageName }}</strong><span>{{ item.id }}</span></td>
                                <td>{{ item.destination }}</td>
                                <td>{{ item.availableSlots }}</td>
                                <td>{{ item.soldCount }}</td>
                                <td>{{ item.reservedCount }}</td>
                                <td><span class="record-badge" :class="statusClass(item.status)">{{ item.status }}</span></td>
                                <td>{{ item.lastUpdated }}</td>
                                <td>
                                    <button class="secondary-button compact-button" type="button" @click="openDrawer(item, 'view')">
                                        <AppIcon name="search" />
                                        View
                                    </button>
                                </td>
                            </tr>
                        </tbody>
                    </table>
                </div>

                <EmptyState
                    v-if="!filteredInventory.length"
                    icon="boxes"
                    title="No packages match these filters"
                    message="Clear the filters or add a package to continue tracking availability."
                    action-label="Add Package"
                    @action="openDrawer(null, 'add')"
                />
            </ContentPanel>

            <div v-if="drawerOpen" class="drawer-backdrop" @click.self="closeDrawer">
                <aside class="record-drawer sprint-drawer">
                    <div class="drawer-header">
                        <div>
                            <p class="eyebrow">{{ drawerMode === 'adjust' ? 'Adjust Stock' : 'Package Details' }}</p>
                            <h3>{{ form.packageName || 'New Package' }}</h3>
                        </div>
                        <button class="icon-button" type="button" aria-label="Close" @click="closeDrawer">
                            <AppIcon name="x" />
                        </button>
                    </div>

                    <div class="form-grid">
                        <label class="account-field">
                            <span>Package name</span>
                            <input v-model="form.packageName" />
                        </label>
                        <label class="account-field">
                            <span>Destination</span>
                            <input v-model="form.destination" />
                        </label>
                        <label class="account-field">
                            <span>Available slots</span>
                            <input v-model.number="form.availableSlots" type="number" min="0" />
                        </label>
                        <label class="account-field">
                            <span>Sold count</span>
                            <input v-model.number="form.soldCount" type="number" min="0" />
                        </label>
                        <label class="account-field">
                            <span>Reserved count</span>
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
                    </div>

                    <div class="drawer-actions">
                        <button class="secondary-button" type="button" @click="closeDrawer">Cancel</button>
                        <button class="primary-button" type="button" @click="savePackage">
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
import StatCard from '../Components/dashboard/StatCard.vue';
import AppIcon from '../Components/icons/AppIcon.vue';
import { cloneRecords, formatNumber, inventory } from '../data/mockData';

export default {
    name: 'Inventory',
    components: {
        AppIcon,
        AppShell,
        ContentPanel,
        EmptyState,
        StatCard,
    },
    data() {
        return {
            inventoryItems: cloneRecords(inventory),
            filters: {
                search: '',
                status: '',
                lowOnly: false,
            },
            selectedId: inventory[0]?.id || null,
            drawerOpen: false,
            drawerMode: 'view',
            form: {},
        };
    },
    computed: {
        selectedItem() {
            return this.inventoryItems.find((item) => item.id === this.selectedId) || null;
        },
        lowItems() {
            return this.inventoryItems.filter((item) => ['Low', 'Critical'].includes(item.status));
        },
        stats() {
            return [
                { icon: 'boxes', label: 'Total Packages', value: formatNumber(this.inventoryItems.length), note: 'Mock package inventory records', status: 'Active', tone: 'primary' },
                { icon: 'alertTriangle', label: 'Low/Critical', value: formatNumber(this.lowItems.length), note: 'Needs review before promos', status: 'Review', tone: 'warning' },
                { icon: 'users', label: 'Sold Slots', value: formatNumber(this.inventoryItems.reduce((sum, item) => sum + item.soldCount, 0)), note: 'Sold count across packages', status: 'Demand', tone: 'data' },
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
        openDrawer(item, mode) {
            this.drawerMode = mode;
            this.selectedId = item?.id || this.selectedId;
            this.form = item
                ? { ...item }
                : {
                    id: `PKG-${100 + this.inventoryItems.length + 1}`,
                    packageName: '',
                    destination: '',
                    availableSlots: 0,
                    soldCount: 0,
                    reservedCount: 0,
                    status: 'Normal',
                    lastUpdated: new Date().toISOString().slice(0, 10),
                };
            this.drawerOpen = true;
        },
        closeDrawer() {
            this.drawerOpen = false;
        },
        savePackage() {
            const existing = this.inventoryItems.some((item) => item.id === this.form.id);
            const next = { ...this.form, lastUpdated: new Date().toISOString().slice(0, 10) };

            this.inventoryItems = existing
                ? this.inventoryItems.map((item) => item.id === next.id ? next : item)
                : [next, ...this.inventoryItems];
            this.selectedId = next.id;
            this.closeDrawer();
        },
        statusClass(value) {
            return `status-${String(value).toLowerCase().replace(/[^a-z0-9]+/g, '-')}`;
        },
    },
};
</script>
