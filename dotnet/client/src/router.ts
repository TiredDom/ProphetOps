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
import { canAccess } from './nav';

const router = createRouter({
  history: createWebHistory(),
  routes: [
    { path: '/login', component: LoginView, meta: { public: true } },
    { path: '/home', component: DashboardView },
    { path: '/bookings', component: BookingsView },
    { path: '/packages', component: InventoryView },
    { path: '/expenses', component: ExpensesView },
    { path: '/reports', component: ReportsView },
    { path: '/team', component: UsersView },
    { path: '/forecast', component: ForecastView },
    { path: '/analytics', component: AnalyticsView },
    // Earlier addresses, kept so bookmarks and old links still land somewhere sensible.
    { path: '/dashboard', redirect: '/home' },
    { path: '/inventory', redirect: '/packages' },
    { path: '/users', redirect: '/team' },
    { path: '/:pathMatch(.*)*', redirect: '/home' },
  ],
});

router.beforeEach(async (to) => {
  const { state, loadSession } = useAuth();
  if (!state.ready) await loadSession();

  if (!to.meta.public && !state.user) return '/login';
  if (to.path === '/login' && state.user) return state.user.defaultPath;
  if (!to.meta.public && state.user && !canAccess(state.user.role, to.path)) return state.user.defaultPath;
  return true;
});

export default router;
