<template>
    <div class="table-scroll" tabindex="0" :aria-label="label">
        <slot />
    </div>
</template>

<script>
export default {
    name: 'DataTableFrame',
    props: {
        label: {
            type: String,
            default: 'Scrollable data table',
        },
    },
};
</script>
