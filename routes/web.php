<?php

use Illuminate\Support\Facades\Route;
use Inertia\Inertia;

Route::get('/', function () {
    return Inertia::render('Login');
});

Route::get('/login', function () {
    return Inertia::render('Login');
});

Route::get('/dashboard', function () {
    return Inertia::render('Welcome');
});

Route::get('/data/operational-records', function () {
    return redirect('/bookings');
});

Route::get('/data/validation', function () {
    return redirect('/analytics');
});

Route::get('/operations/inventory', function () {
    return redirect('/inventory');
});

Route::get('/bookings', function () {
    return Inertia::render('Bookings');
});

Route::get('/inventory', function () {
    return Inertia::render('Inventory');
});

Route::get('/expenses', function () {
    return Inertia::render('Expenses');
});

Route::get('/analytics', function () {
    return Inertia::render('SalesAnalytics');
});

Route::get('/forecasting', function () {
    return Inertia::render('ForecastingPreview');
});

Route::get('/trajectory-insights', function () {
    return Inertia::render('TrajectoryInsights');
});

Route::get('/reports', function () {
    return Inertia::render('Reports');
});

Route::get('/users', function () {
    return Inertia::render('Users');
});
