namespace ProphetOps.Api;

public interface IBusinessClock
{
    DateTimeOffset UtcNow { get; }
    DateOnly Today { get; }
    string TimeZoneId { get; }
}

public sealed class BusinessClock : IBusinessClock
{
    private readonly TimeProvider _timeProvider;
    private readonly TimeZoneInfo _timeZone;

    public BusinessClock(IConfiguration configuration, IHostEnvironment environment, TimeProvider timeProvider)
    {
        _timeProvider = timeProvider;
        TimeZoneId = configuration["Business:TimeZone"]
            ?? throw new InvalidOperationException("Business:TimeZone must be configured explicitly.");
        _timeZone = TimeZoneInfo.FindSystemTimeZoneById(TimeZoneId);
    }

    public DateTimeOffset UtcNow => _timeProvider.GetUtcNow();

    public DateOnly Today
    {
        get
        {
            var local = TimeZoneInfo.ConvertTime(UtcNow, _timeZone);
            return DateOnly.FromDateTime(local.DateTime);
        }
    }

    public string TimeZoneId { get; }
}
