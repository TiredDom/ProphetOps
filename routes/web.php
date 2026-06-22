<?php

use App\Http\Controllers\AnalyticsController;
use App\Http\Controllers\AuthController;
use App\Http\Controllers\BookingController;
use App\Http\Controllers\DashboardController;
use App\Http\Controllers\ExpenseController;
use App\Http\Controllers\ForecastingController;
use App\Http\Controllers\InventoryController;
use App\Http\Controllers\ReportController;
use App\Http\Controllers\UserAccessController;
use Illuminate\Http\Request;
use Illuminate\Support\Facades\Route;

Route::get('/', fn () => redirect('/login'));

Route::get('/login', [AuthController::class, 'create'])->name('login');
Route::post('/login', [AuthController::class, 'store'])->middleware('throttle:5,1');

Route::post('/logout', [AuthController::class, 'destroy'])->middleware('auth');

Route::middleware('auth')->group(function (): void {
    Route::get('/dashboard', DashboardController::class)->middleware('role.access:Dashboard');

    Route::get('/bookings', [BookingController::class, 'index'])->middleware('role.access:Bookings');
    Route::post('/bookings', [BookingController::class, 'store'])->middleware('role.access:Bookings');
    Route::put('/bookings/{booking:code}', [BookingController::class, 'update'])->middleware('role.access:Bookings');
    Route::patch('/bookings/bulk', [BookingController::class, 'bulk'])->middleware('role.access:Bookings');

    Route::get('/inventory', [InventoryController::class, 'index'])->middleware('role.access:Package Catalog');
    Route::post('/inventory', [InventoryController::class, 'store'])->middleware('role.access:Package Catalog');
    Route::put('/inventory/{package:code}', [InventoryController::class, 'update'])->middleware('role.access:Package Catalog');
    Route::patch('/inventory/bulk', [InventoryController::class, 'bulk'])->middleware('role.access:Package Catalog');

    Route::get('/expenses', [ExpenseController::class, 'index'])->middleware('role.access:Expenses');
    Route::post('/expenses', [ExpenseController::class, 'store'])->middleware('role.access:Expenses');
    Route::put('/expenses/{expense:code}', [ExpenseController::class, 'update'])->middleware('role.access:Expenses');
    Route::patch('/expenses/bulk', [ExpenseController::class, 'bulk'])->middleware('role.access:Expenses');

    Route::get('/analytics', AnalyticsController::class)->middleware('role.access:Analytics');
    Route::get('/decision-guide', ForecastingController::class)->middleware('role.access:Package Decision Guide');
    Route::get('/forecasting', function (Request $request) {
        $query = $request->getQueryString();

        return redirect('/decision-guide'.($query ? '?'.$query : ''));
    });
    Route::get('/reports', ReportController::class)->middleware('role.access:Reports');
    Route::get('/users', UserAccessController::class)->middleware('role.access:Users');
});
