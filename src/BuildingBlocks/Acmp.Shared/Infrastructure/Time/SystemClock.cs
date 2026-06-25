using Acmp.Shared.Application.Abstractions;

namespace Acmp.Shared.Infrastructure.Time;

public sealed class SystemClock : IClock
{
    public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
}
