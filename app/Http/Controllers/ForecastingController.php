<?php

namespace App\Http\Controllers;

use App\Models\TravelPackage;
use App\Support\ProphetOpsData;
use App\Support\TopsisDecisionSupport;
use Illuminate\Http\Request;
use Inertia\Inertia;
use Inertia\Response;

class ForecastingController extends Controller
{
    public function __invoke(Request $request): Response
    {
        $preferences = $request->validate([
            'budget' => ['nullable', 'integer', 'min:0', 'max:500000'],
            'destination' => ['nullable', 'string', 'max:120'],
            'duration' => ['nullable', 'string', 'max:80'],
            'travelType' => ['nullable', 'string', 'max:80'],
        ]);

        return Inertia::render('ForecastingPreview', [
            'topsis' => TopsisDecisionSupport::rankPackages(
                TravelPackage::query()->orderBy('code')->get(),
                $preferences,
            ),
        ]);
    }
}
