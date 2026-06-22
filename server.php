<?php

if (function_exists('header_remove')) {
    header_remove('X-Powered-By');
}

$publicPath = getcwd();
$publicRoot = rtrim(str_replace('\\', '/', realpath($publicPath) ?: $publicPath), '/').'/';

$uri = urldecode(
    parse_url($_SERVER['REQUEST_URI'], PHP_URL_PATH) ?? '/'
);

$candidatePath = realpath($publicPath.$uri);
$candidatePathForCheck = $candidatePath ? str_replace('\\', '/', $candidatePath) : false;

if ($uri !== '/'
    && $candidatePathForCheck
    && str_starts_with($candidatePathForCheck, $publicRoot)
    && is_file($candidatePath)
) {
    if (function_exists('header_remove')) {
        header_remove('X-Powered-By');
    }

    $extension = strtolower(pathinfo($candidatePath, PATHINFO_EXTENSION));
    $contentTypes = [
        'css' => 'text/css; charset=UTF-8',
        'ico' => 'image/x-icon',
        'js' => 'application/javascript',
        'json' => 'application/json; charset=UTF-8',
        'map' => 'application/json; charset=UTF-8',
        'png' => 'image/png',
        'svg' => 'image/svg+xml',
        'txt' => 'text/plain; charset=UTF-8',
        'webp' => 'image/webp',
        'woff' => 'font/woff',
        'woff2' => 'font/woff2',
    ];

    header('X-Content-Type-Options: nosniff');
    header('Content-Type: '.($contentTypes[$extension] ?? 'application/octet-stream'));
    header('Content-Length: '.filesize($candidatePath));

    if (str_starts_with($uri, '/build/assets/')) {
        header('Cache-Control: public, max-age=31536000, immutable');
    }

    readfile($candidatePath);

    return true;
}

$formattedDateTime = date('D M j H:i:s Y');

$requestMethod = $_SERVER['REQUEST_METHOD'];
$remoteAddress = $_SERVER['REMOTE_ADDR'].':'.$_SERVER['REMOTE_PORT'];

file_put_contents('php://stdout', "[$formattedDateTime] $remoteAddress [$requestMethod] URI: $uri\n");

require_once $publicPath.'/index.php';
