<template>
  <AppShell title="Users" description="Accounts and access levels.">
    <section class="dss-page">
      <section class="stat-band" aria-label="User totals">
        <div class="stat-cell">
          <span class="stat-label">Users</span>
          <strong class="stat-value">{{ users.length }}</strong>
          <span class="stat-note">Internal accounts</span>
        </div>
        <div class="stat-cell">
          <span class="stat-label">Active</span>
          <strong class="stat-value">{{ activeCount }}</strong>
          <span class="stat-note">Able to sign in</span>
        </div>
        <div class="stat-cell">
          <span class="stat-label">Roles</span>
          <strong class="stat-value">{{ roleCount }}</strong>
          <span class="stat-note">Distinct access levels</span>
        </div>
      </section>

      <div class="dss-table-frame">
        <table class="dss-table">
          <thead>
            <tr>
              <th>Name</th>
              <th>Email</th>
              <th>Role</th>
              <th>Status</th>
            </tr>
          </thead>
          <tbody>
            <tr v-if="!users.length">
              <td colspan="4">No internal users configured.</td>
            </tr>
            <tr v-for="user in users" :key="user.email">
              <td><strong>{{ user.name }}</strong></td>
              <td>{{ user.email }}</td>
              <td><span class="record-badge" :class="badge(user.role)">{{ user.role }}</span></td>
              <td><span class="record-badge" :class="badge(user.status)">{{ user.status }}</span></td>
            </tr>
          </tbody>
        </table>
      </div>
    </section>
  </AppShell>
</template>

<script setup lang="ts">
import { computed, onMounted, ref } from 'vue';
import AppShell from '../components/AppShell.vue';
import { api, type UserRow } from '../api';

const users = ref<UserRow[]>([]);

const activeCount = computed(() => users.value.filter((u) => u.status === 'Active').length);
const roleCount = computed(() => new Set(users.value.map((u) => u.role)).size);

function badge(value: string): string {
  return 'status-' + value.toLowerCase().replace(/[^a-z0-9]+/g, '-');
}

async function load() {
  users.value = await api.users();
}

onMounted(load);
</script>
