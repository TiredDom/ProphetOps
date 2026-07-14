import { createRouter, createWebHistory } from 'vue-router';
import LoginView from './views/LoginView.vue';
import DashboardView from './views/DashboardView.vue';
import { useAuth } from './stores/auth';

const router = createRouter({
  history: createWebHistory(),
  routes: [
    { path: '/login', component: LoginView, meta: { public: true } },
    { path: '/dashboard', component: DashboardView },
    { path: '/:pathMatch(.*)*', redirect: '/dashboard' },
  ],
});

router.beforeEach(async (to) => {
  const { state, loadSession } = useAuth();
  if (!state.ready) await loadSession();

  if (!to.meta.public && !state.user) return '/login';
  if (to.path === '/login' && state.user) return state.user.defaultPath;
  return true;
});

export default router;
