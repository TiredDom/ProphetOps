<template>
    <svg
        class="app-icon"
        :class="className"
        viewBox="0 0 24 24"
        fill="none"
        stroke="currentColor"
        stroke-width="1.8"
        stroke-linecap="round"
        stroke-linejoin="round"
        aria-hidden="true"
    >
        <template v-for="shape in shapes" :key="shape.key">
            <component :is="shape.tag" v-bind="shape.attrs" />
        </template>
    </svg>
</template>

<script>
const icons = {
    dashboard: [
        { tag: 'rect', attrs: { x: 3, y: 3, width: 7, height: 8, rx: 2 } },
        { tag: 'rect', attrs: { x: 14, y: 3, width: 7, height: 5, rx: 2 } },
        { tag: 'rect', attrs: { x: 14, y: 12, width: 7, height: 9, rx: 2 } },
        { tag: 'rect', attrs: { x: 3, y: 15, width: 7, height: 6, rx: 2 } },
    ],
    database: [
        { tag: 'ellipse', attrs: { cx: 12, cy: 5, rx: 8, ry: 3 } },
        { tag: 'path', attrs: { d: 'M4 5v6c0 1.7 3.6 3 8 3s8-1.3 8-3V5' } },
        { tag: 'path', attrs: { d: 'M4 11v6c0 1.7 3.6 3 8 3s8-1.3 8-3v-6' } },
    ],
    shieldCheck: [
        { tag: 'path', attrs: { d: 'M12 3l7 3v5c0 4.4-2.8 8.2-7 10-4.2-1.8-7-5.6-7-10V6l7-3z' } },
        { tag: 'path', attrs: { d: 'M9 12l2 2 4-5' } },
    ],
    mapPinned: [
        { tag: 'path', attrs: { d: 'M9 18l-6 3V6l6-3 6 3 6-3v15l-6 3-6-3z' } },
        { tag: 'path', attrs: { d: 'M9 3v15' } },
        { tag: 'path', attrs: { d: 'M15 6v15' } },
        { tag: 'path', attrs: { d: 'M12 8c1.4 0 2.5 1.1 2.5 2.5 0 2-2.5 4.5-2.5 4.5s-2.5-2.5-2.5-4.5C9.5 9.1 10.6 8 12 8z' } },
    ],
    wallet: [
        { tag: 'path', attrs: { d: 'M4 7h15a2 2 0 0 1 2 2v9a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2V6a2 2 0 0 1 2-2h12' } },
        { tag: 'path', attrs: { d: 'M16 13h5' } },
        { tag: 'circle', attrs: { cx: 17.5, cy: 13, r: 0.8 } },
    ],
    boxes: [
        { tag: 'path', attrs: { d: 'M4 8l4-2 4 2-4 2-4-2z' } },
        { tag: 'path', attrs: { d: 'M12 8l4-2 4 2-4 2-4-2z' } },
        { tag: 'path', attrs: { d: 'M8 10v5l4 2 4-2v-5' } },
        { tag: 'path', attrs: { d: 'M4 8v5l4 2' } },
        { tag: 'path', attrs: { d: 'M20 8v5l-4 2' } },
    ],
    fileBarChart: [
        { tag: 'path', attrs: { d: 'M7 3h7l5 5v13H7a2 2 0 0 1-2-2V5a2 2 0 0 1 2-2z' } },
        { tag: 'path', attrs: { d: 'M14 3v5h5' } },
        { tag: 'path', attrs: { d: 'M9 17v-4' } },
        { tag: 'path', attrs: { d: 'M12 17v-7' } },
        { tag: 'path', attrs: { d: 'M15 17v-5' } },
    ],
    lineChart: [
        { tag: 'path', attrs: { d: 'M4 19V5' } },
        { tag: 'path', attrs: { d: 'M4 19h16' } },
        { tag: 'path', attrs: { d: 'M7 15l3-4 3 2 4-6 3 3' } },
        { tag: 'circle', attrs: { cx: 7, cy: 15, r: 1 } },
        { tag: 'circle', attrs: { cx: 10, cy: 11, r: 1 } },
        { tag: 'circle', attrs: { cx: 13, cy: 13, r: 1 } },
        { tag: 'circle', attrs: { cx: 17, cy: 7, r: 1 } },
    ],
    routePath: [
        { tag: 'circle', attrs: { cx: 6, cy: 18, r: 2.2 } },
        { tag: 'circle', attrs: { cx: 18, cy: 6, r: 2.2 } },
        { tag: 'path', attrs: { d: 'M8 18h3.5a3 3 0 0 0 0-6H10a3 3 0 0 1 0-6h5.8' } },
        { tag: 'path', attrs: { d: 'M15.5 3.5L18 6l-2.5 2.5' } },
    ],
    sparkles: [
        { tag: 'path', attrs: { d: 'M12 3l1.5 4.5L18 9l-4.5 1.5L12 15l-1.5-4.5L6 9l4.5-1.5L12 3z' } },
        { tag: 'path', attrs: { d: 'M5 15l.8 2.2L8 18l-2.2.8L5 21l-.8-2.2L2 18l2.2-.8L5 15z' } },
        { tag: 'path', attrs: { d: 'M19 14l.6 1.6L21 16l-1.4.4L19 18l-.6-1.6L17 16l1.4-.4L19 14z' } },
    ],
    users: [
        { tag: 'path', attrs: { d: 'M16 21v-2a4 4 0 0 0-4-4H7a4 4 0 0 0-4 4v2' } },
        { tag: 'circle', attrs: { cx: 9.5, cy: 7, r: 4 } },
        { tag: 'path', attrs: { d: 'M22 21v-2a4 4 0 0 0-3-3.8' } },
        { tag: 'path', attrs: { d: 'M16 3.3a4 4 0 0 1 0 7.4' } },
    ],
    settings: [
        { tag: 'circle', attrs: { cx: 12, cy: 12, r: 3 } },
        { tag: 'path', attrs: { d: 'M19.4 15a1.8 1.8 0 0 0 .4 2l.1.1-2 3.4-.2-.1a1.8 1.8 0 0 0-2 .4l-.1.1a1.8 1.8 0 0 0-.5 1.7H9a1.8 1.8 0 0 0-.5-1.7l-.1-.1a1.8 1.8 0 0 0-2-.4l-.2.1-2-3.4.1-.1a1.8 1.8 0 0 0 .4-2 1.8 1.8 0 0 0-1.5-1H3v-4h.2a1.8 1.8 0 0 0 1.5-1 1.8 1.8 0 0 0-.4-2l-.1-.1 2-3.4.2.1a1.8 1.8 0 0 0 2-.4l.1-.1A1.8 1.8 0 0 0 9 1.5h6a1.8 1.8 0 0 0 .5 1.7l.1.1a1.8 1.8 0 0 0 2 .4l.2-.1 2 3.4-.1.1a1.8 1.8 0 0 0-.4 2 1.8 1.8 0 0 0 1.5 1h.2v4h-.2a1.8 1.8 0 0 0-1.4 1z' } },
    ],
    calendar: [
        { tag: 'rect', attrs: { x: 3, y: 4, width: 18, height: 17, rx: 2 } },
        { tag: 'path', attrs: { d: 'M8 2v4' } },
        { tag: 'path', attrs: { d: 'M16 2v4' } },
        { tag: 'path', attrs: { d: 'M3 10h18' } },
    ],
    bell: [
        { tag: 'path', attrs: { d: 'M18 8a6 6 0 0 0-12 0c0 7-3 7-3 9h18c0-2-3-2-3-9' } },
        { tag: 'path', attrs: { d: 'M10 21a2 2 0 0 0 4 0' } },
    ],
    plus: [
        { tag: 'path', attrs: { d: 'M12 5v14' } },
        { tag: 'path', attrs: { d: 'M5 12h14' } },
    ],
    check: [
        { tag: 'path', attrs: { d: 'M20 6L9 17l-5-5' } },
    ],
    arrowRight: [
        { tag: 'path', attrs: { d: 'M5 12h14' } },
        { tag: 'path', attrs: { d: 'M13 6l6 6-6 6' } },
    ],
    message: [
        { tag: 'path', attrs: { d: 'M21 15a4 4 0 0 1-4 4H8l-5 3V7a4 4 0 0 1 4-4h10a4 4 0 0 1 4 4v8z' } },
    ],
    sheet: [
        { tag: 'rect', attrs: { x: 4, y: 3, width: 16, height: 18, rx: 2 } },
        { tag: 'path', attrs: { d: 'M8 8h8' } },
        { tag: 'path', attrs: { d: 'M8 12h8' } },
        { tag: 'path', attrs: { d: 'M8 16h5' } },
    ],
    mail: [
        { tag: 'rect', attrs: { x: 3, y: 5, width: 18, height: 14, rx: 2 } },
        { tag: 'path', attrs: { d: 'M4 7l8 6 8-6' } },
    ],
    notebook: [
        { tag: 'path', attrs: { d: 'M6 4h13a2 2 0 0 1 2 2v14H6a3 3 0 0 1-3-3V7a3 3 0 0 1 3-3z' } },
        { tag: 'path', attrs: { d: 'M7 4v17' } },
        { tag: 'path', attrs: { d: 'M10 8h7' } },
    ],
    paperRecord: [
        { tag: 'path', attrs: { d: 'M7 3h7l5 5v13H7a2 2 0 0 1-2-2V5a2 2 0 0 1 2-2z' } },
        { tag: 'path', attrs: { d: 'M14 3v5h5' } },
        { tag: 'path', attrs: { d: 'M8 13h8' } },
        { tag: 'path', attrs: { d: 'M8 17h6' } },
    ],
    search: [
        { tag: 'circle', attrs: { cx: 11, cy: 11, r: 7 } },
        { tag: 'path', attrs: { d: 'M20 20l-4.5-4.5' } },
    ],
    filter: [
        { tag: 'path', attrs: { d: 'M4 5h16' } },
        { tag: 'path', attrs: { d: 'M7 12h10' } },
        { tag: 'path', attrs: { d: 'M10 19h4' } },
    ],
    edit: [
        { tag: 'path', attrs: { d: 'M12 20h9' } },
        { tag: 'path', attrs: { d: 'M16.5 3.5a2.1 2.1 0 0 1 3 3L8 18l-4 1 1-4 11.5-11.5z' } },
    ],
    archive: [
        { tag: 'path', attrs: { d: 'M4 7h16v13H4V7z' } },
        { tag: 'path', attrs: { d: 'M3 3h18v4H3V3z' } },
        { tag: 'path', attrs: { d: 'M9 12h6' } },
    ],
    save: [
        { tag: 'path', attrs: { d: 'M5 3h12l2 2v16H5a2 2 0 0 1-2-2V5a2 2 0 0 1 2-2z' } },
        { tag: 'path', attrs: { d: 'M8 3v6h8V3' } },
        { tag: 'path', attrs: { d: 'M8 21v-7h8v7' } },
    ],
    x: [
        { tag: 'path', attrs: { d: 'M18 6L6 18' } },
        { tag: 'path', attrs: { d: 'M6 6l12 12' } },
    ],
    alertTriangle: [
        { tag: 'path', attrs: { d: 'M12 3l10 18H2L12 3z' } },
        { tag: 'path', attrs: { d: 'M12 9v5' } },
        { tag: 'path', attrs: { d: 'M12 17h.01' } },
    ],
    lock: [
        { tag: 'rect', attrs: { x: 5, y: 10, width: 14, height: 11, rx: 2 } },
        { tag: 'path', attrs: { d: 'M8 10V7a4 4 0 0 1 8 0v3' } },
    ],
    eye: [
        { tag: 'path', attrs: { d: 'M2 12s4-7 10-7 10 7 10 7-4 7-10 7S2 12 2 12z' } },
        { tag: 'circle', attrs: { cx: 12, cy: 12, r: 3 } },
    ],
    eyeOff: [
        { tag: 'path', attrs: { d: 'M3 3l18 18' } },
        { tag: 'path', attrs: { d: 'M10.6 10.6a2 2 0 0 0 2.8 2.8' } },
        { tag: 'path', attrs: { d: 'M9.9 4.6A10.7 10.7 0 0 1 12 4c6 0 10 8 10 8a16.8 16.8 0 0 1-3.2 4.2' } },
        { tag: 'path', attrs: { d: 'M6.4 6.4A16.2 16.2 0 0 0 2 12s4 8 10 8a10.9 10.9 0 0 0 4.1-.8' } },
    ],
    logIn: [
        { tag: 'path', attrs: { d: 'M15 3h4a2 2 0 0 1 2 2v14a2 2 0 0 1-2 2h-4' } },
        { tag: 'path', attrs: { d: 'M10 17l5-5-5-5' } },
        { tag: 'path', attrs: { d: 'M15 12H3' } },
    ],
    logOut: [
        { tag: 'path', attrs: { d: 'M9 21H5a2 2 0 0 1-2-2V5a2 2 0 0 1 2-2h4' } },
        { tag: 'path', attrs: { d: 'M16 17l5-5-5-5' } },
        { tag: 'path', attrs: { d: 'M21 12H9' } },
    ],
};

export default {
    name: 'AppIcon',
    props: {
        name: {
            type: String,
            required: true,
        },
        className: {
            type: String,
            default: '',
        },
    },
    computed: {
        shapes() {
            return (icons[this.name] || icons.dashboard).map((shape, index) => ({
                ...shape,
                key: `${shape.tag}-${index}`,
            }));
        },
    },
};
</script>
