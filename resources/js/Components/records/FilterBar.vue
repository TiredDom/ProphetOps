<template>
    <div class="dss-filterbar" :class="className" role="search" :aria-label="label">
        <slot />
    </div>
</template>

<script>
export default {
    name: 'FilterBar',
    props: {
        label: {
            type: String,
            default: 'Filters',
        },
        className: {
            type: [String, Array, Object],
            default: '',
        },
    },
};
</script>
