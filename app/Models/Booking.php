<?php

namespace App\Models;

use Illuminate\Database\Eloquent\Factories\HasFactory;
use Illuminate\Database\Eloquent\Model;
use Illuminate\Database\Eloquent\Relations\BelongsTo;

class Booking extends Model
{
    use HasFactory;

    protected $fillable = [
        'code',
        'booking_date',
        'passenger_count',
        'client',
        'travel_package_id',
        'package_name',
        'package_code',
        'entry_type',
        'destination',
        'gross_revenue',
        'payment_status',
        'booking_status',
        'staff_assigned',
        'source',
        'notes',
    ];

    protected function casts(): array
    {
        return [
            'booking_date' => 'date',
            'passenger_count' => 'integer',
            'gross_revenue' => 'integer',
        ];
    }

    public function travelPackage(): BelongsTo
    {
        return $this->belongsTo(TravelPackage::class);
    }
}
