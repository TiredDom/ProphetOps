<template>
    <main class="login-shell">
        <section class="login-card" aria-labelledby="login-title">
            <div class="login-card-header">
                <div class="login-brand">
                    <div class="brand-mark">PO</div>
                    <div>
                        <p class="brand-name">ProphetOps</p>
                        <p class="brand-subtitle">Secure Workspace</p>
                    </div>
                </div>
                <span class="login-access-pill">
                    <span class="status-dot"></span>
                    Internal access
                </span>
            </div>

            <div class="login-copy">
                <h1 id="login-title">Sign in to your workspace</h1>
            </div>

            <form class="login-form" autocomplete="off" novalidate @submit.prevent="submitLogin">
                <label class="account-field" for="email">
                    <span>Email</span>
                    <div class="account-input">
                        <AppIcon name="mail" />
                        <input
                            id="email"
                            v-model="form.email"
                            type="email"
                            inputmode="email"
                            autocomplete="off"
                            placeholder="Email address"
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
                            autocomplete="off"
                            placeholder="Enter password"
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

            <div class="login-security-note">
                <AppIcon name="shieldCheck" />
                <span>Use your assigned internal account. Sessions are protected by the backend and cleared when you log out.</span>
            </div>
        </section>
    </main>
</template>

<script>
import { router } from '@inertiajs/vue3';
import AppIcon from '../Components/icons/AppIcon.vue';
import { normalizeLoginEmail } from '../services/authAccess';

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
            },
            errors: {},
            isLoading: false,
            showPassword: false,
            statusMessage: '',
        };
    },
    computed: {
        normalizedEmail() {
            return normalizeLoginEmail(this.form.email);
        },
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

            router.post('/login', {
                email: this.form.email,
                password: this.form.password,
            }, {
                onStart: () => {
                    this.isLoading = true;
                    this.statusMessage = '';
                },
                onSuccess: () => {
                    this.statusMessage = 'Access confirmed. Opening workspace...';
                },
                onError: (errors) => {
                    this.errors = errors;
                    this.statusMessage = '';
                },
                onFinish: () => {
                    this.isLoading = false;
                },
            });
        },
    },
};
</script>
