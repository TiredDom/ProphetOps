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
    return Inertia::render('OperationalRecords');
});

Route::get('/data/validation', function () {
    return Inertia::render('DataValidation');
});

Route::get('/operations/inventory', function () {
    return Inertia::render('Inventory');
});
