<template>
  <Drawer :open="open && user !== null" :title="user ? user.name : 'Account'" @close="emit('close')">
    <div v-if="user" class="record-detail">
      <header class="person-head">
        <span class="person-avatar-large" :class="{ suspended: user.status !== 'Active' }" aria-hidden="true">{{ initials }}</span>
        <div class="detail-head">
          <p class="detail-name">
            {{ user.name }}
            <span v-if="isSelf" class="person-you">You</span>
          </p>
          <p class="detail-sub">{{ user.email }}</p>
          <dl class="detail-states">
            <div>
              <dt>Access</dt>
              <dd class="person-role">{{ user.role }}</dd>
            </div>
            <div>
              <dt>Status</dt>
              <dd><span class="record-badge" :class="badge(user.status)">{{ stateLabel(user.status) }}</span></dd>
            </div>
          </dl>
        </div>
      </header>

      <p v-if="access" class="detail-note">{{ access }}</p>

      <p v-if="lockedReason" class="detail-note person-locked">{{ lockedReason }}</p>
      <div v-else class="detail-quick" role="group" aria-label="Quick updates">
        <button
          v-if="user.status === 'Active'"
          class="secondary-button compact-button"
          type="button"
          :disabled="busy !== '' || askSuspend"
          @click="askSuspend = true"
        >
          <UiIcon name="void" />
          Suspend account
        </button>
        <button
          v-else
          class="secondary-button compact-button"
          type="button"
          :disabled="busy !== ''"
          @click="emit('reactivate')"
        >
          <UiIcon name="restore" />
          {{ busy === 'reactivate' ? 'Saving…' : 'Reactivate account' }}
        </button>
      </div>

      <div v-if="askSuspend && user.status === 'Active'" class="void-panel" role="group" aria-label="Suspend this account">
        <p class="void-lead">
          {{ user.name }} is signed out at once and cannot sign in until the account is reactivated. Everything they recorded stays as it is.
        </p>
        <div class="void-actions">
          <button class="secondary-button" type="button" :disabled="busy === 'suspend'" @click="askSuspend = false">Keep active</button>
          <button class="danger-button" type="button" :disabled="busy === 'suspend'" @click="emit('suspend')">
            {{ busy === 'suspend' ? 'Suspending…' : 'Suspend account' }}
          </button>
        </div>
      </div>

      <p v-if="actionError" class="drawer-form-error" role="alert">{{ actionError }}</p>

      <dl class="detail-list">
        <div>
          <dt>Email</dt>
          <dd>{{ user.email }}</dd>
        </div>
        <div>
          <dt>Access level</dt>
          <dd>{{ user.role }}</dd>
        </div>
        <div>
          <dt>Last sign-in</dt>
          <dd>{{ lastSignIn }}</dd>
        </div>
      </dl>

      <RecordHistory entity-type="User" :code="user.email" :version="historyVersion" />
    </div>

    <template v-if="user" #footer>
      <span class="footer-spacer"></span>
      <button class="primary-button" type="button" :disabled="busy !== ''" aria-keyshortcuts="E" @click="emit('edit')">
        <UiIcon name="pencil" />
        Edit account
      </button>
    </template>
  </Drawer>
</template>

<script lang="ts">
export type PersonBusy = '' | 'suspend' | 'reactivate';
</script>

<script setup lang="ts">
import { computed, ref, watch } from 'vue';
import type { UserRow } from '../api';
import { stateLabel } from '../format';
import Drawer from './Drawer.vue';
import RecordHistory from './RecordHistory.vue';
import UiIcon from './UiIcon.vue';

const props = withDefaults(
  defineProps<{
    open: boolean;
    user: UserRow | null;
    access?: string;
    isSelf: boolean;
    /** Why access and status cannot change here, when they cannot. */
    lockedReason?: string;
    historyVersion?: number;
    busy?: PersonBusy;
    actionError?: string;
  }>(),
  { access: '', lockedReason: '', historyVersion: 0, busy: '', actionError: '' },
);

const emit = defineEmits<{ close: []; edit: []; suspend: []; reactivate: [] }>();

const askSuspend = ref(false);

watch(
  () => [props.open, props.user?.email, props.user?.status],
  () => {
    askSuspend.value = false;
  },
);

const initials = computed(() =>
  (props.user?.name ?? '?').split(/\s+/).filter(Boolean).slice(0, 2).map((part) => part[0]).join('').toUpperCase() || '?',
);

const signInFormat = new Intl.DateTimeFormat('en-GB', { day: 'numeric', month: 'short', year: 'numeric', hour: 'numeric', minute: '2-digit' });

const lastSignIn = computed(() => {
  const value = props.user?.lastLoginAt;
  if (!value) return 'Never signed in';
  const date = new Date(value);
  return Number.isNaN(date.getTime()) ? 'Never signed in' : signInFormat.format(date);
});

function badge(value: string): string {
  return 'status-' + value.toLowerCase().replace(/[^a-z0-9]+/g, '-');
}
</script>

<style scoped>
.person-head {
  display: flex;
  align-items: flex-start;
  gap: 16px;
}

.person-avatar-large {
  display: grid;
  place-items: center;
  flex: none;
  width: 52px;
  height: 52px;
  border-radius: 50%;
  background: var(--color-shell);
  color: var(--color-on-shell);
  font-size: var(--text-md);
  font-weight: 600;
}

.person-avatar-large.suspended {
  background: var(--color-text-faint);
}

.person-you {
  display: inline-block;
  margin-left: 6px;
  padding: 0 7px;
  border-radius: var(--radius-pill);
  background: var(--tone-primary-surface);
  color: var(--tone-primary-ink);
  font-size: var(--text-xs);
  font-weight: 600;
  vertical-align: 3px;
}

.person-role {
  margin: 0;
  color: var(--color-text-primary);
  font-size: var(--text-sm);
  font-weight: 600;
}

.person-locked {
  margin-top: -8px;
}
</style>
