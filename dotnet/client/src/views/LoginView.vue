<template>
  <main class="login-shell">
    <aside class="login-aside">
      <div class="login-aside-brand">
        <p class="brand-name">ProphetOps</p>
        <p class="brand-subtitle">Renan-Tina Travels &amp; Tours</p>
        <p class="login-aside-tagline">Revenue forecasting and decision support</p>
        <ul class="login-aside-points">
          <li>Six months of booking revenue forecast from your own records with Holt-Winters</li>
          <li>A likely range for every month, not a single guess</li>
          <li>Decisions to review, each shown with the evidence behind it</li>
        </ul>
      </div>
      <p class="login-aside-foot">Authorized access only</p>
    </aside>

    <div class="login-main">
      <section class="login-card" aria-labelledby="login-title">
        <div class="login-copy">
          <h1 id="login-title">Sign in</h1>
          <p>Enter your credentials to continue.</p>
        </div>

        <form class="login-form" novalidate @submit.prevent="submit">
          <div class="account-field">
            <label for="email">Email</label>
            <div class="account-input">
              <input
                id="email"
                ref="emailInput"
                v-model="email"
                type="email"
                inputmode="email"
                autocomplete="username"
                placeholder="you@example.com"
                :aria-invalid="emailError ? 'true' : undefined"
                :aria-describedby="emailError ? 'email-error' : undefined"
                @blur="email = normalized"
                @input="emailError = ''"
              />
            </div>
            <p v-if="emailError" id="email-error" class="field-error">
              <UiIcon name="alert" class="field-message-icon" />
              <span>{{ emailError }}</span>
            </p>
          </div>

          <div class="account-field">
            <label for="password">Password</label>
            <div class="account-input">
              <input
                id="password"
                ref="passwordInput"
                v-model="password"
                :type="show ? 'text' : 'password'"
                autocomplete="current-password"
                :aria-invalid="passwordError ? 'true' : undefined"
                :aria-describedby="passwordHelp"
                @input="passwordError = ''"
                @keydown="checkCaps"
                @keyup="checkCaps"
                @blur="capsOn = false"
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
            <p v-if="passwordError" id="password-error" class="field-error">
              <UiIcon name="alert" class="field-message-icon" />
              <span>{{ passwordError }}</span>
            </p>
            <p v-else-if="capsOn" id="password-caps" class="field-note">
              <UiIcon name="alert" class="field-message-icon" />
              <span>Caps Lock is on.</span>
            </p>
          </div>

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
import { computed, nextTick, ref } from 'vue';
import { useRoute, useRouter } from 'vue-router';
import { useAuth } from '../stores/auth';
import { ApiError } from '../api';
import { canAccess } from '../nav';
import UiIcon from '../components/UiIcon.vue';

const router = useRouter();
const route = useRoute();
const { login } = useAuth();

/** The page to return to after signing in: only a path inside this app, and one the role can open. */
function returnPath(role: string, fallback: string): string {
  const next = typeof route.query.next === 'string' ? route.query.next : '';
  if (!next.startsWith('/') || next.startsWith('//') || next.startsWith('/login')) return fallback;
  return canAccess(role, next.split(/[?#]/)[0]) ? next : fallback;
}

const email = ref('');
const password = ref('');
const show = ref(false);
const loading = ref(false);
const error = ref('');
const emailError = ref('');
const passwordError = ref('');
const capsOn = ref(false);
const emailInput = ref<HTMLInputElement | null>(null);
const passwordInput = ref<HTMLInputElement | null>(null);

const emailPattern = /^[^\s@]+@[^\s@]+\.[^\s@]+$/;
const normalized = computed(() => email.value.trim().toLowerCase());
const passwordHelp = computed(() => (passwordError.value ? 'password-error' : capsOn.value ? 'password-caps' : undefined));

/** A password typed with Caps Lock on is the most common reason a right one fails. */
function checkCaps(event: KeyboardEvent) {
  capsOn.value = typeof event.getModifierState === 'function' && event.getModifierState('CapsLock');
}

async function submit() {
  if (loading.value) return;
  error.value = '';
  email.value = normalized.value;
  emailError.value = emailPattern.test(email.value) ? '' : 'Enter your full email address, such as name@renantina.ph.';
  passwordError.value = password.value ? '' : 'Enter your password.';
  if (emailError.value || passwordError.value) {
    await nextTick();
    (emailError.value ? emailInput.value : passwordInput.value)?.focus();
    return;
  }

  loading.value = true;
  try {
    const user = await login(email.value, password.value);
    await router.replace(returnPath(user.role, user.defaultPath));
  } catch (e) {
    if (e instanceof ApiError && e.status === 401) {
      // The server does not say which part was wrong, and neither does this.
      error.value = 'That email and password do not match an active account. Check both, or ask an owner whether the account is suspended.';
      await nextTick();
      passwordInput.value?.select();
    } else if (e instanceof ApiError) {
      error.value = e.message;
    } else {
      error.value = 'Could not reach ProphetOps. Check the connection, then sign in again.';
    }
  } finally {
    loading.value = false;
  }
}
</script>

<style scoped>
.login-error {
  margin: 0;
  color: var(--color-danger-ink);
  font-size: var(--text-sm);
}
</style>
