using GeneradorAnexos.Application.Abstractions.Time;

namespace GeneradorAnexos.Application.Services;

public sealed class SystemClock : IClock
{
    public DateTime Now => DateTime.Now;

    public DateTime UtcNow => DateTime.UtcNow;
}
