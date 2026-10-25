import { createRouter, createWebHistory } from 'vue-router';
import LoginView from './views/LoginView.vue';
import HomeView from './views/HomeView.vue';
import BookingsView from './views/BookingsView.vue';
import InventoryView from './views/InventoryView.vue';
import ExpensesView from './views/ExpensesView.vue';
import ReportsView from './views/ReportsView.vue';
import UsersView from './views/UsersView.vue';
import { useAuth } from './stores/auth';
import { canAccess } from './nav';

const router = createRouter({
  history: createWebHistory(),
  routes: [
    { path: '/login', component: LoginView, meta: { public: true } },
    { path: '/home', component: HomeView },
    { path: '/bookings', component: BookingsView },
    { path: '/packages', component: InventoryView },
    { path: '/expenses', component: ExpensesView },
    { path: '/reports', component: ReportsView },
    { path: '/team', component: UsersView },
    // Earlier addresses, kept so bookmarks and old links still land somewhere sensible.
    { path: '/dashboard', redirect: '/home' },
    { path: '/forecast', redirect: { path: '/home', hash: '#forecast-details' } },
    { path: '/inventory', redirect: '/packages' },
    { path: '/users', redirect: '/team' },
    { path: '/analytics', redirect: '/reports' },
    { path: '/:pathMatch(.*)*', redirect: '/home' },
  ],
});

router.beforeEach(async (to) => {
  const { state, loadSession } = useAuth();
  if (!state.ready) await loadSession();

  // Signing in again goes back to the page that was asked for.
  if (!to.meta.public && !state.user) {
    return to.fullPath === '/' || to.path === '/home' ? '/login' : { path: '/login', query: { next: to.fullPath } };
  }
  if (to.path === '/login' && state.user) return state.user.defaultPath;
  if (!to.meta.public && state.user && !canAccess(state.user.role, to.path)) return state.user.defaultPath;
  return true;
});

export default router;
