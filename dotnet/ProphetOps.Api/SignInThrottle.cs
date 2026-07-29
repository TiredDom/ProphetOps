using System.Collections.Concurrent;

namespace ProphetOps.Api;

/// Slows down repeated failed sign-ins. Counts against the email and the caller's address
/// separately, so neither can be used to lock out the other. Every lockout expires.
public sealed class SignInThrottle
{
    public const int EmailFreeAttempts = 5;
    public const int AddressFreeAttempts = 10;

    public static readonly TimeSpan FirstLockout = TimeSpan.FromSeconds(15);
    public static readonly TimeSpan LongestLockout = TimeSpan.FromMinutes(5);

    /// A quiet stretch this long clears the count, and bounds how long entries are kept.
    public static readonly TimeSpan ForgetAfter = TimeSpan.FromMinutes(15);

    private const int PruneAbove = 512;

    private readonly ConcurrentDictionary<string, Attempts> _records = new();
    private readonly TimeProvider _clock;

    public SignInThrottle(TimeProvider clock) => _clock = clock;

    public SignInThrottle() : this(TimeProvider.System) { }

    /// How long this caller must wait. Zero means the attempt may go ahead.
    public TimeSpan RetryAfter(string email, string? address)
    {
        var now = _clock.GetUtcNow();
        var wait = Remaining(EmailKey(email), now);

        if (!string.IsNullOrWhiteSpace(address))
        {
            var byAddress = Remaining(AddressKey(address), now);
            if (byAddress > wait) wait = byAddress;
        }

        return wait;
    }

    public void RecordFailure(string email, string? address)
    {
        var now = _clock.GetUtcNow();

        Bump(EmailKey(email), EmailFreeAttempts, now);
        if (!string.IsNullOrWhiteSpace(address)) Bump(AddressKey(address), AddressFreeAttempts, now);

        Prune(now);
    }

    /// Clearing the address as well as the email is safe: reaching this point took a working
    /// password, and every other account keeps its own count regardless.
    public void RecordSuccess(string email, string? address)
    {
        _records.TryRemove(EmailKey(email), out _);
        if (!string.IsNullOrWhiteSpace(address)) _records.TryRemove(AddressKey(address), out _);
    }

    private TimeSpan Remaining(string key, DateTimeOffset now)
    {
        if (!_records.TryGetValue(key, out var attempts)) return TimeSpan.Zero;

        var left = attempts.LockedUntil - now;
        return left > TimeSpan.Zero ? left : TimeSpan.Zero;
    }

    private void Bump(string key, int freeAttempts, DateTimeOffset now) =>
        _records.AddOrUpdate(
            key,
            _ => Advance(null, freeAttempts, now),
            (_, existing) => Advance(existing, freeAttempts, now));

    private static Attempts Advance(Attempts? existing, int freeAttempts, DateTimeOffset now)
    {
        var failures = existing is not null && now - existing.LastFailure < ForgetAfter
            ? existing.Failures + 1
            : 1;

        return new Attempts(failures, now + LockoutFor(failures, freeAttempts), now);
    }

    /// Doubling keeps an honest stumble cheap — six wrong tries costs fifteen seconds — while a
    /// sustained run reaches the ceiling quickly and stays there.
    private static TimeSpan LockoutFor(int failures, int freeAttempts)
    {
        var over = failures - freeAttempts;
        if (over <= 0) return TimeSpan.Zero;

        var seconds = FirstLockout.TotalSeconds * Math.Pow(2, over - 1);
        return TimeSpan.FromSeconds(Math.Min(seconds, LongestLockout.TotalSeconds));
    }

    /// Only entries that are both forgotten and unlocked go, and only by matching value, so a
    /// count that moved between the read and the removal is left alone.
    private void Prune(DateTimeOffset now)
    {
        if (_records.Count <= PruneAbove) return;

        foreach (var entry in _records)
        {
            if (now - entry.Value.LastFailure > ForgetAfter && entry.Value.LockedUntil <= now)
                _records.TryRemove(entry);
        }
    }

    private static string EmailKey(string email) => "email:" + email;

    private static string AddressKey(string address) => "address:" + address;

    private sealed record Attempts(int Failures, DateTimeOffset LockedUntil, DateTimeOffset LastFailure);
}
