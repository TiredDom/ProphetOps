<template>
  <div class="app-shell" :class="{ 'account-open': accountOpen }" @keydown="onShellKeydown">
    <a class="skip-link" href="#main-content">Skip to content</a>

    <div class="account-scrim" aria-hidden="true" @click="closeAccount"></div>

    <!-- On wide screens this is the navigation sidebar. On narrow screens the navigation moves
         to the tab bar and this element becomes the account sheet opened from the header. -->
    <aside
      id="app-sidebar"
      ref="sidebar"
      class="sidebar"
      :class="{ open: accountOpen }"
      tabindex="-1"
      :role="sheetMode ? 'dialog' : undefined"
      :aria-modal="sheetMode && accountOpen ? 'true' : undefined"
      :aria-labelledby="sheetMode ? 'account-sheet-title' : undefined"
    >
      <div class="brand">
        <p class="brand-name">ProphetOps</p>
        <p class="brand-subtitle">Renan-Tina Travels &amp; Tours</p>
      </div>

      <nav class="sidebar-nav" aria-label="Main navigation">
        <RouterLink
          v-for="item in items"
          :key="item.path"
          :to="item.path"
          class="nav-item"
          active-class="active"
        >
          <NavIcon :name="item.icon" class="nav-item-icon" />
          <span>{{ item.label }}</span>
        </RouterLink>
      </nav>

      <ForecastPulse />

      <footer class="sidebar-account">
        <div class="sheet-heading">
          <h2 id="account-sheet-title">Account</h2>
          <button class="sheet-close" type="button" @click="closeAccount">Close</button>
        </div>

        <div class="sidebar-account-identity">
          <span class="sidebar-account-avatar" aria-hidden="true">{{ initials }}</span>
          <div class="sidebar-account-details">
            <p class="sidebar-account-name" :title="state.user?.name">{{ state.user?.name }}</p>
            <p class="sidebar-account-role">{{ displayRole }}</p>
          </div>
        </div>

        <RouterLink
          v-for="link in accountLinks"
          :key="link.path"
          :to="link.path"
          class="sidebar-account-link"
          active-class="active"
          @click="closeAccount"
        >
          <NavIcon :name="link.icon" class="nav-item-icon" />
          <span>{{ link.label }}</span>
        </RouterLink>

        <button class="sidebar-logout" type="button" :disabled="signingOut" @click="signOut">
          {{ signingOut ? 'Logging out…' : 'Log out' }}
        </button>
        <p v-if="signOutError" class="sidebar-account-error" role="alert">{{ signOutError }}</p>
      </footer>
    </aside>

    <main id="main-content" class="main-panel" tabindex="-1">
      <header class="page-header">
        <h1 class="page-title">{{ title }}</h1>

        <div v-if="$slots.actions" class="page-actions">
          <slot name="actions" />
        </div>

        <button
          class="account-button"
          type="button"
          aria-label="Account"
          aria-haspopup="dialog"
          aria-controls="app-sidebar"
          :aria-expanded="accountOpen ? 'true' : 'false'"
          @click="accountOpen = !accountOpen"
        >
          <span class="account-button-avatar" aria-hidden="true">{{ initials }}</span>
        </button>
      </header>

      <slot />
    </main>

    <nav class="tab-bar" aria-label="Main navigation" :style="{ '--tab-count': items.length }">
      <RouterLink
        v-for="item in items"
        :key="item.path"
        :to="item.path"
        class="tab-bar-item"
        active-class="active"
      >
        <span class="tab-bar-icon">
          <NavIcon :name="item.icon" />
        </span>
        <span class="tab-bar-label">{{ item.label }}</span>
      </RouterLink>
    </nav>

    <ToastHost />
  </div>
</template>

<script setup lang="ts">
import { computed, ref, watch } from 'vue';
import { useRoute, useRouter } from 'vue-router';
import { useAuth } from '../stores/auth';
import { accountLinksFor, navFor } from '../nav';
import { useMediaQuery } from '../composables/useMediaQuery';
import { useModalFocus } from '../composables/useModalFocus';
import NavIcon from './NavIcon.vue';
import ForecastPulse from './forecast/ForecastPulse.vue';
import ToastHost from './ToastHost.vue';

defineProps<{ title: string }>();

const route = useRoute();
const router = useRouter();
const { state, logout } = useAuth();
const accountOpen = ref(false);
const sidebar = ref<HTMLElement | null>(null);
const signingOut = ref(false);
const signOutError = ref('');

// Matches the stylesheet breakpoint where the sidebar turns into the account sheet.
const sheetMode = useMediaQuery('(max-width: 1024px)');

useModalFocus(sidebar, () => accountOpen.value && sheetMode.value);

watch(sheetMode, (narrow) => {
  if (!narrow) accountOpen.value = false;
});

watch(() => route.fullPath, () => {
  accountOpen.value = false;
});

function closeAccount() {
  accountOpen.value = false;
}

function onShellKeydown(event: KeyboardEvent) {
  if (event.key === 'Escape' && accountOpen.value) closeAccount();
}

const items = computed(() => navFor(state.user?.role));
const accountLinks = computed(() => accountLinksFor(state.user?.role));

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
  if (signingOut.value) return;
  signingOut.value = true;
  signOutError.value = '';
  try {
    await logout();
    accountOpen.value = false;
    await router.replace('/login');
  } catch {
    signOutError.value = 'Unable to log out. Please try again.';
  } finally {
    signingOut.value = false;
  }
}
</script>
