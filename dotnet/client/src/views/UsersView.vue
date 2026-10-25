<template>
  <AppShell title="Team &amp; access">
    <template #actions>
      <button class="primary-button" type="button" aria-keyshortcuts="N" @click="openCreate">
        <UiIcon name="plus" />
        Add account
      </button>
    </template>

    <section class="dss-page">
      <PersonDetail
        :open="detailOpen"
        :user="detailUser"
        :access="detailAccess"
        :is-self="detailUser?.email === me"
        :locked-reason="detailUser ? lockedReason(detailUser) : ''"
        :history-version="historyVersion"
        :busy="detailBusy"
        :action-error="detailActionError"
        @close="detailOpen = false"
        @edit="editFromDetail"
        @suspend="setStatus('Suspended')"
        @reactivate="setStatus('Active')"
      />

      <Drawer :open="showForm" :title="drawerTitle" :dirty="formDirty" @close="closeForm">
        <p v-if="formError" class="drawer-form-error" role="alert">{{ formError }}</p>
        <FormProblems :problems="attempted ? problems : []" @focus="focusField" />

        <p class="form-helper drawer-intro">
          <template v-if="formLocked">{{ formLocked }}</template>
          <template v-else-if="isEdit">A new access level, a suspension or a new password signs this person out straight away.</template>
          <template v-else>They sign in with this email and the temporary password you set here.</template>
        </p>

        <div class="form-grid">
          <FormField :id="fieldId('name')" label="Full name" wide :error="errors.name" v-slot="f">
            <input
              :id="f.id"
              v-model.trim="form.name"
              maxlength="120"
              placeholder="e.g. Maria Santos"
              autocomplete="off"
              required
              :aria-invalid="f.invalid ? 'true' : undefined"
              :aria-describedby="f.describedBy"
              @blur="touch('name')"
            />
          </FormField>

          <FormField
            :id="fieldId('email')"
            label="Email"
            wide
            :error="errors.email"
            :hint="isEdit ? 'The sign-in name; it cannot change' : 'Used to sign in'"
            v-slot="f"
          >
            <input
              :id="f.id"
              v-model.trim="form.email"
              type="email"
              maxlength="160"
              :readonly="isEdit"
              placeholder="name@example.com"
              autocomplete="off"
              required
              :aria-invalid="f.invalid ? 'true' : undefined"
              :aria-describedby="f.describedBy"
              @blur="touch('email')"
            />
          </FormField>

          <FormField
            :id="fieldId('role')"
            label="Access level"
            wide
            :error="errors.role"
            :hint="selectedAccess"
            v-slot="f"
          >
            <select
              :id="f.id"
              v-model="form.role"
              :disabled="!!formLocked"
              required
              :aria-invalid="f.invalid ? 'true' : undefined"
              :aria-describedby="f.describedBy"
            >
              <option v-for="role in roles" :key="role.name" :value="role.name">{{ role.name }}</option>
            </select>
          </FormField>

          <FormField
            :id="fieldId('password')"
            :label="isEdit ? 'New password' : 'Temporary password'"
            :optional="isEdit"
            wide
            :error="errors.password"
            :hint="isEdit ? 'Leave blank to keep the current password. At least 8 characters.' : 'At least 8 characters. Share it with them privately.'"
            v-slot="f"
          >
            <div class="password-field">
              <input
                :id="f.id"
                v-model="form.password"
                :type="showPassword ? 'text' : 'password'"
                autocomplete="new-password"
                :required="!isEdit"
                :aria-invalid="f.invalid ? 'true' : undefined"
                :aria-describedby="f.describedBy"
                @blur="touch('password')"
              />
              <button
                type="button"
                class="password-show"
                :aria-pressed="showPassword"
                :aria-controls="f.id"
                @click="showPassword = !showPassword"
              >
                {{ showPassword ? 'Hide' : 'Show' }}
              </button>
            </div>
          </FormField>

          <FormField
            :id="fieldId('status')"
            label="Status"
            wide
            hint="Suspended accounts cannot sign in"
            v-slot="f"
          >
            <select :id="f.id" v-model="form.status" :disabled="!!formLocked" :aria-describedby="f.describedBy">
              <option value="Active">Active</option>
              <option value="Suspended">Suspended</option>
            </select>
          </FormField>
        </div>

        <template #footer="{ requestClose }">
          <button class="secondary-button" type="button" :disabled="saving" @click="requestClose">Cancel</button>
          <button class="primary-button" type="button" :disabled="saving" @click="save">
            {{ saving ? 'Saving…' : isEdit ? 'Save changes' : 'Save account' }}
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
              <button class="person" :class="{ suspended: user.status !== 'Active' }" type="button" @click="openDetail(user)">
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
import { computed, onMounted, reactive, ref, watch } from 'vue';
import { useRoute, useRouter } from 'vue-router';
import AppShell from '../components/AppShell.vue';
import Drawer from '../components/Drawer.vue';
import Skeleton from '../components/Skeleton.vue';
import EmptyState from '../components/EmptyState.vue';
import SearchField from '../components/SearchField.vue';
import FormField from '../components/FormField.vue';
import FormProblems from '../components/FormProblems.vue';
import PersonDetail, { type PersonBusy } from '../components/PersonDetail.vue';
import UiIcon from '../components/UiIcon.vue';
import { undoAction, useToast } from '../composables/useToast';
import { useFieldErrors } from '../composables/useFieldErrors';
import { stateLabel } from '../format';
import { looksLikeEmail } from '../records';
import { rememberRecent } from '../recents';
import { useAuth } from '../stores/auth';
import { api, ApiError, type UserRow, type UserInput, type RoleOption } from '../api';

const OWNER = 'Owner / Management';

const toast = useToast();
const route = useRoute();
const router = useRouter();

const users = ref<UserRow[]>([]);
const roles = ref<RoleOption[]>([]);
const loading = ref(true);
const query = ref('');
const showForm = ref(false);
const saving = ref(false);
const formError = ref('');
const isEdit = ref(false);
const originalEmail = ref('');
const showPassword = ref(false);

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

// Dates read the same way on every page: Oct 13, 2026.
const signInFormat = new Intl.DateTimeFormat('en-US', {
  day: 'numeric',
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

/**
 * The rules the server enforces, said before anyone tries: nobody changes their own access or
 * suspends themselves, and the last active owner stays an active owner.
 */
function lockedReason(user: UserRow): string {
  if (user.email === me.value) return 'This is your own account, so its access level and status stay as they are. Another owner can change them.';
  const owners = users.value.filter((u) => u.role === OWNER && u.status === 'Active');
  if (user.role === OWNER && user.status === 'Active' && owners.length === 1) {
    return 'This is the only active owner account, so it stays an active owner. Add or reactivate another owner first.';
  }
  return '';
}

function toInput(user: UserRow): UserInput {
  return { name: user.name, email: user.email, role: user.role, password: '', status: user.status };
}

// ----- The person panel -----

const detailOpen = ref(false);
const detailEmail = ref<string | null>(null);
const detailUser = computed(() => users.value.find((u) => u.email === detailEmail.value) ?? null);
const detailAccess = computed(() => roles.value.find((r) => r.name === detailUser.value?.role)?.access ?? '');
const detailBusy = ref<PersonBusy>('');
const detailActionError = ref('');
const returnToDetail = ref(false);

function openDetail(user: UserRow) {
  detailEmail.value = user.email;
  detailActionError.value = '';
  detailOpen.value = true;
  rememberRecent({ kind: 'account', code: user.email, label: user.name, detail: `${user.email} · ${user.role}` });
}

function editFromDetail() {
  const user = detailUser.value;
  if (!user) return;
  detailOpen.value = false;
  openEdit(user);
  returnToDetail.value = true;
}

/** Puts an account back as a change found it, unless someone has changed it again since. */
async function undoTo(before: UserRow, after: UserRow, done: string) {
  if (!(await load())) {
    toast.error('Could not undo the change. Check the connection, then change the account by hand.');
    return;
  }
  const now = users.value.find((u) => u.email === after.email);
  if (!now || now.name !== after.name || now.role !== after.role || now.status !== after.status) {
    toast.error(`${before.name}'s account has changed since, so this could not be undone. Open it to change it by hand.`);
    return;
  }
  try {
    await api.updateUser(before.email, toInput(before));
    await load();
    historyVersion.value += 1;
    toast.success(done);
  } catch (e) {
    toast.error(e instanceof ApiError ? Object.values(e.fields)[0] ?? e.message : 'Could not undo the change.');
  }
}

async function setStatus(status: 'Active' | 'Suspended') {
  const user = detailUser.value;
  if (!user || detailBusy.value) return;
  detailBusy.value = status === 'Suspended' ? 'suspend' : 'reactivate';
  detailActionError.value = '';
  const before = { ...user };
  try {
    const after = await api.updateUser(user.email, { ...toInput(user), status });
    await load();
    historyVersion.value += 1;
    const said = status === 'Suspended' ? `${user.name}'s account suspended` : `${user.name}'s account reactivated`;
    toast.success(said, {
      action: undoAction(() => undoTo(before, after, status === 'Suspended' ? `${user.name} can sign in again` : `${user.name}'s account is suspended again`)),
    });
  } catch (e) {
    detailActionError.value = e instanceof ApiError ? Object.values(e.fields)[0] ?? e.message : 'Could not update the account. Check the connection, then try again.';
  } finally {
    detailBusy.value = '';
  }
}

// ----- The account form -----

const FIELD_LABELS = {
  name: 'Full name',
  email: 'Email',
  role: 'Access level',
  password: 'Password',
  status: 'Status',
} as const;
type UserField = keyof typeof FIELD_LABELS;

const openedSnapshot = ref('');

const {
  errors,
  attempted,
  problems,
  touch,
  recheck,
  validateAll,
  placeServerErrors,
  reset: resetFieldErrors,
  fieldId,
  focusField,
} = useFieldErrors<UserField>('account', FIELD_LABELS, {
  name: () => (form.name.trim() ? '' : "Enter the person's full name."),
  email: () => {
    const email = form.email.trim();
    if (!email) return 'Enter the email they will sign in with.';
    if (!looksLikeEmail(email)) return 'Enter a full email address, such as name@example.com.';
    if (!isEdit.value && users.value.some((u) => u.email.toLowerCase() === email.toLowerCase())) {
      return 'That email already has an account. Open it from the list instead.';
    }
    return '';
  },
  role: () => (form.role ? '' : 'Choose an access level.'),
  password: () => {
    if (!form.password) return isEdit.value ? '' : 'Set a temporary password of at least 8 characters.';
    return form.password.length >= 8 ? '' : `Use at least 8 characters; this has ${form.password.length}.`;
  },
});

watch(form, recheck);

const formLocked = computed(() => {
  if (!isEdit.value) return '';
  const user = users.value.find((u) => u.email === originalEmail.value);
  return user ? lockedReason(user) : '';
});

function snapshotForm(): string {
  return JSON.stringify({ ...form });
}

const formDirty = computed(() => showForm.value && snapshotForm() !== openedSnapshot.value);

function openCreate() {
  Object.assign(form, blank());
  // New accounts start at the least access; anything more is a choice.
  form.role = roles.value[roles.value.length - 1]?.name ?? roles.value[0]?.name ?? '';
  isEdit.value = false;
  originalEmail.value = '';
  formError.value = '';
  showPassword.value = false;
  resetFieldErrors();
  returnToDetail.value = false;
  detailOpen.value = false;
  openedSnapshot.value = snapshotForm();
  showForm.value = true;
}

function openEdit(user: UserRow) {
  Object.assign(form, toInput(user));
  isEdit.value = true;
  originalEmail.value = user.email;
  formError.value = '';
  showPassword.value = false;
  resetFieldErrors();
  openedSnapshot.value = snapshotForm();
  showForm.value = true;
}

function closeForm() {
  showForm.value = false;
  if (returnToDetail.value && detailUser.value) detailOpen.value = true;
  returnToDetail.value = false;
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
  const found = validateAll();
  if (found.length > 0) {
    void focusField(found[0].key);
    return;
  }

  saving.value = true;
  const savedName = form.name || 'This account';
  const wasEdit = isEdit.value;
  const changedPassword = !!form.password;
  const before = wasEdit ? users.value.find((u) => u.email === originalEmail.value) ?? null : null;
  try {
    const saved = wasEdit
      ? await api.updateUser(originalEmail.value, { ...form })
      : await api.createUser({ ...form });
    await load();
    historyVersion.value += 1;
    showForm.value = false;
    if (wasEdit && before) {
      // A new password cannot be taken back, so an edit that set one offers no undo.
      const snapshot = { ...before };
      toast.success(`${savedName}'s account saved`, changedPassword
        ? undefined
        : { action: undoAction(() => undoTo(snapshot, saved, `${snapshot.name}'s account is back as it was`)) });
      detailEmail.value = saved.email;
      if (returnToDetail.value) detailOpen.value = true;
    } else {
      toast.success(`Account added for ${savedName}`, {
        action: {
          label: 'Open',
          run: () => {
            detailEmail.value = saved.email;
            detailOpen.value = true;
          },
        },
      });
    }
    returnToDetail.value = false;
  } catch (e) {
    if (e instanceof ApiError) {
      const unplaced = placeServerErrors(e.fields, {});
      if (problems.value.length > 0) void focusField(problems.value[0].key);
      formError.value = unplaced[0] ?? (problems.value.length > 0 ? '' : e.message);
    } else {
      formError.value = 'Could not reach the server, so nothing was saved. Your entries are still here; check the connection and save again.';
    }
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

/** A link such as /team?open=name@example.com (from search) opens that account. */
function openFromAddress() {
  const email = typeof route.query.open === 'string' ? route.query.open : '';
  if (!email) return;
  const user = users.value.find((u) => u.email.toLowerCase() === email.toLowerCase());
  if (user) openDetail(user);
  void router.replace({ query: { ...route.query, open: undefined } });
}

onMounted(async () => {
  if (!(await load())) {
    loadError.value = true;
    return;
  }
  openFromAddress();
  // Search's "Add an account" arrives with ?new=1.
  if (route.query.new === '1') {
    openCreate();
    void router.replace({ query: { ...route.query, new: undefined } });
  }
});

watch(() => route.query.open, (value) => {
  if (value && !loading.value) openFromAddress();
});
</script>

<style scoped>
.drawer-intro {
  margin-bottom: var(--space-4);
}

.password-field {
  position: relative;
  display: flex;
}

.password-field input {
  padding-right: 68px;
}

.password-show {
  position: absolute;
  top: 50%;
  right: 4px;
  min-height: 30px;
  padding: 0 10px;
  border: 0;
  border-radius: var(--radius-sm);
  background: none;
  color: var(--color-primary);
  font: inherit;
  font-size: var(--text-sm);
  font-weight: 600;
  cursor: pointer;
  transform: translateY(-50%);
}

.password-show:hover {
  background: var(--color-primary-soft);
}

.password-show:focus-visible {
  outline: 2px solid var(--color-ring);
  outline-offset: 0;
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
  color: var(--color-on-shell);
  font-size: var(--text-xs);
  font-weight: 600;
}

.person.suspended .person-avatar {
  /* Greyed out, with initials still legible at 4.5:1 or more. */
  background: var(--color-text-muted);
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
