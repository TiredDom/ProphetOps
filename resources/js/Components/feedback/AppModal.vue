<template>
    <div class="modal-backdrop" role="presentation" @click.self="$emit('close')">
        <section
            ref="dialog"
            class="app-modal"
            role="dialog"
            aria-modal="true"
            :aria-labelledby="titleId"
            tabindex="-1"
        >
            <div class="modal-header">
                <div>
                    <p v-if="eyebrow" class="eyebrow">{{ eyebrow }}</p>
                    <h3 :id="titleId">{{ title }}</h3>
                </div>
                <button class="icon-button" type="button" aria-label="Close modal" @click="$emit('close')">
                    <AppIcon name="x" />
                </button>
            </div>

            <div class="modal-body">
                <slot />
            </div>

            <div v-if="$slots.footer" class="modal-footer">
                <slot name="footer" />
            </div>
        </section>
    </div>
</template>

<script>
import AppIcon from '../icons/AppIcon.vue';

export default {
    name: 'AppModal',
    components: {
        AppIcon,
    },
    props: {
        title: {
            type: String,
            required: true,
        },
        eyebrow: {
            type: String,
            default: '',
        },
    },
    emits: ['close'],
    computed: {
        titleId() {
            return `modal-title-${this.title.toLowerCase().replace(/[^a-z0-9]+/g, '-')}`;
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
