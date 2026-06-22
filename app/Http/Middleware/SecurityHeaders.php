<?php

namespace App\Http\Middleware;

use Closure;
use Illuminate\Http\Request;
use Symfony\Component\HttpFoundation\Response;

class SecurityHeaders
{
    private const LOCAL_DEV_HTTP_SOURCES = [
        'http://localhost:5173',
        'http://localhost:5174',
        'http://localhost:5175',
        'http://127.0.0.1:5173',
        'http://127.0.0.1:5174',
        'http://127.0.0.1:5175',
    ];

    private const LOCAL_DEV_WEBSOCKET_SOURCES = [
        'ws://localhost:5173',
        'ws://localhost:5174',
        'ws://localhost:5175',
        'ws://127.0.0.1:5173',
        'ws://127.0.0.1:5174',
        'ws://127.0.0.1:5175',
    ];

    public function handle(Request $request, Closure $next): Response
    {
        $response = $next($request);

        if (function_exists('header_remove')) {
            header_remove('X-Powered-By');
        }

        $response->headers->set('X-Frame-Options', 'SAMEORIGIN');
        $response->headers->set('X-Content-Type-Options', 'nosniff');
        $response->headers->set('Content-Security-Policy', $this->contentSecurityPolicy());
        $response->headers->set('Referrer-Policy', 'strict-origin-when-cross-origin');
        $response->headers->set('Permissions-Policy', 'camera=(), microphone=(), geolocation=()');
        $response->headers->set('X-XSS-Protection', '0');
        $response->headers->set('Cache-Control', 'no-store, no-cache, must-revalidate, private');
        $response->headers->set('Pragma', 'no-cache');
        $response->headers->remove('X-Powered-By');

        return $response;
    }

    private function contentSecurityPolicy(): string
    {
        $localHttpSources = '';
        $localWebSocketSources = '';
        $localUnsafeEval = '';

        if ($this->usesViteDevServer()) {
            $localHttpSources = ' '.implode(' ', self::LOCAL_DEV_HTTP_SOURCES);
            $localWebSocketSources = ' '.implode(' ', self::LOCAL_DEV_WEBSOCKET_SOURCES);
            $localUnsafeEval = " 'unsafe-eval'";
        }

        return implode('; ', [
            "default-src 'self'",
            "script-src 'self'{$localUnsafeEval}{$localHttpSources}",
            "style-src 'self'{$localHttpSources}",
            "img-src 'self' data:",
            "font-src 'self' data:",
            "connect-src 'self'{$localHttpSources}{$localWebSocketSources}",
            "frame-ancestors 'self'",
            "base-uri 'self'",
            "form-action 'self'",
        ]);
    }

    private function usesViteDevServer(): bool
    {
        return ! app()->environment('production') && is_file(public_path('hot'));
    }
}
