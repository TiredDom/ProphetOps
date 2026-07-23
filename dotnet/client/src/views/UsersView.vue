<template>
  <AppShell title="Users" description="Create and manage sign-in accounts for your team.">
    <template #actions>
      <button class="primary-button" type="button" @click="openCreate">Add account</button>
    </template>

    <section class="dss-page">
      <section class="stat-band" aria-label="Account totals">
        <div class="stat-cell">
          <span class="stat-label">Accounts</span>
          <strong class="stat-value">{{ users.length }}</strong>
          <span class="stat-note">People with an account</span>
        </div>
        <div class="stat-cell">
          <span class="stat-label">Active</span>
          <strong class="stat-value">{{ activeCount }}</strong>
          <span class="stat-note">Able to sign in</span>
        </div>
        <div class="stat-cell">
          <span class="stat-label">Access levels</span>
          <strong class="stat-value">{{ roleCount }}</strong>
          <span class="stat-note">Different permission sets in use</span>
        </div>
      </section>

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

      <EmptyState
        v-else-if="!users.length"
        title="No accounts yet"
        message="Add your first team member so they can sign in and start working."
      >
        <template #action>
          <button class="primary-button" type="button" @click="openCreate">Add account</button>
        </template>
      </EmptyState>

      <div v-else class="users-stack">
        <section class="content-panel">
          <div class="panel-header accounts-header">
            <div class="panel-title-group">
              <h2>Team accounts</h2>
              <span class="panel-meta">{{ tableMeta }}</span>
            </div>
            <SearchField v-model="query" placeholder="Search by name, email, or access" />
          </div>

          <div class="table-scroll">
            <table class="dss-table">
              <thead>
                <tr>
                  <th>Name</th>
                  <th>Email</th>
                  <th>Access level</th>
                  <th>Status</th>
                  <th>Last sign-in</th>
                  <th>Actions</th>
                </tr>
              </thead>
              <tbody>
                <tr v-if="!filteredUsers.length">
                  <td colspan="6" class="table-empty-cell">No accounts match “{{ query }}”.</td>
                </tr>
                <tr v-for="user in filteredUsers" :key="user.email">
                  <td><strong>{{ user.name }}</strong></td>
                  <td>{{ user.email }}</td>
                  <td><span class="record-badge" :class="badge(user.role)">{{ user.role }}</span></td>
                  <td><span class="record-badge" :class="badge(user.status)">{{ user.status }}</span></td>
                  <td>{{ lastSignIn(user.lastLoginAt) }}</td>
                  <td>
                    <button class="table-link" type="button" @click="openEdit(user)">Edit</button>
                  </td>
                </tr>
              </tbody>
            </table>
          </div>
        </section>

        <aside class="content-panel access-panel" aria-label="Access levels">
          <div class="panel-header">
            <div class="panel-title-group">
              <h2>Access levels</h2>
            </div>
          </div>

          <p class="access-intro">What each permission set can do, and how many accounts use it.</p>

          <div class="access-list">
            <div v-for="role in roleUsage" :key="role.name" class="access-item">
              <div class="access-item-head">
                <span class="access-item-name">{{ role.name }}</span>
                <span class="access-item-count">{{ usageLabel(role.count) }}</span>
              </div>
              <p v-if="role.access" class="access-item-desc">{{ role.access }}</p>
            </div>
          </div>
        </aside>
      </div>
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
import { useToast } from '../composables/useToast';
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

const activeCount = computed(() => users.value.filter((u) => u.status === 'Active').length);
const roleCount = computed(() => new Set(users.value.map((u) => u.role)).size);
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

const roleUsage = computed(() => {
  const counts = new Map<string, number>();
  for (const u of users.value) counts.set(u.role, (counts.get(u.role) ?? 0) + 1);
  return roles.value.map((r) => ({ ...r, count: counts.get(r.name) ?? 0 }));
});

const signInFormat = new Intl.DateTimeFormat('en-GB', {
  day: '2-digit',
  month: 'short',
  year: 'numeric',
});

function usageLabel(count: number): string {
  if (count === 0) return 'Not assigned';
  return count === 1 ? '1 account' : `${count} accounts`;
}

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

async function load() {
  const [userList, roleList] = await Promise.all([api.users(), api.userRoles()]);
  users.value = userList;
  roles.value = roleList;
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

onMounted(async () => {
  try {
    await load();
  } catch {
    toast.error('Could not load the team accounts.');
  } finally {
    loading.value = false;
  }
});
</script>

<style scoped>
.drawer-form-error {
  margin-bottom: var(--space-4);
  color: var(--color-danger-ink);
  font-size: 13px;
}

.drawer-intro {
  margin-bottom: var(--space-4);
}

.access-label {
  display: grid;
  gap: 6px;
  min-width: 0;
}

.access-label > span {
  color: var(--color-text-muted);
  font-size: 11px;
  font-weight: 600;
  letter-spacing: 0.06em;
  text-transform: uppercase;
}

.account-field .form-helper {
  margin-top: 6px;
}

.accounts-header {
  align-items: center;
  flex-wrap: wrap;
  gap: var(--space-3) var(--space-4);
}

.users-stack {
  display: flex;
  flex-direction: column;
  gap: var(--space-6);
}

.access-intro {
  margin-bottom: var(--space-2);
  color: var(--color-text-muted);
  font-size: 12.5px;
  line-height: 1.5;
}

.access-list {
  display: grid;
  grid-template-columns: repeat(auto-fit, minmax(230px, 1fr));
  gap: var(--space-5);
  margin-top: var(--space-3);
}

.access-item {
  display: grid;
  gap: 4px;
  align-content: start;
}

.access-item-head {
  display: flex;
  align-items: baseline;
  justify-content: space-between;
  gap: var(--space-3);
}

.access-item-name {
  color: var(--color-text-primary);
  font-size: 13.5px;
  font-weight: 600;
}

.access-item-count {
  color: var(--color-text-muted);
  font-size: 12px;
  white-space: nowrap;
  font-variant-numeric: lining-nums tabular-nums;
}

.access-item-desc {
  color: var(--color-text-muted);
  font-size: 12.5px;
  line-height: 1.5;
}
</style>
