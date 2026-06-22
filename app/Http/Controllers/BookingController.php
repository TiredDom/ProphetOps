<?php

namespace App\Http\Controllers;

use App\Models\Booking;
use App\Models\TravelPackage;
use App\Support\ProphetOpsData;
use Illuminate\Http\RedirectResponse;
use Illuminate\Http\Request;
use Illuminate\Validation\Rule;
use Inertia\Inertia;
use Inertia\Response;

class BookingController extends Controller
{
    public function index(): Response
    {
        return Inertia::render('Bookings', [
            'bookings' => ProphetOpsData::bookings(),
            'packages' => ProphetOpsData::packages(),
        ]);
    }

    public function store(Request $request): RedirectResponse
    {
        $data = $this->validated($request);
        $booking = Booking::query()->create($this->payload($data));

        return back()->with('notice', [
            'tone' => 'success',
            'title' => 'Booking added',
            'message' => $booking->code.' was saved to the database.',
        ]);
    }

    public function update(Request $request, Booking $booking): RedirectResponse
    {
        $data = $this->validated($request, $booking);
        $booking->update($this->payload($data));

        return back()->with('notice', [
            'tone' => 'success',
            'title' => 'Booking updated',
            'message' => $booking->code.' was saved to the database.',
        ]);
    }

    public function bulk(Request $request): RedirectResponse
    {
        $data = $request->validate([
            'ids' => ['required', 'array', 'min:1'],
            'ids.*' => ['string', 'exists:bookings,code'],
            'action' => ['required', Rule::in(['confirm', 'paid'])],
        ]);

        $updates = $data['action'] === 'confirm'
            ? ['booking_status' => 'Confirmed']
            : ['payment_status' => 'Paid'];

        Booking::query()->whereIn('code', $data['ids'])->update($updates);

        return back()->with('notice', [
            'tone' => 'success',
            'title' => count($data['ids']).' booking'.(count($data['ids']) === 1 ? '' : 's').' updated',
            'message' => $data['action'] === 'confirm'
                ? 'Booking status changed to Confirmed.'
                : 'Payment status changed to Paid.',
        ]);
    }

    private function validated(Request $request, ?Booking $booking = null): array
    {
        return $request->validate([
            'id' => ['required', 'string', 'max:40', Rule::unique('bookings', 'code')->ignore($booking?->id)],
            'ds' => ['required', 'date'],
            'y' => ['required', 'integer', 'min:1'],
            'client' => ['required', 'string', 'max:120'],
            'packageId' => ['nullable', 'string', 'max:40'],
            'entryType' => ['required', Rule::in(['Package preset', 'Manual quotation'])],
            'package' => ['required', 'string', 'max:120'],
            'destination' => ['required', 'string', 'max:120'],
            'grossRevenue' => ['required', 'integer', 'min:0'],
            'paymentStatus' => ['required', Rule::in(['Paid', 'Partially Paid', 'Pending'])],
            'bookingStatus' => ['required', Rule::in(['Confirmed', 'Reserved', 'Pending'])],
            'staffAssigned' => ['nullable', 'string', 'max:80'],
            'source' => ['nullable', 'string', 'max:80'],
            'notes' => ['nullable', 'string', 'max:320'],
        ]);
    }

    private function payload(array $data): array
    {
        $package = TravelPackage::query()->where('code', $data['packageId'] ?? null)->first();

        return [
            'code' => $data['id'],
            'booking_date' => $data['ds'],
            'passenger_count' => $data['y'],
            'client' => $data['client'],
            'travel_package_id' => $package?->id,
            'package_name' => $data['package'],
            'package_code' => $data['packageId'] ?? null,
            'entry_type' => $data['entryType'],
            'destination' => $data['destination'],
            'gross_revenue' => $data['grossRevenue'],
            'payment_status' => $data['paymentStatus'],
            'booking_status' => $data['bookingStatus'],
            'staff_assigned' => $data['staffAssigned'] ?? null,
            'source' => $data['source'] ?? 'Manual quotation',
            'notes' => $data['notes'] ?? null,
        ];
    }
}
