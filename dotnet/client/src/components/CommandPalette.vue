<template>
  <Transition name="palette">
    <div v-if="state.paletteOpen" class="palette-scrim" @mousedown.self="closePalette">
      <div ref="panel" class="palette" role="dialog" aria-modal="true" aria-label="Search and go" tabindex="-1">
        <div class="palette-field">
          <UiIcon name="search" class="palette-field-icon" />
          <input
            ref="input"
            v-model="query"
            class="palette-input"
            type="text"
            role="combobox"
            aria-expanded="true"
            aria-controls="palette-results"
            aria-autocomplete="list"
            :aria-activedescendant="activeItem?.domId"
            aria-label="Search pages, actions and records"
            placeholder="Search pages, actions, bookings, packages…"
            autocomplete="off"
            spellcheck="false"
            @keydown="onKeydown"
          />
          <button type="button" class="palette-close" @click="closePalette">Esc</button>
        </div>

        <div id="palette-results" ref="list" class="palette-results" role="listbox" aria-label="Results">
          <div
            v-for="group in groups"
            :key="group.key"
            class="palette-group"
            role="group"
            :aria-label="group.name"
          >
            <p class="palette-group-name" aria-hidden="true">{{ group.name }}</p>
            <div
              v-for="item in group.items"
              :id="item.domId"
              :key="item.domId"
              class="palette-option"
              :class="{ active: activeItem?.domId === item.domId }"
              role="option"
              :aria-selected="activeItem?.domId === item.domId"
              @mousemove="activate(item)"
              @click="choose(item)"
            >
              <span class="palette-option-icon" aria-hidden="true">
                <NavIcon v-if="item.nav" :name="item.nav" />
                <UiIcon v-else :name="item.icon ?? 'arrow-right'" />
              </span>
              <span class="palette-option-text">
                <span class="palette-option-label">{{ item.label }}</span>
                <span v-if="item.detail" class="palette-option-detail">{{ item.detail }}</span>
              </span>
              <kbd v-if="item.keys" class="palette-option-keys">{{ item.keys }}</kbd>
            </div>
          </div>

          <p v-if="loadingRecords && searching" class="palette-note">Looking through the records…</p>
          <p v-else-if="!flat.length" class="palette-note">
            Nothing matches “{{ query.trim() }}”. Try part of a client, package, ID or page name.
          </p>
        </div>

        <footer class="palette-foot" aria-hidden="true">
          <span><kbd>↑</kbd><kbd>↓</kbd> move</span>
          <span><kbd>Enter</kbd> open</span>
          <span><kbd>Esc</kbd> close</span>
        </footer>
      </div>
    </div>
  </Transition>
</template>

<script setup lang="ts">
import { computed, nextTick, ref, watch } from 'vue';
import { useRouter } from 'vue-router';
import NavIcon from './NavIcon.vue';
import UiIcon, { type UiIconName } from './UiIcon.vue';
import { closePalette, openHelp, useHelpState } from '../composables/useHelp';
import { useModalFocus } from '../composables/useModalFocus';
import { useAuth } from '../stores/auth';
import { accountLinksFor, canAccess, navFor, type NavIconName } from '../nav';
import { api, type Booking, type ExpenseRow, type PackageRow, type UserRow } from '../api';
import { peso } from '../format';
import { formatDate } from '../periods';
import { helpTopics, topicText } from '../help';
import { recentPath, useRecentsFor, type RecentRecord } from '../recents';

interface PaletteItem {
  domId: string;
  label: string;
  detail?: string;
  nav?: NavIconName;
  icon?: UiIconName;
  keys?: string;
  /** Words it is found by, beyond the label. */
  terms?: string;
  run: () => void;
}

interface PaletteGroup {
  key: string;
  name: string;
  items: PaletteItem[];
}

const state = useHelpState();
const router = useRouter();
const { state: auth } = useAuth();

const panel = ref<HTMLElement | null>(null);
const input = ref<HTMLInputElement | null>(null);
const list = ref<HTMLElement | null>(null);
const query = ref('');
const activeIndex = ref(0);

useModalFocus(panel, () => state.paletteOpen);

const role = computed(() => auth.user?.role);
const recents = computed(() => useRecentsFor(auth.user?.email).value);

// Records are read when search opens, and reused for a minute.
const records = ref<{ bookings: Booking[]; packages: PackageRow[]; expenses: ExpenseRow[]; people: UserRow[] }>({
  bookings: [],
  packages: [],
  expenses: [],
  people: [],
});
const loadingRecords = ref(false);
let loadedAt = 0;

async function loadRecords() {
  if (Date.now() - loadedAt < 60_000 || loadingRecords.value) return;
  loadingRecords.value = true;
  const can = (path: string) => canAccess(role.value, path);
  const [bookings, packages, expenses, people] = await Promise.allSettled([
    can('/bookings') ? api.bookings() : Promise.resolve(null),
    can('/packages') ? api.packages() : Promise.resolve(null),
    can('/expenses') ? api.expenses() : Promise.resolve(null),
    can('/team') ? api.users() : Promise.resolve(null),
  ]);
  records.value = {
    bookings: bookings.status === 'fulfilled' && bookings.value ? bookings.value.bookings : [],
    packages: packages.status === 'fulfilled' && packages.value ? packages.value : [],
    expenses: expenses.status === 'fulfilled' && expenses.value ? expenses.value : [],
    people: people.status === 'fulfilled' && people.value ? people.value : [],
  };
  loadedAt = Date.now();
  loadingRecords.value = false;
}

function go(path: string) {
  void router.push(path);
}

const words = computed(() => query.value.trim().toLowerCase().split(/\s+/).filter(Boolean));
const searching = computed(() => words.value.length > 0);

/** How well an item matches: every word must appear; a word at the start of a word counts more. */
function score(text: string): number {
  const haystack = ` ${text.toLowerCase()}`;
  let total = 0;
  for (const word of words.value) {
    const at = haystack.indexOf(word);
    if (at < 0) return -1;
    total += haystack[at - 1] === ' ' ? 2 : 1;
  }
  return total;
}

function best<T>(items: T[], text: (item: T) => string, limit: number): T[] {
  return items
    .map((item) => ({ item, score: score(text(item)) }))
    .filter((hit) => hit.score >= 0)
    .sort((a, b) => b.score - a.score)
    .slice(0, limit)
    .map((hit) => hit.item);
}

const pages = computed<PaletteItem[]>(() => [
  ...navFor(role.value).map((item) => ({
    domId: `palette-page-${item.path.slice(1)}`,
    label: item.label,
    nav: item.icon,
    detail: 'Page',
    run: () => go(item.path),
  })),
  ...accountLinksFor(role.value).map((item) => ({
    domId: `palette-page-${item.path.slice(1)}`,
    label: item.label,
    nav: item.icon,
    detail: 'Page',
    terms: 'users accounts people staff',
    run: () => go(item.path),
  })),
]);

const actions = computed<PaletteItem[]>(() => {
  const can = (path: string) => canAccess(role.value, path);
  const all: (PaletteItem & { needs: string })[] = [
    { needs: '/bookings', domId: 'palette-act-booking', label: 'Add a booking', icon: 'plus', keys: 'N', terms: 'new record', run: () => go('/bookings?new=1') },
    { needs: '/expenses', domId: 'palette-act-expense', label: 'Add an expense', icon: 'plus', terms: 'new cost record', run: () => go('/expenses?new=1') },
    { needs: '/packages', domId: 'palette-act-package', label: 'Add a package', icon: 'plus', terms: 'new', run: () => go('/packages?new=1') },
    { needs: '/team', domId: 'palette-act-account', label: 'Add an account', icon: 'plus', terms: 'new user staff person', run: () => go('/team?new=1') },
    { needs: '/bookings', domId: 'palette-act-unpaid', label: 'Show unpaid bookings', icon: 'wallet', terms: 'pending payment owed', run: () => go('/bookings?payment=unpaid') },
    { needs: '/packages', domId: 'palette-act-low', label: 'Show packages low on slots', icon: 'alert', terms: 'seats full critical', run: () => go('/packages?stock=low') },
    { needs: '/expenses', domId: 'palette-act-pending', label: 'Show expenses still owed', icon: 'wallet', terms: 'pending unpaid', run: () => go('/expenses?pending=1') },
    { needs: '/home', domId: 'palette-act-forecast', label: 'See the revenue forecast', icon: 'forecast', terms: 'holt-winters prediction', run: () => go('/home') },
    { needs: '/home', domId: 'palette-act-forecast-bookings', label: 'Forecast bookings', icon: 'forecast', terms: 'demand holt-winters', run: () => go('/home?measure=bookings') },
    { needs: '/home', domId: 'palette-act-forecast-passengers', label: 'Forecast passengers', icon: 'forecast', terms: 'demand pax holt-winters', run: () => go('/home?measure=passengers') },
    { needs: '/reports', domId: 'palette-act-report', label: 'Report for this year', icon: 'table', terms: 'reports totals compare', run: () => go('/reports') },
    { needs: '/bookings', domId: 'palette-act-export', label: 'Export bookings as CSV', icon: 'download', terms: 'download spreadsheet', run: () => { window.location.assign('/api/export/bookings.csv'); } },
    { needs: '', domId: 'palette-act-shortcuts', label: 'Keyboard shortcuts', icon: 'keyboard', keys: '?', terms: 'keys help', run: () => openHelp('shortcuts') },
    { needs: '', domId: 'palette-act-help', label: 'Open help', icon: 'help', terms: 'how guide', run: () => openHelp() },
  ];
  return all.filter((item) => !item.needs || can(item.needs));
});

const recentItems = computed<PaletteItem[]>(() =>
  recents.value
    .filter((item) => canAccess(role.value, recentPath(item).split('?')[0]))
    .map((item: RecentRecord) => ({
      domId: `palette-recent-${item.kind}-${item.code}`.replace(/[^\w-]/g, '-'),
      label: item.label,
      detail: item.detail,
      icon: 'clock' as const,
      run: () => go(recentPath(item)),
    })),
);

const groups = computed<PaletteGroup[]>(() => {
  if (!searching.value) {
    return [
      { key: 'recent', name: 'Recently opened', items: recentItems.value.slice(0, 5) },
      { key: 'pages', name: 'Pages', items: pages.value },
      { key: 'actions', name: 'Actions', items: actions.value.slice(0, 6) },
    ].filter((group) => group.items.length > 0);
  }

  const r = records.value;
  const bookingItems = best(r.bookings.filter((b) => !b.voided), (b) => `${b.id} ${b.client} ${b.package} ${b.destination}`, 5).map((b) => ({
    domId: `palette-booking-${b.id}`.replace(/[^\w-]/g, '-'),
    label: b.client,
    detail: `${b.id} · ${b.package} · ${formatDate(b.ds)} · ${peso(b.grossRevenue)}`,
    nav: 'bookings' as const,
    run: () => go(`/bookings?open=${encodeURIComponent(b.id)}`),
  }));
  const packageItems = best(r.packages, (p) => `${p.id} ${p.packageName} ${p.destination}`, 5).map((p) => ({
    domId: `palette-package-${p.id}`.replace(/[^\w-]/g, '-'),
    label: p.packageName,
    detail: `${p.id} · ${p.destination} · ${p.availableSlots === 0 ? 'full' : `${p.availableSlots} slots left`}`,
    nav: 'packages' as const,
    run: () => go(`/packages?open=${encodeURIComponent(p.id)}`),
  }));
  const expenseItems = best(r.expenses.filter((e) => !e.voided), (e) => `${e.id} ${e.category} ${e.relatedPackage} ${e.notes ?? ''}`, 5).map((e) => ({
    domId: `palette-expense-${e.id}`.replace(/[^\w-]/g, '-'),
    label: `${e.category} · ${peso(e.amount)}`,
    detail: `${e.id} · ${formatDate(e.date)}${e.relatedPackage ? ` · ${e.relatedPackage}` : ''}`,
    nav: 'expenses' as const,
    run: () => go(`/expenses?open=${encodeURIComponent(e.id)}`),
  }));
  const peopleItems = best(r.people, (u) => `${u.name} ${u.email} ${u.role}`, 5).map((u) => ({
    domId: `palette-person-${u.email}`.replace(/[^\w-]/g, '-'),
    label: u.name,
    detail: `${u.email} · ${u.role}`,
    nav: 'team' as const,
    run: () => go(`/team?open=${encodeURIComponent(u.email)}`),
  }));
  const helpItems = best(
    helpTopics.filter((topic) => !topic.access || canAccess(role.value, topic.access)),
    (topic) => topicText(topic),
    3,
  ).map((topic) => ({
    domId: `palette-help-${topic.id}`,
    label: topic.title,
    detail: 'Help',
    icon: 'help' as const,
    run: () => openHelp(topic.id),
  }));

  return [
    { key: 'pages', name: 'Pages', items: best(pages.value, (item) => `${item.label} ${item.terms ?? ''}`, 6) },
    { key: 'actions', name: 'Actions', items: best(actions.value, (item) => `${item.label} ${item.terms ?? ''}`, 5) },
    { key: 'bookings', name: 'Bookings', items: bookingItems },
    { key: 'packages', name: 'Packages', items: packageItems },
    { key: 'expenses', name: 'Expenses', items: expenseItems },
    { key: 'people', name: 'Team', items: peopleItems },
    { key: 'help', name: 'Help', items: helpItems },
  ].filter((group) => group.items.length > 0);
});

const flat = computed(() => groups.value.flatMap((group) => group.items));
const activeItem = computed(() => flat.value[activeIndex.value] ?? null);

function activate(item: PaletteItem) {
  activeIndex.value = flat.value.indexOf(item);
}

function choose(item: PaletteItem) {
  closePalette();
  item.run();
}

async function reveal() {
  await nextTick();
  const id = activeItem.value?.domId;
  if (id) document.getElementById(id)?.scrollIntoView({ block: 'nearest' });
}

function onKeydown(event: KeyboardEvent) {
  const count = flat.value.length;
  switch (event.key) {
    case 'ArrowDown':
      event.preventDefault();
      if (count) activeIndex.value = (activeIndex.value + 1) % count;
      void reveal();
      break;
    case 'ArrowUp':
      event.preventDefault();
      if (count) activeIndex.value = (activeIndex.value - 1 + count) % count;
      void reveal();
      break;
    case 'Home':
      if (!query.value) {
        event.preventDefault();
        activeIndex.value = 0;
        void reveal();
      }
      break;
    case 'End':
      if (!query.value) {
        event.preventDefault();
        activeIndex.value = Math.max(0, count - 1);
        void reveal();
      }
      break;
    case 'Enter':
      event.preventDefault();
      if (activeItem.value) choose(activeItem.value);
      break;
    case 'Escape':
      event.preventDefault();
      event.stopPropagation();
      closePalette();
      break;
  }
}

watch(words, () => {
  activeIndex.value = 0;
});

watch(
  () => state.paletteOpen,
  async (open) => {
    if (!open) return;
    query.value = '';
    activeIndex.value = 0;
    void loadRecords();
    await nextTick();
    input.value?.focus();
  },
);

</script>

<style scoped>
.palette-scrim {
  position: fixed;
  inset: 0;
  z-index: var(--z-modal);
  display: grid;
  justify-items: center;
  align-items: start;
  padding: 12vh 16px 16px;
  background: rgba(14, 34, 71, 0.38);
}

.palette {
  display: flex;
  flex-direction: column;
  width: min(620px, 100%);
  max-height: min(560px, 76vh);
  overflow: hidden;
  border: 1px solid var(--color-border);
  border-radius: var(--radius-lg);
  background: var(--color-surface);
  box-shadow: 0 24px 60px rgba(14, 34, 71, 0.28);
  outline: none;
}

.palette-field {
  display: flex;
  align-items: center;
  gap: 10px;
  padding: 0 12px 0 16px;
  border-bottom: 1px solid var(--color-border);
}

.palette-field-icon {
  color: var(--color-text-muted);
  font-size: 18px;
}

.palette-input {
  flex: 1;
  min-width: 0;
  min-height: 54px;
  padding: 0;
  border: 0;
  background: none;
  color: var(--color-text-primary);
  font-size: var(--text-md);
}

.palette-input:focus {
  border: 0;
  box-shadow: none;
  outline: none;
}

.palette-close {
  min-height: 28px;
  padding: 0 8px;
  border: 1px solid var(--color-border-strong);
  border-radius: var(--radius-sm);
  background: var(--color-surface);
  color: var(--color-text-muted);
  font: inherit;
  font-size: var(--text-xs);
  font-weight: 600;
  cursor: pointer;
}

.palette-results {
  flex: 1;
  overflow-y: auto;
  padding: 6px 8px 10px;
}

.palette-group + .palette-group {
  margin-top: 6px;
}

.palette-group-name {
  padding: 8px 10px 4px;
  color: var(--color-text-muted);
  font-size: var(--text-xs);
  font-weight: 600;
}

.palette-option {
  display: flex;
  align-items: center;
  gap: 12px;
  min-height: 44px;
  padding: 6px 10px;
  border-radius: var(--radius-md);
  cursor: pointer;
}

.palette-option.active {
  background: var(--color-primary-soft);
}

.palette-option-icon {
  display: grid;
  place-items: center;
  flex: none;
  width: 28px;
  height: 28px;
  border-radius: var(--radius-md);
  background: var(--color-surface-sunken);
  color: var(--color-text-secondary);
  font-size: 16px;
}

.palette-option-icon :deep(svg) {
  width: 16px;
  height: 16px;
}

.palette-option.active .palette-option-icon {
  background: var(--color-surface);
  color: var(--color-primary);
}

.palette-option-text {
  display: grid;
  flex: 1;
  min-width: 0;
}

.palette-option-label {
  overflow: hidden;
  color: var(--color-text-primary);
  font-size: var(--text-base);
  font-weight: 600;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.palette-option-detail {
  overflow: hidden;
  color: var(--color-text-muted);
  font-size: var(--text-sm);
  text-overflow: ellipsis;
  white-space: nowrap;
  font-variant-numeric: lining-nums tabular-nums;
}

.palette-option-keys,
.palette-foot kbd {
  min-width: 22px;
  padding: 0 6px;
  border: 1px solid var(--color-border-strong);
  border-bottom-width: 2px;
  border-radius: var(--radius-sm);
  background: var(--color-surface);
  color: var(--color-text-muted);
  font-family: var(--font-family);
  font-size: 11px;
  font-weight: 600;
  line-height: 18px;
  text-align: center;
}

.palette-note {
  padding: 16px 10px;
  color: var(--color-text-muted);
  font-size: var(--text-sm);
}

.palette-foot {
  display: flex;
  gap: 16px;
  padding: 10px 16px;
  border-top: 1px solid var(--color-border-subtle);
  background: var(--color-surface-hover);
  color: var(--color-text-muted);
  font-size: var(--text-xs);
}

.palette-foot span {
  display: inline-flex;
  align-items: center;
  gap: 4px;
}

.palette-enter-active,
.palette-leave-active {
  transition: opacity 160ms var(--ease-out);
}

.palette-enter-active .palette,
.palette-leave-active .palette {
  transition: transform 160ms var(--ease-out);
}

.palette-enter-from,
.palette-leave-to {
  opacity: 0;
}

.palette-enter-from .palette,
.palette-leave-to .palette {
  transform: translateY(-8px) scale(0.99);
}

/* Phones: the whole screen, with the field at the top where the keyboard leaves room. */
@media (max-width: 640px) {
  .palette-scrim {
    padding: 0;
    background: var(--color-surface);
  }

  .palette {
    width: 100%;
    max-height: none;
    height: 100%;
    border: 0;
    border-radius: 0;
    box-shadow: none;
  }

  .palette-input {
    font-size: 16px;
  }

  .palette-foot {
    display: none;
  }
}

@media (prefers-reduced-motion: reduce) {
  .palette-enter-active,
  .palette-leave-active,
  .palette-enter-active .palette,
  .palette-leave-active .palette {
    transition: none;
  }
}
</style>
