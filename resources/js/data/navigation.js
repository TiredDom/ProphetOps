export function createNavigationGroups(activeLabel = 'Dashboard') {
    const makeItem = (item) => ({
        ...item,
        active: item.label === activeLabel,
    });

    return [
        {
            label: 'Overview',
            items: [
                makeItem({ label: 'Dashboard', icon: 'dashboard', href: '/dashboard' }),
            ],
        },
        {
            label: 'Data Workspace',
            items: [
                makeItem({ label: 'Operational Records', icon: 'database', href: '/data/operational-records' }),
                makeItem({ label: 'Data Validation', icon: 'shieldCheck', href: '/data/validation' }),
            ],
        },
        {
            label: 'Business Records',
            items: [
                makeItem({ label: 'Package / Destination References', icon: 'mapPinned' }),
                makeItem({ label: 'Expenses', icon: 'wallet' }),
            ],
        },
        {
            label: 'Operations',
            items: [
                makeItem({ label: 'Inventory', icon: 'boxes', href: '/operations/inventory' }),
            ],
        },
        {
            label: 'Reports',
            items: [
                makeItem({ label: 'Reports', icon: 'fileBarChart' }),
            ],
        },
        {
            label: 'Analytics',
            items: [
                makeItem({ label: 'Forecasting', icon: 'sparkles', locked: true }),
            ],
        },
        {
            label: 'Administration',
            items: [
                makeItem({ label: 'Users', icon: 'users' }),
                makeItem({ label: 'Settings', icon: 'settings' }),
            ],
        },
    ];
}
