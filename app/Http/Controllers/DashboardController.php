<?php

namespace App\Http\Controllers;

use App\Support\ProphetOpsData;
use Inertia\Inertia;
use Inertia\Response;

class DashboardController extends Controller
{
    public function __invoke(): Response
    {
        return Inertia::render('Welcome', [
            'bookings' => ProphetOpsData::bookings(),
            'expenses' => ProphetOpsData::expenses(),
            'inventory' => ProphetOpsData::packages(),
            'decisionReviewSignals' => ProphetOpsData::decisionReviewSignals(),
            'lastUpdated' => now()->format('g:i A'),
        ]);
    }
}
