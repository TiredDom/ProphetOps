<?php

namespace Tests\Feature;

use Tests\TestCase;

class StaticAssetSecurityHeadersTest extends TestCase
{
    public function test_local_static_asset_server_sets_nosniff_header(): void
    {
        $serverPath = base_path('server.php');

        $this->assertFileExists($serverPath);
        $this->assertStringContainsString('X-Content-Type-Options: nosniff', file_get_contents($serverPath));
        $this->assertStringContainsString("header_remove('X-Powered-By')", file_get_contents($serverPath));
    }

    public function test_apache_static_asset_rules_set_nosniff_header(): void
    {
        $htaccess = file_get_contents(public_path('.htaccess'));

        $this->assertStringContainsString('Header always set X-Content-Type-Options "nosniff"', $htaccess);
    }
}
