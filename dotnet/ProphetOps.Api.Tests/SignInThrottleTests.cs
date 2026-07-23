using Xunit;

namespace ProphetOps.Api.Tests;

public class SignInThrottleTests
{
    private const string Owner = "owner@prophetops.local";
    private const string Staff = "staff@prophetops.local";
    private const string HerPc = "192.168.1.24";
    private const string HisPc = "192.168.1.25";

    private sealed class StoppedClock : TimeProvider
    {
        private DateTimeOffset _now = new(2026, 7, 19, 9, 0, 0, TimeSpan.Zero);

        public override DateTimeOffset GetUtcNow() => _now;

        public void Advance(TimeSpan by) => _now += by;
    }

    private static void Fail(SignInThrottle throttle, int times, string email, string? address = null)
    {
        for (var i = 0; i < times; i++) throttle.RecordFailure(email, address);
    }

    [Fact]
    public void A_handful_of_wrong_passwords_costs_nothing()
    {
        var throttle = new SignInThrottle(new StoppedClock());

        Fail(throttle, SignInThrottle.EmailFreeAttempts, Owner, HerPc);

        Assert.Equal(TimeSpan.Zero, throttle.RetryAfter(Owner, HerPc));
    }

    [Fact]
    public void The_attempt_past_the_allowance_starts_a_wait()
    {
        var throttle = new SignInThrottle(new StoppedClock());

        Fail(throttle, SignInThrottle.EmailFreeAttempts + 1, Owner, HerPc);

        Assert.Equal(SignInThrottle.FirstLockout, throttle.RetryAfter(Owner, HerPc));
    }

    [Fact]
    public void Sitting_out_the_wait_lets_the_next_attempt_through()
    {
        var clock = new StoppedClock();
        var throttle = new SignInThrottle(clock);

        Fail(throttle, SignInThrottle.EmailFreeAttempts + 1, Owner, HerPc);
        clock.Advance(SignInThrottle.FirstLockout);

        Assert.Equal(TimeSpan.Zero, throttle.RetryAfter(Owner, HerPc));
    }

    [Fact]
    public void Each_further_failure_makes_the_next_wait_longer()
    {
        var throttle = new SignInThrottle(new StoppedClock());

        Fail(throttle, SignInThrottle.EmailFreeAttempts + 1, Owner, HerPc);
        var first = throttle.RetryAfter(Owner, HerPc);

        throttle.RecordFailure(Owner, HerPc);
        var second = throttle.RetryAfter(Owner, HerPc);

        Assert.True(second > first, $"expected a longer wait than {first}, got {second}");
    }

    [Fact]
    public void The_wait_stops_growing_at_the_ceiling()
    {
        var throttle = new SignInThrottle(new StoppedClock());

        Fail(throttle, 40, Owner, HerPc);

        Assert.Equal(SignInThrottle.LongestLockout, throttle.RetryAfter(Owner, HerPc));
    }

    [Fact]
    public void A_successful_sign_in_forgets_the_failures()
    {
        var clock = new StoppedClock();
        var throttle = new SignInThrottle(clock);

        Fail(throttle, SignInThrottle.EmailFreeAttempts + 1, Owner, HerPc);
        clock.Advance(SignInThrottle.FirstLockout);
        throttle.RecordSuccess(Owner, HerPc);

        // The whole allowance is available again, which only holds if the count went back to zero
        // rather than merely running out its lockout.
        Fail(throttle, SignInThrottle.EmailFreeAttempts, Owner, HerPc);

        Assert.Equal(TimeSpan.Zero, throttle.RetryAfter(Owner, HerPc));
    }

    [Fact]
    public void A_quiet_stretch_forgets_the_failures()
    {
        var clock = new StoppedClock();
        var throttle = new SignInThrottle(clock);

        Fail(throttle, SignInThrottle.EmailFreeAttempts + 1, Owner, HerPc);
        clock.Advance(SignInThrottle.ForgetAfter + TimeSpan.FromMinutes(1));

        Fail(throttle, SignInThrottle.EmailFreeAttempts, Owner, HerPc);

        Assert.Equal(TimeSpan.Zero, throttle.RetryAfter(Owner, HerPc));
    }

    [Fact]
    public void One_locked_account_does_not_lock_the_others()
    {
        var throttle = new SignInThrottle(new StoppedClock());

        Fail(throttle, SignInThrottle.EmailFreeAttempts + 1, Owner);

        Assert.True(throttle.RetryAfter(Owner, null) > TimeSpan.Zero);
        Assert.Equal(TimeSpan.Zero, throttle.RetryAfter(Staff, null));
    }

    [Fact]
    public void Cycling_addresses_from_one_machine_still_runs_into_the_limit()
    {
        var throttle = new SignInThrottle(new StoppedClock());

        // A different email every time, so no single account ever reaches its own allowance.
        for (var i = 0; i <= SignInThrottle.AddressFreeAttempts; i++)
            throttle.RecordFailure($"guess{i}@renantina.ph", HerPc);

        Assert.True(throttle.RetryAfter("guess0@renantina.ph", HerPc) > TimeSpan.Zero);
        Assert.Equal(TimeSpan.Zero, throttle.RetryAfter("guess0@renantina.ph", null));
    }

    [Fact]
    public void A_locked_machine_does_not_lock_the_colleague_next_to_it()
    {
        var throttle = new SignInThrottle(new StoppedClock());

        for (var i = 0; i <= SignInThrottle.AddressFreeAttempts; i++)
            throttle.RecordFailure($"guess{i}@renantina.ph", HerPc);

        Assert.True(throttle.RetryAfter(Staff, HerPc) > TimeSpan.Zero);
        Assert.Equal(TimeSpan.Zero, throttle.RetryAfter(Staff, HisPc));
    }

    [Fact]
    public void The_email_and_the_machine_are_counted_separately()
    {
        var throttle = new SignInThrottle(new StoppedClock());

        Fail(throttle, SignInThrottle.EmailFreeAttempts + 1, Owner, HerPc);

        // The email is over its allowance; the machine, on the same failures, is not over its own.
        Assert.True(throttle.RetryAfter(Owner, null) > TimeSpan.Zero);
        Assert.Equal(TimeSpan.Zero, throttle.RetryAfter(Staff, HerPc));
    }

    [Fact]
    public void Failures_arriving_at_once_are_still_counted_one_by_one()
    {
        var throttle = new SignInThrottle(new StoppedClock());

        Parallel.For(0, SignInThrottle.EmailFreeAttempts, _ => throttle.RecordFailure(Owner, HerPc));
        Assert.Equal(TimeSpan.Zero, throttle.RetryAfter(Owner, HerPc));

        // A lost update would leave the allowance unspent and this last failure harmless.
        throttle.RecordFailure(Owner, HerPc);
        Assert.True(throttle.RetryAfter(Owner, HerPc) > TimeSpan.Zero);
    }
}
