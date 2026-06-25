namespace Acmp.Shared.Application.Abstractions;

// Abstracts the system clock so time-dependent logic is testable.
public interface IClock
{
    DateTimeOffset UtcNow { get; }
}
