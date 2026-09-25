using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using ProphetOps.Api;
using Xunit;

namespace ProphetOps.Api.Tests;

public class BusinessClockTests
{
    [Fact]
    public void Manila_business_day_rolls_forward_at_utc_1600()
    {
        var before = ClockAt("2026-08-31T15:59:59Z");
        var after = ClockAt("2026-08-31T16:00:00Z");

        Assert.Equal(new DateOnly(2026, 8, 31), before.Today);
        Assert.Equal(new DateOnly(2026, 9, 1), after.Today);
        Assert.Equal("Asia/Manila", after.TimeZoneId);
    }

    [Fact]
    public void UtcNow_remains_a_utc_instant()
    {
        var clock = ClockAt("2026-08-31T16:00:00Z");
        Assert.Equal(TimeSpan.Zero, clock.UtcNow.Offset);
        Assert.Equal(DateTimeOffset.Parse("2026-08-31T16:00:00Z"), clock.UtcNow);
    }

    private static BusinessClock ClockAt(string utc)
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["Business:TimeZone"] = "Asia/Manila" })
            .Build();
        return new BusinessClock(config, new TestEnvironment(), new FixedTimeProvider(DateTimeOffset.Parse(utc)));
    }

    private sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;
    }

    private sealed class TestEnvironment : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = "Testing";
        public string ApplicationName { get; set; } = "ProphetOps.Api.Tests";
        public string ContentRootPath { get; set; } = "";
        public IFileProvider ContentRootFileProvider { get; set; } = null!;
    }
}
