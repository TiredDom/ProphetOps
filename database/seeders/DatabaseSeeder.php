<?php

namespace Database\Seeders;

use App\Models\Booking;
use App\Models\Expense;
use App\Models\TravelPackage;
use App\Models\User;
use App\Support\ProphetOpsData;
use Illuminate\Database\Console\Seeds\WithoutModelEvents;
use Illuminate\Database\Seeder;

class DatabaseSeeder extends Seeder
{
    use WithoutModelEvents;

    /**
     * Seed the application's database.
     */
    public function run(): void
    {
        foreach (ProphetOpsData::seededAccounts() as $account) {
            User::query()->updateOrCreate(
                ['email' => $account['email']],
                [
                    'name' => $account['name'],
                    'role' => $account['role'],
                    'status' => $account['status'],
                    'password' => $account['password'],
                ],
            );
        }

        $packages = [
            ['code' => 'PKG-101', 'package_name' => 'Boracay Group Package', 'destination' => 'Boracay', 'duration' => '3D2N', 'base_price' => 12000, 'inclusions' => 'Hotel, island hopping, local transfers, coordinator', 'travel_type' => 'Leisure', 'supplier_reliability_score' => 88, 'business_value_score' => 86, 'risk_score' => 35, 'available_slots' => 4, 'sold_count' => 34, 'reserved_count' => 8, 'status' => 'Low', 'last_updated_at' => '2026-06-07'],
            ['code' => 'PKG-102', 'package_name' => 'Baguio Educational Tour', 'destination' => 'Baguio', 'duration' => '2D1N', 'base_price' => 11000, 'inclusions' => 'Bus transfer, museum itinerary, meals, tour guide', 'travel_type' => 'Educational', 'supplier_reliability_score' => 92, 'business_value_score' => 84, 'risk_score' => 18, 'available_slots' => 18, 'sold_count' => 42, 'reserved_count' => 5, 'status' => 'Normal', 'last_updated_at' => '2026-06-06'],
            ['code' => 'PKG-103', 'package_name' => 'Palawan Weekend Package', 'destination' => 'Palawan', 'duration' => '3D2N', 'base_price' => 19000, 'inclusions' => 'Hotel, island tour, airport transfer, coordinator', 'travel_type' => 'Leisure', 'supplier_reliability_score' => 74, 'business_value_score' => 82, 'risk_score' => 48, 'available_slots' => 3, 'sold_count' => 27, 'reserved_count' => 6, 'status' => 'Critical', 'last_updated_at' => '2026-06-07'],
            ['code' => 'PKG-104', 'package_name' => 'Cebu Heritage Tour', 'destination' => 'Cebu', 'duration' => '2D1N', 'base_price' => 11000, 'inclusions' => 'Heritage route, guide, local transfers, meals', 'travel_type' => 'Cultural', 'supplier_reliability_score' => 90, 'business_value_score' => 80, 'risk_score' => 22, 'available_slots' => 14, 'sold_count' => 30, 'reserved_count' => 4, 'status' => 'Normal', 'last_updated_at' => '2026-06-05'],
            ['code' => 'PKG-105', 'package_name' => 'Bohol Countryside Tour', 'destination' => 'Bohol', 'duration' => '2D1N', 'base_price' => 12000, 'inclusions' => 'Countryside route, ferry coordination, guide, meals', 'travel_type' => 'Cultural', 'supplier_reliability_score' => 86, 'business_value_score' => 78, 'risk_score' => 24, 'available_slots' => 9, 'sold_count' => 21, 'reserved_count' => 3, 'status' => 'Normal', 'last_updated_at' => '2026-06-04'],
            ['code' => 'PKG-106', 'package_name' => 'Siargao Surf Package', 'destination' => 'Siargao', 'duration' => '4D3N', 'base_price' => 16000, 'inclusions' => 'Surf lesson, hotel, island transfers, coordinator', 'travel_type' => 'Adventure', 'supplier_reliability_score' => 78, 'business_value_score' => 76, 'risk_score' => 38, 'available_slots' => 5, 'sold_count' => 19, 'reserved_count' => 7, 'status' => 'Low', 'last_updated_at' => '2026-06-07'],
        ];

        foreach ($packages as $package) {
            TravelPackage::query()->updateOrCreate(['code' => $package['code']], $package);
        }

        $bookings = [
            ['code' => 'BKG-2401', 'booking_date' => '2026-06-01', 'passenger_count' => 18, 'client' => 'Northstar Travel Services', 'package_code' => 'PKG-101', 'entry_type' => 'Package preset', 'package_name' => 'Boracay Group Package', 'destination' => 'Boracay', 'gross_revenue' => 216000, 'payment_status' => 'Partially Paid', 'booking_status' => 'Confirmed', 'staff_assigned' => 'Staff User', 'source' => 'Messenger', 'notes' => 'Group booking for July company outing.'],
            ['code' => 'BKG-2402', 'booking_date' => '2026-06-02', 'passenger_count' => 12, 'client' => 'Apex Corporate Tours', 'package_code' => 'PKG-102', 'entry_type' => 'Package preset', 'package_name' => 'Baguio Educational Tour', 'destination' => 'Baguio', 'gross_revenue' => 132000, 'payment_status' => 'Paid', 'booking_status' => 'Confirmed', 'staff_assigned' => 'Admin User', 'source' => 'Google Sheets', 'notes' => 'School partner requested final itinerary.'],
            ['code' => 'BKG-2403', 'booking_date' => '2026-06-03', 'passenger_count' => 9, 'client' => 'MetroLink Agency', 'package_code' => 'PKG-103', 'entry_type' => 'Manual quotation', 'package_name' => 'Palawan Weekend Package', 'destination' => 'Palawan', 'gross_revenue' => 171000, 'payment_status' => 'Pending', 'booking_status' => 'Pending', 'staff_assigned' => 'Staff User', 'source' => 'Gmail', 'notes' => 'Awaiting deposit confirmation.'],
            ['code' => 'BKG-2404', 'booking_date' => '2026-06-04', 'passenger_count' => 22, 'client' => 'Summit Incentive Travel', 'package_code' => 'PKG-104', 'entry_type' => 'Package preset', 'package_name' => 'Cebu Heritage Tour', 'destination' => 'Cebu', 'gross_revenue' => 242000, 'payment_status' => 'Paid', 'booking_status' => 'Confirmed', 'staff_assigned' => 'Admin User', 'source' => 'Notebook', 'notes' => 'High passenger count. Check guide allocation.'],
            ['code' => 'BKG-2405', 'booking_date' => '2026-06-05', 'passenger_count' => 7, 'client' => 'Harbor Trade Group', 'package_code' => 'PKG-105', 'entry_type' => 'Package preset', 'package_name' => 'Bohol Countryside Tour', 'destination' => 'Bohol', 'gross_revenue' => 87500, 'payment_status' => 'Partially Paid', 'booking_status' => 'Reserved', 'staff_assigned' => 'Staff User', 'source' => 'Messenger', 'notes' => 'Client may add more passengers next week.'],
            ['code' => 'BKG-2406', 'booking_date' => '2026-06-06', 'passenger_count' => 16, 'client' => 'Pacific Link Partners', 'package_code' => 'PKG-101', 'entry_type' => 'Package preset', 'package_name' => 'Boracay Group Package', 'destination' => 'Boracay', 'gross_revenue' => 192000, 'payment_status' => 'Paid', 'booking_status' => 'Confirmed', 'staff_assigned' => 'Admin User', 'source' => 'Google Sheets', 'notes' => 'Repeat partner booking.'],
            ['code' => 'BKG-2407', 'booking_date' => '2026-06-07', 'passenger_count' => 6, 'client' => 'Eastway Agency', 'package_code' => 'PKG-106', 'entry_type' => 'Manual quotation', 'package_name' => 'Siargao Surf Package', 'destination' => 'Siargao', 'gross_revenue' => 96000, 'payment_status' => 'Pending', 'booking_status' => 'Pending', 'staff_assigned' => 'Staff User', 'source' => 'Gmail', 'notes' => 'Needs flight availability review.'],
            ['code' => 'BKG-2408', 'booking_date' => '2026-06-08', 'passenger_count' => 14, 'client' => 'Bluewave Corporate Travel', 'package_code' => 'PKG-105', 'entry_type' => 'Package preset', 'package_name' => 'Bohol Countryside Tour', 'destination' => 'Bohol', 'gross_revenue' => 168000, 'payment_status' => 'Paid', 'booking_status' => 'Confirmed', 'staff_assigned' => 'Staff User', 'source' => 'Messenger', 'notes' => 'Company outing confirmed for late June.'],
            ['code' => 'BKG-2409', 'booking_date' => '2026-06-09', 'passenger_count' => 17, 'client' => 'IslandBridge Tours', 'package_code' => 'PKG-101', 'entry_type' => 'Package preset', 'package_name' => 'Boracay Group Package', 'destination' => 'Boracay', 'gross_revenue' => 205000, 'payment_status' => 'Partially Paid', 'booking_status' => 'Reserved', 'staff_assigned' => 'Admin User', 'source' => 'Google Sheets', 'notes' => 'High-interest group; check remaining package capacity.'],
        ];

        foreach ($bookings as $booking) {
            $package = TravelPackage::query()->where('code', $booking['package_code'])->first();
            Booking::query()->updateOrCreate(
                ['code' => $booking['code']],
                [...$booking, 'travel_package_id' => $package?->id],
            );
        }

        $expenses = [
            ['code' => 'EXP-3101', 'expense_date' => '2026-06-01', 'category' => 'Tour operations', 'amount' => 74000, 'related_package' => 'Boracay Group Package', 'payment_status' => 'Paid', 'notes' => 'Hotel coordination and local transfers.'],
            ['code' => 'EXP-3102', 'expense_date' => '2026-06-02', 'category' => 'Marketing', 'amount' => 38500, 'related_package' => 'Multiple destinations', 'payment_status' => 'Paid', 'notes' => 'June partner campaign placements.'],
            ['code' => 'EXP-3103', 'expense_date' => '2026-06-03', 'category' => 'Seasonal cost', 'amount' => 52000, 'related_package' => 'Palawan Weekend Package', 'payment_status' => 'Pending', 'notes' => 'Peak-period supplier surcharge.'],
            ['code' => 'EXP-3104', 'expense_date' => '2026-06-04', 'category' => 'Overhead', 'amount' => 21000, 'related_package' => 'Office operations', 'payment_status' => 'Paid', 'notes' => 'Office utilities and admin support.'],
            ['code' => 'EXP-3105', 'expense_date' => '2026-06-05', 'category' => 'Tour operations', 'amount' => 63000, 'related_package' => 'Cebu Heritage Tour', 'payment_status' => 'Paid', 'notes' => 'Guide fees and destination coordination.'],
        ];

        foreach ($expenses as $expense) {
            Expense::query()->updateOrCreate(['code' => $expense['code']], $expense);
        }
    }
}
