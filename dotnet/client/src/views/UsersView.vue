<template>
  <AppShell title="Team &amp; access">
    <template #actions>
      <button class="primary-button" type="button" @click="openCreate">
        <UiIcon name="plus" />
        Add account
      </button>
    </template>

    <section class="dss-page">
      <Drawer :open="showForm" :title="drawerTitle" @close="showForm = false">
        <p v-if="formError" class="drawer-form-error" role="alert">{{ formError }}</p>

        <p class="form-helper drawer-intro">
          <template v-if="isEdit">
            Update this person's details below. Suspended accounts cannot sign in.
          </template>
          <template v-else>
            They sign in with this email and temporary password. Suspended accounts cannot sign in.
          </template>
        </p>

        <div class="form-grid">
          <label class="account-field field-wide">
            <span>Full name</span>
            <input v-model.trim="form.name" maxlength="120" placeholder="e.g. Maria Santos" />
          </label>

          <label class="account-field field-wide">
            <span>Email</span>
            <input
              v-model.trim="form.email"
              type="email"
              maxlength="160"
              :readonly="isEdit"
              placeholder="name@renantina.ph"
            />
          </label>

          <div class="account-field field-wide">
            <label class="access-label">
              <span>Access level</span>
              <select v-model="form.role">
                <option v-for="role in roles" :key="role.name" :value="role.name">{{ role.name }}</option>
              </select>
            </label>
            <p v-if="selectedAccess" class="form-helper">{{ selectedAccess }}</p>
          </div>

          <label class="account-field field-wide">
            <span>{{ isEdit ? 'New password' : 'Temporary password' }}</span>
            <input
              v-model="form.password"
              type="password"
              autocomplete="new-password"
              :placeholder="isEdit ? 'Leave blank to keep current password' : 'At least 8 characters'"
            />
          </label>

          <label class="account-field field-wide">
            <span>Status</span>
            <select v-model="form.status">
              <option value="Active">Active</option>
              <option value="Suspended">Suspended</option>
            </select>
          </label>
        </div>

        <RecordHistory v-if="isEdit" class="form-history" entity-type="User" :code="originalEmail" :version="historyVersion" />

        <template #footer>
          <button class="secondary-button" type="button" :disabled="saving" @click="showForm = false">Cancel</button>
          <button class="primary-button" type="button" :disabled="saving" @click="save">
            {{ saving ? 'Saving…' : isEdit ? 'Save changes' : 'Create account' }}
          </button>
        </template>
      </Drawer>

      <div v-if="loading" class="content-panel">
        <div class="panel-header">
          <div class="panel-title-group">
            <h2>Team accounts</h2>
          </div>
        </div>
        <Skeleton :rows="5" />
      </div>

      <div v-else-if="loadError" class="content-panel load-error" role="alert">
        <p class="load-error-title">The team accounts could not be loaded</p>
        <p class="load-error-text">Check the connection, then try again. Nothing has been changed.</p>
        <button class="secondary-button" type="button" @click="retryLoad">Try again</button>
      </div>
      <EmptyState
        v-else-if="!users.length"
        title="No accounts yet"
        message="Add your first team member so they can sign in and start working."
      >
        <template #action>
          <button class="primary-button" type="button" @click="openCreate">Add account</button>
        </template>
      </EmptyState>

      <section v-else class="content-panel records-panel" aria-label="Team accounts">
        <div class="records-tools">
          <SearchField v-model="query" placeholder="Search name, email or access level" />
        </div>

        <p class="records-summary">
          <span class="summary-figure"><strong>{{ users.length }}</strong> {{ users.length === 1 ? 'account' : 'accounts' }}</span>
          <span class="summary-figure"><strong>{{ activeCount }}</strong> can sign in</span>
          <span v-if="suspendedCount" class="summary-figure tone-warning"><strong>{{ suspendedCount }}</strong> suspended</span>
          <span v-if="query.trim()" class="summary-scope">{{ tableMeta }}</span>
        </p>

        <!-- People grouped by what they can do -->
        <section v-for="group in groups" :key="group.name" class="access-group" :aria-labelledby="groupId(group.name)">
          <header class="access-group-head">
            <h2 :id="groupId(group.name)" class="access-group-title">
              {{ group.name }}
              <span class="access-group-count">{{ group.people.length }}</span>
            </h2>
            <p v-if="group.access" class="access-group-desc">{{ group.access }}</p>
          </header>

          <ul v-if="group.people.length" class="people">
            <li v-for="user in group.people" :key="user.email">
              <button class="person" :class="{ suspended: user.status !== 'Active' }" type="button" @click="openEdit(user)">
                <span class="person-avatar" aria-hidden="true">{{ initials(user.name) }}</span>
                <span class="person-main">
                  <span class="person-name">
                    {{ user.name }}
                    <span v-if="user.email === me" class="person-you">You</span>
                  </span>
                  <span class="person-email">{{ user.email }}</span>
                </span>
                <span class="person-meta">
                  <span class="record-badge" :class="badge(user.status)">{{ stateLabel(user.status) }}</span>
                  <span class="person-last">Last sign-in {{ lastSignIn(user.lastLoginAt) }}</span>
                </span>
                <UiIcon name="chevron-right" class="person-chevron" />
              </button>
            </li>
          </ul>
          <p v-else class="access-group-empty">
            {{ query.trim() ? 'No one at this level matches the search.' : 'No one has this access yet.' }}
          </p>
        </section>
      </section>
    </section>
  </AppShell>
</template>

<script setup lang="ts">
import { computed, onMounted, reactive, ref } from 'vue';
import AppShell from '../components/AppShell.vue';
import Drawer from '../components/Drawer.vue';
import Skeleton from '../components/Skeleton.vue';
import EmptyState from '../components/EmptyState.vue';
import SearchField from '../components/SearchField.vue';
import RecordHistory from '../components/RecordHistory.vue';
import UiIcon from '../components/UiIcon.vue';
import { useToast } from '../composables/useToast';
import { stateLabel } from '../format';
import { useAuth } from '../stores/auth';
import { api, ApiError, type UserRow, type UserInput, type RoleOption } from '../api';

const toast = useToast();

const users = ref<UserRow[]>([]);
const roles = ref<RoleOption[]>([]);
const loading = ref(true);
const query = ref('');
const showForm = ref(false);
const saving = ref(false);
const formError = ref('');
const isEdit = ref(false);
const originalEmail = ref('');

const form = reactive<UserInput>(blank());

const { state: auth } = useAuth();
const me = computed(() => auth.user?.email ?? '');
const historyVersion = ref(0);

const activeCount = computed(() => users.value.filter((u) => u.status === 'Active').length);
const suspendedCount = computed(() => users.value.length - activeCount.value);
const drawerTitle = computed(() => (isEdit.value ? 'Edit account' : 'New account'));
const selectedAccess = computed(() => roles.value.find((r) => r.name === form.role)?.access ?? '');

const filteredUsers = computed(() => {
  const q = query.value.trim().toLowerCase();
  if (!q) return users.value;
  return users.value.filter(
    (u) =>
      u.name.toLowerCase().includes(q) ||
      u.email.toLowerCase().includes(q) ||
      u.role.toLowerCase().includes(q),
  );
});

const tableMeta = computed(() => {
  const total = users.value.length;
  const shown = filteredUsers.value.length;
  if (query.value.trim() && shown !== total) return `${shown} of ${total} shown`;
  return total === 1 ? '1 account' : `${total} accounts`;
});

// Every access level, in the order the server lists them, with the people who have it.
const groups = computed(() =>
  roles.value.map((role) => ({
    ...role,
    people: filteredUsers.value.filter((u) => u.role === role.name),
  })),
);

function groupId(name: string): string {
  return 'access-' + name.toLowerCase().replace(/[^a-z0-9]+/g, '-');
}

function initials(name: string): string {
  return name.split(/\s+/).filter(Boolean).slice(0, 2).map((part) => part[0]).join('').toUpperCase() || '?';
}

const signInFormat = new Intl.DateTimeFormat('en-GB', {
  day: '2-digit',
  month: 'short',
  year: 'numeric',
});

function badge(value: string): string {
  return 'status-' + value.toLowerCase().replace(/[^a-z0-9]+/g, '-');
}

function blank(): UserInput {
  return {
    name: '',
    email: '',
    role: '',
    password: '',
    status: 'Active',
  };
}

function lastSignIn(value: string | null): string {
  if (!value) return 'Never';
  const date = new Date(value);
  if (Number.isNaN(date.getTime())) return 'Never';
  return signInFormat.format(date);
}

function openCreate() {
  Object.assign(form, blank());
  form.role = roles.value[0]?.name ?? '';
  isEdit.value = false;
  originalEmail.value = '';
  formError.value = '';
  showForm.value = true;
}

function openEdit(user: UserRow) {
  Object.assign(form, {
    name: user.name,
    email: user.email,
    role: user.role,
    password: '',
    status: user.status,
  });
  isEdit.value = true;
  originalEmail.value = user.email;
  formError.value = '';
  showForm.value = true;
}

async function load(): Promise<boolean> {
  try {
    const [userList, roleList] = await Promise.all([api.users(), api.userRoles()]);
    users.value = userList;
    roles.value = roleList;
    return true;
  } catch {
    return false;
  } finally {
    loading.value = false;
  }
}

async function save() {
  if (saving.value) return;
  formError.value = '';
  saving.value = true;
  const savedName = form.name || 'This account';
  const wasEdit = isEdit.value;
  try {
    if (wasEdit) {
      await api.updateUser(originalEmail.value, { ...form });
    } else {
      await api.createUser({ ...form });
    }
    await load();
    historyVersion.value += 1;
    showForm.value = false;
    toast.success(wasEdit ? `${savedName}'s account updated` : `Account created for ${savedName}`);
  } catch (e) {
    const message =
      e instanceof ApiError ? Object.values(e.fields)[0] ?? e.message : 'Could not save the account.';
    formError.value = message;
    toast.error(message);
  } finally {
    saving.value = false;
  }
}

const loadError = ref(false);

/** The first load failed: say so, rather than showing an empty list as if there were no records. */
async function retryLoad() {
  loadError.value = false;
  loading.value = true;
  if (!(await load())) loadError.value = true;
}

onMounted(async () => {
  if (!(await load())) loadError.value = true;
});
</script>

<style scoped>
.drawer-intro {
  margin-bottom: var(--space-4);
}

.access-label {
  display: grid;
  gap: 6px;
  min-width: 0;
}

.access-label > span {
  color: var(--color-text-secondary);
  font-size: var(--text-sm);
  font-weight: 500;
}

.account-field .form-helper {
  margin-top: 6px;
}

.form-history {
  margin-top: 28px;
  padding-top: 20px;
  border-top: 1px solid var(--color-border-subtle);
}

/* Each access level is a heading with what it allows, then the people who have it. */
.access-group {
  display: grid;
  gap: 4px;
  padding-top: 8px;
}

.access-group + .access-group {
  margin-top: 8px;
}

.access-group-head {
  display: grid;
  gap: 2px;
  padding-bottom: 8px;
  border-bottom: 1px solid var(--color-border-strong);
}

.access-group-title {
  display: inline-flex;
  align-items: center;
  gap: 8px;
  color: var(--color-text-primary);
  font-size: var(--text-md);
  font-weight: 600;
}

.access-group-count {
  display: inline-grid;
  place-items: center;
  min-width: 22px;
  height: 20px;
  padding: 0 7px;
  border-radius: var(--radius-pill);
  background: var(--tone-neutral-surface);
  color: var(--color-text-secondary);
  font-size: var(--text-xs);
  font-weight: 600;
  font-variant-numeric: lining-nums tabular-nums;
}

.access-group-desc {
  color: var(--color-text-muted);
  font-size: var(--text-sm);
}

.access-group-empty {
  padding: 12px 0 4px;
  color: var(--color-text-muted);
  font-size: var(--text-sm);
}

.people {
  margin: 0 -12px;
  padding: 0;
  list-style: none;
}

.people li + li {
  border-top: 1px solid var(--color-border-subtle);
}

.person {
  display: grid;
  grid-template-columns: 36px minmax(0, 1fr) auto 18px;
  align-items: center;
  gap: 14px;
  width: 100%;
  min-height: 56px;
  padding: 10px 12px;
  border: 0;
  border-radius: var(--radius-md);
  background: none;
  color: inherit;
  font: inherit;
  text-align: left;
  cursor: pointer;
  transition: background var(--transition-fast);
}

.person:hover {
  background: var(--color-surface-hover);
}

.person:focus-visible {
  outline: 2px solid var(--color-ring);
  outline-offset: -2px;
}

.person-avatar {
  display: grid;
  place-items: center;
  width: 36px;
  height: 36px;
  border-radius: 50%;
  background: var(--color-shell);
  color: #FFFFFF;
  font-size: var(--text-xs);
  font-weight: 600;
}

.person.suspended .person-avatar {
  background: var(--color-text-faint);
}

.person-main {
  display: grid;
  gap: 1px;
  min-width: 0;
}

.person-name {
  display: inline-flex;
  align-items: center;
  gap: 8px;
  color: var(--color-text-primary);
  font-weight: 600;
}

.person.suspended .person-name {
  color: var(--color-text-secondary);
}

.person-you {
  padding: 0 7px;
  border-radius: var(--radius-pill);
  background: var(--tone-primary-surface);
  color: var(--tone-primary-ink);
  font-size: var(--text-xs);
  font-weight: 600;
}

.person-email {
  overflow: hidden;
  color: var(--color-text-muted);
  font-size: var(--text-sm);
  text-overflow: ellipsis;
  white-space: nowrap;
}

.person-meta {
  display: grid;
  justify-items: end;
  gap: 2px;
  text-align: right;
}

.person-last {
  color: var(--color-text-muted);
  font-size: var(--text-xs);
  white-space: nowrap;
}

.person-chevron {
  color: var(--color-text-faint);
  font-size: 18px;
}

.person:hover .person-chevron {
  color: var(--color-primary);
}

@media (max-width: 640px) {
  .person {
    grid-template-columns: 36px minmax(0, 1fr) 18px;
  }

  .person-meta {
    display: flex;
    flex-wrap: wrap;
    align-items: center;
    gap: 2px 12px;
    grid-column: 2;
    grid-row: 2;
    text-align: left;
  }

  .person-chevron {
    grid-column: 3;
    grid-row: 1;
  }
}
</style>
