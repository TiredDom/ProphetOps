import { createRouter, createWebHistory } from 'vue-router';
import LoginView from './views/LoginView.vue';
import DashboardView from './views/DashboardView.vue';
import BookingsView from './views/BookingsView.vue';
import InventoryView from './views/InventoryView.vue';
import ExpensesView from './views/ExpensesView.vue';
import AnalyticsView from './views/AnalyticsView.vue';
import ForecastView from './views/ForecastView.vue';
import ReportsView from './views/ReportsView.vue';
import UsersView from './views/UsersView.vue';
import { useAuth } from './stores/auth';

const router = createRouter({
  history: createWebHistory(),
  routes: [
    { path: '/login', component: LoginView, meta: { public: true } },
    { path: '/dashboard', component: DashboardView },
    { path: '/bookings', component: BookingsView },
    { path: '/inventory', component: InventoryView },
    { path: '/expenses', component: ExpensesView },
    { path: '/analytics', component: AnalyticsView },
    { path: '/forecast', component: ForecastView },
    { path: '/reports', component: ReportsView },
    { path: '/users', component: UsersView },
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
