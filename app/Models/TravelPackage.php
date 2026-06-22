<?php

namespace App\Models;

use Illuminate\Database\Eloquent\Factories\HasFactory;
use Illuminate\Database\Eloquent\Model;
use Illuminate\Database\Eloquent\Relations\HasMany;

class TravelPackage extends Model
{
    use HasFactory;

    protected $fillable = [
        'code',
        'package_name',
        'destination',
        'duration',
        'base_price',
        'inclusions',
        'travel_type',
        'supplier_reliability_score',
        'business_value_score',
        'risk_score',
        'available_slots',
        'sold_count',
        'reserved_count',
        'status',
        'last_updated_at',
    ];

    protected function casts(): array
    {
        return [
            'base_price' => 'integer',
            'supplier_reliability_score' => 'integer',
            'business_value_score' => 'integer',
            'risk_score' => 'integer',
            'available_slots' => 'integer',
            'sold_count' => 'integer',
            'reserved_count' => 'integer',
            'last_updated_at' => 'date',
        ];
    }

    public function bookings(): HasMany
    {
        return $this->hasMany(Booking::class);
    }
}
