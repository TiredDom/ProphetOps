import { createApp } from 'vue';
import '@fontsource-variable/inter';
import '@fontsource-variable/fraunces';
import './assets/app.css';
import App from './App.vue';
import router from './router';

createApp(App).use(router).mount('#app');
