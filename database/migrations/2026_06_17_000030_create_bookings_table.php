<?php

use Illuminate\Database\Migrations\Migration;
use Illuminate\Database\Schema\Blueprint;
use Illuminate\Support\Facades\Schema;

return new class extends Migration
{
    public function up(): void
    {
        Schema::create('bookings', function (Blueprint $table): void {
            $table->id();
            $table->string('code')->unique();
            $table->date('booking_date');
            $table->unsignedInteger('passenger_count');
            $table->string('client');
            $table->foreignId('travel_package_id')->nullable()->constrained()->nullOnDelete();
            $table->string('package_name');
            $table->string('package_code')->nullable();
            $table->string('entry_type')->default('Package preset');
            $table->string('destination');
            $table->unsignedInteger('gross_revenue')->default(0);
            $table->string('payment_status')->default('Pending');
            $table->string('booking_status')->default('Pending');
            $table->string('staff_assigned')->nullable();
            $table->string('source')->default('Manual quotation');
            $table->text('notes')->nullable();
            $table->timestamps();
        });
    }

    public function down(): void
    {
        Schema::dropIfExists('bookings');
    }
};
