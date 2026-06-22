<?php

namespace App\Http\Controllers;

use App\Support\ProphetOpsData;
use Inertia\Inertia;
use Inertia\Response;

class ReportController extends Controller
{
    public function __invoke(): Response
    {
        return Inertia::render('Reports', [
            'reportCards' => ProphetOpsData::reportCards(),
        ]);
    }
}
