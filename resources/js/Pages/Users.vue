<template>
    <AppShell
        active-label="Users"
        eyebrow="Access"
        title="Users & Access"
        description="Prototype-only role visibility for Sprint 1 demo accounts."
    >
        <section class="dss-page">
            <section class="business-gist">
                <div>
                    <span class="insight-label">Prototype access management</span>
                    <h2>Demo users only. Real database-backed users are not implemented.</h2>
                    <p>Role behavior controls frontend navigation during Sprint 1 and must be replaced by real Laravel authorization later.</p>
                </div>
            </section>

            <ContentPanel icon="users" eyebrow="User Table" title="Access Roles" badge="Mock users">
                <div class="table-scroll">
                    <table class="dss-table">
                        <thead>
                            <tr>
                                <th>Name</th>
                                <th>Role</th>
                                <th>Email / username</th>
                                <th>Status</th>
                                <th>Last login</th>
                                <th>Access</th>
                            </tr>
                        </thead>
                        <tbody>
                            <tr v-for="user in safeUsers" :key="user.email">
                                <td><strong>{{ user.name }}</strong></td>
                                <td>{{ user.role }}</td>
                                <td>{{ user.email }}</td>
                                <td><span class="record-badge status-active">{{ user.status }}</span></td>
                                <td>{{ user.lastLogin }}</td>
                                <td>{{ accessSummary(user.role) }}</td>
                            </tr>
                        </tbody>
                    </table>
                </div>
            </ContentPanel>
        </section>
    </AppShell>
</template>

<script>
import AppShell from '../Components/layout/AppShell.vue';
import ContentPanel from '../Components/dashboard/ContentPanel.vue';
import { demoUsers, rolePermissions } from '../services/mockAuth';

export default {
    name: 'Users',
    components: { AppShell, ContentPanel },
    computed: {
        safeUsers() {
            return demoUsers.map(({ password: _password, ...user }) => user);
        },
    },
    methods: {
        accessSummary(role) {
            return rolePermissions[role]?.join(', ') || 'No access';
        },
    },
};
</script>
