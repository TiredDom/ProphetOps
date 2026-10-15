import { createApp } from 'vue';
import '@fontsource-variable/inter';
import '@fontsource-variable/fraunces';
import './assets/app.css';
import App from './App.vue';
import router from './router';
import { listenForInstall } from './composables/usePwaInstall';

listenForInstall();

createApp(App).use(router).mount('#app');

// The worker only makes the app installable and explains a lost connection; it stores nothing.
// The development server reloads modules itself, so the worker is left to the built app.
if (import.meta.env.PROD && 'serviceWorker' in navigator) {
  window.addEventListener('load', () => {
    navigator.serviceWorker.register('/sw.js').catch(() => {
      // Installing is optional: the app works the same without the worker.
    });
  });
}
