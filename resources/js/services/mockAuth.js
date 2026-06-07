const SESSION_KEY = 'prophetops_mock_auth_session';

const mockInternalUser = {
    name: 'Administrator',
    role: 'Admin',
    access: 'internal',
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

export function requireMockAuth(redirectPath = '/login') {
    if (isMockAuthenticated()) {
        return true;
    }

    window.location.replace(redirectPath);

    return false;
}

export function mockLogin({ email, rememberMe = false }) {
    const normalizedEmail = normalizeLoginEmail(email);

    // Mock auth only. Replace this function with a real backend call when Laravel auth is implemented.
    const session = {
        authenticated: true,
        user: {
            ...mockInternalUser,
            email: normalizedEmail,
        },
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
