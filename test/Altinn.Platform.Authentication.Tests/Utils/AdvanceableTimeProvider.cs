using System;

namespace Altinn.Platform.Authentication.Tests.Utils;

public class AdvanceableTimeProvider : TimeProvider
{
    private readonly TimeProvider _inner = TimeProvider.System;

    private TimeSpan _offset;

    public override DateTimeOffset GetUtcNow()
        => _inner.GetUtcNow() + _offset;

    public override long GetTimestamp()
        => _inner.GetTimestamp() + _offset.Ticks;

    public void Advance(TimeSpan offset)
        => _offset += offset;
}
