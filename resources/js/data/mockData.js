export const bookings = [
    {
        id: 'BKG-2401',
        ds: '2026-06-01',
        y: 18,
        client: 'Northstar Travel Services',
        package: 'Boracay Group Package',
        destination: 'Boracay',
        grossRevenue: 216000,
        paymentStatus: 'Partially Paid',
        bookingStatus: 'Confirmed',
        staffAssigned: 'Staff User',
        source: 'Messenger',
        notes: 'Group booking for July company outing.',
    },
    {
        id: 'BKG-2402',
        ds: '2026-06-02',
        y: 12,
        client: 'Apex Corporate Tours',
        package: 'Baguio Educational Tour',
        destination: 'Baguio',
        grossRevenue: 132000,
        paymentStatus: 'Paid',
        bookingStatus: 'Confirmed',
        staffAssigned: 'Admin User',
        source: 'Google Sheets',
        notes: 'School partner requested final itinerary.',
    },
    {
        id: 'BKG-2403',
        ds: '2026-06-03',
        y: 9,
        client: 'MetroLink Agency',
        package: 'Palawan Weekend Package',
        destination: 'Palawan',
        grossRevenue: 171000,
        paymentStatus: 'Pending',
        bookingStatus: 'Pending',
        staffAssigned: 'Staff User',
        source: 'Gmail',
        notes: 'Awaiting deposit confirmation.',
    },
    {
        id: 'BKG-2404',
        ds: '2026-06-04',
        y: 22,
        client: 'Summit Incentive Travel',
        package: 'Cebu Heritage Tour',
        destination: 'Cebu',
        grossRevenue: 242000,
        paymentStatus: 'Paid',
        bookingStatus: 'Confirmed',
        staffAssigned: 'Admin User',
        source: 'Notebook',
        notes: 'High passenger count. Check guide allocation.',
    },
    {
        id: 'BKG-2405',
        ds: '2026-06-05',
        y: 7,
        client: 'Harbor Trade Group',
        package: 'Bohol Countryside Tour',
        destination: 'Bohol',
        grossRevenue: 87500,
        paymentStatus: 'Partially Paid',
        bookingStatus: 'Reserved',
        staffAssigned: 'Staff User',
        source: 'Messenger',
        notes: 'Client may add more passengers next week.',
    },
    {
        id: 'BKG-2406',
        ds: '2026-06-06',
        y: 16,
        client: 'Pacific Link Partners',
        package: 'Boracay Group Package',
        destination: 'Boracay',
        grossRevenue: 192000,
        paymentStatus: 'Paid',
        bookingStatus: 'Confirmed',
        staffAssigned: 'Admin User',
        source: 'Google Sheets',
        notes: 'Repeat partner booking.',
    },
    {
        id: 'BKG-2407',
        ds: '2026-06-07',
        y: 6,
        client: 'Eastway Agency',
        package: 'Siargao Surf Package',
        destination: 'Siargao',
        grossRevenue: 96000,
        paymentStatus: 'Pending',
        bookingStatus: 'Pending',
        staffAssigned: 'Staff User',
        source: 'Gmail',
        notes: 'Needs flight availability review.',
    },
];

export const inventory = [
    {
        id: 'PKG-101',
        packageName: 'Boracay Group Package',
        destination: 'Boracay',
        availableSlots: 4,
        soldCount: 34,
        reservedCount: 8,
        status: 'Low',
        lastUpdated: '2026-06-07',
    },
    {
        id: 'PKG-102',
        packageName: 'Baguio Educational Tour',
        destination: 'Baguio',
        availableSlots: 18,
        soldCount: 42,
        reservedCount: 5,
        status: 'Normal',
        lastUpdated: '2026-06-06',
    },
    {
        id: 'PKG-103',
        packageName: 'Palawan Weekend Package',
        destination: 'Palawan',
        availableSlots: 3,
        soldCount: 27,
        reservedCount: 6,
        status: 'Critical',
        lastUpdated: '2026-06-07',
    },
    {
        id: 'PKG-104',
        packageName: 'Cebu Heritage Tour',
        destination: 'Cebu',
        availableSlots: 14,
        soldCount: 30,
        reservedCount: 4,
        status: 'Normal',
        lastUpdated: '2026-06-05',
    },
    {
        id: 'PKG-105',
        packageName: 'Bohol Countryside Tour',
        destination: 'Bohol',
        availableSlots: 9,
        soldCount: 21,
        reservedCount: 3,
        status: 'Normal',
        lastUpdated: '2026-06-04',
    },
    {
        id: 'PKG-106',
        packageName: 'Siargao Surf Package',
        destination: 'Siargao',
        availableSlots: 5,
        soldCount: 19,
        reservedCount: 7,
        status: 'Low',
        lastUpdated: '2026-06-07',
    },
];

export const expenses = [
    {
        id: 'EXP-3101',
        date: '2026-06-01',
        category: 'Tour operations',
        amount: 74000,
        package: 'Boracay Group Package',
        paymentStatus: 'Paid',
        notes: 'Hotel coordination and local transfers.',
    },
    {
        id: 'EXP-3102',
        date: '2026-06-02',
        category: 'Marketing',
        amount: 38500,
        package: 'Multiple destinations',
        paymentStatus: 'Paid',
        notes: 'June partner campaign placements.',
    },
    {
        id: 'EXP-3103',
        date: '2026-06-03',
        category: 'Seasonal cost',
        amount: 52000,
        package: 'Palawan Weekend Package',
        paymentStatus: 'Pending',
        notes: 'Peak-period supplier surcharge.',
    },
    {
        id: 'EXP-3104',
        date: '2026-06-04',
        category: 'Overhead',
        amount: 21000,
        package: 'Office operations',
        paymentStatus: 'Paid',
        notes: 'Office utilities and admin support.',
    },
    {
        id: 'EXP-3105',
        date: '2026-06-05',
        category: 'Tour operations',
        amount: 63000,
        package: 'Cebu Heritage Tour',
        paymentStatus: 'Paid',
        notes: 'Guide fees and destination coordination.',
    },
];

export const forecastProjection = [
    { date: 'Jun 09', bookings: 7, revenue: 98000 },
    { date: 'Jun 14', bookings: 10, revenue: 142000 },
    { date: 'Jun 19', bookings: 13, revenue: 181000 },
    { date: 'Jun 24', bookings: 15, revenue: 215000 },
    { date: 'Jun 29', bookings: 17, revenue: 242000 },
    { date: 'Jul 04', bookings: 16, revenue: 231000 },
];

export const trajectoryInsights = [
    {
        type: 'Risk',
        category: 'Inventory risk',
        finding: 'Boracay Package has only 4 slots left.',
        reason: 'Recent bookings are concentrated on Boracay while reserved count remains high.',
        action: 'Review package capacity before approving more promos.',
    },
    {
        type: 'Warning',
        category: 'Cost risk',
        finding: 'Marketing cost increased by 18%.',
        reason: 'Spending is rising faster than confirmed booking count this period.',
        action: 'Compare campaign cost with booking conversion.',
    },
    {
        type: 'Opportunity',
        category: 'Demand increase',
        finding: 'Cebu Heritage Tour has strong passenger volume.',
        reason: 'Passenger count is higher than other mid-range packages.',
        action: 'Prepare inventory and review pricing for next week.',
    },
    {
        type: 'Trend',
        category: 'Sales trend',
        finding: 'Revenue is above last month pace.',
        reason: 'Large B2B group bookings increased average transaction value.',
        action: 'Monitor operating costs to protect estimated profit.',
    },
];

export const reportCards = [
    {
        title: 'Sales Summary',
        description: 'Revenue, bookings, passenger count, and top destinations.',
        status: 'View ready',
    },
    {
        title: 'Inventory Summary',
        description: 'Package availability, reserved slots, and low inventory warnings.',
        status: 'View ready',
    },
    {
        title: 'Expense Summary',
        description: 'Operational, marketing, seasonal, and overhead cost review.',
        status: 'View ready',
    },
    {
        title: 'Forecast Summary',
        description: 'Sample forecast preview and future data requirements.',
        status: 'Placeholder',
    },
    {
        title: 'DSS Evaluation Summary',
        description: 'Prototype DSS review aligned to usability and functional suitability.',
        status: 'Draft',
    },
];

export function formatCurrency(value) {
    return new Intl.NumberFormat('en-PH', {
        style: 'currency',
        currency: 'PHP',
        maximumFractionDigits: 0,
    }).format(value || 0);
}

export function formatNumber(value) {
    return new Intl.NumberFormat('en-US').format(value || 0);
}

export function cloneRecords(records) {
    return JSON.parse(JSON.stringify(records));
}

export function totalBy(records, key) {
    return records.reduce((sum, record) => sum + Number(record[key] || 0), 0);
}

export function groupBookingsByDestination(records = bookings) {
    return Object.values(records.reduce((groups, booking) => {
        const key = booking.destination;
        groups[key] ||= {
            destination: key,
            revenue: 0,
            passengers: 0,
            bookings: 0,
        };

        groups[key].revenue += booking.grossRevenue;
        groups[key].passengers += booking.y;
        groups[key].bookings += 1;

        return groups;
    }, {})).sort((a, b) => b.revenue - a.revenue);
}

export function expensesByCategory(records = expenses) {
    return Object.values(records.reduce((groups, expense) => {
        groups[expense.category] ||= {
            category: expense.category,
            amount: 0,
        };

        groups[expense.category].amount += expense.amount;

        return groups;
    }, {})).sort((a, b) => b.amount - a.amount);
}
