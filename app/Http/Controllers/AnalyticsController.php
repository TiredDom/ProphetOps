<?php

namespace App\Http\Controllers;

use App\Support\ProphetOpsData;
use Inertia\Inertia;
use Inertia\Response;

class AnalyticsController extends Controller
{
    public function __invoke(): Response
    {
        return Inertia::render('SalesAnalytics', [
            'bookings' => ProphetOpsData::bookings(),
            'expenses' => ProphetOpsData::expenses(),
        ]);
    }
}
