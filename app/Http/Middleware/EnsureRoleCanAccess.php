<?php

namespace App\Http\Middleware;

use App\Support\ProphetOpsData;
use Closure;
use Illuminate\Http\Request;
use Symfony\Component\HttpFoundation\Response;

class EnsureRoleCanAccess
{
    public function handle(Request $request, Closure $next, string $label): Response
    {
        $user = $request->user();

        if ($user && ProphetOpsData::canAccess($user->role, $label)) {
            return $next($request);
        }

        return redirect(ProphetOpsData::defaultPathForRole($user?->role));
    }
}
