import { createApp, h } from 'vue';
import { createInertiaApp, http } from '@inertiajs/vue3';
import { resolvePageComponent } from 'laravel-vite-plugin/inertia-helpers';
import '../css/app.css';

const appName = import.meta.env.VITE_APP_NAME || 'ProphetOps';
const csrfToken = document.querySelector('meta[name="csrf-token"]')?.getAttribute('content');

if (csrfToken) {
    http.onRequest((config) => ({
        ...config,
        headers: {
            ...config.headers,
            'X-CSRF-TOKEN': csrfToken,
        },
    }));
}

createInertiaApp({
    title: (title) => (title ? `${title} - ${appName}` : appName),
    resolve: (name) =>
        resolvePageComponent(
            `./Pages/${name}.vue`,
            import.meta.glob('./Pages/**/*.vue'),
        ),
    setup({ el, App, props, plugin }) {
        return createApp({ render: () => h(App, props) })
            .use(plugin)
            .mount(el);
    },
    progress: {
        color: '#2563EB',
    },
});
