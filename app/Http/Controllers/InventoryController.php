<?php

namespace App\Http\Controllers;

use App\Models\TravelPackage;
use App\Support\ProphetOpsData;
use Illuminate\Http\RedirectResponse;
use Illuminate\Http\Request;
use Illuminate\Validation\Rule;
use Inertia\Inertia;
use Inertia\Response;

class InventoryController extends Controller
{
    public function index(): Response
    {
        return Inertia::render('Inventory', [
            'packages' => ProphetOpsData::packages(),
        ]);
    }

    public function store(Request $request): RedirectResponse
    {
        $data = $this->validated($request);
        $package = TravelPackage::query()->create($this->payload($data));

        return back()->with('notice', [
            'tone' => 'success',
            'title' => 'Package preset added',
            'message' => $package->code.' was saved to the database.',
        ]);
    }

    public function update(Request $request, TravelPackage $package): RedirectResponse
    {
        $data = $this->validated($request, $package);
        $package->update($this->payload($data));

        return back()->with('notice', [
            'tone' => 'success',
            'title' => 'Package preset updated',
            'message' => $package->code.' was saved to the database.',
        ]);
    }

    public function bulk(Request $request): RedirectResponse
    {
        $data = $request->validate([
            'ids' => ['required', 'array', 'min:1'],
            'ids.*' => ['string', 'exists:travel_packages,code'],
            'action' => ['required', Rule::in(['normal', 'low'])],
        ]);
        $status = $data['action'] === 'low' ? 'Low' : 'Normal';

        TravelPackage::query()->whereIn('code', $data['ids'])->update([
            'status' => $status,
            'last_updated_at' => now()->toDateString(),
        ]);

        return back()->with('notice', [
            'tone' => $status === 'Low' ? 'warning' : 'success',
            'title' => count($data['ids']).' preset'.(count($data['ids']) === 1 ? '' : 's').' updated',
            'message' => 'Capacity status changed to '.$status.'.',
        ]);
    }

    private function validated(Request $request, ?TravelPackage $package = null): array
    {
        return $request->validate([
            'id' => ['required', 'string', 'max:40', Rule::unique('travel_packages', 'code')->ignore($package?->id)],
            'packageName' => ['required', 'string', 'max:120'],
            'destination' => ['required', 'string', 'max:120'],
            'duration' => ['nullable', 'string', 'max:80'],
            'basePrice' => ['required', 'integer', 'min:0'],
            'inclusions' => ['nullable', 'string', 'max:420'],
            'travelType' => ['required', 'string', 'max:80'],
            'supplierReliabilityScore' => ['required', 'integer', 'min:0', 'max:100'],
            'businessValueScore' => ['required', 'integer', 'min:0', 'max:100'],
            'riskScore' => ['required', 'integer', 'min:0', 'max:100'],
            'availableSlots' => ['required', 'integer', 'min:0'],
            'soldCount' => ['nullable', 'integer', 'min:0'],
            'reservedCount' => ['nullable', 'integer', 'min:0'],
            'status' => ['required', Rule::in(['Normal', 'Low', 'Critical'])],
        ]);
    }

    private function payload(array $data): array
    {
        return [
            'code' => $data['id'],
            'package_name' => $data['packageName'],
            'destination' => $data['destination'],
            'duration' => $data['duration'] ?? null,
            'base_price' => $data['basePrice'],
            'inclusions' => $data['inclusions'] ?? null,
            'travel_type' => $data['travelType'],
            'supplier_reliability_score' => $data['supplierReliabilityScore'],
            'business_value_score' => $data['businessValueScore'],
            'risk_score' => $data['riskScore'],
            'available_slots' => $data['availableSlots'],
            'sold_count' => $data['soldCount'] ?? 0,
            'reserved_count' => $data['reservedCount'] ?? 0,
            'status' => $data['status'],
            'last_updated_at' => now()->toDateString(),
        ];
    }
}
