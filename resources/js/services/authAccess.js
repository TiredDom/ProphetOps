export const rolePermissions = {
    'Owner / Management': [
        'Dashboard',
        'Bookings',
        'Package Catalog',
        'Expenses',
        'Analytics',
        'Package Decision Guide',
        'Reports',
        'Users',
    ],
    Admin: [
        'Dashboard',
        'Bookings',
        'Package Catalog',
        'Expenses',
        'Analytics',
        'Package Decision Guide',
        'Reports',
    ],
    Staff: [
        'Bookings',
        'Package Catalog',
    ],
};

export const defaultPathByRole = {
    'Owner / Management': '/dashboard',
    Admin: '/dashboard',
    Staff: '/bookings',
};

export function normalizeLoginEmail(email) {
    return String(email || '').trim().toLowerCase();
}

export function canAccessNavigationLabel(label, role) {
    return Boolean(rolePermissions[role]?.includes(label));
}

export function defaultPathForRole(role) {
    return defaultPathByRole[role] || '/login';
}
