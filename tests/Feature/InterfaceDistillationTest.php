<?php

namespace Tests\Feature;

use Tests\TestCase;

class InterfaceDistillationTest extends TestCase
{
    private function projectFile(string $path): string
    {
        return file_get_contents(base_path($path));
    }

    public function test_dashboard_is_not_a_forecasting_or_transaction_table_page(): void
    {
        $dashboard = $this->projectFile('resources/js/Pages/Welcome.vue');
        $controller = $this->projectFile('app/Http/Controllers/DashboardController.php');

        $this->assertStringNotContainsString('<ChartPanel', $dashboard);
        $this->assertStringNotContainsString('<LineTrendChart', $dashboard);
        $this->assertStringNotContainsString('<ComparisonTrack', $dashboard);
        $this->assertStringNotContainsString('<DataTableFrame', $dashboard);
        $this->assertStringNotContainsString('<table', $dashboard);
        $this->assertStringNotContainsString('Revenue Outlook', $dashboard);
        $this->assertStringNotContainsString('Recent Transactions', $dashboard);
        $this->assertStringNotContainsString('businessTrajectoryRanges', $controller);
    }

    public function test_analytics_keeps_core_charts_without_duplicate_volume_or_table(): void
    {
        $analytics = $this->projectFile('resources/js/Pages/SalesAnalytics.vue');

        $this->assertStringContainsString('Revenue By Booking Date', $analytics);
        $this->assertStringContainsString('Revenue By Destination', $analytics);
        $this->assertStringContainsString('Revenue, Cost, and Margin', $analytics);
        $this->assertStringNotContainsString('Booking Volume', $analytics);
        $this->assertStringNotContainsString('Package Performance Table', $analytics);
        $this->assertStringNotContainsString('<DataTableFrame', $analytics);
    }

    public function test_distilled_pages_hide_secondary_table_and_action_clutter(): void
    {
        $bookings = $this->projectFile('resources/js/Pages/Bookings.vue');
        $inventory = $this->projectFile('resources/js/Pages/Inventory.vue');
        $expenses = $this->projectFile('resources/js/Pages/Expenses.vue');
        $guide = $this->projectFile('resources/js/Pages/ForecastingPreview.vue');
        $reports = $this->projectFile('resources/js/Pages/Reports.vue');
        $users = $this->projectFile('resources/js/Pages/Users.vue');

        $this->assertStringNotContainsString('<th>Date</th>', $bookings);
        $this->assertStringNotContainsString('<th>Staff</th>', $bookings);
        $this->assertStringNotContainsString('<th>Reliability</th>', $inventory);
        $this->assertStringNotContainsString('<th>Booked</th>', $inventory);
        $this->assertStringNotContainsString('<th>Reserved</th>', $inventory);
        $this->assertStringNotContainsString('<th>Last Updated</th>', $inventory);
        $this->assertStringNotContainsString("label: 'Cost Signal'", $expenses);
        $this->assertStringNotContainsString("label: 'Criteria'", $guide);
        $this->assertStringNotContainsString('Prepare PDF', $reports);
        $this->assertStringNotContainsString('Prepare Excel', $reports);
        $this->assertStringNotContainsString('<th>Last login</th>', $users);
    }
}
