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

      <button type="button" class="sidebar-search" aria-keyshortcuts="Control+K Meta+K" @click="openPalette">
        <UiIcon name="search" class="sidebar-search-icon" />
        <span>Search</span>
        <kbd>{{ modKey }} K</kbd>
      </button>

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

      <!-- Home is the forecast itself; elsewhere the sidebar keeps this month's figure in view. -->
      <ForecastPulse v-if="route.path !== '/home'" />

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

        <button class="sidebar-account-link" type="button" aria-keyshortcuts="Shift+?" @click="showHelp">
          <UiIcon name="help" class="nav-item-icon" />
          <span>Help &amp; shortcuts</span>
        </button>

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

        <button class="header-search" type="button" aria-label="Search" @click="openPalette">
          <UiIcon name="search" />
        </button>

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
    <CommandPalette />
    <HelpPanel />
  </div>
</template>

<script setup lang="ts">
import { computed, onBeforeUnmount, onMounted, ref, watch } from 'vue';
import { useRoute, useRouter } from 'vue-router';
import { useAuth } from '../stores/auth';
import { accountLinksFor, canAccess, navFor } from '../nav';
import { useMediaQuery } from '../composables/useMediaQuery';
import { useModalFocus } from '../composables/useModalFocus';
import { closePalette, openHelp, openPalette, useHelpState } from '../composables/useHelp';
import { isTypingTarget, modKey, openOverlays, overlayOpen } from '../keyboard';
import { useRecentsFor } from '../recents';
import CommandPalette from './CommandPalette.vue';
import HelpPanel from './HelpPanel.vue';
import NavIcon from './NavIcon.vue';
import UiIcon from './UiIcon.vue';
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

// Recently opened records belong to whoever is signed in.
useRecentsFor(state.user?.email);
watch(() => state.user?.email, (email) => useRecentsFor(email));

function showHelp() {
  accountOpen.value = false;
  openHelp();
}

// ----- Keyboard shortcuts -----
// Ctrl+K (⌘K) searches from anywhere. The rest are single keys, which wait while someone is
// typing in a field or a panel is open: / searches the list, N adds, E edits the open record,
// G then a letter goes to a page, ? opens help.

const helpState = useHelpState();
const goKeys: Record<string, string> = { h: '/home', b: '/bookings', p: '/packages', e: '/expenses', r: '/reports', t: '/team' };
let goPending = false;
let goTimer: ReturnType<typeof setTimeout> | undefined;

function topDialog(): HTMLElement | null {
  const dialogs = openOverlays('[role="dialog"][aria-modal="true"]');
  return dialogs.length ? dialogs[dialogs.length - 1] : null;
}

function onShortcut(event: KeyboardEvent) {
  if (event.defaultPrevented || event.isComposing) return;
  const key = event.key;

  if ((event.ctrlKey || event.metaKey) && !event.altKey && !event.shiftKey && key.toLowerCase() === 'k') {
    event.preventDefault();
    if (helpState.paletteOpen) closePalette();
    else openPalette();
    return;
  }
  if (event.ctrlKey || event.metaKey || event.altKey || isTypingTarget(event.target)) return;

  if (key === 'e' || key === 'E') {
    const edit = topDialog()?.querySelector<HTMLButtonElement>('[aria-keyshortcuts="E"]:not(:disabled)');
    if (edit) {
      event.preventDefault();
      edit.click();
      return;
    }
  }
  if (overlayOpen()) return;

  if (goPending) {
    goPending = false;
    clearTimeout(goTimer);
    const path = goKeys[key.toLowerCase()];
    if (path && canAccess(state.user?.role, path)) {
      event.preventDefault();
      if (route.path !== path) void router.push(path);
    }
    return;
  }

  switch (key) {
    case '/': {
      event.preventDefault();
      const search = document.querySelector<HTMLInputElement>('.main-panel .search-field input');
      if (search) search.focus();
      else openPalette();
      break;
    }
    case '?':
      event.preventDefault();
      openHelp('shortcuts');
      break;
    case 'n':
    case 'N': {
      const add = document.querySelector<HTMLButtonElement>('.page-actions [aria-keyshortcuts="N"]:not(:disabled)');
      if (add) {
        event.preventDefault();
        add.click();
      }
      break;
    }
    case 'g':
    case 'G':
      goPending = true;
      clearTimeout(goTimer);
      goTimer = setTimeout(() => {
        goPending = false;
      }, 1500);
      break;
  }
}

onMounted(() => document.addEventListener('keydown', onShortcut));
onBeforeUnmount(() => {
  document.removeEventListener('keydown', onShortcut);
  clearTimeout(goTimer);
});

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
