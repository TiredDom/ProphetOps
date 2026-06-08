<template>
    <main class="login-shell">
        <section class="login-card" aria-labelledby="login-title">
            <div class="login-brand">
                <div class="brand-mark">PO</div>
                <div>
                    <p class="brand-name">ProphetOps</p>
                    <p class="brand-subtitle">Local Intranet DSS</p>
                </div>
            </div>

                <div class="login-copy">
                <span class="login-kicker">
                    <span class="status-dot"></span>
                    Internal Account Access
                </span>
                <h1 id="login-title">Sign in to ProphetOps</h1>
                <p>
                    Decision Support System for Travel Operations.
                </p>
            </div>

            <form class="login-form" novalidate @submit.prevent="submitLogin">
                <label class="account-field" for="email">
                    <span>Email</span>
                    <div class="account-input">
                        <AppIcon name="mail" />
                        <input
                            id="email"
                            v-model="form.email"
                            type="email"
                            inputmode="email"
                            autocomplete="email"
                            placeholder="admin@prophetops.local"
                            :aria-invalid="Boolean(errors.email)"
                            :aria-describedby="errors.email ? 'email-error' : undefined"
                            @blur="form.email = normalizedEmail"
                        />
                    </div>
                    <small v-if="errors.email" id="email-error">{{ errors.email }}</small>
                </label>

                <label class="account-field" for="password">
                    <span>Password</span>
                    <div class="account-input">
                        <AppIcon name="lock" />
                        <input
                            id="password"
                            v-model="form.password"
                            :type="showPassword ? 'text' : 'password'"
                            autocomplete="current-password"
                            placeholder="Enter internal password"
                            :aria-invalid="Boolean(errors.password)"
                            :aria-describedby="errors.password ? 'password-error' : undefined"
                        />
                        <button
                            class="password-toggle"
                            type="button"
                            :aria-label="showPassword ? 'Hide password' : 'Show password'"
                            @click="showPassword = !showPassword"
                        >
                            <AppIcon :name="showPassword ? 'eyeOff' : 'eye'" />
                        </button>
                    </div>
                    <small v-if="errors.password" id="password-error">{{ errors.password }}</small>
                </label>

                <div class="login-options">
                    <label class="remember-control">
                        <input v-model="form.rememberMe" type="checkbox" />
                        <span>Remember this device</span>
                    </label>
                    <span class="mock-auth-note">Authorized users only</span>
                </div>

                <div class="demo-account-list" aria-label="Demo accounts">
                    <p>Demo accounts</p>
                    <button
                        v-for="account in demoAccounts"
                        :key="account.email"
                        type="button"
                        @click="fillDemoAccount(account)"
                    >
                        <strong>{{ account.role }}</strong>
                        <span>{{ account.email }} / {{ account.password }}</span>
                    </button>
                </div>

                <div v-if="statusMessage" class="login-status" role="status">
                    <AppIcon name="shieldCheck" />
                    <span>{{ statusMessage }}</span>
                </div>

                <button class="primary-button login-submit" type="submit" :disabled="isLoading">
                    <span v-if="isLoading" class="loading-dot" aria-hidden="true"></span>
                    <AppIcon v-else name="logIn" />
                    {{ isLoading ? 'Checking access...' : 'Sign In' }}
                </button>
            </form>
        </section>
    </main>
</template>

<script>
import AppIcon from '../Components/icons/AppIcon.vue';
import {
    defaultPathForRole,
    demoUsers,
    getMockUser,
    isMockAuthenticated,
    mockLogin,
    normalizeLoginEmail,
} from '../services/mockAuth';

const emailPattern = /^[^\s@]+@[^\s@]+\.[^\s@]+$/;

export default {
    name: 'Login',
    components: {
        AppIcon,
    },
    data() {
        return {
            form: {
                email: '',
                password: '',
                rememberMe: false,
            },
            errors: {},
            isLoading: false,
            showPassword: false,
            statusMessage: '',
            demoAccounts: demoUsers,
        };
    },
    computed: {
        normalizedEmail() {
            return normalizeLoginEmail(this.form.email);
        },
    },
    mounted() {
        if (isMockAuthenticated()) {
            window.location.replace(defaultPathForRole(getMockUser()?.role));
        }
    },
    methods: {
        validateForm() {
            const errors = {};
            const email = this.normalizedEmail;

            if (!email) {
                errors.email = 'Email is required.';
            } else if (!emailPattern.test(email)) {
                errors.email = 'Use a valid email address.';
            }

            if (!this.form.password) {
                errors.password = 'Password is required.';
            }

            this.errors = errors;
            this.form.email = email;

            return Object.keys(errors).length === 0;
        },
        submitLogin() {
            if (this.isLoading || !this.validateForm()) {
                return;
            }

            this.isLoading = true;
            this.statusMessage = '';

            try {
                const user = mockLogin({
                    email: this.form.email,
                    password: this.form.password,
                    rememberMe: this.form.rememberMe,
                });

                this.statusMessage = 'Access confirmed. Opening workspace...';
                window.setTimeout(() => {
                    window.location.href = defaultPathForRole(user.role);
                }, 250);
            } catch (error) {
                this.errors = {
                    password: error.message || 'Use one of the Sprint 1 demo accounts.',
                };
                this.statusMessage = '';
                this.isLoading = false;
            }
        },
        fillDemoAccount(account) {
            this.form.email = account.email;
            this.form.password = account.password;
            this.errors = {};
        },
    },
};
</script>
