<?php

use Illuminate\Database\Migrations\Migration;
use Illuminate\Database\Schema\Blueprint;
use Illuminate\Support\Facades\Schema;

return new class extends Migration
{
    public function up(): void
    {
        Schema::table('travel_packages', function (Blueprint $table): void {
            $table->string('travel_type')->default('Leisure')->after('inclusions');
            $table->unsignedTinyInteger('supplier_reliability_score')->default(80)->after('travel_type');
            $table->unsignedTinyInteger('business_value_score')->default(75)->after('supplier_reliability_score');
            $table->unsignedTinyInteger('risk_score')->default(25)->after('business_value_score');
        });
    }

    public function down(): void
    {
        Schema::table('travel_packages', function (Blueprint $table): void {
            $table->dropColumn([
                'travel_type',
                'supplier_reliability_score',
                'business_value_score',
                'risk_score',
            ]);
        });
    }
};
