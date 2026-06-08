const SESSION_KEY = 'prophetops_mock_auth_session';

export const demoUsers = [
    {
        name: 'Maria Santos',
        role: 'Owner / Management',
        email: 'owner@prophetops.local',
        password: 'owner123',
        status: 'Active',
        lastLogin: 'Today, 8:12 AM',
    },
    {
        name: 'Admin User',
        role: 'Admin',
        email: 'admin@prophetops.local',
        password: 'admin123',
        status: 'Active',
        lastLogin: 'Today, 7:48 AM',
    },
    {
        name: 'Staff User',
        role: 'Staff',
        email: 'staff@prophetops.local',
        password: 'staff123',
        status: 'Active',
        lastLogin: 'Yesterday, 5:33 PM',
    },
];

export const rolePermissions = {
    'Owner / Management': [
        'Dashboard',
        'Bookings',
        'Inventory',
        'Expenses',
        'Analytics',
        'Forecasting',
        'Trajectory Insights',
        'Reports',
        'Users',
    ],
    Admin: [
        'Dashboard',
        'Bookings',
        'Inventory',
        'Expenses',
        'Analytics',
        'Reports',
    ],
    Staff: [
        'Bookings',
        'Inventory',
    ],
};

export const defaultPathByRole = {
    'Owner / Management': '/dashboard',
    Admin: '/dashboard',
    Staff: '/bookings',
};

function preferredStorage(rememberMe = false) {
    return rememberMe ? window.localStorage : window.sessionStorage;
}

function readSessionFrom(storage) {
    try {
        return JSON.parse(storage.getItem(SESSION_KEY));
    } catch {
        return null;
    }
}

function clearStorage(storage) {
    try {
        storage.removeItem(SESSION_KEY);
    } catch {
        // Frontend mock auth only. Storage failures are handled by the login page.
    }
}

export function normalizeLoginEmail(email) {
    return String(email || '').trim().toLowerCase();
}

export function getMockAuthSession() {
    if (typeof window === 'undefined') {
        return null;
    }

    return readSessionFrom(window.sessionStorage) || readSessionFrom(window.localStorage);
}

export function getMockUser() {
    return getMockAuthSession()?.user || null;
}

export function isMockAuthenticated() {
    return Boolean(getMockUser());
}

export function clearMockAuthSession() {
    if (typeof window === 'undefined') {
        return;
    }

    clearStorage(window.sessionStorage);
    clearStorage(window.localStorage);
}

export function canAccessNavigationLabel(label, role = getMockUser()?.role) {
    return Boolean(rolePermissions[role]?.includes(label));
}

export function defaultPathForRole(role = getMockUser()?.role) {
    return defaultPathByRole[role] || '/login';
}

export function requireMockAuth(redirectPath = '/login') {
    if (isMockAuthenticated()) {
        return true;
    }

    window.location.replace(redirectPath);

    return false;
}

export function requirePageAccess(label, redirectPath = null) {
    if (!requireMockAuth()) {
        return false;
    }

    const user = getMockUser();

    if (canAccessNavigationLabel(label, user?.role)) {
        return true;
    }

    window.location.replace(redirectPath || defaultPathForRole(user?.role));

    return false;
}

export function mockLogin({ email, password, rememberMe = false }) {
    const normalizedEmail = normalizeLoginEmail(email);
    const matchedUser = demoUsers.find((user) =>
        user.email === normalizedEmail && user.password === password,
    );

    if (!matchedUser) {
        throw new Error('Invalid demo credentials.');
    }

    const { password: _password, ...safeUser } = matchedUser;

    // Mock auth only. Replace this function with a real backend call when Laravel auth is implemented.
    const session = {
        authenticated: true,
        user: safeUser,
        issuedAt: new Date().toISOString(),
        mode: 'frontend-mock-only',
    };

    clearMockAuthSession();

    try {
        preferredStorage(rememberMe).setItem(SESSION_KEY, JSON.stringify(session));
    } catch {
        window.sessionStorage.setItem(SESSION_KEY, JSON.stringify(session));
    }

    return session.user;
}
