<template>
  <main class="login-shell">
    <aside class="login-aside">
      <div class="login-aside-brand">
        <p class="brand-name">ProphetOps</p>
      </div>
      <p class="login-aside-mark" aria-hidden="true">P</p>
      <p class="login-aside-foot">Authorized access only</p>
    </aside>

    <div class="login-main">
      <section class="login-card" aria-labelledby="login-title">
        <div class="login-copy">
          <h1 id="login-title">Sign in</h1>
          <p>Enter your credentials to continue.</p>
        </div>

        <form class="login-form" novalidate @submit.prevent="submit">
          <label class="account-field" for="email">
            <span>Email</span>
            <div class="account-input">
              <input
                id="email"
                v-model="email"
                type="email"
                inputmode="email"
                autocomplete="username"
                placeholder="you@example.com"
                @blur="email = normalized"
              />
            </div>
          </label>

          <label class="account-field" for="password">
            <span>Password</span>
            <div class="account-input">
              <input
                id="password"
                v-model="password"
                :type="show ? 'text' : 'password'"
                autocomplete="current-password"
              />
              <button
                class="password-toggle"
                type="button"
                :aria-pressed="show"
                :aria-label="show ? 'Hide password' : 'Show password'"
                @click="show = !show"
              >
                {{ show ? 'Hide' : 'Show' }}
              </button>
            </div>
          </label>

          <p v-if="error" class="login-error" role="alert">{{ error }}</p>

          <button class="primary-button login-submit" type="submit" :disabled="loading">
            <span v-if="loading" class="loading-dot" aria-hidden="true"></span>
            {{ loading ? 'Signing in…' : 'Sign in' }}
          </button>
        </form>

        <div class="login-security-note">
          <span>Authorized personnel only. Contact your administrator if you need access.</span>
        </div>
      </section>
    </div>
  </main>
</template>

<script setup lang="ts">
import { computed, ref } from 'vue';
import { useRouter } from 'vue-router';
import { useAuth } from '../stores/auth';
import { ApiError } from '../api';

const router = useRouter();
const { login } = useAuth();

const email = ref('');
const password = ref('');
const show = ref(false);
const loading = ref(false);
const error = ref('');

const emailPattern = /^[^\s@]+@[^\s@]+\.[^\s@]+$/;
const normalized = computed(() => email.value.trim().toLowerCase());

async function submit() {
  if (loading.value) return;
  error.value = '';
  email.value = normalized.value;

  if (!emailPattern.test(email.value)) {
    error.value = 'Use a valid email address.';
    return;
  }
  if (!password.value) {
    error.value = 'Password is required.';
    return;
  }

  loading.value = true;
  try {
    const user = await login(email.value, password.value);
    await router.replace(user.defaultPath);
  } catch (e) {
    error.value = e instanceof ApiError ? e.message : 'Sign in failed.';
  } finally {
    loading.value = false;
  }
}
</script>

<style scoped>
.login-error {
  margin: 0;
  color: #B42318;
  font-size: 0.85rem;
}
</style>
