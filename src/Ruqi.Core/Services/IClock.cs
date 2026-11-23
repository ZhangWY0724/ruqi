namespace Ruqi.Core.Services;

public interface IClock
{
    DateTime NowLocal { get; }
    DateTimeOffset UtcNow { get; }
    DateOnly Today => DateOnly.FromDateTime(NowLocal);
}

public sealed class SystemClock : IClock
{
    public DateTime NowLocal => DateTime.Now;
    public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
}
