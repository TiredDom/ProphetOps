<?php

namespace App\Support;

use App\Models\Booking;
use App\Models\Expense;
use App\Models\TravelPackage;
use App\Models\User;
use Illuminate\Support\Collection;

class ProphetOpsData
{
    public const ROLES = ['Owner / Management', 'Admin', 'Staff'];

    public const ROLE_PERMISSIONS = [
        'Owner / Management' => [
            'Dashboard',
            'Bookings',
            'Package Catalog',
            'Expenses',
            'Analytics',
            'Package Decision Guide',
            'Reports',
            'Users',
        ],
        'Admin' => [
            'Dashboard',
            'Bookings',
            'Package Catalog',
            'Expenses',
            'Analytics',
            'Package Decision Guide',
            'Reports',
        ],
        'Staff' => [
            'Bookings',
            'Package Catalog',
        ],
    ];

    public const DEFAULT_PATHS = [
        'Owner / Management' => '/dashboard',
        'Admin' => '/dashboard',
        'Staff' => '/bookings',
    ];

    public static function defaultPathForRole(?string $role): string
    {
        return self::DEFAULT_PATHS[$role] ?? '/login';
    }

    public static function canAccess(?string $role, string $label): bool
    {
        return in_array($label, self::ROLE_PERMISSIONS[$role] ?? [], true);
    }

    public static function seededAccounts(): array
    {
        return [
            [
                'name' => 'Maria Santos',
                'role' => 'Owner / Management',
                'email' => 'owner@prophetops.local',
                'password' => 'owner123',
                'status' => 'Active',
            ],
            [
                'name' => 'Admin User',
                'role' => 'Admin',
                'email' => 'admin@prophetops.local',
                'password' => 'admin123',
                'status' => 'Active',
            ],
            [
                'name' => 'Staff User',
                'role' => 'Staff',
                'email' => 'staff@prophetops.local',
                'password' => 'staff123',
                'status' => 'Active',
            ],
        ];
    }

    public static function booking(Booking $booking): array
    {
        return [
            'id' => $booking->code,
            'backendId' => $booking->id,
            'ds' => optional($booking->booking_date)->toDateString(),
            'y' => $booking->passenger_count,
            'client' => $booking->client,
            'package' => $booking->package_name,
            'packageId' => $booking->package_code,
            'entryType' => $booking->entry_type,
            'destination' => $booking->destination,
            'grossRevenue' => $booking->gross_revenue,
            'paymentStatus' => $booking->payment_status,
            'bookingStatus' => $booking->booking_status,
            'staffAssigned' => $booking->staff_assigned,
            'source' => $booking->source,
            'notes' => $booking->notes,
        ];
    }

    public static function bookings(): array
    {
        return Booking::query()
            ->latest('booking_date')
            ->get()
            ->map(fn (Booking $booking) => self::booking($booking))
            ->values()
            ->all();
    }

    public static function package(TravelPackage $package): array
    {
        return [
            'id' => $package->code,
            'backendId' => $package->id,
            'packageName' => $package->package_name,
            'destination' => $package->destination,
            'duration' => $package->duration,
            'basePrice' => $package->base_price,
            'inclusions' => $package->inclusions,
            'travelType' => $package->travel_type,
            'supplierReliabilityScore' => $package->supplier_reliability_score,
            'businessValueScore' => $package->business_value_score,
            'riskScore' => $package->risk_score,
            'availableSlots' => $package->available_slots,
            'soldCount' => $package->sold_count,
            'reservedCount' => $package->reserved_count,
            'status' => $package->status,
            'lastUpdated' => optional($package->last_updated_at)->toDateString(),
        ];
    }

    public static function packages(): array
    {
        return TravelPackage::query()
            ->orderBy('code')
            ->get()
            ->map(fn (TravelPackage $package) => self::package($package))
            ->values()
            ->all();
    }

    public static function expense(Expense $expense): array
    {
        return [
            'id' => $expense->code,
            'backendId' => $expense->id,
            'date' => optional($expense->expense_date)->toDateString(),
            'category' => $expense->category,
            'amount' => $expense->amount,
            'package' => $expense->related_package,
            'paymentStatus' => $expense->payment_status,
            'notes' => $expense->notes,
        ];
    }

    public static function expenses(): array
    {
        return Expense::query()
            ->latest('expense_date')
            ->get()
            ->map(fn (Expense $expense) => self::expense($expense))
            ->values()
            ->all();
    }

    public static function users(): array
    {
        return User::query()
            ->orderByRaw("CASE role WHEN 'Owner / Management' THEN 1 WHEN 'Admin' THEN 2 ELSE 3 END")
            ->orderBy('name')
            ->get()
            ->map(fn (User $user) => [
                'name' => $user->name,
                'role' => $user->role,
                'email' => $user->email,
                'status' => $user->status,
                'lastLogin' => $user->last_login_at?->format('M j, g:i A') ?? 'Not yet',
            ])
            ->values()
            ->all();
    }

    public static function decisionReviewSignals(): array
    {
        $packages = collect(self::packages());
        $bookings = collect(self::bookings());
        $expenses = collect(self::expenses());
        $lowPackages = $packages->filter(fn (array $item) => in_array($item['status'], ['Low', 'Critical'], true));
        $topDestination = self::destinationRevenue($bookings)->first();
        $costliest = self::expensesByCategory($expenses)->first();
        $totalRevenue = $bookings->sum('grossRevenue');
        $totalExpense = $expenses->sum('amount');

        return [
            [
                'type' => 'Risk',
                'category' => 'Capacity risk',
                'priority' => 'High',
                'signal' => $lowPackages->count().' package presets have limited capacity.',
                'finding' => $lowPackages->first()['packageName'] ?? 'Capacity needs review',
                'reason' => 'Package slots are limited while booking records continue to grow.',
                'meaning' => 'The business may lose demand if promotions continue without checking capacity.',
                'action' => 'Review package capacity before approving more promotions.',
                'evidence' => $lowPackages->map(fn (array $item) => $item['packageName'].': '.$item['availableSlots'].' slots')->take(3)->join(', '),
                'horizon' => 'Next 7 to 14 days',
            ],
            [
                'type' => 'Warning',
                'category' => 'Cost risk',
                'priority' => 'Medium',
                'signal' => 'Recorded costs are '.self::money($totalExpense).' against '.self::money($totalRevenue).' revenue.',
                'finding' => ($costliest['category'] ?? 'Operating costs').' is the largest cost driver.',
                'reason' => 'Spending should be checked against confirmed booking conversion.',
                'meaning' => 'Profit may narrow even if gross sales look healthy.',
                'action' => 'Compare campaign and operating costs with booking conversion.',
                'evidence' => ($costliest['category'] ?? 'Costs').': '.self::money($costliest['amount'] ?? 0),
                'horizon' => 'This month',
            ],
            [
                'type' => 'Opportunity',
                'category' => 'Demand increase',
                'priority' => 'Medium',
                'signal' => ($topDestination['destination'] ?? 'Top route').' is leading current destination revenue.',
                'finding' => ($topDestination['destination'] ?? 'Top route').' has strong passenger volume.',
                'reason' => 'Recent booking records show the strongest revenue and passenger concentration here.',
                'meaning' => 'This route may support a controlled promo or package push.',
                'action' => 'Prepare package capacity and review pricing for next week.',
                'evidence' => (string) ($topDestination['passengers'] ?? 0).' passengers, '.self::money($topDestination['revenue'] ?? 0).' revenue.',
                'horizon' => 'Next week',
            ],
            [
                'type' => 'Trend',
                'category' => 'Sales trend',
                'priority' => 'Low',
                'signal' => 'Database-backed revenue is now available for review.',
                'finding' => 'Revenue and cost records are connected.',
                'reason' => 'Bookings and expenses can now be compared from saved records.',
                'meaning' => 'The business can grow if costs and capacity stay controlled.',
                'action' => 'Monitor operating costs to protect estimated profit.',
                'evidence' => self::money(max($totalRevenue - $totalExpense, 0)).' estimated profit from current records.',
                'horizon' => 'Current period',
            ],
        ];
    }

    public static function reportCards(): array
    {
        $bookings = collect(self::bookings());
        $packages = collect(self::packages());
        $expenses = collect(self::expenses());
        $insights = collect(self::decisionReviewSignals());
        $topDestination = self::destinationRevenue($bookings)->first();
        $costliest = self::expensesByCategory($expenses)->first();

        return [
            [
                'title' => 'Sales Summary',
                'description' => 'Revenue, bookings, passenger count, and top destinations.',
                'status' => 'View ready',
                'metrics' => [
                    ['label' => 'Revenue', 'value' => self::money($bookings->sum('grossRevenue'))],
                    ['label' => 'Bookings', 'value' => (string) $bookings->count()],
                    ['label' => 'Top route', 'value' => $topDestination['destination'] ?? 'None'],
                ],
                'sections' => ['Revenue', 'Bookings', 'Passengers', 'Top destinations'],
            ],
            [
                'title' => 'Package Catalog Summary',
                'description' => 'Package presets, available slots, reserved slots, and capacity warnings.',
                'status' => 'View ready',
                'metrics' => [
                    ['label' => 'Packages', 'value' => (string) $packages->count()],
                    ['label' => 'Low/Critical', 'value' => (string) $packages->whereIn('status', ['Low', 'Critical'])->count()],
                    ['label' => 'Reserved', 'value' => (string) $packages->sum('reservedCount')],
                ],
                'sections' => ['Package presets', 'Available slots', 'Reserved slots', 'Capacity warnings'],
            ],
            [
                'title' => 'Expense Summary',
                'description' => 'Operational, marketing, seasonal, and overhead cost review.',
                'status' => 'View ready',
                'metrics' => [
                    ['label' => 'Total costs', 'value' => self::money($expenses->sum('amount'))],
                    ['label' => 'Driver', 'value' => $costliest['category'] ?? 'None'],
                    ['label' => 'Pending', 'value' => (string) $expenses->where('paymentStatus', 'Pending')->count()],
                ],
                'sections' => ['Cost categories', 'Payment status', 'Profit watch'],
            ],
            [
                'title' => 'Package Decision Summary',
                'description' => 'Package guide readiness and criteria details from saved records.',
                'status' => 'View ready',
                'metrics' => [
                    ['label' => 'Criteria', 'value' => '7'],
                    ['label' => 'Direction', 'value' => 'Guided review'],
                    ['label' => 'Packages', 'value' => (string) $packages->count()],
                ],
                'sections' => ['Criteria weights', 'Compared options', 'Decision explanation'],
            ],
            [
                'title' => 'Decision Support Summary',
                'description' => 'Action readiness, access coverage, and known limits.',
                'status' => 'Ready',
                'metrics' => [
                    ['label' => 'Signals', 'value' => (string) $insights->count()],
                    ['label' => 'High priority', 'value' => (string) $insights->where('priority', 'High')->count()],
                    ['label' => 'Status', 'value' => 'Ready'],
                ],
                'sections' => ['Action readiness', 'Access coverage', 'Known limits'],
            ],
        ];
    }

    public static function destinationRevenue(Collection $bookings): Collection
    {
        return $bookings
            ->groupBy('destination')
            ->map(fn (Collection $rows, string $destination) => [
                'destination' => $destination,
                'revenue' => $rows->sum('grossRevenue'),
                'passengers' => $rows->sum('y'),
                'bookings' => $rows->count(),
            ])
            ->sortByDesc('revenue')
            ->values();
    }

    public static function expensesByCategory(Collection $expenses): Collection
    {
        return $expenses
            ->groupBy('category')
            ->map(fn (Collection $rows, string $category) => [
                'category' => $category,
                'amount' => $rows->sum('amount'),
            ])
            ->sortByDesc('amount')
            ->values();
    }

    private static function money(int|float $value): string
    {
        return 'PHP '.number_format((float) $value, 0);
    }
}
