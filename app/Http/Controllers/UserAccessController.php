<?php

namespace App\Http\Controllers;

use App\Support\ProphetOpsData;
use Inertia\Inertia;
use Inertia\Response;

class UserAccessController extends Controller
{
    public function __invoke(): Response
    {
        return Inertia::render('Users', [
            'users' => ProphetOpsData::users(),
            'rolePermissions' => ProphetOpsData::ROLE_PERMISSIONS,
        ]);
    }
}
