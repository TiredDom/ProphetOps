<?php

namespace Tests\Feature;

use App\Models\TravelPackage;
use App\Support\TopsisDecisionSupport;
use Illuminate\Foundation\Testing\RefreshDatabase;
use InvalidArgumentException;
use Tests\TestCase;

class TopsisDecisionSupportTest extends TestCase
{
    use RefreshDatabase;

    public function test_topsis_ranks_best_matching_package_first(): void
    {
        $this->package([
            'code' => 'PKG-A',
            'package_name' => 'Boracay Group Package',
            'destination' => 'Boracay',
            'duration' => '3D2N',
            'base_price' => 16000,
            'travel_type' => 'Leisure',
            'available_slots' => 4,
            'supplier_reliability_score' => 75,
            'business_value_score' => 80,
        ]);
        $this->package([
            'code' => 'PKG-B',
            'package_name' => 'Bohol Countryside Tour',
            'destination' => 'Bohol',
            'duration' => '2D1N',
            'base_price' => 12000,
            'travel_type' => 'Cultural',
            'available_slots' => 18,
            'supplier_reliability_score' => 90,
            'business_value_score' => 86,
        ]);
        $this->package([
            'code' => 'PKG-C',
            'package_name' => 'Palawan Weekend Package',
            'destination' => 'Palawan',
            'duration' => '3D2N',
            'base_price' => 19000,
            'travel_type' => 'Leisure',
            'available_slots' => 3,
            'supplier_reliability_score' => 72,
            'business_value_score' => 82,
        ]);

        $ranking = TopsisDecisionSupport::rankPackages(TravelPackage::query()->orderBy('code')->get(), [
            'budget' => 12000,
            'destination' => 'Bohol',
            'duration' => '2D1N',
            'travelType' => 'Cultural',
        ]);

        $this->assertSame('PKG-B', $ranking['best']['id']);
        $this->assertSame(1, $ranking['best']['rank']);
        $this->assertNotEmpty($ranking['best']['explanation']);
    }

    public function test_topsis_returns_clear_empty_state(): void
    {
        $ranking = TopsisDecisionSupport::rankPackages(collect());

        $this->assertSame([], $ranking['results']);
        $this->assertNull($ranking['best']);
        $this->assertSame('Add package presets before comparing options.', $ranking['warning']);
    }

    public function test_topsis_rejects_invalid_weight_total(): void
    {
        $this->expectException(InvalidArgumentException::class);

        TopsisDecisionSupport::rankPackages(collect(), [
            'budget' => 12000,
        ], [
            'budgetFit' => 0.90,
            'destinationMatch' => 0.20,
            'availableSlots' => 0.15,
            'supplierReliability' => 0.15,
            'durationFit' => 0.10,
            'travelTypeMatch' => 0.10,
            'businessValue' => 0.10,
        ]);
    }

    public function test_topsis_tied_scores_use_stable_package_name_order(): void
    {
        $this->package([
            'code' => 'PKG-Z',
            'package_name' => 'Zeta Package',
            'destination' => 'Cebu',
        ]);
        $this->package([
            'code' => 'PKG-A',
            'package_name' => 'Alpha Package',
            'destination' => 'Cebu',
        ]);

        $ranking = TopsisDecisionSupport::rankPackages(TravelPackage::query()->orderBy('code')->get(), [
            'budget' => 12000,
            'destination' => 'Cebu',
            'duration' => '3D2N',
            'travelType' => 'Leisure',
        ]);

        $this->assertSame($ranking['results'][0]['score'], $ranking['results'][1]['score']);
        $this->assertSame('Alpha Package', $ranking['results'][0]['packageName']);
    }

    private function package(array $overrides = []): TravelPackage
    {
        return TravelPackage::query()->create([
            'code' => $overrides['code'] ?? 'PKG-TEST',
            'package_name' => $overrides['package_name'] ?? 'Test Package',
            'destination' => $overrides['destination'] ?? 'Cebu',
            'duration' => $overrides['duration'] ?? '3D2N',
            'base_price' => $overrides['base_price'] ?? 12000,
            'inclusions' => $overrides['inclusions'] ?? 'Hotel, transfers, guide',
            'travel_type' => $overrides['travel_type'] ?? 'Leisure',
            'supplier_reliability_score' => $overrides['supplier_reliability_score'] ?? 85,
            'business_value_score' => $overrides['business_value_score'] ?? 80,
            'risk_score' => $overrides['risk_score'] ?? 20,
            'available_slots' => $overrides['available_slots'] ?? 10,
            'sold_count' => $overrides['sold_count'] ?? 0,
            'reserved_count' => $overrides['reserved_count'] ?? 0,
            'status' => $overrides['status'] ?? 'Normal',
            'last_updated_at' => $overrides['last_updated_at'] ?? '2026-06-19',
        ]);
    }
}
