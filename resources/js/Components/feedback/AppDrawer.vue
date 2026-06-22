<template>
    <div class="drawer-backdrop" role="presentation" @click.self="$emit('close')">
        <aside
            ref="dialog"
            class="record-drawer sprint-drawer app-drawer open"
            role="dialog"
            aria-modal="true"
            :aria-labelledby="titleId"
            tabindex="-1"
        >
            <div class="drawer-header">
                <div>
                    <p v-if="eyebrow" class="eyebrow">{{ eyebrow }}</p>
                    <h3 :id="titleId">{{ title }}</h3>
                    <p v-if="description">{{ description }}</p>
                </div>
                <button class="icon-button" type="button" aria-label="Close drawer" @click="$emit('close')">
                    <AppIcon name="x" />
                </button>
            </div>

            <div class="drawer-body">
                <slot />
            </div>

            <div v-if="$slots.footer" class="drawer-actions">
                <slot name="footer" />
            </div>
        </aside>
    </div>
</template>

<script>
import AppIcon from '../icons/AppIcon.vue';

export default {
    name: 'AppDrawer',
    components: { AppIcon },
    emits: ['close'],
    props: {
        description: {
            type: String,
            default: '',
        },
        eyebrow: {
            type: String,
            default: '',
        },
        title: {
            type: String,
            required: true,
        },
    },
    computed: {
        titleId() {
            return `drawer-title-${this.title.toLowerCase().replace(/[^a-z0-9]+/g, '-')}`;
        },
    },
    mounted() {
        window.addEventListener('keydown', this.handleKeydown);
        this.$nextTick(() => {
            this.$refs.dialog?.focus();
        });
    },
    beforeUnmount() {
        window.removeEventListener('keydown', this.handleKeydown);
    },
    methods: {
        handleKeydown(event) {
            if (event.key === 'Escape') {
                this.$emit('close');
            }
        },
    },
};
</script>
