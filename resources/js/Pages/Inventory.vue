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
                eyebrow="Operations"
                title="Inventory"
                description="Track internal items, quantities, and stock changes used in daily operations."
                :current-date="currentDate"
                :sidebar-open="sidebarOpen"
                @toggle-sidebar="toggleSidebar"
            />

            <section class="inventory-page simple-validation-page">
                <section class="module-intro simple-validation-intro inventory-intro">
                    <div>
                        <div class="hero-status inventory-status">
                            <span class="status-dot"></span>
                            Internal Stock Tracker
                        </div>
                        <h2>Track available items and stock changes.</h2>
                        <p>
                            Manage supplies, documents, office items, and operational resources without turning the
                            page into a complicated stock system.
                        </p>
                    </div>

                    <div class="module-actions">
                        <button class="primary-button" type="button" @click="openItemDrawer">
                            <AppIcon name="plus" />
                            Add Inventory Item
                        </button>
                        <button class="secondary-button" type="button" :disabled="!items.length" @click="openMovementDrawer">
                            <AppIcon name="edit" />
                            Record Stock Movement
                        </button>
                    </div>
                </section>

                <section class="stat-grid simple-summary-grid inventory-summary-grid" aria-label="Inventory summary">
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

                <section class="inventory-layout simple-validation-layout">
                    <ContentPanel
                        icon="boxes"
                        eyebrow="Inventory Workspace"
                        title="Internal Items"
                        :badge="activeTabLabel"
                        panel-class="inventory-list-panel simple-queue-panel"
                    >
                        <div class="inventory-tabs" aria-label="Inventory views">
                            <button
                                v-for="tab in tabs"
                                :key="tab.value"
                                class="inventory-tab"
                                :class="{ active: activeTab === tab.value }"
                                type="button"
                                @click="activeTab = tab.value"
                            >
                                <AppIcon :name="tab.icon" />
                                {{ tab.label }}
                            </button>
                        </div>

                        <div class="inventory-toolbar simple-validation-toolbar" aria-label="Inventory filters">
                            <label class="search-control">
                                <AppIcon name="search" />
                                <input
                                    v-model.trim="filters.search"
                                    type="search"
                                    placeholder="Search item, category, notes, or reason"
                                />
                            </label>

                            <label class="filter-control">
                                <span>Category</span>
                                <select v-model="filters.category">
                                    <option value="">All categories</option>
                                    <option v-for="category in categories" :key="category" :value="category">
                                        {{ category }}
                                    </option>
                                </select>
                            </label>

                            <label class="filter-control">
                                <span>Status</span>
                                <select v-model="filters.status">
                                    <option value="">All active items</option>
                                    <option v-for="status in itemStatuses" :key="status" :value="status">
                                        {{ status }}
                                    </option>
                                </select>
                            </label>

                            <button class="secondary-button compact-button" type="button" @click="advancedFiltersOpen = !advancedFiltersOpen">
                                <AppIcon name="filter" />
                                {{ advancedFiltersOpen ? 'Hide Advanced' : 'Advanced Filters' }}
                            </button>
                        </div>

                        <div v-if="advancedFiltersOpen" class="advanced-filter-card" aria-label="Advanced inventory filters">
                            <label class="filter-control">
                                <span>Min Quantity</span>
                                <input v-model.trim="filters.quantityMin" inputmode="decimal" placeholder="Any" />
                            </label>

                            <label class="filter-control">
                                <span>Max Quantity</span>
                                <input v-model.trim="filters.quantityMax" inputmode="decimal" placeholder="Any" />
                            </label>

                            <label class="filter-control">
                                <span>Last Updated</span>
                                <input v-model="filters.updatedAfter" type="date" />
                            </label>

                            <label class="filter-control">
                                <span>Movement Type</span>
                                <select v-model="filters.movementType">
                                    <option value="">Any movement</option>
                                    <option v-for="type in movementTypes" :key="type" :value="type">
                                        {{ type }}
                                    </option>
                                </select>
                            </label>

                            <label class="filter-control">
                                <span>Encoded By</span>
                                <input v-model.trim="filters.encodedBy" placeholder="Any encoder" />
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

                        <template v-if="activeTab === 'items'">
                            <div v-if="filteredItems.length" class="validation-table-wrapper simple-table-wrapper">
                                <table class="validation-table simple-validation-table inventory-table">
                                    <thead>
                                        <tr>
                                            <th>Item Name</th>
                                            <th>Category</th>
                                            <th>Current Quantity</th>
                                            <th>Minimum Quantity</th>
                                            <th>Status</th>
                                            <th>Action</th>
                                        </tr>
                                    </thead>
                                    <tbody>
                                        <tr
                                            v-for="item in filteredItems"
                                            :key="item.id"
                                            :class="{ selected: selectedItem?.id === item.id, archived: item.status === 'Archived' }"
                                        >
                                            <td>
                                                <strong>{{ item.itemName }}</strong>
                                                <span>{{ item.id }}</span>
                                            </td>
                                            <td>{{ item.category }}</td>
                                            <td>
                                                <strong>{{ formatQuantity(item.currentQuantity) }} {{ item.unit }}</strong>
                                                <span>{{ lastMovementLabel(item.id) }}</span>
                                            </td>
                                            <td>{{ formatQuantity(item.minimumQuantity) }} {{ item.unit }}</td>
                                            <td>
                                                <span class="record-badge" :class="statusClass(displayStatus(item))">
                                                    {{ displayStatus(item) }}
                                                </span>
                                            </td>
                                            <td>
                                                <button class="secondary-button compact-button queue-review-button" type="button" @click="selectItem(item)">
                                                    <AppIcon name="search" />
                                                    View
                                                </button>
                                            </td>
                                        </tr>
                                    </tbody>
                                </table>
                            </div>

                            <div v-if="filteredItems.length" class="validation-card-list simple-card-list" aria-label="Mobile inventory items">
                                <article
                                    v-for="item in filteredItems"
                                    :key="`${item.id}-card`"
                                    class="validation-review-card simple-review-card"
                                    :class="{ selected: selectedItem?.id === item.id, archived: item.status === 'Archived' }"
                                >
                                    <button class="mobile-card-main" type="button" @click="selectItem(item)">
                                        <span class="source-icon">
                                            <AppIcon name="boxes" />
                                        </span>
                                        <span>
                                            <strong>{{ item.itemName }}</strong>
                                            <small>{{ item.category }} - {{ item.id }}</small>
                                        </span>
                                        <span class="record-badge" :class="statusClass(displayStatus(item))">
                                            {{ displayStatus(item) }}
                                        </span>
                                    </button>

                                    <div class="mobile-card-details">
                                        <div>
                                            <span>Current Quantity</span>
                                            <strong>{{ formatQuantity(item.currentQuantity) }} {{ item.unit }}</strong>
                                        </div>
                                        <div>
                                            <span>Minimum Quantity</span>
                                            <strong>{{ formatQuantity(item.minimumQuantity) }} {{ item.unit }}</strong>
                                        </div>
                                    </div>

                                    <button class="secondary-button compact-button" type="button" @click="selectItem(item)">
                                        <AppIcon name="search" />
                                        View Details
                                    </button>
                                </article>
                            </div>
                        </template>

                        <template v-else-if="activeTab === 'movements'">
                            <div v-if="filteredMovements.length" class="validation-table-wrapper simple-table-wrapper">
                                <table class="validation-table simple-validation-table movement-table">
                                    <thead>
                                        <tr>
                                            <th>Date</th>
                                            <th>Item</th>
                                            <th>Movement Type</th>
                                            <th>Quantity</th>
                                            <th>Reason</th>
                                            <th>Encoded By</th>
                                        </tr>
                                    </thead>
                                    <tbody>
                                        <tr v-for="movement in filteredMovements" :key="movement.id">
                                            <td>
                                                <strong>{{ displayDate(movement.movementDate) }}</strong>
                                                <span>{{ movement.id }}</span>
                                            </td>
                                            <td>{{ itemName(movement.itemId) }}</td>
                                            <td>
                                                <span class="record-badge" :class="movementClass(movement.movementType)">
                                                    {{ movement.movementType }}
                                                </span>
                                            </td>
                                            <td>{{ formatQuantity(movement.quantity) }}</td>
                                            <td>{{ movement.reason }}</td>
                                            <td>{{ movement.encodedBy }}</td>
                                        </tr>
                                    </tbody>
                                </table>
                            </div>

                            <div v-if="filteredMovements.length" class="validation-card-list simple-card-list" aria-label="Mobile stock movement history">
                                <article
                                    v-for="movement in filteredMovements"
                                    :key="`${movement.id}-card`"
                                    class="validation-review-card simple-review-card"
                                >
                                    <div class="mobile-card-main movement-card-main">
                                        <span class="source-icon">
                                            <AppIcon name="edit" />
                                        </span>
                                        <span>
                                            <strong>{{ itemName(movement.itemId) }}</strong>
                                            <small>{{ displayDate(movement.movementDate) }} - {{ movement.id }}</small>
                                        </span>
                                        <span class="record-badge" :class="movementClass(movement.movementType)">
                                            {{ movement.movementType }}
                                        </span>
                                    </div>

                                    <div class="mobile-card-details">
                                        <div>
                                            <span>Quantity</span>
                                            <strong>{{ formatQuantity(movement.quantity) }}</strong>
                                        </div>
                                        <div>
                                            <span>Encoded By</span>
                                            <strong>{{ movement.encodedBy }}</strong>
                                        </div>
                                    </div>

                                    <p class="movement-card-reason">{{ movement.reason }}</p>
                                </article>
                            </div>
                        </template>

                        <template v-else>
                            <div v-if="lowStockItems.length" class="low-stock-list">
                                <button
                                    v-for="item in lowStockItems"
                                    :key="`${item.id}-low`"
                                    class="low-stock-row"
                                    type="button"
                                    @click="selectItem(item)"
                                >
                                    <span class="source-icon warning-icon">
                                        <AppIcon name="alertTriangle" />
                                    </span>
                                    <span>
                                        <strong>{{ item.itemName }}</strong>
                                        <small>
                                            {{ formatQuantity(item.currentQuantity) }} {{ item.unit }} available;
                                            minimum is {{ formatQuantity(item.minimumQuantity) }} {{ item.unit }}.
                                        </small>
                                    </span>
                                    <span class="record-badge" :class="statusClass(displayStatus(item))">
                                        {{ displayStatus(item) }}
                                    </span>
                                </button>
                            </div>
                        </template>

                        <div v-if="showEmptyState" class="validation-empty inventory-empty-state">
                            <EmptyState
                                icon="boxes"
                                :title="emptyStateTitle"
                                :message="emptyStateMessage"
                                :action-label="emptyStateActionLabel"
                                @action="handleEmptyAction"
                            />
                        </div>
                    </ContentPanel>

                    <ContentPanel
                        icon="fileBarChart"
                        eyebrow="Item Details"
                        :title="selectedItem ? selectedItem.itemName : 'Inventory Guide'"
                        :badge="selectedItem ? displayStatus(selectedItem) : 'Details'"
                        panel-class="record-review-panel simple-review-panel inventory-detail-panel"
                    >
                        <div v-if="selectedItem" class="review-detail simple-review-detail">
                            <div class="review-record-header">
                                <span class="source-icon">
                                    <AppIcon name="boxes" />
                                </span>
                                <div>
                                    <strong>{{ selectedItem.itemName }}</strong>
                                    <p>{{ selectedItem.category }} - {{ formatQuantity(selectedItem.currentQuantity) }} {{ selectedItem.unit }} available</p>
                                </div>
                            </div>

                            <div class="review-answer-card">
                                <span class="answer-icon" :class="statusTone(selectedItem)">
                                    <AppIcon :name="displayStatus(selectedItem) === 'In Stock' ? 'check' : 'alertTriangle'" />
                                </span>
                                <div>
                                    <p>Stock Status</p>
                                    <strong>{{ stockHelperText(selectedItem) }}</strong>
                                </div>
                            </div>

                            <div class="record-details-list">
                                <div>
                                    <span>Current Quantity</span>
                                    <strong>{{ formatQuantity(selectedItem.currentQuantity) }} {{ selectedItem.unit }}</strong>
                                </div>
                                <div>
                                    <span>Minimum Quantity</span>
                                    <strong>{{ formatQuantity(selectedItem.minimumQuantity) }} {{ selectedItem.unit }}</strong>
                                </div>
                                <div>
                                    <span>Last Movement</span>
                                    <strong>{{ lastMovementLabel(selectedItem.id) }}</strong>
                                </div>
                                <div>
                                    <span>Encoded By</span>
                                    <strong>{{ selectedItem.encodedBy }}</strong>
                                </div>
                                <div>
                                    <span>Created</span>
                                    <strong>{{ selectedItem.createdAt }}</strong>
                                </div>
                                <div>
                                    <span>Updated</span>
                                    <strong>{{ selectedItem.updatedAt }}</strong>
                                </div>
                                <div class="details-wide">
                                    <span>Description</span>
                                    <strong>{{ selectedItem.description || 'No description recorded' }}</strong>
                                </div>
                                <div class="details-wide">
                                    <span>Notes</span>
                                    <strong>{{ selectedItem.notes || 'No notes recorded' }}</strong>
                                </div>
                            </div>

                            <div class="movement-mini-list">
                                <p class="detail-label">Recent Movements</p>
                                <div v-if="itemMovements(selectedItem.id).length" class="movement-mini-stack">
                                    <div v-for="movement in itemMovements(selectedItem.id).slice(0, 3)" :key="`${movement.id}-mini`">
                                        <span>{{ displayDate(movement.movementDate) }}</span>
                                        <strong>{{ movement.movementType }} - {{ formatQuantity(movement.quantity) }}</strong>
                                        <p>{{ movement.reason }}</p>
                                    </div>
                                </div>
                                <p v-else class="panel-note no-indent">No stock movement recorded yet.</p>
                            </div>

                            <div class="review-actions simple-review-actions">
                                <button class="primary-button" type="button" @click="editItem(selectedItem)">
                                    <AppIcon name="edit" />
                                    Edit Item
                                </button>
                                <button class="secondary-button" type="button" @click="openMovementDrawer(selectedItem)">
                                    <AppIcon name="plus" />
                                    Record Stock Movement
                                </button>
                                <button class="secondary-button danger-button" type="button" @click="archiveItem(selectedItem.id)">
                                    <AppIcon name="archive" />
                                    Archive Item
                                </button>
                            </div>
                        </div>

                        <div v-else class="review-placeholder simple-review-placeholder">
                            <span class="empty-state-icon" aria-hidden="true">
                                <AppIcon name="boxes" />
                            </span>
                            <h4>{{ items.length ? 'Select an item to view details' : 'Start tracking internal inventory' }}</h4>
                            <p>
                                {{ items.length
                                    ? 'Open an item to see quantities, recent movements, notes, and available actions.'
                                    : 'Add items or resources used in operations so stock changes and low stock alerts can be monitored.' }}
                            </p>
                            <button class="primary-button" type="button" @click="openItemDrawer">
                                <AppIcon name="plus" />
                                Add Inventory Item
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
            v-if="itemDrawerOpen || movementDrawerOpen"
            class="drawer-backdrop"
            type="button"
            aria-label="Close inventory form"
            @click="closeDrawers"
        ></button>

        <aside class="record-drawer" :class="{ open: itemDrawerOpen }" role="dialog" aria-modal="true" aria-label="Inventory item form">
            <form class="record-form simple-record-form" @submit.prevent="saveItem">
                <div class="drawer-header">
                    <div>
                        <p class="eyebrow">Inventory Item</p>
                        <h3>{{ editingItemId ? 'Edit Inventory Item' : 'Add Inventory Item' }}</h3>
                        <p>Track an internal item, resource, or supply used in operations.</p>
                    </div>
                    <button class="icon-button" type="button" aria-label="Close form" @click="closeDrawers">
                        <AppIcon name="x" />
                    </button>
                </div>

                <div class="form-section">
                    <h4>Required Information</h4>
                    <div class="form-grid">
                        <label class="form-field form-field-wide">
                            <span>Item Name</span>
                            <input v-model.trim="itemForm.itemName" placeholder="Example: Passport holders" />
                            <small v-if="itemErrors.itemName">{{ itemErrors.itemName }}</small>
                        </label>

                        <label class="form-field">
                            <span>Category</span>
                            <select v-model="itemForm.category">
                                <option value="">Select category</option>
                                <option v-for="category in categories" :key="category" :value="category">
                                    {{ category }}
                                </option>
                            </select>
                            <small v-if="itemErrors.category">{{ itemErrors.category }}</small>
                        </label>

                        <label class="form-field">
                            <span>Unit</span>
                            <input v-model.trim="itemForm.unit" placeholder="pcs, reams, sets" />
                            <small v-if="itemErrors.unit">{{ itemErrors.unit }}</small>
                        </label>

                        <label class="form-field">
                            <span>Current Quantity</span>
                            <input v-model.trim="itemForm.currentQuantity" inputmode="decimal" placeholder="0" />
                            <small v-if="itemErrors.currentQuantity">{{ itemErrors.currentQuantity }}</small>
                        </label>

                        <label class="form-field">
                            <span>Minimum Quantity</span>
                            <input v-model.trim="itemForm.minimumQuantity" inputmode="decimal" placeholder="0" />
                            <small v-if="itemErrors.minimumQuantity">{{ itemErrors.minimumQuantity }}</small>
                        </label>
                    </div>
                </div>

                <div class="form-section">
                    <h4>Optional Details</h4>
                    <div class="form-grid">
                        <label class="form-field">
                            <span>Status</span>
                            <select v-model="itemForm.status">
                                <option v-for="status in itemStatuses" :key="status" :value="status">
                                    {{ status }}
                                </option>
                            </select>
                        </label>

                        <label class="form-field">
                            <span>Encoded By</span>
                            <input v-model.trim="itemForm.encodedBy" placeholder="Admin" />
                        </label>

                        <label class="form-field form-field-wide">
                            <span>Description</span>
                            <textarea v-model.trim="itemForm.description" rows="3" placeholder="Optional item description"></textarea>
                        </label>

                        <label class="form-field form-field-wide">
                            <span>Notes</span>
                            <textarea v-model.trim="itemForm.notes" rows="3" placeholder="Optional internal notes"></textarea>
                        </label>
                    </div>
                </div>

                <div class="drawer-actions">
                    <button class="secondary-button" type="button" @click="closeDrawers">Cancel</button>
                    <button class="primary-button" type="submit">
                        <AppIcon name="save" />
                        {{ editingItemId ? 'Save Changes' : 'Save Inventory Item' }}
                    </button>
                </div>
            </form>
        </aside>

        <aside class="record-drawer" :class="{ open: movementDrawerOpen }" role="dialog" aria-modal="true" aria-label="Stock movement form">
            <form class="record-form simple-record-form" @submit.prevent="saveMovement">
                <div class="drawer-header">
                    <div>
                        <p class="eyebrow">Stock Movement</p>
                        <h3>Record Stock Movement</h3>
                        <p>Update stock manually when items are added, used, or corrected.</p>
                    </div>
                    <button class="icon-button" type="button" aria-label="Close form" @click="closeDrawers">
                        <AppIcon name="x" />
                    </button>
                </div>

                <div class="form-section">
                    <h4>Movement Details</h4>
                    <div class="form-grid">
                        <label class="form-field form-field-wide">
                            <span>Inventory Item</span>
                            <select v-model="movementForm.itemId">
                                <option value="">Select item</option>
                                <option v-for="item in activeItems" :key="item.id" :value="item.id">
                                    {{ item.itemName }} - {{ formatQuantity(item.currentQuantity) }} {{ item.unit }}
                                </option>
                            </select>
                            <small v-if="movementErrors.itemId">{{ movementErrors.itemId }}</small>
                        </label>

                        <label class="form-field">
                            <span>Movement Type</span>
                            <select v-model="movementForm.movementType">
                                <option value="">Select type</option>
                                <option v-for="type in movementTypes" :key="type" :value="type">
                                    {{ type }}
                                </option>
                            </select>
                            <small v-if="movementErrors.movementType">{{ movementErrors.movementType }}</small>
                        </label>

                        <label class="form-field">
                            <span>Quantity</span>
                            <input v-model.trim="movementForm.quantity" inputmode="decimal" placeholder="0" />
                            <small v-if="movementErrors.quantity">{{ movementErrors.quantity }}</small>
                        </label>

                        <label class="form-field">
                            <span>Movement Date</span>
                            <input v-model="movementForm.movementDate" type="date" />
                            <small v-if="movementErrors.movementDate">{{ movementErrors.movementDate }}</small>
                        </label>

                        <label class="form-field">
                            <span>Encoded By</span>
                            <input v-model.trim="movementForm.encodedBy" placeholder="Admin" />
                        </label>

                        <label class="form-field form-field-wide">
                            <span>Reason</span>
                            <input v-model.trim="movementForm.reason" placeholder="Example: Used for tour documents" />
                            <small v-if="movementErrors.reason">{{ movementErrors.reason }}</small>
                        </label>

                        <label class="form-field form-field-wide">
                            <span>Related Operational Record</span>
                            <input v-model.trim="movementForm.relatedRecord" placeholder="Optional record reference" />
                        </label>

                        <label class="form-field form-field-wide">
                            <span>Notes</span>
                            <textarea v-model.trim="movementForm.notes" rows="3" placeholder="Optional internal notes"></textarea>
                        </label>
                    </div>
                </div>

                <div class="drawer-actions">
                    <button class="secondary-button" type="button" @click="closeDrawers">Cancel</button>
                    <button class="primary-button" type="submit">
                        <AppIcon name="save" />
                        Save Stock Movement
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

const categories = ['Supplies', 'Documents', 'Package-related resources', 'Office items', 'Operational resources', 'Other'];
const itemStatuses = ['In Stock', 'Low Stock', 'Out of Stock', 'Archived'];
const movementTypes = ['Stock In', 'Stock Out', 'Adjustment'];

function blankItemForm() {
    return {
        itemName: '',
        category: '',
        currentQuantity: '',
        minimumQuantity: '',
        unit: '',
        description: '',
        notes: '',
        status: 'In Stock',
        encodedBy: 'Admin',
    };
}

function blankMovementForm(itemId = '') {
    return {
        itemId,
        movementType: '',
        quantity: '',
        movementDate: new Date().toISOString().slice(0, 10),
        reason: '',
        relatedRecord: '',
        notes: '',
        encodedBy: 'Admin',
    };
}

export default {
    name: 'Inventory',
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
            activeTab: 'items',
            advancedFiltersOpen: false,
            itemDrawerOpen: false,
            movementDrawerOpen: false,
            editingItemId: null,
            selectedItemId: null,
            itemErrors: {},
            movementErrors: {},
            toastMessage: '',
            toastTone: 'success',
            toastTimer: null,
            currentDate: new Intl.DateTimeFormat('en-US', {
                month: 'short',
                day: 'numeric',
                year: 'numeric',
            }).format(new Date()),
            navigationGroups: createNavigationGroups('Inventory'),
            items: [],
            movements: [],
            filters: {
                search: '',
                category: '',
                status: '',
                quantityMin: '',
                quantityMax: '',
                updatedAfter: '',
                movementType: '',
                encodedBy: '',
            },
            itemForm: blankItemForm(),
            movementForm: blankMovementForm(),
            categories,
            itemStatuses,
            movementTypes,
            tabs: [
                { value: 'items', label: 'Items', icon: 'boxes' },
                { value: 'movements', label: 'Movements', icon: 'edit' },
                { value: 'low-stock', label: 'Low Stock', icon: 'alertTriangle' },
            ],
        };
    },
    computed: {
        selectedItem() {
            return this.items.find((item) => item.id === this.selectedItemId) || null;
        },
        activeItems() {
            return this.items.filter((item) => item.status !== 'Archived');
        },
        lowStockItems() {
            return this.activeItems.filter((item) => ['Low Stock', 'Out of Stock'].includes(this.displayStatus(item)));
        },
        recentlyUpdatedItem() {
            return this.items[0] || null;
        },
        summaryStats() {
            return [
                {
                    icon: 'boxes',
                    label: 'Total Items',
                    value: String(this.activeItems.length),
                    note: this.activeItems.length ? 'Active internal items' : 'No inventory items yet',
                    status: this.activeItems.length ? 'Active' : 'Empty',
                    tone: 'data',
                },
                {
                    icon: 'alertTriangle',
                    label: 'Low Stock',
                    value: String(this.lowStockItems.length),
                    note: this.lowStockItems.length ? 'Items need attention' : 'No low stock items',
                    status: this.lowStockItems.length ? 'Check' : 'Clear',
                    tone: this.lowStockItems.length ? 'warning' : 'success',
                },
                {
                    icon: 'edit',
                    label: 'Stock Movements',
                    value: String(this.movements.length),
                    note: 'Stock in, stock out, and adjustments',
                    status: 'History',
                    tone: 'primary',
                },
                {
                    icon: 'calendar',
                    label: 'Recently Updated',
                    value: this.recentlyUpdatedItem ? this.recentlyUpdatedItem.updatedAt : 'None',
                    note: this.recentlyUpdatedItem ? this.recentlyUpdatedItem.itemName : 'No updates yet',
                    status: 'Latest',
                    tone: 'data',
                },
            ];
        },
        activeTabLabel() {
            const tab = this.tabs.find((item) => item.value === this.activeTab);
            return tab?.label || 'Items';
        },
        filteredItems() {
            const searchTerm = this.filters.search.toLowerCase();
            const minQuantity = this.parseOptionalNumber(this.filters.quantityMin);
            const maxQuantity = this.parseOptionalNumber(this.filters.quantityMax);

            return this.items.filter((item) => {
                if (!this.filters.status && item.status === 'Archived') {
                    return false;
                }

                if (this.filters.category && item.category !== this.filters.category) {
                    return false;
                }

                if (this.filters.status && this.displayStatus(item) !== this.filters.status) {
                    return false;
                }

                if (minQuantity !== null && item.currentQuantity < minQuantity) {
                    return false;
                }

                if (maxQuantity !== null && item.currentQuantity > maxQuantity) {
                    return false;
                }

                if (this.filters.updatedAfter && this.dateComparable(item.updatedAt) < this.filters.updatedAfter) {
                    return false;
                }

                if (
                    this.filters.encodedBy
                    && !String(item.encodedBy || '').toLowerCase().includes(this.filters.encodedBy.toLowerCase())
                ) {
                    return false;
                }

                if (this.filters.movementType && !this.itemMovements(item.id).some((movement) => movement.movementType === this.filters.movementType)) {
                    return false;
                }

                if (!searchTerm) {
                    return true;
                }

                return [
                    item.id,
                    item.itemName,
                    item.category,
                    item.description,
                    item.notes,
                    item.encodedBy,
                    this.displayStatus(item),
                ].some((value) => String(value || '').toLowerCase().includes(searchTerm));
            });
        },
        filteredMovements() {
            const filteredItemIds = new Set(this.filteredItems.map((item) => item.id));
            const searchTerm = this.filters.search.toLowerCase();

            return this.movements.filter((movement) => {
                if (!filteredItemIds.has(movement.itemId)) {
                    return false;
                }

                if (this.filters.movementType && movement.movementType !== this.filters.movementType) {
                    return false;
                }

                if (
                    this.filters.encodedBy
                    && !String(movement.encodedBy || '').toLowerCase().includes(this.filters.encodedBy.toLowerCase())
                ) {
                    return false;
                }

                if (!searchTerm) {
                    return true;
                }

                return [
                    movement.id,
                    movement.movementType,
                    movement.reason,
                    movement.notes,
                    movement.encodedBy,
                    this.itemName(movement.itemId),
                ].some((value) => String(value || '').toLowerCase().includes(searchTerm));
            });
        },
        activeFilterText() {
            const visibleFilters = ['search', 'category', 'status'];
            const activeFilters = visibleFilters
                .filter((key) => this.filters[key])
                .map((key) => `${this.filterLabel(key)}: ${this.filters[key]}`);

            const advancedCount = Object.entries(this.filters)
                .filter(([key, value]) => !visibleFilters.includes(key) && value)
                .length;

            if (advancedCount) {
                activeFilters.push(`${advancedCount} advanced`);
            }

            return activeFilters.length ? `${this.visibleResultCount} result(s) with ${activeFilters.join(', ')}` : '';
        },
        visibleResultCount() {
            if (this.activeTab === 'movements') {
                return this.filteredMovements.length;
            }

            if (this.activeTab === 'low-stock') {
                return this.lowStockItems.length;
            }

            return this.filteredItems.length;
        },
        showEmptyState() {
            if (!this.items.length) {
                return true;
            }

            if (this.activeTab === 'movements') {
                return !this.filteredMovements.length;
            }

            if (this.activeTab === 'low-stock') {
                return !this.lowStockItems.length;
            }

            return !this.filteredItems.length;
        },
        emptyStateTitle() {
            if (!this.items.length) {
                return 'Start tracking internal inventory';
            }

            if (this.activeTab === 'movements') {
                return 'No stock movements recorded yet';
            }

            if (this.activeTab === 'low-stock') {
                return 'No low stock items right now';
            }

            return 'No items match these filters';
        },
        emptyStateMessage() {
            if (!this.items.length) {
                return 'Add items or resources used in operations so stock changes and low stock alerts can be monitored.';
            }

            if (this.activeTab === 'movements') {
                return 'Record stock movement when items are added, used, or adjusted.';
            }

            if (this.activeTab === 'low-stock') {
                return 'Items will appear here when current quantity is at or below the minimum quantity.';
            }

            return 'Try clearing filters or searching with a simpler term.';
        },
        emptyStateActionLabel() {
            if (!this.items.length || this.activeTab === 'items') {
                return this.items.length ? 'Reset Filters' : 'Add Inventory Item';
            }

            if (this.activeTab === 'movements') {
                return 'Record Stock Movement';
            }

            return 'View Items';
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
                this.closeDrawers();
            }
        },
        openItemDrawer() {
            this.editingItemId = null;
            this.itemForm = blankItemForm();
            this.itemErrors = {};
            this.itemDrawerOpen = true;
        },
        editItem(item) {
            this.editingItemId = item.id;
            this.itemForm = {
                itemName: item.itemName,
                category: item.category,
                currentQuantity: String(item.currentQuantity),
                minimumQuantity: String(item.minimumQuantity),
                unit: item.unit,
                description: item.description,
                notes: item.notes,
                status: item.status,
                encodedBy: item.encodedBy,
            };
            this.itemErrors = {};
            this.itemDrawerOpen = true;
        },
        openMovementDrawer(item = null) {
            if (!this.items.length) {
                return;
            }

            this.movementForm = blankMovementForm(item?.id || this.selectedItem?.id || '');
            this.movementErrors = {};
            this.movementDrawerOpen = true;
        },
        closeDrawers() {
            this.itemDrawerOpen = false;
            this.movementDrawerOpen = false;
        },
        saveItem() {
            if (!this.validateItemForm()) {
                return;
            }

            const now = this.currentDate;
            const existingItem = this.items.find((item) => item.id === this.editingItemId);
            const item = {
                id: this.editingItemId || this.nextItemId(),
                itemName: this.itemForm.itemName,
                category: this.itemForm.category,
                currentQuantity: this.parseRequiredNumber(this.itemForm.currentQuantity),
                minimumQuantity: this.parseRequiredNumber(this.itemForm.minimumQuantity),
                unit: this.itemForm.unit,
                description: this.itemForm.description,
                notes: this.itemForm.notes,
                status: this.itemForm.status,
                encodedBy: this.itemForm.encodedBy || 'Admin',
                createdAt: existingItem?.createdAt || now,
                updatedAt: now,
            };

            if (this.editingItemId) {
                this.items = this.items.map((existing) => existing.id === this.editingItemId ? item : existing);
                this.showToast(`${item.itemName} updated.`, 'success');
            } else {
                this.items = [item, ...this.items];
                this.showToast(`${item.itemName} added.`, 'success');
            }

            this.selectedItemId = item.id;
            this.closeDrawers();
        },
        validateItemForm() {
            const errors = {};

            if (!this.itemForm.itemName) {
                errors.itemName = 'Item name is required.';
            }

            if (!this.itemForm.category) {
                errors.category = 'Category is required.';
            }

            if (!this.itemForm.unit) {
                errors.unit = 'Unit is required.';
            }

            this.validateRequiredNumber(errors, 'currentQuantity', 'Current quantity is required.');
            this.validateRequiredNumber(errors, 'minimumQuantity', 'Minimum quantity is required.');

            this.itemErrors = errors;
            return Object.keys(errors).length === 0;
        },
        saveMovement() {
            if (!this.validateMovementForm()) {
                return;
            }

            const item = this.items.find((existing) => existing.id === this.movementForm.itemId);
            const quantity = this.parseRequiredNumber(this.movementForm.quantity);
            const nextQuantity = this.nextQuantityForMovement(item, this.movementForm.movementType, quantity);
            const movement = {
                id: this.nextMovementId(),
                itemId: item.id,
                movementType: this.movementForm.movementType,
                quantity,
                movementDate: this.movementForm.movementDate,
                reason: this.movementForm.reason,
                relatedRecord: this.movementForm.relatedRecord,
                notes: this.movementForm.notes,
                encodedBy: this.movementForm.encodedBy || 'Admin',
            };

            this.movements = [movement, ...this.movements];
            this.items = this.items.map((existing) =>
                existing.id === item.id
                    ? { ...existing, currentQuantity: nextQuantity, updatedAt: this.currentDate }
                    : existing,
            );
            this.selectedItemId = item.id;
            this.activeTab = 'movements';
            this.closeDrawers();
            this.showToast(`${movement.movementType} saved for ${item.itemName}.`, 'success');
        },
        validateMovementForm() {
            const errors = {};
            const item = this.items.find((existing) => existing.id === this.movementForm.itemId);
            const quantity = this.parseRequiredNumber(this.movementForm.quantity);

            if (!item) {
                errors.itemId = 'Inventory item is required.';
            }

            if (!this.movementForm.movementType) {
                errors.movementType = 'Movement type is required.';
            }

            if (!this.movementForm.movementDate) {
                errors.movementDate = 'Movement date is required.';
            }

            if (this.movementForm.quantity === '') {
                errors.quantity = 'Quantity is required.';
            } else if (quantity === null) {
                errors.quantity = 'Use a valid numeric quantity.';
            }

            if (this.movementForm.movementType === 'Adjustment' && !this.movementForm.reason) {
                errors.reason = 'Reason is required for adjustment.';
            } else if (!this.movementForm.reason) {
                errors.reason = 'Reason is required.';
            }

            if (
                item
                && this.movementForm.movementType === 'Stock Out'
                && quantity !== null
                && item.currentQuantity - quantity < 0
            ) {
                errors.quantity = 'Stock out cannot make quantity negative.';
            }

            this.movementErrors = errors;
            return Object.keys(errors).length === 0;
        },
        validateRequiredNumber(errors, field, message) {
            if (this.itemForm[field] === '') {
                errors[field] = message;
                return;
            }

            if (this.parseRequiredNumber(this.itemForm[field]) === null) {
                errors[field] = 'Use a valid numeric quantity.';
            }
        },
        parseRequiredNumber(value) {
            if (value === '' || value === null || value === undefined) {
                return null;
            }

            const parsed = Number(value);
            return Number.isFinite(parsed) && parsed >= 0 ? parsed : null;
        },
        parseOptionalNumber(value) {
            if (value === '' || value === null || value === undefined) {
                return null;
            }

            return this.parseRequiredNumber(value);
        },
        nextQuantityForMovement(item, type, quantity) {
            if (type === 'Stock In') {
                return item.currentQuantity + quantity;
            }

            if (type === 'Stock Out') {
                return item.currentQuantity - quantity;
            }

            return quantity;
        },
        resetFilters() {
            this.filters = {
                search: '',
                category: '',
                status: '',
                quantityMin: '',
                quantityMax: '',
                updatedAfter: '',
                movementType: '',
                encodedBy: '',
            };
        },
        handleEmptyAction() {
            if (!this.items.length) {
                this.openItemDrawer();
                return;
            }

            if (this.activeTab === 'movements') {
                this.openMovementDrawer();
                return;
            }

            if (this.activeTab === 'low-stock') {
                this.activeTab = 'items';
                return;
            }

            this.resetFilters();
        },
        selectItem(item) {
            this.selectedItemId = item.id;
        },
        archiveItem(itemId) {
            this.items = this.items.map((item) =>
                item.id === itemId
                    ? { ...item, status: 'Archived', updatedAt: this.currentDate }
                    : item,
            );
            this.showToast('Inventory item archived.', 'success');
        },
        showToast(message, tone = 'success') {
            this.toastMessage = message;
            this.toastTone = tone;
            window.clearTimeout(this.toastTimer);
            this.toastTimer = window.setTimeout(() => {
                this.toastMessage = '';
            }, 2600);
        },
        nextItemId() {
            return `INV-${String(this.items.length + 1).padStart(4, '0')}`;
        },
        nextMovementId() {
            return `MOV-${String(this.movements.length + 1).padStart(4, '0')}`;
        },
        displayStatus(item) {
            if (item.status === 'Archived') {
                return 'Archived';
            }

            if (item.currentQuantity <= 0) {
                return 'Out of Stock';
            }

            if (item.currentQuantity <= item.minimumQuantity) {
                return 'Low Stock';
            }

            return 'In Stock';
        },
        statusClass(status) {
            return `status-${status.toLowerCase().replace(/\s+/g, '-')}`;
        },
        movementClass(type) {
            const classes = {
                'Stock In': 'status-validated',
                'Stock Out': 'status-needs-review',
                Adjustment: 'status-draft',
            };

            return classes[type] || 'status-draft';
        },
        statusTone(item) {
            const status = this.displayStatus(item);

            if (status === 'In Stock') {
                return 'success';
            }

            if (status === 'Low Stock') {
                return 'warning';
            }

            return 'warning';
        },
        stockHelperText(item) {
            const status = this.displayStatus(item);

            if (status === 'Out of Stock') {
                return 'This item has no available quantity. Record Stock In when more becomes available.';
            }

            if (status === 'Low Stock') {
                return 'Current quantity is at or below the minimum quantity. Staff may need to prepare more soon.';
            }

            return 'Current quantity is above the minimum quantity.';
        },
        lastMovementLabel(itemId) {
            const movement = this.itemMovements(itemId)[0];

            if (!movement) {
                return 'No movement yet';
            }

            return `${movement.movementType} on ${this.displayDate(movement.movementDate)}`;
        },
        itemMovements(itemId) {
            return this.movements.filter((movement) => movement.itemId === itemId);
        },
        itemName(itemId) {
            return this.items.find((item) => item.id === itemId)?.itemName || 'Unknown item';
        },
        formatQuantity(value) {
            if (value === null || value === undefined) {
                return '0';
            }

            return Number(value).toLocaleString('en-US', {
                maximumFractionDigits: 2,
            });
        },
        displayDate(value) {
            if (!value) {
                return 'No date';
            }

            return new Intl.DateTimeFormat('en-US', {
                month: 'short',
                day: 'numeric',
                year: 'numeric',
            }).format(new Date(`${value}T00:00:00`));
        },
        dateComparable(value) {
            const parsed = new Date(value);

            if (Number.isNaN(parsed.getTime())) {
                return '';
            }

            return parsed.toISOString().slice(0, 10);
        },
        filterLabel(key) {
            const labels = {
                search: 'Search',
                category: 'Category',
                status: 'Status',
            };

            return labels[key] || key;
        },
    },
};
</script>
