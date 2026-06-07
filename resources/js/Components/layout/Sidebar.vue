<template>
    <aside class="sidebar" :class="{ open: isOpen }" id="app-sidebar">
        <div class="brand">
            <div class="brand-mark">PO</div>
            <div>
                <p class="brand-name">ProphetOps</p>
                <p class="brand-subtitle">Local DSS</p>
            </div>
        </div>

        <nav class="sidebar-nav" aria-label="Main navigation">
            <div v-for="group in navigationGroups" :key="group.label" class="nav-group">
                <p class="nav-group-label">{{ group.label }}</p>
                <Link
                    v-for="item in linkedItems(group.items)"
                    :key="item.label"
                    :href="item.href"
                    class="nav-item"
                    :class="{ active: item.active, locked: item.locked }"
                    @click="$emit('item-selected')"
                >
                    <span class="nav-icon" aria-hidden="true">
                        <AppIcon :name="item.icon" />
                    </span>
                    <span>{{ item.label }}</span>
                    <span v-if="item.locked" class="nav-lock">Soon</span>
                </Link>
                <button
                    v-for="item in buttonItems(group.items)"
                    :key="item.label"
                    class="nav-item"
                    :class="{ active: item.active, locked: item.locked }"
                    type="button"
                    @click="$emit('item-selected')"
                >
                    <span class="nav-icon" aria-hidden="true">
                        <AppIcon :name="item.icon" />
                    </span>
                    <span>{{ item.label }}</span>
                    <span v-if="item.locked" class="nav-lock">Soon</span>
                </button>
            </div>
        </nav>
    </aside>
</template>

<script>
import { Link } from '@inertiajs/vue3';
import AppIcon from '../icons/AppIcon.vue';

export default {
    name: 'Sidebar',
    components: {
        AppIcon,
        Link,
    },
    props: {
        navigationGroups: {
            type: Array,
            required: true,
        },
        isOpen: {
            type: Boolean,
            default: false,
        },
    },
    emits: ['item-selected'],
    methods: {
        linkedItems(items) {
            return items.filter((item) => item.href && !item.locked);
        },
        buttonItems(items) {
            return items.filter((item) => !item.href || item.locked);
        },
    },
};
</script>
