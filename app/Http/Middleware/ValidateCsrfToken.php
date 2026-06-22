<?php

namespace App\Http\Middleware;

use Illuminate\Foundation\Http\Middleware\ValidateCsrfToken as FoundationValidateCsrfToken;
use Symfony\Component\HttpFoundation\Cookie;

class ValidateCsrfToken extends FoundationValidateCsrfToken
{
    protected function newCookie($request, $config): Cookie
    {
        return new Cookie(
            'XSRF-TOKEN',
            $request->session()->token(),
            $this->availableAt(60 * $config['lifetime']),
            $config['path'],
            $config['domain'],
            $config['secure'],
            true,
            false,
            $config['same_site'] ?? null,
            $config['partitioned'] ?? false
        );
    }
}
