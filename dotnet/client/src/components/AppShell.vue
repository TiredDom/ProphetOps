<template>
  <div class="dashboard-shell" :class="{ 'sidebar-open': open }">
    <button class="sidebar-overlay" type="button" aria-label="Close navigation" @click="open = false"></button>

    <aside id="app-sidebar" class="sidebar" :class="{ open }">
      <div class="brand">
        <p class="brand-name">ProphetOps</p>
        <p class="brand-subtitle">Renan-Tina Travels &amp; Tours</p>
      </div>

      <nav class="sidebar-nav" aria-label="Main navigation">
        <div v-for="group in groups" :key="group.label" class="nav-group">
          <p class="nav-group-label">{{ group.label }}</p>
          <RouterLink
            v-for="item in group.items"
            :key="item.label"
            :to="item.path"
            class="nav-item"
            active-class="active"
            @click="open = false"
          >
            <svg
              class="nav-item-icon"
              viewBox="0 0 24 24"
              fill="none"
              stroke="currentColor"
              stroke-width="1.6"
              stroke-linecap="round"
              stroke-linejoin="round"
              aria-hidden="true"
            >
              <template v-if="item.label === 'Dashboard'">
                <rect x="3.5" y="3.5" width="7" height="7" rx="1.4" />
                <rect x="13.5" y="3.5" width="7" height="7" rx="1.4" />
                <rect x="3.5" y="13.5" width="7" height="7" rx="1.4" />
                <rect x="13.5" y="13.5" width="7" height="7" rx="1.4" />
              </template>
              <template v-else-if="item.label === 'Forecast'">
                <polyline points="3 16.5 9 10.5 13 14.5 21 6.5" />
                <polyline points="15.5 6.5 21 6.5 21 12" />
              </template>
              <template v-else-if="item.label === 'Analytics'">
                <line x1="4" y1="19" x2="20" y2="19" />
                <line x1="7.5" y1="19" x2="7.5" y2="12" />
                <line x1="12" y1="19" x2="12" y2="8" />
                <line x1="16.5" y1="19" x2="16.5" y2="14" />
              </template>
              <template v-else-if="item.label === 'Bookings'">
                <rect x="4" y="5" width="16" height="15" rx="2" />
                <line x1="4" y1="9.5" x2="20" y2="9.5" />
                <line x1="8.5" y1="3" x2="8.5" y2="6.5" />
                <line x1="15.5" y1="3" x2="15.5" y2="6.5" />
              </template>
              <template v-else-if="item.label === 'Package Catalog'">
                <path d="M12 3 3.5 7.5v9L12 21l8.5-4.5v-9L12 3Z" />
                <polyline points="3.5 7.5 12 12 20.5 7.5" />
                <line x1="12" y1="12" x2="12" y2="21" />
              </template>
              <template v-else-if="item.label === 'Expenses'">
                <path d="M6.5 3.5h11v17l-2.2-1.5-2.2 1.5-2.2-1.5-2.2 1.5-2.2-1.5Z" />
                <line x1="9.5" y1="8.5" x2="14.5" y2="8.5" />
                <line x1="9.5" y1="12" x2="14.5" y2="12" />
              </template>
              <template v-else-if="item.label === 'Reports'">
                <path d="M6.5 3.5H13L17.5 8V20.5H6.5Z" />
                <polyline points="13 3.5 13 8 17.5 8" />
                <line x1="9" y1="13" x2="15" y2="13" />
                <line x1="9" y1="16.5" x2="15" y2="16.5" />
              </template>
              <template v-else-if="item.label === 'Users'">
                <circle cx="9" cy="8.5" r="3.3" />
                <path d="M3.5 20c0-3.2 2.5-5.3 5.5-5.3s5.5 2.1 5.5 5.3" />
                <path d="M15.5 5.7a3.3 3.3 0 0 1 0 5.6" />
                <path d="M16.8 15c2.1.5 3.7 2.3 3.7 5" />
              </template>
            </svg>
            <span>{{ item.label }}</span>
          </RouterLink>
        </div>
      </nav>
    </aside>

    <main class="main-panel">
      <header class="topbar">
        <div class="topbar-utilities">
          <span class="date-pill">{{ today }}</span>
          <span class="topbar-divider" aria-hidden="true"></span>
          <button class="profile-button" type="button">
            <span class="profile-avatar">{{ initials }}</span>
            <span>{{ displayRole }}</span>
          </button>
          <button class="topbar-logout" type="button" @click="signOut">Log out</button>
        </div>

        <div class="topbar-title-row">
          <div class="topbar-heading">
            <button
              class="menu-button"
              type="button"
              :aria-expanded="open ? 'true' : 'false'"
              aria-controls="app-sidebar"
              aria-label="Toggle navigation"
              @click="open = !open"
            >
              <span></span><span></span><span></span>
            </button>
            <div>
              <h1>{{ title }}</h1>
              <p v-if="description" class="topbar-description">{{ description }}</p>
            </div>
          </div>

          <div v-if="$slots.actions" class="topbar-page-actions">
            <slot name="actions" />
          </div>
        </div>
      </header>

      <slot />
    </main>

    <ToastHost />
  </div>
</template>

<script setup lang="ts">
import { computed, ref } from 'vue';
import { useRouter } from 'vue-router';
import { useAuth } from '../stores/auth';
import { navFor } from '../nav';
import ToastHost from './ToastHost.vue';

defineProps<{ title: string; description?: string }>();

const router = useRouter();
const { state, logout } = useAuth();
const open = ref(false);

const groups = computed(() => navFor(state.user?.role));

const today = new Intl.DateTimeFormat('en-US', {
  month: 'short',
  day: 'numeric',
  year: 'numeric',
}).format(new Date());

const initials = computed(() =>
  (state.user?.name ?? 'PO')
    .split(' ')
    .filter(Boolean)
    .slice(0, 2)
    .map((part) => part[0])
    .join('')
    .toUpperCase());

const displayRole = computed(() =>
  state.user?.role === 'Owner / Management' ? 'Owner' : state.user?.role ?? 'User');

async function signOut() {
  await logout();
  await router.replace('/login');
}
</script>
