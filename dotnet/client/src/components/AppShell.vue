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
  </div>
</template>

<script setup lang="ts">
import { computed, ref } from 'vue';
import { useRouter } from 'vue-router';
import { useAuth } from '../stores/auth';
import { navFor } from '../nav';

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
