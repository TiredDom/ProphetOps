import { reactive } from 'vue';
import { api, type AuthUser } from '../api';

const state = reactive<{ user: AuthUser | null; ready: boolean }>({
  user: null,
  ready: false,
});

export function useAuth() {
  return {
    state,
    async loadSession(): Promise<void> {
      try {
        state.user = await api.me();
      } catch {
        state.user = null;
      } finally {
        state.ready = true;
      }
    },
    async login(email: string, password: string): Promise<AuthUser> {
      state.user = await api.login(email, password);
      return state.user;
    },
    async logout(): Promise<void> {
      await api.logout();
      state.user = null;
    },
  };
}
