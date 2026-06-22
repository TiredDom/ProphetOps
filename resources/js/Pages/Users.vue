<template>
    <AppShell
        active-label="Users"
        eyebrow="Access"
        title="Users & Access"
        description="Role visibility and access overview for internal users."
    >
        <section class="dss-page">
            <section class="business-gist">
                <div>
                    <span class="insight-label">Access management</span>
                    <h2>Role behavior controls navigation across the workspace.</h2>
                    <p>Use this page to review which work areas are visible for each internal role.</p>
                </div>
            </section>

            <ContentPanel icon="users" eyebrow="User Table" title="Access Roles" badge="Access list">
                <DataTableFrame label="User access table">
                    <table class="dss-table">
                        <thead>
                            <tr>
                                <th>Name</th>
                                <th>Role</th>
                                <th>Email / username</th>
                                <th>Status</th>
                                <th>Access</th>
                                <th>Action</th>
                            </tr>
                        </thead>
                        <tbody>
                            <tr v-for="user in safeUsers" :key="user.email">
                                <td><strong>{{ user.name }}</strong></td>
                                <td>{{ user.role }}</td>
                                <td>{{ user.email }}</td>
                                <td><span class="record-badge status-active">{{ user.status }}</span></td>
                                <td>{{ accessSummary(user.role) }}</td>
                                <td>
                                    <button class="secondary-button compact-button" type="button" @click="selectedUser = user">
                                        <AppIcon name="search" />
                                        View
                                    </button>
                                </td>
                            </tr>
                        </tbody>
                    </table>
                </DataTableFrame>
            </ContentPanel>

            <AppModal
                v-if="selectedUser"
                eyebrow="User Access"
                :title="selectedUser.name"
                @close="selectedUser = null"
            >
                <div class="profile-modal-summary">
                    <span class="profile-avatar large">{{ userInitials(selectedUser.name) }}</span>
                    <div>
                        <strong>{{ selectedUser.email }}</strong>
                        <p>{{ selectedUser.role }} · {{ selectedUser.status }} · Last login {{ selectedUser.lastLogin }}</p>
                    </div>
                </div>

                <div class="modal-section">
                    <p class="modal-section-title">Visible pages</p>
                    <div class="modal-chip-list">
                        <span v-for="page in pagesForRole(selectedUser.role)" :key="page">{{ page }}</span>
                    </div>
                </div>

                <div class="modal-section">
                    <p class="modal-section-title">Access summary</p>
                    <p class="modal-copy">{{ selectedUser.role }} can access {{ accessSummary(selectedUser.role) }}.</p>
                </div>

                <template #footer>
                    <button class="primary-button" type="button" @click="selectedUser = null">Close</button>
                </template>
            </AppModal>
        </section>
    </AppShell>
</template>

<script>
import AppShell from '../Components/layout/AppShell.vue';
import ContentPanel from '../Components/dashboard/ContentPanel.vue';
import DataTableFrame from '../Components/records/DataTableFrame.vue';
import AppModal from '../Components/feedback/AppModal.vue';
import AppIcon from '../Components/icons/AppIcon.vue';

export default {
    name: 'Users',
    components: { AppIcon, AppModal, AppShell, ContentPanel, DataTableFrame },
    props: {
        users: {
            type: Array,
            default: () => [],
        },
        rolePermissions: {
            type: Object,
            default: () => ({}),
        },
    },
    data() {
        return { selectedUser: null };
    },
    computed: {
        safeUsers() {
            return this.users;
        },
    },
    methods: {
        accessSummary(role) {
            return this.rolePermissions[role]?.join(', ') || 'No access';
        },
        pagesForRole(role) {
            return this.rolePermissions[role] || [];
        },
        userInitials(name) {
            return name
                .split(' ')
                .filter(Boolean)
                .slice(0, 2)
                .map((part) => part[0])
                .join('')
                .toUpperCase();
        },
    },
};
</script>
