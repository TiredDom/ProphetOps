export type NavIconName = 'home' | 'bookings' | 'packages' | 'expenses' | 'reports' | 'team';

export interface NavItem {
  label: string;
  path: string;
  icon: NavIconName;
}

const sections: NavItem[] = [
  { label: 'Home', path: '/home', icon: 'home' },
  { label: 'Bookings', path: '/bookings', icon: 'bookings' },
  { label: 'Packages', path: '/packages', icon: 'packages' },
  { label: 'Expenses', path: '/expenses', icon: 'expenses' },
  { label: 'Reports', path: '/reports', icon: 'reports' },
];

// Account utilities sit with the signed-in user rather than in the operational navigation.
const accountLinks: NavItem[] = [{ label: 'Team & access', path: '/team', icon: 'team' }];

// Forecast and Analytics are being folded into Home and Reports. Until then they stay reachable
// by link for the roles that could open them before, but they are no longer navigation sections.
const allowedPaths: Record<string, string[]> = {
  'Owner / Management': ['/home', '/bookings', '/packages', '/expenses', '/reports', '/team', '/forecast', '/analytics'],
  Admin: ['/home', '/bookings', '/packages', '/expenses', '/reports', '/forecast', '/analytics'],
  Staff: ['/bookings', '/packages'],
};

const knownPaths = new Set(Object.values(allowedPaths).flat());

function allowed(role: string | undefined): string[] {
  return role ? allowedPaths[role] ?? [] : [];
}

export function navFor(role: string | undefined): NavItem[] {
  const paths = allowed(role);
  return sections.filter((item) => paths.includes(item.path));
}

export function accountLinksFor(role: string | undefined): NavItem[] {
  const paths = allowed(role);
  return accountLinks.filter((item) => paths.includes(item.path));
}

export function canAccess(role: string | undefined, path: string): boolean {
  if (!knownPaths.has(path)) return true;
  return allowed(role).includes(path);
}
