import { canAccessNavigationLabel } from '../services/authAccess';

export function createNavigationGroups(activeLabel = 'Dashboard', role = null) {
    const makeItem = (item) => ({
        ...item,
        active: item.label === activeLabel,
    });

    const groups = [
        {
            label: 'Decision Support',
            items: [
                makeItem({ label: 'Dashboard', icon: 'dashboard', href: '/dashboard' }),
                makeItem({ label: 'Package Decision Guide', icon: 'shieldCheck', href: '/decision-guide' }),
                makeItem({ label: 'Analytics', icon: 'fileBarChart', href: '/analytics' }),
            ],
        },
        {
            label: 'Business Records',
            items: [
                makeItem({ label: 'Bookings', icon: 'database', href: '/bookings' }),
                makeItem({ label: 'Package Catalog', icon: 'boxes', href: '/inventory' }),
                makeItem({ label: 'Expenses', icon: 'wallet', href: '/expenses' }),
            ],
        },
        {
            label: 'Reports',
            items: [
                makeItem({ label: 'Reports', icon: 'fileBarChart', href: '/reports' }),
            ],
        },
        {
            label: 'Administration',
            items: [
                makeItem({ label: 'Users', icon: 'users', href: '/users' }),
            ],
        },
    ];

    return groups
        .map((group) => ({
            ...group,
            items: group.items.filter((item) => canAccessNavigationLabel(item.label, role)),
        }))
        .filter((group) => group.items.length);
}
