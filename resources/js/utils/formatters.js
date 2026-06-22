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
    return JSON.parse(JSON.stringify(records || []));
}

export function totalBy(records, key) {
    return records.reduce((sum, record) => sum + Number(record[key] || 0), 0);
}

export function groupBookingsByDestination(records = []) {
    return Object.values(records.reduce((groups, booking) => {
        const key = booking.destination;
        groups[key] ||= {
            destination: key,
            revenue: 0,
            passengers: 0,
            bookings: 0,
        };

        groups[key].revenue += Number(booking.grossRevenue || 0);
        groups[key].passengers += Number(booking.y || 0);
        groups[key].bookings += 1;

        return groups;
    }, {})).sort((a, b) => b.revenue - a.revenue);
}

export function expensesByCategory(records = []) {
    return Object.values(records.reduce((groups, expense) => {
        groups[expense.category] ||= {
            category: expense.category,
            amount: 0,
        };

        groups[expense.category].amount += Number(expense.amount || 0);

        return groups;
    }, {})).sort((a, b) => b.amount - a.amount);
}
