<?php

namespace Tests\Feature;

use App\Models\User;
use App\Models\TravelPackage;
use Illuminate\Foundation\Testing\RefreshDatabase;
use Illuminate\Support\Facades\Hash;
use Tests\TestCase;

class ExampleTest extends TestCase
{
    use RefreshDatabase;

    public function test_root_redirects_to_login(): void
    {
        $this->get('/')->assertRedirect('/login');
    }

    public function test_guest_internal_pages_redirect_to_login(): void
    {
        $this->get('/dashboard')->assertRedirect('/login');
    }

    public function test_security_headers_are_set(): void
    {
        $this->get('/login')
            ->assertHeader('X-Frame-Options', 'SAMEORIGIN')
            ->assertHeader('X-Content-Type-Options', 'nosniff')
            ->assertHeader('Referrer-Policy', 'strict-origin-when-cross-origin')
            ->assertHeader('Permissions-Policy', 'camera=(), microphone=(), geolocation=()')
            ->assertHeader('Content-Security-Policy')
            ->assertHeader('Cache-Control', 'must-revalidate, no-cache, no-store, private');
    }

    public function test_security_policy_stays_strict_for_built_assets(): void
    {
        $contentSecurityPolicy = $this->get('/login')->headers->get('Content-Security-Policy');

        $this->assertStringContainsString("script-src 'self'", $contentSecurityPolicy);
        $this->assertStringContainsString("style-src 'self'", $contentSecurityPolicy);
        $this->assertStringNotContainsString("'unsafe-inline'", $contentSecurityPolicy);
        $this->assertStringNotContainsString("'unsafe-eval'", $contentSecurityPolicy);
        $this->assertStringNotContainsString('fonts.googleapis.com', $contentSecurityPolicy);
        $this->assertStringNotContainsString('fonts.gstatic.com', $contentSecurityPolicy);
        $this->assertStringNotContainsString('http://127.0.0.1:5173', $contentSecurityPolicy);
    }

    public function test_local_security_policy_allows_configured_vite_dev_assets_when_hot_file_exists(): void
    {
        file_put_contents(public_path('hot'), 'http://127.0.0.1:5173');

        try {
            $contentSecurityPolicy = $this->get('/login')->headers->get('Content-Security-Policy');
        } finally {
            @unlink(public_path('hot'));
        }

        $this->assertStringContainsString('http://127.0.0.1:5173', $contentSecurityPolicy);
        $this->assertStringContainsString('ws://127.0.0.1:5173', $contentSecurityPolicy);
        $this->assertStringContainsString("'unsafe-eval'", $contentSecurityPolicy);
    }

    public function test_owner_can_open_dashboard(): void
    {
        $user = User::factory()->create([
            'role' => 'Owner / Management',
            'status' => 'Active',
        ]);

        $this->actingAs($user)->get('/dashboard')->assertOk();
    }

    public function test_demo_login_creates_backend_session(): void
    {
        User::factory()->create([
            'email' => 'owner@prophetops.local',
            'password' => Hash::make('owner123'),
            'role' => 'Owner / Management',
            'status' => 'Active',
        ]);

        $this->post('/login', [
            'email' => 'owner@prophetops.local',
            'password' => 'owner123',
        ])->assertRedirect('/dashboard');

        $this->assertAuthenticated();
    }

    public function test_staff_cannot_open_expenses(): void
    {
        $user = User::factory()->create([
            'role' => 'Staff',
            'status' => 'Active',
        ]);

        $this->actingAs($user)->get('/expenses')->assertRedirect('/bookings');
    }

    public function test_owner_can_open_package_decision_guide(): void
    {
        $user = User::factory()->create([
            'role' => 'Owner / Management',
            'status' => 'Active',
        ]);

        $this->actingAs($user)->get('/decision-guide')->assertOk();
    }

    public function test_old_forecasting_link_redirects_to_package_decision_guide(): void
    {
        $user = User::factory()->create([
            'role' => 'Owner / Management',
            'status' => 'Active',
        ]);

        $this->actingAs($user)
            ->get('/forecasting?duration=3D2N')
            ->assertRedirect('/decision-guide?duration=3D2N');
    }

    public function test_staff_cannot_open_package_decision_guide(): void
    {
        $user = User::factory()->create([
            'role' => 'Staff',
            'status' => 'Active',
        ]);

        $this->actingAs($user)->get('/decision-guide')->assertRedirect('/bookings');
    }

    public function test_booking_can_be_saved_to_database(): void
    {
        $user = User::factory()->create([
            'role' => 'Owner / Management',
            'status' => 'Active',
        ]);
        TravelPackage::query()->create([
            'code' => 'PKG-999',
            'package_name' => 'Test Package',
            'destination' => 'Bohol',
            'base_price' => 10000,
            'available_slots' => 10,
            'status' => 'Normal',
        ]);

        $this->actingAs($user)->post('/bookings', [
            'id' => 'BKG-9999',
            'ds' => '2026-06-17',
            'y' => 2,
            'client' => 'Assurance Travel Group',
            'packageId' => 'PKG-999',
            'entryType' => 'Package preset',
            'package' => 'Test Package',
            'destination' => 'Bohol',
            'grossRevenue' => 20000,
            'paymentStatus' => 'Pending',
            'bookingStatus' => 'Pending',
            'staffAssigned' => 'Staff User',
            'source' => 'Package preset',
            'notes' => 'Backend feature test record.',
        ])->assertSessionHasNoErrors();

        $this->assertDatabaseHas('bookings', [
            'code' => 'BKG-9999',
            'client' => 'Assurance Travel Group',
            'gross_revenue' => 20000,
        ]);
    }
}
