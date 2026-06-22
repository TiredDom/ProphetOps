<?php

namespace App\Http\Middleware;

use App\Support\ProphetOpsData;
use Illuminate\Http\Request;
use Inertia\Middleware;

class HandleInertiaRequests extends Middleware
{
    /**
     * The root template that's loaded on the first page visit.
     *
     * @see https://inertiajs.com/server-side-setup#root-template
     *
     * @var string
     */
    protected $rootView = 'app';

    /**
     * Determines the current asset version.
     *
     * @see https://inertiajs.com/asset-versioning
     */
    public function version(Request $request): ?string
    {
        return parent::version($request);
    }

    /**
     * Define the props that are shared by default.
     *
     * @see https://inertiajs.com/shared-data
     *
     * @return array<string, mixed>
     */
    public function share(Request $request): array
    {
        $user = $request->user();

        return [
            ...parent::share($request),
            'auth' => [
                'user' => $user ? [
                    'name' => $user->name,
                    'email' => $user->email,
                    'role' => $user->role,
                    'status' => $user->status,
                    'lastLogin' => $user->last_login_at?->format('M j, g:i A') ?? 'Not yet',
                ] : null,
                'permissions' => $user ? ProphetOpsData::ROLE_PERMISSIONS[$user->role] ?? [] : [],
                'rolePermissions' => ProphetOpsData::ROLE_PERMISSIONS,
            ],
            'flash' => [
                'notice' => fn () => $request->session()->get('notice'),
            ],
        ];
    }
}
