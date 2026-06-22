<?php

use Illuminate\Database\Migrations\Migration;
use Illuminate\Database\Schema\Blueprint;
use Illuminate\Support\Facades\Schema;

return new class extends Migration
{
    public function up(): void
    {
        Schema::create('travel_packages', function (Blueprint $table): void {
            $table->id();
            $table->string('code')->unique();
            $table->string('package_name');
            $table->string('destination');
            $table->string('duration')->nullable();
            $table->unsignedInteger('base_price')->default(0);
            $table->text('inclusions')->nullable();
            $table->unsignedInteger('available_slots')->default(0);
            $table->unsignedInteger('sold_count')->default(0);
            $table->unsignedInteger('reserved_count')->default(0);
            $table->string('status')->default('Normal');
            $table->date('last_updated_at')->nullable();
            $table->timestamps();
        });
    }

    public function down(): void
    {
        Schema::dropIfExists('travel_packages');
    }
};
