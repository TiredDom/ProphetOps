export interface NavItem {
  label: string;
  path: string;
}

export interface NavGroup {
  label: string;
  items: NavItem[];
}

const groups: NavGroup[] = [
  {
    label: 'Decision Support',
    items: [
      { label: 'Dashboard', path: '/dashboard' },
      { label: 'Forecast', path: '/forecast' },
      { label: 'Analytics', path: '/analytics' },
    ],
  },
  {
    label: 'Business Records',
    items: [
      { label: 'Bookings', path: '/bookings' },
      { label: 'Package Catalog', path: '/inventory' },
      { label: 'Expenses', path: '/expenses' },
    ],
  },
  {
    label: 'Reports',
    items: [{ label: 'Reports', path: '/reports' }],
  },
  {
    label: 'Administration',
    items: [{ label: 'Users', path: '/users' }],
  },
];

const permissions: Record<string, string[]> = {
  'Owner / Management': ['Dashboard', 'Bookings', 'Package Catalog', 'Expenses', 'Analytics', 'Forecast', 'Reports', 'Users'],
  Admin: ['Dashboard', 'Bookings', 'Package Catalog', 'Expenses', 'Analytics', 'Forecast', 'Reports'],
  Staff: ['Bookings', 'Package Catalog'],
};

export function navFor(role: string | undefined): NavGroup[] {
  const allowed = role ? permissions[role] ?? [] : [];
  return groups
    .map((group) => ({ ...group, items: group.items.filter((item) => allowed.includes(item.label)) }))
    .filter((group) => group.items.length > 0);
}
